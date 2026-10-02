using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Upgrade menu in the Nebula look (UiKit), made over its scene objects when it first wakes.
/// Each power-up is a glass card: its icon in a glowing orb, the name and what it does, its level
/// as lit segments, what the next level gives, and a glowing price button - or a gold MAX badge.
/// UpgradeMenu keeps its logic and tells the skin what to show (SetState).
/// </summary>
public class UpgradeMenuSkin : MonoBehaviour
{
    public class Card
    {
        public RectTransform root;
        public Slider slider;
        public int maxLevel;
        public Color accent;
        public TextMeshProUGUI level;
        public TextMeshProUGUI effect;
        public RectTransform button;
        public Image buttonImage;
        public UiGradient buttonGradient;
        public Image buttonGlow;
        public TextMeshProUGUI price;
        public TextMeshProUGUI buttonLabel;
        public RectTransform max;
        public readonly List<Image> lit = new List<Image>();
        public Image orbGlow;
        public Transform mesh;
        public float shown;          // a level lit ahead of the slider, by Celebrate
    }

    private const float CARD_HEIGHT = 300f;
    private const float TEXT_X = -10f;      // the middle of the text column, from the card's middle
    private const float TEXT_WIDTH = 500f;
    private const float ORB_X = -378f;
    private static readonly Vector2 ICON_NUDGE = new Vector2(16f, 38f); // the 3D icons sat low and left
    private readonly List<Card> cards = new List<Card>();

    /// <summary>The menu's header: a shade behind it all, the title and a line under it, the
    /// diamonds; and a tip at the foot.</summary>
    public void BuildHeader(TMP_Text title, RectTransform diamonds)
    {
        Image shade = UiKit.Image(transform, "Shade", "shade", new Color(0.01f, 0.015f, 0.06f, 1f));
        UiKit.Stretch(shade.rectTransform);
        shade.transform.SetAsFirstSibling();

        title.colorGradientPreset = null;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Color.white, Color.white, UiKit.Cyan, UiKit.Cyan);
        title.color = Color.white;
        TextMeshProUGUI line = UiKit.Label(title, title.transform.parent, "Subtitle", "Make your power-ups stronger",
                                           30, UiKit.TextDim, TextAlignmentOptions.Center);
        line.rectTransform.anchoredPosition = title.rectTransform.anchoredPosition + new Vector2(0, -84);
        line.rectTransform.sizeDelta = new Vector2(900, 44);

        Transform count = diamonds != null ? diamonds.Find("Count") : null;
        if (count != null && count.GetComponent<Image>() != null)
            UiKit.Style(count.GetComponent<Image>(), "pill", UiKit.GlassDeep);

