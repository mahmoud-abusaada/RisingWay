using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MenusController : MonoBehaviour
{

    [SerializeField] private Transform player;
    [SerializeField] private Transform moonsParent;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Canvas mainMenu;
    [SerializeField] private Canvas shopMenu;
    [SerializeField] private Canvas diamondsShopMenu;
    [SerializeField] private Canvas settingsMenu;
    [SerializeField] private Canvas gameOverMenu;
    [SerializeField] private Canvas inGameUI;
    [SerializeField] private Canvas purchaseMenu;
    [SerializeField] private Canvas upgradeMenu;
    [SerializeField] private Canvas reviveMenu;
    [SerializeField] private Canvas pauseMenu;
    [SerializeField] private ConfirmationDialog confirmationDialog;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera canvasCamera;

    private Stack<Menus> menusStack = new Stack<Menus>();
    private AdmobManager adsManager;
    private bool cameraSwitched = false;

    // P2-01: Awake() used to call PlayerStats.Instance.setAdsEnabled(true) here, which wiped the
    // "remove_ads" non-consumable on every launch. isAdEnabled() is the only gate on every ad in
    // the game, so a paying player saw ads again the next time they opened the app - permanently,
    // with no way to fix it themselves.
    // The call was never needed: isAdEnabled() already defaults to enabled via
    // PlayerPrefs.GetInt(KEY_ADS_ENABLED, 1), so fresh installs still get ads without it.
    // Do not reintroduce.

    void Start()
    {
        // hideAllMenus(); // Because it has fade animation we don't want to use it here
        // mainMenu.gameObject.SetActive(false);
        // shopMenu.gameObject.SetActive(false);
        // diamondsShopMenu.gameObject.SetActive(false);
        // settingsMenu.gameObject.SetActive(false);
        // gameOverMenu.gameObject.SetActive(false);
        // inGameUI.gameObject.SetActive(false);
        // purchaseMenu.gameObject.SetActive(false);
        // upgradeMenu.gameObject.SetActive(false);
        adsManager = FindAnyObjectByType<AdmobManager>();
        adsManager.LoadBannerAd();
        adsManager.LoadInterstitialAd();
        if (PlayerStats.Instance.getBoxesCount() == 0)
            adsManager.LoadMysteryBoxAd();
        adsManager.LoadReviveAd();
        applyTheme();
        showAndAddMenuToStack(Menus.MainMenu);
    }

    // The Nebula look on every menu without a skin of its own (NebulaSkin), before any is shown.
    private void applyTheme()
    {
        foreach (Canvas menu in new[] { shopMenu, purchaseMenu, diamondsShopMenu })
            if (menu != null)
                NebulaSkin.Apply(menu.transform, true);
        foreach (Canvas menu in new[] { pauseMenu, gameOverMenu, reviveMenu })
            if (menu != null)
                NebulaSkin.Apply(menu.transform, true);
        if (confirmationDialog != null)
            NebulaSkin.Apply(confirmationDialog.transform, false);
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.name == "UpdateDialog")
                NebulaSkin.Apply(c.transform, false);
            // 3D icons in the menus seen square on (FacesCamera), not from the side; a sound for
            // every control without one (UiSound).
            if (c.isRootCanvas)
            {
                FacesCamera.AddUnder(c.transform);
                UiSound.AddUnder(c.transform);
            }
        }
    }

    // A second back press on the main menu within this many seconds exits the game.
    private const float EXIT_PRESS_WINDOW = 2f;
    private float lastBackOnMainMenu = -10f;
    private const float BACK_REPEAT_SECONDS = 0.3f;
    private float lastBackPress = -10f;

    /// <summary>
    /// Android's back button (InputManager). It does what the screen's own back or cancel does:
    /// a dialog closes; in a run the game pauses, and back on the pause menu resumes; the revive
    /// offer is declined; game over goes to the main menu; the mystery box reveal skips its spin,
    /// then collects; other menus go back. On the main menu the first press says to press again,
    /// and a second within two seconds exits.
    /// </summary>
    public void handleSystemBack()
    {
        Debug.Log("[Back] on " + (menusStack.Count > 0 ? menusStack.Peek().ToString() : "nothing") +
                  (UIFader.isFadingIn || UIFader.isFadingOut ? " (fading, ignored)" : "") +
                  (Time.unscaledTime - lastBackPress < BACK_REPEAT_SECONDS ? " (repeat, ignored)" : ""));
        // One press is one step back. Some phones' back gestures arrive twice in a row, and the
        // second press then acted on the screen the first had just opened - Settings to the main
        // menu, and on to "press again to exit".
        if (Time.unscaledTime - lastBackPress < BACK_REPEAT_SECONDS)
            return;
        lastBackPress = Time.unscaledTime;

        if (menusStack.Count == 0 || UIFader.isFadingIn || UIFader.isFadingOut)
            return;

        if (confirmationDialog.isShowing()) // also the odds
        {
            confirmationDialog.NegativeButton();
            return;
        }

        switch (menusStack.Peek())
        {
            case Menus.MainMenu:
                if (Time.unscaledTime - lastBackOnMainMenu <= EXIT_PRESS_WINDOW)
                {
                    Application.Quit();
                }
                else
                {
                    lastBackOnMainMenu = Time.unscaledTime;
                    AndroidToast.Show("Press back again to exit");
                }
                return;

            case Menus.InGameUI:
                if (Utility.gameStarted && !Utility.isGamePaused)
                    inGameUI.GetComponent<InGameUI>().PauseGame(); // itself checks the run can pause now
                return;

            case Menus.PauseMenu:
                pauseMenu.GetComponent<PauseMenu>().ResumeGame();
                return;

            case Menus.ReviveMenu:
                reviveMenu.GetComponent<ReviveMenu>().CancelRevive();
                return;

            case Menus.GameOverMenu:
                gameOverMenu.GetComponent<GameOverMenu>().RestartGame(); // back to the main menu
                return;

            case Menus.ShopMenu:
                ShopMenu shop = shopMenu.GetComponent<ShopMenu>();
                if (shop.isMysteryBoxSeeking)
                    shop.revealBack();
                else
                    shop.Back();
                return;

            default:
                hideCurrentMenu(false);
                return;
        }
    }

    public void showAndAddMenuToStack(Menus menu)
    {
        if (UIFader.isFadingIn)
            return;

        GameAnalytics.Screen(menu.ToString());

        hideCurrentMenu(true);

        switch (menu)
        {
            case Menus.MainMenu:
                showMainMenu();
                break;

            case Menus.ShopMenu:
                showShopMenu();
                break;

            case Menus.DiamondsShopMenu:
                showDiamondsShopMenu();
                break;

            case Menus.SettingsMenu:
                showSettingsMenu();
                break;

            case Menus.GameOverMenu:
                showGameOverMenu();
                break;

            case Menus.InGameUI:
                showInGameUI();
                break;

            case Menus.PurchaseMenu:
                showPurchaseMenu();
                break;

            case Menus.UpgradeMenu:
                showUpgradeMenu();
                break;

            case Menus.ReviveMenu:
                showReviveMenu();
                break;

            case Menus.PauseMenu:
                showPauseMenu();
                break;
        }

        menusStack.Push(menu);
    }

    public void hideAllMenus()
    {
        if (UIFader.isFadingIn)
            return;

        foreach (Menus menu in System.Enum.GetValues(typeof(Menus)))
        {
            hideMenu(menu);
            // Debug.Log("hiding menu :" + menu.ToString());
        }

    }

    public void hideStackMenus()
    {

        while (menusStack.Count > 0)
        {
            hideCurrentMenu(false);
        }

    }

    public void hideCurrentMenu(bool keepInStack)
    {

        if (menusStack.Count == 0 || UIFader.isFadingOut)
            return;

        Menus currentMenu = menusStack.Pop();

        hideMenu(currentMenu);

        if (keepInStack)
            menusStack.Push(currentMenu);
        else if (menusStack.Count > 0)
            showAndAddMenuToStack(menusStack.Pop());

    }

    private void hideMenu(Menus menu)
    {

        switch (menu)
        {
            case Menus.MainMenu:
                hideMainMenu();
                break;

            case Menus.ShopMenu:
                hideShopMenu();
                break;

            case Menus.DiamondsShopMenu:
                hideDiamondsShopMenu();
                break;

            case Menus.SettingsMenu:
                hideSettingsMenu();
                break;

            case Menus.GameOverMenu:
                hideGameOverMenu();
                break;

            case Menus.InGameUI:
                hideInGameUI();
                break;

            case Menus.PurchaseMenu:
                hidePurchaseMenu();
                break;

            case Menus.UpgradeMenu:
                hideUpgradeMenu();
                break;

            case Menus.ReviveMenu:
                hideReviveMenu();
                break;

            case Menus.PauseMenu:
                hidePauseMenu();
                break;
        }

    }

    private void showMainMenu()
    {
        if (cameraSwitched)
        {
            parentCanvas.worldCamera = mainCamera;
            mainCamera.cullingMask |= (1 << 5);
            mainCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(canvasCamera);
            cameraSwitched = false;
        }
        mainMenu.gameObject.GetComponent<UIFader>().FadeIn();
        player.gameObject.SetActive(true);
        adsManager.ShowBannerAd();
    }

    private void hideMainMenu()
    {
        mainMenu.gameObject.GetComponent<UIFader>().FadeOut();
    }

    private void showShopMenu()
    {
        shopMenu.gameObject.GetComponent<UIFader>().FadeIn();
        player.GetComponent<Moons>().clearMoons();
        player.gameObject.SetActive(false);
        adsManager.HideBannerAd();
        // shopMenu.GetComponent<ShopMenu>().initShopMenu();
    }

    private void hideShopMenu()
    {
        shopMenu.gameObject.GetComponent<UIFader>().FadeOut();
        player.gameObject.SetActive(true);
        // shopMenu.GetComponent<ShopMenu>().resetShopMenu();
    }

    private void showGameOverMenu()
    {
        gameOverMenu.gameObject.GetComponent<UIFader>().FadeIn();
    }

    private void hideGameOverMenu()
    {
        gameOverMenu.gameObject.GetComponent<UIFader>().FadeOut();
    }

    private void showDiamondsShopMenu()
    {
        diamondsShopMenu.gameObject.GetComponent<UIFader>().FadeIn();
        player.gameObject.SetActive(false);
    }

    private void hideDiamondsShopMenu()
    {
        diamondsShopMenu.gameObject.GetComponent<UIFader>().FadeOut();
        player.gameObject.SetActive(true);
    }

    private void showSettingsMenu()
    {
        settingsMenu.gameObject.GetComponent<UIFader>().FadeIn();
        player.GetComponent<Moons>().clearMoons();
        player.gameObject.SetActive(false);
    }

    private void hideSettingsMenu()
    {
        settingsMenu.gameObject.GetComponent<UIFader>().FadeOut();
        player.gameObject.SetActive(true);
    }

    private void showInGameUI()
    {
        if (!cameraSwitched)
        {
            parentCanvas.worldCamera = canvasCamera;
            mainCamera.cullingMask &= ~(1 << 5);
            mainCamera.GetUniversalAdditionalCameraData().cameraStack.Add(canvasCamera);
            cameraSwitched = true;
        }
        inGameUI.gameObject.GetComponent<UIFader>().FadeIn();
        adsManager.ShowBannerAd();
    }

    private void hideInGameUI()
    {
        inGameUI.gameObject.GetComponent<UIFader>().FadeOut();
    }

    private void showPurchaseMenu()
    {
        purchaseMenu.gameObject.GetComponent<UIFader>().FadeIn();
        player.GetComponent<Moons>().clearMoons();
        player.gameObject.SetActive(false);
        adsManager.HideBannerAd();
    }

    private void hidePurchaseMenu()
    {
        purchaseMenu.gameObject.GetComponent<UIFader>().FadeOut();
        player.gameObject.SetActive(true);
    }

    private void showUpgradeMenu()
    {
        upgradeMenu.gameObject.GetComponent<UIFader>().FadeIn();
        player.GetComponent<Moons>().clearMoons();
        player.gameObject.SetActive(false);
    }

    private void hideUpgradeMenu()
    {
        upgradeMenu.gameObject.GetComponent<UIFader>().FadeOut();
        player.gameObject.SetActive(true);
    }

    private void showReviveMenu()
    {
        reviveMenu.gameObject.GetComponent<UIFader>().FadeIn();
    }

    private void hideReviveMenu()
    {
        reviveMenu.gameObject.GetComponent<UIFader>().FadeOut();
    }

    private void showPauseMenu()
    {
        pauseMenu.gameObject.GetComponent<UIFader>().FadeIn();
    }

    private void hidePauseMenu()
    {
        pauseMenu.gameObject.GetComponent<UIFader>().FadeOut();
    }

}
