using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utility : MonoBehaviour
{

    public class Constants
    {
        // ------------------------------------------------------------------------------
        // RETIRED PlayerPrefs keys - P2-04.
        //
        // Everything in this block moved into the versioned save file (SaveData/SaveSystem)
        // and is no longer read or written anywhere. They are kept, not deleted, because they
        // are the only remaining record of the pre-2024 on-device key names.
        //
        // That matters if the clean-slate decision is ever revisited: reading a legacy save
        // would mean reading exactly these keys, plus one key per owned cosmetic in the form
        // BALL_KEY_NAME + id / FLOOR_KEY_NAME + id / FLOOR_PATTERN_KEY_NAME + id, where
        // ownership was signalled by mere key existence rather than by value.
        //
        // Do not reuse these names for anything new.
        // ------------------------------------------------------------------------------
        public const string KEY_CURRENT_BALL = "key_current_ball";
        public const string KEY_CURRENT_FLOOR = "key_current_floor";
        public const string KEY_DIAMONDS = "key_diamonds";
        public const string KEY_DOUBLE_POINTS = "key_double_points";
        public const string KEY_BOLTS = "key_bolts";
        public const string KEY_CHANCES = "key_chances";
        public const string KEY_MYSTERY_BOXES = "key_mystery_boxes";
        public const string KEY_DOUBLE_POINTS_LEVEL = "key_double_points_level";
        public const string KEY_BOLT_LEVEL = "key_bolt_level";
        public const string KEY_CHANCE_LEVEL = "key_chance_level";
        public const string KEY_HIGH_SCORE = "key_high_score";
        public const string KEY_TIMES_PLAYED = "key_times_played";
        public const string KEY_TUTORIALS_ON = "key_tutorials_on";
        public const string KEY_ADS_ENABLED = "key_ads_enabled";
        public const string KEY_SELECTED_GAMEPLAY_MODE = "key_selected_gameplay_mode";

        // ------------------------------------------------------------------------------
        // Live PlayerPrefs keys - device-local settings and the disposable remote-config
        // cache. These deliberately stay out of the save file so they do not sync when
        // cloud save lands in P7-02.
        // ------------------------------------------------------------------------------
        public const string KEY_AUTO_PILOT_ON = "key_auto_pilot_on";
        public const string KEY_STAY_IN_SPACE_ON = "key_stay_in_space_on";
        public const string KEY_EMISSION_ON = "key_emission_on";
        public const string KEY_MUSIC_LEVEL = "key_music_level";
        public const string KEY_AMBIENT_LEVEL = "key_ambient_level";
        public const string KEY_SFX_LEVEL = "key_sfx_level";
        public const string KEY_MENUS_LEVEL = "key_menus_level";
        public const string KEY_FRAMES_LIMIT = "key_frames_limit";
        public const string KEY_DENSITY_LEVEL = "key_density_level";
        public const string KEY_UPDATE_VERSION = "key_update_version";
        public const string KEY_FORCE_UPDATE_VERSION = "key_force_update_version";
        public const string KEY_FACEBOOK_LINK = "key_facebook_link";
        public const string KEY_YOUTUBE_LINK = "key_youtube_link";
        public const string KEY_INSTAGRAM_LINK = "key_instagram_link";
        public const string KEY_X_LINK = "key_X_link";

        public const string DESTROYING_OBJECT_NAME = "Destroying...";
        public const string BOLT_STRAIGHT_PART_NAME = "LandStraightForBolt";
        public const string BALL_KEY_NAME = "Ball_";
        public const string FLOOR_KEY_NAME = "Floor_";
        public const string FLOOR_PATTERN_KEY_NAME = "FloorPattern_";
        public const string HIGH_SCORE_TEXT = "Highscore: ";
        public const float DEFAULT_TIME_SCALE = 1.2f;
        public const float DEFAULT_FOV = 60;
        public const float TUTORIAL_PLAYER_SPEED = 4;
        public const float START_PLAYER_SPEED = 6;
        public const float TOP_PLAYER_SPEED = 14;
        public const float ROTATION_SPEED = 10;
        public const int MYTERYBOX_RANDOM_SEEKING_TIMES = 25;
        public const int SCORE_LIMIT_TO_SHOW_REVIVE = 10;
        public const int MAX_REVIVES = 3;

        // PickUps
        public const int DOUBLE_POINTS_MAX_LEVEL = 5;
        public const int DOUBLE_POINTS_UPGRADE_PRICE = 10000;
        public const int BOLT_MAX_LEVEL = 5;
        public const int BOLT_UPGRADE_PRICE = 10000;
        public const int CHANCE_MAX_LEVEL = 3;
        public const int CHANCE_UPGRADE_PRICE = 15000;
    }

    public static bool startClicked = false;

    public static bool startGroundCreated = false;

    public static bool playerIsInPosition = false;

    public static bool gameStarted = false;

    public static bool camFollowPlayer = false;

    // P8-03: true only after a remote-config fetch SUCCEEDED this session. Deliberately NOT
    // persisted - a force-update must never be honoured from a cached value, or a dead endpoint
    // keeps gating startup forever. Not cleared by resetFlags(): it describes this app run,
    // not this game run.
    public static bool remoteConfigLoaded = false;

    public static bool stoppedForTutorials = false;

    public static bool shouldDequeue = false;

    public static bool boltIsOn = false;

    public static bool doublePointIsOn = false;

    public static bool chanceIsOn = false;

    public static bool spawningAfterChance = false;
    
    public static bool isGamePaused = false;
    
    public static bool userCanPause = false;

    public static void resetFlags()
    {
        startClicked = false;
        startGroundCreated = false;
        playerIsInPosition = false;
        gameStarted = false;
        camFollowPlayer = false;
        stoppedForTutorials = false;
        boltIsOn = false;
        doublePointIsOn = false;
        chanceIsOn = false;
        spawningAfterChance = false;
        isGamePaused = false;
        userCanPause = false;
    }

    public static void clearAllChilds(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child != null)
                GameObject.Destroy(child.gameObject);
        }
    }

    public static void disableAllChilds(Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (child != null)
                child.gameObject.SetActive(false);
        }
    }

    public static Material decreaseIntensity(Material material)
    {
        return material;
        // Material copyMaterial = new Material(material);
        // Color originalColor = material.GetColor("_EmissionColor");
        // Color newColor = originalColor * 0.15f;
        // copyMaterial.SetColor("_EmissionColor", newColor);
        // return copyMaterial;
    }

    public static string getFormatedNumber(int number)
    {
        return number.ToString("#,##0");
    }

    public static float getDoublePointsPeriod(int level = 0)
    {
        int doublePointsLevel = level;

        if (level == 0)
            doublePointsLevel = PlayerStats.Instance.getDoublePointsLevel();

        if (doublePointsLevel == 1)
        {
            return 5;
        }
        else if (doublePointsLevel == 2)
        {
            return 10;
        }
        else if (doublePointsLevel == 3)
        {
            return 15;
        }
        else if (doublePointsLevel == 4)
        {
            return 20;
        }
        else if (doublePointsLevel == 5)
        {
            return 25;
        }
        else
        {
            return 0;
        }
    }

    public static float getBoltDistance(int level = 0)
    {
        int boltLevel = level;

        if (level == 0)
            boltLevel = PlayerStats.Instance.getBoltLevel();

        if (boltLevel == 1)
        {
            return 25;
        }
        else if (boltLevel == 2)
        {
            return 40;
        }
        else if (boltLevel == 3)
        {
            return 60;
        }
        else if (boltLevel == 4)
        {
            return 80;
        }
        else if (boltLevel == 5)
        {
            return 100;
        }
        else
        {
            return 0;
        }
    }

    public static float getChanceTimes(int level = 0)
    {
        int chanceLevel = level;

        if (level == 0)
            chanceLevel = PlayerStats.Instance.getChanceLevel();

        if (chanceLevel == 1)
        {
            return 1;
        }
        else if (chanceLevel == 2)
        {
            return 2;
        }
        else if (chanceLevel == 3)
        {
            return 3;
        }
        else
        {
            return 0;
        }
    }

    // List<string> planetNames = new List<string>() { "Earth", "Moon"};

    // public static bool isPlanet(Material material)
    // {
    //     string name = material.name;
    //     return name.Contains("Earth")
    // }

}
