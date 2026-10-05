using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverMenu : MonoBehaviour
{
    [SerializeField] private RectTransform title;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI highscoreText;
    [SerializeField] private TextMeshProUGUI newHighscoreText;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button shareButton;
    private CameraController cameraController;
    private PlayerMovement playerMovement;
    private PathMaker pathMaker;
    private MenusController menusController;
    private ScoreManager scoreManager;
    private PlayerStats playerStats;
    private PlayerTrigger playerTrigger;
    private AmbientEffectsController ambientEffectsController;
    private AdmobManager adsManager;
    private MenusOperations menusOperations;
    private IEnumerator scoreCountCoroutine;
    private int numberOfLosesAfterAd = 0;

    void Awake()
    {
        cameraController = FindObjectOfType<CameraController>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        pathMaker = FindObjectOfType<PathMaker>();
        menusController = FindObjectOfType<MenusController>();
        scoreManager = FindObjectOfType<ScoreManager>();
        playerStats = PlayerStats.Instance;
        playerTrigger = FindObjectOfType<PlayerTrigger>();
        ambientEffectsController = FindObjectOfType<AmbientEffectsController>();
        adsManager = FindAnyObjectByType<AdmobManager>();
        menusOperations = FindAnyObjectByType<MenusOperations>();

        // float ratio = (float)Screen.height / Screen.width;
        // if (ratio >= 1.1f && ratio < 1.3f) // 2176x1812 Fold
        // {
        //     title.anchoredPosition = new Vector2(title.anchoredPosition.x, 460);
        //     scoreText.GetComponent<RectTransform>().anchoredPosition = new Vector2(scoreText.GetComponent<RectTransform>().anchoredPosition.x, 300);
        //     highscoreText.GetComponent<RectTransform>().anchoredPosition = new Vector2(highscoreText.GetComponent<RectTransform>().anchoredPosition.x, 140);
        //     newHighscoreText.GetComponent<RectTransform>().anchoredPosition = new Vector2(newHighscoreText.GetComponent<RectTransform>().anchoredPosition.x, 140);
        //     replayButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(replayButton.GetComponent<RectTransform>().anchoredPosition.x, -168);
        //     shareButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(shareButton.GetComponent<RectTransform>().anchoredPosition.x, -400);
        // }

        scoreCountCoroutine = scoreCount();
    }

    float value;
    void FixedUpdate()
    {
        if (newHighscoreText.gameObject.activeSelf)
        {
            if (newHighscoreText.transform.localScale.x > 1)
            {
                value = newHighscoreText.transform.localScale.x - newHighscoreText.transform.localScale.x / 10f;
                if (value < 1)
                    value = 1;
                newHighscoreText.transform.localScale = new Vector3(value, value, value);
            }
        }
    }

    void OnEnable()
    {
        // numberOfLosesAfterAd++;
        // if (numberOfLosesAfterAd % Random.Range(4, 7) == 0 || numberOfLosesAfterAd > 7 || true)
        // {
        //     numberOfLosesAfterAd = 0;
        //     adsManager.ShowInterstitialAd(() => { StartCoroutine(scoreCountCoroutine); });
        // }
        // else
            StartCoroutine(scoreCountCoroutine);
    }

    void OnDisable()
    {
        resetGameOver();
        _ShowAndroidToastMessage("OnDisable");
    }

    private void _ShowAndroidToastMessage(string message)
    {
        // AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        // AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        // if (unityActivity != null)
        // {
        //     AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
        //     unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        //     {
        //         AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity, message, 0);
        //         toastObject.Call("show");
        //     }));
        // }
    }

    // The count is a flourish, not a progress bar: COUNT_SECONDS whatever the score, slowing into
    // the final number, and in unscaled time so the game's own time scale cannot stretch it.
    //
    // It used to add a step worked out from one frame's delta and then wait on WaitForSeconds,
    // which runs on scaled time: dying while the time scale was low dragged the count out for many
    // seconds, and the replay and share buttons below only appear once it ends, so the screen sat
    // there with nothing on it.
    private const float COUNT_SECONDS = 0.6f;

    private IEnumerator scoreCount()
    {
        int playerScore = scoreManager.getScore();
        float from = playerScore / 2f; // the count has always started halfway up
        float elapsed = 0f;

        while (elapsed < COUNT_SECONDS && playerScore > 0)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / COUNT_SECONDS);
            t = 1f - (1f - t) * (1f - t); // ease out: quick, then settling
            scoreText.text = ((int)Mathf.Lerp(from, playerScore, t)).ToString();
            yield return null;
        }

        scoreText.text = playerScore.ToString();
        scoreText.GetComponent<Animation>().Play();

        if (!GameMode.T.hasBestScore)
        {
            // Chill: no best score - the longest climb without a fall, this run and ever.
            int longest = scoreManager.getLongestStreak();
            bool record = longest > playerStats.getChillLongestStreak();
            if (record)
                playerStats.setChillLongestStreak(longest);
            highscoreText.text = record ? "New longest climb: " + longest
                                        : "Longest climb: " + longest + "\nBest: " + playerStats.getChillLongestStreak();
            highscoreText.gameObject.SetActive(true);
            highscoreText.GetComponent<Animation>().Play();
        }
        else if (playerScore > playerStats.getHighScore())
        {
            newHighscoreText.transform.localScale = new Vector3(5, 5, 5);
            newHighscoreText.gameObject.SetActive(true);
            playerStats.setHighScore(playerScore);
        }
        else
        {
            highscoreText.text = Utility.Constants.HIGH_SCORE_TEXT + playerStats.getHighScore().ToString();
            highscoreText.gameObject.SetActive(true);
            highscoreText.GetComponent<Animation>().Play();
        }

        replayButton.gameObject.SetActive(true);
        shareButton.gameObject.SetActive(true);
        GetComponent<Animation>().Play();

        // The mode's board keeps the player's best by itself: every run's goes up.
        Leaderboards.Report(GameMode.Current, GameMode.T.hasBestScore ? playerScore : scoreManager.getLongestStreak());
        offerDoubleDiamonds();
        suggestMode(playerScore);
    }

    // ---- Chill: double the run's diamonds for an ad ------------------------------------------
    private Button doubleButton;
    private TextMeshProUGUI doubleText;

    private void offerDoubleDiamonds()
    {
        int diamonds = scoreManager.getDiamondsCollected();
        bool offer = GameMode.T.unlimitedRevives && diamonds > 0 && adsManager != null && adsManager.CanShowReviveAd();
        if (!offer)
        {
            if (doubleButton != null)
                doubleButton.gameObject.SetActive(false);
            return;
        }
        if (doubleButton == null)
            buildDoubleButton();
        doubleText.text = "DOUBLE DIAMONDS  +" + Utility.getFormatedNumber(diamonds);
        doubleButton.interactable = true;
        doubleButton.gameObject.SetActive(true);
    }

    private void buildDoubleButton()
    {
        RectTransform replay = (RectTransform)replayButton.transform;
        Image back = UiKit.Image(replay.parent, "DoubleDiamonds", "round_sheen", UiKit.WithAlpha(UiKit.Glass, 0.85f));
        back.pixelsPerUnitMultiplier = 1.6f;
        RectTransform r = back.rectTransform;
        r.anchorMin = replay.anchorMin;
        r.anchorMax = replay.anchorMax;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(620, 104);
        r.anchoredPosition = replay.anchoredPosition + new Vector2(0, replay.sizeDelta.y * 0.5f + 92f);
        Image edge = UiKit.Image(back.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.7f));
        edge.pixelsPerUnitMultiplier = back.pixelsPerUnitMultiplier;
        UiKit.Gradient(edge, UiKit.Hex("E58CFF"), UiKit.Hex("8A3CFF"), false);
        UiKit.Stretch(edge.rectTransform);
        Image gem = UiKit.Image(back.transform, "Gem", "gem", Color.white);
        UiKit.Gradient(gem, UiKit.Hex("E58CFF"), UiKit.Hex("8A3CFF"), true);
        UiKit.Place(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(56, 0), new Vector2(54, 54));
        doubleText = UiKit.Label(scoreText, back.transform, "Text", "", 34, UiKit.Text, TextAlignmentOptions.Center);
        doubleText.enableAutoSizing = true;
        doubleText.fontSizeMin = 20;
        doubleText.fontSizeMax = 34;
        doubleText.rectTransform.anchorMin = Vector2.zero;
        doubleText.rectTransform.anchorMax = Vector2.one;
        doubleText.rectTransform.offsetMin = new Vector2(96, 0);
        doubleText.rectTransform.offsetMax = new Vector2(-28, 0);
        doubleButton = back.gameObject.AddComponent<Button>();
        doubleButton.targetGraphic = back;
        doubleButton.onClick.AddListener(watchForDoubleDiamonds);
        back.gameObject.AddComponent<PressDip>();
    }

    private void watchForDoubleDiamonds()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;
        doubleButton.interactable = false;
        int diamonds = scoreManager.getDiamondsCollected();
        adsManager.ShowDoubleDiamondsAd(() =>
        {
            for (int i = 0; i < diamonds; i++)
                playerStats.addDiamonds();
            playerStats.Flush();
            doubleText.text = "DIAMONDS DOUBLED!  +" + Utility.getFormatedNumber(diamonds);
            SoundManager.Instance.PlayDiamond();
        }, () =>
        {
            doubleButton.interactable = true; // no ad after all, or closed early: still on offer
        });
    }

    // ---- One-time suggestions (docs/game-modes-plan.md) --------------------------------------
    // Insane, the first time Standard's best opens it: a celebration. Chill, after a few short
    // Standard runs in a row: an invitation, never a judgement. Each shown once.
    private const int SHORT_RUN = 20;
    private const int SHORT_RUNS_FOR_CHILL = 3;

    private void suggestMode(int score)
    {
        if (GameMode.Current != RunMode.Standard || playerStats.isTutorialsOn())
            return;
        ConfirmationDialog dialog = FindAnyObjectByType<ConfirmationDialog>(FindObjectsInactive.Include);

        if (!playerStats.wasInsaneUnlockShown() && playerStats.getHighScore(RunMode.Standard) >= GameMode.INSANE_UNLOCK_SCORE)
        {
            playerStats.markInsaneUnlockShown();
            GameAnalytics.ModeSuggested("insane");
            if (dialog != null)
                dialog.setConfirmationDialog("Insane unlocked!",
                    "You reached " + GameMode.INSANE_UNLOCK_SCORE + ". Insane is open: faster, harder turns, no help. Pure reflex.\nPlay it now?",
                    true, () => playMode(RunMode.Insane));
            return;
        }

        int shortRuns = score < SHORT_RUN ? playerStats.getShortRunsInARow() + 1 : 0;
        playerStats.setShortRunsInARow(shortRuns);
        if (shortRuns >= SHORT_RUNS_FOR_CHILL && !playerStats.wasChillSuggested())
        {
            playerStats.markChillSuggested();
            GameAnalytics.ModeSuggested("chill");
            if (dialog != null)
                dialog.setConfirmationDialog("Want a relaxed run?",
                    "Try Chill: slower, no pressure, and unlimited revives.\nYou can switch any time on the main menu.",
                    true, () => playMode(RunMode.Chill));
        }
    }

    private void playMode(RunMode mode)
    {
        playerStats.setRunMode(mode);
        GameAnalytics.ModeSuggestionTaken(mode.ToString());
        menusOperations.resetGame(false);
        menusOperations.StartGame();
    }

    public void RestartGame()
    {
        // Application.LoadLevel(Application.loadedLevel);
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusOperations.resetGame();
    }

    private void resetGameOver()
    {
        StopCoroutine(scoreCountCoroutine);
        scoreCountCoroutine = scoreCount();
        highscoreText.gameObject.SetActive(false);
        newHighscoreText.gameObject.SetActive(false);
        replayButton.gameObject.SetActive(false);
        shareButton.gameObject.SetActive(false);
        if (doubleButton != null)
            doubleButton.gameObject.SetActive(false);
    }

    public void Share()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        GameAnalytics.Shared(scoreManager.getScore());
        ShareManager.Instance.Share("Rising Way", "OMG! I got " + scoreManager.getScore() + " playing #RisingWay");
    }

}
