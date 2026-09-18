// Helper for MovementProbe: counts what the ball physically collides with during a run.
// Added to the player at runtime by the probe only. Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;

public class MovementProbeCollisionTap : MonoBehaviour
{
    // key -> { enters, total impulse }
    public readonly Dictionary<string, float[]> Hits = new Dictionary<string, float[]>();

    private void OnCollisionEnter(Collision c)
    {
        Transform t = c.collider.transform;
        string root = t.parent != null ? t.parent.tag + "/" : "";
        string key = root + t.name + " (layer " + LayerMask.LayerToName(t.gameObject.layer) + ")";
        float[] v;
        if (!Hits.TryGetValue(key, out v))
        {
            v = new float[2];
            Hits[key] = v;
        }
        v[0] += 1;
        v[1] += c.impulse.magnitude;
    }
}
#endif
