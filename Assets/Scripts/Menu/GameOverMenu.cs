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

        if (playerScore > playerStats.getHighScore())
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
    }

    public void Share()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        ShareManager.Instance.Share("Rising Way", "OMG! I got " + scoreManager.getScore() + " playing #RisingWay");
    }

}
