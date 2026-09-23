using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI diamondsCountText;
    [SerializeField] private TextMeshProUGUI doublePointsLevelTitle;
    [SerializeField] private Slider doublePointsSlider;
    [SerializeField] private RectTransform doublePointsSliderContainer;
    [SerializeField] private TextMeshProUGUI doublePointsNextLevelTitle;
    [SerializeField] private RectTransform doublePointsUpgradeButton;

    [SerializeField] private TextMeshProUGUI boltLevelTitle;
    [SerializeField] private Slider boltSlider;
    [SerializeField] private RectTransform boltSliderContainer;
    [SerializeField] private TextMeshProUGUI boltNextLevelTitle;
    [SerializeField] private RectTransform boltUpgradeButton;

    [SerializeField] private TextMeshProUGUI chanceLevelTitle;
    [SerializeField] private Slider chanceSlider;
    [SerializeField] private RectTransform chanceSliderContainer;
    [SerializeField] private TextMeshProUGUI chanceNextLevelTitle;
    [SerializeField] private RectTransform chanceUpgradeButton;
    [SerializeField] private ConfirmationDialog confirmationDialog;

    private MenusController menusController;
    private int currentDiamondsCount = 0;
    // private int diamondsToSubtract = 0;
    private float doublePointsOldLevel = 0;
    private float doublePointsLevel = 0;
    private float boltOldLevel = 0;
    private float boltLevel = 0;
    private float chanceOldLevel = 0;
    private float chanceLevel = 0;

    void Awake()
    {
        menusController = FindObjectOfType<MenusController>();
        doublePointsSlider.maxValue = Utility.Constants.DOUBLE_POINTS_MAX_LEVEL;
        boltSlider.maxValue = Utility.Constants.BOLT_MAX_LEVEL;
        chanceSlider.maxValue = Utility.Constants.CHANCE_MAX_LEVEL;
    }

    // int amountToSubtractInUpdate;
    private float timeElapsedDoublePoints = 0;
    private float timeElapsedBolt = 0;
    private float timeElapsedChance = 0;
    private float timeElapsedDiamonds = 0;
    private bool shouldUpdateDiamonds = false;
    void FixedUpdate()
    {
        // if (diamondsToSubtract != 0)
        // {
        //     amountToSubtractInUpdate = diamondsToSubtract / 40f < 1 ? 1 : diamondsToSubtract / 40;
        //     diamondsCountText.text = Utility.getFormatedNumber(currentDiamondsCount - amountToSubtractInUpdate);
        //     currentDiamondsCount -= amountToSubtractInUpdate;
        //     diamondsToSubtract -= amountToSubtractInUpdate;
        // }
        if (doublePointsSlider.value != doublePointsLevel)
        {
            doublePointsSlider.value = Mathf.SmoothStep(doublePointsOldLevel, doublePointsLevel, timeElapsedDoublePoints / 2);
            timeElapsedDoublePoints += Time.deltaTime;
            if (doublePointsSlider.value == doublePointsLevel)
            {
                doublePointsOldLevel = doublePointsLevel;
                timeElapsedDoublePoints = 0;
            }
        }
        if (boltSlider.value != boltLevel)
        {
            boltSlider.value = Mathf.SmoothStep(boltOldLevel, boltLevel, timeElapsedBolt / 2);
            timeElapsedBolt += Time.deltaTime;
            if (boltSlider.value == boltLevel)
            {
                boltOldLevel = boltLevel;
                timeElapsedBolt = 0;
            }
        }
        if (chanceSlider.value != chanceLevel)
        {
            chanceSlider.value = Mathf.SmoothStep(chanceOldLevel, chanceLevel, timeElapsedChance / 2);
            timeElapsedChance += Time.deltaTime;
            if (chanceSlider.value == chanceLevel)
            {
                chanceOldLevel = chanceLevel;
                timeElapsedChance = 0;
            }
        }

        if (shouldUpdateDiamonds)
        {
            int value = (int)Mathf.Lerp(currentDiamondsCount, PlayerStats.Instance.getDiamondsCount(), timeElapsedDiamonds / 1);
            diamondsCountText.text = Utility.getFormatedNumber(value);
            timeElapsedDiamonds += Time.deltaTime;
            if (value == PlayerStats.Instance.getDiamondsCount())
            {
                shouldUpdateDiamonds = false;
                timeElapsedDiamonds = 0;
                currentDiamondsCount = PlayerStats.Instance.getDiamondsCount();
            }
        }
    }

    private float increaseSlideValue(Slider slider, float value)
    {
        float amountToAddInUpdate = value / 40 < 0.0001f ? value : value / 40;
        slider.value += amountToAddInUpdate;
        return value - amountToAddInUpdate;
    }

    void OnEnable()
    {
        // PlayerStats.Instance.setDoublePointsLevel(1);
        // PlayerStats.Instance.setBoltLevel(1);
        // PlayerStats.Instance.setChanceLevel(1);
        timeElapsedDiamonds = 0;
        currentDiamondsCount = PlayerStats.Instance.getDiamondsCount();
        // diamondsToSubtract = 0;
        diamondsCountText.text = Utility.getFormatedNumber(currentDiamondsCount);
        doublePointsSlider.value = 0;
        boltSlider.value = 0;
        chanceSlider.value = 0;
        timeElapsedDoublePoints = 0;
        doublePointsOldLevel = 0;
        doublePointsLevel = PlayerStats.Instance.getDoublePointsLevel();
        timeElapsedBolt = 0;
        boltOldLevel = 0;
        boltLevel = PlayerStats.Instance.getBoltLevel();
        timeElapsedChance = 0;
        chanceOldLevel = 0;
        chanceLevel = PlayerStats.Instance.getChanceLevel();
        updateUI();
    }

    private void updateUI()
    {
        doublePointsLevelTitle.text = "Level " + PlayerStats.Instance.getDoublePointsLevel() + ": " + Utility.getDoublePointsPeriod() + " Seconds";
        if (PlayerStats.Instance.getDoublePointsLevel() < Utility.Constants.DOUBLE_POINTS_MAX_LEVEL)
        {
            doublePointsNextLevelTitle.text = "Next level: " + Utility.getDoublePointsPeriod(PlayerStats.Instance.getDoublePointsLevel() + 1) + " Seconds";
        }
        else
        {
            doublePointsNextLevelTitle.text = "Max Level!";
            doublePointsUpgradeButton.gameObject.SetActive(false);
            doublePointsSliderContainer.sizeDelta = new Vector2(831.74f, doublePointsSliderContainer.sizeDelta.y);
            doublePointsSliderContainer.anchoredPosition = new Vector2(84.13f, doublePointsSliderContainer.anchoredPosition.y);
        }

        boltLevelTitle.text = "Level " + PlayerStats.Instance.getBoltLevel() + ": " + Utility.getBoltDistance() + " Meters";
        if (PlayerStats.Instance.getBoltLevel() < Utility.Constants.BOLT_MAX_LEVEL)
        {
            boltNextLevelTitle.text = "Next level: " + Utility.getBoltDistance(PlayerStats.Instance.getBoltLevel() + 1) + " Meters";
        }
        else
        {
            boltNextLevelTitle.text = "Max Level!";
            boltUpgradeButton.gameObject.SetActive(false);
            boltSliderContainer.sizeDelta = new Vector2(831.74f, boltSliderContainer.sizeDelta.y);
            boltSliderContainer.anchoredPosition = new Vector2(84.13f, boltSliderContainer.anchoredPosition.y);
        }

        chanceLevelTitle.text = "Level " + PlayerStats.Instance.getChanceLevel() + ": " + Utility.getChanceTimes() + (Utility.getChanceTimes() == 1 ? " Chance" : " Chances");
        if (PlayerStats.Instance.getChanceLevel() < Utility.Constants.CHANCE_MAX_LEVEL)
        {
            chanceNextLevelTitle.text = "Next level: " + Utility.getChanceTimes(PlayerStats.Instance.getChanceLevel() + 1) + " Chances";
        }
        else
        {
            chanceNextLevelTitle.text = "Max Level!";
            chanceUpgradeButton.gameObject.SetActive(false);
            chanceSliderContainer.sizeDelta = new Vector2(831.74f, chanceSliderContainer.sizeDelta.y);
            chanceSliderContainer.anchoredPosition = new Vector2(84.13f, chanceSliderContainer.anchoredPosition.y);
        }
    }

    public void Back()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.hideCurrentMenu(false);
    }

    private IEnumerator subtractDiamondsAnimation(int diamondsCount, int amount)
    {
        float waitTime = 0.6f / 50;
        int amountToSubtract = amount / 50;
        int targetDiamondsCount = diamondsCount - amount;
        while (diamondsCount != targetDiamondsCount)
        {
            if (diamondsCount - amount < targetDiamondsCount)
                diamondsCountText.text = Utility.getFormatedNumber(targetDiamondsCount);
            else
                diamondsCountText.text = Utility.getFormatedNumber(diamondsCount - amountToSubtract);
            yield return new WaitForSecondsRealtime(waitTime);
        }
    }

    public void UpgradeDoublePoints()
    {
        if (PlayerStats.Instance.getDiamondsCount() >= Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE)
        {
            if (PlayerStats.Instance.getDoublePointsLevel() < Utility.Constants.DOUBLE_POINTS_MAX_LEVEL)
            {
                PlayerStats.Instance.setDoublePointsLevel(PlayerStats.Instance.getDoublePointsLevel() + 1);
                PlayerStats.Instance.subtractDiamonds(Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE);
                // diamondsToSubtract += Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE;
                shouldUpdateDiamonds = true;
                doublePointsLevel++;
                updateUI();
                GameAnalytics.Upgraded("double_points", PlayerStats.Instance.getDoublePointsLevel(), Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE);
            }
        }
        else
        {
            showNotEnoughDiamondsDialog();
        }
    }

    public void UpgradeBolt()
    {
        if (PlayerStats.Instance.getDiamondsCount() >= Utility.Constants.BOLT_UPGRADE_PRICE)
        {
            if (PlayerStats.Instance.getBoltLevel() < Utility.Constants.BOLT_MAX_LEVEL)
            {
                PlayerStats.Instance.setBoltLevel(PlayerStats.Instance.getBoltLevel() + 1);
                PlayerStats.Instance.subtractDiamonds(Utility.Constants.BOLT_UPGRADE_PRICE);
                // diamondsToSubtract += Utility.Constants.BOLT_UPGRADE_PRICE;
                shouldUpdateDiamonds = true;
                boltLevel++;
                updateUI();
                GameAnalytics.Upgraded("bolt", PlayerStats.Instance.getBoltLevel(), Utility.Constants.BOLT_UPGRADE_PRICE);
            }
        }
        else
        {
            showNotEnoughDiamondsDialog();
        }
    }

    public void UpgradeChance()
    {
        if (PlayerStats.Instance.getDiamondsCount() >= Utility.Constants.CHANCE_UPGRADE_PRICE)
        {
            if (PlayerStats.Instance.getChanceLevel() < Utility.Constants.CHANCE_MAX_LEVEL)
            {
                PlayerStats.Instance.setChanceLevel(PlayerStats.Instance.getChanceLevel() + 1);
                PlayerStats.Instance.subtractDiamonds(Utility.Constants.CHANCE_UPGRADE_PRICE);
                // diamondsToSubtract += Utility.Constants.CHANCE_UPGRADE_PRICE;
                shouldUpdateDiamonds = true;
                chanceLevel++;
                updateUI();
                GameAnalytics.Upgraded("chance", PlayerStats.Instance.getChanceLevel(), Utility.Constants.CHANCE_UPGRADE_PRICE);
            }
        }
        else
        {
            showNotEnoughDiamondsDialog();
        }
    }

    private void showNotEnoughDiamondsDialog()
    {
        confirmationDialog.setConfirmationDialog("Upgrade Failed", "You do not have enough diamonds to upgrade.", false);
    }

}
