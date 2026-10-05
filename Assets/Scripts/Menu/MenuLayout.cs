using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One layout for the menus' scrolling lists (Shop, Purchase, Settings), so every screen has the
/// same spacing: the list starts the same distance under what is above it, and on Purchase and
/// Settings its groups are as wide as the Purchase counters box.
/// </summary>
public static class MenuLayout
{
    /// <summary>The space between a list and what is above it (canvas units, 1080 wide).</summary>
    public const float GAP_ABOVE_LIST = 30f;
    /// <summary>The width of the Purchase counters box, and of the groups in the lists.</summary>
    public const float GROUP_WIDTH = 1000f;

    /// <summary>
    /// Moves the list's top edge to <see cref="GAP_ABOVE_LIST"/> under the lowest of
    /// <paramref name="above"/>, keeping its bottom edge; with <paramref name="width"/> &gt; 0 it is
    /// also that wide, centred.
    /// </summary>
    public static void ListBelow(RectTransform list, float width, params RectTransform[] above)
    {
        if (list == null || !(list.parent is RectTransform parent))
            return;
        Canvas.ForceUpdateCanvases();
        Vector3[] c = new Vector3[4];
        float aboveBottom = float.MaxValue;
        foreach (RectTransform a in above)
        {
            if (a == null || !a.gameObject.activeSelf)
                continue;
            a.GetWorldCorners(c);
            aboveBottom = Mathf.Min(aboveBottom, parent.InverseTransformPoint(c[0]).y);
        }
        if (aboveBottom == float.MaxValue)
            return;
        list.GetWorldCorners(c);
        Vector3 bottomLeft = parent.InverseTransformPoint(c[0]);
        Vector3 topRight = parent.InverseTransformPoint(c[2]);
        float top = aboveBottom - GAP_ABOVE_LIST;
        float left = bottomLeft.x, right = topRight.x;
        if (width > 0f)
        {
            float centre = parent.rect.center.x;
            left = centre - width / 2f;
            right = centre + width / 2f;
        }
        Vector2 size = new Vector2(right - left, top - bottomLeft.y);
        if (size.y <= 0f)
            return;
        list.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        list.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        Vector2 centreAt = new Vector2((left + right) / 2f, (top + bottomLeft.y) / 2f);
        list.localPosition = new Vector3(centreAt.x + (list.pivot.x - 0.5f) * size.x,
                                         centreAt.y + (list.pivot.y - 0.5f) * size.y, list.localPosition.z);
    }

    /// <summary>
    /// A list with no box of its own: the box's colour gone (it still takes the drags) and the
    /// viewport out to its edges, so the groups can be as wide as the list.
    /// </summary>
    public static void NoBox(ScrollRect scroll)
    {
        if (scroll == null)
            return;
        Image box = scroll.GetComponent<Image>();
        if (box != null)
            box.color = Color.clear;
        Transform edge = scroll.transform.Find("Edge");
        if (edge != null)
            edge.gameObject.SetActive(false);
        if (scroll.viewport != null)
        {
            scroll.viewport.offsetMin = Vector2.zero;
            scroll.viewport.offsetMax = Vector2.zero;
        }
    }
}

/// <summary>
/// Keeps the groups of a list (the content's panels, and the rows in them) as wide as the content,
/// also those made later - Purchase builds its groups when the store has loaded.
/// </summary>
public class ListGroupsFit : MonoBehaviour
{
    private int seen = -1;
    private float seenWidth = -1f;

    void LateUpdate()
    {
        RectTransform content = (RectTransform)transform;
        float width = content.rect.width;
        if (content.childCount == seen && Mathf.Approximately(width, seenWidth))
            return;
        seen = content.childCount;
        seenWidth = width;
        foreach (Transform child in content)
        {
            if (child.GetComponent<Image>() == null || child.GetComponent<Selectable>() != null)
                continue;
            RectTransform group = (RectTransform)child;
            float was = group.rect.width;
            if (was <= 0f || Mathf.Approximately(was, width))
                continue;
            // The rows' widths before the group changes (a stretched row follows the group by itself).
            float[] rows = new float[group.childCount];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = ((RectTransform)group.GetChild(i)).rect.width;
            group.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            for (int i = 0; i < rows.Length; i++)
            {
                RectTransform row = (RectTransform)group.GetChild(i);
                // Rows that spanned the group (dividers) span it still; the item rows keep their inset.
                if (Mathf.Abs(rows[i] - was) < 1f)
                    row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                else if (rows[i] > was * 0.8f)
                    row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - (was - rows[i]));
            }
        }
    }
}
