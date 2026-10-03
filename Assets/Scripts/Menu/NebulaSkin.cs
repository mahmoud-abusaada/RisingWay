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
        {
            image.color = Color.clear; // a frame round a list: the list is enough
            return;
        }
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
