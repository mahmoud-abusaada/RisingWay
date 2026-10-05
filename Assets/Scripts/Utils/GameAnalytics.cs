using System;
using System.Collections.Generic;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Everything the game reports to Google Analytics for Firebase, in one place.
///
/// Added for the 2026 relaunch. The 2024 release logged a single event (a "login" every time the
/// shop opened), so when players left in large numbers there was nothing to say why. These events
/// are built to answer that: how far a new player gets, where the tutorial loses them, how their
/// first runs go, and whether ads, difficulty or the shop drive them away.
///
/// Rules this class keeps:
///   - It never throws into gameplay. Firebase not ready, not installed or failing: events are
///     dropped (or held briefly, see below), the game carries on.
///   - Events fired before Firebase has finished initialising are held (up to 50) and sent once
///     it is ready, so the very first session - the one that matters most - is not lost.
///   - Nothing personal: no names, no free text, only game numbers and choices. Covered by the
///     privacy policy's Google Analytics for Firebase entry.
///   - Editor: logged to the console instead of sent, so testing does not pollute the data.
///
/// Event and parameter names follow Firebase's limits (40 characters, letters/digits/underscore)
/// and are a contract with the dashboards built on them: rename one and its history splits.
/// </summary>
public static class GameAnalytics
{
    private static bool ready;
    private static bool initStarted;
    private static readonly Queue<Action> pending = new Queue<Action>();
    private const int MAX_PENDING = 50;

    // Run state, for the numbers only known at the end of a run.
    private static float runStartTime;
    private static int runRevives;
    private static int runIndex;
    private static bool runIsTutorial;
    private static bool userPropertiesSet;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (initStarted) return;
        initStarted = true;
#if !UNITY_EDITOR
        // ContinueWithOnMainThread: the held events are sent from the main thread.
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            bool ok = !task.IsFaulted && task.Result == Firebase.DependencyStatus.Available;
            if (!ok)
            {
                Debug.LogWarning("GameAnalytics: Firebase unavailable (" + (task.IsFaulted ? task.Exception?.Message : task.Result.ToString()) + "). Events are dropped.");
                pending.Clear();
                return;
            }
            ready = true;
#if DEVELOPMENT_BUILD
            Debug.Log("[GameAnalytics] Firebase ready, sending " + pending.Count + " held event(s)");
#endif
            while (pending.Count > 0) Safe(pending.Dequeue());
        });
