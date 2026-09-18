using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Measures the walkable surface of each track piece, to check that consecutive pieces meet
/// without a step. PathMaker places pieces with hard-coded height offsets (PathMaker.setPartPosition);
/// if a mesh's own rise differs from its offset, the ball meets a small ledge at every seam.
///
/// Batch: Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod TrackPieceSurvey.Run
///
/// Each piece is instantiated unrotated, where its local +X is the travel direction (PathMaker
/// rotates North-bound pieces by -90 degrees, which maps local +X onto world +Z), and the surface
/// height is sampled along its centre line.
/// </summary>
public static class TrackPieceSurvey
{
    private static readonly string[] PIECES =
    {
        "PathLandStart", "PathLandStraight", "PathLandLeft", "PathLandRight",
        "PathCurveUp", "PathSlide", "PathCurveSt"
    };

    public static void Run()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[Survey] surface height along each piece's centre line (x = distance along travel)");
        for (int p = 0; p < PIECES.Length; p++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Parts/" + PIECES[p] + ".prefab");
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(0, 0, p * 50f); // well apart
            go.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();

            sb.Append("[Survey] ").Append(PIECES[p].PadRight(17));
            float first = float.NaN, last = float.NaN;
            float firstX = 0, lastX = 0;
            for (float x = -1.50f; x <= 1.501f; x += 0.05f)
            {
                RaycastHit hit;
                Vector3 origin = new Vector3(x, 30f, p * 50f);
                bool ok = Physics.Raycast(origin, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore)
                          && hit.collider.transform.IsChildOf(go.transform);
                if (ok)
                {
                    if (float.IsNaN(first)) { first = hit.point.y; firstX = x; }
                    last = hit.point.y;
                    lastX = x;
                }
            }
            sb.Append(" starts x=").Append(firstX.ToString("F2", ci)).Append(" y=").Append(first.ToString("F4", ci))
              .Append("   ends x=").Append(lastX.ToString("F2", ci)).Append(" y=").Append(last.ToString("F4", ci));

            // Exact heights at the nominal ends (+-1.25) and the centre.
            sb.Append("   y(-1.25)=").Append(Height(go, p, -1.2499f).ToString("F4", ci))
              .Append(" y(0)=").Append(Height(go, p, 0f).ToString("F4", ci))
              .Append(" y(+1.25)=").Append(Height(go, p, 1.2499f).ToString("F4", ci));
            sb.AppendLine();
            Object.DestroyImmediate(go);
        }
        Debug.Log(sb.ToString());
    }

    private static float Height(GameObject go, int p, float x)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 30f, p * 50f), Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.transform.IsChildOf(go.transform))
            return hit.point.y;
        return float.NaN;
    }
}
