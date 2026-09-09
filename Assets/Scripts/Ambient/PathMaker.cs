using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PathMaker : MonoBehaviour
{
    [SerializeField] private Transform pathParent;
    public int startBlocksCount = 8;
    public int straightPathLength = 5;
    public int maxSlidesCount = 7;
    public bool allowCurveStAfterCurveUp = false;
    public int diamondProbability = 4;
    public int boltProbability = 50;
    public int doublePointProbability = 50;
    public int chanceProbability = 3;
    public int boxProbability = 100;
    public int maxNumberOfBolts = 1;
    public int maxNumberOfDoublePoints = 1;
    public int maxNumberOfChances = 1;
    public bool isDestroyingOldPath = false;
    public Queue directions = new Queue();
    public TurnDirection? nextDirection;
    public Shader litShader;
    public Shader fadeShader;

    public Hashtable landPatterns = new Hashtable();
    private Queue pickedLandsPattern = null;
    private Transform currentGamePathParent;
    private string currentGamePathParentName;
    private IEnumerator startGroundCoroutine;
    private int spawnedStraightCount = 0; // In case user turned in the straight path and restarted the game quickly, coroutine will not reset the loop counter
    private int spawnedBolts = 0;
    private int spawnedDoublePoints = 0;
    private int spawnedChances = 0;
    private int spawnedPartsWithBolt = 0;
    private int spawnedStraightForBoltCount = 0;
    private int turnNumber = 0;
    private int slidesCount = 0;
    private bool isStoppingBolt = false;
    private Transform lastSpawnedPart = null;
    private Vector3 lastSpawnedPartPosition = new Vector3(0, 0, 0);
    private Transform nextPart = null;
    private PartsPool partsPool;
    private PlayerStats playerStats;
    private ScoreManager scoreManager;
    private Directions pathDirection = Directions.North;
    private int turnsSpawned = 0;
    private int spawnedSlidesForTutorials = 0;

    void Awake()
    {
        partsPool = FindObjectOfType<PartsPool>();
        playerStats = PlayerStats.Instance;
        scoreManager = FindObjectOfType<ScoreManager>();
        startGroundCoroutine = spawnStartBlocks();

        // landPatterns.Add(landPatterns.Count, new ArrayList { Parts.LandRight });
        // landPatterns.Add(landPatterns.Count, new ArrayList { Parts.LandLeft });

        // // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandRight });
        // // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandLeft });

        // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandRight });
        // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandLeft });

        // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandLeft });
        // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandRight });

        // // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandRight, Parts.LandStraight, Parts.LandRight });
        // // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandLeft, Parts.LandStraight, Parts.LandLeft });

        // // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandRight, Parts.LandStraight, Parts.LandLeft });
        // // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandLeft, Parts.LandStraight, Parts.LandRight });

        // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandRight, Parts.LandStraight, Parts.LandRight });
        // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandLeft, Parts.LandStraight, Parts.LandLeft });

        // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandRight, Parts.LandLeft });
        // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandLeft, Parts.LandRight });

        // landPatterns.Add(i++, new ArrayList { Parts.LandRight, Parts.LandLeft, Parts.LandRight });
        // landPatterns.Add(i++, new ArrayList { Parts.LandLeft, Parts.LandRight, Parts.LandLeft });

        resetPathValues();
    }

    public void resetPathValues()
    {
        pathDirection = Directions.North;
        lastSpawnedPart = null;
        nextPart = null;
        lastSpawnedPartPosition = new Vector3(0, 0, 0);
        spawnedStraightCount = 0;
        turnNumber = 0;
        spawnedBolts = 0;
        spawnedChances = 0;
        spawnedStraightForBoltCount = 0;
        spawnedPartsWithBolt = 0;
        isStoppingBolt = false;
        spawnedDoublePoints = 0;
        directions.Clear();
        pickedLandsPattern = null;
        turnsSpawned = 0;
        spawnedSlidesForTutorials = 0;

        landPatterns.Clear();

        landPatterns.Add("R", new ArrayList { Parts.LandRight });
        landPatterns.Add("L", new ArrayList { Parts.LandLeft });

        currentGamePathParentName = playerStats.getTimesPlayed().ToString();
        currentGamePathParent = new GameObject(currentGamePathParentName).transform;
        currentGamePathParent.parent = pathParent;

        diamondsToSpawn = 0;
        numberOfPartsWithoutPickups = 0;
        pickupIndex = 0;

        StopCoroutine(startGroundCoroutine);
    }

    void Update()
    {
        if (Utility.startClicked && !Utility.startGroundCreated)
        {
            createStartGround();
            Utility.startGroundCreated = true;
        }
    }

    private int fixedUpdatesCount = 0;

    // void FixedUpdate()
    // {
    //     if (Utility.gameStarted)
    //     {
    //         fixedUpdatesCount++;
    //         if (fixedUpdatesCount == 20)
    //             updatePartsRenderQueue();
    //     }
    // }

    public void setFirstTurnAsNextDirection()
    {
        if (currentGamePathParent != null)
        {
            for (int i = 0; i < currentGamePathParent.childCount; i++)
            {
                Transform child = currentGamePathParent.GetChild(i);

                if (child.name.Equals(Utility.Constants.DESTROYING_OBJECT_NAME))
                    continue;

                if (child.CompareTag("LandLeft"))
                {
                    TurnDirection newTurn;
                    newTurn.turnNumber = turnNumber;
                    newTurn.nextDistination = NextDistination.LEFT;
                    nextDirection = newTurn;
                    // Debug.Log("next first direction = " + nextDirection?.nextDistination);
                    break;
                }
                else if (child.CompareTag("LandRight"))
                {
                    TurnDirection newTurn;
                    newTurn.turnNumber = turnNumber;
                    newTurn.nextDistination = NextDistination.RIGHT;
                    nextDirection = newTurn;
                    // Debug.Log("next first direction = " + nextDirection?.nextDistination);
                    break;
                }
            }
        }
    }

    public void createStartGround()
    {
        if (startGroundCoroutine != null)
            StopCoroutine(startGroundCoroutine);
        startGroundCoroutine = spawnStartBlocks();
        StartCoroutine(startGroundCoroutine);
    }

    IEnumerator spawnStartBlocks()
    {
        for (spawnedStraightCount = 0; spawnedStraightCount < startBlocksCount; spawnedStraightCount++)
        {
            if (spawnedStraightCount < straightPathLength - 1)
                if (spawnedStraightCount == 0)
                    spawnPart(Parts.LandStart, false);
                else
                    spawnPart(Parts.LandStraight, false);
            else
                spawnPart();

            // if (i == startBlocksCount - 1)
            //     Utility.gameStarted = true;

            updatePartsRenderQueue();

            yield return new WaitForSeconds(0.15f);
        }
    }

    public void spawnPart()
    {
        if (isDestroyingOldPath)
            return;

        if (!isStoppingBolt && spawnedPartsWithBolt > Utility.getBoltDistance() && lastSpawnedPart.CompareTag("CurveSt"))
        {
            isStoppingBolt = true;
        }

        if (Utility.boltIsOn && spawnedStraightForBoltCount < 5 && isStoppingBolt)
        {
            spawnPart(Parts.LandStraight);
            if (spawnedStraightForBoltCount == 0)
                nextPart.name = Utility.Constants.BOLT_STRAIGHT_PART_NAME;
            spawnedStraightForBoltCount++;
        }
        else
        {
            if (isCurveSt(lastSpawnedPart) || (pickedLandsPattern != null && pickedLandsPattern.Count > 0))
            {
                if (pickedLandsPattern == null || pickedLandsPattern.Count == 0)
                {
                    List<string> keys = new List<string>();
                    foreach (DictionaryEntry entry in landPatterns)
                    {
                        keys.Add(entry.Key as string);
                    }
                    pickedLandsPattern = new Queue((ArrayList)landPatterns[keys[Random.Range(0, keys.Count)]]);
                }

                spawnPart((Parts)pickedLandsPattern.Dequeue());
                if (PlayerStats.Instance.isTutorialsOn())
                    turnsSpawned++;
            }
            else
            {
                if (isLand(lastSpawnedPart))
                {
                    spawnPart(Parts.CurveUp);
                }
                else if (isCurveUp(lastSpawnedPart))
                {
                    if (allowCurveStAfterCurveUp)
                        pickPart(Parts.CurveStraight, Parts.Slide);
                    else
                        spawnPart(Parts.Slide);
                }
                else if (isSlide(lastSpawnedPart))
                {
                    if (turnsSpawned == 2 && spawnedSlidesForTutorials < 3)
                    {
                        spawnPart(Parts.Slide);
                        spawnedSlidesForTutorials++;
                    }
                    else
                    {
                        int maxSlides = playerStats.isTutorialsOn() ? 1 : maxSlidesCount;
                        if (slidesCount >= maxSlides && maxSlides != 0)
                            spawnPart(Parts.CurveStraight);
                        else
                            pickPart(Parts.CurveStraight, Parts.Slide);
                    }
                }
            }
        }
    }

    private void pickPart(Parts part1, Parts part2)
    {
        if (Random.Range(0, 2) == 0)
        {
            spawnPart(part1);
        }
        else
        {
            spawnPart(part2);
        }
    }

    // int diamondsCount = 0;

    private int diamondsToSpawn = 0;
    private int numberOfPartsWithoutPickups = 0;
    private int pickupIndex = 0;

    private void spawnPart(Parts targetPart, bool withPickUp = true)
    {
        PickUpType? pickUpType = null;
        if (withPickUp && !PlayerStats.Instance.isTutorialsOn())
        {
            if (diamondsToSpawn > 0)
            {
                pickUpType = PickUpType.Diamond;
                diamondsToSpawn--;
            }
            else if (numberOfPartsWithoutPickups > 0)
            {
                numberOfPartsWithoutPickups--;
                if (numberOfPartsWithoutPickups == pickupIndex)
                {
                    if (Random.Range(0, 30) == 1)
                    {
                        pickUpType = PickUpType.MysteryBox;
                    }
                    else if (Random.Range(0, 15) == 1 && spawnedChances < maxNumberOfChances && !Utility.chanceIsOn)
                    {
                        pickUpType = PickUpType.Chance;
                        spawnedChances++;
                    }
                    else if (Random.Range(0, 7) == 1 && spawnedBolts < maxNumberOfBolts && !Utility.boltIsOn)
                    {
                        pickUpType = PickUpType.Bolt;
                        spawnedBolts++;
                    }
                    else if (Random.Range(0, 5) == 1 && spawnedDoublePoints < maxNumberOfDoublePoints && !Utility.doublePointIsOn)
                    {
                        pickUpType = PickUpType.DoublePoints;
                        spawnedDoublePoints++;
                    }
                }
            }
            else
            {
                diamondsToSpawn = Random.Range(7, 15);
                numberOfPartsWithoutPickups = Random.Range(7, 17);
                pickupIndex = numberOfPartsWithoutPickups / 2;
            }

            // if (Random.Range(0, chanceProbability) == 1 && spawnedChances < maxNumberOfChances && !Utility.chanceIsOn)
            // {
            //     pickUpType = PickUpType.Chance;
            //     spawnedChances++;
            // }
            // else if (Random.Range(0, boltProbability) == 1 && spawnedBolts < maxNumberOfBolts && !Utility.boltIsOn)
            // {
            //     pickUpType = PickUpType.Bolt;
            //     spawnedBolts++;
            // }
            // else if (Random.Range(0, doublePointProbability) == 1 && spawnedDoublePoints < maxNumberOfDoublePoints && !Utility.doublePointIsOn)
            // {
            //     pickUpType = PickUpType.DoublePoints;
            //     spawnedDoublePoints++;
            // }
            // else if (Random.Range(0, boxProbability) == 1)
            // {
            //     pickUpType = PickUpType.MysteryBox;
            // }
            // else if (Random.Range(0, diamondProbability) == 1)
            // {
            //     pickUpType = PickUpType.Diamond;
            //     // diamondsCount++;
            //     // Debug.Log("Spawning a diamond, count = " + diamondsCount);
            // }
        }

        Transform part = partsPool.getPart(targetPart, pickUpType);

        if (targetPart == Parts.LandLeft || targetPart == Parts.LandRight)
        {
            if (PlayerStats.Instance.isTutorialsOn() && turnsSpawned < 2)
            {
                part.Find("Flag").gameObject.SetActive(true);
            }
            else
            {
                part.Find("Flag").gameObject.SetActive(false);
            }
        }

        part.GetComponent<MoveDown>().setPreviousPart(nextPart);

        nextPart = part;

        part.parent = currentGamePathParent;

        // updatePartsRenderQueue();
        // setPartMaterial(part.Find("Mesh"));
        // setPartMaterial(part.Find("PartStartBlock"));
        // setPartMaterial(part.Find("PartEndBlock"));

        if (Utility.boltIsOn)
        {
            spawnedPartsWithBolt++;
        }

        setPartPosition(part);
    }

    int index = 1;
    int notDestroyingIndex = 0;
    bool isFade = true;
    public void updatePartsRenderQueue()
    {
        fixedUpdatesCount = 0;
        index = 1;
        notDestroyingIndex = 0;
        isFade = true;
        foreach (Transform part in currentGamePathParent)
        {
            if (part.name != Utility.Constants.DESTROYING_OBJECT_NAME)
                notDestroyingIndex++;

            if (notDestroyingIndex == 0 || notDestroyingIndex == 1 || notDestroyingIndex == 2 || !Utility.playerIsInPosition)
                isFade = false;
            else if (notDestroyingIndex > 2)
                isFade = true;

            setPartMaterial(part.Find("Mesh"), isFade, 2000 + index + 1);
            setPartMaterial(part.Find("PartStartBlock"), isFade, 2000 + index - 1);
            setPartMaterial(part.Find("PartEndBlock"), isFade, 2000 + index - 1);

            if (part.Find("PickUp") != null)
                part.Find("PickUp").GetComponentInChildren<Renderer>().material.renderQueue = 2000 + index;
            index++;
        }
    }

    private void setPartMaterial(Transform part, bool isFade = true, int renderQueue = -1)
    {
        if (part != null)
            if (isFade)
                part.GetComponent<Renderer>().material = MaterialsManager.Instance.getFadeMaterial(part.GetComponent<Renderer>().material, renderQueue);
            else
                part.GetComponent<Renderer>().material = MaterialsManager.Instance.getLitMaterial(part.GetComponent<Renderer>().material, renderQueue);
    }

    private void setPartPosition(Transform part)
    {
        float x = 0;
        float y = 0;

        if (lastSpawnedPart == null)
        {
            x = y = 0;
        }
        else
        {
            if (!isSlide(nextPart))
                slidesCount = 0;

            if (isLand(part))
            {
                if (isLand(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 0f;
                }
                else if (isCurveSt(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 0f;
                }
            }
            else if (isSlide(nextPart))
            {
                slidesCount++;
                if (isSlide(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 1.94097f;
                }
                if (isCurveUp(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 1.50901f;
                }
            }
            else if (isCurveUp(nextPart))
            {
                if (isLand(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 0f;
                }
            }
            else if (isCurveSt(nextPart))
            {
                if (isSlide(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 3.44998f;
                }
                else if (isCurveUp(lastSpawnedPart))
                {
                    x = 2.5f;
                    y = 3.01802f;
                }
            }
        }

        if (pathDirection == Directions.North)
        {
            lastSpawnedPartPosition = new Vector3(lastSpawnedPartPosition.x, lastSpawnedPartPosition.y + y, lastSpawnedPartPosition.z + x);
            part = Z(part);
        }
        else if (pathDirection == Directions.East)
        {
            lastSpawnedPartPosition = new Vector3(lastSpawnedPartPosition.x + x, lastSpawnedPartPosition.y + y, lastSpawnedPartPosition.z);
            part = X(part);
        }
        else if (pathDirection == Directions.South)
        {
            lastSpawnedPartPosition = new Vector3(lastSpawnedPartPosition.x, lastSpawnedPartPosition.y + y, lastSpawnedPartPosition.z - x);
            part = _Z(part);
        }
        else if (pathDirection == Directions.West)
        {
            lastSpawnedPartPosition = new Vector3(lastSpawnedPartPosition.x - x, lastSpawnedPartPosition.y + y, lastSpawnedPartPosition.z);
            part = _X(part);
        }

        part.position = lastSpawnedPartPosition;

        if (part.gameObject.GetComponentInChildren<Destroyer>() != null)
            part.gameObject.GetComponentInChildren<Destroyer>().setOriginalPos(lastSpawnedPartPosition);

        part.gameObject.GetComponent<Rigidbody>().useGravity = false;
        part.gameObject.GetComponent<Rigidbody>().isKinematic = true;

        if (part.GetChild(0).gameObject.GetComponent<MeshCollider>())
            (part.GetChild(0).gameObject.GetComponent<MeshCollider>()).enabled = true;

        part.gameObject.SetActive(true);

        if (part.tag == "LandLeft")
        {
            pathDirection -= 90;

            if ((int)pathDirection < 0)
                pathDirection += 360;

            TurnDirection newTurn;
            newTurn.turnNumber = turnNumber;
            newTurn.nextDistination = NextDistination.LEFT;
            directions.Enqueue(newTurn);
            part.GetComponentInChildren<Destroyer>().setNextDirection(newTurn);
        }
        else if (part.tag == "LandRight")
        {
            pathDirection += 90;

            if ((int)pathDirection > 270)
                pathDirection -= 360;

            TurnDirection newTurn;
            newTurn.turnNumber = turnNumber;
            newTurn.nextDistination = NextDistination.RIGHT;
            directions.Enqueue(newTurn);
            part.GetComponentInChildren<Destroyer>().setNextDirection(newTurn);
        }

        lastSpawnedPart = part;
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

    public void destroySkippedParts(Transform target)
    {
        if (currentGamePathParent != null)
        {
            for (int i = 0; i < currentGamePathParent.childCount; i++)
            {
                Transform child = currentGamePathParent.GetChild(i);
                if (child.Equals(target))
                    break;

                if (!child.name.Equals(Utility.Constants.DESTROYING_OBJECT_NAME))
                {
                    spawnPart();
                    child.GetComponentInChildren<Destroyer>().destroy();
                }
            }
        }
    }

    public void startDestroyingOldPath()
    {
        isDestroyingOldPath = true;
        Transform oldPathParent = currentGamePathParent;
        oldPathParent.name += " - Destroying Old Path";
        StartCoroutine(destroyOldPath(oldPathParent));
    }

    public void createPathParent()
    {
        currentGamePathParent = new GameObject(currentGamePathParentName).transform;
        currentGamePathParent.parent = pathParent;
    }

    private IEnumerator destroyOldPath(Transform oldPathParent)
    {
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
            child.GetComponentInChildren<Destroyer>().destroy();
            yield return new WaitForSeconds(0.05f);
        }
        isDestroyingOldPath = false;
        Destroy(oldPathParent.gameObject);
        yield return null;
    }

    public void startSpawningPathAfterChance(Vector3 startPosition, Directions direction)
    {
        lastSpawnedPartPosition = startPosition;
        pathDirection = direction;
        lastSpawnedPart = null;
        directions.Clear();
        pickedLandsPattern = null;
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
    }

    public void doublePointsIsOver()
    {
        spawnedDoublePoints = 0;
    }

    public void chanceIsOver()
    {
        spawnedChances = 0;
    }
}