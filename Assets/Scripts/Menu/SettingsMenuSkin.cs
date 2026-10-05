using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Settings menu in the Nebula look (UiKit), made over its scene objects when it first wakes:
/// glass sections with a lit edge, gradient sliders with a glowing knob, switches in place of the
/// tick boxes, and the three ways to play as cards, the chosen one lit. SettingsMenu keeps its
/// logic; it tells the skin which way to play is chosen (ShowMode).
/// </summary>
public class SettingsMenuSkin : MonoBehaviour
{
    public static readonly Color ModeOn = UiKit.WithAlpha(UiKit.Hex("2F7FB8"), 0.55f);
    public static readonly Color ModeOff = UiKit.WithAlpha(UiKit.GlassDeep, 0.7f);
    private readonly Dictionary<Image, Image> modeEdges = new Dictionary<Image, Image>();

    public void Build(TMP_Text title)
    {
        Image shade = UiKit.Image(transform, "Shade", "shade", new Color(0.01f, 0.015f, 0.06f, 1f));
        UiKit.Stretch(shade.rectTransform);
        shade.transform.SetAsFirstSibling();

        title.colorGradientPreset = null;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Color.white, Color.white, UiKit.Cyan, UiKit.Cyan);
        title.color = Color.white;

        Transform back = transform.Find("BackButton");
        if (back != null && back.GetComponent<Image>() != null)
        {
            UiKit.GlassPanel(back.GetComponent<Image>(), 0.7f);
            back.GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
        }

        Transform scroll = transform.Find("ScrollView");
        if (scroll != null && scroll.GetComponent<Image>() != null)
            scroll.GetComponent<Image>().color = Color.clear; // the sections are the panels now
        // The same spacing as Purchase and the Shop: the list's gap under the header, its sections
        // as wide as Purchase's groups (MenuLayout).
        if (scroll != null && scroll.GetComponent<ScrollRect>() != null)
        {
            ScrollRect list = scroll.GetComponent<ScrollRect>();
            MenuLayout.ListBelow((RectTransform)scroll, MenuLayout.GROUP_WIDTH, back as RectTransform, title.rectTransform);
            MenuLayout.NoBox(list);
            if (list.content != null && list.content.GetComponent<ListGroupsFit>() == null)
                list.content.gameObject.AddComponent<ListGroupsFit>();
        }

        Transform content = scroll != null ? scroll.Find("Viewport/Content") : null;
        if (content == null)
            return;
        foreach (Transform section in content)
        {
            Image panel = section.GetComponent<Image>();
            if (panel == null || section.GetComponent<Selectable>() != null)
                continue;
            UiKit.GlassPanel(panel);
            panel.raycastTarget = false;
            Transform heading = section.Find("Title");
            if (heading != null && heading.GetComponent<TMP_Text>() != null)
            {
                TMP_Text h = heading.GetComponent<TMP_Text>();
                h.colorGradientPreset = null;
                h.enableVertexGradient = true;
                h.colorGradient = new VertexGradient(Color.white, Color.white, UiKit.Cyan, UiKit.Cyan);
                h.color = Color.white;
            }
            Transform divider = section.Find("Divider");
            if (divider != null && divider.GetComponent<Image>() != null)
            {
                Image line = divider.GetComponent<Image>();
                line.sprite = null;
                line.color = UiKit.WithAlpha(Color.white, 0.55f);
                UiKit.Gradient(line, UiKit.Cyan, UiKit.Violet, false);
                ((RectTransform)divider).sizeDelta = new Vector2(((RectTransform)divider).sizeDelta.x, 3);
            }
        }

        // Everything else written in the sections: white (the headings were done above).
        foreach (TMP_Text text in content.GetComponentsInChildren<TMP_Text>(true))
            if (!(text.name == "Title" && text.transform.parent.parent == content))
            {
                text.colorGradientPreset = null;
                text.enableVertexGradient = false;
                text.color = UiKit.Text;
            }

