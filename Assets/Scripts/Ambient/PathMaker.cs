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

    // Turn patterns unlock in tiers as the score grows (unlockPatternsUpTo). Two set pieces sit
    // alongside the land patterns: a spiral - four turns the same way, winding the path up around
    // itself - and short climbs, a few legs in a row that go straight from the curve up into the
    // curve out, for a quicker rhythm of turns.
    public const int TOP_PATTERN_TIER = 5;
    private const int SPIRAL_TURNS = 4;
    private const int SHORT_CLIMB_LEGS = 3;
    // Every pick-up sits this far above the track: the height the flat parts always had, with the
    // diamond's point just touching it. The others sat higher (slide 0.03, curve up 0.11, curve out
    // 0.29), and height decides how far off the centre line the ball can be and still collect it:
    // its centre rolls 0.25 above the track and a pick-up's trigger reaches 0.45 from its own
    // centre, 0.45 above the pick-up. At this height that is 0.68 of room, against 0.50 on the
    // curve out - the one that was being missed.
    private const float PICKUP_HEIGHT = -0.05f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly HashSet<string> pickUpHeightsReported = new HashSet<string>();
#endif

    private int patternTier;
    private Transform pathBeingDestroyed;
    private bool spiralsUnlocked, shortClimbsUnlocked;
    private int spiralTurnsLeft, shortClimbsLeft;
    private Parts spiralTurn;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Development builds: with a file called "try-patterns" in the game's files folder
    // (adb shell touch /sdcard/Android/data/com.abusaada.risingway/files/try-patterns) every run
    // has every pattern from the start, to try them without playing up to a score of 500.
    // MovementProbe sets it with -probeAllPatterns.
    public static bool PreviewAllPatterns;
#endif

    void Awake()
    {
        partsPool = FindObjectOfType<PartsPool>();
        playerStats = PlayerStats.Instance;
        scoreManager = FindObjectOfType<ScoreManager>();
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        PreviewAllPatterns = System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "try-patterns"));
        if (PreviewAllPatterns)
            Debug.Log("[PathMaker] try-patterns: every turn pattern from the start of each run");
