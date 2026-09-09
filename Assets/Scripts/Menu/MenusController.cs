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

    void Awake()
    {
        PlayerStats.Instance.setAdsEnabled(true);
    }

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
        showAndAddMenuToStack(Menus.MainMenu);
    }

    public void handleSystemBack()
    {
        if (confirmationDialog.isShowing())
        {
            confirmationDialog.NegativeButton();
            return;
        }

        if (menusStack.Peek() == Menus.ShopMenu && shopMenu.GetComponent<ShopMenu>().isMysteryBoxSeeking)
            return;

        if (menusStack.Count == 1)
        {
            if (menusStack.Peek() == Menus.MainMenu)
                Application.Quit();
            if (menusStack.Peek() == Menus.InGameUI || menusStack.Peek() == Menus.GameOverMenu || menusStack.Peek() == Menus.PauseMenu || menusStack.Peek() == Menus.ReviveMenu)
                return;
        }
        hideCurrentMenu(false);
    }

    public void showAndAddMenuToStack(Menus menu)
    {
        if (UIFader.isFadingIn)
            return;

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
        Firebase.Analytics.FirebaseAnalytics
          .LogEvent(Firebase.Analytics.FirebaseAnalytics.EventLogin);
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
