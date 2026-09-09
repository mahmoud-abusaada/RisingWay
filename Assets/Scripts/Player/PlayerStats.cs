using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public int getDiamondsCount()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_DIAMONDS, 0);
    }
    public void addDiamonds(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_DIAMONDS, getDiamondsCount() + amount);
    }
    public void subtractDiamonds(int amount)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_DIAMONDS, getDiamondsCount() - amount);
    }
    public int getDoublePointsCount()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_DOUBLE_POINTS, 0);
    }
    public void addDoublePoints(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_DOUBLE_POINTS, getDoublePointsCount() + amount);
    }
    public void subtractDoublePoints(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_DOUBLE_POINTS, getDoublePointsCount() - amount);
    }
    public int getBoltsCount()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_BOLTS, 0);
    }
    public void addBolts(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_BOLTS, getBoltsCount() + amount);
    }
    public void subtractBolts(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_BOLTS, getBoltsCount() - amount);
    }
    public int getChancesCount()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_CHANCES, 0);
    }
    public void addChances(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_CHANCES, getChancesCount() + amount);
    }
    public void subtractChances(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_CHANCES, getChancesCount() - amount);
    }
    public int getBoxesCount()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_MYSTERY_BOXES, 0);
    }
    public void addBoxes(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_MYSTERY_BOXES, getBoxesCount() + amount);
    }
    public void subtractBoxes(int amount = 1)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_MYSTERY_BOXES, getBoxesCount() <= 0 ? 0 : getBoxesCount() - amount);
    }
    public int getHighScore()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_HIGH_SCORE, 0);
    }
    public void setHighScore(int score)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_HIGH_SCORE, score);
    }
    public int getTimesPlayed()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_TIMES_PLAYED, 0);
    }
    public void addTimesPlayed()
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_TIMES_PLAYED, getTimesPlayed() + 1);
    }
    public bool isTutorialsOn()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_TUTORIALS_ON, 1) == 1;
    }
    public void setTutorialsState(bool on)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_TUTORIALS_ON, on ? 1 : 0);
    }
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
    public bool isAdEnabled()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_ADS_ENABLED, 1) == 1;
    }
    public void setAdsEnabled(bool on)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_ADS_ENABLED, on ? 1 : 0);
    }
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
    // public void setRealOrbitState(bool on)
    // {
    //     PlayerPrefs.SetInt(Utility.Constants.KEY_STAY_IN_SPACE_ON, on ? 1 : 0);
    // }
    public int getDoublePointsLevel()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_DOUBLE_POINTS_LEVEL, 1);
    }
    public void setDoublePointsLevel(int level)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_DOUBLE_POINTS_LEVEL, level);
    }
    public int getBoltLevel()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_BOLT_LEVEL, 1);
    }
    public void setBoltLevel(int level)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_BOLT_LEVEL, level);
    }
    public int getChanceLevel()
    {
        return PlayerPrefs.GetInt(Utility.Constants.KEY_CHANCE_LEVEL, 1);
    }
    public void setChanceLevel(int level)
    {
        PlayerPrefs.SetInt(Utility.Constants.KEY_CHANCE_LEVEL, level);
    }
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
    public GamePlayMode getGamePlayMode()
    {
        return (GamePlayMode)System.Enum.Parse(typeof(GamePlayMode), PlayerPrefs.GetString(Utility.Constants.KEY_SELECTED_GAMEPLAY_MODE, "SingleTap"));
    }
    public void setGamePlayMode(GamePlayMode gamePlayMode)
    {
        PlayerPrefs.SetString(Utility.Constants.KEY_SELECTED_GAMEPLAY_MODE, gamePlayMode.ToString());
    }
}