        // Where the power-ups are used: nothing in a run said so.
        TextMeshProUGUI tip = UiKit.Label(title, transform, "Tip",
            "In a run, your power-ups wait on the left.\nTap one to use it.", 28, UiKit.TextDim, TextAlignmentOptions.Center);
        tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 0);
        tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tip.rectTransform.anchoredPosition = new Vector2(0, 230);
        tip.rectTransform.sizeDelta = new Vector2(900, 100);
        tip.lineSpacing = 12;

        foreach (Button b in GetComponentsInChildren<Button>(true))
            if (b.name.ToLower().Contains("back") && b.GetComponent<Image>() != null)
            {
                UiKit.GlassPanel(b.GetComponent<Image>(), 0.7f);
                b.GetComponent<Image>().pixelsPerUnitMultiplier = 1.4f;
            }
    }

    /// <summary>One power-up's card, made over the scene's: <paramref name="slider"/> still holds
    /// the level (UpgradeMenu animates it), shown as segments.</summary>
    public Card BuildCard(RectTransform root, float y, Color accent, string description, Slider slider, int maxLevel,
                          TextMeshProUGUI level, TextMeshProUGUI effect, RectTransform button)
    {
        Card card = new Card { root = root, slider = slider, maxLevel = maxLevel, accent = accent, level = level, effect = effect, button = button };
        root.anchoredPosition = new Vector2(root.anchoredPosition.x, y);
        root.sizeDelta = new Vector2(root.sizeDelta.x, CARD_HEIGHT);
        UiKit.GlassPanel(root.GetComponent<Image>());

        Transform divider = root.Find("Divider");
        if (divider != null)
            divider.gameObject.SetActive(false);

        // The icon's orb.
        int under = root.Find("Edge").GetSiblingIndex() + 1;
        Image glow = UiKit.Image(root, "IconGlow", "glow", UiKit.WithAlpha(accent, 0.42f));
        UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ORB_X, 0), new Vector2(280, 280));
        Image disc = UiKit.Image(root, "IconDisc", "circle", UiKit.GlassDeep);
        UiKit.Place(disc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ORB_X, 0), new Vector2(178, 178));
        Image ring = UiKit.Image(root, "IconRing", "ring", UiKit.WithAlpha(accent, 0.85f));
        UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ORB_X, 0), new Vector2(178, 178));
        glow.transform.SetSiblingIndex(under);
        disc.transform.SetSiblingIndex(under + 1);
        ring.transform.SetSiblingIndex(under + 2);
        Transform mesh = root.Find("Mesh");
        if (mesh != null)
            ((RectTransform)mesh).anchoredPosition += ICON_NUDGE;
        card.orbGlow = glow;
        card.mesh = mesh;

        // Name, what it does, level.
        TextMeshProUGUI name = root.Find("Title").GetComponent<TextMeshProUGUI>();
        UiKit.Restyle(name, 42, UiKit.Text, TextAlignmentOptions.MidlineLeft);
        UiKit.Place(name.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TEXT_X, 96), new Vector2(TEXT_WIDTH, 56));
        TextMeshProUGUI what = UiKit.Label(name, root, "Description", description, 25, UiKit.TextDim, TextAlignmentOptions.MidlineLeft);
        UiKit.Place(what.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TEXT_X, 52), new Vector2(TEXT_WIDTH, 36));
        what.enableAutoSizing = true; // never into the button
        what.fontSizeMin = 18;
        what.fontSizeMax = 25;

        level.transform.SetParent(root, false);
        UiKit.Restyle(level, 26, accent, TextAlignmentOptions.MidlineRight);
        UiKit.Place(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TEXT_X, 96), new Vector2(TEXT_WIDTH, 56));

        // The level, as segments: the slider stays (UpgradeMenu fills it), its picture goes.
        foreach (Transform t in slider.transform)
            t.gameObject.SetActive(false);
        RectTransform bar = UiKit.Rect("Levels", root);
        UiKit.Place(bar, new Vector2(0.5f, 0.5f), new Vector2(TEXT_X, -2), new Vector2(TEXT_WIDTH, 24));
        const float gap = 10f;
        float w = (TEXT_WIDTH - gap * (maxLevel - 1)) / maxLevel;
        for (int i = 0; i < maxLevel; i++)
        {
            Image off = UiKit.Image(bar, "Level" + (i + 1), "pill", UiKit.Off);
            off.pixelsPerUnitMultiplier = 1.15f;
            UiKit.Place(off.rectTransform, new Vector2(0, 0.5f), new Vector2(w / 2 + i * (w + gap), 0), new Vector2(w, 22));
            Image on = UiKit.Image(off.transform, "On", "pill", UiKit.WithAlpha(Color.white, 0));
            on.pixelsPerUnitMultiplier = 1.15f;
            UiKit.Gradient(on, UiKit.Cyan, UiKit.Violet, false);
            UiKit.Stretch(on.rectTransform);
            card.lit.Add(on);
        }

        effect.transform.SetParent(root, false);
        UiKit.Restyle(effect, 30, UiKit.Text, TextAlignmentOptions.MidlineLeft);
        effect.richText = true;
        UiKit.Place(effect.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TEXT_X, -54), new Vector2(TEXT_WIDTH, 44));

        // The price button, glowing, and the MAX badge in its place once there is nothing left.
        Vector2 buttonAt = new Vector2(372, 0), buttonSize = new Vector2(214, 248);
        card.buttonGlow = UiKit.Image(root, "ButtonGlow", "round_glow", UiKit.WithAlpha(UiKit.Violet, 0.55f));
        UiKit.Place(card.buttonGlow.rectTransform, new Vector2(0.5f, 0.5f), buttonAt, buttonSize + new Vector2(64, 64));
        card.buttonGlow.transform.SetSiblingIndex(button.GetSiblingIndex());
        UiKit.Place(button, new Vector2(0.5f, 0.5f), buttonAt, buttonSize);
        card.buttonImage = button.GetComponent<Image>();
        UiKit.Style(card.buttonImage, "round_sheen", Color.white);
        card.buttonImage.raycastTarget = true;
        card.buttonGradient = UiKit.Gradient(card.buttonImage, UiKit.Cyan, UiKit.Violet, true);
        Transform price = button.Find("Price");
        if (price != null)
        {
            card.price = price.GetComponent<TextMeshProUGUI>();
            UiKit.Restyle(card.price, 40, Color.white, TextAlignmentOptions.Center);
            RectTransform p = card.price.rectTransform;
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0);
            p.anchoredPosition = new Vector2(0, 96);
            p.sizeDelta = new Vector2(200, 50);
        }
        Transform now = button.Find("UpgradeNow");
        if (now != null)
        {
            UiKit.Style(now.GetComponent<Image>(), "pill", UiKit.WithAlpha(Color.white, 0.2f));
            now.GetComponent<Image>().pixelsPerUnitMultiplier = 0.7f;
            RectTransform n = (RectTransform)now;
            n.anchorMin = n.anchorMax = new Vector2(0.5f, 0);
            n.anchoredPosition = new Vector2(0, 42);
            n.sizeDelta = new Vector2(172, 44);
            card.buttonLabel = now.GetComponentInChildren<TextMeshProUGUI>(true);
            if (card.buttonLabel != null)
            {
                UiKit.Restyle(card.buttonLabel, 23, Color.white, TextAlignmentOptions.Center);
                UiKit.Stretch(card.buttonLabel.rectTransform);
                card.buttonLabel.text = "UPGRADE";
            }
        }

        card.max = UiKit.Rect("Max", root);
        UiKit.Place(card.max, new Vector2(0.5f, 0.5f), buttonAt, buttonSize);
        Image maxGlow = UiKit.Image(card.max, "Glow", "round_glow", UiKit.WithAlpha(UiKit.Gold, 0.45f));
        UiKit.Stretch(maxGlow.rectTransform, 32);
        Image badge = UiKit.Image(card.max, "Badge", "round_sheen", Color.white);
        UiKit.Gradient(badge, UiKit.Gold, UiKit.Amber, true);
        UiKit.Stretch(badge.rectTransform);
        TextMeshProUGUI maxText = UiKit.Label(name, card.max, "Text", "MAX", 58, UiKit.GlassDeep, TextAlignmentOptions.Center);
        UiKit.Place(maxText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(200, 70));
        TextMeshProUGUI maxLine = UiKit.Label(name, card.max, "Line", "LEVEL", 22, UiKit.WithAlpha(UiKit.GlassDeep, 0.8f), TextAlignmentOptions.Center);
        UiKit.Place(maxLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(200, 30));
        card.max.gameObject.SetActive(false);

        cards.Add(card);
        return card;
    }

    /// <summary>What a card shows: maxed out, or the button, bright if it can be paid for.</summary>
    public void SetState(Card card, bool maxed, bool affordable)
    {
        card.button.gameObject.SetActive(!maxed);
        card.buttonGlow.gameObject.SetActive(!maxed && affordable);
        card.max.gameObject.SetActive(maxed);
        if (maxed)
            return;
        card.buttonGradient.Set(affordable ? UiKit.Cyan : UiKit.Off, affordable ? UiKit.Violet : UiKit.Off * 0.8f, true);
        if (card.price != null)
            card.price.color = affordable ? Color.white : UiKit.Warn;
        if (card.buttonLabel != null)
            card.buttonLabel.alpha = affordable ? 1f : 0.6f;
    }

    void OnEnable()
    {
        foreach (Card card in cards)
            card.shown = 0; // the menu fills the levels in again as it opens
    }

    void Update()
    {
        // The segments follow the slider as UpgradeMenu fills it, the one being reached fading in.
        foreach (Card card in cards)
        {
            float value = Mathf.Max(card.slider.value, card.shown);
            for (int i = 0; i < card.lit.Count; i++)
                card.lit[i].color = UiKit.WithAlpha(Color.white, Mathf.Clamp01(value - i));
        }
    }

    // ---------------------------------------------------------------------------------------
    // The upgrade, celebrated (about a second, all in real time so a paused game does not hold it):
    // the button squeezes and springs back, or the MAX badge pops in; a light sweeps across the
    // card; the icon spins in its orb, which flares and sends out rings and sparks; the new level's
    // segment pops; the level label jumps; and the price floats up off the button.
    // ---------------------------------------------------------------------------------------

    public void Celebrate(Card card, int newLevel, int price)
    {
        StartCoroutine(sweep(card));
        StartCoroutine(fillTo(card, newLevel));
        if (card.max.gameObject.activeSelf)
            StartCoroutine(pop(card.max, 0f, 0.45f));
        else
            StartCoroutine(squeeze(card.button));
        if (card.mesh != null)
            StartCoroutine(spin(card.mesh));
        Vector2 orb = new Vector2(ORB_X, 0);
        StartCoroutine(flare(card.orbGlow, card.accent));
        StartCoroutine(ring(card, orb, 0f));
        StartCoroutine(ring(card, orb, 0.14f));
        for (int i = 0; i < 16; i++)
            StartCoroutine(spark(card, orb, i));
        StartCoroutine(pop(card.level.rectTransform, 1.5f, 0.4f));
        StartCoroutine(floatUp(card, "-" + Utility.getFormatedNumber(price)));
        if (newLevel - 1 >= 0 && newLevel - 1 < card.lit.Count)
            StartCoroutine(pop((RectTransform)card.lit[newLevel - 1].transform.parent, 1.6f, 0.4f, 0.25f));
    }

    private static float easeOutBack(float t)
    {
        const float c = 1.9f;
        t -= 1f;
        return 1f + (c + 1f) * t * t * t + c * t * t;
    }

    private IEnumerator squeeze(RectTransform r)
    {
        for (float t = 0; t < 0.08f; t += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, t / 0.08f);
            yield return null;
        }
        for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, easeOutBack(t / 0.4f));
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    // From <paramref name="from"/> times its size back to its size, overshooting a little.
    private IEnumerator pop(RectTransform r, float from, float time, float delay = 0f)
    {
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);
        for (float t = 0; t < time; t += Time.unscaledDeltaTime)
        {
            r.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, easeOutBack(t / time));
            yield return null;
        }
        r.localScale = Vector3.one;
    }

    private IEnumerator fillTo(Card card, int level)
    {
        float from = Mathf.Max(card.slider.value, level - 1);
        for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            card.shown = Mathf.Lerp(from, level, t / 0.35f);
            yield return null;
        }
        card.shown = level;
    }

    // A turn and a bounce. The scale and rotation it started from are kept per icon, so a second
    // upgrade tapped before the first one ends does not leave the icon bigger or turned.
    private readonly Dictionary<Transform, Vector3> iconScale = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Quaternion> iconTurn = new Dictionary<Transform, Quaternion>();

    private IEnumerator spin(Transform mesh)
    {
        if (!iconScale.ContainsKey(mesh))
        {
            iconScale[mesh] = mesh.localScale;
            iconTurn[mesh] = mesh.localRotation;
        }
        Vector3 scale = iconScale[mesh];
        Quaternion turn = iconTurn[mesh];
        for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.8f, 3f);
            mesh.localRotation = turn * Quaternion.Euler(0, 360f * k, 0);
            mesh.localScale = scale * (1f + 0.25f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f)));
            yield return null;
        }
        mesh.localScale = scale;
        mesh.localRotation = turn;
    }

    private IEnumerator flare(Image glow, Color accent)
    {
        for (float t = 0; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.7f;
            glow.color = UiKit.WithAlpha(accent, Mathf.Lerp(1f, 0.42f, k * k));
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1f, k);
            yield return null;
        }
        glow.color = UiKit.WithAlpha(accent, 0.42f);
        glow.rectTransform.localScale = Vector3.one;
    }

    private IEnumerator ring(Card card, Vector2 at, float delay)
    {
        if (delay > 0)
            yield return new WaitForSecondsRealtime(delay);
        Image ring = UiKit.Image(card.root, "Pulse", "ring", card.accent);
        UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), at, new Vector2(178, 178));
        for (float t = 0; t < 0.65f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.65f;
            ring.rectTransform.localScale = Vector3.one * (1f + 0.9f * (1f - (1f - k) * (1f - k)));
            ring.color = UiKit.WithAlpha(card.accent, 0.9f * (1f - k));
            yield return null;
        }
        Destroy(ring.gameObject);
    }

    private IEnumerator spark(Card card, Vector2 at, int i)
    {
        Image spark = UiKit.Image(card.root, "Spark", "glow", Color.white);
        float size = Random.Range(22f, 40f);
        UiKit.Place(spark.rectTransform, new Vector2(0.5f, 0.5f), at, new Vector2(size, size));
        float angle = (i / 16f + Random.Range(-0.02f, 0.02f)) * Mathf.PI * 2f;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float distance = Random.Range(110f, 190f);
        float time = Random.Range(0.55f, 0.8f);
        Color colour = i % 3 == 0 ? Color.white : card.accent;
        for (float t = 0; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            spark.rectTransform.anchoredPosition = at + dir * distance * (1f - (1f - k) * (1f - k));
            spark.color = UiKit.WithAlpha(colour, 1f - k);
            yield return null;
        }
        Destroy(spark.gameObject);
    }

    // A light passing over the card, left to right.
    private IEnumerator sweep(Card card)
    {
        RectTransform clip = UiKit.Rect("Shine", card.root);
        UiKit.Stretch(clip, -4);
        clip.gameObject.AddComponent<RectMask2D>();
        Image bar = UiKit.Image(clip, "Bar", "round_fill", Color.white);
        bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(0, 0.5f);
        bar.rectTransform.sizeDelta = new Vector2(90, 520);
        bar.rectTransform.localRotation = Quaternion.Euler(0, 0, -22);
        UiKit.Gradient(bar, UiKit.WithAlpha(Color.white, 0f), UiKit.WithAlpha(Color.white, 0.3f), false);
        float width = card.root.rect.width;
        for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.6f;
            bar.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-120f, width + 120f, k * k * (3f - 2f * k)), 0);
            yield return null;
        }
        Destroy(clip.gameObject);
    }

    private IEnumerator floatUp(Card card, string text)
    {
        TMP_Text like = card.price != null ? (TMP_Text)card.price : card.level;
        TextMeshProUGUI cost = UiKit.Label(like, card.root, "Cost", text, 40, UiKit.Hex("E58CFF"), TextAlignmentOptions.Center);
        Vector2 start = new Vector2(372, 40);
        UiKit.Place(cost.rectTransform, new Vector2(0.5f, 0.5f), start, new Vector2(260, 60));
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime)
        {
            cost.rectTransform.anchoredPosition = start + new Vector2(0, 150f * (1f - (1f - t) * (1f - t)));
            cost.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            yield return null;
        }
        Destroy(cost.gameObject);
    }
}
