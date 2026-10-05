using System.Collections;
using System.Collections.Generic;
using MobileVersionCode;
using UnityEngine;
using UnityEngine.Networking;

public class MainMenu : MonoBehaviour
{

    [SerializeField] private string URL;
    [SerializeField] private Canvas updateDialog;
    [SerializeField] private RectTransform buttonsContainer;
    [SerializeField] private RectTransform gameTitle;
    [SerializeField] private RectTransform tapToPlay;
    private MenusController menusController;
    private PlayerStats playerStats;
    private PlayerMovement playerMovement;
    private Moons moons;
    private AmbientEffectsController ambientEffectsController;
    private PathMaker pathMaker;
    private CameraController cameraController;
    private ScoreManager scoreManager;
    private PickUpsManager pickUpsManager;
    private PartsPool partsPool;
    private MenusOperations menusOperations;
    private MainMenuSkin skin;

    void Awake()
    {
        menusController = FindObjectOfType<MenusController>();
        playerStats = PlayerStats.Instance;
        playerMovement = FindObjectOfType<PlayerMovement>();
        moons = FindObjectOfType<Moons>();
        ambientEffectsController = FindObjectOfType<AmbientEffectsController>();
        pathMaker = FindObjectOfType<PathMaker>();
        cameraController = FindObjectOfType<CameraController>();
        scoreManager = FindObjectOfType<ScoreManager>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        partsPool = FindObjectOfType<PartsPool>();
        menusOperations = FindObjectOfType<MenusOperations>();

        // Vector2 viewportPoint = Camera.main.WorldToViewportPoint(playerMovement.previewPosition);
        // ((RectTransform)buttonsContainer).anchorMin = viewportPoint;
        // ((RectTransform)buttonsContainer).anchorMax = viewportPoint;
        // ((RectTransform)buttonsContainer).anchoredPosition3D = Vector3.zero;

        // Vector3 screenPosition = Camera.main.WorldToScreenPoint(playerMovement.previewPosition);
        // float x = screenPosition.x - (Screen.width / 2f);
        // float y = screenPosition.y - (Screen.height / 2f);
        // float s = transform.GetComponent<Canvas>().scaleFactor;
        // ((RectTransform)buttonsContainer).anchoredPosition = new Vector2(x, y) / s;

        // float ratio = (float)Screen.height / Screen.width;
        // if (ratio >= 1.1f && ratio < 1.3f) // 2176x1812 Fold
        // {
        //     gameTitle.anchoredPosition = new Vector2(gameTitle.anchoredPosition.x, 460);
        //     tapToPlay.anchoredPosition = new Vector2(tapToPlay.anchoredPosition.x, 320);
        // }

        Camera.main.fieldOfView = 85; // To zoom in on start

        skin = gameObject.AddComponent<MainMenuSkin>();
        skin.Build(gameTitle.GetComponent<TMPro.TMP_Text>(), (RectTransform)transform.Find("TapToPlayButton/Text (TMP)"),
                   buttonsContainer, (RectTransform)transform.Find("SocialMediaContainer"));
    }

    void Start()
    {
        GetData();
        Leaderboards.SignIn(); // quiet; nothing until the boards are set up
    }

    public void GetData()
    {
        StartCoroutine(FetchData());
    }

    // P8-03: the update gate now fails OPEN.
    //
    // It used to fail closed, and that is what made a lapsed domain fatal rather than merely
    // annoying. Three separate faults combined:
    //
    //   1. Only Result.ConnectionError counted as failure. A 404 or 500 is a ProtocolError, so
    //      an error page fell into the SUCCESS branch and was handed to JsonUtility.FromJson,
    //      which throws on HTML and killed the coroutine part-way through.
    //   2. The dialog was then shown from values cached in PlayerPrefs by an earlier fetch, so a
    //      stale forceUpdateVersion kept gating startup long after the server stopped answering.
    //   3. Cancel() called Application.Quit() when that stale value applied. In the Editor Quit()
    //      is a no-op, which is why the dialog looked "stuck" rather than closing the game.
    //
    // A force-update is now honoured ONLY from a successful fetch in THIS session. An unreachable
    // or broken endpoint can no longer stop anyone from playing, whatever it serves or fails to.
    // Lives on Utility because the dialog's buttons are wired to UpdateHandler, not to this
    // class - both need the same answer to "did a fetch actually succeed this session?".

    public IEnumerator FetchData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(URL))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Remote config unavailable ({request.result}): {request.error}. Continuing without it.");
                HideUpdateDialog();
                yield break;
            }

            UpdateVersions updateVersions = null;
            try
            {
                updateVersions = JsonUtility.FromJson<UpdateVersions>(request.downloadHandler.text);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Remote config was not valid JSON, ignoring it: {e.Message}");
            }

            if (updateVersions == null)
            {
                HideUpdateDialog();
                yield break;
            }

