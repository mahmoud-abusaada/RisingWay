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
        if (Utility.gameStarted && !IsGrounded() && !Utility.spawningAfterChance && Utility.camFollowPlayer && !pathMaker.isDestroyingOldPath)
        {
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
                startRespawn();
            }
            if (!Utility.chanceIsOn && shouldShowRevive())
            {
                menusController.hideStackMenus();
                menusController.showAndAddMenuToStack(Menus.ReviveMenu);
            }
        }
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

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + new Vector3(radiusToCheck, 0, 0), -Vector3.up, distToGround) ||
               Physics.Raycast(transform.position + new Vector3(-radiusToCheck, 0, 0), -Vector3.up, distToGround) ||
               Physics.Raycast(transform.position + new Vector3(0, 0, radiusToCheck), -Vector3.up, distToGround) ||
               Physics.Raycast(transform.position + new Vector3(0, 0, -radiusToCheck), -Vector3.up, distToGround);
    }

    private bool shouldShowRevive()
    {
        return scoreManager.getScore() >= Utility.Constants.SCORE_LIMIT_TO_SHOW_REVIVE && adsManager.CanShowReviveAd() && numberOfRevives < Utility.Constants.MAX_REVIVES;
    }
}
