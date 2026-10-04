using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Sounds for the menus' controls that had none. Only nine buttons in the scene played the click
/// (their OnClick lists SoundManager.PlayMenu), and Back's own sound was never played anywhere;
/// switches and sliders were silent. <see cref="AddUnder"/> gives every button without a sound
/// the click - or the back sound, for Back, Cancel and Close - every switch an on or off blip and
/// every slider a tick when it is let go. Played on the player's own press, never when code sets
/// a value (opening Settings sets every switch).
/// </summary>
public class UiSound : MonoBehaviour, IPointerClickHandler, IPointerUpHandler
{
    public enum Kind { Toggle, Slider }
    public Kind kind;

    private static readonly string[] SOUNDED = { "PlayMenu", "PlayBack", "PlayBox" };

    public static void AddUnder(Transform root)
    {
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            if (hasSound(b) || b.name == "MysteryBox" || b.GetComponent<UiSoundAdded>() != null)
                continue; // the box plays its own (the reveal)
            b.gameObject.AddComponent<UiSoundAdded>();
            if (isBack(b.name))
                b.onClick.AddListener(() => SoundManager.Instance?.PlayBack());
            else
                b.onClick.AddListener(() => SoundManager.Instance?.PlayMenu());
        }
        foreach (Toggle t in root.GetComponentsInChildren<Toggle>(true))
            if (t.GetComponent<UiSound>() == null)
                t.gameObject.AddComponent<UiSound>().kind = Kind.Toggle;
        foreach (Slider s in root.GetComponentsInChildren<Slider>(true))
            if (s.interactable && s.GetComponent<UiSound>() == null)
                s.gameObject.AddComponent<UiSound>().kind = Kind.Slider;
    }

    private static bool hasSound(Button b)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            if (System.Array.IndexOf(SOUNDED, b.onClick.GetPersistentMethodName(i)) >= 0)
                return true;
        return false;
    }

    private static bool isBack(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("back") || n.Contains("cancel") || n.Contains("close") || n == "no" || n.StartsWith("no ");
    }

    // After the Toggle's own handler on the same object (added later), so isOn is the new value.
    public void OnPointerClick(PointerEventData e)
    {
        if (kind != Kind.Toggle)
            return;
        Toggle t = GetComponent<Toggle>();
        if (t != null && t.IsInteractable())
            SoundManager.Instance?.PlayToggle(t.isOn);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (kind == Kind.Slider && GetComponent<Slider>().IsInteractable())
            SoundManager.Instance?.PlaySliderTick();
    }
}

/// <summary>Marks a button <see cref="UiSound.AddUnder"/> has already given a sound.</summary>
public class UiSoundAdded : MonoBehaviour { }