#endif
    }

    // ---- Runs ------------------------------------------------------------------------------------

    /// <summary>A run begins (the player tapped to play).</summary>
    public static void RunStarted(int timesPlayed, bool tutorial, string controlMode)
    {
        runStartTime = Time.realtimeSinceStartup;
        runRevives = 0;
        runIndex = timesPlayed;
        runIsTutorial = tutorial;
        if (!userPropertiesSet)
        {
            // Lets every event be split by control scheme without a parameter on each one.
            userPropertiesSet = true;
            UserProperty("control_mode", controlMode);
        }
        Log("run_start",
            P("run_number", timesPlayed),
            P("tutorial", tutorial ? 1 : 0),
            P("control_mode", controlMode),
            P("game_mode", GameMode.Current.ToString()));
        if (timesPlayed == 1)
            Log("first_run_start", P("control_mode", controlMode));
    }

    /// <summary>A one-time mode suggestion shown at game over ("insane" unlocked, "chill").</summary>
    public static void ModeSuggested(string mode)
    {
        Log("mode_suggested", P("game_mode", mode));
    }

    /// <summary>The player took the suggestion and started that mode.</summary>
    public static void ModeSuggestionTaken(string mode)
    {
        Log("mode_suggestion_taken", P("game_mode", mode));
    }

    /// <summary>The run is over (game over shown). Duration is wall-clock, pauses included.</summary>
    public static void RunEnded(int score, int highScoreBefore, int diamonds, int patternTier)
    {
        int seconds = Mathf.RoundToInt(Time.realtimeSinceStartup - runStartTime);
        Parameter[] ps =
        {
            P("run_number", runIndex),
            P("score", score),
            P("duration_s", seconds),
            P("diamonds", diamonds),
            P("revives", runRevives),
            P("new_best", score > highScoreBefore ? 1 : 0),
            P("pattern_tier", patternTier),
            P("tutorial", runIsTutorial ? 1 : 0),
            P("game_mode", GameMode.Current.ToString()),
        };
        Log("run_end", ps);
        // The first runs get their own events so the funnel is one glance: how did run 1, 2, 3 go.
        if (runIndex >= 1 && runIndex <= 3)
            Log("early_run_end", P("run_number", runIndex), P("score", score), P("duration_s", seconds));
        // Firebase's standard score event, which some dashboards pick up by themselves.
        Log(FirebaseAnalytics.EventPostScore, P(FirebaseAnalytics.ParameterScore, score));
    }

    public static void ReviveOffered(int score) { Log("revive_offer", P("score", score)); }
    public static void ReviveSkipped() { Log("revive_skip"); }
    /// <param name="source">"ad" or "chance".</param>
    public static void Revived(string source) { runRevives++; Log("revive", P("source", source)); }

    public static void PowerUpUsed(string kind) { Log("powerup_use", P("kind", kind)); }
    public static void Paused() { Log("run_pause", P("run_number", runIndex)); }

    // ---- Tutorial --------------------------------------------------------------------------------

    public static void TutorialBegin() { Log(FirebaseAnalytics.EventTutorialBegin); }
    /// <summary>With how often the player was late (the ball stopped for them) and tapped too early.</summary>
    public static void TutorialComplete(int late, int early) { Log(FirebaseAnalytics.EventTutorialComplete, P("late", late), P("early", early)); }
    public static void TutorialStep(string step) { Log("tutorial_step", P("step", step)); }
    public static void TutorialSkipped() { Log("tutorial_skip"); }
    public static void TutorialToggled(bool on) { Log("tutorial_setting", P("on", on ? 1 : 0)); }

    // ---- Screens and settings --------------------------------------------------------------------

    public static void Screen(string name)
    {
        Log(FirebaseAnalytics.EventScreenView, P(FirebaseAnalytics.ParameterScreenName, name), P(FirebaseAnalytics.ParameterScreenClass, name));
    }

    public static void ControlModeChanged(string mode)
    {
        Log("control_mode_set", P("mode", mode));
        UserProperty("control_mode", mode);
    }

    public static void SettingChanged(string setting, int value) { Log("setting_change", P("setting", setting), P("value", value)); }

    // ---- Economy ---------------------------------------------------------------------------------

    public static void MysteryBoxOpened(MysteryBoxPrize prize)
    {
        Log("box_open",
            P("prize", prize.kind.ToString()),
            P("rarity", prize.rarity.ToString()),
            P("amount", prize.amount),
            P("guaranteed", prize.fromPity ? 1 : 0));
    }

    public static void OddsViewed(string where) { Log("box_odds_view", P("where", where)); }

    /// <param name="how">"diamonds" or "box".</param>
    public static void CosmeticUnlocked(string type, int id, string how, int price)
    {
        Log("cosmetic_unlock", P("type", type), P("id", id), P("how", how), P("price", price));
    }

    public static void CosmeticSelected(string type, int id) { Log("cosmetic_select", P("type", type), P("id", id)); }

    public static void Upgraded(string powerUp, int level, int cost)
    {
        Log("upgrade", P("powerup", powerUp), P("level", level), P("cost", cost));
    }

    // Google Play purchases are recorded by Firebase itself (in_app_purchase, with revenue). These
    // add what it cannot see: purchases that were started but not finished, and why.
    public static void PurchaseStarted(string productId) { Log("iap_start", P("product", productId ?? "")); }
    public static void PurchaseFailed(string productId, string reason) { Log("iap_fail", P("product", productId ?? ""), P("reason", reason)); }
    public static void PurchaseDone(string productId) { Log("iap_done", P("product", productId ?? "")); }
    public static void StoreUnavailable() { Log("iap_store_unavailable"); }

    // ---- Ads -------------------------------------------------------------------------------------

    /// <param name="placement">"game_over", "revive", "mystery_box".</param>
    public static void AdShown(string format, string placement) { Log("ad_show", P("format", format), P("placement", placement)); }
    /// <summary>An interstitial that would have shown, held back by the new-player grace period.</summary>
    public static void AdSkippedForNewPlayer(int runNumber) { Log("ad_grace_skip", P("run_number", runNumber)); }
    public static void RewardEarned(string placement) { Log("ad_reward", P("placement", placement)); }

    public static void Shared(int score) { Log(FirebaseAnalytics.EventShare, P(FirebaseAnalytics.ParameterContentType, "score"), P("score", score)); }

    // ---- Plumbing --------------------------------------------------------------------------------

    public static void UserProperty(string name, string value)
    {
        Send(() => FirebaseAnalytics.SetUserProperty(name, value), "user property " + name + "=" + value);
    }

    private static Parameter P(string name, string value) { return new Parameter(name, value); }
    private static Parameter P(string name, long value) { return new Parameter(name, value); }

    private static void Log(string name, params Parameter[] parameters)
    {
        Send(() => FirebaseAnalytics.LogEvent(name, parameters), name);
    }

    private static void Send(Action send, string description)
    {
#if UNITY_EDITOR
        Debug.Log("[GameAnalytics] " + description);
#else
#if DEVELOPMENT_BUILD
        Debug.Log("[GameAnalytics] " + description);
#endif
        if (ready)
            Safe(send);
        else if (pending.Count < MAX_PENDING)
            pending.Enqueue(send);
#endif
    }

    private static void Safe(Action a)
    {
        try { a(); }
        catch (Exception e) { Debug.LogWarning("GameAnalytics: " + e.Message); }
    }
}