#endif
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
        patternTier = 0;
        spiralsUnlocked = shortClimbsUnlocked = false;
        spiralTurnsLeft = shortClimbsLeft = 0;

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

    private const float START_BLOCK_SECONDS = 0.15f;

    IEnumerator spawnStartBlocks()
    {
        float started = Time.time;
        for (spawnedStraightCount = 0; spawnedStraightCount < startBlocksCount; spawnedStraightCount++)
        {
            // Each part at its own time, several in one frame if frames are slow, so a slow phone
            // does not stretch the sequence and leave the path behind the ball.
            while (Time.time - started < spawnedStraightCount * START_BLOCK_SECONDS)
                yield return null;

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
        }
    }

    public void spawnPart()
    {
        // Nothing to add to a path that is being taken down. A revive builds a new path while the
        // old one may still be going, and that one must get all its parts.
        if (isDestroyingOldPath && currentGamePathParent == pathBeingDestroyed)
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
                    pickedLandsPattern = pickLandPattern();

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
                    if (shortClimbsLeft > 0)
                    {
                        // A short climb: straight from the curve up into the curve out.
                        shortClimbsLeft--;
                        spawnPart(Parts.CurveStraight);
                    }
                    else if (allowCurveStAfterCurveUp)
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

    // The next turn, or turns: one of the unlocked land patterns, picked at random as before - or
    // the next turn of a spiral.
    private Queue pickLandPattern()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (PreviewAllPatterns && patternTier < TOP_PATTERN_TIER && !playerStats.isTutorialsOn())
            unlockPatternsUpTo(TOP_PATTERN_TIER);
#endif
        bool tutorials = playerStats.isTutorialsOn();
        if (spiralTurnsLeft > 0)
        {
            spiralTurnsLeft--;
            return new Queue(new ArrayList { spiralTurn });
        }
        if (spiralsUnlocked && !tutorials && Random.Range(0, 8) == 0)
        {
            spiralTurn = Random.Range(0, 2) == 0 ? Parts.LandRight : Parts.LandLeft;
            spiralTurnsLeft = SPIRAL_TURNS - 1;
            // Not with short climbs: the loops would stack only 12 units apart.
            shortClimbsLeft = 0;
            return new Queue(new ArrayList { spiralTurn });
        }
        if (shortClimbsUnlocked && !tutorials && shortClimbsLeft == 0 && Random.Range(0, 8) == 0)
            shortClimbsLeft = SHORT_CLIMB_LEGS;

        List<string> keys = new List<string>();
        foreach (DictionaryEntry entry in landPatterns)
            keys.Add(entry.Key as string);
        return new Queue((ArrayList)landPatterns[keys[Random.Range(0, keys.Count)]]);
    }

    /// <summary>
    /// Adds the turn patterns of every tier up to <paramref name="tier"/> (ScoreManager: score over
    /// 110, 200, 300, 400, 500). R and L are turn parts, S a flat straight part. The ones marked
    /// "new" came with the September 2026 playtest; the rest are the game's original ladder.
    /// </summary>
    public void unlockPatternsUpTo(int tier)
    {
        while (patternTier < tier)
        {
            patternTier++;
            Debug.Log("Path maker update " + patternTier);
            switch (patternTier)
            {
                case 1:
                    addPatterns("R-S-R", "L-S-L", "R-S-L", "L-S-R");
                    addPatterns("S-S-R", "S-S-L"); // new: a flat run, then the turn
                    shortClimbsUnlocked = true;    // new
                    break;
                case 2:
                    addPatterns("R-S-R-S-R", "L-S-L-S-L", "R-S-R-S-L", "L-S-L-S-R");
                    addPatterns("R-S-S-L", "L-S-S-R"); // new: a wide sidestep
                    spiralsUnlocked = true;            // new
                    break;
                case 3:
                    foreach (string retired in new[] { "R-S-R", "L-S-L", "R-S-L", "L-S-R" })
                        landPatterns.Remove(retired);
                    addPatterns("R-R", "L-L", "R-L", "L-R");
                    break;
                case 4:
                    addPatterns("R-R-S-R", "L-L-S-L");
                    break;
                case 5:
                    addPatterns("R-R-L", "L-L-R", "R-L-R", "L-R-L");
                    addPatterns("R-L-R-L", "L-R-L-R"); // new: a long slalom
                    break;
            }
        }
    }

    private void addPatterns(params string[] names)
    {
        foreach (string name in names)
        {
            ArrayList parts = new ArrayList();
            foreach (string p in name.Split('-'))
                parts.Add(p == "R" ? Parts.LandRight : p == "L" ? Parts.LandLeft : Parts.LandStraight);
            landPatterns[name] = parts;
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
        placePickUpAboveSurface(part);
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

    // Every pick-up the same height above the track it sits on. PartsPool places it a fixed height
    // above the part's pivot, which is a different height above the surface on each shape - on the
    // curve out it sat high enough that a ball running a little off the centre line passed under it.
    private void placePickUpAboveSurface(Transform part)
    {
        Transform pickUp = part.Find("PickUp");
        if (pickUp == null)
            return;

        Physics.SyncTransforms(); // the part has just been moved into place
        Vector3 p = pickUp.position;
        RaycastHit hit;
        // Only this part's own surface counts: the ball, or a part of a lower lap of a spiral, can
        // be under the ray as well.
        if (!Physics.Raycast(new Vector3(p.x, part.position.y + 5f, p.z), Vector3.down, out hit, 10f,
                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            || !hit.collider.transform.IsChildOf(part))
            return;

        pickUp.position = new Vector3(p.x, hit.point.y + PICKUP_HEIGHT, p.z);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (pickUpHeightsReported.Add(part.tag))
            Debug.Log("[PickUp] on " + part.tag + ": was " + (p.y - hit.point.y).ToString("F2") +
                      " above the track, now " + PICKUP_HEIGHT.ToString("F2"));
#endif
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

    public bool isOnCurrentPath(Transform part)
    {
        return currentGamePathParent != null && part.parent == currentGamePathParent;
    }

    public void startDestroyingOldPath()
    {
        isDestroyingOldPath = true;
        Transform oldPathParent = currentGamePathParent;
        pathBeingDestroyed = oldPathParent;
        oldPathParent.name += " - Destroying Old Path";
        StartCoroutine(destroyOldPath(oldPathParent));
    }

    public void createPathParent()
    {
        currentGamePathParent = new GameObject(currentGamePathParentName).transform;
        currentGamePathParent.parent = pathParent;
    }

    // Knocks each part of the old path down once, one every 0.05 s, then gives them a few seconds
    // to fall away (Destroyer pools a part 50 below where it stood) and pools any still left.
    //
    // It used to loop until the path was empty, calling destroy() again, every 0.05 s, on parts
    // that were already falling - which, with the ball back in play, wiped the new path (see
    // Destroyer.destroy) - and it could not finish while any part was held up.
    private const float OLD_PATH_FALL_SECONDS = 4f;

    private IEnumerator destroyOldPath(Transform oldPathParent)
    {
        List<Transform> parts = new List<Transform>();
        foreach (Transform child in oldPathParent)
            parts.Add(child);

        foreach (Transform part in parts)
        {
            if (part == null || part.parent != oldPathParent || part.name.Equals(Utility.Constants.DESTROYING_OBJECT_NAME))
                continue;
            Destroyer destroyer = part.GetComponentInChildren<Destroyer>();
            if (destroyer == null)
                continue;
            destroyer.destroy();
            yield return new WaitForSeconds(0.05f);
        }

        for (float waited = 0f; oldPathParent.childCount > 0 && waited < OLD_PATH_FALL_SECONDS; waited += Time.deltaTime)
            yield return null;

        while (oldPathParent.childCount > 0)
        {
            Transform left = oldPathParent.GetChild(0);
            Destroyer destroyer = left.GetComponentInChildren<Destroyer>();
            if (destroyer != null)
                destroyer.returnToPool();
            else
                partsPool.setPart(left);
            if (left.parent == oldPathParent)
                left.SetParent(null); // never loop on something the pool would not take
        }

        isDestroyingOldPath = false;
        Destroy(oldPathParent.gameObject);
    }

    /// <summary>
    /// Whether the first <paramref name="count"/> parts of the current path have finished
    /// dropping into place (MoveDown removes itself when its part lands).
    /// </summary>
    public bool firstPartsHaveLanded(int count)
    {
        if (currentGamePathParent == null)
            return false;
        int landed = 0;
        foreach (Transform part in currentGamePathParent)
        {
            if (part.name == Utility.Constants.DESTROYING_OBJECT_NAME)
                continue;
            if (part.GetComponent<MoveDown>() != null)
                return false;
            if (++landed >= count)
                return true;
        }
        return false; // not that many spawned yet
    }

    public void startSpawningPathAfterChance(Vector3 startPosition, Directions direction)
    {
        lastSpawnedPartPosition = startPosition;
        pathDirection = direction;
        lastSpawnedPart = null;
        directions.Clear();
        pickedLandsPattern = null;
        // The revived ball rolls on before this path is all down: its parts hurry to land ahead of
        // it until the last one is in (placed over startBlocksCount x 0.15 s, 0.5 s to drop).
        MoveDown.HurryUntil = Time.time + startBlocksCount * START_BLOCK_SECONDS + 1f;
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