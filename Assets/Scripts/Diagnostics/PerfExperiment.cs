// Measures what each graphics setting costs on a real device, in one unattended run, so the
// defaults for weak devices can be chosen from numbers rather than by feel.
//
// To run it, create an empty file called "perf-cycle" in the game's files folder, then launch a
// development build:
//
//   adb shell touch /sdcard/Android/data/com.abusaada.risingway/files/perf-cycle
//
// It waits for the menu and starts a run with auto-pilot on and tutorials off. Nobody touches the
// screen - a tap would turn the ball - and each step is held for 15 seconds:
//
//   1 baseline, as shipped
//   2 depth texture off
//   3 + bloom off
//   4 + HDR off
//   5 + render scale 0.8
//   6 baseline again
//   7 baseline, sky sprites hidden
//
// Steps 2-5 keep the previous step's changes, so each one's cost is its difference from the step
// before it. Step 6 is the control: the run speeds up and the phone warms up as it goes, and if
// step 6 disagrees with step 1, the differences in between cannot be trusted. Step 7 is the cost
// of the clouds and galaxy sprites on their own. Each step ends with one line:
//
//   [PERF EXP] 2 depth texture off   fps  52.3  1% low  38.0  frame  19.1 ms  gpu  17.2 ms  cpu   8.1 ms
//
// Afterwards the graphics settings, auto-pilot and tutorials go back to what they were and the
// file is deleted, so it runs once per file. If the app dies first, the file keeps the player's
// own auto-pilot and tutorial settings, and the next launch runs again and restores them.
//
// The graphics settings live on the URP asset and the scene's volume profile, which in a player
// are loaded copies: changing them lasts until the app restarts. In the Editor it would change
// those files, so it refuses to run there.
//
// Deliberately excluded from release builds.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerfExperiment : MonoBehaviour
{
    private const string TAG = "[PERF EXP] ";
    private const string MARKER_FILE = "perf-cycle";
    private const float SECONDS_PER_STEP = 15f;
    // Frames right after a change are not counted: switching HDR or the render scale reallocates
    // the render targets, which is a one-off hitch rather than the cost of the setting.
    private const float SETTLE_SECONDS = 1.5f;

    private string markerPath;
    private UniversalRenderPipelineAsset urp;
    private Bloom bloom;
    private readonly List<GameObject> hiddenSky = new List<GameObject>();
    private readonly List<string> results = new List<string>();

    // What to put back afterwards.
    private bool graphicsCaptured;
    private bool originalDepth, originalHdr, originalBloom, originalAutoPilot, originalTutorials;
    private float originalScale;
    private int originalSleepTimeout;

    // The step being measured.
    private bool measuring, runEnded;
    private readonly List<float> frameMs = new List<float>(2048);
    private double gpuMsSum, cpuMsSum;
    private int gpuSamples, cpuSamples;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string path = Path.Combine(Application.persistentDataPath, MARKER_FILE);
        if (!File.Exists(path))
        {
#if !UNITY_EDITOR
            Debug.Log(TAG + "idle. To measure graphics settings, create " + path + " and relaunch.");
#endif
            return;
        }
        GameObject go = new GameObject("~PerfExperiment");
        DontDestroyOnLoad(go);
        go.AddComponent<PerfExperiment>().markerPath = path;
    }

    private IEnumerator Start()
    {
#if UNITY_EDITOR
        Debug.LogWarning(TAG + "not run in the Editor: it would change the URP asset and the volume profile on disk. Delete " + markerPath + ".");
        Destroy(gameObject);
        yield break;
#else
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            Debug.LogWarning(TAG + "no URP asset - nothing to measure.");
            Destroy(gameObject);
            yield break;
        }

        // The player's own settings - from the file, if an earlier cycle did not finish.
        string saved = File.ReadAllText(markerPath);
        bool resuming = saved.Contains("autopilot=");
        originalAutoPilot = resuming ? saved.Contains("autopilot=1") : PlayerStats.Instance.isAutoPilotOn();
        originalTutorials = resuming ? saved.Contains("tutorials=1") : PlayerStats.Instance.isTutorialsOn();
        File.WriteAllText(markerPath, "autopilot=" + (originalAutoPilot ? 1 : 0) + " tutorials=" + (originalTutorials ? 1 : 0));
        originalSleepTimeout = Screen.sleepTimeout;

        // Let the menu settle. A consent form pauses the game until it is answered.
        yield return new WaitForSecondsRealtime(4f);
        while (Time.timeScale == 0f)
            yield return null;

        PlayerStats.Instance.setAutoPilotState(true);
        PlayerStats.Instance.setTutorialsState(false);
        // Nobody touches the screen during the cycle, so it would otherwise dim and lock.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        FindAnyObjectByType<MenusOperations>().StartGame();

        float waited = 0;
        while (!Utility.gameStarted && waited < 10f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!Utility.gameStarted)
        {
            Debug.LogWarning(TAG + "the run did not start - nothing measured.");
            Finish();
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);

        // Read after the run starts, so whatever GraphicsManager applied from the player's
        // settings is what "as shipped" means, and what gets restored.
        bloom = FindBloom();
        originalDepth = urp.supportsCameraDepthTexture;
        originalHdr = urp.supportsHDR;
        originalScale = urp.renderScale;
        originalBloom = bloom != null && bloom.active;
        graphicsCaptured = true;

        CultureInfo ci = CultureInfo.InvariantCulture;
        Debug.Log(TAG + "measuring on " + SystemInfo.deviceModel + " / " + SystemInfo.graphicsDeviceName + " / " +
                  SystemInfo.graphicsDeviceType + ", screen " + Screen.width + "x" + Screen.height + " @" +
                  Screen.currentResolution.refreshRateRatio.value.ToString("F0", ci) + "Hz, target fps " +
                  Application.targetFrameRate + ". As shipped: scale " + originalScale.ToString("F2", ci) +
                  ", hdr " + originalHdr + ", depth " + originalDepth + ", msaa " + urp.msaaSampleCount +
                  ", bloom " + (bloom != null ? originalBloom.ToString() : "none"));

        (string name, Action apply)[] steps =
        {
            ("1 baseline, as shipped", () => { }),
            ("2 depth texture off", () => urp.supportsCameraDepthTexture = false),
            ("3 + bloom off", () => { if (bloom != null) bloom.active = false; }),
            ("4 + HDR off", () => urp.supportsHDR = false),
            ("5 + render scale 0.8", () => urp.renderScale = 0.8f),
            ("6 baseline again", () => RestoreGraphics()),
            ("7 baseline, sky sprites hidden", () => HideSky(true)),
        };
        foreach ((string name, Action apply) in steps)
        {
            if (!Utility.gameStarted)
            {
                Debug.LogWarning(TAG + "the run ended - stopping early.");
                break;
            }
            yield return Measure(name, apply);
        }
        Finish();
