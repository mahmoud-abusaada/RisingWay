using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PathMakerOld : MonoBehaviour
{

    [SerializeField] private Transform pathParent;
    private Transform currentGamePathParent;
    private string currentGamePathParentName;
    public Transform landStart;
    public Transform landStraight;
    public Transform landLeft;
    public Transform landRight;
    public Transform slide;
    public Transform curveUp;
    public Transform curveSt;
    public Transform diamond;
    public bool arcadeMode;
    public int maxSlidesCount = 2;
    public int diamondProbability = 4;
    public int boltProbability = 50;
    public int doublePointProbability = 50;
    public int chanceProbability = 3;
    public int straightPathLength = 5;
    public int startBlocksCount = 8;
    public int maxLandsCount = 1;
    public bool allowCurveStAfterCurveUp = false;
    public Queue directions = new Queue();
    public int maxNumberOfBolts = 1;
    public int maxNumberOfDoublePoints = 1;
    public int maxNumberOfChances = 1;

    private Directions lastDir = Directions.North;
    private Directions nextDir = Directions.North;
    private Directions nextDir2 = Directions.North;
    private Directions forcedDir = Directions.NONE;
    private Transform lastBrick;
    private Transform currentBrick;
    private Transform nextBrick;
    private Vector3 nextPosition;
    private int slidesCount = 0;
    private int landsCount = 0;
    private int landsLeftRightCount = 0;
    private int difficulity = 0;
    private ScoreManager scoreManager;
    private PlayerStats playerStats;
    private PartsPool partsPool;
    private PickUpsManager pickUpsManager;
    private int spawnedStraightCount = 0; // In case user turned in the straight path and restarted the game quickly, coroutine will not reset the loop counter
    private IEnumerator startGroundCoroutine;
    private int turnNumber = 0;
    List<NextDistination> landsDistinations = new List<NextDistination>();
    private int numberOfLandsToSpawn = 0;
    private bool allowSpawningLands = false;
    public int spawnedBolts = 0;
    public int spawnedChances = 0;
    private int spawnedStraightForBoltCount = 0;
    private int spawnedPartsWithBolt = 0;
    private bool isStoppingBolt = false;
    private int maxLandsCountTempForBolt = 0;
    public int spawnedDoublePoints = 0;
    private bool keepNextDir = false;

    void Awake()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        playerStats = PlayerStats.Instance;
        partsPool = FindObjectOfType<PartsPool>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        startGroundCoroutine = spawnStartBlocks();

        resetPathValues();
    }

    public void resetPathValues()
    {
        curveSt.eulerAngles = new Vector3(0f, 0f, 0f);
        slide.eulerAngles = new Vector3(0f, 0f, 0f);
        curveUp.eulerAngles = new Vector3(0f, 0f, 0f);

        lastDir = Directions.North;
        nextDir = Directions.North;
        nextDir2 = Directions.North;
        directions.Clear();
        // Debug.Log("Directions cleared!");

        lastBrick = null;
        currentBrick = null;
        nextBrick = null;
        nextPosition = new Vector3(0, 0, 0);
        spawnedStraightCount = 0;
        turnNumber = 0;
        landsDistinations.Clear();
        numberOfLandsToSpawn = 0;
        landsCount = 0;
        landsLeftRightCount = 0;
        allowSpawningLands = false;
        spawnedBolts = 0;
        spawnedChances = 0;
        spawnedStraightForBoltCount = 0;
        spawnedPartsWithBolt = 0;
        isStoppingBolt = false;
        maxLandsCount = 1;
        maxLandsCountTempForBolt = 0;
        allowCurveStAfterCurveUp = false;
        maxSlidesCount = 7;
        spawnedDoublePoints = 0;
        keepNextDir = false;

        currentGamePathParentName = playerStats.getTimesPlayed().ToString();
        currentGamePathParent = new GameObject(currentGamePathParentName).transform;
        currentGamePathParent.parent = pathParent;

        StopCoroutine(startGroundCoroutine);
    }

    void Update()
    {
        if (Utility.startClicked && !Utility.startGroundCreated)
        {
            createStartGround();
            Utility.startGroundCreated = true;
        }
        if (Utility.startClicked && !playerStats.isTutorialsOn())
        {
            int score = scoreManager.getScore();
            if (score < 100)
            {
                maxSlidesCount = 7;
                if (!Utility.boltIsOn)
                    maxLandsCount = 1;
                allowCurveStAfterCurveUp = false;
            }
            else if (score >= 100 && score < 250)
            {
                maxSlidesCount = 5;
                if (!Utility.boltIsOn)
                    maxLandsCount = 2;
                allowCurveStAfterCurveUp = false;
            }
            else if (score >= 250 && score < 500)
            {
                maxSlidesCount = 4;
                if (!Utility.boltIsOn)
                    maxLandsCount = 3;
                allowCurveStAfterCurveUp = true;
            }
        }
        if (playerStats.isTutorialsOn())
        {
            maxSlidesCount = 3;
            maxLandsCount = 1;
            allowCurveStAfterCurveUp = false;
        }
    }

    public void createStartGround()
    {
        startGroundCoroutine = spawnStartBlocks();
        StartCoroutine(startGroundCoroutine);
    }

    IEnumerator spawnStartBlocks()
    {

        for (spawnedStraightCount = 0; spawnedStraightCount < startBlocksCount; spawnedStraightCount++)
        {
            if (spawnedStraightCount < straightPathLength - 1)
            {
                // Instantiate(nextBrick, nextPosition, nextBrick.rotation, currentGamePathParent);
                // nextBrick = partsPool.getPart(Parts.LandStart);
                // nextBrick.position = nextPosition;
                // nextBrick = Z(nextBrick);
                // nextBrick.parent = currentGamePathParent;
                if (spawnedStraightCount == 0)
                {
                    getRandomBrick(Parts.LandStart, Parts.LandStart);
                    currentBrick = nextBrick;
                }
                else
                    getRandomBrick(Parts.LandStraight, Parts.LandStraight);
            }
            else
            {
                spawnBrick();
            }

            // if (i == startBlocksCount - 1)
            //     Utility.gameStarted = true;

            yield return new WaitForSeconds(0.15f);
        }

    }

    Transform X(Transform brick)
    {
        Transform obj = brick;
        obj.transform.rotation = Quaternion.Euler(new Vector3(brick.rotation.eulerAngles.x, 0f, brick.rotation.eulerAngles.z));
        return obj;
    }
    Transform Z(Transform brick)
    {
        Transform obj = brick;
        obj.transform.eulerAngles = new Vector3(brick.rotation.eulerAngles.x, -90f, brick.rotation.eulerAngles.z);
        return obj;
    }
    Transform _X(Transform brick)
    {
        Transform obj = brick;
        obj.transform.rotation = Quaternion.Euler(new Vector3(brick.rotation.eulerAngles.x, 180f, brick.rotation.eulerAngles.z));
        return obj;
    }
    Transform _Z(Transform brick)
    {
        Transform obj = brick;
        obj.transform.rotation = Quaternion.Euler(new Vector3(brick.rotation.eulerAngles.x, 90f, brick.rotation.eulerAngles.z));
        return obj;
    }
    bool isLand(Transform brick)
    {
        if (brick.tag == "LandStart" || brick.tag == "LandStraight" || brick.tag == "LandRight" || brick.tag == "LandLeft")
            return true;
        return false;
    }
    bool isCurveUp(Transform brick)
    {
        if (brick.tag == "CurveUp")
            return true;
        return false;
    }
    bool isCurveSt(Transform brick)
    {
        if (brick.tag == "CurveSt")
            return true;
        return false;
    }
    bool isSlide(Transform brick)
    {
        if (brick.tag == "Slide")
            return true;
        return false;
    }
    float getAngle(Transform brick)
    {
        float angle = 0f;
        if (isSlide(brick))
        {
            angle = (40 * Mathf.PI) / 180f;
        }
        else if (isCurveSt(brick) || isCurveUp(brick))
        {
            angle = (30 * Mathf.PI) / 180f;
        }
        return angle;
    }

    public void spawnStraightWay(int length)
    {
        for (int i = 0; i < length; i++)
        {
            Instantiate(nextBrick, nextPosition, nextBrick.rotation, currentGamePathParent);
            getRandomBrick(Parts.LandStraight, Parts.LandStraight);
        }
    }

    void temp()
    {
        if (forcedDir == Directions.NONE || Mathf.Abs(nextDir2 - forcedDir) == 180)
        {
            do
            {
                nextDir2 = (Directions)(Random.Range(0, 4) * 90);
            } while (Mathf.Abs(nextDir2 - nextDir) == 180 || nextDir2 == nextDir);
        }
        else
        {
            nextDir2 = forcedDir;
            forcedDir = Directions.NONE;
        }

        // if ((nextDir > lastDir && nextDir - lastDir == 90) || nextDir - lastDir == -270)
        // { // Turn right
        // }
        // else if (lastDir != nextDir)
        // {
        // }
        if (landsLeftRightCount == numberOfLandsToSpawn)
        {
            getRandomBrick(Parts.CurveUp, Parts.CurveUp);
            numberOfLandsToSpawn = 0;
        }
        else
            getRandomBrick(Parts.LandStraight, Parts.LandStraight);
    }

    public void spawnBrick()
    {
        // Transform spawnedBrick = Instantiate(nextBrick, nextPosition, nextBrick.rotation, currentGamePathParent);
        // if (Random.Range(0, diamondRange) == 1)
        // {
        //     float diamondHeight = 0;
        //     switch (nextBrick.tag)
        //     {
        //         case "CurveSt":
        //             diamondHeight = 0.1f;
        //             break;
        //         case "CurveUp":
        //             diamondHeight = 1f;
        //             break;
        //         case "Slide":
        //             diamondHeight = 1.35f;
        //             break;
        //         case "Land":
        //             diamondHeight = 0.3f;
        //             break;
        //     }
        //     Instantiate(diamond, nextPosition + new Vector3(0f, diamondHeight, 0f), diamond.rotation).parent = nextBrick;
        // }

        if (isDestroyingOldPath)
            return;

        if (!isStoppingBolt && spawnedPartsWithBolt > 25 && currentBrick.CompareTag("CurveSt"))
        {
            isStoppingBolt = true;
        }

        if (Utility.boltIsOn && spawnedStraightForBoltCount < 5 && isStoppingBolt)
        {
            nextDir2 = nextDir;
            getRandomBrick(Parts.LandStraight, Parts.LandStraight);
            nextBrick.name = Utility.Constants.BOLT_STRAIGHT_PART_NAME;
            spawnedStraightForBoltCount++;
            if (maxLandsCount == 3)
            {
                maxLandsCountTempForBolt = maxLandsCount;
                maxLandsCount = 2;
                numberOfLandsToSpawn = 0;
            }
        }
        else
        {
            if (isLand(currentBrick))
            {
                lastDir = nextDir;
                nextDir = nextDir2;

                if (allowSpawningLands && numberOfLandsToSpawn == 0)
                    numberOfLandsToSpawn = Random.Range(0, maxLandsCount) + 1;

                if (numberOfLandsToSpawn == 3 && landsCount == 2 && landsDistinations.Count == 2 && landsDistinations[0] == landsDistinations[1])
                {
                    do
                    {
                        forcedDir = (Directions)(Random.Range(0, 4) * 90);
                    } while (Mathf.Abs(forcedDir - nextDir2) == 180 || forcedDir == nextDir2);

                    if ((forcedDir > nextDir2 && forcedDir - nextDir2 == 90) || forcedDir - nextDir2 == -270)
                    {
                        landsDistinations.Add(NextDistination.RIGHT);
                    }
                    else
                    {
                        landsDistinations.Add(NextDistination.LEFT);
                    }
                    if (landsDistinations[1] == landsDistinations[2])
                    {
                        print("spawning straight land!");
                        getRandomBrick(Parts.LandStraight, Parts.LandStraight);
                    }
                    else
                    {
                        temp();
                    }
                    // landsDistinations.Clear();
                    // keepNextDir = true;
                }
                else
                {
                    temp();
                }
            }
            else if (isCurveUp(currentBrick))
            {
                if (allowCurveStAfterCurveUp)
                    getRandomBrick(Parts.CurveStraight, Parts.Slide);
                else
                    getRandomBrick(Parts.Slide, Parts.Slide);
            }
            else if (isSlide(currentBrick))
            {
                slidesCount++;
                if (slidesCount >= maxSlidesCount && maxSlidesCount != 0)
                    getRandomBrick(Parts.CurveStraight, Parts.CurveStraight);
                else
                    getRandomBrick(Parts.CurveStraight, Parts.Slide);
            }
            else if (isCurveSt(currentBrick))
            {
                getRandomBrick(Parts.LandStraight, Parts.LandStraight);
            }
            if (Utility.boltIsOn)
            {
                spawnedPartsWithBolt++;
            }
        }

        // if (Random.Range(0, diamondRange) == 1)
        // {
        //     float diamondHeight = 0;
        //     switch (nextBrick.tag)
        //     {
        //         case "CurveSt":
        //             diamondHeight = 0.1f;
        //             break;
        //         case "CurveUp":
        //             diamondHeight = 1f;
        //             break;
        //         case "Slide":
        //             diamondHeight = 1.35f;
        //             break;
        //         case "LandStart":
        //         case "LandStraight":
        //         case "LandRight":
        //         case "LandLeft":
        //             diamondHeight = 0.3f;
        //             break;
        //     }
        //     Instantiate(diamond, nextPosition + new Vector3(0f, diamondHeight, 0f), diamond.rotation).parent = nextBrick;
        // }
    }

    private Transform checkLand(Transform b, PickUpType? pickUpType)
    {
        if (b.gameObject.CompareTag("LandStraight"))
        {
            // Debug.Log("next dir = " + nextDir + ", last dir = " + lastDir);
            if (nextDir2 == nextDir)
                return b;
            if ((nextDir2 > nextDir && nextDir2 - nextDir == 90) || nextDir2 - nextDir == -270)
            { // Turn right
              // Debug.Log("next land is right");
                turnNumber++;
                // Debug.Log("Next direction = " + nextDir + ", Last direction = " + lastDir + " Turn Right" + ", Turn Number = " + turnNumber);
                TurnDirection test;
                test.turnNumber = turnNumber;
                test.nextDistination = NextDistination.RIGHT;
                directions.Enqueue(test);
                landsDistinations.Add(test.nextDistination);
                landsLeftRightCount++;
                if (pickUpType != null)
                    partsPool.setPart(b.GetChild(b.childCount - 1));
                partsPool.setPart(b);
                return partsPool.getPart(Parts.LandRight, pickUpType);
            }
            else
            {
                // Debug.Log("next land is left");
                turnNumber++;
                // Debug.Log("Next direction = " + nextDir + ", Last direction = " + lastDir + " Turn Left" + ", Turn Number = " + turnNumber);
                TurnDirection test;
                test.turnNumber = turnNumber;
                test.nextDistination = NextDistination.LEFT;
                directions.Enqueue(test);
                landsDistinations.Add(test.nextDistination);
                landsLeftRightCount++;
                if (pickUpType != null)
                    partsPool.setPart(b.GetChild(b.childCount - 1));
                partsPool.setPart(b);
                return partsPool.getPart(Parts.LandLeft, pickUpType);
            }
        }

        return b;
    }

    void getRandomBrick(Parts p1, Parts p2)
    {
        int choice = Random.Range(0, 2);
        float horizontalSpace = 0;
        float verticalSpace = 0;
        Parts nextPart;
        if (choice == 0)
        {
            nextPart = p1;
        }
        else
        {
            nextPart = p2;
        }

        int pickUpProbability = Random.Range(0, 100);

        PickUpType? pickUpType = null;
        if (Random.Range(0, chanceProbability) == 1 && spawnedChances < maxNumberOfChances && !Utility.chanceIsOn)
        {
            pickUpType = PickUpType.Chance;
            spawnedChances++;
        }
        else if (Random.Range(0, boltProbability) == 1 && spawnedBolts < maxNumberOfBolts && !Utility.boltIsOn)
        {
            pickUpType = PickUpType.Bolt;
            spawnedBolts++;
        }
        else if (Random.Range(0, doublePointProbability) == 1 && spawnedDoublePoints < maxNumberOfDoublePoints && !Utility.doublePointIsOn)
        {
            pickUpType = PickUpType.DoublePoints;
            spawnedDoublePoints++;
        }
        else if (Random.Range(0, diamondProbability) == 1)
            pickUpType = PickUpType.Diamond;

        nextBrick = partsPool.getPart(nextPart, pickUpType);

        nextBrick = checkLand(nextBrick, pickUpType);

        nextBrick.parent = currentGamePathParent;

        if (!isSlide(nextBrick))
            slidesCount = 0;
        if (!isLand(nextBrick))
        {
            landsCount = 0;
            landsLeftRightCount = 0;
            landsDistinations.Clear();
        }

        if (currentBrick == null)
        {
            getRandomPosition(0, 0);
            return;
        }

        if (isLand(nextBrick))
        {
            landsCount++;
            if (isLand(currentBrick))
            {
                horizontalSpace = 2.5f;
                verticalSpace = 0f;
            }
            else if (isCurveSt(currentBrick))
            {
                horizontalSpace = 2.5f; //2.27191f
                verticalSpace = 0f; //0.192275f
            }
        }
        else if (isSlide(nextBrick))
        {
            if (isSlide(currentBrick))
            {
                horizontalSpace = 2.5f;
                verticalSpace = 1.94097f;
            }
            if (isCurveUp(currentBrick))
            {
                horizontalSpace = 2.5f;
                verticalSpace = 1.50901f;
            }

            //horizontalSpace = 1.91511f; //1.9151111f
            //verticalSpace = 1.28557f; //1.60696902f
        }
        else if (isCurveUp(nextBrick))
        {
            allowSpawningLands = true;
            if (isLand(currentBrick))
            {
                horizontalSpace = 2.5f; // 2.27183
                verticalSpace = 0f; // 0.1929196
            }
            /*else if (isCurveSt(currentBrick))
            {
                horizontalSpace = 2.04374f; //2.04374f
                verticalSpace = 0.385194f; //0.385194f
            }*/
        }
        else if (isCurveSt(nextBrick))
        {
            if (isSlide(currentBrick))
            {
                horizontalSpace = 2.5f;
                verticalSpace = 3.44998f;
            }
            else if (isCurveUp(currentBrick))
            {
                horizontalSpace = 2.5f;
                verticalSpace = 3.01802f;
            }

            //horizontalSpace = 1.89729f; //1.78648f
            //verticalSpace = 1.0489f; //1.5108224f
        }
        float brickSize = 2.499f;
        // Debug.Log("current cos: " + Mathf.Cos(getAngle(currentBrick)) + ", angle = " + getAngle(currentBrick));
        // Debug.Log("next cos: " + Mathf.Cos(getAngle(nextBrick)) + ", angle = " + getAngle(nextBrick));
        //horizontalSpace = ((currentBrick.localScale.x * Mathf.Cos(getAngle(currentBrick))) / 2f) + ((nextBrick.localScale.x * Mathf.Cos(getAngle(nextBrick))) / 2f) - 0.0f;
        //verticalSpace = ((currentBrick.localScale.x * Mathf.Sin(getAngle(currentBrick))) / 2f) + ((nextBrick.localScale.x * Mathf.Sin(getAngle(nextBrick))) / 2f) - 0.0f;

        //Debug.Log("Horizontal = " + horizontalSpace);
        //Debug.Log("Vertical = " + verticalSpace);

        getRandomPosition(horizontalSpace, verticalSpace);
    }

    void getRandomPosition(float x, float y)
    {
        if (nextDir == Directions.North)
        {
            nextPosition = new Vector3(nextPosition.x, nextPosition.y + y, nextPosition.z + x);
            nextBrick = Z(nextBrick);
        }
        else if (nextDir == Directions.East)
        {
            nextPosition = new Vector3(nextPosition.x + x, nextPosition.y + y, nextPosition.z);
            nextBrick = X(nextBrick);
        }
        else if (nextDir == Directions.South)
        {
            nextPosition = new Vector3(nextPosition.x, nextPosition.y + y, nextPosition.z - x);
            nextBrick = _Z(nextBrick);
        }
        else if (nextDir == Directions.West)
        {
            nextPosition = new Vector3(nextPosition.x - x, nextPosition.y + y, nextPosition.z);
            nextBrick = _X(nextBrick);
        }

        // Debug.Log("next position = " + nextPosition);

        nextBrick.position = nextPosition;

        if (nextBrick.gameObject.GetComponentInChildren<Destroyer>() != null)
            nextBrick.gameObject.GetComponentInChildren<Destroyer>().setOriginalPos(nextPosition);

        nextBrick.gameObject.GetComponent<Rigidbody>().useGravity = false;
        nextBrick.gameObject.GetComponent<Rigidbody>().isKinematic = true;

        if (nextBrick.GetChild(0).gameObject.GetComponent<MeshCollider>())
            (nextBrick.GetChild(0).gameObject.GetComponent<MeshCollider>()).enabled = true;

        nextBrick.gameObject.SetActive(true);

        lastBrick = currentBrick;
        currentBrick = nextBrick;
    }

    public bool isDestroyingOldPath = false;

    public void startDestroyingOldPath()
    {
        isDestroyingOldPath = true;
        StartCoroutine(destroyOldPath(pathParent.Find(currentGamePathParentName)));
    }

    private IEnumerator destroyOldPath(Transform oldPathParent)
    {
        // foreach (Transform child in oldPathParent)
        // {
        //     child.Find("Destroyer").gameObject.GetComponent<Destroyer>().destroy();
        //     yield return new WaitForSeconds(0.1f);
        // }
        while (oldPathParent.childCount > 0)
        {
            int index = 0;
            Transform child = oldPathParent.GetChild(index);
            while (child.gameObject.name.Equals(Utility.Constants.DESTROYING_OBJECT_NAME))
            {
                index++;
                if (oldPathParent.childCount > index)
                {
                    child = oldPathParent.GetChild(index);
                }
                else
                {
                    break;
                }
            }
            child.Find("Destroyer").gameObject.GetComponent<Destroyer>().destroy();
            yield return new WaitForSeconds(0.05f);
        }
        isDestroyingOldPath = false;
        if (!Utility.chanceIsOn)
        {
            Destroy(oldPathParent.gameObject);
        }
        yield return null;
    }

    public void startSpawningPathAfterChance(Vector3 startPosition, Directions direction)
    {
        nextPosition = startPosition;
        lastDir = direction;
        nextDir = direction;
        nextDir2 = direction;
        landsDistinations.Clear();
        numberOfLandsToSpawn = 0;
        landsCount = 0;
        landsLeftRightCount = 0;
        allowSpawningLands = false;
        currentBrick = null;
        directions.Clear();
        createStartGround();
    }

    public void boltPickedUp()
    {
        spawnedBolts--;
        if (spawnedBolts < 0)
            spawnedBolts = 0;
    }

    public void boltIsOver()
    {
        spawnedPartsWithBolt = 0;
        spawnedStraightForBoltCount = 0;
        isStoppingBolt = false;
        spawnedBolts = 0;
        maxLandsCount = maxLandsCountTempForBolt;
        maxLandsCountTempForBolt = 0;
    }

    public void doublePointIsOver()
    {
        spawnedDoublePoints = 0;
        Utility.doublePointIsOn = false;
    }

    public void chanceIsOver()
    {
        spawnedChances = 0;
    }

    // private IEnumerator destroyOldPath(Transform oldPathParent)
    // {
    //     while (oldPathParent.childCount > 0)
    //     {
    //         int index = 0;
    //         Transform child = oldPathParent.GetChild(index);
    //         while (child.gameObject.name.Equals(Utility.Constants.DESTROYING_OBJECT_NAME))
    //         {
    //             index++;
    //             if (oldPathParent.childCount > index)
    //             {
    //                 child = oldPathParent.GetChild(index);
    //             }
    //             else
    //             {
    //                 break;
    //             }
    //         }
    //         child.Find("Destroyer").gameObject.GetComponent<Destroyer>().destroy();
    //         yield return new WaitForSeconds(0.1f);
    //     }
    //     Destroy(oldPathParent.gameObject);
    // }
}