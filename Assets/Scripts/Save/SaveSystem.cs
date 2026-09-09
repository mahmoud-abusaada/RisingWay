using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads and persists the player's <see cref="SaveData"/> as JSON on disk.
///
/// P2-04. Replaces direct PlayerPrefs access for everything that counts as progress or
/// entitlement.
///
/// WHY THIS EXISTS
/// ---------------
/// The old code wrote currency with PlayerPrefs.SetInt and never called PlayerPrefs.Save()
/// anywhere except MaterialsManager. Unity flushes implicitly on pause and quit, so an orderly
/// exit was fine - but a crash or an OS kill lost everything since the last flush, including
/// real-money IAP grants written in PurchaseMenu.ProcessPurchase().
///
/// DURABILITY MODEL
/// ----------------
/// Writes go through a temp file and a rename, with the previous save kept as a .bak, so a
/// crash mid-write can never leave a truncated save as the only copy. Load falls back:
/// main file -> backup -> fresh default.
///
/// Two write paths, because not every change is worth a disk hit:
///   Save()        - immediate. Money, entitlements, unlocks, upgrades, high score.
///   MarkDirty()   - deferred. High-frequency in-run changes such as diamond pickups.
///                   Flushed by FlushIfDirty() on focus loss, on quit, and at run end.
///
/// The lifecycle hooks are wired here via RuntimeInitializeOnLoadMethod rather than a
/// MonoBehaviour, so this works with no scene setup and cannot be accidentally unhooked.
/// P2-06 adds the Android-specific OnApplicationPause path on top of this.
/// </summary>
public static class SaveSystem
{
    private const string FILE_NAME = "risingway.save.json";
    private const string TEMP_SUFFIX = ".tmp";
    private const string BACKUP_SUFFIX = ".bak";

    private static SaveData data;
    private static bool isDirty;

    private static string SavePath { get { return Path.Combine(Application.persistentDataPath, FILE_NAME); } }
    private static string TempPath { get { return SavePath + TEMP_SUFFIX; } }
    private static string BackupPath { get { return SavePath + BACKUP_SUFFIX; } }

    /// <summary>
    /// The live save. Loads from disk on first access if not already loaded, so callers never
    /// have to worry about initialisation order.
    /// </summary>
    public static SaveData Data
    {
        get
        {
            if (data == null)
                Load();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialise()
    {
        Load();

        // Static events, so there is nothing to unsubscribe and nothing to place in a scene.
        Application.quitting -= HandleQuitting;
        Application.quitting += HandleQuitting;
        Application.focusChanged -= HandleFocusChanged;
        Application.focusChanged += HandleFocusChanged;
    }

    private static void HandleQuitting()
    {
        FlushIfDirty();
    }

    private static void HandleFocusChanged(bool hasFocus)
    {
        if (!hasFocus)
            FlushIfDirty();
    }

    // ------------------------------------------------------------------ loading

    public static void Load()
    {
        data = ReadFrom(SavePath);

        if (data == null)
        {
            Debug.LogWarning("SaveSystem: primary save missing or unreadable, trying backup.");
            data = ReadFrom(BackupPath);
        }

        if (data == null)
        {
            Debug.Log("SaveSystem: no usable save found, creating a fresh one.");
            data = SaveData.CreateDefault();
            Save();
            return;
        }

        Migrate(data);
        Repair(data);
    }

    private static SaveData ReadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json))
                return null;

            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            // FromJson returns an all-defaults object for JSON that parses but is not ours,
            // so treat a nonsensical schema version as corruption rather than trusting it.
            if (loaded == null || loaded.schemaVersion <= 0)
                return null;

            return loaded;
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveSystem: failed to read " + path + " - " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Applies stepwise fix-ups for saves written by an older schema.
    /// Nothing to do yet - version 1 is the first schema. Each future bump adds a step here.
    /// </summary>
    private static void Migrate(SaveData d)
    {
        if (d.schemaVersion == SaveData.CURRENT_SCHEMA_VERSION)
            return;

        if (d.schemaVersion > SaveData.CURRENT_SCHEMA_VERSION)
        {
            // The player has run a newer build than this one, e.g. after a downgrade. Leave the
            // data alone rather than mangling it, and do not stamp it backwards.
            Debug.LogWarning("SaveSystem: save is from a newer schema (v" + d.schemaVersion +
                             " > v" + SaveData.CURRENT_SCHEMA_VERSION + "), leaving as-is.");
            return;
        }

        // Future migrations go here, e.g.:
        //   if (d.schemaVersion < 2) { ...; d.schemaVersion = 2; }

        d.schemaVersion = SaveData.CURRENT_SCHEMA_VERSION;
        Save();
    }

