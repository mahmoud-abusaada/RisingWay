using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "Nebula" look for the menus: dark glass panels with a thin cyan-to-violet edge, gradient
/// buttons that glow, white type. Tried first on the Upgrade menu (UpgradeMenuSkin) and on the
/// power-up buttons in a run (PowerUpDock).
///
/// The sprites are plain white shapes in Resources/UI/Nebula (made by Tools/UI/make_nebula_ui.py),
/// coloured here - by the Image's colour and a UiGradient - so the palette lives in one place.
/// </summary>
public static class UiKit
{
    public static readonly Color Glass = new Color(0.05f, 0.075f, 0.19f, 0.84f);
    public static readonly Color GlassDeep = new Color(0.025f, 0.04f, 0.11f, 0.9f);
    public static readonly Color Cyan = Hex("3FE0FF");
    public static readonly Color Violet = Hex("8C6CFF");
    public static readonly Color Text = Hex("EEF7FF");
    public static readonly Color TextDim = Hex("9DB2DA");
    public static readonly Color Gold = Hex("FFD45A");
    public static readonly Color Amber = Hex("FF9A3D");
    public static readonly Color Off = Hex("3A4468");
    public static readonly Color Warn = Hex("FF8FA3");

    public static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    public static string HexOf(Color c)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Sprite Sprite(string name)
    {
        Sprite s;
        if (!sprites.TryGetValue(name, out s) || s == null)
        {
            s = Resources.Load<Sprite>("UI/Nebula/" + name);
            if (s == null)
                Debug.LogWarning("UiKit: no sprite UI/Nebula/" + name);
            sprites[name] = s;
        }
        return s;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        // Decoration is never laid out with the content (in a box that arranges its children, an
        // edge would take a place of its own: an empty chip over the mystery box's prize count).
        if (parent.GetComponent<LayoutGroup>() != null)
            go.AddComponent<LayoutElement>().ignoreLayout = true;
        return r;
    }

    /// <summary>A picture that is only decoration (it takes no taps).</summary>
    public static Image Image(Transform parent, string name, string sprite, Color color)
    {
        Image image = Rect(name, parent).gameObject.AddComponent<Image>();
        Style(image, sprite, color);
        return image;
    }

    public static void Style(Image image, string sprite, Color color)
    {
        image.sprite = Sprite(sprite);
        image.color = color;
        image.raycastTarget = false;
        image.type = image.sprite != null && image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
    }

    public static UiGradient Gradient(Graphic g, Color from, Color to, bool vertical)
    {
        UiGradient grad = g.GetComponent<UiGradient>();
        if (grad == null)
            grad = g.gameObject.AddComponent<UiGradient>();
        grad.Set(from, to, vertical);
        return grad;
    }

    public static void Stretch(RectTransform r, float outset = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(-outset, -outset);
        r.offsetMax = new Vector2(outset, outset);
    }

    public static void Place(RectTransform r, Vector2 anchor, Vector2 position, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
    }

    /// <summary>A text like <paramref name="like"/> (its font and outline), without its colours
    /// and animations.</summary>
    public static TextMeshProUGUI Label(TMP_Text like, Transform parent, string name, string text, float size,
                                        Color color, TextAlignmentOptions align)
    {
        GameObject go = Object.Instantiate(like.gameObject, parent, false);
        go.name = name;
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(go.transform.GetChild(i).gameObject);
        foreach (Component c in go.GetComponents<Component>())
            if (c is Animation || c is Animator || c is Selectable || c is LayoutElement)
                Object.Destroy(c);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        Restyle(label, size, color, align);
        label.text = text;
        return label;
    }

    public static void Restyle(TMP_Text label, float size, Color color, TextAlignmentOptions align)
    {
        label.enableAutoSizing = false;
        label.fontSize = size;
        label.colorGradientPreset = null;
        label.enableVertexGradient = false;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    /// <summary>Dark glass with a cyan-to-violet edge, on <paramref name="panel"/>'s own Image.</summary>
    public static void GlassPanel(Image panel, float edgeAlpha = 0.8f)
    {
        Style(panel, "round_sheen", Glass);
        panel.raycastTarget = true;
        Transform old = panel.transform.Find("Edge");
        if (old != null)
            return;
        Image edge = Image(panel.transform, "Edge", "round_outline", WithAlpha(Color.white, edgeAlpha));
        Gradient(edge, Cyan, Violet, false);
        Stretch(edge.rectTransform);
        edge.transform.SetAsFirstSibling();
    }
}
