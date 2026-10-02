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
    private GameObject upgradeDot;

    public void Build(TMP_Text title, RectTransform tapToPlay, RectTransform buttons, RectTransform social)
    {
        // The scores row, under the title.
        RectTransform row = UiKit.Rect("Stats", transform);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0, -392);
        row.sizeDelta = new Vector2(640, 64);
        best = chip(title, row, "Best", new Vector2(-160, 0), 290, null);
        diamonds = chip(title, row, "Diamonds", new Vector2(160, 0), 290, "gem");

        // "Tap to play" a little lower and larger, out from under the scores.
        if (tapToPlay != null)
        {
            tapToPlay.anchoredPosition = new Vector2(tapToPlay.anchoredPosition.x, -540);
            TMP_Text tap = tapToPlay.GetComponent<TMP_Text>();
            if (tap != null)
                tap.fontSize *= 1.15f;
        }

        // The buttons on glass, their names in white.
        if (buttons != null)
        {
            Image bar = UiKit.Image(buttons, "Bar", "round_sheen", UiKit.WithAlpha(UiKit.Glass, 0.62f));
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.offsetMin = new Vector2(30, -6);
            bar.rectTransform.offsetMax = new Vector2(-30, 14);
            bar.transform.SetAsFirstSibling();
            Image edge = UiKit.Image(bar.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.45f));
            UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
            UiKit.Stretch(edge.rectTransform);
            foreach (TMP_Text label in buttons.GetComponentsInChildren<TMP_Text>(true))
            {
                label.colorGradientPreset = null;
                label.enableVertexGradient = false;
                label.color = UiKit.Text;
            }
            Transform upgrade = buttons.Find("UpgradeButton");
            if (upgrade != null)
            {
                Image dot = UiKit.Image(upgrade, "CanUpgrade", "circle", UiKit.Gold);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1, 1);
                dot.rectTransform.anchoredPosition = new Vector2(-4, -4);
                dot.rectTransform.sizeDelta = new Vector2(30, 30);
                Image halo = UiKit.Image(dot.transform, "Glow", "glow", UiKit.WithAlpha(UiKit.Gold, 0.6f));
                UiKit.Stretch(halo.rectTransform, 18);
                halo.transform.SetAsFirstSibling();
                upgradeDot = dot.gameObject;
            }
        }

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

    private static TextMeshProUGUI chip(TMP_Text like, RectTransform row, string name, Vector2 at, float width, string icon)
    {
        Image back = UiKit.Image(row, name, "round_sheen", UiKit.WithAlpha(UiKit.Glass, 0.7f));
        back.pixelsPerUnitMultiplier = 1.25f; // corners half the height: a pill
        UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), at, new Vector2(width, 64));
        Image edge = UiKit.Image(back.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.55f));
        edge.pixelsPerUnitMultiplier = 1.25f;
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);
        float textLeft = 0;
        if (icon != null)
        {
            Image gem = UiKit.Image(back.transform, "Icon", icon, Color.white);
            UiKit.Gradient(gem, UiKit.Hex("E58CFF"), UiKit.Hex("8A3CFF"), true);
            gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0, 0.5f);
            gem.rectTransform.anchoredPosition = new Vector2(38, 0);
            gem.rectTransform.sizeDelta = new Vector2(44, 44);
            textLeft = 30;
        }
        TextMeshProUGUI text = UiKit.Label(like, back.transform, "Text", "", 30, UiKit.Text, TextAlignmentOptions.Center);
        text.richText = true;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(textLeft, 0);
        text.rectTransform.offsetMax = Vector2.zero;
        return text;
    }

    /// <summary>The numbers, and the Upgrade dot (when the menu shows).</summary>
    public void Refresh()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null || best == null)
            return;
        int high = stats.getHighScore();
        best.text = "<size=75%><color=" + UiKit.HexOf(UiKit.TextDim) + ">BEST</color></size>  " + high;
        best.transform.parent.gameObject.SetActive(high > 0);
        // On its own in the middle until there is a best score beside it.
        ((RectTransform)diamonds.transform.parent).anchoredPosition = new Vector2(high > 0 ? 160 : 0, 0);
        diamonds.text = Utility.getFormatedNumber(stats.getDiamondsCount());
        if (upgradeDot != null)
            upgradeDot.SetActive(canUpgrade(stats));
    }

    private static bool canUpgrade(PlayerStats stats)
    {
        int d = stats.getDiamondsCount();
        return (stats.getDoublePointsLevel() < Utility.Constants.DOUBLE_POINTS_MAX_LEVEL && d >= Utility.Constants.DOUBLE_POINTS_UPGRADE_PRICE)
            || (stats.getBoltLevel() < Utility.Constants.BOLT_MAX_LEVEL && d >= Utility.Constants.BOLT_UPGRADE_PRICE)
            || (stats.getChanceLevel() < Utility.Constants.CHANCE_MAX_LEVEL && d >= Utility.Constants.CHANCE_UPGRADE_PRICE);
    }
}
