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
        pathMaker.resetPathValues();
        scoreManager.resetScore();
        playerTrigger.stopCoroutines();
        if (showMainMenu)
        {
            menusController.hideStackMenus();
            menusController.showAndAddMenuToStack(Menus.MainMenu);
        }
        ambientEffectsController.setNightSkyColor();
        ambientEffectsController.onReplay();
    }

    public void StartGame()
    {
        if (Utility.startClicked || UIFader.isFadingIn || pathMaker.isDestroyingOldPath)
            return;

        Utility.startClicked = true;
        menusController.hideStackMenus();
        menusController.showAndAddMenuToStack(Menus.InGameUI);
        if (!PlayerStats.Instance.isStayInSpaceOn())
            ambientEffectsController.setDaySkyColor();
        ambientEffectsController.onGameStarted();
        playerStats.addTimesPlayed();
        GameAnalytics.RunStarted(playerStats.getTimesPlayed(), playerStats.isTutorialsOn(), playerStats.getGamePlayMode().ToString());
        cameraController.resetFov();
        scoreManager.initScoreManager();
        pickUpsManager.initPickupsManager();
        playerMovement.movePlayerToPosition();
        SoundManager.Instance.removeFilter();
        FindObjectOfType<PlayerFall>().numberOfRevives = 0;
        AdmobManager.Instance.LoadReviveAd();
    }
}
