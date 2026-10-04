using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Nebula look (UiKit) for the menus that have no skin of their own - the shop, purchase,
/// pause, game over, revive and the dialogs - by the same rules everywhere, over the scene's
/// objects (MenusController applies it once at start):
///   - a shade behind a full-screen menu, a darker veil behind a dialog;
///   - the big titles white into cyan;
///   - panels (the old grey rounded boxes) as dark glass with the lit edge;
///   - a button that was blue is a main action: a cyan-to-violet gradient that glows; any other
///     is glass; its words white;
///   - dividers as a thin cyan-to-violet line; a tick box or count chip as a dark pill.
/// The Upgrade, Settings and main menus and the run have their own skins (UpgradeMenuSkin,
/// SettingsMenuSkin, MainMenuSkin, PowerUpDock).
/// </summary>
public static class NebulaSkin
{
    private const string OLD_PANEL = "PickupCountBackground";

    public static void Apply(Transform menu, bool fullScreen)
    {
        if (menu == null || menu.Find("Shade") != null)
            return; // done already
        if (fullScreen)
        {
            Image shade = UiKit.Image(menu, "Shade", "shade", new Color(0.01f, 0.015f, 0.06f, 1f));
            UiKit.Stretch(shade.rectTransform);
            shade.transform.SetAsFirstSibling();
        }
        else
            UiKit.Rect("Shade", menu).gameObject.SetActive(false); // the marker only

        foreach (TMP_Text title in menu.GetComponentsInChildren<TMP_Text>(true))
            if (title.fontSize >= 64 && (title.name == "Title" || title.name.StartsWith("Title")))
                Heading(title);

        Transform owned = menu.Find("TitleContainer/DiamondsOwned");
        if (owned != null)
            DiamondChip(owned);
        if (menu.name == "PurchaseMenu")
            PurchaseCounters(menu);

        foreach (Image image in menu.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null)
            {
                if (image.name == "DisableTouch")
                    image.color = new Color(0.01f, 0.015f, 0.05f, 0.72f); // behind a dialog
                continue;
            }
            string sprite = image.sprite.name;
            if (image.name == "Divider")
                Divider(image);
            else if (sprite == OLD_PANEL)
                restyle(image);
        }
    }

    private static void restyle(Image image)
    {
        RectTransform r = image.rectTransform;
        Vector2 size = r.rect.size;
        Button button = image.GetComponent<Button>();
        Color was = image.color;
        if (button != null)
        {
            if (image.transform.parent != null && image.transform.parent.GetComponentInChildren<Slider>(true) != null)
                Outline(image); // over a slider (revive): the slider shows through
            else if ((was.b > was.r + 0.25f && was.a > 0.3f) || image.name.StartsWith("Buy"))
                Primary(image); // blue, or a price to pay
            else if (was.r > was.b + 0.3f && was.a > 0.5f)
                Featured(image); // the gold mystery box
            else
                Secondary(image);
            foreach (TMP_Text t in image.GetComponentsInChildren<TMP_Text>(true))
                Words(t, t.color.a);
            return;
        }
        if (was.a < 0.02f)
            return; // an invisible holder
        if (image.GetComponent<ScrollRect>() != null)
            return; // a list's own box (translucent black) stays as it is
        if (size.y < 70f && image.transform.parent != null && image.transform.parent.GetComponentInParent<Button>() != null)
        {
            UiKit.Style(image, "pill", UiKit.WithAlpha(Color.white, 0.2f)); // a label on a button
            image.pixelsPerUnitMultiplier = 0.7f;
            foreach (TMP_Text t in image.GetComponentsInChildren<TMP_Text>(true))
                Words(t, 1f);
            return;
        }
        if (size.y < 70f)
        {
            UiKit.Style(image, "pill", UiKit.GlassDeep); // a count chip
            image.raycastTarget = false;
            foreach (TMP_Text t in image.GetComponentsInChildren<TMP_Text>(true))
                Words(t, 1f);
            return;
        }
        UiKit.GlassPanel(image);
        image.raycastTarget = true; // a dialog's box still stops taps
        foreach (TMP_Text t in image.GetComponentsInChildren<TMP_Text>(true))
            if (t.GetComponentInParent<Button>() == null && !(t.name.StartsWith("Title") && t.fontSize >= 40))
                Words(t, t.color.a);
            else if (t.name.StartsWith("Title"))
                Heading(t);
    }

    /// <summary>
    /// The diamonds owned, top right (the shop, the Upgrade menu): the spinning gem as it was, its
    /// number on a glass pill with the lit edge.
    /// </summary>
    public static void DiamondChip(Transform owned)
    {
        AsTheBox(owned.Find("Mesh"));
        Transform count = owned.Find("Count");
        if (count != null)
            CountPill(count, UiKit.Text);
    }

    // The tilt the shop's mystery box button has its box at (a little from above): the other
    // icons over a count - the diamonds at the top, the Purchase menu's power-ups - the same.
    private const float BOX_TILT = 12f;

    public static void AsTheBox(Transform mesh)
    {
        if (mesh == null)
            return;
        // The box model faces the other way from the gem and the power-ups: the same view from
        // above is the opposite tilt for them.
        bool box = mesh.name.Contains("Box") || (mesh.parent != null && mesh.parent.name.Contains("Box"));
        mesh.localRotation = Quaternion.Euler(box ? BOX_TILT : -BOX_TILT, mesh.localEulerAngles.y, 0f);
    }

    /// <summary>A count's background (the pill a number sits on, under a 3D icon): glass with a
    /// cyan-to-violet edge and room round the number.</summary>
    public static void CountPill(Transform count, Color textColour)
    {
        Image pill = count.GetComponent<Image>();
        if (pill == null || count.Find("Edge") != null)
            return;
        const float HEIGHT = 44f;
        UiKit.Style(pill, "round_sheen", UiKit.WithAlpha(UiKit.Hex("0E1640"), 0.92f));
        pill.pixelsPerUnitMultiplier = 36f / (HEIGHT / 2f); // corners half the height: a pill
        // Its width still follows the number; its height is its own.
        HorizontalOrVerticalLayoutGroup layout = count.GetComponent<HorizontalOrVerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(20, 20, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
        }
        ContentSizeFitter fit = count.GetComponent<ContentSizeFitter>();
        if (fit != null)
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        RectTransform r = (RectTransform)count;
        r.sizeDelta = new Vector2(r.sizeDelta.x, HEIGHT);
        Image edge = UiKit.Image(count, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.6f));
        edge.pixelsPerUnitMultiplier = pill.pixelsPerUnitMultiplier;
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);
        edge.transform.SetAsFirstSibling();
        foreach (TMP_Text text in count.GetComponentsInChildren<TMP_Text>(true))
        {
            text.colorGradientPreset = null;
            text.enableVertexGradient = false;
            text.color = textColour;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, HEIGHT);
        }
    }

    // The Purchase menu's row of what the player has - diamonds, double points, bolts, chances,
    // boxes: on one glass strip, clear of the Back button, each number on its pill in its item's
    // colour. A tap on one scrolls the list to where that item is sold (PurchaseJump).
    private static readonly string[] COUNTERS = { "DiamondsOwned", "DoublePointsOwned", "BoltsOwned", "ChancesOwned", "BoxesOwned" };
    private static readonly string[] SECTIONS = { "Diamonds", "Double Points", "Bolts", "Chances", "Boxes" };
    private static readonly string[] COUNTER_COLOURS = { "E58CFF", "FFD45A", "6FC3FF", "FF7A8A", "FFB347" };
    private const float STRIP_Y = -312f;  // its centre; the Back button ends at -196

    public static void PurchaseCounters(Transform menu)
    {
        RectTransform first = menu.Find(COUNTERS[0]) as RectTransform;
        if (first == null || menu.Find("Counters") != null)
            return;
        Image strip = UiKit.Image(menu, "Counters", "round_sheen", UiKit.WithAlpha(UiKit.Glass, 0.7f));
        RectTransform sr = strip.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.anchoredPosition = new Vector2(0, STRIP_Y);
        sr.sizeDelta = new Vector2(1000, 176);
        strip.transform.SetSiblingIndex(first.GetSiblingIndex());
        Image edge = UiKit.Image(strip.transform, "Edge", "round_outline", UiKit.WithAlpha(Color.white, 0.6f));
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);
        for (int i = 1; i < COUNTERS.Length; i++)
        {
            Image line = UiKit.Image(strip.transform, "Divider" + i, "round_fill", UiKit.WithAlpha(Color.white, 0.14f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            line.rectTransform.anchoredPosition = new Vector2(-500 + 200 * i, 0);
            line.rectTransform.sizeDelta = new Vector2(2, 116);
            line.type = Image.Type.Simple;
        }
        // The list starts under the strip (it ran up behind it), its foot where it was.
        RectTransform list = menu.Find("3DScrollView") as RectTransform;
        if (list != null)
        {
            Vector3[] c = new Vector3[4];
            list.GetWorldCorners(c);
            float listTop = menu.InverseTransformPoint(c[1]).y;
            sr.GetWorldCorners(c);
            float stripFoot = menu.InverseTransformPoint(c[0]).y - 14f;
            float over = listTop - stripFoot;
            if (over > 0f)
            {
                list.sizeDelta -= new Vector2(0f, over);
                list.anchoredPosition -= new Vector2(0f, over * (1f - list.pivot.y));
            }
        }
        PurchaseJump jump = menu.gameObject.AddComponent<PurchaseJump>();
        jump.below = sr;
        float shift = STRIP_Y - -287f; // the counters move down with the strip
        for (int i = 0; i < COUNTERS.Length; i++)
        {
            Transform c = menu.Find(COUNTERS[i]);
            if (c == null)
                continue;
            RectTransform cr = (RectTransform)c;
            cr.anchoredPosition = new Vector2(-400 + 200 * i, cr.anchoredPosition.y + shift);
            AsTheBox(c.Find("Mesh"));
            cr.sizeDelta = new Vector2(196, cr.sizeDelta.y);
            // The numbers in each item's colour, straight on the strip (no pill of their own).
            Transform count = c.Find("Count");
            if (count != null)
            {
                Image pill = count.GetComponent<Image>();
                if (pill != null) { pill.sprite = null; pill.enabled = false; }
                foreach (TMP_Text text in count.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.colorGradientPreset = null;
                    text.enableVertexGradient = false;
                    text.color = UiKit.Hex(COUNTER_COLOURS[i]);
                }
            }
            // Its slot of the strip is a button to its item in the list (the counter's own
            // invisible image would catch the tap first: it lets it through).
            Image own = c.GetComponent<Image>();
            if (own != null)
                own.raycastTarget = false;
            Image slot = UiKit.Image(strip.transform, "Go" + SECTIONS[i].Replace(" ", ""), "round_fill", Color.clear);
            slot.raycastTarget = true;
            UiKit.Place(slot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-400 + 200 * i, 0), new Vector2(196, 176));
            string section = SECTIONS[i];
            Button b = slot.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => jump.To(section));
        }
    }

    public static void Heading(TMP_Text title)
    {
        title.colorGradientPreset = null;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Color.white, Color.white, UiKit.Cyan, UiKit.Cyan);
        title.color = Color.white;
    }

    public static void Words(TMP_Text text, float alpha)
    {
        text.colorGradientPreset = null;
        text.enableVertexGradient = false;
        text.color = UiKit.WithAlpha(UiKit.Text, Mathf.Max(alpha, 0.85f));
    }

    public static void Divider(Image line)
    {
        line.sprite = null;
        line.color = UiKit.WithAlpha(Color.white, 0.55f);
        UiKit.Gradient(line, UiKit.Cyan, UiKit.Violet, false);
        RectTransform r = line.rectTransform;
        if (r.sizeDelta.y > 3f)
            r.sizeDelta = new Vector2(r.sizeDelta.x, 3f);
    }

    /// <summary>A main action: gradient, glowing.</summary>
    public static void Primary(Image image)
    {
        UiKit.Style(image, "round_sheen", Color.white);
        image.raycastTarget = true;
        corners(image);
        UiKit.Gradient(image, UiKit.Cyan, UiKit.Violet, true);
        glow(image, UiKit.WithAlpha(UiKit.Violet, 0.45f));
    }

    /// <summary>A gold button that stands out (the mystery box): gold gradient, glowing.</summary>
    public static void Featured(Image image)
    {
        UiKit.Style(image, "round_sheen", Color.white);
        image.raycastTarget = true;
        corners(image);
        UiKit.Gradient(image, UiKit.Gold, UiKit.Amber, true);
        glow(image, UiKit.WithAlpha(UiKit.Gold, 0.5f));
    }

    /// <summary>Any other button: glass.</summary>
    public static void Secondary(Image image)
    {
        UiKit.GlassPanel(image, 0.7f);
        corners(image);
        Transform edge = image.transform.Find("Edge");
        if (edge != null)
            edge.GetComponent<Image>().pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
    }

    /// <summary>Only the lit edge (a button laid over something that must show).</summary>
    public static void Outline(Image image)
    {
        image.color = Color.clear;
        if (image.transform.Find("Edge") != null)
            return;
        Image edge = UiKit.Image(image.transform, "Edge", "round_outline", Color.white);
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);
        edge.pixelsPerUnitMultiplier = 0.9f;
        edge.transform.SetAsFirstSibling();
    }

    /// <summary>A tab: lit when chosen, glass when not (the shop's Balls and Floors).</summary>
    public static void Tab(Image image, bool chosen)
    {
        if (image.GetComponent<UiGradient>() == null)
        {
            UiKit.Style(image, "round_sheen", Color.white);
            image.raycastTarget = true;
            corners(image);
        }
        image.color = Color.white;
        if (chosen)
            UiKit.Gradient(image, UiKit.Cyan, UiKit.Violet, true);
        else
            UiKit.Gradient(image, UiKit.Off, UiKit.GlassDeep, true);
        Transform halo = image.transform.Find("Glow");
        if (halo == null && chosen)
            halo = glow(image, UiKit.WithAlpha(UiKit.Violet, 0.45f)).transform;
        if (halo != null)
            halo.gameObject.SetActive(chosen);
    }

    // The glow round a button, outside its edge only. The glow sprite's shape sits 32 px in from
    // its border; scaled with the button's corners (pixelsPerUnitMultiplier) and set out by as
    // much, its shape is the button's own. (Set out by a fixed amount instead, the shape fell
    // inside the button on the small ones and drew a second rounded box in it.)
    private static Image glow(Image button, Color colour)
    {
        Transform old = button.transform.Find("Glow");
        Image g = old != null ? old.GetComponent<Image>() : UiKit.Image(button.transform, "Glow", "round_glow", colour);
        float m = button.pixelsPerUnitMultiplier;
        g.pixelsPerUnitMultiplier = m;
        g.color = colour;
        UiKit.Stretch(g.rectTransform, 32f / m);
        g.transform.SetAsFirstSibling();
        return g;
    }

    // Corners in proportion to the button: about a quarter of its height.
    private static void corners(Image image)
    {
        float h = image.rectTransform.rect.height;
        if (h > 1f)
            image.pixelsPerUnitMultiplier = Mathf.Clamp(36f / (h * 0.28f), 0.5f, 3f);
    }
}
