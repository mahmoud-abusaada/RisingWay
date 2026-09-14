// Runtime instrumentation for the "path does not render" bug. READ-ONLY: it reports what the
// renderers actually hold at runtime and changes nothing.
//
// Why this exists: three plausible explanations for that bug were argued from source and asset
// files, and all three turned out wrong or untestable - a render-queue collision (ruled out by a
// Vulkan build behaving identically), a fade distance that is too short (the diagnostic build was
// inconclusive, because the material assets may override the shader default), and a regression
// introduced by the URP 17 upgrade (shader, materials and camera are all byte-identical to the
// shipped build). Reading files cannot settle this. Reading the live material can.
//
// Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using System.Text;
using UnityEngine;

public class RenderDiagnostics : MonoBehaviour
{
    private const int MAX_PARTS_REPORTED = 14;

    private bool lastPlayerIsInPosition;
    private bool autoDumpDone;
    private float autoDumpAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("~RenderDiagnostics");
        go.AddComponent<RenderDiagnostics>();
        DontDestroyOnLoad(go);
        Debug.Log("[RenderDiag] Installed. Press F1 to dump, or tap with 4 fingers on device. " +
                  "An automatic dump fires 1.5s after Utility.playerIsInPosition turns true - " +
                  "the exact moment PathMaker starts handing parts the fade material.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) || Input.touchCount >= 4)
            Dump("manual");

        // playerIsInPosition is the switch that makes PathMaker start handing parts the fade
        // material (PathMaker.cs:408). Before it flips, every part gets the opaque Lit material,
        // so dumping earlier would show a healthy scene and prove nothing.
        bool inPosition = Utility.playerIsInPosition;
        if (inPosition && !lastPlayerIsInPosition && !autoDumpDone)
            autoDumpAt = Time.time + 1.5f;
        lastPlayerIsInPosition = inPosition;

        if (autoDumpAt > 0f && Time.time >= autoDumpAt)
        {
            autoDumpAt = -1f;
            autoDumpDone = true;
            Dump("auto, 1.5s after playerIsInPosition");
        }
    }

    private void Dump(string reason)
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("================ RENDER DIAGNOSTICS (" + reason + ") ================");

        Camera cam = Camera.main;
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        Transform player = playerGo != null ? playerGo.transform : null;

        sb.AppendLine("graphicsDeviceType : " + SystemInfo.graphicsDeviceType);
        sb.AppendLine("screen             : " + Screen.width + "x" + Screen.height +
                      "  (aspect " + (Screen.width / (float)Screen.height).ToString("F3") + ")");
        sb.AppendLine("playerIsInPosition : " + Utility.playerIsInPosition +
                      "   gameStarted: " + Utility.gameStarted +
                      "   camFollowPlayer: " + Utility.camFollowPlayer);

        if (cam != null)
        {
            sb.AppendLine("camera world pos   : " + cam.transform.position.ToString("F2"));
            sb.AppendLine("camera fov         : " + cam.fieldOfView.ToString("F1") +
                          "   near/far: " + cam.nearClipPlane.ToString("F2") +
                          "/" + cam.farClipPlane.ToString("F0"));
            if (player != null)
            {
                sb.AppendLine("player world pos   : " + player.position.ToString("F2"));
                sb.AppendLine("CAMERA->PLAYER dist: " +
                              Vector3.Distance(cam.transform.position, player.position).ToString("F2") +
                              "   <-- compare against _FadeEndDistance below");
            }
        }
        else
        {
            sb.AppendLine("camera             : Camera.main is NULL");
        }

        Transform pathParent = FindLivePathParent();
        if (pathParent == null)
        {
            sb.AppendLine("");
            sb.AppendLine("No live path parent found (PathMaker.currentGamePathParent is null).");
            sb.AppendLine("Dump during an actual run, once the path has spawned.");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine("");
        sb.AppendLine("live path parts    : " + pathParent.childCount +
                      "   (reporting up to " + MAX_PARTS_REPORTED + ")");
        sb.AppendLine("");
        sb.AppendLine("FADE FACTOR is what the shader computes:");
        sb.AppendLine("    saturate((_FadeEndDistance - dist) / _FadeLength)");
        sb.AppendLine("0.00 means fully transparent - invisible - on any GPU or driver.");
        sb.AppendLine("");

        // One Debug.Log PER PART, not one giant string. logcat truncates a single oversized
        // message, and the first attempt at this was cut off mid-line with unrelated AdMob
        // output interleaved into it. Every line is prefixed [RD] so it can be grepped out of
        // a noisy log cleanly.
        Debug.Log(sb.ToString());

        int reported = 0;
        for (int i = 0; i < pathParent.childCount && reported < MAX_PARTS_REPORTED; i++)
        {
            Transform part = pathParent.GetChild(i);
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;

            StringBuilder ps = new StringBuilder(512);
            ps.AppendLine("[RD] part[" + i + "] \"" + part.name + "\"");
            for (int r = 0; r < renderers.Length; r++)
                AppendRenderer(ps, renderers[r], cam);
            Debug.Log(ps.ToString());
            reported++;
        }

        Debug.Log("[RD] ================ END ================");
    }

