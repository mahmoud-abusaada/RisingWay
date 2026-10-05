using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#elif UNITY_IOS
using UnityEngine.SocialPlatforms.GameCenter;
#endif

/// <summary>
/// One leaderboard per mode (docs/game-modes-plan.md): Chill's longest climb without falling,
/// Standard's and Insane's best scores. Google Play Games on Android (the play-games-plugin-for-
/// unity v2 in Assets/GooglePlayGames), Game Center on iOS.
///
/// Signing in is quiet, at launch: the v2 plugin signs a player in automatically when they have
/// a Play Games profile, and nothing is asked of anyone who has not. Scores go up at the end of
/// each run (a board keeps a player's best by itself). Tapping the main menu's best chip opens
/// the board of the mode on screen.
///
/// Until the boards exist (the IDs below are empty, or Play Games is not set up - no app ID in
/// its settings), every call does nothing.
/// </summary>
public static class Leaderboards
{
#if UNITY_ANDROID
    // From the Play Console: Play Games Services > Leaderboards (IDs look like CgkI...EAIQAQ).
    private const string CHILL = "CgkI7-HQnL8REAIQAQ";
    private const string STANDARD = "CgkI7-HQnL8REAIQAA";
    private const string INSANE = "CgkI7-HQnL8REAIQAg";
#else
    // From App Store Connect: Game Center > Leaderboards (the IDs we choose there).
    private const string CHILL = "risingway.chill.longest_climb";
    private const string STANDARD = "risingway.standard.best";
    private const string INSANE = "risingway.insane.best";
#endif

    private static bool started, signedIn;

    private static string IdOf(RunMode mode) => mode == RunMode.Chill ? CHILL : mode == RunMode.Insane ? INSANE : STANDARD;

    /// <summary>Set up and sign in quietly (once, at launch).</summary>
    public static void SignIn()
    {
        if (started || !Configured())
            return;
        started = true;
#if UNITY_ANDROID
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(status =>
        {
            signedIn = status == SignInStatus.Success;
            Debug.Log("[Leaderboards] Play Games sign-in: " + status);
        });
#elif UNITY_IOS
        Social.localUser.Authenticate(ok =>
        {
            signedIn = ok;
            Debug.Log("[Leaderboards] Game Center sign-in: " + ok);
        });
#endif
    }

    /// <summary>The value a mode's board keeps: Chill's longest climb, the others' score.</summary>
    public static void Report(RunMode mode, long value)
    {
        string id = IdOf(mode);
        if (!signedIn || string.IsNullOrEmpty(id) || value <= 0)
            return;
#if UNITY_ANDROID
        PlayGamesPlatform.Instance.ReportScore(value, id, ok => Debug.Log("[Leaderboards] " + mode + " " + value + " sent: " + ok));
#elif UNITY_IOS
        Social.ReportScore(value, id, ok => Debug.Log("[Leaderboards] " + mode + " " + value + " sent: " + ok));
#endif
    }

    /// <summary>The board of that mode. Signs in first if the player was not (they tapped it).</summary>
    public static void Show(RunMode mode)
    {
        string id = IdOf(mode);
        if (!Configured() || string.IsNullOrEmpty(id))
            return;
#if UNITY_ANDROID
        if (!signedIn)
        {
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                signedIn = status == SignInStatus.Success;
                if (signedIn)
                    PlayGamesPlatform.Instance.ShowLeaderboardUI(id);
            });
            return;
        }
        PlayGamesPlatform.Instance.ShowLeaderboardUI(id);
#elif UNITY_IOS
        if (!signedIn)
        {
            Social.localUser.Authenticate(ok =>
            {
                signedIn = ok;
                if (ok)
                    GameCenterPlatform.ShowLeaderboardUI(id, UnityEngine.SocialPlatforms.TimeScope.AllTime);
            });
            return;
        }
        GameCenterPlatform.ShowLeaderboardUI(id, UnityEngine.SocialPlatforms.TimeScope.AllTime);
#endif
    }

    /// <summary>Whether the boards are there to use on this platform.</summary>
    public static bool Configured()
    {
#if UNITY_EDITOR
        return false;
#elif UNITY_ANDROID
        PlayGamesSettings settings = PlayGamesSettings.LoadInstance();
        return settings != null && !string.IsNullOrEmpty(settings.AppId) && !string.IsNullOrEmpty(STANDARD);
#elif UNITY_IOS
        return !string.IsNullOrEmpty(STANDARD);
#else
        return false;
#endif
    }
}
