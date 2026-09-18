using UnityEngine;
using System.Collections;

// Drops a newly placed track part into its place: fast at first, slowing into place, with a small
// settle at the end. It runs on game time, as the ball does, so the two stay in step at any time scale;
// and it moves every rendered frame, so it is smooth on 90 and 120 Hz screens.
//
// It used to move a fixed 0.25 per physics step: a constant speed that stopped dead, in visible
// physics-step jumps on fast screens, and 0.8 s from start to landing where this takes 0.5 s.
public class MoveDown : MonoBehaviour
{
    private const float DROP_HEIGHT = 10f;
    private const float DROP_SECONDS = 0.5f;
    // easeOutBack's overshoot constant: 0.5 dips the part about 1% of the drop (0.08) below its
    // place before it settles. The usual 1.70158 dips it 10%, a full unit here.
    private const float OVERSHOOT = 0.5f;

    private Vector3 originalPos;
    private float elapsed;
    private Transform previousPart = null;

    public void setPreviousPart(Transform previousPart)
    {
        this.previousPart = previousPart;
    }

    void Start()
    {
        originalPos = transform.position;
        transform.position = originalPos + new Vector3(0, DROP_HEIGHT, 0);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < DROP_SECONDS)
        {
            float landed = easeOutBack(elapsed / DROP_SECONDS);
            transform.position = originalPos + new Vector3(0, DROP_HEIGHT * (1f - landed), 0);
            return;
        }

        transform.position = originalPos;
        // gameObject.AddComponent<Destroyer>();
        if (transform.Find("PartStartBlock") != null)
            transform.Find("PartStartBlock").gameObject.SetActive(false);
        if (previousPart != null && previousPart.Find("PartEndBlock") != null)
            previousPart.Find("PartEndBlock").gameObject.SetActive(false);
        Destroy(this);
    }

    // 0 to 1: fast at first, a little past 1 near the end, then back to exactly 1.
    private static float easeOutBack(float t)
    {
        float u = t - 1f;
        return 1f + (OVERSHOOT + 1f) * u * u * u + OVERSHOOT * u * u;
    }
}
