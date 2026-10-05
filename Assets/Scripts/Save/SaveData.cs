using System.Collections.Generic;

/// <summary>
/// The complete player save. Serialised to JSON by <see cref="SaveSystem"/>.
///
/// P2-04. Replaces ~40 loose PlayerPrefs keys plus one key per owned cosmetic
/// (Ball_1 .. Ball_72, Floor_1 .., FloorPattern_101 ..) with a single versioned document.
///
/// WHAT LIVES HERE vs PlayerPrefs
/// ------------------------------
/// Here: anything that represents player progress or entitlement, i.e. everything that
/// should follow the player to a new device once cloud save lands in P7-02.
///
/// PlayerPrefs: genuinely device-local settings - audio levels, render density, frame cap,
/// HDR/emission - plus the remote-config cache, which is disposable.
///
/// JsonUtility constraints
/// -----------------------
/// Fields must be public, non-readonly, and of types JsonUtility understands. That rules out
/// Dictionary and HashSet, which is why ownership is stored as List&lt;int&gt; and wrapped by
/// lookup helpers in PlayerStats rather than exposed directly.
/// </summary>
[System.Serializable]
public class SaveData
{
    /// <summary>
    /// Bump whenever the shape of this class changes in a way that needs fixing up on load,
    /// and add the corresponding step to SaveSystem.Migrate().
    ///
    /// Version 1 is the first schema. There is deliberately no migration from the old
    /// PlayerPrefs layout: the clean-slate decision means pre-2024 saves are not carried over.
    /// The field exists from day one so the NEXT change has an anchor to migrate from.
    /// </summary>
    public const int CURRENT_SCHEMA_VERSION = 1;

    public int schemaVersion = CURRENT_SCHEMA_VERSION;

    // ---------------------------------------------------------------- currencies
    public int diamonds = 0;
    public int doublePoints = 0;
    public int bolts = 0;
    public int chances = 0;
    public int mysteryBoxes = 0;

    /// <summary>
    /// Boxes opened since the last ball or floor: the count behind the guarantee
    /// (MysteryBoxPrizes.PITY_BOXES). Added after schema 1 shipped; an older save simply lacks
    /// it and JsonUtility leaves it at 0, which is the right starting value, so no migration.
    /// </summary>
    public int boxesSinceCosmetic = 0;

    // ---------------------------------------------------------------- progress
    /// <summary>Standard's best (the only mode there was before the modes came in).</summary>
    public int highScore = 0;
    public int timesPlayed = 0;

    // Game modes (GameMode, docs/game-modes-plan.md). Added after schema 1: an older save lacks
    // them and they start at their defaults, which is right - no migration.
    /// <summary>The chosen mode, by name (as gamePlayMode is), so reordering the enum is safe.</summary>
    public string runMode = "Standard";
    public int insaneHighScore = 0;
    /// <summary>Chill has no best score: the longest stretch climbed without falling instead.</summary>
    public int chillLongestStreak = 0;
    /// <summary>Runs in a row that ended under the Chill suggestion's score, and whether the
    /// suggestion and the Insane unlock have been shown (they show once).</summary>
    public int shortRunsInARow = 0;
    public bool chillSuggested = false;
    public bool insaneUnlockShown = false;

    // ---------------------------------------------------------------- upgrades
    // Levels are 1-based. Caps live in Utility.Constants
    // (DOUBLE_POINTS_MAX_LEVEL, BOLT_MAX_LEVEL, CHANCE_MAX_LEVEL).
    public int doublePointsLevel = 1;
    public int boltLevel = 1;
    public int chanceLevel = 1;

    // ---------------------------------------------------------------- cosmetics
    // IDs come from the serialized lists on the MaterialsManager component in SampleScene:
    // ball IDs and colour-floor IDs start at 1, pattern-floor IDs start at 101, so ball and
    // floor ID spaces never collide and a single owned-list per category is enough.
    //
    // P2-05: these IDs are a PERMANENT CONTRACT the moment a build ships. Reordering or
    // inserting into those scene lists reassigns what every player owns. Do not renumber.
    public int selectedBallId = 1;
    public int selectedFloorId = 1;
    public List<int> ownedBallIds = new List<int>();
    public List<int> ownedFloorIds = new List<int>();
    // Cosmetics the player has put on at least once. An owned one that is not here is new: the
    // shop marks it NEW, with a dot on its tab and on the main menu's Shop button.
    // usedTracked is false in a save from before these lists; SaveSystem.Repair then counts
    // everything already owned as used, so an old player is not shown their whole collection as new.
    public bool usedTracked = false;
    public List<int> usedBallIds = new List<int>();
    public List<int> usedFloorIds = new List<int>();

    // ---------------------------------------------------------------- entitlements
    /// <summary>
    /// True once the "remove_ads" non-consumable has been purchased or restored.
    ///
    /// Stored as "removed" rather than "enabled" so the default (false) means ads are on,
    /// and so no code path can turn ads back on by writing a default value. P2-01 was caused
    /// by exactly that: MenusController.Awake() force-wrote the old enabled flag to true on
    /// every launch and destroyed the purchase.
    /// </summary>
    public bool adsRemoved = false;

    // ---------------------------------------------------------------- player preferences
    // These follow the account rather than the device: a returning player should not be
    // re-tutorialised or have their control scheme reset on a new phone.
    public bool tutorialsOn = true;

    /// <summary>
    /// Stored as the string name of <see cref="GamePlayMode"/> rather than the enum value, so
    /// reordering the enum cannot silently change a player's control scheme.
    /// Parsed defensively in PlayerStats.getGamePlayMode().
    /// </summary>
    public string gamePlayMode = "SingleTap";

    /// <summary>
    /// A brand-new player starts owning the first ball and the first floor, matching the old
    /// behaviour where PlayerMovement.Awake() called unlockBall(1) / unlockFloor(1).
    /// </summary>
    public static SaveData CreateDefault()
    {
        SaveData data = new SaveData();
        data.ownedBallIds.Add(1);
        data.ownedFloorIds.Add(1);
        data.usedTracked = true;
        data.usedBallIds.Add(1);
        data.usedFloorIds.Add(1);
        return data;
    }
}
