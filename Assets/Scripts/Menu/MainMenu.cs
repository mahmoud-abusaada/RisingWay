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
    }

    void Start()
    {
        GetData();
    }

    public void GetData()
    {
        StartCoroutine(FetchData());
    }

    public IEnumerator FetchData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(URL))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.Log(request.error);
                ShowUpdateDialog();
            }
            else
            {
                UpdateVersions updateVersions = new UpdateVersions();
                updateVersions = JsonUtility.FromJson<UpdateVersions>(request.downloadHandler.text);

#if UNITY_IPHONE
                PlayerStats.Instance.setUpdateVersion(updateVersions.iosUpdateVersion);
                PlayerStats.Instance.setForceUpdateVersion(updateVersions.iosForceUpdateVersion);
#else
                PlayerStats.Instance.setUpdateVersion(updateVersions.androidUpdateVersion);
                PlayerStats.Instance.setForceUpdateVersion(updateVersions.androidForceUpdateVersion);
#endif

                PlayerStats.Instance.setFacebookLink(updateVersions.facebookLink);
                PlayerStats.Instance.setYoutubeLink(updateVersions.youtubeLink);
                PlayerStats.Instance.setInstagramLink(updateVersions.instagramLink);
                PlayerStats.Instance.setXLink(updateVersions.xLink);

                Debug.Log(request.downloadHandler.text);
                Debug.Log(updateVersions.iosUpdateVersion);
                ShowUpdateDialog();
            }
        }
    }

    public void ShowUpdateDialog()
    {
        int updateVersion = PlayerStats.Instance.getUpdateVersion();
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();
        Debug.Log("Current version = " + VersionCode.GetVersionCode());

        updateDialog.gameObject.SetActive(updateVersion > currentVersion || forceUpdateVersion > currentVersion);
    }

    public void UpdateTheGame()
    {
#if UNITY_ANDROID
        Application.OpenURL(string.Format("market://details?id=" + Application.identifier));
#elif UNITY_IPHONE
        Application.OpenURL("itms-apps://itunes.apple.com/app/" + Application.identifier);
#endif
    }

    public void Cancel()
    {
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();

        if (forceUpdateVersion > currentVersion)
            Application.Quit();
        else
            updateDialog.gameObject.SetActive(false);
    }

    public void StartGame()
    {
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
