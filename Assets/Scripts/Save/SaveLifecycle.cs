using UnityEngine;

/// <summary>
/// Flushes deferred save writes at every point the OS might stop giving us frames.
///
/// P2-06. SaveSystem already subscribes to Application.quitting and Application.focusChanged,
/// which covers the desktop and editor cases. This adds the MonoBehaviour messages, which are
/// the ones that actually matter on mobile:
///
///   OnApplicationPause  - Android's real signal. focusChanged is not reliably delivered when
///                         the activity is backgrounded, and on Android a paused app can be
///                         killed at any moment without further callbacks. This is the last
///                         guaranteed chance to write.
///   OnApplicationFocus  - iOS resign-active, and the editor losing focus.
///   OnApplicationQuit   - orderly exit.
///
/// Installed automatically at startup, so there is nothing to place in a scene and nothing that
/// can be accidentally deleted from SampleScene. The project has 869 GameObjects in one scene
/// and no bootstrap - adding one more hand-wired object would just be another thing to lose.
///
/// Before this existed the project had NO OnApplicationPause / OnApplicationFocus /
/// OnApplicationQuit handler anywhere, so an OS kill lost everything since the last write -
/// including real-money IAP grants.
/// </summary>
public class SaveLifecycle : MonoBehaviour
{
    private static SaveLifecycle instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("~SaveLifecycle");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SaveLifecycle>();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            SaveSystem.FlushIfDirty();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SaveSystem.FlushIfDirty();
    }

    private void OnApplicationQuit()
    {
        SaveSystem.FlushIfDirty();
    }
}
