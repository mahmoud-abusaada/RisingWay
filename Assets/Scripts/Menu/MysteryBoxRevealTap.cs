using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Taps on the reveal overlay (the Choices backdrop), passed to MysteryBoxReveal.</summary>
public class MysteryBoxRevealTap : MonoBehaviour, IPointerClickHandler
{
    public MysteryBoxReveal reveal;
    public void OnPointerClick(PointerEventData eventData) { if (reveal != null) reveal.OnTap(); }
}
