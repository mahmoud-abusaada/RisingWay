using UnityEngine;

/// <summary>The three ways to play (docs/game-modes-plan.md). Not the control scheme - that is
/// SaveData.gamePlayMode (one tap, left/right, swipe).</summary>
public enum RunMode
{
    Chill = 0,    // relaxed: slower, more forgiving, unlimited revives, no best score
    Standard = 1, // the game as it has always been
    Insane = 2,   // faster, harder patterns from the start, no turning help; unlocked by Standard
}

/// <summary>
/// What each mode changes. The mode is taken when a run starts (BeginRun) and holds for the whole
/// run, so choosing another one in the menu never changes a run already under way.
/// </summary>
public static class GameMode
{
    /// <summary>Standard's best that opens Insane.</summary>
    public const int INSANE_UNLOCK_SCORE = 300;

    public sealed class Tuning
    {
        public float startSpeed, topSpeed;
        public float rampScore;          // the score at which top speed is reached
        public float earlyTapSeconds;    // PlayerMovement's early-tap grace; 0 = none
        public int startPatternTier;     // turn patterns open from the first part
        public int maxPatternTier;       // never past this one
        public float patternScoreScale;  // the scores that open each tier (110, 200, ...) times this
        public int diamondRunMin, diamondRunMax; // diamonds in a row (PathMaker)
        public int diamondValue;         // diamonds a pickup is worth
        public int boxOneIn;             // a mystery box: 1 in this many power-up chances
        public bool chanceSpawns;        // the chance pickup turns up on the track
        public bool unlimitedRevives;    // a fall lifts the ball back and waits for a tap
        public bool hasBestScore;
        // The run's sound (SoundManager.mixRun)
        public float pulseGain, shimmerGain, ambientKeep, runPitchTop, pluckVolume;
    }

    private static readonly Tuning chill = new Tuning
    {
        startSpeed = 4.5f, topSpeed = 9f, rampScore = 900f,
        earlyTapSeconds = 0.25f,
        startPatternTier = 0, maxPatternTier = 2, patternScoreScale = 1f,
        diamondRunMin = 4, diamondRunMax = 8, diamondValue = 1,
        boxOneIn = 60, chanceSpawns = false,
        unlimitedRevives = true, hasBestScore = false,
        pulseGain = 0.45f, shimmerGain = 0f, ambientKeep = 0.55f, runPitchTop = 1.06f, pluckVolume = 0.42f,
    };

    private static readonly Tuning standard = new Tuning
    {
        startSpeed = Utility.Constants.START_PLAYER_SPEED, topSpeed = Utility.Constants.TOP_PLAYER_SPEED, rampScore = 600f,
        earlyTapSeconds = 0.15f,
        startPatternTier = 0, maxPatternTier = 5, patternScoreScale = 1f,
        diamondRunMin = 7, diamondRunMax = 15, diamondValue = 1,
        boxOneIn = 30, chanceSpawns = true,
        unlimitedRevives = false, hasBestScore = true,
        pulseGain = 1f, shimmerGain = 1f, ambientKeep = 0f, runPitchTop = 1.16f, pluckVolume = 0.6f,
    };

    private static readonly Tuning insane = new Tuning
    {
        startSpeed = 7.5f, topSpeed = 15.5f, rampScore = 420f,
        earlyTapSeconds = 0f,
        startPatternTier = 3, maxPatternTier = 5, patternScoreScale = 0.5f,
        diamondRunMin = 7, diamondRunMax = 15, diamondValue = 2,
        boxOneIn = 30, chanceSpawns = true,
        unlimitedRevives = false, hasBestScore = true,
        pulseGain = 1.25f, shimmerGain = 1.2f, ambientKeep = 0f, runPitchTop = 1.22f, pluckVolume = 0.6f,
    };

    private static RunMode run = RunMode.Standard;

    /// <summary>The mode of the run under way (or the last one).</summary>
    public static RunMode Current => run;
    public static Tuning T => Of(run);

    public static Tuning Of(RunMode mode) => mode == RunMode.Chill ? chill : mode == RunMode.Insane ? insane : standard;

    /// <summary>A run is starting: from now on it plays the chosen mode.</summary>
    public static void BeginRun()
    {
        run = PlayerStats.Instance != null ? PlayerStats.Instance.getRunMode() : RunMode.Standard;
        // The tutorial is Standard's; a locked mode (a save from a phone that had it) plays Standard.
        if (!IsUnlocked(run) || (PlayerStats.Instance != null && PlayerStats.Instance.isTutorialsOn()))
            run = RunMode.Standard;
    }

    /// <summary>Tests only (MovementProbe -probeMode, StoreCapture): play a mode whatever the save.</summary>
    public static bool IgnoreLocks;

    public static bool IsUnlocked(RunMode mode)
    {
        if (mode != RunMode.Insane || IgnoreLocks)
            return true;
        return PlayerStats.Instance != null && PlayerStats.Instance.getHighScore(RunMode.Standard) >= INSANE_UNLOCK_SCORE;
    }

    public static string Name(RunMode mode) => mode == RunMode.Chill ? "CHILL" : mode == RunMode.Insane ? "INSANE" : "STANDARD";

    /// <summary>Where the speed should be at this score.</summary>
    public static float SpeedAt(int score)
    {
        Tuning t = T;
        return Mathf.Lerp(t.startSpeed, t.topSpeed, Mathf.Clamp01(score / t.rampScore));
    }
}
