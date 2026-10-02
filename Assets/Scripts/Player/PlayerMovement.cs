using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    [SerializeField] public float speed = 1;
    [SerializeField] public Vector3 previewPosition = new Vector3(0, 4.025f, -8.2f);
    private Vector3 originalPosition;
    public Directions direction = Directions.North;
    private Rigidbody myRB;
    private CameraController cameraController;
    private PathMaker pathMaker;
    private PlayerTrigger playerTrigger;
    private PlayerStats playerStats;
    private MaterialsManager materialsManager;
    private MoveToPosition moveToPosition;
    private InGameUI inGameUi;
    private PickUpsManager pickUpsManager;
    private float startSpeed;
    public int timesRespawnedAfterChancePickedUp = 0;

    // ---------------------------------------------------------------------------------------
    // Movement model
    //
    // While the ball is on the track this script decides exactly where it goes every physics step:
    //   - the ball moves at exactly `speed` along the track: on flat parts that is all horizontal,
    //     on a ramp part of it goes into climbing (slopeFactor). It used to be `speed` across the
    //     ground on ramps too, which made the ball visibly faster going up - over 20% on the
    //     steepest part;
    //   - vertical position is the height at which the ball rests on the track at the end of the
    //     step, found with a sphere cast of the ball's own size, so ramps and the seams between
    //     parts no longer kick it. The old code zeroed vertical speed on alternate steps and left
    //     the contact solver to push the ball back out of each ramp: measured as 27-36% of the
    //     asked speed lost on ramps, and hops of up to half the ball's radius at top speed - the
    //     "jump";
    //   - to get there, the ball's contacts with the track are ignored while it is on the track
    //     (see move()), and its roll is set here rather than produced by friction;
    //   - auto-turns (bolt / auto-pilot) happen exactly on the centre of the turn part instead of
    //     whenever a distance check next ran (measured up to 0.42 units off at top speed).
    // Off the track nothing is forced: with no ground under it the ball collides normally again
    // and gravity takes over, which is how a missed turn still ends the run.
    //
    // Measure changes with Assets/Scripts/Diagnostics/MovementProbe.cs.
    // ---------------------------------------------------------------------------------------

    // The ground probe starts this far above the ball's centre, so a ball that has sunk slightly
    // into a seam still finds the surface it is on.
    private const float GROUND_PROBE_LIFT = 0.5f;
    // A surface further than this below where the ball should be means the ball is in the air.
    private const float GROUND_SNAP_DISTANCE = 0.2f;
    // tan(50 degrees): the steepest rise or fall per unit of travel the ball follows on the ground.
    private const float MAX_SLOPE_PER_STEP = 1.19f;
    // Surfaces steeper than about 70 degrees are walls, not track.
    private const float MIN_GROUND_NORMAL_Y = 0.35f;
    // How the ball spins. ROLL_LOOK of true rolling (speed / radius): on a real rolling ball the
    // top moves at twice the ball's speed over the track, and from this camera, looking down on
    // it, true rolling reads as spinning faster than the ball goes - the playtest said so at 1.0.
    // Never more than MAX_SPIN_PER_FRAME between two frames on screen: past ~34 degrees a frame the
    // texture strobes (at top speed, with the game's 1.2 time scale, true rolling is 64 degrees a
    // frame at 60 fps; a bolt is three times that). It used to be capped at 7 rad/s, the project's
    // default max angular speed, and visibly rolled slower than it moved. MAX_SPIN only guards
    // against nonsense.
    private const float ROLL_LOOK = 0.75f;
    private const float MAX_SPIN_PER_FRAME = 0.6f; // radians
    private const float MAX_SPIN = 100f;
    private float frameSeconds = 1f / 60f;         // smoothed real time between rendered frames
    // Radius of the probe that checks there is track under the ball's centre: small, so the ball
    // falls off an edge once its centre is past it, as a real ball does; not zero, so it cannot
    // slip between two parts at a seam.
    private const float SUPPORT_PROBE_RADIUS = 0.04f;

    private float ballRadius = 0.25f;
    // How much of the ball's speed goes across the ground, from the slope of the track along the
    // way it is going (1 on the flat, about 0.79 up the steepest ramp). Only the slope along the
    // direction of travel counts: a ball riding a part's rounded edge leans sideways, not uphill.
    private float slopeFactor = 1f;
    private const float MIN_SLOPE_FACTOR = 0.6f;
    private int groundMask;
    private Transform pendingAutoTurnPart;
    // How far before the centre of pendingAutoTurnPart the ball turns: 0 for the game's own turns.
    private float pendingTurnLead;
    // Early-tap grace (a trial - set EARLY_TAP_SECONDS to 0 to switch it off). A tap this long
    // before the ball reaches a turn part used to turn it there and then, off the track; at top
    // speed that is a tap 20 ms too soon. Now such a tap is kept, and the ball turns as soon as it
    // is on the part, EARLY_TAP_INSET past its near edge - as does a tap on the part's first
    // EARLY_TAP_INSET, which used to turn the ball onto the very edge of the next leg. In seconds
    // the player feels (real time), never quite as far ahead as one part is long (turnPartJustAhead).
    private const float EARLY_TAP_SECONDS = 0.15f;
    private const float EARLY_TAP_MAX_DISTANCE = 2.4f;
    private const float EARLY_TAP_INSET = 0.3f;
    private const float HALF_PART = 1.25f;
    // The part under the ball's centre while it is on the track, and the part the ball last
    // turned on by itself (auto-pilot, bolt, one-tap), so that one part never turns it twice.
    private Transform groundPart;
    private Transform partTurnedOn;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Set by MovementProbe: log lost ground and unusual turns.
    public static bool DebugMovement;
