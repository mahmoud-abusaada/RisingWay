using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTrigger : MonoBehaviour
{
    [SerializeField] private InGameUI inGameUI;
    private PathMaker pathMaker;
    private PlayerMovement playerMovement;
    private PlayerStats playerStats;
    private PickUpsManager pickUpsManager;
    private Rigidbody body;

    // Tutorial: the turn part the ball is on its way across, until the player turns on it.
    //   First step (InGameUI.tutorialStopsAtTurns): the ball slows from the part's near edge and
    //   stops on its centre, by the flag, and waits for the tap.
    //   After that it rolls on at full tutorial speed to the centre - the tap belongs there - and
    //   only if the player has still not tapped does it slow down and stop, TUTORIAL_LATE_STOP
    //   past the centre, where there is still track under it, and wait.
    private Transform tutorialPart;
    private const float TUTORIAL_LATE_STOP = 0.7f;
    private const float HALF_PART = 1.25f;
    private const float SLOWEST = 0.2f;        // creeping up to the centre
    private const float SLOWEST_LATE = 0.8f;   // still moving, so a tap there is still in time

    // Start is called before the first frame update
    void Awake()
    {
        pathMaker = FindObjectOfType<PathMaker>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        playerStats = PlayerStats.Instance;
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        body = GetComponent<Rigidbody>();
        // inGameUI = FindObjectOfType<InGameUI>();
    }

    void Update()
    {
        checkParts();
    }

    void FixedUpdate()
    {
        checkParts();
    }

    private void checkParts()
    {
        if (tutorialPart == null || Utility.stoppedForTutorials)
            return;
        if (!playerStats.isTutorialsOn() || !tutorialPart.gameObject.activeInHierarchy)
        {
            tutorialPart = null;
            return;
        }

        // How far along its way the ball is from the part's centre: negative before it. The
        // physics position, not transform.position: the ball is interpolated while it runs, so in
        // Update its transform is up to one physics step behind.
        Vector3 forward = playerMovement.forward();
        Vector3 fromCentre = body.position - tutorialPart.position;
        float along = fromCentre.x * forward.x + fromCentre.z * forward.z;

        bool stops = inGameUI.tutorialStopsAtTurns();
        float slowFrom = stops ? -HALF_PART : 0f;
        float stopAt = stops ? 0f : TUTORIAL_LATE_STOP;
        float slowed = 1f - Mathf.InverseLerp(slowFrom, stopAt, along);
        playerMovement.speed = Mathf.Max(slowed * Utility.Constants.TUTORIAL_PLAYER_SPEED, stops ? SLOWEST : SLOWEST_LATE);

        if (along >= stopAt - 0.03f)
        {
            Utility.stoppedForTutorials = true;
            playerMovement.speed = 0;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            playerMovement.stopDriving();
            inGameUI.tutorialBallStopped(tutorialPart, !stops);
        }
    }

    /// <summary>The player turned the ball on the tutorial's turn part: nothing more to do there.</summary>
    public void tutorialTurnDone()
    {
        tutorialPart = null;
    }

    public void stopCoroutines()
    {
        tutorialPart = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Destroyer"))
        {
            if (Utility.gameStarted)
                pathMaker.spawnPart();

            if (Utility.boltIsOn && other.transform.parent.gameObject.name.Equals(Utility.Constants.BOLT_STRAIGHT_PART_NAME) && other.transform.parent.gameObject.CompareTag("LandStraight"))
            {
                pickUpsManager.boltIsOver();
                // PlayerStats.Instance.setAutoPilotState(false);
            }
        }

        if (other.gameObject.CompareTag("StartTrigger"))
        {
            if (Utility.gameStarted)
            {
                if (other.transform.parent.Find("PartStartBlock") != null)
                    other.transform.parent.Find("PartStartBlock").gameObject.SetActive(true);
            }
        }

        if (PlayerStats.Instance.isTutorialsOn())
        {
            // The box on each turn part that the ball enters as it comes onto the part.
            if (other.gameObject.CompareTag("TutorialTurnLeft") || other.gameObject.CompareTag("TutorialTurnRight"))
            {
                Transform part = other.transform.parent;
                if (part != tutorialPart && playerMovement.isComingTo(part))
                {
                    tutorialPart = part;
                    inGameUI.tutorialReachedTurn(part);
                }
            }
        }

        if ((PlayerStats.Instance.isAutoPilotOn() || Utility.boltIsOn) && other.gameObject.CompareTag("AutoPilot"))
        {
            // PlayerMovement turns inside the physics step that reaches the part's centre. The old
            // distance check here ran on whichever Update/FixedUpdate came next and could fire up
            // to 0.42 units early or late at top speed.
            playerMovement.queueAutoTurn(other.transform.parent);
        }
    }

    public void stopTutorialAnimation()
    {
        inGameUI.stopTutorialAnimation();
    }
}
