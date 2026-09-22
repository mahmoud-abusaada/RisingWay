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
    private Transform tappedFor;
    private float tapAt;
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

    private bool ReviveStep()
    {
        if (reviveUnderway)
        {
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
        if (revived)
        {
            if (turnErrors.Count - turnsAtRevive >= TURNS_AFTER_REVIVE)
                Finish(TURNS_AFTER_REVIVE + " turns after the revive");
            return false;
        }
        Vector3 q = body.position;
        if (turnErrors.Count >= 3 && haveSegment && new Vector2(q.x - turnPoint.x, q.z - turnPoint.z).magnitude >= 4.5f)
        {
            Utility.chanceIsOn = true;
            Vector3 f = Forward(player.direction);
            body.position += new Vector3(f.z, 0, -f.x) * 6f + Vector3.down * 2f; // well clear of any track
            knockedOffAt = Time.time;
            reviveUnderway = true;
            Debug.Log(TAG + "REVIVE: knocked the ball off with a chance active, at speed " + player.speed.ToString("F1"));
            return true;
        }
        return false;
    }

    private void OneTapStep()
    {
        if (secondTapIn > 0 && --secondTapIn == 0)
            player.autoTurn();

        Vector3 p = body.position;
        Transform part = NearestTurnPart(p);
        if (part == null || part == tappedFor)
            return;
        Vector3 f = Forward(player.direction);
        float ahead = (part.position.x - p.x) * f.x + (part.position.z - p.z) * f.z;
        if (ahead > tapAt)
            return;
        tappedFor = part;
        player.autoTurn();
        // One step later: still on the turn part, so it tests "the same part never turns the
        // ball twice". A tap after the ball has left the part is a late tap, which rightly
        // turns it off the track.
        secondTapIn = 1;
        tapAt = UnityEngine.Random.Range(-0.2f, 0.2f); // the next tap a little early or late
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
        reviveTest = Arg("-probeRevive") != null;
        oneTap = Arg("-probeOneTap") != null;
        boltRejoinTest = Arg("-probeBoltRejoin") != null;
        PathMaker.PreviewAllPatterns = Arg("-probeAllPatterns") != null;
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

        pickUps = FindAnyObjectByType<PickUpsManager>();

        lastDirection = player.direction;
        lastStepPos = body.position;
        startedAt = Time.time;
        running = true;
        Debug.Log(TAG + "run started at " + body.position + " radius=" + ballRadius + " interpolation=" + body.interpolation +
                  " collision=" + body.collisionDetectionMode);
    }

    private void FixedUpdate()
    {
        if (!running)
            return;

        if (fast)
        {
            // Held every step: picking up a real bolt and losing it would otherwise reset both.
            player.speed = Utility.Constants.TOP_PLAYER_SPEED;
            TimeScaleField.SetValue(pickUps, 3.5f);
        }

        if (reviveTest && ReviveStep())
            return;
        if (edgeTest && EdgeStep())
            return;
        if (oneTap && Utility.camFollowPlayer)
            OneTapStep();
        if (boltRejoinTest && Utility.camFollowPlayer)
            BoltRejoinStep();

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

    private static Transform NearestTurnPart(Vector3 p)
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
        return bestD < 4f ? best : null;
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
        Debug.Log(TAG + "parts that hurried to land before the ball: " + MoveDown.HurriedLandings);
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
