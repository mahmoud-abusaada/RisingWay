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
        Utility.boltIsOn = true;
        // playerMovement.setBoltTrail();
        playerMovement.speed += 2;
        if (playerMovement.speed > Utility.Constants.TOP_PLAYER_SPEED)
            playerMovement.speed = Utility.Constants.TOP_PLAYER_SPEED;
        inGameUI.addPickedPickUp(PickUpType.Bolt);
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
        playerLinkedObjects.PlayDoublePointsAmbient();
    }

    private IEnumerator doublePointCoroutine()
    {
        Utility.doublePointIsOn = true;
        scoreManager.makeScoreYellow();
        yield return new WaitForSecondsRealtime(Utility.getDoublePointsPeriod());
        doublePointsIsOver();
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
    }

    public void diamondPickedUp()
    {
        scoreManager.diamondPicked();
        soundManager.PlayDiamond();
    }

    public void boxPickedUp()
    {
        PlayerStats.Instance.addBoxes();
        soundManager.PlayBox();
    }
}
