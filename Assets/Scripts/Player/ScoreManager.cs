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
        inGameUI.updateScoreText();
        currentPlayerSpeed = Utility.Constants.START_PLAYER_SPEED;
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

    public void diamondPicked()
    {
        playerStats.addDiamonds();
        diamondsCollected++;
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
        currentPlayerSpeed = (score / 600f * (Utility.Constants.TOP_PLAYER_SPEED - Utility.Constants.START_PLAYER_SPEED)) + Utility.Constants.START_PLAYER_SPEED;
        if (currentPlayerSpeed > Utility.Constants.TOP_PLAYER_SPEED)
            currentPlayerSpeed = Utility.Constants.TOP_PLAYER_SPEED;

        if (!Utility.boltIsOn && !PlayerStats.Instance.isTutorialsOn())
            playerMovement.speed = currentPlayerSpeed;

        // The turn patterns themselves are listed in PathMaker.unlockPatternsUpTo.
        pathMaker.unlockPatternsUpTo(patternTierFor(score));
    }

    private int patternTierFor(int score)
    {
        if (score > pathUpdateScore5) return 5;
        if (score > pathUpdateScore4) return 4;
        if (score > pathUpdateScore3) return 3;
        if (score > pathUpdateScore2) return 2;
        if (score > pathUpdateScore1) return 1;
        return 0;
    }
}
