using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{

    [SerializeField] private Toggle autoPilotToggle;
    [SerializeField] private Toggle tutorialsToggle;
    [SerializeField] private Toggle stayInSpaceToggle;
    [SerializeField] private Toggle emissionToggle;
    [SerializeField] private Image singleTapImage;
    [SerializeField] private Image tapLeftRightImage;
    [SerializeField] private Image swipeLeftRightImage;
    [SerializeField] private RectTransform tutorialsSingleTapContainer;
    [SerializeField] private RectTransform tutorialsRightTapContainer;
    [SerializeField] private RectTransform tutorialsLeftTapContainer;
    [SerializeField] private RectTransform tutorialsSwipeRightContainer;
    [SerializeField] private RectTransform tutorialsSwipeLeftContainer;

    // P2-07: Google requires users in regulated regions (EEA/UK and some US states) to be able to
    // change their ad-consent choice from inside the app. This button is only shown to them.
    // It must be created in the Settings panel in the Editor and its OnClick wired to
    // OpenPrivacyOptions(). Left unassigned, the menu works exactly as before.
    [SerializeField] private Button privacyOptionsButton;

    private MenusController menusController;
    private PlayerStats playerStats;
    private MaterialsManager materialsManager;
    private Color selectedColor = new Color32(60, 136, 171, 65);
    private Color unselectedColor = new Color32(91, 91, 91, 65);
    private IEnumerator animationsCoroutine;

    // Start is called before the first frame update
    void Start()
    {
        menusController = FindObjectOfType<MenusController>();
        playerStats = PlayerStats.Instance;
        materialsManager = FindAnyObjectByType<MaterialsManager>();

        autoPilotToggle.onValueChanged.AddListener(delegate
        {
            autoPilotToggleValueChanged();
        });
        tutorialsToggle.onValueChanged.AddListener(delegate
        {
            tutorialsToggleValueChanged();
        });
        stayInSpaceToggle.onValueChanged.AddListener(delegate
        {
            stayInSpaceToggleValueChanged();
        });
        emissionToggle.onValueChanged.AddListener(delegate
        {
            emissionToggleValueChanged();
        });

        tutorialsRightTapContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
        tutorialsLeftTapContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
        tutorialsSwipeRightContainer.GetComponent<Animation>()["TutorialsSwipeRight"].speed = 0.7f;
        tutorialsSwipeLeftContainer.GetComponent<Animation>()["TutorialsSwipeLeft"].speed = 0.7f;
        tutorialsSingleTapContainer.GetComponent<Animation>()["TutorialsTap"].speed = 0.7f;
    }

    void OnEnable()
    {
        autoPilotToggle.isOn = PlayerStats.Instance.isAutoPilotOn();
        tutorialsToggle.isOn = PlayerStats.Instance.isTutorialsOn();
        stayInSpaceToggle.isOn = PlayerStats.Instance.isStayInSpaceOn();
        emissionToggle.isOn = PlayerStats.Instance.isEmissionOn();

        // Re-evaluated every time the menu opens: the requirement is only known once UMP has
        // answered, which can be after the menu was first built.
        if (privacyOptionsButton != null)
        {
            privacyOptionsButton.gameObject.SetActive(
                AdmobManager.Instance != null && AdmobManager.Instance.IsPrivacyOptionsRequired);
        }

        GamePlayMode selectedGamePlayMode = PlayerStats.Instance.getGamePlayMode();
        switch (selectedGamePlayMode)
        {
            case GamePlayMode.TapLeftRight:
                SelectTapLeftRightMode();
                break;
            case GamePlayMode.SwipeLeftRight:
                SelectSwipeLeftRightMode();
                break;
            default:
                SelectSingleTapMode();
                break;
        }

        if (Debug.isDebugBuild)
        {
            autoPilotToggle.gameObject.SetActive(true);
        }

        if (animationsCoroutine != null)
            StopCoroutine(animationsCoroutine);
        animationsCoroutine = animationsIEnumerator();
        StartCoroutine(animationsCoroutine);
    }

    void OnDisable()
    {
        tutorialsRightTapContainer.gameObject.SetActive(false);
        tutorialsSwipeRightContainer.gameObject.SetActive(false);
        tutorialsLeftTapContainer.gameObject.SetActive(false);
        tutorialsSwipeLeftContainer.gameObject.SetActive(false);
        tutorialsRightTapContainer.GetComponent<Animation>().Stop();
        tutorialsSwipeRightContainer.GetComponent<Animation>().Stop();
        tutorialsLeftTapContainer.GetComponent<Animation>().Stop();
        tutorialsSwipeLeftContainer.GetComponent<Animation>().Stop();
    }

    private IEnumerator animationsIEnumerator()
    {
        while (true)
        {
            tutorialsRightTapContainer.gameObject.SetActive(true);
            tutorialsSwipeRightContainer.gameObject.SetActive(true);
            tutorialsRightTapContainer.GetComponent<Animation>().Play();
            tutorialsSwipeRightContainer.GetComponent<Animation>().Play();
            yield return new WaitForSecondsRealtime(1.75f);
            tutorialsRightTapContainer.GetComponent<Animation>().Stop();
            tutorialsSwipeRightContainer.GetComponent<Animation>().Stop();
            yield return new WaitForSecondsRealtime(0.3f);
            tutorialsLeftTapContainer.gameObject.SetActive(true);
            tutorialsSwipeLeftContainer.gameObject.SetActive(true);
            tutorialsLeftTapContainer.GetComponent<Animation>().Play();
            tutorialsSwipeLeftContainer.GetComponent<Animation>().Play();
            yield return new WaitForSecondsRealtime(1.75f);
            tutorialsLeftTapContainer.GetComponent<Animation>().Stop();
            tutorialsSwipeLeftContainer.GetComponent<Animation>().Stop();
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }

    public void OpenPrivacyOptions()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;
        if (AdmobManager.Instance == null) return;

        AdmobManager.Instance.ShowPrivacyOptionsForm();
    }

    public void Back()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.hideCurrentMenu(false);
    }

    public void SelectSingleTapMode()
    {
        PlayerStats.Instance.setGamePlayMode(GamePlayMode.SingleTap);
        singleTapImage.color = selectedColor;
        tapLeftRightImage.color = unselectedColor;
        swipeLeftRightImage.color = unselectedColor;
    }

    public void SelectTapLeftRightMode()
    {
        PlayerStats.Instance.setGamePlayMode(GamePlayMode.TapLeftRight);
        singleTapImage.color = unselectedColor;
        tapLeftRightImage.color = selectedColor;
        swipeLeftRightImage.color = unselectedColor;
    }

    public void SelectSwipeLeftRightMode()
    {
        PlayerStats.Instance.setGamePlayMode(GamePlayMode.SwipeLeftRight);
        singleTapImage.color = unselectedColor;
        tapLeftRightImage.color = unselectedColor;
        swipeLeftRightImage.color = selectedColor;
    }

    private void autoPilotToggleValueChanged() => PlayerStats.Instance.setAutoPilotState(autoPilotToggle.isOn);

    private void tutorialsToggleValueChanged() => PlayerStats.Instance.setTutorialsState(tutorialsToggle.isOn);

    private void stayInSpaceToggleValueChanged() => PlayerStats.Instance.setStayInSpaceState(stayInSpaceToggle.isOn);

    private void emissionToggleValueChanged()
    {
        PlayerStats.Instance.setEmissionState(emissionToggle.isOn);
        // materialsManager.setEmissionState(emissionToggle.isOn);
    }
}
