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
    private UpgradeMenuSkin skin;
    private UpgradeMenuSkin.Card doublePointsCard, boltCard, chanceCard;

    void Awake()
    {
        menusController = FindObjectOfType<MenusController>();
        doublePointsSlider.maxValue = Utility.Constants.DOUBLE_POINTS_MAX_LEVEL;
        boltSlider.maxValue = Utility.Constants.BOLT_MAX_LEVEL;
        chanceSlider.maxValue = Utility.Constants.CHANCE_MAX_LEVEL;

        // The Nebula look (UiKit), tried here first.
        skin = gameObject.AddComponent<UpgradeMenuSkin>();
        skin.BuildHeader(transform.Find("TitleContainer/Title").GetComponent<TMP_Text>(),
                         (RectTransform)transform.Find("TitleContainer/DiamondsOwned"));
        doublePointsCard = skin.BuildCard((RectTransform)transform.Find("DoublePoints"), -420, UiKit.Gold,
            "Every point counts twice", doublePointsSlider, Utility.Constants.DOUBLE_POINTS_MAX_LEVEL,
            doublePointsLevelTitle, doublePointsNextLevelTitle, doublePointsUpgradeButton);
        boltCard = skin.BuildCard((RectTransform)transform.Find("Bolt"), -740, UiKit.Hex("4FA8FF"),
            "Full speed, and it turns by itself", boltSlider, Utility.Constants.BOLT_MAX_LEVEL,
            boltLevelTitle, boltNextLevelTitle, boltUpgradeButton);
        chanceCard = skin.BuildCard((RectTransform)transform.Find("Chance"), -1060, UiKit.Hex("FF5C7A"),
            "Back on the track after a fall", chanceSlider, Utility.Constants.CHANCE_MAX_LEVEL,
            chanceLevelTitle, chanceNextLevelTitle, chanceUpgradeButton);
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
        PlayerStats stats = PlayerStats.Instance;
        int diamonds = stats.getDiamondsCount();

        int level = stats.getDoublePointsLevel();
        bool maxed = level >= Utility.Constants.DOUBLE_POINTS_MAX_LEVEL;
        doublePointsLevelTitle.text = levelText(level, Utility.Constants.DOUBLE_POINTS_MAX_LEVEL);
        doublePointsNextLevelTitle.text = effectText("Lasts", seconds(Utility.getDoublePointsPeriod()),
                                                     maxed ? null : seconds(Utility.getDoublePointsPeriod(level + 1)));
        skin.SetState(doublePointsCard, maxed, diamonds >= Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE);

        level = stats.getBoltLevel();
        maxed = level >= Utility.Constants.BOLT_MAX_LEVEL;
        boltLevelTitle.text = levelText(level, Utility.Constants.BOLT_MAX_LEVEL);
        boltNextLevelTitle.text = effectText("Runs", meters(Utility.getBoltDistance()),
                                             maxed ? null : meters(Utility.getBoltDistance(level + 1)));
        skin.SetState(boltCard, maxed, diamonds >= Utility.Constants.BOLT_UPGRADE_PRICE);

        level = stats.getChanceLevel();
        maxed = level >= Utility.Constants.CHANCE_MAX_LEVEL;
        chanceLevelTitle.text = levelText(level, Utility.Constants.CHANCE_MAX_LEVEL);
        chanceNextLevelTitle.text = effectText("Saves", times(Utility.getChanceTimes()),
                                               maxed ? null : times(Utility.getChanceTimes(level + 1)));
        skin.SetState(chanceCard, maxed, diamonds >= Utility.Constants.CHANCE_UPGRADE_PRICE);
    }

    private static string levelText(int level, int max)
    {
        return "LV " + level + "/" + max;
    }

    // "Lasts 5s   NEXT 10s": what the power-up does now, and at the next level in the accent
    // colour (at the top level the MAX badge says the rest). Plain letters: the font has no arrows.
    private static string effectText(string verb, string now, string next)
    {
        string text = "<color=" + UiKit.HexOf(UiKit.TextDim) + ">" + verb + "</color> " + now;
        if (next == null)
            return text;
        return text + "    <size=75%><color=" + UiKit.HexOf(UiKit.TextDim) + ">NEXT</color></size> <color=" +
               UiKit.HexOf(UiKit.Cyan) + ">" + next + "</color>";
    }

    private static string seconds(float s)
    {
        return s.ToString("0.#") + "s";
    }

    private static string meters(float m)
    {
        return m.ToString("0") + " m";
    }

    private static string times(float n)
    {
        return n.ToString("0") + (n == 1 ? " fall" : " falls");
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
                celebrate(doublePointsCard, PlayerStats.Instance.getDoublePointsLevel(), Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE);
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
                celebrate(boltCard, PlayerStats.Instance.getBoltLevel(), Utility.Constants.BOLT_UPGRADE_PRICE);
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
                celebrate(chanceCard, PlayerStats.Instance.getChanceLevel(), Utility.Constants.CHANCE_UPGRADE_PRICE);
                GameAnalytics.Upgraded("chance", PlayerStats.Instance.getChanceLevel(), Utility.Constants.CHANCE_UPGRADE_PRICE);
            }
        }
        else
        {
            showNotEnoughDiamondsDialog();
        }
    }

    private void celebrate(UpgradeMenuSkin.Card card, int level, int price)
    {
        skin.Celebrate(card, level, price);
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayUpgrade(level);
    }

    private void showNotEnoughDiamondsDialog()
    {
        confirmationDialog.setConfirmationDialog("Upgrade Failed", "You do not have enough diamonds to upgrade.", false);
    }

}
