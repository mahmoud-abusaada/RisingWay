using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InGameUI : MonoBehaviour
{
    [SerializeField] private RectTransform pickedPickUps;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TMP_ColorGradient blueTextGradient;
    [SerializeField] private TMP_ColorGradient yellowTextGradient;
    [SerializeField] private ParticleSystem plusOneEffect;
    [SerializeField] private ParticleSystem plusTwoEffect;
    [SerializeField] private ParticleSystem plusTwoDoublePointsEffect;
    [SerializeField] private ParticleSystem plusFourDoublePointsEffect;
    [SerializeField] private RectTransform singleTap;
    [SerializeField] private RectTransform tapLeftRight;
    [SerializeField] private RectTransform swipeLeftRight;
    [SerializeField] private RectTransform tutorialsContainer;
    [SerializeField] private RectTransform tutorialsSingleTapContainer;
    [SerializeField] private RectTransform tutorialsRightTapContainer;
    [SerializeField] private RectTransform tutorialsLeftRightDivider;
    [SerializeField] private RectTransform tutorialsLeftTapContainer;
    [SerializeField] private RectTransform tutorialsSwipeRightContainer;
    [SerializeField] private RectTransform tutorialsSwipeLeftContainer;
    [SerializeField] private RectTransform gamePlayChoicesContainer;
    [SerializeField] private RectTransform tutorialsSingleTapChoiceContainer;
    [SerializeField] private RectTransform tutorialsRightTapChoiceContainer;
    [SerializeField] private RectTransform tutorialsLeftTapChoiceContainer;
    [SerializeField] private RectTransform tutorialsSwipeRightChoiceContainer;
    [SerializeField] private RectTransform tutorialsSwipeLeftChoiceContainer;
    [SerializeField] private RectTransform tutorialsFinish;
    [SerializeField] private TextMeshProUGUI tutorialsInfo;
    [SerializeField] private RectTransform diamondsCollectedContainer;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Sprite boldLogo;
    [SerializeField] private Sprite chanceLogo;
    [SerializeField] private Sprite doublePointsLogo;
    [SerializeField] private TextMeshProUGUI diamondsCollectedText;
    [SerializeField] private TextMeshProUGUI doublePointsOwned;
    [SerializeField] private TextMeshProUGUI boltsOwned;
    [SerializeField] private TextMeshProUGUI chancesOwned;
    private ScoreManager scoreManager;
    private PlayerMovement playerMovement;
    private InputManager inputManager;
    private PickUpsManager pickUpsManager;
    private MenusController menusController;
    private PathMaker pathMaker;
    private List<PickUpType> currentPickUps = new List<PickUpType>();
    private Vector2 targetPickedPickUpsContainerSize = new Vector2(0, 0);
    private Hashtable targetPickedPickUpsPositions = new Hashtable();
    private bool pickedGamePlayChoice = false;
    private IEnumerator animationsCoroutine;

    // ---------------------------------------------------------------------------------------
    // Tutorial (a new player's first run; Settings can switch it back on)
    //
    // Two steps, and no way to fall in either:
    //   Stop    the ball stops by itself on the first two turns, a hand shows the tap, and it
    //           waits. This teaches what a tap does.
    //   Timing  the ball no longer stops. This is the game itself - tap while the ball is on the
    //           turn - which the old tutorial never showed: it let go after the two stops, and
    //           the first real turn was the first one the player had to time. Here a tap that is
    //           too early is ignored, and a ball that gets past the flag untapped slows, stops
    //           and waits, saying so. TIMING_HITS taps in time finish the tutorial.
    // Every turn part carries a flag while it lasts (PathMaker). PlayerMovement.tutorialTurn
    // decides what a tap does; PlayerTrigger slows and stops the ball.
    // ---------------------------------------------------------------------------------------
    private enum TutorialStage { Stop, Timing, Done }
    private const int STOP_TURNS = 2;
    private const int TIMING_HITS = 3;
    private const int TIMING_TURNS_AT_MOST = 8;   // then it ends anyway: nobody is kept here
    private const float INFO_TOP_Y = -250f;
    private const float INFO_HINT_Y = -620f;       // under the score
    private TutorialStage tutorialStage = TutorialStage.Stop;
    private int stopTurns, timingHits, timingTurns, tutorialLate, tutorialEarly;
    private string tutorialText = "";
    private GameObject tutorialSkip;
    private bool infoBusy;
    private const string HINT_KEY = "key_hint_seen_";
    private TextMeshProUGUI bestText;   // under the score: the score to beat
    private GameObject powerUpDock;

    void Awake()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        inputManager = FindObjectOfType<InputManager>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        menusController = FindObjectOfType<MenusController>();
        pathMaker = FindObjectOfType<PathMaker>();

        // The power-up buttons (PickUps/<name>/Count/CountText), in the Nebula look.
        Transform dock = boltsOwned.transform.parent.parent.parent;
        dock.gameObject.AddComponent<PowerUpDock>().Build(pickUpsManager);
        powerUpDock = dock.gameObject;
        pickedPickUps.gameObject.SetActive(false); // see addPickedPickUp

        bestText = UiKit.Label(scoreText, scoreText.transform.parent, "Best", "", 34, UiKit.TextDim, TextAlignmentOptions.Center);
        bestText.rectTransform.anchoredPosition = scoreText.rectTransform.anchoredPosition + new Vector2(0, -92);
        bestText.rectTransform.sizeDelta = new Vector2(600, 50);
        bestText.transform.SetSiblingIndex(scoreText.transform.GetSiblingIndex() + 1);
    }

    void Start()
    {
        tutorialsRightTapChoiceContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
        tutorialsLeftTapChoiceContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
        tutorialsSwipeRightChoiceContainer.GetComponent<Animation>()["TutorialsSwipeRight"].speed = 0.7f;
        tutorialsSwipeLeftChoiceContainer.GetComponent<Animation>()["TutorialsSwipeLeft"].speed = 0.7f;
        tutorialsSingleTapChoiceContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
    }

    // Update is called once per frame
    void Update()
    {
        if (pickedPickUps.sizeDelta != targetPickedPickUpsContainerSize)
        {
            pickedPickUps.sizeDelta = Vector2.Lerp(pickedPickUps.sizeDelta, targetPickedPickUpsContainerSize, Time.deltaTime * 10);
        }
        if (targetPickedPickUpsPositions.Count > 0)
        {
            foreach (DictionaryEntry de in targetPickedPickUpsPositions)
            {
                ((RectTransform)de.Key).anchoredPosition = Vector2.Lerp(((RectTransform)de.Key).anchoredPosition, ((Vector2)de.Value), Time.deltaTime * 10);
            }
        }
    }

    void OnEnable()
    {
        updatePickUpsCount();
        bool tutorial = PlayerStats.Instance.isTutorialsOn();
        tutorialsContainer.gameObject.SetActive(tutorial);
        scoreText.gameObject.SetActive(!tutorial);
        bestText.gameObject.SetActive(!tutorial);
        updateBestText();
        // Not in the tutorial, which is about turning (and they cannot be used in it).
        powerUpDock.SetActive(!tutorial);
        if (!tutorial && ownsPowerUps())
            StartCoroutine(dockHint());
        pauseButton.gameObject.SetActive(!tutorial);
        // Also runs when the game comes back from the pause menu, which stopped whatever was being
        // said (coroutines end with the object): put the line back as it was, or clear it.
        infoBusy = false;
        setUpInfo();
        tutorialsInfo.rectTransform.anchoredPosition3D = new Vector3(0, INFO_TOP_Y, 0);
        tutorialsInfo.text = tutorial ? tutorialText : "";
        tutorialsInfo.alpha = tutorial && Utility.gameStarted ? 1 : 0;
        if (tutorialSkip != null)
            tutorialSkip.SetActive(tutorial);
        resetSelectedGamePlayMode();
        pickedGamePlayChoice = false;
    }

    void OnDisable()
    {
        if (diamondsCollectedIsVisible)
        {
            diamondsCollectedContainer.GetComponent<Animation>().Play("HideDiamondsCollected");
            diamondsCollectedIsVisible = false;
        }

        tutorialsRightTapChoiceContainer.gameObject.SetActive(false);
        tutorialsSwipeRightChoiceContainer.gameObject.SetActive(false);
        tutorialsLeftTapChoiceContainer.gameObject.SetActive(false);
        tutorialsSwipeLeftChoiceContainer.gameObject.SetActive(false);
        tutorialsRightTapChoiceContainer.GetComponent<Animation>().Stop();
        tutorialsSwipeRightChoiceContainer.GetComponent<Animation>().Stop();
        tutorialsLeftTapChoiceContainer.GetComponent<Animation>().Stop();
        tutorialsSwipeLeftChoiceContainer.GetComponent<Animation>().Stop();
    }

    private void resetSelectedGamePlayMode()
    {
        GamePlayMode selectedGamePlayMode = PlayerStats.Instance.getGamePlayMode();
        switch (selectedGamePlayMode)
        {
            case GamePlayMode.TapLeftRight:
                singleTap.gameObject.SetActive(false);
                tapLeftRight.gameObject.SetActive(true);
                swipeLeftRight.gameObject.SetActive(false);
                break;
            case GamePlayMode.SwipeLeftRight:
                singleTap.gameObject.SetActive(false);
                tapLeftRight.gameObject.SetActive(false);
                swipeLeftRight.gameObject.SetActive(true);
                break;
            default:
                singleTap.gameObject.SetActive(true);
                tapLeftRight.gameObject.SetActive(false);
                swipeLeftRight.gameObject.SetActive(false);
                break;
        }
    }

    private void hideGamePlayControls()
    {
        singleTap.gameObject.SetActive(false);
        tapLeftRight.gameObject.SetActive(false);
        swipeLeftRight.gameObject.SetActive(false);
    }

    private IEnumerator animationsIEnumerator()
    {
        while (true)
        {
            tutorialsRightTapChoiceContainer.gameObject.SetActive(true);
            tutorialsSwipeRightChoiceContainer.gameObject.SetActive(true);
            tutorialsRightTapChoiceContainer.GetComponent<Animation>().Play();
            tutorialsSwipeRightChoiceContainer.GetComponent<Animation>().Play();
            yield return new WaitForSecondsRealtime(1.75f);
            tutorialsRightTapChoiceContainer.GetComponent<Animation>().Stop();
            tutorialsSwipeRightChoiceContainer.GetComponent<Animation>().Stop();
            yield return new WaitForSecondsRealtime(0.3f);
            tutorialsLeftTapChoiceContainer.gameObject.SetActive(true);
            tutorialsSwipeLeftChoiceContainer.gameObject.SetActive(true);
            tutorialsLeftTapChoiceContainer.GetComponent<Animation>().Play();
            tutorialsSwipeLeftChoiceContainer.GetComponent<Animation>().Play();
            yield return new WaitForSecondsRealtime(1.75f);
            tutorialsLeftTapChoiceContainer.GetComponent<Animation>().Stop();
            tutorialsSwipeLeftChoiceContainer.GetComponent<Animation>().Stop();
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }

    public void PauseGame()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        if (!Utility.userCanPause)
            return;

        GameAnalytics.Paused();
        menusController.hideStackMenus();
        menusController.showAndAddMenuToStack(Menus.PauseMenu);
    }

    public void updatePickUpsCount()
    {
        boltsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getBoltsCount());
        doublePointsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getDoublePointsCount());
        chancesOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getChancesCount());
    }

    // In the tutorial PlayerMovement decides what each of these does (tutorialTurn) and reports
    // back through tutorialTurned / tutorialTapIgnored.
    // Chill's revive waits in the air for a tap: whichever control the player uses, that tap is it.
    public void AutoTurn()
    {
        if (PlayerMovement.WaitingForTap)
            playerMovement.continueAfterChillRevive();
        else if (inputManager.userCanControl())
            playerMovement.autoTurn();
    }

    public void TurnLeft()
    {
        if (PlayerMovement.WaitingForTap)
            playerMovement.continueAfterChillRevive();
        else if (inputManager.userCanControl())
            playerMovement.manualTurn(true);
    }

    public void TurnRight()
    {
        if (PlayerMovement.WaitingForTap)
            playerMovement.continueAfterChillRevive();
        else if (inputManager.userCanControl())
            playerMovement.manualTurn(false);
    }

    /// <summary>Chill's revive: "Tap to continue" while the ball waits in the air.</summary>
    public void showContinuePrompt(bool show)
    {
        if (show)
        {
            setUpInfo();
            tutorialsInfo.rectTransform.anchoredPosition3D = new Vector3(0, INFO_HINT_Y, 0);
            if (tutorialSkip != null)
                tutorialSkip.SetActive(false);
            tutorialsContainer.gameObject.SetActive(true);
            say(tapWord() + " to continue");
            tutorialText = "";
        }
        else if (tutorialsContainer.gameObject.activeSelf)
        {
            tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
            tutorialsContainer.gameObject.SetActive(false);
        }
    }

    // The line of text the tutorial and the hints share: up to two lines, shrinking to fit.
    private void setUpInfo()
    {
        tutorialsInfo.enableAutoSizing = true;
        tutorialsInfo.fontSizeMin = 30;
        tutorialsInfo.fontSizeMax = 62;
        tutorialsInfo.rectTransform.sizeDelta = new Vector2(960, 190);
    }

    private static string tapWord()
    {
        return PlayerStats.Instance.getGamePlayMode() == GamePlayMode.SwipeLeftRight ? "Swipe" : "Tap";
    }

    private static string howToTurn()
    {
        switch (PlayerStats.Instance.getGamePlayMode())
        {
            case GamePlayMode.TapLeftRight: return "Tap left or right to turn";
            case GamePlayMode.SwipeLeftRight: return "Swipe left or right to turn";
            default: return "Tap anywhere to turn";
        }
    }

    private void say(string text)
    {
        tutorialText = text;
        tutorialsInfo.text = text;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[Tutorial] " + text.Replace("\n", " / "));
#endif
        Animation fade = tutorialsInfo.GetComponent<Animation>();
        fade.Stop();
        fade.Play("ShowTutorialInfo");
    }

    /// <summary>The ball is in place and the run starts with the tutorial on.</summary>
    public void startTutorial()
    {
        tutorialStage = TutorialStage.Stop;
        stopTurns = timingHits = timingTurns = tutorialLate = tutorialEarly = 0;
        setUpInfo();
        tutorialsInfo.rectTransform.anchoredPosition3D = new Vector3(0, INFO_TOP_Y, 0);
        showSkip();
        say("The ball stops at the flag.\n" + howToTurn() + "!");
    }

    // "Skip", top right where the pause button will be: made here from the info text so that it
    // needs nothing new in the scene.
    private void showSkip()
    {
        if (tutorialSkip == null)
        {
            tutorialSkip = Instantiate(tutorialsInfo.gameObject, tutorialsContainer);
            tutorialSkip.name = "Skip";
            DestroyImmediate(tutorialSkip.GetComponent<Animation>());
            TextMeshProUGUI label = tutorialSkip.GetComponent<TextMeshProUGUI>();
            label.text = "Skip";
            label.enableAutoSizing = false;
            label.fontSize = 40;
            label.alpha = 0.65f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = true;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition3D = new Vector3(-30, -50, 0);
            rect.sizeDelta = new Vector2(220, 110);
            Button skip = tutorialSkip.AddComponent<Button>();
            skip.onClick.AddListener(FinishTutorial);
            skip.onClick.AddListener(() => SoundManager.Instance?.PlayMenu());
        }
        tutorialSkip.SetActive(true);
    }

    /// <summary>Whether the ball stops by itself at each turn (the tutorial's first step).</summary>
    public bool tutorialStopsAtTurns()
    {
        return tutorialStage == TutorialStage.Stop;
    }

    /// <summary>The ball is coming onto a turn part. The first one it does not stop at gets the hand.</summary>
    public void tutorialReachedTurn(Transform turnPart)
    {
        if (tutorialStage == TutorialStage.Timing && timingHits == 0)
            playTutorialsAnimation(turnPart);
    }

    /// <summary>The ball has stopped on a turn part and waits for the player.</summary>
    public void tutorialBallStopped(Transform turnPart, bool late)
    {
        playTutorialsAnimation(turnPart);
        if (late)
        {
            tutorialLate++;
            say("Too late!\n" + tapWord() + " a little sooner next time");
        }
    }

    /// <summary>A tap the tutorial did not take: before the turn, or for the wrong side.</summary>
    public void tutorialTapIgnored(bool wrongSide)
    {
        if (wrongSide)
            say("The other side!");
        else if (tutorialStage == TutorialStage.Timing)
        {
            tutorialEarly++;
            say("Too early!\nWait until the ball is on the flag");
        }
    }

    /// <summary>The player turned the ball; <paramref name="wasStopped"/> if it was waiting for them.</summary>
    public void tutorialTurned(bool wasStopped)
    {
        if (tutorialStage == TutorialStage.Stop)
        {
            stopTurns++;
            if (stopTurns < STOP_TURNS)
                return;
            tutorialStage = TutorialStage.Timing;
            GameAnalytics.TutorialStep("timing");
            say("Now it will not stop!\n" + tapWord() + " when the ball is on the flag");
            return;
        }
        if (tutorialStage != TutorialStage.Timing)
            return;

        timingTurns++;
        if (!wasStopped)
            timingHits++;
        if (timingHits >= TIMING_HITS || timingTurns >= TIMING_TURNS_AT_MOST)
            StartCoroutine(tutorialDone());
        else if (wasStopped)
            say(tapWord() + " at the flag");
        else
            say(timingHits == 1 ? "Nice!" : "Great! One more");
    }

    private void endTutorial()
    {
        tutorialStage = TutorialStage.Done;
        tutorialText = "";
        PlayerStats.Instance.setTutorialsState(false);
        stopTutorialAnimation();
        if (tutorialSkip != null)
            tutorialSkip.SetActive(false);
        pauseButton.gameObject.SetActive(true);
        scoreText.gameObject.SetActive(true);
        bestText.gameObject.SetActive(true);
        updateBestText();
        powerUpDock.SetActive(true);
    }

    private static bool ownsPowerUps()
    {
        PlayerStats stats = PlayerStats.Instance;
        return stats.getBoltsCount() + stats.getDoublePointsCount() + stats.getChancesCount() > 0;
    }

    // Once ever: where the power-ups are, the first run that has any.
    private IEnumerator dockHint()
    {
        while (!Utility.gameStarted)
            yield return null;
        yield return new WaitForSecondsRealtime(2.5f);
        showHintOnce("dock", "Your power-ups are on the left.\nTap one to use it");
    }

    // From here it is the game: the score counts and the speed follows it. The goodbye is said
    // over the next stretch of track.
    private IEnumerator tutorialDone()
    {
        endTutorial();
        GameAnalytics.TutorialComplete(tutorialLate, tutorialEarly);
        infoBusy = true;
        say("Perfect!");
        yield return new WaitForSecondsRealtime(1.2f);
        say("You are ready. Don't fall!");
        yield return new WaitForSecondsRealtime(2f);
        tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
        yield return new WaitForSecondsRealtime(0.5f);
        tutorialsContainer.gameObject.SetActive(false);
        infoBusy = false;
    }

    /// <summary>The Skip button.</summary>
    public void FinishTutorial()
    {
        if (!PlayerStats.Instance.isTutorialsOn())
            return;
        GameAnalytics.TutorialSkipped();
        StopAllCoroutines();
        playerMovement.tutorialSkipped(); // a ball waiting at a turn is turned and sent on
        endTutorial();
        tutorialsInfo.alpha = 0;
        tutorialsContainer.gameObject.SetActive(false);
        infoBusy = false;
    }

    /// <summary>
    /// One line, once ever on this device, the first time something happens in a run: what the
    /// thing just collected is for. Dropped if the line is busy - it will come up again.
    /// </summary>
    public void showHintOnce(string key, string text)
    {
        if (!isActiveAndEnabled || infoBusy || PlayerStats.Instance.isTutorialsOn() || PlayerPrefs.GetInt(HINT_KEY + key, 0) == 1)
            return;
        PlayerPrefs.SetInt(HINT_KEY + key, 1);
        StartCoroutine(hint(text));
    }

    private IEnumerator hint(string text)
    {
        infoBusy = true;
        setUpInfo();
        tutorialsInfo.rectTransform.anchoredPosition3D = new Vector3(0, INFO_HINT_Y, 0);
        if (tutorialSkip != null)
            tutorialSkip.SetActive(false);
        tutorialsContainer.gameObject.SetActive(true);
        say(text);
        tutorialText = "";
        yield return new WaitForSecondsRealtime(3.5f);
        tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
        yield return new WaitForSecondsRealtime(0.5f);
        tutorialsContainer.gameObject.SetActive(false);
        infoBusy = false;
    }

    public void updateDiamondsText()
    {
        showDiamondsCollected();
        diamondsCollectedText.text = Utility.getFormatedNumber(scoreManager.getDiamondsCollected());
        diamondsCollectedText.GetComponent<Animation>().Play("DiamondsCollectedText");
    }

    private IEnumerator diamondsCollectedCoroutine;
    private bool diamondsCollectedIsVisible = false;
    public void showDiamondsCollected()
    {
        if (diamondsCollectedCoroutine != null)
            StopCoroutine(diamondsCollectedCoroutine);
        diamondsCollectedCoroutine = diamondsCollectedVisibility();
        StartCoroutine(diamondsCollectedCoroutine);
    }

    private IEnumerator diamondsCollectedVisibility()
    {
        if (!diamondsCollectedIsVisible)
        {
            diamondsCollectedContainer.GetComponent<Animation>().Play("ShowDiamondsCollected");
            diamondsCollectedIsVisible = true;
        }
        yield return new WaitForSeconds(2f);
        if (diamondsCollectedIsVisible)
        {
            diamondsCollectedContainer.GetComponent<Animation>().Play("HideDiamondsCollected");
            diamondsCollectedIsVisible = false;
        }
    }

    public void updateScoreText()
    {
        scoreText.text = scoreManager.getScore().ToString();
        scoreText.GetComponent<Animation>().Play();
        updateBestText();
    }

    // "BEST 240" while there is one to beat, "NEW BEST!" once it is beaten (the high score itself
    // is saved at game over).
    private void updateBestText()
    {
        int best = PlayerStats.Instance.getHighScore();
        int score = scoreManager != null ? scoreManager.getScore() : 0;
        if (!GameMode.T.hasBestScore)
        {
            bestText.text = "CHILL"; // no best to chase in Chill
            bestText.color = UiKit.TextDim;
        }
        else if (best <= 0)
            bestText.text = "";
        else if (score > best)
        {
            bestText.text = "NEW BEST!";
            bestText.color = UiKit.Gold;
        }
        else
        {
            bestText.text = "BEST " + best;
            bestText.color = UiKit.TextDim;
        }
    }

    public void playPlus1Effect()
    {
        // if (Utility.doublePointIsOn)
        //     playEffect(plusTwoDoublePointsEffect);
        // else
        //     playEffect(plusOneEffect);
        playEffect(plusOneEffect);
        if (Utility.doublePointIsOn)
            playEffect(plusOneEffect);
    }

    public void playPlus2Effect()
    {
        // if (Utility.doublePointIsOn)
        //     playEffect(plusFourDoublePointsEffect);
        // else
        //     playEffect(plusTwoEffect);
        playEffect(plusTwoEffect);
        if (Utility.doublePointIsOn)
            playEffect(plusTwoEffect);
    }

    void playEffect(ParticleSystem effect)
    {
        ParticleSystem effectClone = (ParticleSystem)Instantiate(effect, transform);
        effectClone.transform.localEulerAngles = Vector3.zero;
        effectClone.Play();
        Destroy(effectClone.gameObject, effectClone.main.startLifetimeMultiplier);
    }

    // The hand: a tap, or a tap or swipe on the side the part turns to. The side comes from the
    // part itself - PathMaker.nextDirection, which this used to read, can be empty.
    public void playTutorialsAnimation(Transform turnPart = null)
    {
        if (turnPart == null)
            return;
        bool right = turnPart.CompareTag("LandRight");
        GamePlayMode selectedGamePlayMode = PlayerStats.Instance.getGamePlayMode();
        switch (selectedGamePlayMode)
        {
            case GamePlayMode.TapLeftRight:
                tutorialsLeftRightDivider.gameObject.SetActive(true);
                if (right)
                {
                    tutorialsRightTapContainer.gameObject.SetActive(true);
                    tutorialsRightTapContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
                    tutorialsRightTapContainer.GetComponent<Animation>().Play();
                }
                else
                {
                    tutorialsLeftTapContainer.gameObject.SetActive(true);
                    tutorialsLeftTapContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
                    tutorialsLeftTapContainer.GetComponent<Animation>().Play();
                }
                break;
            case GamePlayMode.SwipeLeftRight:
                if (right)
                {
                    tutorialsSwipeRightContainer.gameObject.SetActive(true);
                    tutorialsSwipeRightContainer.GetComponent<Animation>()["TutorialsSwipeRight"].speed = 0.7f;
                    tutorialsSwipeRightContainer.GetComponent<Animation>().Play();
                }
                else
                {
                    tutorialsSwipeLeftContainer.gameObject.SetActive(true);
                    tutorialsSwipeLeftContainer.GetComponent<Animation>()["TutorialsSwipeLeft"].speed = 0.7f;
                    tutorialsSwipeLeftContainer.GetComponent<Animation>().Play();
                }
                break;
            default:
                tutorialsSingleTapContainer.gameObject.SetActive(true);
                tutorialsSingleTapContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
                tutorialsSingleTapContainer.GetComponent<Animation>().Play();
                break;
        }
        tutorialsFinish.gameObject.SetActive(false);
        gamePlayChoicesContainer.gameObject.SetActive(false);
    }

    public void SelectSingleTapMode()
    {
        PlayerStats.Instance.setGamePlayMode(GamePlayMode.SingleTap);
        gamePlayChoicesContainer.gameObject.SetActive(false);
        pickedGamePlayChoice = true;
        playTutorialsAnimation();
        tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
        resetSelectedGamePlayMode();
        if (animationsCoroutine != null)
            StopCoroutine(animationsCoroutine);
    }

    public void SelectTapLeftRightMode()
    {
        PlayerStats.Instance.setGamePlayMode(GamePlayMode.TapLeftRight);
        gamePlayChoicesContainer.gameObject.SetActive(false);
        pickedGamePlayChoice = true;
        playTutorialsAnimation();
        tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
        resetSelectedGamePlayMode();
        if (animationsCoroutine != null)
            StopCoroutine(animationsCoroutine);
    }

    public void SelectSwipeLeftRightMode()
    {
        PlayerStats.Instance.setGamePlayMode(GamePlayMode.SwipeLeftRight);
        gamePlayChoicesContainer.gameObject.SetActive(false);
        pickedGamePlayChoice = true;
        playTutorialsAnimation();
        tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
        resetSelectedGamePlayMode();
        if (animationsCoroutine != null)
            StopCoroutine(animationsCoroutine);
    }

    public void stopTutorialAnimation()
    {
        tutorialsSingleTapContainer.gameObject.SetActive(false);
        tutorialsRightTapContainer.gameObject.SetActive(false);
        tutorialsLeftTapContainer.gameObject.SetActive(false);
        tutorialsSwipeRightContainer.gameObject.SetActive(false);
        tutorialsSwipeLeftContainer.gameObject.SetActive(false);
        tutorialsSingleTapContainer.GetComponent<Animation>().Stop();
        tutorialsRightTapContainer.GetComponent<Animation>().Stop();
        tutorialsLeftTapContainer.GetComponent<Animation>().Stop();
        tutorialsSwipeRightContainer.GetComponent<Animation>().Stop();
        tutorialsSwipeLeftContainer.GetComponent<Animation>().Stop();
        tutorialsFinish.gameObject.SetActive(false);
        tutorialsLeftRightDivider.gameObject.SetActive(false);
    }

    public void makeScoreBlue()
    {
        scoreText.colorGradientPreset = blueTextGradient;
    }

    public void makeScoreYellow()
    {
        scoreText.colorGradientPreset = yellowTextGradient;
    }

    public void addPickedPickUp(PickUpType pickUpType)
    {

        if (currentPickUps.Contains(pickUpType))
            return;

        // The row under the score is kept for its bookkeeping but not shown: the power-up buttons
        // on the left light up and count down while one is on (PowerUpDock).
        if (currentPickUps.Count == 0)
        {
            pickedPickUps.sizeDelta = new Vector2(100, 100);
            pickedPickUps.gameObject.SetActive(false);
        }

        setPickedUpImageUI(pickUpType);

        currentPickUps.Add(pickUpType);

        resetPickedPickUpsPosition();
    }

    private void setPickedUpImageUI(PickUpType pickUpType)
    {
        GameObject newPickUpGameObject = new GameObject(pickUpType.ToString());
        newPickUpGameObject.transform.SetParent(pickedPickUps.transform);
        newPickUpGameObject.transform.localEulerAngles = Vector3.zero;

        RectTransform newPickUpRect = newPickUpGameObject.AddComponent<RectTransform>();
        newPickUpRect.localScale = Vector3.one;
        newPickUpRect.anchoredPosition3D = Vector3.zero;
        newPickUpRect.sizeDelta = new Vector2(70, 70);
        newPickUpRect.anchorMin = new Vector2(0, 0.5f);
        newPickUpRect.anchorMax = new Vector2(0, 0.5f);
        targetPickedPickUpsPositions.Add(newPickUpRect, new Vector2(0, 0));
        // newPickUpRect.pivot = new Vector2(-0.25f, 0.5f);

        Image image = newPickUpGameObject.AddComponent<Image>();
        switch (pickUpType)
        {
            case PickUpType.Bolt:
                image.sprite = boldLogo;
                break;
            case PickUpType.Chance:
                image.sprite = chanceLogo;
                break;
            case PickUpType.DoublePoints:
                image.sprite = doublePointsLogo;
                break;
        }
        image.preserveAspect = true;
    }

    private void resetPickedPickUpsPosition()
    {
        if (currentPickUps.Count > 0)
            targetPickedPickUpsContainerSize = new Vector2(25 * 2 + 70 * currentPickUps.Count + 20 * (currentPickUps.Count - 1), 100);
        else
            targetPickedPickUpsContainerSize = new Vector2(0, 0);

        float currentX = 25 + 70 / 2;
        for (int i = 0; i < currentPickUps.Count; i++)
        {
            Transform currentPickUp = pickedPickUps.Find(currentPickUps[i].ToString());
            if (currentPickUp != null)
            {
                // if()
                // targetPickedPickUpsPositions.Add(currentPickUp.GetComponent<RectTransform>(), new Vector2(currentX, 0));
                // currentPickUp.GetComponent<RectTransform>().anchoredPosition = new Vector2(currentX, 0);
                targetPickedPickUpsPositions[currentPickUp.GetComponent<RectTransform>()] = new Vector2(currentX, 0);
                currentX += 90;
            }
        }
    }

    /// <summary>The run is over: no power-up icon stays for the next one.</summary>
    public void clearPickedPickUps()
    {
        foreach (PickUpType p in new List<PickUpType>(currentPickUps))
            removePickedPickUp(p);
    }

    public void removePickedPickUp(PickUpType pickUpType)
    {
        for (int i = 0; i < currentPickUps.Count; i++)
        {
            if (pickUpType == currentPickUps[i])
            {
                targetPickedPickUpsPositions.Remove(pickedPickUps.Find(pickUpType.ToString()).GetComponent<RectTransform>());
                GameObject.Destroy(pickedPickUps.Find(pickUpType.ToString()).gameObject);
                currentPickUps.Remove(pickUpType);
                resetPickedPickUpsPosition();
            }
        }
    }

    public void ActivateBolt()
    {
        if (Utility.boltIsOn || PlayerStats.Instance.getBoltsCount() < 1 || PlayerStats.Instance.isTutorialsOn() || !Utility.gameStarted)
            return;

        PlayerStats.Instance.subtractBolts();
        GameAnalytics.PowerUpUsed("bolt");
        boltsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getBoltsCount());
        pickUpsManager.activateBolt();
    }

    public void ActivateDoublePoints()
    {
        if (Utility.doublePointIsOn || PlayerStats.Instance.getDoublePointsCount() < 1 || PlayerStats.Instance.isTutorialsOn() || !Utility.gameStarted)
            return;

        PlayerStats.Instance.subtractDoublePoints();
        GameAnalytics.PowerUpUsed("double_points");
        doublePointsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getDoublePointsCount());
        pickUpsManager.activateDoublePoint();
    }

    public void ActivateChance()
    {
        if (Utility.chanceIsOn || PlayerStats.Instance.getChancesCount() < 1 || PlayerStats.Instance.isTutorialsOn())
            return;

        PlayerStats.Instance.subtractChances();
        GameAnalytics.PowerUpUsed("chance");
        chancesOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getChancesCount());
        pickUpsManager.activateChance();
    }
}
