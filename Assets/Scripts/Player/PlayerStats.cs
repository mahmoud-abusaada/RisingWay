using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single gateway to persisted player state.
///
/// P2-04. The public API is unchanged from the PlayerPrefs-backed version, so none of the 27
/// files that call into this needed touching. Only the storage behind it changed:
///
///   Progress and entitlement  -> SaveData / SaveSystem (a versioned JSON document)
///   Device-local settings     -> PlayerPrefs (audio, render density, frame cap, HDR)
///   Remote-config cache       -> PlayerPrefs (disposable)
///
/// The split matters for P7-02: everything in the save file is what cloud save will sync.
/// Volume sliders and render scale should stay on the device they were set on.
/// </summary>
public sealed class PlayerStats : ScriptableObject
{
    private PlayerStats() { }
    private static PlayerStats instance = null;
    public static PlayerStats Instance
    {
        get
        {
            if (instance == null)
            {
                instance = ScriptableObject.CreateInstance<PlayerStats>();
            }
            return instance;
        }
    }

    private static SaveData Data { get { return SaveSystem.Data; } }

    /// <summary>
    /// Writes any deferred changes to disk. Call at natural boundaries - end of a run, leaving
    /// a menu, completing a purchase. SaveSystem also flushes on focus loss and on quit.
    /// </summary>
    public void Flush()
    {
        SaveSystem.FlushIfDirty();
    }

    // ================================================================== currencies
    //
    // Spending always writes immediately. Earning writes immediately too, EXCEPT the
    // single-diamond pickup case: that fires many times per run, and a disk write per pickup
    // would risk a frame hitch during the most performance-sensitive part of the game.
    // Any amount above 1 is a grant - mystery box, ad reward, or an IAP - and is worth the
    // immediate write. The deferred single-diamond case is covered by the flush on focus loss,
    // on quit, and at run end.

    public int getDiamondsCount()
    {
        return Data.diamonds;
    }
    public void addDiamonds(int amount = 1)
    {
        Data.diamonds += amount;
        if (amount > 1) SaveSystem.Save(); else SaveSystem.MarkDirty();
    }
    public void subtractDiamonds(int amount)
    {
        // Clamped at zero. Every caller checks affordability first, so this only ever fires if
        // something upstream is wrong - better a floor than a negative balance written to disk.
        Data.diamonds = Mathf.Max(0, Data.diamonds - amount);
        SaveSystem.Save();
    }

    public int getDoublePointsCount()
    {
        return Data.doublePoints;
    }
    public void addDoublePoints(int amount = 1)
    {
        Data.doublePoints += amount;
        SaveSystem.Save();
    }
    public void subtractDoublePoints(int amount = 1)
    {
        Data.doublePoints = Mathf.Max(0, Data.doublePoints - amount);
        SaveSystem.Save();
    }

    public int getBoltsCount()
    {
        return Data.bolts;
    }
    public void addBolts(int amount = 1)
    {
        Data.bolts += amount;
        SaveSystem.Save();
    }
    public void subtractBolts(int amount = 1)
    {
        Data.bolts = Mathf.Max(0, Data.bolts - amount);
        SaveSystem.Save();
    }

    public int getChancesCount()
    {
        return Data.chances;
    }
    public void addChances(int amount = 1)
    {
        Data.chances += amount;
        SaveSystem.Save();
    }
    public void subtractChances(int amount = 1)
    {
        Data.chances = Mathf.Max(0, Data.chances - amount);
        SaveSystem.Save();
    }

    public int getBoxesCount()
    {
        return Data.mysteryBoxes;
    }
    public void addBoxes(int amount = 1)
    {
        Data.mysteryBoxes += amount;
        SaveSystem.Save();
    }
    public void subtractBoxes(int amount = 1)
    {
        Data.mysteryBoxes = Mathf.Max(0, Data.mysteryBoxes - amount);
        SaveSystem.Save();
    }

    // ================================================================== progress

    public int getHighScore()
    {
        return Data.highScore;
    }
    public void setHighScore(int score)
    {
        Data.highScore = score;
        SaveSystem.Save();
    }

    public int getTimesPlayed()
    {
        return Data.timesPlayed;
    }
    public void addTimesPlayed()
    {
        Data.timesPlayed++;
        SaveSystem.Save();
    }

    // ================================================================== upgrades

