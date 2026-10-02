using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The power-up buttons in a run, on the left (InGameUI's "PickUps"), in the Nebula look (UiKit):
/// a glass column of round buttons, each with its icon, how many are owned on a badge, and its
/// state at a glance - dimmed when there are none, lit with a ring while it is on. Double points
/// show the time left on the ring.
///
/// Made over the scene's buttons, which keep their clicks (InGameUI.ActivateBolt and the rest).
/// </summary>
public class PowerUpDock : MonoBehaviour
{
    private class Slot
    {
        public PickUpType type;
        public Color accent;
        public CanvasGroup group;
        public Image ring;
        public Image glow;
        public Image timer;
        public RectTransform badge;
        public RectTransform button;
        public RectTransform mesh;
        public Renderer model;
    }

    private const float BUTTON = 124f;
    private const float SPACING = 150f;
    private readonly List<Slot> slots = new List<Slot>();
    private PickUpsManager pickUps;

    public void Build(PickUpsManager manager)
    {
        pickUps = manager;
        RectTransform dock = (RectTransform)transform;
        dock.anchorMin = dock.anchorMax = new Vector2(0, 0);
        dock.pivot = new Vector2(0.5f, 0.5f);
        dock.anchoredPosition = new Vector2(100, 430);
        dock.sizeDelta = new Vector2(152, 3 * SPACING + 22);
        Image back = GetComponent<Image>();
        if (back != null)
        {
            UiKit.GlassPanel(back, 0.45f);
            back.color = UiKit.WithAlpha(UiKit.Glass, 0.5f);
            back.pixelsPerUnitMultiplier = 0.5f; // a capsule
            back.raycastTarget = false;          // only the buttons take taps
            Transform edge = back.transform.Find("Edge");
            if (edge != null)
                edge.GetComponent<Image>().pixelsPerUnitMultiplier = 0.5f;
        }

        add("DoublePoints", PickUpType.DoublePoints, UiKit.Gold, SPACING);
        add("Bolt", PickUpType.Bolt, UiKit.Hex("4FA8FF"), 0);
        add("Chance", PickUpType.Chance, UiKit.Hex("FF5C7A"), -SPACING);
    }

    private void add(string name, PickUpType type, Color accent, float y)
    {
        Transform button = transform.Find(name);
        if (button == null)
            return;
        Slot slot = new Slot { type = type, accent = accent };
        UiKit.Place((RectTransform)button, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(BUTTON, BUTTON));
        slot.group = button.GetComponent<CanvasGroup>();
        if (slot.group == null)
            slot.group = button.gameObject.AddComponent<CanvasGroup>();

        Image disc = button.GetComponent<Image>();
        UiKit.Style(disc, "circle", UiKit.GlassDeep);
        disc.raycastTarget = true;

        slot.glow = UiKit.Image(button, "Glow", "glow", UiKit.WithAlpha(accent, 0));
        UiKit.Stretch(slot.glow.rectTransform, 46);
        slot.glow.transform.SetAsFirstSibling();
        slot.ring = UiKit.Image(button, "Ring", "ring", UiKit.WithAlpha(accent, 0.55f));
        UiKit.Stretch(slot.ring.rectTransform, 2);
        slot.ring.transform.SetSiblingIndex(1);
        slot.timer = UiKit.Image(button, "Timer", "ring_thick", accent);
        UiKit.Stretch(slot.timer.rectTransform, 8);
        slot.timer.type = Image.Type.Filled;
        slot.timer.fillMethod = Image.FillMethod.Radial360;
        slot.timer.fillOrigin = (int)Image.Origin360.Top;
        slot.timer.fillClockwise = false;
        slot.timer.transform.SetSiblingIndex(2);

        // The 3D model is kept in the middle of its circle by its own bounds (Update): its pivot is
        // not its middle, and a fixed offset that looks right through one camera is off through
        // another (the run's UI camera is orthographic, the menus' is not).
        slot.button = (RectTransform)button;
        Transform mesh = button.Find("Mesh");
        if (mesh != null)
        {
            slot.mesh = (RectTransform)mesh;
            slot.model = mesh.GetComponentInChildren<Renderer>(true);
        }

        Transform count = button.Find("Count");
        if (count != null)
        {
            slot.badge = (RectTransform)count;
            UiKit.Place(slot.badge, new Vector2(0.5f, 0.5f), new Vector2(46, 46), new Vector2(56, 40));
            Image badge = count.GetComponent<Image>();
            UiKit.Style(badge, "pill", Color.white);
            UiKit.Gradient(badge, accent, Color.Lerp(accent, Color.black, 0.35f), true);
            TextMeshProUGUI text = count.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                UiKit.Restyle(text, 24, Color.white, TextAlignmentOptions.Center);
                UiKit.Stretch(text.rectTransform);
                text.margin = Vector4.zero;
            }
            count.SetAsLastSibling();
        }
        slots.Add(slot);
    }

    private static void centre(Slot slot)
    {
        if (slot.model == null || !slot.model.isVisible && !slot.model.gameObject.activeInHierarchy)
            return;
        Vector3 c = slot.button.InverseTransformPoint(slot.model.bounds.center);
        Vector2 off = new Vector2(c.x, c.y) - slot.button.rect.center;
        if (off.sqrMagnitude > 0.25f)
            slot.mesh.anchoredPosition -= off;
    }

    void Update()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
            return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (Slot slot in slots)
        {
            int owned;
            bool on;
            float left = 1f;
            switch (slot.type)
            {
                case PickUpType.DoublePoints:
                    owned = stats.getDoublePointsCount();
                    on = Utility.doublePointIsOn;
                    if (on && pickUps != null)
                        left = pickUps.doublePointsLeft();
                    break;
                case PickUpType.Bolt:
                    owned = stats.getBoltsCount();
                    on = Utility.boltIsOn;
                    break;
                default:
                    owned = stats.getChancesCount();
                    on = Utility.chanceIsOn;
                    break;
            }
            slot.group.alpha = owned > 0 || on ? 1f : 0.38f;
            centre(slot);
            slot.ring.color = UiKit.WithAlpha(slot.accent, on ? 0 : 0.55f);
            slot.timer.enabled = on;
            slot.timer.fillAmount = left;
            slot.glow.color = UiKit.WithAlpha(slot.accent, on ? 0.35f + 0.25f * pulse : 0f);
            if (slot.badge != null)
                slot.badge.gameObject.SetActive(owned > 0);
        }
    }
}
