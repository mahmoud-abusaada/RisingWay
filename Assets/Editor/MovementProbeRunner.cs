using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode entry point for <see cref="MovementProbe"/>: opens the game scene and enters play
/// mode. The probe drives the run and exits the Editor itself when it has its numbers.
///
///   Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod MovementProbeRunner.Run
///             -movementProbe [-probeFast] [-probeFps 60] [-probeTurns 40] -logFile probe.log
///
/// Do NOT pass -quit: that would close the Editor before play mode starts.
///
/// The run plays the game with the Editor's own save file and PlayerPrefs. The probe restores the
/// auto-pilot preference it changes, but pickups collected during the run are saved, so back up
/// %USERPROFILE%\AppData\LocalLow\Abu Sa'da\Rising Way\risingway.save.json first if it matters.
/// </summary>
public static class MovementProbeRunner
{
    private const string SCENE = "Assets/Scenes/SampleScene.unity";

    public static void Run()
    {
        EditorSceneManager.OpenScene(SCENE);
        Debug.Log("[MP] entering play mode");
        EditorApplication.EnterPlaymode();
    }
}
