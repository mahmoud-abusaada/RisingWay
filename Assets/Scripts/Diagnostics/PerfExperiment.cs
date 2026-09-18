// Cycles through graphics settings during a run so one play session answers "what would help a
// weak device, and by how much" - instead of one build per setting.
//
// Start it with a THREE-finger tap (three fingers on screen at once). It then changes settings
// every 12 seconds and says so in the log; PerformanceProbe reports frame times every 5 seconds
// with the settings of that window, so the two line up:
//
//   [PERF EXP] step 2/5: depth texture off, bloom off
//   [PERF] 5.0s  fps avg 41.2 ...
//   [PERF]       gpu ms 18.9 ... scale 1.00  hdr True  bloom False  depth False ...
//
// A second three-finger tap stops it and restores everything.
//
// The settings live on the URP asset and the scene's volume profile, which in a player build are
// loaded copies - changing them costs nothing and lasts until the app restarts. In the Editor it
// would dirty those files, so it refuses to run there.
//
// Deliberately excluded from release builds.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerfExperiment : MonoBehaviour
{
    private const string TAG = "[PERF EXP] ";
    private const float SECONDS_PER_STEP = 12f;

    private UniversalRenderPipelineAsset urp;
    private Bloom bloom;
    private bool running;
    private bool wasTouching;

    // What to restore when the cycle stops.
    private bool originalDepth, originalHdr, originalBloom;
    private float originalScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("~PerfExperiment");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfExperiment>();
    }

    private void Update()
    {
        // Three fingers down, counted once per touch.
        bool touching = Input.touchCount >= 3;
        if (touching && !wasTouching)
        {
            if (running)
                StopCycle();
            else
                StartCycle();
        }
        wasTouching = touching;
    }

    private void StartCycle()
    {
#if UNITY_EDITOR
        Debug.LogWarning(TAG + "not run in the Editor: it would change the URP asset and the volume profile on disk.");
        return;
#else
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            Debug.LogWarning(TAG + "no URP asset - nothing to change.");
            return;
        }

        bloom = FindBloom();
        originalDepth = urp.supportsCameraDepthTexture;
        originalHdr = urp.supportsHDR;
        originalScale = urp.renderScale;
        originalBloom = bloom != null && bloom.active;

        running = true;
        StartCoroutine(Cycle());
#endif
    }

    private void StopCycle()
    {
        running = false;
        StopAllCoroutines();
        if (urp != null)
        {
            urp.supportsCameraDepthTexture = originalDepth;
            urp.supportsHDR = originalHdr;
            urp.renderScale = originalScale;
        }
        if (bloom != null)
            bloom.active = originalBloom;
        Debug.Log(TAG + "stopped, settings restored.");
    }

    private IEnumerator Cycle()
    {
        // Each step keeps the previous one's changes, so the cost of each is the difference from
        // the step before it.
        yield return Step(1, "baseline, as shipped", () => { });
        yield return Step(2, "depth texture off", () => urp.supportsCameraDepthTexture = false);
        yield return Step(3, "+ bloom off", () => { if (bloom != null) bloom.active = false; });
        yield return Step(4, "+ HDR off", () => urp.supportsHDR = false);
        yield return Step(5, "+ render scale 0.8", () => urp.renderScale = 0.8f);
        Debug.Log(TAG + "cycle complete. Three-finger tap again to restore.");
    }

    private IEnumerator Step(int number, string what, System.Action apply)
    {
        apply();
        Debug.Log(TAG + "step " + number + "/5: " + what);
        yield return new WaitForSecondsRealtime(SECONDS_PER_STEP);
    }

    private static Bloom FindBloom()
    {
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
        {
            Bloom b;
            if (v.profile != null && v.profile.TryGet(out b))
                return b;
        }
        return null;
    }
}
#endif
