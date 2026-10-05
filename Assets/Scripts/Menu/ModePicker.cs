using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Choosing the mode on the main menu (docs/game-modes-plan.md): swipe left or right anywhere on
/// the "tap to play" area - the name follows the finger and the next one slides in - or tap the
/// arrows. Dots under it show which of the three it is. Insane shows its lock and what opens it
/// until Standard's best gets there; a tap to play on it nudges the lock instead of starting.
///
/// Sits on the full-screen TapToPlayButton, so a swipe there is not also the tap that starts a
/// run: MainMenu.StartGame asks ConsumeSwipe() first.
/// </summary>
public class ModePicker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
{
    private const float SWIPE = 90f;       // canvas units to change mode
    private const float SLIDE = 520f;      // how far a name travels in and out
    private const float EASE = 14f;

    private static readonly RunMode[] Modes = { RunMode.Chill, RunMode.Standard, RunMode.Insane };

    private RectTransform holder;
    private TextMeshProUGUI title, subtitle;
    private CanvasGroup group;
    private Image lockIcon;
    private Image[] dots;
    private MainMenuSkin skin;
    private Canvas canvas;

    private int index = 1;
    private float offset, dragX;   // the name's x: eased back to 0 after a change; the finger's while dragging
    private bool dragging, swiped;
    private float nudge;           // a locked mode's shake

    public RunMode Shown => Modes[index];

    public static ModePicker Build(TMP_Text like, RectTransform menu, RectTransform tapArea, MainMenuSkin skin)
    {
        ModePicker p = tapArea.gameObject.AddComponent<ModePicker>();
        p.skin = skin;
        p.canvas = menu.GetComponentInParent<Canvas>();

        p.holder = UiKit.Rect("ModePicker", menu);
        p.holder.anchorMin = p.holder.anchorMax = new Vector2(0.5f, 1f);
        p.holder.pivot = new Vector2(0.5f, 0.5f);
        p.holder.anchoredPosition = new Vector2(0, -575);
        p.holder.sizeDelta = new Vector2(900, 240);

        RectTransform content = UiKit.Rect("Content", p.holder);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(760, 200);
        p.group = content.gameObject.AddComponent<CanvasGroup>();
        p.group.blocksRaycasts = false; // the swipe area underneath takes the touches

        p.title = UiKit.Label(like, content, "Mode", "", 68, Color.white, TextAlignmentOptions.Center);
        UiKit.Place(p.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(520, 96));
        p.title.enableAutoSizing = true; // INSANE and STANDARD both inside the arrows
        p.title.fontSizeMin = 40;
        p.title.fontSizeMax = 68;
        p.subtitle = UiKit.Label(like, content, "About", "", 34, UiKit.TextDim, TextAlignmentOptions.Center);
        UiKit.Place(p.subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -48), new Vector2(600, 50));
        p.subtitle.enableAutoSizing = true; // the lock's line is long: inside the arrows, clear of the social buttons
        p.subtitle.fontSizeMin = 20;
        p.subtitle.fontSizeMax = 34;
        p.lockIcon = UiKit.Image(content, "Lock", "lock", UiKit.TextDim);
        UiKit.Place(p.lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(54, 54));
        p.lockIcon.raycastTarget = false;

        // Arrows either side: they take taps (above the swipe area).
        p.previous = p.arrow(-1, new Vector2(-330, 30));
        p.next = p.arrow(1, new Vector2(330, 30));

