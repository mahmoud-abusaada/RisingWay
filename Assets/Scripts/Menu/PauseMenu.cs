using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI diamondsCollectedText;
    [SerializeField] private ConfirmationDialog confirmationDialog;

    private ScoreManager scoreManager;
    private PickUpsManager pickUpsManager;
    private GameOverMenu gameOverMenu;
    private PlayerFall playerFall;
    private MenusController menusController;
    private MenusOperations menusOperations;
    private PathMaker pathMaker;
    private float timeScaleBeforePause = 0;
    private bool isResumeClicked = false;
    private bool isRestartClicked = false;
    private bool isHomeClicked = false;

    void Awake()
    {
        scoreManager = FindObjectOfType<ScoreManager>();
        pickUpsManager = FindObjectOfType<PickUpsManager>();
        gameOverMenu = FindObjectOfType<GameOverMenu>();
        playerFall = FindObjectOfType<PlayerFall>();
        menusController = FindObjectOfType<MenusController>();
        menusOperations = FindObjectOfType<MenusOperations>();
        pathMaker = FindObjectOfType<PathMaker>();
    }

    void OnEnable()
    {
        diamondsCollectedText.text = Utility.getFormatedNumber(scoreManager.getDiamondsCollected());
        scoreText.text = scoreManager.getScore().ToString();
        Utility.isGamePaused = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0;
        isResumeClicked = false;
        isRestartClicked = false;
        isHomeClicked = false;
        SoundManager.Instance.PauseSounds();
    }

    public void ResumeGame()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        if (isResumeClicked || isRestartClicked || isHomeClicked)
            return;
        isResumeClicked = true;
        Utility.isGamePaused = false;
        Time.timeScale = timeScaleBeforePause;
        menusController.hideStackMenus();
        menusController.showAndAddMenuToStack(Menus.InGameUI);
        SoundManager.Instance.ResumeSounds();
    }

    public void RestartGame()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        if (isRestartClicked || isResumeClicked || isHomeClicked)
            return;
        isRestartClicked = true;
        pathMaker.startDestroyingOldPath();
        pickUpsManager.clearSpawnedPickups();
        menusOperations.resetGame(false);
        menusOperations.StartGame();
    }

    public void HomeMenu()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        if (isHomeClicked || isResumeClicked || isRestartClicked)
            return;
        isHomeClicked = true;
        confirmationDialog.setConfirmationDialog("Confirmation", "Are you sure you want to exit?", true, () =>
        {
            if (!MultiClickHandler.Instance.CanClick()) return;

            pathMaker.startDestroyingOldPath();
            pickUpsManager.clearSpawnedPickups();
            pickUpsManager.clearActivePickups(false);
            SoundManager.Instance.StopSounds();
            menusOperations.resetGame();
        }, () =>
        {
            isHomeClicked = false;
        });
    }
}