    public int getDoublePointsLevel()
    {
        return Data.doublePointsLevel;
    }
    public void setDoublePointsLevel(int level)
    {
        Data.doublePointsLevel = level;
        SaveSystem.Save();
    }

    public int getBoltLevel()
    {
        return Data.boltLevel;
    }
    public void setBoltLevel(int level)
    {
        Data.boltLevel = level;
        SaveSystem.Save();
    }

    public int getChanceLevel()
    {
        return Data.chanceLevel;
    }
    public void setChanceLevel(int level)
    {
        Data.chanceLevel = level;
        SaveSystem.Save();
    }

    // ================================================================== cosmetics
    //
    // New in P2-04. Ownership used to be one PlayerPrefs key per item (Ball_1 .. Ball_72,
    // Floor_1 .., FloorPattern_101 ..) tested with PlayerPrefs.HasKey - so ANY value, including
    // zero, counted as owned. It is now an explicit list, and MaterialsManager delegates here
    // rather than touching storage itself.
    //
    // Ball IDs and colour-floor IDs start at 1; pattern-floor IDs start at 101. The two spaces
    // never collide, so one list per category replaces the old dual-key lookup for floors.

    public int getSelectedBallId()
    {
        return Data.selectedBallId;
    }
    public void setSelectedBallId(int id)
    {
        Data.selectedBallId = id;
        unlockBall(id);
        SaveSystem.Save();
    }

    public int getSelectedFloorId()
    {
        return Data.selectedFloorId;
    }
    public void setSelectedFloorId(int id)
    {
        Data.selectedFloorId = id;
        unlockFloor(id);
        SaveSystem.Save();
    }

    public bool isBallOwned(int id)
    {
        return Data.ownedBallIds.Contains(id);
    }
    public void unlockBall(int id)
    {
        if (Data.ownedBallIds.Contains(id))
            return;
        Data.ownedBallIds.Add(id);
        SaveSystem.Save();
    }

    public bool isFloorOwned(int id)
    {
        return Data.ownedFloorIds.Contains(id);
    }
    public void unlockFloor(int id)
    {
        if (Data.ownedFloorIds.Contains(id))
            return;
        Data.ownedFloorIds.Add(id);
        SaveSystem.Save();
    }

    /// <summary>Owned ball count. Feeds the "12 of 72" collection surfacing in P6-02.</summary>
    public int getOwnedBallCount()
    {
        return Data.ownedBallIds.Count;
    }

    /// <summary>Owned floor count, colour and pattern floors combined.</summary>
    public int getOwnedFloorCount()
    {
        return Data.ownedFloorIds.Count;
    }

    // ================================================================== entitlements

    public bool isAdEnabled()
    {
        return !Data.adsRemoved;
    }
    public void setAdsEnabled(bool on)
    {
        Data.adsRemoved = !on;
        SaveSystem.Save();
    }

    // ================================================================== player preferences
    // Stored in the save file rather than PlayerPrefs: a returning player on a new device
    // should not be re-tutorialised or have their control scheme reset.

    public bool isTutorialsOn()
    {
        return Data.tutorialsOn;
    }
    public void setTutorialsState(bool on)
    {
        Data.tutorialsOn = on;
        SaveSystem.Save();
    }

    public GamePlayMode getGamePlayMode()
    {
        // The old implementation used Enum.Parse, which throws on anything unexpected - a
        // corrupted or hand-edited value would take the game down. TryParse falls back instead.
        GamePlayMode parsed;
        if (System.Enum.TryParse(Data.gamePlayMode, out parsed) && System.Enum.IsDefined(typeof(GamePlayMode), parsed))
            return parsed;

        return GamePlayMode.SingleTap;
    }
    public void setGamePlayMode(GamePlayMode gamePlayMode)
    {
        Data.gamePlayMode = gamePlayMode.ToString();
        SaveSystem.Save();
    }

    // ================================================================== device-local settings
    // These stay in PlayerPrefs on purpose. They describe the device, not the player, and
    // should NOT sync when cloud save lands in P7-02.

