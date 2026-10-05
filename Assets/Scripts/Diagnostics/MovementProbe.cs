// Measures how the ball actually moves, so movement changes can be judged by numbers instead of
// by eye. READ-ONLY with respect to the game: it drives a run the way a player with auto-pilot
// would, and records what happens.
//
// Only active when the process was started with -movementProbe, which in practice means the
// batch-mode Editor run in Assets/Editor/MovementProbeRunner.cs. Options:
//   -probeFast      top speed (14) and the bolt time scale (3.5) for the whole run
//   -probeFps N     simulated render frame rate (default 60)
//   -probeTurns N   stop after this many turns (default 40)
//   -probeVerbose   log every hop, every unusual turn, and traces around them
//   -probeOneTap    taps instead of auto-pilot; with -probeTapSpread D (taps up to D either side of
//                   a turn part's centre), -probeManual (left/right controls), -probeBoltButton P
//   -probeEarly F   (with -probeOneTap) this share of the taps comes before the turn part, within
//                   the early-tap grace (PlayerMovement.EarlyTapSeconds, by mode); the ball must still turn
//   -probeTutorial  (with -probeOneTap) the run starts with the tutorial on, played by a beginner:
//                   taps too early, misses a turn altogether, then gets it (see TutorialStep)
//   -probeSpeed S / -probeSeed N / -probePatterns R-R,L-L / -probeHug D: see each option below
//
// What it reports, per run:
//   turn error    how far from the centre of the turn part the ball's new line is. This is what
//                 the trail shows as "not straight" after a turn.
//   drift         how much the ball wanders sideways while travelling one straight line
//   air           how high above the track surface the ball gets while it should be rolling
//   vy flips      how often vertical velocity reverses sign on consecutive physics steps
//   frame step    distance the rendered ball moves per frame (its cv includes speed changes)
//   frame jitter  how much that distance changes from one frame to the next: stutter
//
// Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class MovementProbe : MonoBehaviour
{
    private const string TAG = "[MP] ";
    private float ballRadius = 0.25f;

    private bool fast;
    private int fps = 60;
    private int turnsWanted = 40;
    private bool verbose;

    private PlayerMovement player;
    private Rigidbody body;
    private bool running;
    private bool originalAutoPilot;
    // -probeMode Chill|Standard|Insane: the run plays that mode (restored after).
    private RunMode originalMode;
    private float waitingSince = -1f;

    private Directions lastDirection;
    private bool haveSegment;
    private float laneCentre;          // cross-axis coordinate the current line should hold
    private float segMin, segMax;      // cross-axis extent seen on the current line
    private int segSteps;
    private readonly List<float> turnErrors = new List<float>();
    private readonly List<float> drifts = new List<float>();

    private int steps, groundedSteps, airSteps, vyFlips;
    private float maxAir;
    private float lastVy;
    private float speedRatioMin = float.MaxValue, speedRatioMax, speedRatioSum;
    private int speedRatioCount;
    private Vector3 lastStepPos;

    private readonly List<float> frameSteps = new List<float>();
    private Vector3 lastFramePos;
    private bool haveFramePos;

    private bool fell;
    private readonly Dictionary<string, float[]> byPiece = new Dictionary<string, float[]>(); // sum, count, min, flips, air
    private string pieceBelow = "none";
    private PickUpsManager pickUps;
    private MovementProbeCollisionTap tap;
    private static readonly FieldInfo TimeScaleField =
        typeof(PickUpsManager).GetField("myTimeScale", BindingFlags.NonPublic | BindingFlags.Instance);
    private float startedAt;
    private Vector3 turnPoint;

    // -probeEdge: a few units into a leg, push the ball sideways a little every step until it stops
    // being driven on the track, and report how far past the part's edge its centre was then.
    private bool edgeTest, edgePushing;
    private float edgeCentre, edgeHalfWidth;
    private const float EDGE_PUSH_PER_STEP = 0.01f;
    private static readonly FieldInfo DrivenField =
        typeof(PlayerMovement).GetField("drivenOnTrack", BindingFlags.NonPublic | BindingFlags.Static);

    // -probeRevive: a few turns in, give the ball a chance and knock it off the track. The revive
    // must put it back only on a path that has landed, and it must not fall again.
    private bool reviveTest, reviveUnderway, reviveFallSeen, revived;
    private int turnsAtRevive;
    private float knockedOffAt;
    private const int TURNS_AFTER_REVIVE = 15;

    // -probeOneTap: auto-pilot off; tap (autoTurn, the one-tap control) near each turn part's
    // centre, a little early or late like a player, and tap a second time straight after, which
    // must not turn the ball again.
    private bool oneTap;
    // The last few turn parts tapped for, each with where it stood: parts are pooled, and the same
    // object comes back later as a new turn somewhere else.
    private readonly Queue<KeyValuePair<Transform, Vector3>> tappedFor = new Queue<KeyValuePair<Transform, Vector3>>();

    private bool TappedFor(Transform part)
    {
        foreach (KeyValuePair<Transform, Vector3> t in tappedFor)
            if (t.Key == part && (t.Value - part.position).sqrMagnitude < 0.01f)
                return true;
        return false;
    }
    private float tapAt;
    // -probeEarly F: nextEarly is how far into the grace the next tap comes (0 at the part's near
    // edge, 1 at the grace's limit), or -1 for a tap on the part.
    private float earlyShare, nextEarly = -1f;
    private int earlyTaps;
    private int secondTapIn;

    // -probeBoltRejoin: every few turns, turn the ball off the path as a player reaching for a bolt
    // would, run sideways for a few steps, then do what picking the bolt up does
    // (PlayerMovement.rejoinPathAt). The ball must carry on along the path.
    private bool boltRejoinTest, pushingSideways;
    private int rejoins, turnsAtLastRejoin;
    private Vector3 sidewaysFrom;
    private Transform rejoinPart;
    // How far off the centre line the ball is when the bolt takes over. A bolt sits at its part's
    // centre and the ball collects it within about 0.7 of that, so it can be no further out.
    private const float SIDEWAYS_DISTANCE = 0.7f;

    // -probeHug D: on the curve out before each turn, move the ball D off the centre line towards
    // the side it is about to turn to - the side a U-turn (R-R, L-L) comes back on. With
    // -probePatterns R-R,L-L every approach is a U-turn.
    private float hug;
    private Transform huggedOn;
    private Directions hugDirection;
    private int hugs;

    private void HugStep()
    {
        Vector3 p = body.position;
        RaycastHit under;
        int notPlayer = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
        if (!Physics.Raycast(p, Vector3.down, out under, 2f, notPlayer, QueryTriggerInteraction.Ignore) ||
            under.collider.attachedRigidbody == null)
            return;
        Transform part = under.collider.attachedRigidbody.transform;
        if (!part.CompareTag("CurveSt") || part == huggedOn)
            return;
        Vector3 f = Forward(player.direction);
        Transform turn = null;
        foreach (string tag in new[] { "LandLeft", "LandRight" })
            foreach (GameObject go in GameObject.FindGameObjectsWithTag(tag))
            {
                Vector3 d = go.transform.position - part.position;
                float ahead = d.x * f.x + d.z * f.z;
                if (ahead > 1f && ahead < 4f && Mathf.Abs(d.y) < 2f &&
                    Mathf.Abs(CrossAxis(player.direction, d)) < 0.5f)
                    turn = go.transform;
            }
        if (turn == null)
            return;
        huggedOn = part;
        hugDirection = player.direction;
        Vector3 right = new Vector3(f.z, 0, -f.x);
        float side = turn.CompareTag("LandRight") ? 1f : -1f;
        SetCross(CrossAxis(player.direction, part.position) + side * CrossAxis(player.direction, right) * hug);
        hugs++;
        if (verbose)
            Debug.Log(TAG + "HUG " + hugs + ": " + hug + " towards the " + (side > 0 ? "right" : "left") + " before " + turn.tag);
    }

    private void BoltRejoinStep()
    {
        if (pushingSideways)
        {
            Vector3 moved = body.position - sidewaysFrom;
            if (new Vector2(moved.x, moved.z).magnitude < SIDEWAYS_DISTANCE)
                return;
            player.rejoinPathAt(rejoinPart);
            rejoins++;
            pushingSideways = false;
            turnsAtLastRejoin = turnErrors.Count;
            return;
        }
        Vector3 p = body.position;
        if (turnErrors.Count < 3 || turnErrors.Count - turnsAtLastRejoin < 3 || !haveSegment ||
            new Vector2(p.x - turnPoint.x, p.z - turnPoint.z).magnitude < 4.5f || NearestTurnPart(p) != null)
            return;
        RaycastHit under;
        int notPlayer = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
        if (!Physics.Raycast(p, Vector3.down, out under, 2f, notPlayer, QueryTriggerInteraction.Ignore) ||
            under.collider.attachedRigidbody == null)
            return;
        rejoinPart = under.collider.attachedRigidbody.transform;
        sidewaysFrom = p;
        player.turnRight(); // off the path, as if towards a bolt
        pushingSideways = true;
    }

    private static Vector3 Forward(Directions d)
    {
        switch (d)
        {
            case Directions.East: return Vector3.right;
            case Directions.South: return Vector3.back;
            case Directions.West: return Vector3.left;
            default: return Vector3.forward;
        }
    }

    private void SetCross(float value)
    {
        Vector3 p = body.position;
        if (player.direction == Directions.North || player.direction == Directions.South)
            p.x = value;
        else
            p.z = value;
        body.position = p;
    }

    private float PastTheEdge()
    {
        return Mathf.Abs(CrossAxis(player.direction, body.position) - edgeCentre) - edgeHalfWidth;
    }

    // True while the probe should not measure this step.
    private bool EdgeStep()
    {
        if (!edgePushing)
        {
            Vector3 p = body.position;
            if (!haveSegment || turnErrors.Count < 2 || new Vector2(p.x - turnPoint.x, p.z - turnPoint.z).magnitude < 3f)
                return false;
            RaycastHit under;
            int notPlayer = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
            if (!Physics.Raycast(p, Vector3.down, out under, 2f, notPlayer, QueryTriggerInteraction.Ignore))
                return false;
            Bounds b = under.collider.bounds;
            edgeCentre = CrossAxis(player.direction, b.center);
            edgeHalfWidth = CrossAxis(player.direction, b.extents);
            SetCross(edgeCentre + edgeHalfWidth - 0.1f);
            edgePushing = true;
            Debug.Log(TAG + "edge test on " + PartLabel(under.collider) + ": half width " + edgeHalfWidth.ToString("F3") +
                      ", pushing outwards " + EDGE_PUSH_PER_STEP + " per step");
            return true;
        }

        if (!(bool)DrivenField.GetValue(null))
        {
            Debug.Log(TAG + "EDGE: left the track with its centre " + PastTheEdge().ToString("F3") +
                      " past the part's edge (ball radius " + ballRadius + ")");
            edgeTest = false; // record the fall as usual from here
            return false;
        }
        if (player.direction != lastDirection)
        {
            Debug.Log(TAG + "EDGE: reached a turn still on the track, " + PastTheEdge().ToString("F3") + " past the edge; trying the next leg");
            edgePushing = false;
            return false;
        }
        SetCross(CrossAxis(player.direction, body.position) +
                 Mathf.Sign(CrossAxis(player.direction, body.position) - edgeCentre) * EDGE_PUSH_PER_STEP);
        return true;
    }

    private bool fallAtUTurn;
    private int uTurnFalls;
    private static readonly FieldInfo NotGroundField =
        typeof(PathMaker).GetField("notGroundUntil", BindingFlags.NonPublic | BindingFlags.Instance);

    // Between the two turns of a U-turn: a part is being kept from the ball until the second.
    private bool UTurnPending()
    {
        object d = NotGroundField != null ? NotGroundField.GetValue(FindAnyObjectByType<PathMaker>()) : null;
        return d is System.Collections.ICollection c && c.Count > 0 && turnErrors.Count > 0;
    }

    // -probeEndRun: at Chill's revive, press the End run button as a finger would - what the
    // event system finds at that point, top first, gets the press.
    private void PressEndRun()
    {
        GameObject button = GameObject.Find("EndRun");
        UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
        if (button == null || events == null)
        {
            Debug.LogWarning(TAG + "END RUN: no button (" + (button != null) + ") or no event system (" + (events != null) + ")");
            return;
        }
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 at = RectTransformUtility.WorldToScreenPoint(eventCam, button.transform.position);
        UnityEngine.EventSystems.PointerEventData pointer = new UnityEngine.EventSystems.PointerEventData(events) { position = at, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        List<UnityEngine.EventSystems.RaycastResult> results = new List<UnityEngine.EventSystems.RaycastResult>();
        events.RaycastAll(pointer, results);
        Debug.Log(TAG + "END RUN: at " + at + " (canvas " + (canvas != null ? canvas.name + " order " + canvas.sortingOrder + " mode " + canvas.renderMode + " cam " + (eventCam != null ? eventCam.name : "none") : "none") + "), hits: " + results.Count);
        {
            UnityEngine.UI.Image img = button.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.GraphicRaycaster gr = button.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            var reg = UnityEngine.UI.GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas);
            Debug.Log(TAG + "END RUN: image depth " + img.depth + " cull " + img.canvasRenderer.cull + " raycastTarget " + img.raycastTarget +
                      " alpha " + img.color.a + " canvas enabled " + canvas.enabled + " raycaster " + (gr != null && gr.isActiveAndEnabled) +
                      " registered " + (reg != null ? reg.Count : -1) + " contains " + RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, at, eventCam) +
                      " graphic canvas " + (img.canvas != null ? img.canvas.name : "none") + " groups:");
            for (Transform t = button.transform; t != null; t = t.parent)
                foreach (CanvasGroup g in t.GetComponents<CanvasGroup>())
                    Debug.Log(TAG + "END RUN:   group on " + t.name + " alpha " + g.alpha + " blocks " + g.blocksRaycasts + " interactable " + g.interactable + " ignoreParent " + g.ignoreParentGroups);
        }
        foreach (UnityEngine.EventSystems.RaycastResult r in results)
            Debug.Log(TAG + "END RUN:   " + r.gameObject.name + " (" + r.module.GetType().Name + " on " + r.module.name + ", depth " + r.depth + ", order " + r.sortingOrder + ")");
        if (results.Count == 0)
            return;
        GameObject top = results[0].gameObject;
        pointer.pointerCurrentRaycast = results[0];
        pointer.pointerPressRaycast = results[0];
        GameObject down = UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(top, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
        GameObject click = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(top);
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(top, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
        if (click != null)
            UnityEngine.EventSystems.ExecuteEvents.Execute(click, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        StartCoroutine(AfterEndRun());
        Debug.Log(TAG + "END RUN: pressed " + top.name + " (down on " + (down != null ? down.name : "nothing") + ", click on " + (click != null ? click.name : "nothing") + "); waiting now " + PlayerMovement.WaitingForTap + ", game started " + Utility.gameStarted);
    }

    private IEnumerator AfterEndRun()
    {
        yield return new WaitForSecondsRealtime(2f);
        GameOverMenu over = FindAnyObjectByType<GameOverMenu>(FindObjectsInactive.Include);
        CanvasGroup g = over != null ? over.GetComponent<CanvasGroup>() : null;
        Debug.Log(TAG + "END RUN: 2 s later - game over menu active " + (over != null && over.gameObject.activeInHierarchy) +
                  " alpha " + (g != null ? g.alpha : -1f) + ", fading in " + UIFader.isFadingIn + ", game started " + Utility.gameStarted + ", waiting " + PlayerMovement.WaitingForTap);
        Finish("end run pressed");
    }

    private bool ReviveStep()
    {
        if (reviveUnderway)
        {
            // Chill's revive waits in the air for a tap: give it one a second after it starts waiting.
            if (PlayerMovement.WaitingForTap)
            {
                if (waitingSince < 0f)
                {
                    waitingSince = Time.unscaledTime;
                    Debug.Log(TAG + "REVIVE: Chill - the ball is waiting for a tap " + (Time.time - knockedOffAt).ToString("F2") + "s after the fall");
                }
                else if (Time.unscaledTime - waitingSince > 1f)
                {
                    waitingSince = -1f;
                    if (Arg("-probeEndRun") != null)
                    {
                        PressEndRun();
                        return true;
                    }
                    Debug.Log(TAG + "REVIVE: Chill - tapping to continue");
                    player.continueAfterChillRevive();
                }
                reviveFallSeen = true;
                return true;
            }
            if (!Utility.camFollowPlayer || Utility.spawningAfterChance)
            {
                reviveFallSeen = true;
                return true; // falling, flying back up, or waiting for the path to land
            }
            if (!reviveFallSeen)
            {
                if (Time.time - knockedOffAt > 5f)
                    Finish("the knock-off was never noticed as a fall");
                return true;
            }
            reviveUnderway = false;
            revived = true;
            turnsAtRevive = turnErrors.Count;
            PathMaker pm = FindAnyObjectByType<PathMaker>();
            Debug.Log(TAG + "REVIVE: back on the track " + (Time.time - knockedOffAt).ToString("F2") +
                      "s after being knocked off, speed " + player.speed.ToString("F1") + ", first " + pm.startBlocksCount +
                      " parts landed: " + pm.firstPartsHaveLanded(pm.startBlocksCount) +
                      ", old path still being cleared: " + pm.isDestroyingOldPath);
            haveSegment = false; // measure afresh from here
            lastDirection = player.direction;
            lastStepPos = body.position;
            return true;
        }
        // -probeFallAtUTurn: fall again and again, each time between the two turns of a U-turn - the
        // fall a player makes by missing the second turn. The part after that U-turn is then left
        // hidden from the ball (PathMaker.notGroundUntil) when the path is taken down.
        if (revived && fallAtUTurn)
        {
            revived = false;
            reviveFallSeen = false;
            Debug.Log(TAG + "REVIVE " + (++uTurnFalls) + " done; next fall at the next U-turn");
            return false;
        }
        if (revived)
        {
            if (turnErrors.Count - turnsAtRevive >= TURNS_AFTER_REVIVE)
                Finish(TURNS_AFTER_REVIVE + " turns after the revive");
            return false;
        }
        Vector3 q = body.position;
        bool knock = fallAtUTurn
            ? turnErrors.Count >= 3 && UTurnPending() && new Vector2(q.x - turnPoint.x, q.z - turnPoint.z).magnitude >= 0.6f
            : turnErrors.Count >= 3 && haveSegment && new Vector2(q.x - turnPoint.x, q.z - turnPoint.z).magnitude >= 4.5f;
        if (knock)
        {
            if (!GameMode.T.unlimitedRevives)
                Utility.chanceIsOn = true; // (Chill revives every fall by itself)
            Vector3 f = Forward(player.direction);
            body.position += new Vector3(f.z, 0, -f.x) * 6f + Vector3.down * 2f; // well clear of any track
            knockedOffAt = Time.time;
            reviveUnderway = true;
            Debug.Log(TAG + "REVIVE: knocked the ball off with a chance active, at speed " + player.speed.ToString("F1"));
            return true;
        }
        return false;
    }

    // -probeTapSpread D (with -probeOneTap): each tap lands anywhere from D before the turn part's
    // centre to D after it (default 0.2). A turn part reaches 1.25 each way, so 1.1 is a player
    // tapping as early and as late as the part allows - and the ball then rides the next parts
    // 1.1 off their centre line, along the edge. Any fall in such a run is the game's fault: every
    // tap was on the turn part. Also counted: a tap that turned the ball the wrong way for the
    // part, and a tap on a turn part that did not turn it at all.
    private float tapSpread = 0.2f;
    private int wrongTurns, lostTaps, taps, repeatTaps;
    private float boltButton;
    private int boltPresses;
    private static readonly FieldInfo TurnedOnField =
        typeof(PlayerMovement).GetField("partTurnedOn", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo GroundPartField =
        typeof(PlayerMovement).GetField("groundPart", BindingFlags.NonPublic | BindingFlags.Instance);
    // -probeSpeed S: hold this speed (the game's own time scale stays, unlike -probeFast).
    private float holdSpeed = -1f;

    // -probeManual (with -probeOneTap): the left/right and swipe controls instead of one-tap - each
    // turn is the player choosing the side, here always the side the part turns to.
    private bool manual;
    private bool lastTapLeft;

    // -probeTutorial: the tutorial, as a beginner plays it. Turns 1 and 2: waits for the ball to
    // stop, then taps. Turn 3: taps well before the turn (must be ignored), then at the centre.
    // Turn 4: does not tap (the ball must stop past the flag and wait), then taps. Turn 5: taps as
    // the ball comes onto the part. From turn 6: taps late, past the centre. The ball must never
    // fall, and the tutorial must end by itself.
    private bool tutorialTest, tutorialEnded;
    private int tutorialTurns, tutorialTaps;
    private Directions tutorialDirection;
    private float tutorialStoppedAt = -1f;
    private bool tutorialTappedEarly;
    private Transform tutorialTappedOn;

    private void TutorialStep()
    {
        // -probeIdle: nobody taps. The ball must stop at the first flag and stay there.
        if (Arg("-probeIdle") != null)
        {
            if (Time.time - startedAt > 25f)
            {
                Debug.Log(TAG + "TUTORIAL idle for 25s: stopped=" + Utility.stoppedForTutorials + " direction " + player.direction + " at " + body.position.ToString("F2"));
                Finish("idle tutorial");
            }
            return;
        }
        if (tutorialTurns == 0 && tutorialTaps == 0 && tutorialStoppedAt < 0f && tutorialTappedOn == null && tutorialDirection != player.direction)
            tutorialDirection = player.direction;
        if (player.direction != tutorialDirection)
        {
            tutorialTurns++;
            tutorialDirection = player.direction;
            tutorialTappedEarly = false;
            Debug.Log(TAG + "TUTORIAL turn " + tutorialTurns + " done at " + body.position.ToString("F2"));
        }
        Vector3 p = body.position;
        Transform part = NearestTurnPart(p, 25f);
        if (part == null)
            return;
        bool left = part.CompareTag("LandLeft");
        if (Utility.stoppedForTutorials)
        {
            if (tutorialStoppedAt < 0f)
            {
                tutorialStoppedAt = Time.time;
                Vector3 c = part.position - p;
                Debug.Log(TAG + "TUTORIAL ball stopped " + new Vector2(c.x, c.z).magnitude.ToString("F2") + " from the centre of " + part.tag);
            }
            if (Time.time - tutorialStoppedAt > 0.6f)
            {
                if (manual && tutorialTaps % 2 == 0)
                    player.manualTurn(!left); // the wrong side first: must be refused
                Tap(left);
                tutorialTaps++;
                tutorialStoppedAt = -1f;
            }
            return;
        }
        tutorialStoppedAt = -1f;
        Vector3 f = Forward(player.direction);
        float ahead = (part.position.x - p.x) * f.x + (part.position.z - p.z) * f.z;
        if (ahead < -1.3f || Mathf.Abs(CrossAxis(player.direction, part.position - p)) > 1.24f || part == tutorialTappedOn)
            return;
        float tapWhen;
        switch (tutorialTurns)
        {
            case 0: case 1: return;
            case 2:
                if (!tutorialTappedEarly && ahead < 3.4f && ahead > 1.5f)
                {
                    tutorialTappedEarly = true;
                    Tap(left); // far too early
                    Debug.Log(TAG + "TUTORIAL tapped " + ahead.ToString("F2") + " before the centre of the next turn");
                    if (player.direction != tutorialDirection)
                        Debug.LogWarning(TAG + "TUTORIAL: a tap " + ahead.ToString("F2") + " before the turn turned the ball");
                }
                tapWhen = 0f;
                break;
            case 3: return;
            case 4: tapWhen = 1.2f; break;
            default: tapWhen = -0.5f; break;
        }
        if (ahead > tapWhen)
            return;
        tutorialTappedOn = part;
        Tap(left);
        tutorialTaps++;
        Debug.Log(TAG + "TUTORIAL tapped " + ahead.ToString("F2") + " before the centre, at speed " + player.speed.ToString("F1"));
    }

    // Every bolt: how long it lasted, and whether one never ended (the S20, October 2026: a bolt
    // that stayed on for good). A bolt still on after BOLT_STUCK_SECONDS ends the run, with the
    // path maker's bolt counters.
    private const float BOLT_STUCK_SECONDS = 60f;
    private bool boltWasOn;
    private float boltSince;
    private int bolts;
    private float longestBolt;

    private static object PathField(PathMaker pm, string name)
    {
        FieldInfo f = typeof(PathMaker).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        return f != null ? f.GetValue(pm) : "?";
    }

    private void BoltWatch()
    {
        if (Utility.boltIsOn && !boltWasOn)
        {
            boltSince = Time.time;
            bolts++;
            PathMaker pm = FindAnyObjectByType<PathMaker>();
            Debug.Log(TAG + "BOLT " + bolts + " on at " + body.position.ToString("F1") + " stopping=" + PathField(pm, "isStoppingBolt") +
                      " withBolt=" + PathField(pm, "spawnedPartsWithBolt") + " straights=" + PathField(pm, "spawnedStraightForBoltCount"));
        }
        else if (!Utility.boltIsOn && boltWasOn)
        {
            float lasted = Time.time - boltSince;
            longestBolt = Mathf.Max(longestBolt, lasted);
            Debug.Log(TAG + "BOLT " + bolts + " off after " + lasted.ToString("F1") + "s");
        }
        boltWasOn = Utility.boltIsOn;
        if (Utility.boltIsOn && Time.time - boltSince > BOLT_STUCK_SECONDS)
        {
            PathMaker pm = FindAnyObjectByType<PathMaker>();
            Debug.LogWarning(TAG + "BOLT STUCK: on for " + (Time.time - boltSince).ToString("F0") + "s. stopping=" + PathField(pm, "isStoppingBolt") +
                             " withBolt=" + PathField(pm, "spawnedPartsWithBolt") + " straights=" + PathField(pm, "spawnedStraightForBoltCount") +
                             " distance=" + Utility.getBoltDistance());
            foreach (Transform part in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (part.name == Utility.Constants.BOLT_STRAIGHT_PART_NAME)
                    Debug.LogWarning(TAG + "  stop part at " + part.position.ToString("F1") + " active=" + part.gameObject.activeInHierarchy +
                                     " ball " + body.position.ToString("F1"));
            Finish("bolt stuck");
        }
    }

    private void Tap(bool left)
    {
        if (manual)
            player.manualTurn(left);
        else
            player.autoTurn();
    }

    private void OneTapStep()
    {
        if (secondTapIn > 0 && --secondTapIn == 0)
            Tap(lastTapLeft);

        Vector3 p = body.position;
        Transform part = NearestTurnPart(p, earlyShare > 0 ? 16f : 4f);
        if (part == null || TappedFor(part))
            return;
        Vector3 f = Forward(player.direction);
        // Only a turn part on the ball's own line: off centre the nearest one can be in the lane
        // alongside, and a tap for that one comes far too early.
        if (huggedOn != null && hugDirection == player.direction
                ? Mathf.Abs(CrossAxis(player.direction, part.position - huggedOn.position)) > 0.5f
                : Mathf.Abs(CrossAxis(player.direction, part.position - p)) > 1.24f)
            return;
        float ahead = (part.position.x - p.x) * f.x + (part.position.z - p.z) * f.z;
        float tapWhen = tapAt;
        if (nextEarly >= 0f)
        {
            float seconds = PlayerMovement.EarlyTapSeconds;
            float reach = Mathf.Min(player.speed * Time.timeScale * seconds, 2.4f); // EARLY_TAP_MAX_DISTANCE
            // Inside the grace by a step at its far end: the tap lands up to a step after this.
            tapWhen = 1.25f + 0.02f + nextEarly * Mathf.Max(0f, reach - 0.04f);
        }
        if (ahead > tapWhen)
            return;
        if (ahead < -1.3f)
            return; // behind the ball: a part it has left, already falling away
        tappedFor.Enqueue(new KeyValuePair<Transform, Vector3>(part, part.position));
        if (tappedFor.Count > 4)
            tappedFor.Dequeue();
        // -probeBoltButton P: now and then press the bolt button here instead of tapping - on the
        // turn part, before the turn. The game must then make this turn itself.
        if (boltButton > 0f && UnityEngine.Random.value < boltButton)
        {
            pickUps.activateBolt();
            boltPresses++;
            return;
        }
        Directions before = player.direction;
        object under = GroundPartField != null ? GroundPartField.GetValue(player) : null;
        bool turnedHere = under != null && TurnedOnField != null && ReferenceEquals(TurnedOnField.GetValue(player), under);
        lastTapLeft = part.CompareTag("LandLeft");
        Tap(lastTapLeft);
        taps++;
        // Before the part, or on its first 0.3 (EARLY_TAP_INSET): the game keeps the tap and turns
        // the ball 0.3 into the part.
        bool early = PlayerMovement.EarlyTapSeconds > 0f && ahead > 0.95f;
        nextEarly = UnityEngine.Random.value < earlyShare ? UnityEngine.Random.value : -1f;
        if (early)
        {
            // Kept, not turned yet: the turn comes once the ball is on the part. A fall is the failure.
            earlyTaps++;
            secondTapIn = 0;
            tapAt = UnityEngine.Random.Range(-Mathf.Max(0f, tapSpread - player.speed * Time.fixedDeltaTime), tapSpread);
            return;
        }
        if (turnedHere)
        {
            repeatTaps++; // the ball had turned on this part already: rightly ignored
            return;
        }
        Directions expected = (Directions)(((int)before + (part.CompareTag("LandRight") ? 90 : 270)) % 360);
        if (player.direction == before)
        {
            lostTaps++;
            Debug.LogWarning(TAG + "TAP LOST: no turn on " + part.tag + " at " + part.position.ToString("F2") + ", ball " + p.ToString("F2") +
                             " heading " + before + ", " + ahead.ToString("F2") + " before its centre");
        }
        else if (player.direction != expected)
        {
            wrongTurns++;
            Debug.LogWarning(TAG + "WRONG TURN: " + before + " -> " + player.direction + " on " + part.tag + " at " + part.position.ToString("F2") +
                             ", ball " + p.ToString("F2") + " (expected " + expected + ")");
        }
        // One step later: still on the turn part, so it tests "the same part never turns the
        // ball twice". A tap after the ball has left the part is a late tap, which rightly
        // turns it off the track.
        // Not off the centre line (-probeHug, a wide -probeTapSpread): from the lane's edge the
        // ball is on the next turn part a step after turning, and the second tap would be a real
        // turn there.
        secondTapIn = hug > 0 || tapSpread > 0.3f ? 0 : 1;
        // The next tap early or late. It lands up to one physics step after the point chosen, so
        // the late end is pulled in by a step: a tap past the part's end is the player's miss.
        float step = player.speed * Time.fixedDeltaTime;
        tapAt = UnityEngine.Random.Range(-Mathf.Max(0f, tapSpread - step), tapSpread);
    }

    private static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name)
                return i + 1 < args.Length ? args[i + 1] : "";
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Arg("-movementProbe") == null)
            return;
        GameObject go = new GameObject("~MovementProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<MovementProbe>();
    }

    private IEnumerator Start()
    {
        fast = Arg("-probeFast") != null;
        verbose = Arg("-probeVerbose") != null;
        // Through reflection so the probe still compiles against an older PlayerMovement when
        // measuring the two side by side.
        FieldInfo debugField = typeof(PlayerMovement).GetField("DebugMovement", BindingFlags.Public | BindingFlags.Static);
        if (debugField != null)
            debugField.SetValue(null, verbose);
        int.TryParse(Arg("-probeFps") ?? "60", out fps);
        int.TryParse(Arg("-probeTurns") ?? "40", out turnsWanted);
        Time.captureDeltaTime = 1f / fps;

        edgeTest = Arg("-probeEdge") != null;
        reviveTest = Arg("-probeRevive") != null || Arg("-probeFallAtUTurn") != null;
        fallAtUTurn = Arg("-probeFallAtUTurn") != null;
        oneTap = Arg("-probeOneTap") != null;
        boltRejoinTest = Arg("-probeBoltRejoin") != null;
        PathMaker.PreviewAllPatterns = Arg("-probeAllPatterns") != null;
        string forced = Arg("-probePatterns");
        PathMaker.ForcedPatterns = string.IsNullOrEmpty(forced) ? null : forced.Split(',');
        float.TryParse(Arg("-probeHug") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out hug);
        float.TryParse(Arg("-probeTapSpread") ?? "0.2", NumberStyles.Float, CultureInfo.InvariantCulture, out tapSpread);
        float.TryParse(Arg("-probeEarly") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out earlyShare);
        float.TryParse(Arg("-probeSpeed") ?? "-1", NumberStyles.Float, CultureInfo.InvariantCulture, out holdSpeed);
        manual = Arg("-probeManual") != null;
        float.TryParse(Arg("-probeBoltButton") ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out boltButton);
        int seed;
        if (int.TryParse(Arg("-probeSeed") ?? "", out seed))
            UnityEngine.Random.InitState(seed); // the same path and taps again
        tutorialTest = Arg("-probeTutorial") != null;
        if (tutorialTest)
            PlayerStats.Instance.setTutorialsState(true);
        originalMode = PlayerStats.Instance.getRunMode();
        if (System.Enum.TryParse(Arg("-probeMode") ?? "", out RunMode probeMode))
        {
            PlayerStats.Instance.setRunMode(probeMode);
            GameMode.IgnoreLocks = true; // a test save may not have Insane open
        }
        originalAutoPilot = PlayerStats.Instance.isAutoPilotOn();
        PlayerStats.Instance.setAutoPilotState(!oneTap);

        Debug.Log(TAG + "probe fast=" + fast + " fps=" + fps + " turns=" + turnsWanted +
                  " edge=" + edgeTest + " revive=" + reviveTest + " oneTap=" + oneTap +
                  " fixedDt=" + Time.fixedDeltaTime + " tutorials=" + PlayerStats.Instance.isTutorialsOn());

        // Let every Awake/Start in the scene finish and the menu settle. In the Editor the Google
        // UMP placeholder shows its own consent form and sets Time.timeScale to 0 until it is
        // answered, which would freeze the run, so press its only button.
        for (int i = 0; i < 90; i++)
        {
            foreach (UnityEngine.UI.Button b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                if (b.transform.root.name.StartsWith("ConsentForm"))
                {
                    Debug.Log(TAG + "answering the Editor placeholder consent form");
                    b.onClick.Invoke();
                    break;
                }
            }
            yield return null;
        }
        if (Time.timeScale == 0f)
            Debug.LogWarning(TAG + "time scale is still 0 - the run will not start");

        player = FindAnyObjectByType<PlayerMovement>();
        body = player.GetComponent<Rigidbody>();
        SphereCollider sphere = player.GetComponent<SphereCollider>();
        Vector3 s = player.transform.lossyScale;
        ballRadius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        tap = player.gameObject.AddComponent<MovementProbeCollisionTap>();
        FindAnyObjectByType<MenusOperations>().StartGame();

        while (!Utility.gameStarted)
            yield return null;
        GameMode.Tuning mt = GameMode.T;
        Debug.Log(TAG + "mode " + GameMode.Current + ": speed " + mt.startSpeed + " to " + mt.topSpeed + " over " + mt.rampScore +
                  ", early tap " + PlayerMovement.EarlyTapSeconds + "s, patterns " + mt.startPatternTier + ".." + mt.maxPatternTier +
                  ", unlimited revives " + mt.unlimitedRevives);

        pickUps = FindAnyObjectByType<PickUpsManager>();

        lastDirection = player.direction;
        lastStepPos = body.position;
        startedAt = Time.time;
        running = true;
        FieldInfo moveRadius = typeof(PlayerMovement).GetField("ballRadius", BindingFlags.NonPublic | BindingFlags.Instance);
        Debug.Log(TAG + "run started at " + body.position + " radius=" + ballRadius + " (PlayerMovement's: " +
                  (moveRadius != null ? moveRadius.GetValue(player) : "?") + ", rendered " +
                  player.GetComponent<Renderer>().bounds.extents.x.ToString("F3") + ", lossyScale " + player.transform.lossyScale.ToString("F3") +
                  ", maxAngularVelocity " + body.maxAngularVelocity + ") interpolation=" + body.interpolation +
                  " collision=" + body.collisionDetectionMode);
    }

    private void FixedUpdate()
    {
        if (!running)
            return;

        if (holdSpeed > 0)
            player.speed = holdSpeed;

        if (fast)
        {
            // Held every step: picking up a real bolt and losing it would otherwise reset both.
            player.speed = GameMode.T.topSpeed; // the mode's own top speed
            TimeScaleField.SetValue(pickUps, 3.5f);
        }

        BoltWatch();
        if (reviveTest && ReviveStep())
            return;
        if (edgeTest && EdgeStep())
            return;
        // Not during a bolt: the game turns the ball itself then and takes no taps (InputManager).
        if (tutorialTest && !tutorialEnded && !PlayerStats.Instance.isTutorialsOn())
        {
            tutorialEnded = true;
            Debug.Log(TAG + "TUTORIAL ended by itself after " + tutorialTurns + " turns and " + tutorialTaps + " taps, " +
                      (Time.time - startedAt).ToString("F1") + "s in");
        }
        if (tutorialTest && !tutorialEnded)
        {
            if (Utility.camFollowPlayer)
                TutorialStep();
        }
        else if (oneTap && Utility.camFollowPlayer && !Utility.boltIsOn)
            OneTapStep();
        if (boltRejoinTest && Utility.camFollowPlayer)
            BoltRejoinStep();
        if (hug > 0 && Utility.camFollowPlayer)
            HugStep();

        if (!Utility.camFollowPlayer)
        {
            fell = true;
            ReportSurroundings(body.position);
            Finish("ball left the track");
            return;
        }

        Vector3 p = body.position;
        steps++;
        if (verbose)
            TraceStep(p);

        // Speed actually achieved over the last step, against what move() asked for.
        Vector3 d = p - lastStepPos;
        // From the position change: it is what the ball actually did this step.
        float stepVy = steps > 1 ? d.y / Time.fixedDeltaTime : 0f;
        float horizontal = new Vector2(d.x, d.z).magnitude;
        float wanted = player.speed * Time.fixedDeltaTime;
        if (wanted > 0.0001f && steps > 5 && player.direction == lastDirection)
        {
            float r = horizontal / wanted;
            speedRatioMin = Mathf.Min(speedRatioMin, r);
            speedRatioMax = Mathf.Max(speedRatioMax, r);
            speedRatioSum += r;
            speedRatioCount++;
            Piece()[0] += r;
            Piece()[1] += 1;
            Piece()[2] = Mathf.Min(Piece()[2], r);
        }
        lastStepPos = p;

        // Height above the track: how far the ball could drop before touching anything, found with
        // a sphere cast of the ball's own size so ramp starts and crests are measured correctly.
        RaycastHit hit;
        int notPlayer = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
        if (Physics.SphereCast(p + Vector3.up * 0.5f, ballRadius, Vector3.down, out hit, 3.5f, notPlayer, QueryTriggerInteraction.Ignore))
        {
            Transform root = hit.collider.transform.parent != null ? hit.collider.transform.parent : hit.collider.transform;
            pieceBelow = root.tag;
            float air = hit.distance - 0.5f;
            if (air > 0.03f)
            {
                if (verbose && airSteps < 40)
                {
                    DumpTrace();
                    Rigidbody hb = hit.collider.attachedRigidbody;
                    MoveDown md = root.GetComponent<MoveDown>();
                    Debug.Log(TAG + "AIR " + air.ToString("F3") + " step " + steps + " on " + root.tag + "/" + hit.collider.name +
                              " '" + root.name + "' partKinematic=" + (hb != null && hb.isKinematic) + " partVel=" + (hb != null ? hb.linearVelocity.y.ToString("F2") : "-") +
                              " moveDown=" + (md != null) + " vy=" + stepVy.ToString("F2") + " pos=" + p.ToString("F3") +
                              " dir=" + player.direction + " normal=" + hit.normal.ToString("F2"));
                }
                airSteps++;
                Piece()[4] += 1;
                maxAir = Mathf.Max(maxAir, air);
            }
            else
            {
                groundedSteps++;
            }
        }

        float vy = stepVy;
        if (steps > 5 && Mathf.Abs(vy) > 0.5f && Mathf.Abs(lastVy) > 0.5f && Mathf.Sign(vy) != Mathf.Sign(lastVy))
        {
            vyFlips++;
            Piece()[3] += 1;
        }
        lastVy = vy;

        if (player.direction != lastDirection)
        {
            CloseSegment();
            Transform part = NearestTurnPart(p);
            if (part != null)
            {
                // The new line should run through the part's centre. Its cross-axis coordinate
                // for the new direction is the part's coordinate on the OLD travel axis.
                laneCentre = CrossAxis(player.direction, part.position);
                turnPoint = part.position;
                haveSegment = true;
                segMin = float.MaxValue;
                segMax = float.MinValue;
                segSteps = 0;
            }
            lastDirection = player.direction;

            if (turnErrors.Count + (haveSegment ? 1 : 0) > turnsWanted)
                Finish("turn target reached");
        }
        else if (haveSegment)
        {
            // Skip the first few steps after a turn: the turn step itself is still moving on the
            // old axis in the current code, and that is measured by the offset, not the drift.
            segSteps++;
            if (segSteps > 3)
            {
                float c = CrossAxis(player.direction, p) - laneCentre;
                segMin = Mathf.Min(segMin, c);
                segMax = Mathf.Max(segMax, c);
            }
        }

        if (Time.time - startedAt > 600f)
            Finish("time limit");

        if (verbose && steps % 300 == 0)
            Debug.Log(TAG + "path: " + PathAhead(p) + " | " + TrackAhead(p));
    }

    // Verbose mode: keep the last few steps and print them around the first air events, with what
    // the ground looks like from several heights (a sphere cast ignores colliders it starts inside).
    private readonly Queue<string> trace = new Queue<string>();
    private int traceAfter;
    private int tracedEvents;

    private void TraceStep(Vector3 p)
    {
        int mask = Physics.DefaultRaycastLayers & ~(1 << player.gameObject.layer);
        string line = "step " + steps + " pos=" + p.ToString("F3") + " vy=" + ((p.y - lastStepPos.y) / Time.fixedDeltaTime).ToString("F2");
        foreach (float lift in new[] { 0.05f, 0.5f, 2f })
        {
            RaycastHit h;
            bool ok = Physics.SphereCast(p + Vector3.up * lift, ballRadius, Vector3.down, out h, lift + 3f, mask, QueryTriggerInteraction.Ignore);
            line += "  sc" + lift + "=" + (ok ? (h.distance - lift).ToString("F3") + "@" + PartLabel(h.collider) : "none");
        }
        RaycastHit r;
        if (Physics.Raycast(p, Vector3.down, out r, 3f, mask, QueryTriggerInteraction.Ignore))
            line += "  ray=" + (r.distance - ballRadius / r.normal.y).ToString("F3") + " n=" + r.normal.ToString("F2");
        trace.Enqueue(line);
        if (trace.Count > 6)
            trace.Dequeue();
        if (traceAfter > 0)
        {
            Debug.Log(TAG + "  +" + line);
            traceAfter--;
        }
    }

    private static string PartLabel(Collider c)
    {
        Transform t = c.transform.parent != null ? c.transform.parent : c.transform;
        return t.name.Replace("(Clone)", "") + "#" + t.GetHashCode();
    }

    private void DumpTrace()
    {
        if (tracedEvents >= 4)
            return;
        tracedEvents++;
        Debug.Log(TAG + "---- trace around air event " + tracedEvents + " ----");
        foreach (string l in trace)
            Debug.Log(TAG + "   " + l);
        traceAfter = 4;
    }

    private float[] Piece()
    {
        float[] v;
        if (!byPiece.TryGetValue(pieceBelow, out v))
        {
            v = new float[] { 0, 0, float.MaxValue, 0, 0 };
            byPiece[pieceBelow] = v;
        }
        return v;
    }

    private void LateUpdate()
    {
        if (!running)
            return;
        Vector3 p = player.transform.position;
        if (verbose && frameSteps.Count < 12)
            Debug.Log(TAG + "frame " + frameSteps.Count + " transform=" + p.ToString("F3") + " body=" + body.position.ToString("F3") +
                      " kinematic=" + body.isKinematic + " interp=" + body.interpolation + " follow=" + Utility.camFollowPlayer);
        if (haveFramePos)
            frameSteps.Add(new Vector2(p.x - lastFramePos.x, p.z - lastFramePos.z).magnitude);
        lastFramePos = p;
        haveFramePos = true;
    }

    private void CloseSegment()
    {
        if (!haveSegment || segSteps <= 3 || segMin == float.MaxValue)
            return;
        // The line's offset is where it settled; its drift is how much it moved while travelling.
        float offset = Mathf.Abs((segMin + segMax) * 0.5f);
        if (verbose && offset > 0.02f)
            Debug.Log(TAG + "OFFSET " + offset.ToString("F3") + " on the line after turn " + (turnErrors.Count + 1) + " step " + steps);
        turnErrors.Add(offset);
        drifts.Add(segMax - segMin);
    }

    private static float CrossAxis(Directions dir, Vector3 v)
    {
        return (dir == Directions.North || dir == Directions.South) ? v.x : v.z;
    }

    private static Transform NearestTurnPart(Vector3 p, float within = 4f)
    {
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (string tag in new[] { "LandLeft", "LandRight" })
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag(tag))
            {
                Vector3 q = go.transform.position;
                // Only parts at the ball's own level: the path spirals upwards, so a turn part
                // of a later lap can be right above the ball.
                if (Mathf.Abs(q.y - p.y) > 2f)
                    continue;
                float dd = (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z);
                if (dd < bestD)
                {
                    bestD = dd;
                    best = go.transform;
                }
            }
        }
        return bestD < within ? best : null;
    }

    // What the track looks like where the ball fell: a part still dropping into place and a part
    // that was destroyed under the ball look the same from the ball's point of view.
    // Where the ball is in the path's own order: how many parts after the one it is standing on
    // are placed, and how many of those are still dropping into position.
    private static System.Reflection.FieldInfo pathParentField;

    private string PathAhead(Vector3 p)
    {
        if (pathParentField == null)
            pathParentField = typeof(PathMaker).GetField("currentGamePathParent", BindingFlags.NonPublic | BindingFlags.Instance);
        PathMaker maker = FindAnyObjectByType<PathMaker>();
        Transform parent = pathParentField != null ? pathParentField.GetValue(maker) as Transform : null;
        if (parent == null)
            return "path parent not found";

        int nearest = -1;
        float nearestD = float.MaxValue;
        for (int i = 0; i < parent.childCount; i++)
        {
            Vector3 d = parent.GetChild(i).position - p;
            float flat = new Vector2(d.x, d.z).magnitude + Mathf.Abs(d.y);
            if (flat < nearestD)
            {
                nearestD = flat;
                nearest = i;
            }
        }

        int placed = 0, dropping = 0;
        for (int i = nearest + 1; i < parent.childCount; i++)
        {
            Transform part = parent.GetChild(i);
            if (part.name == Utility.Constants.DESTROYING_OBJECT_NAME)
                continue;
            if (part.GetComponent<MoveDown>() != null)
                dropping++;
            else
                placed++;
        }
        return "on part " + nearest + " of " + parent.childCount + "; after it: " + placed + " placed, " + dropping + " still dropping";
    }

    // The same question answered by distance instead of by path order: parts that are placed (not
    // dropping in, not being destroyed), and how far the farthest one is along the way the ball
    // is going.
    private string TrackAhead(Vector3 p)
    {
        Vector3 f = player.direction == Directions.North ? Vector3.forward
                  : player.direction == Directions.South ? Vector3.back
                  : player.direction == Directions.East ? Vector3.right : Vector3.left;
        int live = 0, dropping = 0, dying = 0;
        float farthest = 0;
        foreach (MeshCollider c in FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude))
        {
            Transform part = c.transform.parent != null ? c.transform.parent : c.transform;
            if (part.GetComponent<Destroyer>() == null && part.GetComponentInChildren<Destroyer>() == null)
                continue;
            Vector3 d = part.position - p;
            if (new Vector2(d.x, d.z).magnitude > 60f)
                continue;
            if (part.name == Utility.Constants.DESTROYING_OBJECT_NAME)
            {
                dying++;
                continue;
            }
            if (part.GetComponent<MoveDown>() != null)
            {
                dropping++;
                continue;
            }
            live++;
            float along = d.x * f.x + d.z * f.z;
            if (along > farthest)
                farthest = along;
        }
        return live + " placed, " + dropping + " still dropping, " + dying + " being destroyed; farthest placed part " +
               farthest.ToString("F1") + " ahead (" + (farthest / 2.5f).ToString("F1") + " parts)";
    }

    private void ReportSurroundings(Vector3 p)
    {
        Debug.Log(TAG + "at the fall - path: " + PathAhead(p) + " | " + TrackAhead(p));
        Debug.Log(TAG + "fell at " + p.ToString("F2") + " heading " + player.direction + ". Parts within 4 units:");
        foreach (MeshCollider c in FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude))
        {
            Transform part = c.transform.parent != null ? c.transform.parent : c.transform;
            Vector3 q = part.position;
            float flat = new Vector2(q.x - p.x, q.z - p.z).magnitude;
            if (flat > 4f)
                continue;
            Debug.Log(TAG + "   " + part.name + " [" + part.tag + "] at " + q.ToString("F2") +
                      "  dy=" + (q.y - p.y).ToString("F2") + "  flat=" + flat.ToString("F2") +
                      "  collider=" + c.enabled + "  stillDropping=" + (part.GetComponent<MoveDown>() != null) +
                      "  kinematic=" + (part.GetComponent<Rigidbody>() != null && part.GetComponent<Rigidbody>().isKinematic));
        }
    }

    private void Finish(string why)
    {
        if (!running)
            return;
        running = false;
        CloseSegment();

        CultureInfo ci = CultureInfo.InvariantCulture;
        Debug.Log(TAG + "==== RESULT (" + (fast ? "fast" : "normal") + ", " + fps + " fps) - " + why + " ====");
        Debug.Log(TAG + "turns measured: " + turnErrors.Count + "   fell: " + fell +
                  "   game time: " + (Time.time - startedAt).ToString("F1", ci) + "s");
        if (boltRejoinTest)
            Debug.Log(TAG + "bolt rejoin test: " + rejoins + " times off the path and back, fell: " + fell);
        if (oneTap)
            Debug.Log(TAG + (manual ? "left/right test: " : "one-tap test: ") + taps + " taps within " + tapSpread + " of the turn parts' centres, wrong turns: " + wrongTurns +
                      ", lost taps: " + lostTaps + ", repeat taps ignored: " + repeatTaps + ", early taps: " + earlyTaps +
                      ", bolt button presses on a turn part: " + boltPresses + ", fell: " + fell);
        if (tutorialTest)
            Debug.Log(TAG + "tutorial test: " + (tutorialEnded ? "ended by itself" : "DID NOT END") + " after " + tutorialTurns + " turns, fell: " + fell);
        if (hug > 0)
            Debug.Log(TAG + "hug test: " + hugs + " approaches " + hug + " off centre towards the turn, fell: " + fell);
        Debug.Log(TAG + "parts that hurried to land before the ball: " + MoveDown.HurriedLandings);
        Debug.Log(TAG + "bolts: " + bolts + ", longest " + longestBolt.ToString("F1") + "s" + (Utility.boltIsOn ? ", ONE STILL ON" : ""));
        if (reviveTest)
            Debug.Log(TAG + "revive test: " + (!revived ? "the revive never completed"
                : (fell ? "FELL AGAIN " : "no fall in ") + (turnErrors.Count - turnsAtRevive) + " turns after the revive"));
        Debug.Log(TAG + "turn error  " + Stats(turnErrors));
        Debug.Log(TAG + "line drift  " + Stats(drifts));
        Debug.Log(TAG + "air steps " + airSteps + "/" + steps + "   max air " + maxAir.ToString("F3", ci) +
                  "   vy sign flips " + vyFlips);
        if (speedRatioCount > 0)
            Debug.Log(TAG + "speed achieved / asked  mean " + (speedRatioSum / speedRatioCount).ToString("F3", ci) +
                      "  min " + speedRatioMin.ToString("F3", ci) + "  max " + speedRatioMax.ToString("F3", ci));
        foreach (KeyValuePair<string, float[]> kv in byPiece)
        {
            float[] v = kv.Value;
            if (v[1] > 0)
                Debug.Log(TAG + "  on " + kv.Key.PadRight(13) + " speed mean " + (v[0] / v[1]).ToString("F3", ci) +
                          "  min " + v[2].ToString("F3", ci) + "  steps " + v[1] + "  vy flips " + v[3] + "  air steps " + v[4]);
        }
        Debug.Log(TAG + "frame step  " + Stats(frameSteps) + "   (cv = stdev/mean; 0 is perfectly even)");
        Debug.Log(TAG + "frame jitter " + Jitter(frameSteps).ToString("F3", ci) +
                  "   (mean change between consecutive frame steps, relative to the mean step; 0 is smooth)");

        foreach (KeyValuePair<string, float[]> kv in tap.Hits)
            Debug.Log(TAG + "  hit " + kv.Key + "  x" + kv.Value[0] + "  avg impulse " +
                      (kv.Value[1] / kv.Value[0]).ToString("F2", ci));
        Destroy(tap);

        PlayerStats.Instance.setAutoPilotState(originalAutoPilot);
        PlayerStats.Instance.setRunMode(originalMode);
        PlayerPrefs.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(fell ? 3 : 0);
#endif
    }

    private static float Jitter(List<float> xs)
    {
        if (xs.Count < 2)
            return 0;
        float sum = 0, diff = 0;
        for (int i = 1; i < xs.Count; i++)
        {
            sum += xs[i];
            diff += Mathf.Abs(xs[i] - xs[i - 1]);
        }
        return sum > 0 ? diff / sum : 0;
    }

    private static string Stats(List<float> xs)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        if (xs.Count == 0)
            return "n=0";
        float sum = 0, max = 0, sq = 0;
        foreach (float x in xs)
        {
            sum += x;
            sq += x * x;
            max = Mathf.Max(max, x);
        }
        float mean = sum / xs.Count;
        float sd = Mathf.Sqrt(Mathf.Max(0, sq / xs.Count - mean * mean));
        List<float> sorted = new List<float>(xs);
        sorted.Sort();
        float p90 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.9f))];
        return "n=" + xs.Count + "  mean " + mean.ToString("F3", ci) + "  p90 " + p90.ToString("F3", ci) +
               "  max " + max.ToString("F3", ci) + "  cv " + (mean > 0 ? sd / mean : 0).ToString("F2", ci);
    }
}
#endif