        // Dots: which of the three.
        p.dots = new Image[Modes.Length];
        for (int i = 0; i < Modes.Length; i++)
        {
            Image d = UiKit.Image(p.holder, "Dot" + i, "circle", Color.white);
            UiKit.Place(d.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 34, -100), new Vector2(14, 14));
            d.raycastTarget = false;
            p.dots[i] = d;
        }

        RunMode saved = PlayerStats.Instance.getRunMode();
        p.index = System.Array.IndexOf(Modes, GameMode.IsUnlocked(saved) ? saved : RunMode.Standard);
        if (p.index < 0)
            p.index = 1;
        p.show(false);
        return p;
    }

    private GameObject previous, next;

    private GameObject arrow(int step, Vector2 at)
    {
        Image a = UiKit.Image(holder, step < 0 ? "Previous" : "Next", "chevron", UiKit.WithAlpha(Color.white, 0.85f));
        UiKit.Place(a.rectTransform, new Vector2(0.5f, 0.5f), at, new Vector2(64, 64));
        if (step < 0)
            a.rectTransform.localEulerAngles = new Vector3(0, 0, 180); // turned, not mirrored: PressDip sets the scale
        UiKit.Gradient(a, UiKit.Cyan, UiKit.Violet, false);
        // A bigger target than the arrow itself.
        Image hit = UiKit.Image(a.transform, "Hit", "round_fill", Color.clear);
        UiKit.Place(hit.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 170));
        hit.raycastTarget = true; // the tap area (UiKit's images take no taps by default)
        Button b = a.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => go(step));
        a.gameObject.AddComponent<PressDip>();
        return a.gameObject;
    }

    // ---- choosing ------------------------------------------------------------------------------

    private void go(int step)
    {
        int next = Mathf.Clamp(index + step, 0, Modes.Length - 1);
        if (next == index)
        {
            nudge = 0.35f * step; // at the end: a little bump the way it was pushed
            return;
        }
        index = next;
        offset = SLIDE * step; // the new name comes in from the side it was pushed from
        show(true);
    }

    private void show(bool sound)
    {
        RunMode mode = Shown;
        bool open = GameMode.IsUnlocked(mode);
        title.text = GameMode.Name(mode);
        lockIcon.gameObject.SetActive(!open);
        if (!open)
            subtitle.text = "Reach " + GameMode.INSANE_UNLOCK_SCORE + " in Standard to unlock";
        else
            subtitle.text = mode == RunMode.Chill ? "Relaxed. Unlimited revives"
                          : mode == RunMode.Insane ? "No help. Pure reflex"
                          : "The classic climb";
        Color from, to;
        colours(mode, out from, out to);
        if (!open)
            from = to = UiKit.Off;
        // TextMeshPro's own gradient (a mesh effect does not reach its text): left to right.
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(from, to, from, to);
        title.color = Color.white;
        // No arrow past the ends: none to the left of Chill, none to the right of Insane.
        if (previous != null)
            previous.SetActive(index > 0);
        if (next != null)
            next.SetActive(index < Modes.Length - 1);
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = i == index ? Color.Lerp(from, to, 0.5f) : UiKit.WithAlpha(Color.white, 0.25f);
        if (open)
            PlayerStats.Instance.setRunMode(mode); // the next run plays it
        if (skin != null)
            skin.ShowMode(mode);
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMenuMode(mode);
            if (sound)
                SoundManager.Instance.PlayMenu();
        }
    }

    public static void colours(RunMode mode, out Color from, out Color to)
    {
        switch (mode)
        {
            case RunMode.Chill: from = UiKit.Cyan; to = UiKit.Hex("5CFFB0"); break;
            case RunMode.Insane: from = UiKit.Amber; to = UiKit.Hex("FF4D6D"); break;
            default: from = UiKit.Cyan; to = UiKit.Violet; break;
        }
    }

    /// <summary>Shows that mode (tests: StoreCapture -shotMode).</summary>
    public void Select(RunMode mode)
    {
        int i = System.Array.IndexOf(Modes, mode);
        if (i < 0 || i == index)
            return;
        index = i;
        offset = 0f;
        show(false);
    }

    /// <summary>Tap to play on the mode shown: false (and the lock nudged) while it is locked.</summary>
    public bool CanPlay()
    {
        if (GameMode.IsUnlocked(Shown))
            return true;
        nudge = 0.5f;
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayBack();
        return false;
    }

    /// <summary>The tap that ends a swipe is not a tap to play.</summary>
    public bool ConsumeSwipe()
    {
        bool was = swiped;
        swiped = false;
        return was;
    }

    // ---- the swipe -----------------------------------------------------------------------------

    public void OnPointerDown(PointerEventData e)
    {
        swiped = false;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        // Marked here, not at the end: Unity sends the button its click on release BEFORE the
        // drag's end, so a swipe marked only then had already started a run.
        dragging = true;
        swiped = true;
        dragX = 0f;
    }

    public void OnDrag(PointerEventData e)
    {
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        dragX += e.delta.x / Mathf.Max(scale, 0.01f);
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        if (Mathf.Abs(dragX) >= SWIPE)
        {
            // Finger to the left: the next mode (from the right), and the other way.
            int step = dragX < 0 ? 1 : -1;
            int before = index;
            go(step);
            if (index != before)
                offset += dragX * 0.5f; // nearer, by where the finger left the old one
        }
        else
            offset = dragX * 0.5f; // not far enough: back into place
        dragX = 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float x = dragging ? dragX * 0.5f : offset;
        if (!dragging)
            offset = Mathf.Lerp(offset, 0f, 1f - Mathf.Exp(-EASE * dt));
        nudge = Mathf.MoveTowards(nudge, 0f, dt * 1.6f);
        float shake = nudge != 0f ? Mathf.Sin(Time.unscaledTime * 55f) * 18f * Mathf.Abs(nudge) : 0f;
        RectTransform content = (RectTransform)group.transform;
        content.anchoredPosition = new Vector2(x + shake, 0f);
        group.alpha = Mathf.Clamp01(1f - Mathf.Abs(x) / SLIDE * 1.4f);
    }
}
