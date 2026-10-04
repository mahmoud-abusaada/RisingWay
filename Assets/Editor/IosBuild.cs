// The iOS build: player settings, what Xcode's Info.plist and build settings need, and an Xcode
// project exported from here (Windows can export one; the Mac only needs Xcode and CocoaPods).
// The steps on the Mac are in docs/ios-build.md.
//
//   Unity.exe -batchmode -quit -projectPath <project> -buildTarget iOS -executeMethod IosBuild.Export -logFile <log>
//   (-bundleId <id> to use another bundle identifier for that export)
//
// IosBuild.Apply (Tools > Rising Way > iOS settings) sets the settings alone; Export applies
// them first.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public static class IosBuild
{
    private const string TAG = "[IosBuild] ";
    private const string OUTPUT = "Builds/iOS/Xcode";
    // The App Store icon may not have an alpha channel, even an opaque one: this copy is RGB, and
    // imported without alpha (1024.png, the Android/Play one, is RGBA).
    private const string ICON = "Assets/Textures/RisingWay icon/ios_1024.png";
    // The build number goes up with every upload (App Store Connect refuses one it has seen).
    // It follows the Android version code; raise it in Xcode if the old app already used it.
    private const string BUILD_NUMBER = "3";
    private const string TRACKING_TEXT =
        "Your permission lets us show ads that fit you better. Rising Way stays free either way.";

    [MenuItem("Tools/Rising Way/iOS settings")]
    public static void Apply()
    {
        string bundleId = Arg("-bundleId");
        if (!string.IsNullOrEmpty(bundleId))
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
        PlayerSettings.iOS.buildNumber = BUILD_NUMBER;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.requiresFullScreen = true;
        // Portrait only: the menus and the run are laid out for it. (The project allowed turning
        // to landscape, which an iPad or a phone with rotation on would do.)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true; // the team is picked in Xcode
        PlayerSettings.iOS.hideHomeButton = false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // ARM64

        // Icons: every iOS size from the 1024 one, the App Store's included.
        TextureImporter ti = AssetImporter.GetAtPath(ICON) as TextureImporter;
        if (ti != null && (ti.alphaSource != TextureImporterAlphaSource.None || ti.mipmapEnabled))
        {
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ICON);
        if (icon == null)
            Debug.LogError(TAG + "no icon at " + ICON);
        else
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.iOS))
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (PlatformIcon i in icons)
                    i.SetTextures(Enumerable.Repeat(icon, Math.Max(1, i.maxLayerCount)).ToArray());
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }

        // The App Tracking Transparency prompt's text: the ads SDK puts it in Info.plist
        // (NSUserTrackingUsageDescription); without it, asking would crash the app.
        string adsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        UnityEngine.Object ads = AssetDatabase.LoadMainAssetAtPath(adsSettings);
        if (ads != null)
        {
            SerializedObject so = new SerializedObject(ads);
            SerializedProperty p = so.FindProperty("userTrackingUsageDescription");
            if (p != null && string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = TRACKING_TEXT;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log(TAG + "bundle " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) + ", version " +
                  PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + "), iOS " + PlayerSettings.iOS.targetOSVersionString);
    }

    /// <summary>Exports the Xcode project to Builds/iOS/Xcode (batch mode; exits 0 or 1).</summary>
    public static void Export()
    {
        Apply();
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string output = Arg("-buildOutput") ?? OUTPUT;
        Directory.CreateDirectory(output);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log(TAG + "result " + report.summary.result + ", errors " + report.summary.totalErrors + ", " + report.summary.totalTime + " -> " + output);
        foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception)
                    Debug.LogError(TAG + m.content);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // After every iOS build: what the App Store needs in Info.plist and the project.
    [PostProcessBuild(900)] // after the ads and Firebase plugins' own steps
    public static void OnPostprocess(BuildTarget target, string path)
    {
#if UNITY_IOS
        if (target != BuildTarget.iOS)
            return;
        string plistPath = Path.Combine(path, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        PlistElementDict root = plist.root;
        // No encryption beyond what iOS itself does (HTTPS): App Store Connect stops asking the
        // export-compliance question on every upload.
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        if (root["NSUserTrackingUsageDescription"] == null)
            root.SetString("NSUserTrackingUsageDescription", TRACKING_TEXT);
        // Sharing a screenshot can offer "Save Image" (NativeShare adds these too; kept if so).
        if (root["NSPhotoLibraryAddUsageDescription"] == null)
            root.SetString("NSPhotoLibraryAddUsageDescription", "Lets you save your Rising Way screenshots to Photos.");
        plist.WriteToFile(plistPath);

        string projectPath = PBXProject.GetPBXProjectPath(path);
        PBXProject project = new PBXProject();
        project.ReadFromFile(projectPath);
        foreach (string guid in new[] { project.GetUnityMainTargetGuid(), project.GetUnityFrameworkTargetGuid(), project.ProjectGuid() })
            project.SetBuildProperty(guid, "ENABLE_BITCODE", "NO"); // gone from Xcode 14; Firebase never had it
        project.WriteToFile(projectPath);
        Debug.Log(TAG + "Info.plist and project settings written");
#endif
    }

    private static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
#endif
