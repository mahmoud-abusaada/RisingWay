// Frame timings, for judging performance changes by measurement instead of by feel.
//
// In a development build it installs itself and prints one line every few seconds:
//
//   [PERF] 5.0s  fps avg 58.9  1% low 41.2   frame ms avg 17.0 p95 22.1 p99 24.3 worst 31.5
//   [PERF]       gpu ms 11.4  cpu ms 9.8   scale 1.00  hdr True  target 1000  quality Ultra
//
// Read it on a device with:  adb logcat -s Unity | findstr PERF
//
// "1% low" is the average of the slowest 1% of frames, which is what stutter feels like; a high
// p99 with a good average is a stutter problem, a bad average is a throughput problem. The GPU and
// CPU lines come from Unity's FrameTimingManager and need a few frames before they report.
//
// Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerformanceProbe : MonoBehaviour
{
    private const string TAG = "[PERF] ";
    private const float REPORT_EVERY_SECONDS = 5f;

    private readonly List<float> frames = new List<float>(1024);
    private readonly FrameTiming[] timings = new FrameTiming[1];
    private float windowStarted;
    private double gpuMsSum, cpuMsSum;
    private int timingSamples;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject go = new GameObject("~PerformanceProbe");
        DontDestroyOnLoad(go);
        go.AddComponent<PerformanceProbe>();
    }

    private void Start()
    {
        windowStarted = Time.realtimeSinceStartup;
        Debug.Log(TAG + "watching. Device: " + SystemInfo.deviceModel + " / " + SystemInfo.graphicsDeviceName +
                  " / " + SystemInfo.graphicsDeviceType + ", " + SystemInfo.systemMemorySize + " MB, " +
                  SystemInfo.processorCount + " cores, screen " + Screen.width + "x" + Screen.height +
                  " @" + Screen.currentResolution.refreshRateRatio.value.ToString("F0", CultureInfo.InvariantCulture) + "Hz");
    }

    private void Update()
    {
        frames.Add(Time.unscaledDeltaTime * 1000f);

        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            gpuMsSum += timings[0].gpuFrameTime;
            cpuMsSum += timings[0].cpuFrameTime;
            timingSamples++;
        }

        float elapsed = Time.realtimeSinceStartup - windowStarted;
        if (elapsed < REPORT_EVERY_SECONDS || frames.Count < 2)
            return;

        Report(elapsed);
        frames.Clear();
        gpuMsSum = cpuMsSum = 0;
        timingSamples = 0;
        windowStarted = Time.realtimeSinceStartup;
    }

    private void Report(float elapsed)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        List<float> sorted = new List<float>(frames);
        sorted.Sort();

        float sum = 0;
        foreach (float f in frames)
            sum += f;
        float mean = sum / frames.Count;

        // The slowest 1% of frames, averaged: the stutter the player actually notices.
        int lowCount = Mathf.Max(1, sorted.Count / 100);
        float lowSum = 0;
        for (int i = sorted.Count - lowCount; i < sorted.Count; i++)
            lowSum += sorted[i];
        float onePercentLowMs = lowSum / lowCount;

        Debug.Log(TAG + elapsed.ToString("F1", ci) + "s  fps avg " + (1000f / mean).ToString("F1", ci) +
                  "  1% low " + (1000f / onePercentLowMs).ToString("F1", ci) +
                  "   frame ms avg " + mean.ToString("F1", ci) +
                  " p95 " + Percentile(sorted, 0.95f).ToString("F1", ci) +
                  " p99 " + Percentile(sorted, 0.99f).ToString("F1", ci) +
                  " worst " + sorted[sorted.Count - 1].ToString("F1", ci) +
                  "  frames " + frames.Count);

        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        Debug.Log(TAG + "      " +
                  (timingSamples > 0
                      ? "gpu ms " + (gpuMsSum / timingSamples).ToString("F1", ci) + "  cpu ms " + (cpuMsSum / timingSamples).ToString("F1", ci)
                      : "gpu/cpu ms unavailable") +
                  "   scale " + (urp != null ? urp.renderScale.ToString("F2", ci) : "?") +
                  "  hdr " + (urp != null ? urp.supportsHDR.ToString() : "?") +
                  "  msaa " + (urp != null ? urp.msaaSampleCount.ToString() : "?") +
                  "  depth " + (urp != null ? urp.supportsCameraDepthTexture.ToString() : "?") +
                  "  bloom " + BloomState() +
                  "  target " + Application.targetFrameRate +
                  "  quality " + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                  "  scene " + (Utility.gameStarted ? "run" : "menu"));
    }

    // Whether the scene's bloom is on, for the line above: it is a per-frame GPU cost and the
    // PerfExperiment cycle switches it.
    private static string BloomState()
    {
        foreach (Volume v in FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
        {
            Bloom b;
            if (v.profile != null && v.profile.TryGet(out b))
                return b.active.ToString();
        }
        return "none";
    }

    private static float Percentile(List<float> sorted, float p)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[i];
    }
}
#endif
