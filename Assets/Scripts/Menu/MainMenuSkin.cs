using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main menu, tidied (made over its scene objects when it wakes): the best score and the
/// diamonds under the title - what there is to beat and to spend - the four buttons on a glass
/// bar so they read on any sky, a dot on Upgrade when one can be bought, and the social buttons
/// smaller and out of the way. The menu's own look is kept; the glass is the Nebula kit's (UiKit).
/// </summary>
public class MainMenuSkin : MonoBehaviour
{
    private TextMeshProUGUI best, diamonds;
    private ModePicker picker;
    private RunMode shownMode = RunMode.Standard;
    private bool modeShown;

    public ModePicker Picker => picker;

    /// <summary>The mode picker moved: the scores row shows that mode's best.</summary>
    public void ShowMode(RunMode mode)
    {
        shownMode = mode;
        modeShown = true;
        Refresh();
    }
    private GameObject upgradeDot, shopDot;

    public void Build(TMP_Text title, RectTransform tapToPlay, RectTransform buttons, RectTransform social)
    {
        // The scores row, under the title.
        RectTransform row = UiKit.Rect("Stats", transform);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0, -392);
        row.sizeDelta = new Vector2(640, 64);
        best = chip(title, row, "Best", new Vector2(-160, 0), 290, Leaderboards.Configured() ? "trophy" : null);
        // Once the leaderboards are set up, the best chip opens the board of the mode on screen.
        if (Leaderboards.Configured())
        {
            Image chipBack = best.transform.parent.GetComponent<Image>();
            chipBack.raycastTarget = true;
            Button open = chipBack.gameObject.AddComponent<Button>();
            open.transition = Selectable.Transition.None;
            open.onClick.AddListener(() => Leaderboards.Show(modeShown ? shownMode : PlayerStats.Instance.getRunMode()));
            chipBack.gameObject.AddComponent<PressDip>();
        }
        diamonds = chip(title, row, "Diamonds", new Vector2(160, 0), 290, "gem");

        // The mode picker under the scores (ModePicker), "tap to play" under it, a little larger.
        if (tapToPlay != null && tapToPlay.parent is RectTransform tapArea)
            picker = ModePicker.Build(title, (RectTransform)transform, tapArea, this);
        if (tapToPlay != null)
        {
            tapToPlay.anchoredPosition = new Vector2(tapToPlay.anchoredPosition.x, -790);
            TMP_Text tap = tapToPlay.GetComponent<TMP_Text>();
            if (tap != null)
                tap.fontSize *= 1.15f;
        }

        // The buttons on a glass bar: each icon and its name centred in the bar (they sat high,
        // with twice the room under them as over), the icons in the theme's gradient, thin lines
        // between them. The bar takes the taps that miss a button - one there started the game -
        // and each button's whole tile is its target, not just the icon.
        if (buttons != null)
            buildBar(buttons);