#endif
    }

    private IEnumerator Measure(string name, Action apply)
    {
        apply();
        Debug.Log(TAG + "step " + name);
        yield return new WaitForSecondsRealtime(SETTLE_SECONDS);

        frameMs.Clear();
        gpuMsSum = cpuMsSum = 0;
        gpuSamples = cpuSamples = 0;
        runEnded = false;
        measuring = true;
        yield return new WaitForSecondsRealtime(SECONDS_PER_STEP - SETTLE_SECONDS);
        measuring = false;

        string row = Row(name);
        results.Add(row);
        Debug.Log(TAG + row);
    }

    private void Update()
    {
        if (!measuring)
            return;

        frameMs.Add(Time.unscaledDeltaTime * 1000f);
        if (!Utility.gameStarted)
            runEnded = true;

        if (PerformanceProbe.HasLatestTiming)
        {
            FrameTiming t = PerformanceProbe.LatestTiming;
            if (t.gpuFrameTime > 0)
            {
                gpuMsSum += t.gpuFrameTime;
                gpuSamples++;
            }
            if (t.cpuFrameTime > 0)
            {
                cpuMsSum += t.cpuFrameTime;
                cpuSamples++;
            }
        }
    }

    private string Row(string name)
    {
        if (frameMs.Count < 2)
            return name + "  no frames";

        CultureInfo ci = CultureInfo.InvariantCulture;
        float meanMs, onePercentLowMs;
        PerformanceProbe.Summarise(frameMs, new List<float>(frameMs.Count), out meanMs, out onePercentLowMs);
        return name.PadRight(32) +
               "fps " + (1000f / meanMs).ToString("F1", ci).PadLeft(5) +
               "  1% low " + (1000f / onePercentLowMs).ToString("F1", ci).PadLeft(5) +
               "  frame " + meanMs.ToString("F1", ci).PadLeft(5) + " ms" +
               "  gpu " + PerformanceProbe.AverageMs(gpuMsSum, gpuSamples).PadLeft(5) + " ms" +
               "  cpu " + PerformanceProbe.AverageMs(cpuMsSum, cpuSamples).PadLeft(5) + " ms" +
               (runEnded ? "  (the run ended during this step)" : "");
    }

    private void Finish()
    {
        if (graphicsCaptured)
            RestoreGraphics();
        HideSky(false);
        PlayerStats.Instance.setAutoPilotState(originalAutoPilot);
        PlayerStats.Instance.setTutorialsState(originalTutorials);
        Screen.sleepTimeout = originalSleepTimeout;

        try
        {
            File.Delete(markerPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning(TAG + "could not delete " + markerPath + ": " + e.Message);
        }

        Debug.Log(TAG + "done - settings, auto-pilot and tutorials restored. Results:");
        foreach (string row in results)
            Debug.Log(TAG + row);
        Destroy(gameObject);
    }

    private void RestoreGraphics()
    {
        urp.supportsCameraDepthTexture = originalDepth;
        urp.supportsHDR = originalHdr;
        urp.renderScale = originalScale;
        if (bloom != null)
            bloom.active = originalBloom;
    }

    // The sky sprites AmbientEffectsController spawns: 120 clouds, each with its own copy of its
    // material, and the galaxy sprites, all with a CloudLookAt script running every frame.
    private void HideSky(bool hide)
    {
        if (hide)
        {
            foreach (CloudLookAt c in FindObjectsByType<CloudLookAt>(FindObjectsInactive.Exclude))
            {
                c.gameObject.SetActive(false);
                hiddenSky.Add(c.gameObject);
            }
            Debug.Log(TAG + "hid " + hiddenSky.Count + " sky sprites");
            return;
        }

        foreach (GameObject g in hiddenSky)
            if (g != null)
                g.SetActive(true);
        hiddenSky.Clear();
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
