using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{

    [SerializeField] private Transform player;
    private InGameUI inGameUI;
    private PlayerStats playerStats;
    private PlayerMovement playerMovement;
    private PathMaker pathMaker;
    private int playerHeight = 0;
    private int score = 0;
    private int diamondsCollected = 0;
    private float currentPlayerSpeed = Utility.Constants.START_PLAYER_SPEED;
    // Chill: the climb since the last fall, and the longest one this run (its stat in place of a best).
    private int streakStartScore = 0;
    private int longestStreak = 0;
    private int pathUpdateScore1 = 110;
    private int pathUpdateScore2 = 200;
    private int pathUpdateScore3 = 300;
    private int pathUpdateScore4 = 400;
    private int pathUpdateScore5 = 500;

    public void initScoreManager()
    {
        inGameUI = FindObjectOfType<InGameUI>();
        playerStats = PlayerStats.Instance;
        playerMovement = FindObjectOfType<PlayerMovement>();
        pathMaker = FindObjectOfType<PathMaker>();
    }

    public void resetScore()
    {
        playerHeight = 0;
        score = 0;
        diamondsCollected = 0;
        streakStartScore = 0;
        longestStreak = 0;
        inGameUI.updateScoreText();
        currentPlayerSpeed = GameMode.T.startSpeed;
    }

    public int getDiamondsCollected()
    {
        return diamondsCollected;
    }

    int difference;
    // Update is called once per frame
    void Update()
    {
        if (Utility.gameStarted && !Utility.spawningAfterChance)
        {
            difference = ((int)player.position.y) - playerHeight;
            if (difference >= 2)
            {
                playerHeight = (int)player.position.y;
                addRisingScore();
            }
        }
    }

    public int getScore()
    {
        return score;
    }

    public int getPlayerHeight()
    {
        return playerHeight;
    }

    public float getCurrentPlayerSpeed()
    {
        return currentPlayerSpeed;
    }

    public void makeScoreBlue()
    {
        inGameUI.makeScoreBlue();
    }

    public void makeScoreYellow()
    {
        inGameUI.makeScoreYellow();
    }

    /// <summary>The ball fell (Chill, where it is lifted back): the climb without a fall starts over.</summary>
    public void fell()
    {
        longestStreak = Mathf.Max(longestStreak, score - streakStartScore);
        streakStartScore = score;
    }

    /// <summary>Chill's stat: the longest climb this run without falling (the current one counts).</summary>
    public int getLongestStreak()
    {
        return Mathf.Max(longestStreak, score - streakStartScore);
    }

    public void diamondPicked()
    {
        // Insane pays double (GameMode.diamondValue).
        int value = GameMode.T.diamondValue;
        for (int i = 0; i < value; i++)
            playerStats.addDiamonds();
        diamondsCollected += value;
        addDiamondScore();
    }

    private void addRisingScore()
    {
        if(PlayerStats.Instance.isTutorialsOn()) return;

        score += Utility.doublePointIsOn ? 2 : 1;
        inGameUI.playPlus1Effect();
        inGameUI.updateScoreText();
        handleDifficallity();
    }

    private void addDiamondScore()
    {
        if(PlayerStats.Instance.isTutorialsOn()) return;
        
        score += Utility.doublePointIsOn ? 4 : 2;
        inGameUI.playPlus2Effect();
        inGameUI.updateScoreText();
        inGameUI.updateDiamondsText();
        handleDifficallity();
    }

    private void handleDifficallity()
    {
        currentPlayerSpeed = GameMode.SpeedAt(score); // start to top speed over the mode's ramp

        if (!Utility.boltIsOn && !PlayerStats.Instance.isTutorialsOn())
            playerMovement.speed = currentPlayerSpeed;

        // The turn patterns themselves are listed in PathMaker.unlockPatternsUpTo.
        pathMaker.unlockPatternsUpTo(patternTierFor(score));
    }

    public int patternTierFor(int score)
    {
        // Each mode opens tiers at its own scores (Insane at half) and never past its last one,
        // and none below the one it starts at.
        GameMode.Tuning t = GameMode.T;
        float s = score / t.patternScoreScale;
        int tier = s > pathUpdateScore5 ? 5 : s > pathUpdateScore4 ? 4 : s > pathUpdateScore3 ? 3 : s > pathUpdateScore2 ? 2 : s > pathUpdateScore1 ? 1 : 0;
        return Mathf.Clamp(tier, t.startPatternTier, t.maxPatternTier);
    }
}
