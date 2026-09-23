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
    private int turnsWithTutorialsCount = 0;
    private bool pickedGamePlayChoice = false;
    private IEnumerator animationsCoroutine;

    void Awake()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        inputManager = FindObjectOfType<InputManager>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        menusController = FindObjectOfType<MenusController>();
        pathMaker = FindObjectOfType<PathMaker>();
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
        tutorialsContainer.gameObject.SetActive(PlayerStats.Instance.isTutorialsOn());
        scoreText.gameObject.SetActive(!PlayerStats.Instance.isTutorialsOn());
        pauseButton.gameObject.SetActive(!PlayerStats.Instance.isTutorialsOn());
        tutorialsInfo.text = (PlayerStats.Instance.getGamePlayMode() == GamePlayMode.SwipeLeftRight ? "Swipe" : "Tap") + " once you reach the flag";
        // tutorialsInfo.fontSize = 60;
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

    public void AutoTurn()
    {
        // Debug.Log("Turning " + inputManager.userCanControl());
        if (inputManager.userCanControl())
        {
            playerMovement.autoTurn();
            if (PlayerStats.Instance.isTutorialsOn())
            {
                turnsWithTutorialsCount++;
                // if (turnsWithTutorialsCount > 10)
                // {
                //     turnsWithTutorialsCount = 0;
                //     PlayerStats.Instance.setTutorialsState(false);
                // }
                if (turnsWithTutorialsCount == 2)
                {
                    setThirdMessage();
                }
            }
        }
    }

    public void TurnLeft()
    {
        if (inputManager.userCanControl())
        {
            if (PlayerStats.Instance.isTutorialsOn() && ((TurnDirection)pathMaker.nextDirection).nextDistination == NextDistination.RIGHT) return;
            playerMovement.turnLeft();
            if (PlayerStats.Instance.isTutorialsOn())
            {
                turnsWithTutorialsCount++;
                // if (turnsWithTutorialsCount > 10)
                // {
                //     turnsWithTutorialsCount = 0;
                //     PlayerStats.Instance.setTutorialsState(false);
                // }
                if (turnsWithTutorialsCount == 2)
                {
                    setThirdMessage();
                }
            }
        }
    }

    public void TurnRight()
    {
        if (inputManager.userCanControl())
        {
            if (PlayerStats.Instance.isTutorialsOn() && ((TurnDirection)pathMaker.nextDirection).nextDistination == NextDistination.LEFT) return;
            playerMovement.turnRight();
            if (PlayerStats.Instance.isTutorialsOn())
            {
                turnsWithTutorialsCount++;
                // if (turnsWithTutorialsCount > 10)
                // {
                //     turnsWithTutorialsCount = 0;
                //     PlayerStats.Instance.setTutorialsState(false);
                // }
                if (turnsWithTutorialsCount == 2)
                {
                    setThirdMessage();
                }
            }
        }
    }

    private void setThirdMessage()
    {
        // FinishTutorial();
        StartCoroutine(finishTutorialNumerator());
    }

    private IEnumerator finishTutorialNumerator()
    {
        turnsWithTutorialsCount = 0;

        if (tutorialsInfo.alpha > 0)
            tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");

        yield return new WaitForSecondsRealtime(0.4f);

        // tutorialsInfo.fontSize = 70;
        tutorialsInfo.rectTransform.anchoredPosition3D = new Vector3(0, -800, 0);
        tutorialsInfo.text = "Perfect!";
        showTutorialInfo();

        yield return new WaitForSecondsRealtime(1);

        if (tutorialsInfo.alpha > 0)
            tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");

        yield return new WaitForSecondsRealtime(0.4f);

        tutorialsInfo.text = "You are ready to go!";
        showTutorialInfo();

        yield return new WaitForSecondsRealtime(1.5f);

        scoreText.gameObject.SetActive(true);
        PlayerStats.Instance.setTutorialsState(false);
        GameAnalytics.TutorialComplete();

        // yield return new WaitForSecondsRealtime(1);

        if (tutorialsInfo.alpha > 0)
            tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");

        yield return new WaitForSecondsRealtime(0.4f);

        tutorialsInfo.rectTransform.anchoredPosition3D = new Vector3(0, -232, 0);
        tutorialsContainer.gameObject.SetActive(false);
        pauseButton.gameObject.SetActive(true);
    }

    public void FinishTutorial()
    {
        GameAnalytics.TutorialSkipped();
        // AutoTurn();
        if (tutorialsInfo.alpha > 0)
            tutorialsInfo.GetComponent<Animation>().Play("HideTutorialInfo");
        turnsWithTutorialsCount = 0;
        PlayerStats.Instance.setTutorialsState(false);
        tutorialsContainer.gameObject.SetActive(false);
        pauseButton.gameObject.SetActive(true);
        scoreText.gameObject.SetActive(true);
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

    public void showTutorialInfo()
    {
        tutorialsInfo.GetComponent<Animation>().Play("ShowTutorialInfo");
    }

    public void playTutorialsAnimation()
    {
        // if (turnsWithTutorialsCount == 1 && !pickedGamePlayChoice)
        // {
        //     // tutorialsInfo.text = "How do you prefer to play?";
        //     // showTutorialInfo();
        //     // hideGamePlayControls();
        //     // gamePlayChoicesContainer.gameObject.SetActive(true);
        //     // if (animationsCoroutine != null)
        //     //     StopCoroutine(animationsCoroutine);
        //     // animationsCoroutine = animationsIEnumerator();
        //     // StartCoroutine(animationsCoroutine);
        // }
        // else
        // {
        GamePlayMode selectedGamePlayMode = PlayerStats.Instance.getGamePlayMode();
        switch (selectedGamePlayMode)
        {
            case GamePlayMode.TapLeftRight:
                tutorialsLeftRightDivider.gameObject.SetActive(true);
                if (((TurnDirection)pathMaker.nextDirection).nextDistination == NextDistination.RIGHT)
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
                if (((TurnDirection)pathMaker.nextDirection).nextDistination == NextDistination.RIGHT)
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
        if (turnsWithTutorialsCount > 1)
        {
            tutorialsFinish.gameObject.SetActive(true);
        }
        else
        {
            tutorialsFinish.gameObject.SetActive(false);
        }
        gamePlayChoicesContainer.gameObject.SetActive(false);
        // }
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

        if (currentPickUps.Count == 0)
        {
            pickedPickUps.sizeDelta = new Vector2(100, 100);
            pickedPickUps.gameObject.SetActive(true);
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
