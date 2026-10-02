using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// Refuses to build an app bundle (what gets uploaded to Play) while the STORE_CAPTURE define is
/// set. That define keeps the test hooks (free boxes, forced prizes, unlock-all) in a release-looking
/// build for store pictures; RisingWayBuilder adds it for one build and takes it off afterwards, but
/// a build that is killed half-way (it happened: the PC restarted) leaves it in ProjectSettings.
/// </summary>
public class StoreCaptureGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android || !EditorUserBuildSettings.buildAppBundle)
            return;
        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
        if (System.Array.IndexOf(defines.Replace(" ", "").Split(';'), "STORE_CAPTURE") >= 0)
            throw new BuildFailedException("STORE_CAPTURE is defined (Player Settings > Scripting Define Symbols). " +
                "It is for store-picture APKs only and must never be in an upload. Remove it and build again.");
    }
}
