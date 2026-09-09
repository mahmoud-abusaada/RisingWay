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
    private int pathUpdateScore6 = 600;

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

        if (score > pathUpdateScore1 && score < pathUpdateScore2 && !pathMaker.landPatterns.Contains("R-S-R"))
        {
            Debug.Log("Path maker update 1");
            pathMaker.landPatterns.Add("R-S-R", new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandRight });
            pathMaker.landPatterns.Add("L-S-L", new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandLeft });
            pathMaker.landPatterns.Add("R-S-L", new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandLeft });
            pathMaker.landPatterns.Add("L-S-R", new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandRight });
        }
        else if (score > pathUpdateScore2 && score < pathUpdateScore3 && !pathMaker.landPatterns.Contains("R-S-R-S-R"))
        {
            Debug.Log("Path maker update 2");
            pathMaker.landPatterns.Add("R-S-R-S-R", new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandRight, Parts.LandStraight, Parts.LandRight });
            pathMaker.landPatterns.Add("L-S-L-S-L", new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandLeft, Parts.LandStraight, Parts.LandLeft });
            pathMaker.landPatterns.Add("R-S-R-S-L", new ArrayList { Parts.LandRight, Parts.LandStraight, Parts.LandRight, Parts.LandStraight, Parts.LandLeft });
            pathMaker.landPatterns.Add("L-S-L-S-R", new ArrayList { Parts.LandLeft, Parts.LandStraight, Parts.LandLeft, Parts.LandStraight, Parts.LandRight });
        }
        else if (score > pathUpdateScore3 && score < pathUpdateScore4 && !pathMaker.landPatterns.Contains("R-R"))
        {
            Debug.Log("Path maker update 3");
            pathMaker.landPatterns.Remove("R-S-R");
            pathMaker.landPatterns.Remove("L-S-L");
            pathMaker.landPatterns.Remove("R-S-L");
            pathMaker.landPatterns.Remove("L-S-R");
            pathMaker.landPatterns.Add("R-R", new ArrayList { Parts.LandRight, Parts.LandRight });
            pathMaker.landPatterns.Add("L-L", new ArrayList { Parts.LandLeft, Parts.LandLeft });
            pathMaker.landPatterns.Add("R-L", new ArrayList { Parts.LandRight, Parts.LandLeft });
            pathMaker.landPatterns.Add("L-R", new ArrayList { Parts.LandLeft, Parts.LandRight });
        }
        else if (score > pathUpdateScore4 && score < pathUpdateScore5 && !pathMaker.landPatterns.Contains("R-R-S-R"))
        {
            Debug.Log("Path maker update 4");
            pathMaker.landPatterns.Add("R-R-S-R", new ArrayList { Parts.LandRight, Parts.LandRight, Parts.LandStraight, Parts.LandRight });
            pathMaker.landPatterns.Add("L-L-S-L", new ArrayList { Parts.LandLeft, Parts.LandLeft, Parts.LandStraight, Parts.LandLeft });
        }
        else if (score > pathUpdateScore5 && score < pathUpdateScore6 && !pathMaker.landPatterns.Contains("R-R-L"))
        {
            Debug.Log("Path maker update 5");
            // pathMaker.landPatterns.Remove("R-S-R-S-R");
            // pathMaker.landPatterns.Remove("L-S-L-S-L");
            // pathMaker.landPatterns.Remove("R-S-R-S-L");
            // pathMaker.landPatterns.Remove("L-S-L-S-R");
            pathMaker.landPatterns.Add("R-R-L", new ArrayList { Parts.LandRight, Parts.LandRight, Parts.LandLeft });
            pathMaker.landPatterns.Add("L-L-R", new ArrayList { Parts.LandLeft, Parts.LandLeft, Parts.LandRight });
            pathMaker.landPatterns.Add("R-L-R", new ArrayList { Parts.LandRight, Parts.LandLeft, Parts.LandRight });
            pathMaker.landPatterns.Add("L-R-L", new ArrayList { Parts.LandLeft, Parts.LandRight, Parts.LandLeft });
        }
    }
}