    private void AppendRenderer(StringBuilder sb, Renderer rend, Camera cam)
    {
        // sharedMaterial, NOT material: reading .material on a renderer with no instance yet
        // would create one, perturbing the very thing being measured.
        Material m = rend.sharedMaterial;
        sb.Append("[RD]   ").Append(rend.gameObject.name.PadRight(16));

        if (m == null)
        {
            sb.AppendLine("  material: NULL");
            return;
        }

        sb.Append("  shader=").Append(ShortShaderName(m.shader));
        sb.Append("  queue=").Append(m.renderQueue);
        sb.Append("  enabled=").Append(rend.enabled ? "Y" : "N");
        sb.Append("  visible=").Append(rend.isVisible ? "Y" : "N");
        sb.AppendLine();

        sb.Append("[RD]        ");
        sb.Append("alpha=").Append(SafeColorAlpha(m).ToString("F3"));
        sb.Append("  _Surface=").Append(Prop(m, "_Surface"));
        sb.Append("  _SrcBlend=").Append(Prop(m, "_SrcBlend"));
        sb.Append("  _DstBlend=").Append(Prop(m, "_DstBlend"));
        sb.Append("  _ZWrite=").Append(Prop(m, "_ZWrite"));
        sb.AppendLine();

        bool hasFade = m.HasProperty("_FadeEndDistance") && m.HasProperty("_FadeLength");
        sb.Append("[RD]        ");
        if (!hasFade)
        {
            sb.AppendLine("no fade properties on this shader (opaque Lit path)");
            return;
        }

        float fadeEnd = m.GetFloat("_FadeEndDistance");
        float fadeLen = m.GetFloat("_FadeLength");
        sb.Append("_FadeEndDistance=").Append(fadeEnd.ToString("F2"));
        sb.Append("  _FadeLength=").Append(fadeLen.ToString("F2"));

        if (cam != null)
        {
            // The two fade shaders use OPPOSITE formulas, and getting this wrong sent an earlier
            // investigation down a dead end - it reported healthy parts as INVISIBLE.
            //
            //   LitFadeWhenClose : alpha *= saturate((dist - _FadeEndDistance) / _FadeLength)
            //                      -> transparent NEAR the camera, so the track underneath the
            //                         player does not block the view. This is the one the path
            //                         actually uses.
            //   LitFadeWhenAway  : alpha *= saturate((_FadeEndDistance - dist) / _FadeLength)
            //                      -> transparent FAR from the camera.
            //
            // So read the shader rather than assuming which is in play.
            string shaderName = m.shader != null ? m.shader.name : "";
            bool fadeWhenClose = shaderName.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0;

            float dist = Vector3.Distance(rend.bounds.center, cam.transform.position);
            float fade = 1f;
            if (fadeLen > 0f)
            {
                fade = fadeWhenClose
                    ? Mathf.Clamp01((dist - fadeEnd) / fadeLen)
                    : Mathf.Clamp01((fadeEnd - dist) / fadeLen);
            }

            sb.Append("  dist=").Append(dist.ToString("F2"));
            sb.Append("  mode=").Append(fadeWhenClose ? "close" : "away");
            sb.Append("  ALPHA x").Append(fade.ToString("F2"));
            if (fade <= 0.001f) sb.Append("   <-- INVISIBLE");
        }
        sb.AppendLine();
    }

    private static string ShortShaderName(Shader s)
    {
        if (s == null) return "NULL";
        string n = s.name;
        int slash = n.LastIndexOf('/');
        return slash >= 0 && slash < n.Length - 1 ? n.Substring(slash + 1) : n;
    }

    private static float SafeColorAlpha(Material m)
    {
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor").a;
        if (m.HasProperty("_Color")) return m.GetColor("_Color").a;
        return -1f;
    }

    private static string Prop(Material m, string name)
    {
        return m.HasProperty(name) ? m.GetFloat(name).ToString("F0") : "-";
    }

    // currentGamePathParent is private on PathMaker. Reflection keeps this diagnostic entirely
    // self-contained, so PathMaker does not grow an accessor for a temporary tool.
    private static Transform FindLivePathParent()
    {
        PathMaker pm = FindObjectOfType<PathMaker>();
        if (pm == null) return null;

        FieldInfo f = typeof(PathMaker).GetField("currentGamePathParent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Transform t = f != null ? f.GetValue(pm) as Transform : null;
        if (t != null && t.childCount > 0) return t;

        FieldInfo fallback = typeof(PathMaker).GetField("pathParent",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Transform fb = fallback != null ? fallback.GetValue(pm) as Transform : null;
        return fb != null ? fb : t;
    }
}
#endif
