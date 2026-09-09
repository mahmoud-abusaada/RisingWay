using System;
using System.Collections;
using System.Collections.Generic;
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
    private float startSpeed;
    public int timesRespawnedAfterChancePickedUp = 0;

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

        resetPlayerValues();

        materialsManager.unlockBall(1);
        materialsManager.unlockFloor(1);

        // GetComponent<Moons>().setMoons();
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
        GetComponent<Outline>().enabled = !(GetComponent<MeshRenderer>().material.name.Contains("Earth") || GetComponent<MeshRenderer>().material.name.Contains("Saturn") || GetComponent<MeshRenderer>().material.name.Contains("Bright"));
    }

    void FixedUpdate()
    {
        if (Utility.gameStarted)
        {
            if (!Utility.stoppedForTutorials && !Utility.spawningAfterChance && Utility.camFollowPlayer)
                move();
            // stickToTheGround();
        }
        else
        {
            transform.Rotate(new Vector3(0, Time.deltaTime * -Utility.Constants.ROTATION_SPEED, 0));
        }
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
                    startSpeed = Utility.Constants.TUTORIAL_PLAYER_SPEED;
                    if (inGameUi == null)
                        inGameUi = FindObjectOfType<InGameUI>();
                    inGameUi.showTutorialInfo();
                }
                else
                    startSpeed = Utility.Constants.START_PLAYER_SPEED;
                speed = startSpeed;

                // setNormalTrail();
            });
    }

    public void respawnForChance()
    {
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
            moveToPosition.MoveTransform(transform.position, cameraController.transform.position, 1, true, () =>
            {
                Utility.camFollowPlayer = true;
                Utility.spawningAfterChance = false;
                myRB.useGravity = true;
                transform.position = cameraController.transform.position;
            });
        });
    }

    public void turnRight()
    {
        if (playerStats.isTutorialsOn() && pathMaker.nextDirection?.nextDistination == NextDistination.LEFT)
            return;

        int currentDirection = ((int)direction);

        currentDirection = (currentDirection + 90) % 360;

        direction = (Directions)currentDirection;

        cameraController.turnRight();

        if (Utility.shouldDequeue)
        {
            TurnDirection test = (TurnDirection)pathMaker.nextDirection;
            // Debug.Log("Dequeue Turn Number = " + test.turnNumber + ", Distination = " + test.nextDistination);
            Utility.shouldDequeue = false;
        }

        keepGoingAfterTutorial();
    }

    public void turnLeft()
    {
        if (playerStats.isTutorialsOn() && pathMaker.nextDirection?.nextDistination == NextDistination.RIGHT)
            return;

        int currentDirection = ((int)direction);

        currentDirection = currentDirection - 90;

        if (currentDirection < 0)
            currentDirection += 360;

        direction = (Directions)currentDirection;

        cameraController.turnLeft();

        if (Utility.shouldDequeue)
        {
            TurnDirection test = (TurnDirection)pathMaker.nextDirection;
            // Debug.Log("Dequeue Turn Number = " + test.turnNumber + ", Distination = " + test.nextDistination);
            Utility.shouldDequeue = false;
        }

        keepGoingAfterTutorial();
    }

    public void autoTurn()
    {
        // if (pathMaker.directions.Count == 0)
        // {
        //     Debug.Log("Directions queue is empty");
        //     return;
        // }

        // int currentDirection = (int)direction;
        // NextDistination nextDirection = ((TurnDirection)pathMaker.directions.Peek()).nextDistination;

        // if (pathMaker.nextDirection == null)
        // {
        //     Debug.Log("Path maker next direction is null");
        //     pathMaker.setFirstTurnAsNextDirection();

        // Debug.Log("nextDirection " + pathMaker.nextDirection);

        if (pathMaker.nextDirection == null)
            return;
        // }

        // NextDistination nextDirection = ((TurnDirection)pathMaker.nextDirection).nextDistination;

        //Debug.Log("Current direction = " + currentDirection + ", Next direction = " + nextDirection + ", Turning " + (nextDirection < currentDirection ? "Left" : "Right"));

        if (((TurnDirection)pathMaker.nextDirection).nextDistination == NextDistination.RIGHT)
            turnRight();
        else
            turnLeft();

        pathMaker.nextDirection = null;
    }

    private void keepGoingAfterTutorial()
    {
        if (playerStats.isTutorialsOn())
        {
            speed = startSpeed;
            Utility.stoppedForTutorials = false;
            playerTrigger.stopTutorialAnimation();
        }
    }

    Vector3 calculatedVelocity;
    void move()
    {

        calculatedVelocity = new Vector3(direction == Directions.East ? speed : direction == Directions.West ? -speed : 0,
                                        0,
                                        direction == Directions.North ? speed : direction == Directions.South ? -speed : 0);

        if (myRB.linearVelocity.magnitude < speed)
            calculatedVelocity.y += myRB.linearVelocity.y * 1.1f;

        myRB.linearVelocity = calculatedVelocity;
    }

    void stickToTheGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.1f, transform.position.z), -Vector3.up, out hit))
        {
            float distanceToGround = hit.distance - 0.05f;

            // if (distanceToGround != 0.25f)
            // Debug.Log("distance to ground = " + distanceToGround + ", " + transform.GetComponent<Collider>().bounds.extents.y + ", " + transform.position.y);

            if (distanceToGround > 0.3f)
                transform.position = new Vector3(transform.position.x, transform.position.y + (0.3f - distanceToGround), transform.position.z);
        }
    }
}
