using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Presses on the reveal overlay (the Choices backdrop, over the whole list) and on the box
/// button, passed to MysteryBoxReveal: the press itself, not the release - a click needs the
/// finger to leave where it went down, and the list's scrolling took small moves for drags, so
/// taps often did nothing. One press skips the spin, the next collects.
/// </summary>
public class MysteryBoxRevealTap : MonoBehaviour, IPointerDownHandler
{
    public MysteryBoxReveal reveal;
    public void OnPointerDown(PointerEventData eventData) { if (reveal != null) reveal.OnTap(); }
}
