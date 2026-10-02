using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shades a UI graphic from one colour to another across its rectangle (multiplied with its own
/// colour). The Nebula UI kit's sprites are white, and this is where most of their colour comes
/// from: one sprite serves every button and edge (UiKit).
/// </summary>
[AddComponentMenu("UI/Effects/Gradient (Rising Way)")]
public class UiGradient : BaseMeshEffect
{
    public Color from = Color.white;
    public Color to = Color.white;
    [Tooltip("Top to bottom; otherwise left to right.")]
    public bool vertical;

    public void Set(Color a, Color b, bool topToBottom)
    {
        from = a;
        to = b;
        vertical = topToBottom;
        if (graphic != null)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;
        Rect r = graphic.rectTransform.rect;
        UIVertex v = default;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            float t = vertical ? Mathf.InverseLerp(r.yMax, r.yMin, v.position.y)
                               : Mathf.InverseLerp(r.xMin, r.xMax, v.position.x);
            v.color = (Color)v.color * Color.Lerp(from, to, t);
            vh.SetUIVertex(v, i);
        }
    }
}