    public bool isAutoPilotOn()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_AUTO_PILOT_ON, 0) == 1;
    }
    public void setAutoPilotState(bool on)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_AUTO_PILOT_ON, on ? 1 : 0);
    }

    public bool isStayInSpaceOn()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_STAY_IN_SPACE_ON, 1) == 1;
    }
    public void setStayInSpaceState(bool on)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_STAY_IN_SPACE_ON, on ? 1 : 0);
    }

    public bool isEmissionOn()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_EMISSION_ON, 1) == 1;
    }
    public void setEmissionState(bool on)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_EMISSION_ON, on ? 1 : 0);
    }

    public float getMusicLevel()
    {
        return PlayerPrefs.GetFloat(Utility.Constants.KEY_MUSIC_LEVEL, 0.5f);
    }
    public void setMusicLevel(float value)
    {
        PlayerPrefs.SetFloat(Utility.Constants.KEY_MUSIC_LEVEL, value);
    }

    public float getAmbientLevel()
    {
        return PlayerPrefs.GetFloat(Utility.Constants.KEY_AMBIENT_LEVEL, 1);
    }
    public void setAmbientLevel(float value)
    {
        PlayerPrefs.SetFloat(Utility.Constants.KEY_AMBIENT_LEVEL, value);
    }

    public float getSfxLevel()
    {
        return PlayerPrefs.GetFloat(Utility.Constants.KEY_SFX_LEVEL, 1);
    }
    public void setSfxLevel(float value)
    {
        PlayerPrefs.SetFloat(Utility.Constants.KEY_SFX_LEVEL, value);
    }

    public float getMenusLevel()
    {
        return PlayerPrefs.GetFloat(Utility.Constants.KEY_MENUS_LEVEL, 1);
    }
    public void setMenusLevel(float value)
    {
        PlayerPrefs.SetFloat(Utility.Constants.KEY_MENUS_LEVEL, value);
    }

    /// <summary>
    /// NOTE: the default of refreshRate - 20 is non-zero on every device, and GraphicsManager
    /// treats any value above zero as "uncapped", so a fresh install renders unthrottled.
    /// That is the P4-04 bug. Behaviour is preserved verbatim here on purpose - fixing it
    /// belongs to that ticket, not to the save-layer rewrite.
    /// </summary>
    public int getFramesLimit()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_FRAMES_LIMIT, Screen.currentResolution.refreshRate - 20);
    }
    public void setFramesLimit(int limit)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_FRAMES_LIMIT, limit);
    }

    public float getDensityLevel()
    {
        return PlayerPrefs.GetFloat(Utility.Constants.KEY_DENSITY_LEVEL, 1);
    }
    public void setDensityLevel(float level)
    {
        PlayerPrefs.SetFloat(Utility.Constants.KEY_DENSITY_LEVEL, level);
    }

    public bool isRealOrbitOn()
    {
        return false;
    }

    // ================================================================== remote config cache
    // Populated by MainMenu.FetchData() from abusaada.com/rising/version.php. Disposable, so
    // PlayerPrefs is the right home. See P8-03 - that endpoint is currently a dead domain and
    // the social-link write-through is a hijack risk while it stays unregistered.

    public int getUpdateVersion()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_UPDATE_VERSION, 0);
    }
    public void setUpdateVersion(int version)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_UPDATE_VERSION, version);
    }

    public int getForceUpdateVersion()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_FORCE_UPDATE_VERSION, 0);
    }
    public void setForceUpdateVersion(int version)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_FORCE_UPDATE_VERSION, version);
    }

    public string getFacebookLink()
    {
        return PlayerPrefs.GetString(Utility.Constants.KEY_FACEBOOK_LINK, "https://www.facebook.com/profile.php?id=61555506035327");
    }
    public void setFacebookLink(string link)
    {
        PlayerPrefs.SetString(Utility.Constants.KEY_FACEBOOK_LINK, link);
    }

    public string getYoutubeLink()
    {
        return PlayerPrefs.GetString(Utility.Constants.KEY_YOUTUBE_LINK, "https://www.youtube.com/@MahmoudAbuSada");
    }
    public void setYoutubeLink(string link)
    {
        PlayerPrefs.SetString(Utility.Constants.KEY_YOUTUBE_LINK, link);
    }

    public string getInstagramLink()
    {
        return PlayerPrefs.GetString(Utility.Constants.KEY_INSTAGRAM_LINK, "https://www.instagram.com/risingway.abusada");
    }
    public void setInstagramLink(string link)
    {
        PlayerPrefs.SetString(Utility.Constants.KEY_INSTAGRAM_LINK, link);
    }

    public string getXLink()
    {
        return PlayerPrefs.GetString(Utility.Constants.KEY_X_LINK, "https://twitter.com/RisingWayMobile");
    }
    public void setXLink(string link)
    {
        PlayerPrefs.SetString(Utility.Constants.KEY_X_LINK, link);
    }
}