        foreach (Slider slider in content.GetComponentsInChildren<Slider>(true))
            styleSlider(slider);
        foreach (Toggle toggle in content.GetComponentsInChildren<Toggle>(true))
            toggle.gameObject.AddComponent<NebulaSwitch>().Build(toggle);
        foreach (Button button in content.GetComponentsInChildren<Button>(true))
        {
            Image image = button.GetComponent<Image>();
            if (image == null)
                continue;
            if (button.GetComponentInChildren<Animation>(true) != null)
                styleMode(image);
            else
            {
                UiKit.GlassPanel(image, 0.75f);
                image.pixelsPerUnitMultiplier = 1.2f;
            }
        }
    }

    private static void styleSlider(Slider slider)
    {
        Transform background = slider.transform.Find("Background");
        Image track = background != null ? background.GetComponent<Image>() : null;
        if (track != null)
        {
            UiKit.Style(track, "pill", UiKit.Off);
            track.pixelsPerUnitMultiplier = 1.1f;
            ((RectTransform)track.transform).sizeDelta = new Vector2(((RectTransform)track.transform).sizeDelta.x, -16);
        }
        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            UiKit.Style(fill, "pill", Color.white);
            fill.pixelsPerUnitMultiplier = 1.1f;
            UiKit.Gradient(fill, UiKit.Cyan, UiKit.Violet, false);
            RectTransform area = (RectTransform)fill.transform.parent;
            area.anchorMin = new Vector2(area.anchorMin.x, 0.35f);
            area.anchorMax = new Vector2(area.anchorMax.x, 0.65f);
        }
        Image knob = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
        if (knob != null)
        {
            UiKit.Style(knob, "circle", Color.white);
            knob.raycastTarget = true;
            // The slider stretches the knob over its area's height: the area is made the knob's.
            RectTransform area = (RectTransform)knob.transform.parent;
            area.anchorMin = new Vector2(area.anchorMin.x, 0.5f);
            area.anchorMax = new Vector2(area.anchorMax.x, 0.5f);
            area.sizeDelta = new Vector2(area.sizeDelta.x, 56);
            knob.rectTransform.sizeDelta = new Vector2(56, 0);
            Image glow = UiKit.Image(knob.transform, "Glow", "glow", UiKit.WithAlpha(UiKit.Cyan, 0.7f));
            UiKit.Stretch(glow.rectTransform, 22);
            glow.transform.SetAsFirstSibling();
            Image ring = UiKit.Image(knob.transform, "Ring", "ring", UiKit.WithAlpha(UiKit.Violet, 0.9f));
            UiKit.Stretch(ring.rectTransform, -6);
        }
        slider.transition = Selectable.Transition.None;
    }

    private void styleMode(Image card)
    {
        UiKit.Style(card, "round_sheen", ModeOff);
        card.raycastTarget = true;
        Image edge = UiKit.Image(card.transform, "Edge", "round_outline", Color.white);
        UiKit.Gradient(edge, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(edge.rectTransform);
        edge.transform.SetAsFirstSibling();
        modeEdges[card] = edge;
    }

    /// <summary>The way to play that is chosen: its card lit, the others dark.</summary>
    public void ShowMode(Image chosen)
    {
        foreach (KeyValuePair<Image, Image> m in modeEdges)
        {
            bool on = m.Key == chosen;
            m.Key.color = on ? ModeOn : ModeOff;
            m.Value.color = UiKit.WithAlpha(Color.white, on ? 1f : 0.18f);
        }
    }
}

/// <summary>A Toggle drawn as a switch: a track that lights up and a knob that slides across.
/// The Toggle still decides (its tick, which it fades, is hidden).</summary>
public class NebulaSwitch : MonoBehaviour
{
    private const float WIDTH = 104f, HEIGHT = 56f;
    private Toggle toggle;
    private Image lit;
    private RectTransform knob;
    private float shown;

    public void Build(Toggle t)
    {
        toggle = t;
        Transform box = t.transform.Find("Background");
        if (box == null)
            return;
        RectTransform r = (RectTransform)box;
        r.localScale = Vector3.one;
        r.sizeDelta = new Vector2(WIDTH, HEIGHT);
        r.anchoredPosition = new Vector2(WIDTH / 2f + 4f, r.anchoredPosition.y);
        Image track = box.GetComponent<Image>();
        UiKit.Style(track, "pill", UiKit.Off);
        track.raycastTarget = true;
        track.pixelsPerUnitMultiplier = 0.5f; // round ends
        if (t.graphic != null)
            t.graphic.enabled = false;
        t.transition = Selectable.Transition.None;

        lit = UiKit.Image(box, "On", "pill", Color.white);
        lit.pixelsPerUnitMultiplier = 0.5f;
        UiKit.Gradient(lit, UiKit.Cyan, UiKit.Violet, false);
        UiKit.Stretch(lit.rectTransform);
        Image k = UiKit.Image(box, "Knob", "circle", Color.white);
        knob = k.rectTransform;
        knob.anchorMin = knob.anchorMax = new Vector2(0, 0.5f);
        knob.sizeDelta = new Vector2(HEIGHT - 10, HEIGHT - 10);

        Transform label = t.transform.Find("Label");
        if (label != null)
        {
            RectTransform l = (RectTransform)label;
            l.anchorMin = l.anchorMax = new Vector2(0, 0.5f);
            l.pivot = new Vector2(0, 0.5f);
            l.anchoredPosition = new Vector2(WIDTH + 28f, 0);
            TMP_Text text = label.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                l.sizeDelta = new Vector2(Mathf.Max(l.sizeDelta.x, 400f), l.sizeDelta.y);
            }
        }
        shown = t.isOn ? 1f : 0f;
        Update();
    }

    void OnEnable()
    {
        if (toggle != null)
            shown = toggle.isOn ? 1f : 0f; // no slide when the menu opens
    }

    void Update()
    {
        if (toggle == null || lit == null)
            return;
        shown = Mathf.MoveTowards(shown, toggle.isOn ? 1f : 0f, Time.unscaledDeltaTime * 7f);
        float k = shown * shown * (3f - 2f * shown);
        lit.color = UiKit.WithAlpha(Color.white, k);
        float travel = WIDTH - HEIGHT;
        knob.anchoredPosition = new Vector2(HEIGHT / 2f + travel * k, 0);
    }
}