#endif
    // True while move() is placing the ball on the track. Static and volatile because the physics
    // thread reads it (ignoreContactsOnTrack); only the scene's player installs that hook.
    private static volatile bool drivenOnTrack;
    private float trackVerticalSpeed;
    private bool contactHookInstalled;

    void Awake()
    {
        myRB = GetComponent<Rigidbody>();
        cameraController = FindObjectOfType<CameraController>();
        pathMaker = FindObjectOfType<PathMaker>();
        materialsManager = FindObjectOfType<MaterialsManager>();
        moveToPosition = GetComponent<MoveToPosition>();
        inGameUi = FindObjectOfType<InGameUI>();
        playerTrigger = GetComponent<PlayerTrigger>();
        playerStats = PlayerStats.Instance;

        originalPosition = transform.position;

        SphereCollider sphere = GetComponent<SphereCollider>();
        Vector3 scale = transform.lossyScale;
        ballRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        myRB.maxAngularVelocity = MAX_SPIN; // PhysX would clamp the roll to its default, 7 rad/s
        sphere.sharedMaterial = new PhysicsMaterial("Ball (frictionless)")
        {
            staticFriction = 0,
            dynamicFriction = 0,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        groundMask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
        if (CompareTag("Player"))
        {
            sphere.hasModifiableContacts = true;
            Physics.ContactModifyEvent += ignoreContactsOnTrack;
            contactHookInstalled = true;
        }

        resetPlayerValues();

        materialsManager.unlockBall(1);
        materialsManager.unlockFloor(1);

        // GetComponent<Moons>().setMoons();
    }

    void OnDestroy()
    {
        if (contactHookInstalled)
        {
            Physics.ContactModifyEvent -= ignoreContactsOnTrack;
            drivenOnTrack = false;
        }
    }

    // [System.Obsolete]
    // IEnumerator Start()
    // {
    //     using (WWW www = new WWW(Application.dataPath + "/Audio/SuperSpeedLoop.wav"))
    //     {
    //         yield return www;
    //         boltTrail.gameObject.GetComponent<AudioSource>().clip = www.GetAudioClipCompressed();
    //         boltTrail.gameObject.GetComponent<AudioSource>().Play();
    //     }
    // }

    public void resetPlayerValues()
    {
        // A chance's respawn, or the move into position, may still be under way (paused, then
        // Home or Restart): finished later, it would build the chance's path into the next game.
        moveToPosition.Cancel();
        timesRespawnedAfterChancePickedUp = 0;
        stopInterpolation();
        pendingAutoTurnPart = null;
        groundPart = partTurnedOn = null;
        drivenOnTrack = false;
        slopeFactor = 1f;
        myRB.isKinematic = true;
        transform.position = previewPosition;
        transform.localRotation = Quaternion.Euler(0, 0, 15);
        direction = Directions.North;
        myRB.useGravity = false;
        myRB.isKinematic = false;
        FindObjectOfType<PlayerLinkedObjectsController>().reset();
    }

    public void setPlayerMaterial()
    {
        GetComponent<MeshRenderer>().material = materialsManager.getSelectedBallMaterial().material;
        GetComponent<Moons>().setMoons();
        GetComponent<PlayerLinkedObjectsController>().prepareLinkedObjects(false);
        GetComponent<Outline>().enabled = !MaterialsManager.isSolarBall(GetComponent<MeshRenderer>().material);
    }

    void Update()
    {
        if (Time.unscaledDeltaTime > 0f && Time.unscaledDeltaTime < 0.25f)
            frameSeconds = Mathf.Lerp(frameSeconds, Time.unscaledDeltaTime, 0.05f);
    }

    void FixedUpdate()
    {
        // Interpolation smooths the rendered ball between physics steps, which otherwise land
        // unevenly across frames (1, 2 or 3 steps per frame depending on time scale and frame
        // rate). It is only on while this script drives the ball: the menu and the respawn
        // sequence move the transform directly, which interpolation would fight.
        bool driving = Utility.gameStarted && Utility.camFollowPlayer && !Utility.spawningAfterChance;
        RigidbodyInterpolation wanted = driving ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        if (myRB.interpolation != wanted)
            myRB.interpolation = wanted;

        if (Utility.gameStarted)
        {
            if (!Utility.stoppedForTutorials && !Utility.spawningAfterChance && Utility.camFollowPlayer)
                move();
            else
                drivenOnTrack = false; // stopped, respawning or the run ended: the track holds the ball up again
        }
        else
        {
            drivenOnTrack = false;
            transform.Rotate(new Vector3(0, Time.deltaTime * -Utility.Constants.ROTATION_SPEED, 0));
        }
    }

    private void stopInterpolation()
    {
        if (myRB != null)
            myRB.interpolation = RigidbodyInterpolation.None;
    }

    public void movePlayerToPosition()
    {
        Utility.userCanPause = true;
        moveToPosition.MoveTransform(transform.position, originalPosition, 1, true, () =>
            {
                transform.position = originalPosition;
                myRB.useGravity = true;
                Debug.Log("WT, Player is in position");
                Utility.playerIsInPosition = true;
                Utility.gameStarted = true;
                Utility.camFollowPlayer = true;
                Destroyer.destroyedBricksCount = 0;

                if (playerStats.isTutorialsOn())
                {
                    GameAnalytics.TutorialBegin();
                    startSpeed = Utility.Constants.TUTORIAL_PLAYER_SPEED;
                    if (inGameUi == null)
                        inGameUi = FindObjectOfType<InGameUI>();
                    inGameUi.startTutorial();
                }
                else
                    startSpeed = Utility.Constants.START_PLAYER_SPEED;
                speed = startSpeed;

                // setNormalTrail();
            });
    }

    public void respawnForChance()
    {
        stopInterpolation();
        pendingAutoTurnPart = null;
        groundPart = partTurnedOn = null;
        drivenOnTrack = false;
        slopeFactor = 1f;
        Utility.spawningAfterChance = true;
        myRB.isKinematic = false;
        cameraController.PlayChanceTakenEffect();
        Vector3 previewPositionWithDirection = Quaternion.AngleAxis((int)direction, Vector3.up) * previewPosition;
        moveToPosition.MoveTransform(transform.position, cameraController.transform.position + previewPositionWithDirection, 3f, false, () =>
        {
            SoundManager.Instance.removeFilter();
            // GetComponent<PlayerLinkedObjectsController>().PlayChanceTakenEffect();
            timesRespawnedAfterChancePickedUp++;
            if (timesRespawnedAfterChancePickedUp >= Utility.getChanceTimes())
            {
                pathMaker.chanceIsOver();
                Utility.chanceIsOn = false;
            }
            pathMaker.startSpawningPathAfterChance(new Vector3(cameraController.transform.position.x, cameraController.transform.position.y - 0.6f, cameraController.transform.position.z), direction);
            // Rolling again the moment it arrives, while the path may still be dropping into place:
            // parts close ahead of the ball hurry down so it never reaches one in the air
            // (MoveDown), so the player is not kept waiting.
            moveToPosition.MoveTransform(transform.position, cameraController.transform.position, 1, true, () =>
            {
                Utility.camFollowPlayer = true;
                Utility.spawningAfterChance = false;
                myRB.useGravity = true;
                transform.position = cameraController.transform.position;
            });
        });
    }

    /// <summary>
    /// A bolt has just started: from here the game turns the ball and takes no taps. If the ball
    /// is on a turn part it has not turned on - the bolt button pressed at the corner instead of a
    /// tap - that turn is the game's to make: at the part's centre if the ball has yet to reach it,
    /// here and now if it is past. (move() queues such a part too, but a step later, and from the
    /// last stretch of the part that was a step too late: the ball ran off the end.)
    /// </summary>
    public void boltStarted()
    {
        if (groundPart == null || !isTurnPart(groundPart) || groundPart == partTurnedOn ||
            direction != pathDirectionOf(groundPart))
            return;
        Vector3 forward = directionVector(direction);
        Vector3 toCentre = groundPart.position - myRB.position;
        if (toCentre.x * forward.x + toCentre.z * forward.z > 0f)
            queueAutoTurn(groundPart);
        else
            turnForPart(groundPart);
    }

    /// <summary>
    /// A turn from the left/right and swipe controls. On a turn part, once the ball has turned the
    /// way the part goes, that part is done: a second tap or swipe there - a bounce, a second
    /// finger - used to turn the ball again, straight off the track. One-tap has always had this
    /// (autoTurn). Anywhere else, and after a wrong turn, the player turns freely as before.
    /// </summary>
    public void manualTurn(bool left)
    {
        if (playerStats.isTutorialsOn())
        {
            tutorialTurn(left);
            return;
        }
        Transform ahead = turnPartJustAhead();
        if (ahead != null && left == ahead.CompareTag("LandLeft"))
        {
            queueEarlyTurn(ahead);
            return;
        }
        if (groundPart != null && isTurnPart(groundPart))
        {
            if (groundPart == partTurnedOn)
                return;
            if (left == groundPart.CompareTag("LandLeft"))
            {
                if (justOnto(groundPart))
                {
                    queueEarlyTurn(groundPart);
                    return;
                }
                partTurnedOn = groundPart;
            }
        }
        if (left)
            turnLeft();
        else
            turnRight();
    }

    public void turnRight()
    {
        // Any turn settles the ball's direction for this part, so a queued auto-turn must not fire
        // as well: picking up a bolt on a turn part turns the ball there and then (BoltPickUp),
        // and turning twice sends the ball back down the track it came from.
        pendingAutoTurnPart = null;
        if (playerStats.isTutorialsOn() && pathMaker.nextDirection?.nextDistination == NextDistination.LEFT)
            return;

        int currentDirection = ((int)direction);

        currentDirection = (currentDirection + 90) % 360;

        direction = (Directions)currentDirection;

        cameraController.turnRight();
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayTurn();

        // The cast that used to be here read pathMaker.nextDirection into an unused variable and
        // threw if it was null - which a manual turn can be.
        Utility.shouldDequeue = false;

        keepGoingAfterTutorial();
    }

    public void turnLeft()
    {
        pendingAutoTurnPart = null;
        if (playerStats.isTutorialsOn() && pathMaker.nextDirection?.nextDistination == NextDistination.RIGHT)
            return;

        int currentDirection = ((int)direction);

        currentDirection = currentDirection - 90;

        if (currentDirection < 0)
            currentDirection += 360;

        direction = (Directions)currentDirection;

        cameraController.turnLeft();
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayTurn();

        Utility.shouldDequeue = false;

        keepGoingAfterTutorial();
    }

    public void autoTurn()
    {
        if (playerStats.isTutorialsOn())
        {
            tutorialTurn(null);
            return;
        }
        // One-tap: when the ball is on a turn part, that part decides. The direction the parts'
        // Destroyer triggers leave in pathMaker.nextDirection was missed when the ball ran near a
        // part's edge (the triggers are smaller than the parts, so that two turn parts side by side
        // cannot both fire), and then the ball turned the wrong way or not at all.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugMovement)
            Debug.Log("[Tap] on " + (groundPart != null ? groundPart.tag + " " + groundPart.name : "no part") +
                      (groundPart != null && groundPart == partTurnedOn ? " (already turned here, ignored)" : "") +
                      " next=" + (pathMaker.nextDirection.HasValue ? pathMaker.nextDirection.Value.nextDistination.ToString() : "null") +
                      " ball " + myRB.position.ToString("F2") + " heading " + direction);
#endif
        if (groundPart != null && isTurnPart(groundPart) && groundPart != partTurnedOn)
        {
            if (justOnto(groundPart))
                queueEarlyTurn(groundPart);
            else
                turnForPart(groundPart);
            return;
        }
        Transform ahead = turnPartJustAhead();
        if (ahead != null)
        {
            queueEarlyTurn(ahead);
            return;
        }
        if (groundPart != null && isTurnPart(groundPart))
            return; // already turned here

        if (pathMaker.nextDirection == null)
            return;

        if (((TurnDirection)pathMaker.nextDirection).nextDistination == NextDistination.RIGHT)
            turnRight();
        else
            turnLeft();

        pathMaker.nextDirection = null;
    }

    /// <summary>
    /// A tap or swipe in the tutorial (InGameUI): it can only do the right thing. On the turn part
    /// the ball is coming to, or just before it, it turns the ball the way the part goes; anywhere
    /// else, or for the wrong side, nothing happens and the player is told why.
    /// <paramref name="left"/> is the side chosen, null for one-tap.
    /// </summary>
    private void tutorialTurn(bool? left)
    {
        if (inGameUi == null)
            inGameUi = FindObjectOfType<InGameUI>();
        if (pendingAutoTurnPart != null)
            return; // this turn is already on its way
        Transform part = groundPart != null && isTurnPart(groundPart) && groundPart != partTurnedOn &&
                         direction == pathDirectionOf(groundPart) ? groundPart : turnPartJustAhead();
        if (part == null)
        {
            // A second tap on the part it has just turned on is not a mistake.
            if (groundPart == null || groundPart != partTurnedOn)
                inGameUi.tutorialTapIgnored(false);
            return;
        }
        if (left.HasValue && left.Value != part.CompareTag("LandLeft"))
        {
            inGameUi.tutorialTapIgnored(true);
            return;
        }
        bool wasStopped = Utility.stoppedForTutorials;
        if (part == groundPart && !justOnto(part))
            turnForPart(part);
        else
            queueEarlyTurn(part);
        inGameUi.tutorialTurned(wasStopped);
    }

    /// <summary>The tutorial was skipped: a ball waiting at a turn is turned and sent on its way.</summary>
    public void tutorialSkipped()
    {
        if (Utility.stoppedForTutorials && groundPart != null && isTurnPart(groundPart) && groundPart != partTurnedOn)
            turnForPart(groundPart);
        keepGoingAfterTutorial();
    }

    /// <summary>The way the ball is going.</summary>
    public Vector3 forward()
    {
        return directionVector(direction);
    }

    /// <summary>Whether the ball is heading along the path onto this turn part, not yet turned on it.</summary>
    public bool isComingTo(Transform turnPart)
    {
        return turnPart != partTurnedOn && direction == pathDirectionOf(turnPart);
    }

    /// <summary>
    /// Bolt / auto-pilot: turn when the ball reaches the centre of <paramref name="turnPart"/>.
    /// The turn itself happens inside the physics step that crosses the centre (see move()).
    /// </summary>
    public void queueAutoTurn(Transform turnPart)
    {
        // One part turns the ball once. It may already have - a tap, or a bolt collected on it -
        // by the time its auto-pilot trigger reports the ball: turning again sent the ball back
        // the way it came.
        if (turnPart == partTurnedOn)
            return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugMovement)
        {
            Vector3 f = directionVector(direction);
            Vector3 d = turnPart.position - myRB.position;
            Debug.Log("[Turn] queued " + turnPart.name + " ahead=" + (d.x * f.x + d.z * f.z).ToString("F3") +
                      " replacing=" + (pendingAutoTurnPart != null ? pendingAutoTurnPart.name : "none") + " dir=" + direction +
                      " next=" + (pathMaker.nextDirection.HasValue ? pathMaker.nextDirection.Value.nextDistination.ToString() : "null"));
        }
#endif
        pendingAutoTurnPart = turnPart;
        pendingTurnLead = 0f;
    }

    // The turn part the ball will be on within EARLY_TAP_SECONDS, if it is coming to one along the
    // path. Parts are HALF_PART each way and the look-ahead no longer than a part, so the point
    // that far ahead is on that part whenever its near edge is within reach.
    private Transform turnPartJustAhead()
    {
        if (EARLY_TAP_SECONDS <= 0f || !drivenOnTrack || Utility.stoppedForTutorials)
            return null;
        float reach = Mathf.Min(speed * Time.timeScale * EARLY_TAP_SECONDS, EARLY_TAP_MAX_DISTANCE);
        Vector3 from = myRB.position + directionVector(direction) * reach + Vector3.up * 1.5f;
        RaycastHit hit;
        if (!Physics.Raycast(from, Vector3.down, out hit, 3f, groundMask, QueryTriggerInteraction.Ignore))
            return null;
        Transform part = partOf(hit.collider);
        if (part == null || part == groundPart || part == partTurnedOn || part == pendingAutoTurnPart ||
            !isTurnPart(part) || pathDirectionOf(part) != direction || pathMaker.isNotGroundYet(part))
            return null;
        return part;
    }

    // On the first EARLY_TAP_INSET of a turn part it is coming onto along the path.
    private bool justOnto(Transform turnPart)
    {
        if (EARLY_TAP_SECONDS <= 0f || !drivenOnTrack || direction != pathDirectionOf(turnPart))
            return false;
        Vector3 forward = directionVector(direction);
        Vector3 toCentre = turnPart.position - myRB.position;
        return toCentre.x * forward.x + toCentre.z * forward.z > HALF_PART - EARLY_TAP_INSET;
    }

    private void queueEarlyTurn(Transform turnPart)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugMovement)
            Debug.Log("[Tap] early, kept for " + turnPart.tag + " at " + turnPart.position.ToString("F2") + " ball " + myRB.position.ToString("F2"));