#if UNITY_IPHONE
            PlayerStats.Instance.setUpdateVersion(updateVersions.iosUpdateVersion);
            PlayerStats.Instance.setForceUpdateVersion(updateVersions.iosForceUpdateVersion);
#else
            PlayerStats.Instance.setUpdateVersion(updateVersions.androidUpdateVersion);
            PlayerStats.Instance.setForceUpdateVersion(updateVersions.androidForceUpdateVersion);
#endif

            // Only overwrite a link when the server actually sent one. UpdateVersions defaults
            // these to "", and they are persisted permanently, so applying a partial response
            // would blank every social button with no way back.
            setLinkIfPresent(updateVersions.facebookLink, PlayerStats.Instance.setFacebookLink);
            setLinkIfPresent(updateVersions.youtubeLink, PlayerStats.Instance.setYoutubeLink);
            setLinkIfPresent(updateVersions.instagramLink, PlayerStats.Instance.setInstagramLink);
            setLinkIfPresent(updateVersions.xLink, PlayerStats.Instance.setXLink);
            if (updateVersions.blackHoles)
                MaterialsManager.releaseBlackHoles();

            Utility.remoteConfigLoaded = true;
            ShowUpdateDialog();
        }
    }

    private void setLinkIfPresent(string value, System.Action<string> setter)
    {
        if (!string.IsNullOrWhiteSpace(value))
            setter(value);
    }

    private void HideUpdateDialog()
    {
        updateDialog.gameObject.SetActive(false);
    }

    public void ShowUpdateDialog()
    {
        if (!Utility.remoteConfigLoaded)
        {
            HideUpdateDialog();
            return;
        }

        int updateVersion = PlayerStats.Instance.getUpdateVersion();
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();

        updateDialog.gameObject.SetActive(updateVersion > currentVersion || forceUpdateVersion > currentVersion);
    }

    public void UpdateTheGame()
    {
#if UNITY_ANDROID
        Application.OpenURL(string.Format("market://details?id=" + Application.identifier));
#elif UNITY_IPHONE
        // The App Store's own app id (as ShareManager's link), not the bundle id: an itms-apps
        // link with the bundle id opened nothing.
        Application.OpenURL("itms-apps://apps.apple.com/app/id6473210923");
#endif
    }

    public void Cancel()
    {
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();

        // Quit only for a force-update this session's fetch actually asked for. Without the
        // remoteConfigLoaded guard a stale cached value quits the game on Cancel - and in the
        // Editor, where Quit() does nothing, the dialog simply refuses to close.
        if (Utility.remoteConfigLoaded && forceUpdateVersion > currentVersion)
            Application.Quit();
        else
            HideUpdateDialog();
    }

    public void StartGame()
    {
        // A swipe on the mode picker ends where a tap would: that is not a tap to play. Nor is
        // one on a mode still locked (the picker nudges its lock).
        ModePicker picker = skin != null ? skin.Picker : null;
        if (picker != null && (picker.ConsumeSwipe() || !picker.CanPlay()))
            return;
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusOperations.StartGame();
    }

    void OnEnable()
    {
        // if (moons != null)
        // {
        //     Moons.resetRenderModifier();
        //     moons.setMoons();
        // }
        playerMovement.setPlayerMaterial();
        partsPool.refreshMaterials();
        if (skin != null)
            skin.Refresh(); // after a run, or a purchase
    }

    void OnDisable()
    {
        // if (moons != null)
        //     moons.clearMoons();
    }

    public void ShowShopMenu()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.showAndAddMenuToStack(Menus.ShopMenu);
    }

    public void ShowDiamondsShopMenu()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.showAndAddMenuToStack(Menus.PurchaseMenu);
    }

    public void ShowSettingsMenu()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.showAndAddMenuToStack(Menus.SettingsMenu);
    }

    public void ShowUpgradeMenu()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.showAndAddMenuToStack(Menus.UpgradeMenu);
    }

    public void OpenFacebook()
    {
        openUrl(PlayerStats.Instance.getFacebookLink());
    }

    public void OpenYoutube()
    {
        openUrl(PlayerStats.Instance.getYoutubeLink());
    }

    public void OpenInstagram()
    {
        openUrl(PlayerStats.Instance.getInstagramLink());
    }

    public void OpenX()
    {
        openUrl(PlayerStats.Instance.getXLink());
    }

    private void openUrl(string url)
    {
        Application.OpenURL(url);
    }

}
