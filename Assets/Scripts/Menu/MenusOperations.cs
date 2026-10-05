using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenusOperations : MonoBehaviour
{
    private CameraController cameraController;
    private PlayerMovement playerMovement;
    private PathMaker pathMaker;
    private MenusController menusController;
    private ScoreManager scoreManager;
    private PlayerStats playerStats;
    private PlayerTrigger playerTrigger;
    private PickUpsManager pickUpsManager;
    private AmbientEffectsController ambientEffectsController;

    void Awake()
    {
        cameraController = FindObjectOfType<CameraController>();
        playerMovement = FindObjectOfType<PlayerMovement>();
        pathMaker = FindObjectOfType<PathMaker>();
        menusController = FindObjectOfType<MenusController>();
        scoreManager = FindObjectOfType<ScoreManager>();
        playerStats = PlayerStats.Instance;
        playerTrigger = FindObjectOfType<PlayerTrigger>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        ambientEffectsController = FindObjectOfType<AmbientEffectsController>();

        if (!Debug.isDebugBuild)
        {
            playerStats.setAutoPilotState(false);
        }
    }

    public void resetGame(bool showMainMenu = true)
    {
        Time.timeScale = 1.5f;
        Utility.resetFlags();
        cameraController.resetCameraPosition();
        playerMovement.resetPlayerValues();
        FindObjectOfType<PlayerFall>().resetFall();
        pickUpsManager.clearForNewRun();
        pathMaker.resetPathValues();
        scoreManager.resetScore();
        playerTrigger.stopCoroutines();
        if (showMainMenu)
        {
            menusController.hideStackMenus();
            menusController.showAndAddMenuToStack(Menus.MainMenu);
        }
        ambientEffectsController.onReplay();
    }

    public void StartGame()
    {
        if (Utility.startClicked || UIFader.isFadingIn || pathMaker.isDestroyingOldPath)
            return;

        Utility.startClicked = true;
        GameMode.BeginRun(); // the run plays the mode chosen now, whatever happens in the menus
        menusController.hideStackMenus();
        menusController.showAndAddMenuToStack(Menus.InGameUI);
        ambientEffectsController.onGameStarted();
        playerStats.addTimesPlayed();
        GameAnalytics.RunStarted(playerStats.getTimesPlayed(), playerStats.isTutorialsOn(), playerStats.getGamePlayMode().ToString());
        cameraController.resetFov();
        scoreManager.initScoreManager();
        pickUpsManager.initPickupsManager();
        // Insane opens its harder turn patterns from the first part.
        if (!playerStats.isTutorialsOn())
            pathMaker.unlockPatternsUpTo(GameMode.T.startPatternTier);
        playerMovement.movePlayerToPosition();
        SoundManager.Instance.removeFilter();
        PlayerFall playerFall = FindObjectOfType<PlayerFall>();
        playerFall.numberOfRevives = 0;
        playerFall.runStarted();
        AdmobManager.Instance.LoadReviveAd();
    }
}