#endif
        pendingAutoTurnPart = turnPart;
        pendingTurnLead = HALF_PART - EARLY_TAP_INSET;
    }

    /// <summary>
    /// Turns the way the part itself goes. autoTurn() reads PathMaker.nextDirection, which the
    /// parts' Destroyer triggers share and every turn consumes: a run caught it already null at a
    /// turn part's centre, so the ball did not turn there and turned late instead. The part the
    /// ball is standing on cannot be out of date.
    /// </summary>
    private void turnForPart(Transform part)
    {
        bool left = part.CompareTag("LandLeft");
        if (!left && !part.CompareTag("LandRight"))
        {
            autoTurn(); // not a turn part after all: fall back to the queue
            return;
        }

        // Keep the shared queue in step: manual turns and the tutorial read it.
        pathMaker.nextDirection = null;
        partTurnedOn = part;
        if (left)
            turnLeft();
        else
            turnRight();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugMovement)
            Debug.Log("[Turn] " + (left ? "LEFT" : "RIGHT") + " at " + part.name + " " + part.position.ToString("F2") +
                      " ball " + myRB.position.ToString("F2") + " now heading " + direction);
#endif
    }

    /// <summary>
    /// Picking up a bolt faces the ball the way the path goes at <paramref name="part"/>. A player
    /// who turned off the path to reach a bolt used to carry on sideways at bolt speed and fall.
    /// Only the direction: moving the ball onto the part's centre line as well looked like the
    /// bolt shoving it sideways, and the next automatic turn puts it back on the centre anyway.
    /// Arriving the normal way on a turn part, the ball still turns on the centre, as before.
    /// </summary>
    public void rejoinPathAt(Transform part)
    {
        Directions incoming = pathDirectionOf(part);
        bool turnPart = isTurnPart(part);
        Directions outgoing = !turnPart ? incoming
            : (Directions)(((int)incoming + (part.CompareTag("LandRight") ? 90 : 270)) % 360);

        if (turnPart && direction == incoming)
        {
            queueAutoTurn(part);
        }
        else if (direction != incoming && direction != outgoing)
        {
            // Off the path: face the way it leaves this part. A turn already queued further on
            // still stands (turning cancels it).
            Transform queuedFurtherOn = pendingAutoTurnPart != part ? pendingAutoTurnPart : null;
            turnTo(outgoing);
            pendingAutoTurnPart = queuedFurtherOn;
        }
        if (turnPart && direction == outgoing)
            partTurnedOn = part; // turned for this part: taps on it must not turn the ball again
    }

    // The way the path enters a part: PathMaker turns each part so its own X axis points that way
    // (setPartPosition: yaw 0 for East, 90 for South, 180 for West, -90 for North).
    private static Directions pathDirectionOf(Transform part)
    {
        Vector3 along = part.rotation * Vector3.right;
        if (Mathf.Abs(along.x) >= Mathf.Abs(along.z))
            return along.x > 0 ? Directions.East : Directions.West;
        return along.z > 0 ? Directions.North : Directions.South;
    }

    private void turnTo(Directions target)
    {
        int quartersRight = (((int)target - (int)direction) / 90 + 4) % 4;
        if (quartersRight == 3)
            turnLeft();
        else
            for (int i = 0; i < quartersRight; i++)
                turnRight();
    }

    /// <summary>The ball is being stopped where it is (tutorial): let the track hold it up again.</summary>
    public void stopDriving()
    {
        drivenOnTrack = false;
    }

    private void keepGoingAfterTutorial()
    {
        if (playerStats.isTutorialsOn())
        {
            speed = startSpeed;
            Utility.stoppedForTutorials = false;
            playerTrigger.tutorialTurnDone();
            playerTrigger.stopTutorialAnimation();
        }
    }

    private static Vector3 directionVector(Directions d)
    {
        switch (d)
        {
            case Directions.East: return Vector3.right;
            case Directions.South: return Vector3.back;
            case Directions.West: return Vector3.left;
            default: return Vector3.forward;
        }
    }

    void move()
    {
        Vector3 position = myRB.position;
        float dt = Time.fixedDeltaTime;
        Vector3 forward = directionVector(direction);
        float moveSpeed = speed * slopeFactor; // across the ground
        Vector3 velocity = forward * moveSpeed;

        if (pendingAutoTurnPart != null)
            velocity = applyPendingAutoTurn(position, forward, velocity, dt, moveSpeed);

        // Where the ball will be horizontally at the end of this step, and the height at which it
        // rests on the track there.
        Vector3 next = position + velocity * dt;
        float horizontalStep = new Vector2(velocity.x, velocity.z).magnitude * dt;
        float restHeight;
        Vector3 groundNormal;
        if (findRestHeight(next, position.y, horizontalStep, out restHeight, out groundNormal))
        {
            // Arrive exactly at the resting height. The ball's contacts with the track are ignored
            // meanwhile (ignoreContactsOnTrack): each part has its own collider, and the solver met
            // the start edge of the next part as a bump and pushed the ball off it ("ghost
            // collisions") - measured as hops of up to half the ball's radius and one-step speed
            // drops to 64% at the seams.
            drivenOnTrack = true;
            if (pathMaker != null)
                pathMaker.ballIsOn(groundPart);
            // The bolt ends on its straight: normally its trigger says so (PlayerTrigger); this is
            // the same check from the ground under the ball, which does not depend on a trigger.
            if (Utility.boltIsOn)
            {
                if (pickUpsManager == null)
                    pickUpsManager = FindObjectOfType<PickUpsManager>();
                pickUpsManager.checkBoltEnd(groundPart);
            }
            // While the game turns the ball itself (a bolt, auto-pilot), the turn part under it
            // must be queued. Its trigger queues it as the ball rolls on - but not if the bolt
            // started with the ball already on the part (the bolt button; taps are off during a
            // bolt), and then the ball ran straight off the track.
            if (pendingAutoTurnPart == null && groundPart != null && groundPart != partTurnedOn &&
                isTurnPart(groundPart) && (Utility.boltIsOn || playerStats.isAutoPilotOn()) &&
                direction == pathDirectionOf(groundPart))
                queueAutoTurn(groundPart);
            // The slope ahead, for the next step's speed across the ground: rise per unit of
            // travel is -(n . forward) / n.y. Eased, so the seams between parts do not jolt it.
            Vector3 travel = new Vector3(velocity.x, 0f, velocity.z).normalized;
            float rise = -Vector3.Dot(groundNormal, travel) / Mathf.Max(groundNormal.y, 0.2f);
            float wantedFactor = Mathf.Clamp(1f / Mathf.Sqrt(1f + rise * rise), MIN_SLOPE_FACTOR, 1f);
            slopeFactor = Mathf.Lerp(slopeFactor, wantedFactor, 0.5f);
            trackVerticalSpeed = (restHeight - position.y) / dt;
            velocity.y = trackVerticalSpeed;
            // PhysX adds gravity to the velocity during the step; cancel it.
            if (myRB.useGravity)
                velocity.y -= Physics.gravity.y * dt;

            // Roll about the axis perpendicular to the surface and the motion.
            Vector3 rollAxis = Vector3.Cross(groundNormal, new Vector3(velocity.x, 0, velocity.z));
            if (rollAxis.sqrMagnitude > 0.000001f)
            {
                float visible = MAX_SPIN_PER_FRAME / (frameSeconds * Mathf.Max(Time.timeScale, 0.01f));
                myRB.angularVelocity = rollAxis.normalized * Mathf.Min(ROLL_LOOK * speed / ballRadius, visible, MAX_SPIN);
            }
        }
        else if (drivenOnTrack)
        {
            // Just left the track: carry on as it was moving; collisions count again and gravity
            // decides from here.
            drivenOnTrack = false;
            velocity.y = trackVerticalSpeed;
        }
        else
        {
            velocity.y = myRB.linearVelocity.y;
        }

        myRB.linearVelocity = velocity;
    }

    // Runs on the physics thread, for every contact of the ball's collider (the only collider with
    // hasModifiableContacts). While move() is placing the ball on the track, the track must not
    // push it; off the track the ball collides normally.
    private static void ignoreContactsOnTrack(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
    {
        if (!drivenOnTrack)
            return;
        for (int p = 0; p < pairs.Length; p++)
        {
            ModifiableContactPair pair = pairs[p];
            for (int i = 0; i < pair.contactCount; i++)
                pair.IgnoreContact(i);
        }
    }

    private Vector3 applyPendingAutoTurn(Vector3 position, Vector3 forward, Vector3 velocity, float dt, float moveSpeed)
    {
        if (!pendingAutoTurnPart.gameObject.activeInHierarchy)
        {
            // The part went back to the pool before the ball reached it; its position is now
            // somewhere else entirely.
            pendingAutoTurnPart = null;
            return velocity;
        }

        Vector3 toCentre = pendingAutoTurnPart.position - position;
        float ahead = toCentre.x * forward.x + toCentre.z * forward.z - pendingTurnLead;
        float step = moveSpeed * dt;
        if (ahead > step)
            return velocity; // the centre is not reached during this step

        Transform turningAt = pendingAutoTurnPart;
        pendingAutoTurnPart = null;
        if (turningAt == partTurnedOn)
            return velocity; // this part has turned the ball already (see queueAutoTurn)
        turnForPart(turningAt);

        Vector3 newForward = directionVector(direction);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugMovement && (newForward == forward || Mathf.Abs(ahead) > step))
            Debug.Log("[Turn] at " + turningAt.name + " ahead=" + ahead.ToString("F3") + " step=" + step.ToString("F3") +
                      " turned=" + (newForward != forward) + " dir=" + direction);
#endif
        if (newForward == forward)
            return velocity; // nothing to turn to

        if (Mathf.Abs(ahead) > step)
            return newForward * moveSpeed; // far off (should not happen): turn in place, as before

        // Finish the old line exactly at the centre and spend the rest of this step on the new
        // line, so the ball ends the step on the new centre line. The one-step chord cuts the
        // corner by at most a quarter of a step.
        Vector3 target = forward * ahead + newForward * (step - Mathf.Abs(ahead));
        return target / dt;
    }

    // The height at which the ball's centre rests on the track at horizontal position `at`.
    // A sphere cast with the ball's own radius touches exactly where the ball would, whatever the
    // shape underneath - flat, ramp, the start or top of a ramp. The ball only counts as on the
    // track while there is track under its centre (supportUnderCentre).
    private bool findRestHeight(Vector3 at, float currentY, float horizontalStep, out float restY, out Vector3 normal)
    {
        groundPart = null;
        float maxClimb = horizontalStep * MAX_SLOPE_PER_STEP;
        float maxDrop = maxClimb + GROUND_SNAP_DISTANCE;

        Vector3 origin = new Vector3(at.x, currentY + GROUND_PROBE_LIFT, at.z);
        RaycastHit hit;
        bool overTheEdge = false;
        if (castToGround(origin, ballRadius, GROUND_PROBE_LIFT + maxDrop, out hit)
            && hit.normal.y >= MIN_GROUND_NORMAL_Y)
        {
            restY = origin.y - hit.distance;
            normal = hit.normal;
            if (restY - currentY <= maxClimb + 0.01f)
            {
                if (supportUnderCentre(origin, restY))
                    return true;
                overTheEdge = true;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugMovement)
        {
            bool any = Physics.SphereCast(origin, ballRadius, Vector3.down, out hit, 5f, groundMask, QueryTriggerInteraction.Ignore);
            Debug.Log("[Ground] lost at " + at.ToString("F3") + (overTheEdge ? " (centre past the edge)" : "") +
                      " currentY=" + currentY.ToString("F3") + " maxClimb=" + maxClimb.ToString("F3") +
                      " maxDrop=" + maxDrop.ToString("F3") + " castHit=" + any + (any ? " dist=" + hit.distance.ToString("F3") + " restY=" + (origin.y - hit.distance).ToString("F3") +
                      " normal=" + hit.normal.ToString("F2") + " collider=" + hit.collider.name + "/" + (hit.collider.transform.parent != null ? hit.collider.transform.parent.name : "-") : ""));
        }
#endif

        restY = currentY;
        normal = Vector3.up;
        return false;
    }

    // Whether there is track under the ball's centre, and which part it is. The sphere cast in
    // findRestHeight also touches the edge of a part while the ball hangs over it: on its own it
    // let the ball ride along an edge in the air, centre off the track, instead of falling off.
    private bool supportUnderCentre(Vector3 origin, float restY)
    {
        // The track under the centre is at most about 1.3 radii below it, on the steepest ramp;
        // 1.5 leaves room for the curved pieces.
        float reach = origin.y - (restY - ballRadius * 1.5f);
        RaycastHit support;
        if (!castToGround(origin, SUPPORT_PROBE_RADIUS, reach, out support))
            return false;

        groundPart = partOf(support.collider);
        return true;
    }

    // Each part is one kinematic Rigidbody with its colliders below it.
    private static Transform partOf(Collider c)
    {
        return c.attachedRigidbody != null ? c.attachedRigidbody.transform : c.transform.parent;
    }

    // A sphere cast down onto the track that passes through the parts that are not ground for the
    // ball yet: the part after a U-turn, beside the approach to it (PathMaker.notGroundUntil).
    // With no such part in the way it is the plain SphereCast it always was.
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    private bool castToGround(Vector3 origin, float radius, float distance, out RaycastHit hit)
    {
        if (!Physics.SphereCast(origin, radius, Vector3.down, out hit, distance, groundMask, QueryTriggerInteraction.Ignore))
            return false;
        if (pathMaker == null || !pathMaker.isNotGroundYet(partOf(hit.collider)))
            return true;

        // The nearest surface belongs to such a part: take the nearest one that does not. Unlike
        // SphereCast, the NonAlloc version also reports colliders the sphere starts inside, at
        // distance 0 and point zero; those are skipped to match.
        int n = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits, distance, groundMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = groundHits[i];
            if ((h.distance == 0f && h.point == Vector3.zero) || pathMaker.isNotGroundYet(partOf(h.collider)))
                continue;
            if (!found || h.distance < hit.distance)
            {
                hit = h;
                found = true;
            }
        }
        return found;
    }

    private static bool isTurnPart(Transform part)
    {
        return part.CompareTag("LandLeft") || part.CompareTag("LandRight");
    }
}