    /// <summary>
    /// Guards against a structurally valid file with impossible contents - hand-edited saves,
    /// partially written JSON that still parsed, or null collections.
    /// </summary>
    private static void Repair(SaveData d)
    {
        bool changed = false;

        if (d.ownedBallIds == null) { d.ownedBallIds = new System.Collections.Generic.List<int>(); changed = true; }
        if (d.ownedFloorIds == null) { d.ownedFloorIds = new System.Collections.Generic.List<int>(); changed = true; }

        // The starting ball and floor are always owned; without this a corrupted list could
        // leave the player with no equippable cosmetic at all.
        if (!d.ownedBallIds.Contains(1)) { d.ownedBallIds.Add(1); changed = true; }
        if (!d.ownedFloorIds.Contains(1)) { d.ownedFloorIds.Add(1); changed = true; }

        if (d.diamonds < 0) { d.diamonds = 0; changed = true; }
        if (d.doublePoints < 0) { d.doublePoints = 0; changed = true; }
        if (d.bolts < 0) { d.bolts = 0; changed = true; }
        if (d.chances < 0) { d.chances = 0; changed = true; }
        if (d.mysteryBoxes < 0) { d.mysteryBoxes = 0; changed = true; }
        if (d.highScore < 0) { d.highScore = 0; changed = true; }
        if (d.timesPlayed < 0) { d.timesPlayed = 0; changed = true; }

        if (d.doublePointsLevel < 1) { d.doublePointsLevel = 1; changed = true; }
        if (d.boltLevel < 1) { d.boltLevel = 1; changed = true; }
        if (d.chanceLevel < 1) { d.chanceLevel = 1; changed = true; }

        if (string.IsNullOrEmpty(d.gamePlayMode)) { d.gamePlayMode = "SingleTap"; changed = true; }

        if (changed)
            Save();
    }

    // ------------------------------------------------------------------ saving

    /// <summary>
    /// Marks the save as needing a write without touching the disk. Use for high-frequency
    /// in-run changes; call <see cref="FlushIfDirty"/> at a natural boundary.
    /// </summary>
    public static void MarkDirty()
    {
        isDirty = true;
    }

    public static void FlushIfDirty()
    {
        if (isDirty)
            Save();
    }

    /// <summary>
    /// Writes immediately. Temp file first, then rotate, so an interrupted write cannot destroy
    /// the only good copy.
    /// </summary>
    public static void Save()
    {
        if (data == null)
            return;

        try
        {
            string json = JsonUtility.ToJson(data, true);

            File.WriteAllText(TempPath, json);

            if (File.Exists(SavePath))
            {
                if (File.Exists(BackupPath))
                    File.Delete(BackupPath);
                File.Move(SavePath, BackupPath);
            }

            File.Move(TempPath, SavePath);

            isDirty = false;
        }
        catch (Exception e)
        {
            // Never let a failed write take gameplay down with it. The in-memory state is still
            // correct and the next flush may succeed.
            Debug.LogError("SaveSystem: failed to write save - " + e.Message);
        }

        // Device-local settings still live in PlayerPrefs, so flush those on the same beat.
        try
        {
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError("SaveSystem: PlayerPrefs.Save failed - " + e.Message);
        }
    }

    /// <summary>
    /// Wipes the save and starts fresh. Development and QA only - there is no in-game path to
    /// this, and it is deliberately not exposed through PlayerStats.
    /// </summary>
    public static void DeleteAndReset()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
        catch (Exception e)
        {
            Debug.LogError("SaveSystem: failed to delete save - " + e.Message);
        }

        data = SaveData.CreateDefault();
        Save();
    }
}