        // The social buttons: smaller, closer together, under the scores' line.
        if (social != null)
        {
            int i = 0;
            foreach (RectTransform b in social)
            {
                b.sizeDelta = new Vector2(66, 66);
                b.anchoredPosition = new Vector2(70, -i * 92);
                i++;
            }
        }
        Refresh();
    }

    private const float BAR_HEIGHT = 236f;
    private const float ICON = 100f;
    private const float LABEL_HEIGHT = 46f, LABEL_GAP = 8f;

    private void buildBar(RectTransform buttons)
    {
        Image bar = UiKit.Image(buttons, "Bar", "round_sheen", UiKit.WithAlpha(UiKit.Glass, 0.72f));
        UiKit.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(buttons.rect.width - 60f, BAR_HEIGHT));
        bar.pixelsPerUnitMultiplier = 0.8f;
        bar.raycastTarget = true; // a tap on the bar between buttons does nothing
        bar.transform.SetAsFirstSibling();
        Image edge = UiKit.Image(bar.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.55f));
        edge.pixelsPerUnitMultiplier = bar.pixelsPerUnitMultiplier;
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);

        // The icon's centre, so that icon, gap and name are centred in the bar together.
        float block = ICON + LABEL_GAP + LABEL_HEIGHT;
        float iconY = block / 2f - ICON / 2f;
        float[] xs = { -400f, -133.3f, 133.3f, 400f };
        for (int i = 1; i < xs.Length; i++)
        {
            Image line = UiKit.Image(bar.transform, "Divider" + i, "round_fill", UiKit.WithAlpha(Color.white, 0.12f));
            line.type = Image.Type.Simple;
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((xs[i - 1] + xs[i]) / 2f, 0f), new Vector2(2f, BAR_HEIGHT - 70f));
        }

        foreach (string name in new[] { "ShopButton", "UpgradeButton", "PurchaseButton", "SettingsButton", "NoAdsButton" })
        {
            RectTransform b = buttons.Find(name) as RectTransform;
            if (b == null)
                continue;
            float x = b.anchoredPosition.x;
            UiKit.Place(b, new Vector2(0.5f, 0.5f), new Vector2(x, iconY), new Vector2(ICON, ICON));
            Image icon = b.GetComponent<Image>();
            if (icon != null && name != "NoAdsButton")
            {
                icon.color = Color.white;
                UiKit.Gradient(icon, UiKit.Hex("6FE6FF"), UiKit.Hex("9C7BFF"), true);
            }
            // The tile: the whole of the button's share of the bar opens it.
            if (b.Find("Tile") == null)
            {
                Image tile = UiKit.Image(b, "Tile", "round_fill", Color.clear);
                tile.raycastTarget = true;
                UiKit.Place(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -iconY), new Vector2(250f, BAR_HEIGHT - 12f));
                tile.transform.SetAsFirstSibling();
            }
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.colorGradientPreset = null;
                label.enableVertexGradient = false;
                label.color = UiKit.Text;
                label.enableAutoSizing = false;
                label.fontSize = 34f;
                label.alignment = TextAlignmentOptions.Top;
                RectTransform l = label.rectTransform;
                l.anchorMin = l.anchorMax = new Vector2(0.5f, 0f);
                l.pivot = new Vector2(0.5f, 1f);
                l.anchoredPosition = new Vector2(0f, -LABEL_GAP);
                l.sizeDelta = new Vector2(240f, LABEL_HEIGHT);
            }
            // Pressed: the tile dips a little.
            if (b.GetComponent<PressDip>() == null)
                b.gameObject.AddComponent<PressDip>();
        }

        Transform upgrade = buttons.Find("UpgradeButton");
        if (upgrade != null)
            upgradeDot = UiKit.Dot(upgrade, "CanUpgrade", new Vector2(4, 4));
        // And on Shop when a ball or floor has been won or bought and never put on.
        Transform shop = buttons.Find("ShopButton");
        if (shop != null)
            shopDot = UiKit.Dot(shop, "SomethingNew", new Vector2(4, 4));
    }

    private static TextMeshProUGUI chip(TMP_Text like, RectTransform row, string name, Vector2 at, float width, string icon)
    {
        Image back = UiKit.Image(row, name, "round_sheen", UiKit.WithAlpha(UiKit.Glass, 0.7f));
        back.pixelsPerUnitMultiplier = 1.25f; // corners half the height: a pill
        UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), at, new Vector2(width, 64));
        Image edge = UiKit.Image(back.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.55f));
        edge.pixelsPerUnitMultiplier = 1.25f;
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);
        float textLeft = CHIP_PAD;
        if (icon != null)
        {
            Image gem = UiKit.Image(back.transform, "Icon", icon, Color.white);
            UiKit.Gradient(gem, UiKit.Hex("E58CFF"), UiKit.Hex("8A3CFF"), true);
            gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0, 0.5f);
            gem.rectTransform.anchoredPosition = new Vector2(38, 0);
            gem.rectTransform.sizeDelta = new Vector2(44, 44);
            textLeft = CHIP_ICON_ROOM;
        }
        TextMeshProUGUI text = UiKit.Label(like, back.transform, "Text", "", 30, UiKit.Text, TextAlignmentOptions.Center);
        text.richText = true;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(textLeft, 0);
        text.rectTransform.offsetMax = new Vector2(-CHIP_PAD, 0);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    // A chip is as wide as what it says: the icon's room on the left, the text, a margin each side.
    private const float CHIP_PAD = 26f, CHIP_ICON_ROOM = 68f, CHIP_MIN = 150f, CHIP_GAP = 24f;

    private static float fitChip(TextMeshProUGUI text)
    {
        RectTransform back = (RectTransform)text.transform.parent;
        float width = Mathf.Max(CHIP_MIN, -text.rectTransform.offsetMax.x + text.rectTransform.offsetMin.x +
                                          text.GetPreferredValues(text.text, 10000f, 64f).x);
        back.sizeDelta = new Vector2(Mathf.Ceil(width), back.sizeDelta.y);
        return back.sizeDelta.x;
    }

    /// <summary>The numbers, and the Upgrade dot (when the menu shows).</summary>
    public void Refresh()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null || best == null)
            return;
        // The best of the mode on screen; Chill's longest climb without falling in its place.
        RunMode mode = modeShown ? shownMode : stats.getRunMode();
        bool chill = mode == RunMode.Chill;
        int high = chill ? stats.getChillLongestStreak() : stats.getHighScore(mode);
        // The word a little smaller than the number and raised to sit on its middle, not its foot.
        best.text = "<size=88%><voffset=0.06em><color=" + UiKit.HexOf(UiKit.TextDim) + ">" + (chill ? "LONGEST" : "BEST") +
                    "</color></voffset></size>  " + high;
        best.transform.parent.gameObject.SetActive(high > 0);
        diamonds.text = Utility.getFormatedNumber(stats.getDiamondsCount());
        // Each chip fits its number; the two sit side by side round the middle (the diamonds on
        // their own there until there is a best score beside them).
        float wd = fitChip(diamonds);
        if (high > 0)
        {
            float wb = fitChip(best);
            float total = wb + CHIP_GAP + wd;
            ((RectTransform)best.transform.parent).anchoredPosition = new Vector2(-total / 2f + wb / 2f, 0);
            ((RectTransform)diamonds.transform.parent).anchoredPosition = new Vector2(total / 2f - wd / 2f, 0);
        }
        else
            ((RectTransform)diamonds.transform.parent).anchoredPosition = Vector2.zero;
        if (upgradeDot != null)
            upgradeDot.SetActive(canUpgrade(stats));
        if (shopDot != null)
            shopDot.SetActive(stats.hasNewBall() || stats.hasNewFloor());
    }

    private static bool canUpgrade(PlayerStats stats)
    {
        int d = stats.getDiamondsCount();
        return (stats.getDoublePointsLevel() < Utility.Constants.DOUBLE_POINTS_MAX_LEVEL && d >= Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE)
            || (stats.getBoltLevel() < Utility.Constants.BOLT_MAX_LEVEL && d >= Utility.Constants.BOLT_UPGRADE_PRICE)
            || (stats.getChanceLevel() < Utility.Constants.CHANCE_MAX_LEVEL && d >= Utility.Constants.CHANCE_UPGRADE_PRICE);
    }
}

/// <summary>A main menu button dips a little while pressed.</summary>
public class PressDip : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler,
                        UnityEngine.EventSystems.IPointerExitHandler
{
    private float target = 1f;
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { target = 0.9f; }
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { target = 1f; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { target = 1f; }
    void OnDisable() { target = 1f; transform.localScale = Vector3.one; }
    void Update()
    {
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 2.5f);
        transform.localScale = Vector3.one * s;
    }
}
