using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Purchase menu's counters (NebulaSkin.PurchaseCounters) as shortcuts: a tap on one glides
/// the list to the section that sells it - its top just under the list's top edge.
/// </summary>
public class PurchaseJump : MonoBehaviour
{
    private const float DURATION = 0.35f;
    private const float MARGIN = 24f;
    private ScrollRect scroll;
    /// <summary>Something over the top of the list (the counters' strip): sections stop under it.</summary>
    public RectTransform below;
    private Coroutine running;

    public void To(string section)
    {
        if (scroll == null)
            scroll = GetComponentInChildren<ScrollRect>(true);
        if (scroll == null || scroll.content == null)
            return;
        RectTransform target = null;
        foreach (Transform child in scroll.content)
            if (child.name == section && child.gameObject.activeInHierarchy) // the scene keeps switched-off copies
                target = (RectTransform)child;
        if (target == null)
            return;

        RectTransform content = scroll.content, viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        // Where the section's top is now, in the viewport's space, against where it should be.
        Vector3[] c = new Vector3[4];
        target.GetWorldCorners(c);
        float top = viewport.InverseTransformPoint(c[1]).y;           // corner 1: top left
        float want = viewport.rect.yMax - MARGIN;
        if (below != null)
        {
            below.GetWorldCorners(c);
            want = Mathf.Min(want, viewport.InverseTransformPoint(c[0]).y - MARGIN); // corner 0: bottom left
            target.GetWorldCorners(c);
        }
        float y = content.anchoredPosition.y + (want - top); // raising the content raises the section
        float most = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        y = Mathf.Clamp(y, 0f, most);

        scroll.StopMovement();
        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(glide(content, y));
    }

    private IEnumerator glide(RectTransform content, float to)
    {
        float from = content.anchoredPosition.y;
        for (float e = 0; e < DURATION; e += Time.unscaledDeltaTime)
        {
            float k = e / DURATION;
            k = 1f - (1f - k) * (1f - k) * (1f - k); // ease out
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Lerp(from, to, k));
            yield return null;
        }
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, to);
        running = null;
    }
}
