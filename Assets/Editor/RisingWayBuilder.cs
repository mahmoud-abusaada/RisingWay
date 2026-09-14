using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line build entry points.
///
/// Added at P1-04 because Unity has no built-in command-line build for Android - only desktop
/// targets build without an -executeMethod. Needed for the build/inspect loop while the Gradle
/// chain is being modernised, and it becomes the CI entry point later (P8, P9).
///
/// Usage via the Unity CLI:
///   unity build "&lt;project&gt;" --target Android --execute-method RisingWayBuilder.BuildAndroidApk -o out.apk
///   unity build "&lt;project&gt;" --target Android --execute-method RisingWayBuilder.BuildAndroidAab -o out.aab
///
/// Or directly:
///   Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod RisingWayBuilder.BuildAndroidApk -buildOutput out.apk
///
/// Exits with code 1 on failure so the caller and CI see a non-zero status - a Unity batch-mode
/// build otherwise exits 0 even when the build failed.
/// </summary>
public static class RisingWayBuilder
{
    private const string DEFAULT_APK = "Builds/RisingWay.apk";
    private const string DEFAULT_AAB = "Builds/RisingWay.aab";

    public static void BuildAndroidApk()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        Build(ResolveOutput(DEFAULT_APK));
    }

    public static void BuildAndroidAab()
    {
        EditorUserBuildSettings.buildAppBundle = true;
        Build(ResolveOutput(DEFAULT_AAB));
    }

    /// <summary>
    /// Development build: defines DEVELOPMENT_BUILD, which is what gates
    /// Assets/Scripts/Diagnostics/RenderDiagnostics.cs. A release build strips that class out
    /// entirely, so use this entry point when the diagnostic needs to run on a device.
    /// </summary>
    public static void BuildAndroidApkDev()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        Build(ResolveOutput(DEFAULT_APK), true);
    }

    private static void Build(string outputPath, bool development = false)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Fail("No enabled scenes in Build Settings - nothing to build.");
            return;
        }

        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        Log("Building Android -> " + outputPath);
        Log("Scenes (" + scenes.Length + "): " + string.Join(", ", scenes));
        Log("targetSdk=" + PlayerSettings.Android.targetSdkVersion +
            "  minSdk=" + PlayerSettings.Android.minSdkVersion +
            "  scripting=" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) +
            "  architectures=" + PlayerSettings.Android.targetArchitectures +
            "  appBundle=" + EditorUserBuildSettings.buildAppBundle +
            "  development=" + development);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        catch (Exception e)
        {
            Fail("BuildPipeline threw: " + e);
            return;
        }

        BuildSummary summary = report.summary;
        Log("Result: " + summary.result +
            "  errors=" + summary.totalErrors +
            "  warnings=" + summary.totalWarnings +
            "  duration=" + summary.totalTime);

        if (summary.result == BuildResult.Succeeded)
        {
            Log("Output: " + summary.outputPath + "  (" + summary.totalSize + " bytes)");
            // Per-step timings are the fastest way to see where a slow build actually goes.
            foreach (BuildStep step in report.steps)
                Log(string.Format("  step {0,-60} {1}", Truncate(step.name, 60), step.duration));
            EditorApplication.Exit(0);
            return;
        }

        // Surface every error message the report carries. Unity's own log is enormous; these
        // are the lines that actually explain the failure.
        foreach (BuildStep step in report.steps)
        {
            foreach (BuildStepMessage msg in step.messages)
            {
                if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Assert)
                    LogError("[" + Truncate(step.name, 50) + "] " + msg.content);
            }
        }

        Fail("Build failed: " + summary.result);
    }

    private static string ResolveOutput(string fallback)
    {
        // The Unity CLI forwards -o as -buildOutput; honour it so the caller controls placement.
        string fromArgs = GetArg("-buildOutput");
        if (!string.IsNullOrEmpty(fromArgs))
            return fromArgs;

        Log("No -buildOutput supplied, defaulting to " + fallback);
        return fallback;
    }

    private static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s.Substring(0, max - 1) + "~";
    }

    private static void Log(string msg)
    {
        Debug.Log("[RisingWayBuilder] " + msg);
    }

    private static void LogError(string msg)
    {
        Debug.LogError("[RisingWayBuilder] " + msg);
    }

    private static void Fail(string msg)
    {
        LogError(msg);
        EditorApplication.Exit(1);
    }
}
