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
    private float currentSpeed = 0;
    private IEnumerator slowDownCoroutine;
    private IEnumerator fullStopCoroutine;
    private bool isFirstTurn = true;
    private Transform fullStopPart;
    private float fullStopTotalDistance = 0;
    private Rigidbody body;

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

    Vector3 playerStopPosition;
    float fullStopCurrentDistance;
    float newSpeed;
    private void checkParts()
    {
        if (fullStopPart != null)
        {
            playerStopPosition = new Vector3(fullStopPart.position.x, fullStopPart.position.y + 0.6f, fullStopPart.position.z); // Added 0.6f to make the player above the land part
            // The physics position, not transform.position: the ball is interpolated while it runs,
            // so in Update its transform is up to one physics step behind.
            if (fullStopTotalDistance == 0)
                fullStopTotalDistance = Vector3.Distance(body.position, playerStopPosition);
            fullStopCurrentDistance = Vector3.Distance(body.position, playerStopPosition);
            newSpeed = fullStopCurrentDistance / fullStopTotalDistance * Utility.Constants.TUTORIAL_PLAYER_SPEED;
            if (newSpeed > 0.2f)
                playerMovement.speed = newSpeed;
            else if (newSpeed != 0)
                playerMovement.speed = 0.2f;

            if (fullStopCurrentDistance <= 0.1f)
            {
                Utility.stoppedForTutorials = true;
                playerMovement.speed = 0;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                playerMovement.stopDriving();
                // transform.position = playerStopPosition;
                fullStopCurrentDistance = 0;
                fullStopTotalDistance = 0;
                fullStopPart = null;
                inGameUI.playTutorialsAnimation();
            }
        }
    }

    public void stopCoroutines()
    {
        if (slowDownCoroutine != null)
            StopCoroutine(slowDownCoroutine);

        if (fullStopCoroutine != null)
            StopCoroutine(fullStopCoroutine);
    }

    private void OnTriggerEnter(Collider other)
    {
        // if (other.gameObject.CompareTag("Diamond"))
        // {
        //     // if (MenuCont.volumeIsOn)
        //     //     myAudio.PlayOneShot(diamondSound, 0.85F);
        //     ParticleSystem ps = (ParticleSystem)Instantiate(diamondEffect);
        //     ps.transform.position = other.transform.position + new Vector3(0, .5f, 0);
        //     ps.Play();
        //     scoreManager.diamondPicked();
        //     Destroy(ps.gameObject, ps.main.startLifetimeMultiplier);
        //     Destroy(other.gameObject);
        // }

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
            // if (other.transform.parent.CompareTag("LandLeft") || other.transform.parent.CompareTag("LandRight"))
            //     if (!isFirstTurn)
            //         pathMaker.directions.Dequeue();
            //     else
            //         isFirstTurn = false;
        }

        if (PlayerStats.Instance.isTutorialsOn())
        {
            // if (other.gameObject.CompareTag("TutorialSlowDown"))
            // {
            //     currentSpeed = playerMovement.speed;
            //     slowDownCoroutine = slowDown();
            //     StartCoroutine(slowDownCoroutine);
            // }

            if (other.gameObject.CompareTag("TutorialTurnLeft") || other.gameObject.CompareTag("TutorialTurnRight"))
            {
                // if (slowDownCoroutine != null)
                //     StopCoroutine(slowDownCoroutine);
                // fullStopCoroutine = fullStop(other.transform.parent);
                // StartCoroutine(fullStopCoroutine);

                fullStopPart = other.transform.parent;
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

    private IEnumerator slowDown()
    {
        float targetSpeed = currentSpeed / 2;
        float valueLeft = currentSpeed - targetSpeed;
        float valueToDecrease = valueLeft * Time.fixedDeltaTime * 12;
        Vector3 startPosition = transform.position;
        float distanceToPosition;
        float newSpeed = 0;
        float startSpeed = playerMovement.speed;
        while (true)
        {
            print("Slowing Down");
            // playerMovement.speed -= valueToDecrease;

            distanceToPosition = Vector3.Distance(startPosition, transform.position);
            newSpeed = Mathf.Lerp(2.5f, 0, distanceToPosition) * startSpeed;

            if (newSpeed < playerMovement.speed)
                playerMovement.speed = newSpeed;

            if (playerMovement.speed <= targetSpeed)
                break;

            yield return new WaitForSeconds(0.0001f);
        }
    }

    private IEnumerator fullStop(Transform part)
    {
        Vector3 playerStopPosition = new Vector3(part.position.x, part.position.y + 0.6f, part.position.z); // Added 0.6f to make the player above the land part
        float startDistanceToPosition = Vector3.Distance(transform.position, playerStopPosition);
        float distanceToPosition = startDistanceToPosition;
        float startSpeed = playerMovement.speed;
        float newSpeed = startSpeed;
        while (playerMovement.speed != 0 || distanceToPosition != 0)
        {
            distanceToPosition = Vector3.Distance(transform.position, playerStopPosition);

            newSpeed = Mathf.Lerp(0, startDistanceToPosition, distanceToPosition) * startSpeed;

            if (newSpeed > 0.2f)
                playerMovement.speed = newSpeed;
            else
                playerMovement.speed = 0.2f;

            if (distanceToPosition <= 0.01f)
            {
                Utility.stoppedForTutorials = true;
                playerMovement.speed = 0;
                GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                transform.position = playerStopPosition;
                distanceToPosition = 0;
                break;
            }

            yield return new WaitForSeconds(0.0001f);
        }

        inGameUI.playTutorialsAnimation();
    }

    // private IEnumerator fullStop(Transform part)
    // {
    //     Vector3 playerStopPosition = new Vector3(part.position.x, part.position.y + 0.6f, part.position.z); // Added 0.6f to make the player above the land part
    //     float distanceToPosition = Vector3.Distance(transform.position, playerStopPosition);
    //     while (playerMovement.speed != 0 || distanceToPosition != 0)
    //     {
    //         distanceToPosition = Vector3.Distance(transform.position, playerStopPosition);

    //         float valueToDecrease = playerMovement.speed * Time.deltaTime * 4;

    //         if (playerMovement.speed - valueToDecrease > 0.2f)
    //             playerMovement.speed -= valueToDecrease;

    //         if (distanceToPosition <= 0.01f)
    //         {
    //             Utility.stoppedForTutorials = true;
    //             playerMovement.speed = 0;
    //             GetComponent<Rigidbody>().velocity = Vector3.zero;
    //             GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
    //             transform.position = playerStopPosition;
    //             distanceToPosition = 0;
    //             break;
    //         }

    //         yield return new WaitForSeconds(0.005f);
    //     }

    //     inGameUI.playTutorialsAnimation();
    // }

    public void stopTutorialAnimation()
    {
        inGameUI.stopTutorialAnimation();
    }
}
