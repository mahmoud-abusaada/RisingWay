using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickUpsManager : MonoBehaviour
{

    [SerializeField] private bool manualTimeScale = false;
    private CameraController cameraController;
    private PathMaker pathMaker;
    private ScoreManager scoreManager;
    private PlayerMovement playerMovement;
    private PlayerLinkedObjectsController playerLinkedObjects;
    private InGameUI inGameUI;
    private IEnumerator mDoublePointCoroutine;
    private float myTimeScale = Utility.Constants.DEFAULT_TIME_SCALE;
    private float tempSpeed = 0;
    private SoundManager soundManager;
    // A bolt ends on the straight stretch the path lays for it once its distance has run
    // (PathMaker). Should that ever be missed, it is asked for again after this long - a bolt of
    // the longest distance takes about 15 s of game time.
    private const float BOLT_OVERDUE_SECONDS = 35f;
    private float boltStartedAt;
    private float doublePointsFrom, doublePointsUntil; // real time, as the coroutine waits

    // Start is called before the first frame update
    public void initPickupsManager()
    {
        cameraController = FindObjectOfType<CameraController>();
        pathMaker = FindObjectOfType<PathMaker>();
        scoreManager = FindObjectOfType<ScoreManager>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        playerLinkedObjects = FindObjectOfType<PlayerLinkedObjectsController>();
        inGameUI = FindObjectOfType<InGameUI>();
        soundManager = FindObjectOfType<SoundManager>();
    }

    // [System.Obsolete]
    // IEnumerator Start()
    // {
    //     using (WWW www = new WWW(Application.dataPath + "/Audio/Diamond.mp3"))
    //     {
    //         yield return www;
    //         diamondAudio = www.GetAudioClip();
    //     }
    //     using (WWW www = new WWW(Application.dataPath + "/Audio/Chance.mp3"))
    //     {
    //         yield return www;
    //         chanceAudio = www.GetAudioClip();
    //     }
    //     using (WWW www = new WWW(Application.dataPath + "/Audio/DoublePointsOn.mp3"))
    //     {
    //         yield return www;
    //         doublePointsOnAudio = www.GetAudioClip();
    //     }
    //     using (WWW www = new WWW(Application.dataPath + "/Audio/DoublePointsOff.mp3"))
    //     {
    //         yield return www;
    //         doublePointsOffAudio = www.GetAudioClip();
    //     }
    // }

    // Update is called once per frame
    void Update()
    {
        if (Utility.boltIsOn && Time.time - boltStartedAt > BOLT_OVERDUE_SECONDS)
        {
            boltStartedAt = Time.time; // once per overdue period
            pathMaker.stopBoltSoon();
        }
        if (!manualTimeScale && !Utility.isGamePaused)
        {
            if (Time.timeScale != myTimeScale)
            {
                Time.timeScale += (myTimeScale - Time.timeScale) * Time.deltaTime * 2.5f;
                if (Mathf.Abs(Time.timeScale - myTimeScale) < 0.1f)
                    Time.timeScale = myTimeScale;
            }
        }
    }

    public void activateBolt()
    {
        if (Utility.boltIsOn)
        {
            PlayerStats.Instance.addBolts();
            inGameUI.updatePickUpsCount();
        }

        soundManager.PlayBoltOn();
        soundManager.setPitch();

        // PlayerStats.Instance.setAutoPilotState(true);
        if (!Utility.boltIsOn)
            boltStartedAt = Time.time;
        Utility.boltIsOn = true;
        playerMovement.boltStarted();
        // playerMovement.setBoltTrail();
        playerMovement.speed += 2;
        if (playerMovement.speed > Utility.Constants.TOP_PLAYER_SPEED)
            playerMovement.speed = Utility.Constants.TOP_PLAYER_SPEED;
        inGameUI.addPickedPickUp(PickUpType.Bolt);
        inGameUI.showHintOnce("bolt", "Bolt! Full speed,\nand the ball turns by itself");
        StartCoroutine(delayTimeScale());
        playerLinkedObjects.PlayBoltAmbient();
    }

    private IEnumerator delayTimeScale()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        if (Utility.boltIsOn)
            setTimeScale(3.5f);
        yield return null;
    }

    public void clearSpawnedPickups()
    {
        pathMaker.boltIsOver();
        pathMaker.doublePointsIsOver();
        pathMaker.chanceIsOver();
    }

    public void clearActivePickups(bool withSound = true)
    {
        if (Utility.boltIsOn)
            boltIsOver(withSound);
        if (Utility.doublePointIsOn)
            doublePointsIsOver(withSound);
    }

    /// <summary>
    /// The run was left (Home, Restart): nothing it had carries into the next one - a chance, even
    /// half-used on a fall still under way, and the power-up icons at the top of the screen.
    /// </summary>
    public void clearForNewRun()
    {
        if (inGameUI == null)
            return; // no run yet
        clearActivePickups(false);
        if (Utility.chanceIsOn)
            pathMaker.chanceIsOver();
        Utility.chanceIsOn = false;
        playerMovement.timesRespawnedAfterChancePickedUp = 0;
        inGameUI.clearPickedPickUps();
    }

    /// <summary>The ball is on <paramref name="part"/>: if that is the straight that ends the bolt, it ends.</summary>
    public void checkBoltEnd(Transform part)
    {
        if (Utility.boltIsOn && part != null && part.CompareTag("LandStraight") &&
            part.name == Utility.Constants.BOLT_STRAIGHT_PART_NAME)
            boltIsOver();
    }

    public void boltIsOver(bool withSound = true)
    {
        Utility.boltIsOn = false;
        pathMaker.boltIsOver();
        myTimeScale = Utility.Constants.DEFAULT_TIME_SCALE;
        cameraController.resetFov();

        if (withSound)
            soundManager.PlayBoltOff();
        soundManager.resetPitch();

        // if (!Utility.spawningAfterChance)
        // {
        //     if (Utility.doublePointIsOn)
        //         playerMovement.setDoublePointsTrail();
        //     else
        //         playerMovement.setNormalTrail();
        // }
        playerMovement.speed = scoreManager.getCurrentPlayerSpeed();
        inGameUI.removePickedPickUp(PickUpType.Bolt);
        playerLinkedObjects.StopBoltAmbient();
        SoundManager.Instance.increaseSfxPitch(0);
    }

    public void doublePointsIsOver(bool withSound = true)
    {
        if (mDoublePointCoroutine != null)
            StopCoroutine(mDoublePointCoroutine);

        if (withSound)
            soundManager.PlayDoublePointsOff();
        Utility.doublePointIsOn = false;
        pathMaker.doublePointsIsOver();
        scoreManager.makeScoreBlue();
        // if (!Utility.boltIsOn && !Utility.spawningAfterChance)
        //     playerMovement.setNormalTrail();
        inGameUI.removePickedPickUp(PickUpType.DoublePoints);
        playerLinkedObjects.StopDoublePointsAmbient();
    }

    private void setTimeScale(float timeScale)
    {
        myTimeScale = timeScale;
        cameraController.setFov(75);
    }

    public void activateDoublePoint()
    {
        if (Utility.doublePointIsOn)
        {
            PlayerStats.Instance.addDoublePoints();
            inGameUI.updatePickUpsCount();
        }
        soundManager.PlayDoublePointsOn();
        mDoublePointCoroutine = doublePointCoroutine();
        StartCoroutine(mDoublePointCoroutine);
        // playerMovement.setDoublePointsTrail();
        inGameUI.addPickedPickUp(PickUpType.DoublePoints);
        inGameUI.showHintOnce("double", "Double points!\nEverything counts twice for a while");
        playerLinkedObjects.PlayDoublePointsAmbient();
    }

    private IEnumerator doublePointCoroutine()
    {
        Utility.doublePointIsOn = true;
        scoreManager.makeScoreYellow();
        doublePointsFrom = Time.realtimeSinceStartup;
        doublePointsUntil = doublePointsFrom + Utility.getDoublePointsPeriod();
        yield return new WaitForSecondsRealtime(Utility.getDoublePointsPeriod());
        doublePointsIsOver();
    }

    /// <summary>How much of the double points is left, 1 to 0 (the power-up button's ring).</summary>
    public float doublePointsLeft()
    {
        if (!Utility.doublePointIsOn || doublePointsUntil <= doublePointsFrom)
            return 0f;
        return Mathf.Clamp01((doublePointsUntil - Time.realtimeSinceStartup) / (doublePointsUntil - doublePointsFrom));
    }

    public void activateChance()
    {
        if (Utility.chanceIsOn)
        {
            PlayerStats.Instance.addChances();
            inGameUI.updatePickUpsCount();
        }
        soundManager.PlayChanceOn();
        playerMovement.timesRespawnedAfterChancePickedUp = 0;
        Utility.chanceIsOn = true;
        // playerMovement.setNormalTrail();
        inGameUI.addPickedPickUp(PickUpType.Chance);
        inGameUI.showHintOnce("chance", "A chance!\nIf you fall, it puts you back on the track");
    }

    public void diamondPickedUp()
    {
        scoreManager.diamondPicked();
        soundManager.PlayDiamond();
        inGameUI.showHintOnce("diamond", "Diamonds buy new balls\nand tracks in the shop");
    }

    public void boxPickedUp()
    {
        PlayerStats.Instance.addBoxes();
        soundManager.PlayBox();
        inGameUI.showHintOnce("box", "A mystery box!\nOpen it after the run");
    }
}
