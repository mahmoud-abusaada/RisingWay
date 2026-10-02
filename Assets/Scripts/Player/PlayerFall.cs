using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFall : MonoBehaviour
{

    private float distToGround = 5; // Distance between the player and the ground
    private float radiusToCheck = 0.4f;
    private MenusController menusController;
    private ScoreManager scoreManager;
    private PathMaker pathMaker;
    private PickUpsManager pickUpsManager;
    private PlayerMovement playerMovement;
    private InGameUI inGameUI;
    private AdmobManager adsManager;
    private bool checkForHeightDelta = false;
    private bool reviveAdLoaded = false;
    public int numberOfRevives = 0;

    void Awake()
    {
        menusController = FindObjectOfType<MenusController>();
        scoreManager = FindObjectOfType<ScoreManager>();
        pathMaker = FindObjectOfType<PathMaker>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        inGameUI = FindObjectOfType<InGameUI>();
        adsManager = FindObjectOfType<AdmobManager>();
    }

    void Update()
    {
        // Not held back while an old path is still being taken down: the ball is back on the new
        // path by then, and a fall there has to count. It used to be, and a fall right after a
        // revive went unnoticed - the camera following the ball down - until the old path was gone.
        // camFollowPlayer and spawningAfterChance already cover the time the ball is being carried back.
        if (Utility.gameStarted && !IsGrounded() && !Utility.spawningAfterChance && Utility.camFollowPlayer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PlayerMovement.DebugMovement)
                logRays();
#endif
            playerFell();
        }

        if (checkForHeightDelta && scoreManager.getPlayerHeight() - transform.position.y > 25)
        {
            checkForHeightDelta = false;
            transform.GetComponent<Rigidbody>().useGravity = false;
            transform.GetComponent<Rigidbody>().isKinematic = true;
            transform.localRotation = Quaternion.Euler(0, 0, 15);
            if (Utility.chanceIsOn && !Utility.spawningAfterChance)
            {
                GameAnalytics.Revived("chance");
                startRespawn();
            }
            if (!Utility.chanceIsOn && shouldShowRevive())
            {
                GameAnalytics.ReviveOffered(scoreManager.getScore());
                menusController.hideStackMenus();
                menusController.showAndAddMenuToStack(Menus.ReviveMenu);
            }
        }
    }

    /// <summary>
    /// The run is over (Home, Restart): forget a fall still being watched, or the "fallen far
    /// enough" check above would respawn the ball or offer a revive from the main menu.
    /// </summary>
    public void resetFall()
    {
        checkForHeightDelta = false;
    }

    public void startRespawn()
    {
        if (inGameUI == null)
            inGameUI = FindObjectOfType<InGameUI>();
        pathMaker.createPathParent();
        playerMovement.respawnForChance();
        GetComponent<PlayerLinkedObjectsController>().PlaySpawningEffect();
        if (playerMovement.timesRespawnedAfterChancePickedUp == Utility.getChanceTimes() - 1)
            inGameUI.removePickedPickUp(PickUpType.Chance);
    }

    private int numberOfLosesAfterAd = 0;
    public void endGame()
    {
        pickUpsManager.clearActivePickups();
        Utility.resetFlags();

        // P2-06: end of a run is the natural flush point. Diamonds picked up during play are
        // deferred (see PlayerStats.addDiamonds) to avoid a disk write per pickup mid-run, so
        // without this they would only reach disk on backgrounding or quit. An interstitial may
        // be about to take over the process, which is exactly when we want the write already done.
        PlayerStats.Instance.Flush();

        // Before GameOverMenu, which is where a new high score gets written.
        int score = scoreManager.getScore();
        GameAnalytics.RunEnded(score, PlayerStats.Instance.getHighScore(), scoreManager.getDiamondsCollected(), scoreManager.patternTierFor(score));

        numberOfLosesAfterAd++;
        if (numberOfLosesAfterAd % Random.Range(4, 7) == 0 || numberOfLosesAfterAd > 7)
        {
            numberOfLosesAfterAd = 0;
            adsManager.ShowInterstitialAd(() =>
            {
                StartCoroutine(showGameOverAfterDelay());
            });
        }
        else
        {
            menusController.hideStackMenus();
            menusController.showAndAddMenuToStack(Menus.GameOverMenu);
        }
    }

    private IEnumerator showGameOverAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.4f);
        menusController.hideStackMenus();
        menusController.showAndAddMenuToStack(Menus.GameOverMenu);
    }

    public void showInGameUi()
    {
        menusController.hideStackMenus();
        menusController.showAndAddMenuToStack(Menus.InGameUI);
    }

    public void playerFell()
    {
        if (!Utility.chanceIsOn && !shouldShowRevive())
        {
            endGame();
        }
        Utility.camFollowPlayer = false;
        pathMaker.startDestroyingOldPath();
        pickUpsManager.clearSpawnedPickups();
        checkForHeightDelta = true;
        SoundManager.Instance.setFilter();
    }

    // Whether there is track under the ball: four rays straight down, any one finding solid track
    // that is still in place.
    //
    // It used to accept any collider. The project's physics queries hit triggers, and a track part
    // keeps its trigger (and shows its end blocks) while it falls away after being knocked down -
    // so a ball falling together with the parts that had been under it counted as grounded, and
    // the run did not end: the camera followed it down for seconds. Now triggers never count, and
    // neither does anything belonging to a part that is falling away.
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    private bool IsGrounded()
    {
        return trackBelow(new Vector3(radiusToCheck, 0, 0)) ||
               trackBelow(new Vector3(-radiusToCheck, 0, 0)) ||
               trackBelow(new Vector3(0, 0, radiusToCheck)) ||
               trackBelow(new Vector3(0, 0, -radiusToCheck));
    }

    private bool trackBelow(Vector3 offset)
    {
        int n = Physics.RaycastNonAlloc(transform.position + offset, -Vector3.up, groundHits, distToGround,
                                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!isFallingAway(groundHits[i].collider.transform))
                return true;
        return false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void logRays()
    {
        foreach (Vector3 offset in new[] { new Vector3(radiusToCheck, 0, 0), new Vector3(-radiusToCheck, 0, 0),
                                           new Vector3(0, 0, radiusToCheck), new Vector3(0, 0, -radiusToCheck) })
        {
            int n = Physics.RaycastNonAlloc(transform.position + offset, -Vector3.up, groundHits, distToGround,
                                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            string line = "[Fall] ray at " + (transform.position + offset).ToString("F2") + ": " + n + " hits";
            for (int i = 0; i < n; i++)
                line += "  " + groundHits[i].collider.name + "/" + (groundHits[i].collider.transform.parent != null ? groundHits[i].collider.transform.parent.name : "-") +
                        " d=" + groundHits[i].distance.ToString("F2") + " away=" + isFallingAway(groundHits[i].collider.transform);
            Debug.Log(line);
        }
    }
#endif

    // Destroyer.destroy() renames the part it knocks down; the collider may sit a level or two below it.
    private static bool isFallingAway(Transform t)
    {
        for (int depth = 0; t != null && depth < 4; depth++, t = t.parent)
            if (t.name == Utility.Constants.DESTROYING_OBJECT_NAME)
                return true;
        return false;
    }

    private bool shouldShowRevive()
    {
        return scoreManager.getScore() >= Utility.Constants.SCORE_LIMIT_TO_SHOW_REVIVE && adsManager.CanShowReviveAd() && numberOfRevives < Utility.Constants.MAX_REVIVES;
    }
}
