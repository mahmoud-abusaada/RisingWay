using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// The in-app purchase menu. Display and input only: the store connection, and what each
/// purchase grants, live in <see cref="IapStore"/> (P1-06).
/// </summary>
public class PurchaseMenu : MonoBehaviour
{
    [SerializeField] public RectTransform contentRect;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI diamondsOwned;
    [SerializeField] private TextMeshProUGUI doublePointsOwned;
    [SerializeField] private TextMeshProUGUI boltsOwned;
    [SerializeField] private TextMeshProUGUI chancesOwned;
    [SerializeField] private TextMeshProUGUI boxesOwned;
    [SerializeField] private List<ProductItem> products;
    [SerializeField] private ConfirmationDialog confirmationDialog;
    private MenusController menusController;
    private Action OnPurchaseCompleted;
    private IapStore subscribedStore;

    void Start()
    {
        menusController = FindObjectOfType<MenusController>();
    }

    void OnEnable()
    {
        // Subscribed once and kept while the menu is closed: a purchase can finish after the
        // player has left the menu, and its Buy button must still be re-enabled.
        subscribe();
        updatePickUpsCount();
        CreateProductsUI();
        // Not connected (opened offline, or Play services dropped)? Try again now; prices fill in
        // through ProductsReady when it succeeds.
        if (IapStore.Instance != null)
            IapStore.Instance.EnsureReady();
    }

    void OnDestroy()
    {
        unsubscribe();
    }

    private void subscribe()
    {
        IapStore store = IapStore.Instance;
        if (store == null || store == subscribedStore)
            return;
        subscribedStore = store;
        store.ProductsReady += CreateProductsUI;
        store.PurchaseGranted += OnPurchaseGranted;
        store.PurchaseFailed += OnPurchaseFailed;
        store.PurchaseDeferred += OnPurchaseDeferred;
    }

    private void unsubscribe()
    {
        if (subscribedStore == null)
            return;
        subscribedStore.ProductsReady -= CreateProductsUI;
        subscribedStore.PurchaseGranted -= OnPurchaseGranted;
        subscribedStore.PurchaseFailed -= OnPurchaseFailed;
        subscribedStore.PurchaseDeferred -= OnPurchaseDeferred;
        subscribedStore = null;
    }

    private void updatePickUpsCount()
    {
        diamondsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getDiamondsCount());
        doublePointsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getDoublePointsCount());
        boltsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getBoltsCount());
        chancesOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getChancesCount());
        boxesOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getBoxesCount());
    }

    public void Back()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.hideCurrentMenu(false);
    }

    private void CreateProductsUI()
    {
        foreach (ProductItem p in products)
        {
            // P2-03: CreateProductsUI() runs from BOTH OnEnable and ProductsReady, so without
            // this unsubscribe the handler stacked up once per store visit and a single Buy tap
            // started one purchase for every previous visit.
            // Unsubscribing a delegate that was never added is a safe no-op.
            p.OnPurchase -= HandlePurchase;
            p.OnPurchase += HandlePurchase;
            p.Setup(IapStore.Instance != null ? IapStore.Instance.GetProduct(p.productId) : null);
        }
    }

    private void HandlePurchase(Product product, Action OnPurchaseCompleted)
    {
        // Set before starting: a store may report the outcome before Purchase() returns.
        this.OnPurchaseCompleted = OnPurchaseCompleted;
        if (IapStore.Instance == null || !IapStore.Instance.Purchase(product))
        {
            // Almost always the store not being reachable yet, not a missing product.
            confirmationDialog.setConfirmationDialog("Store Unavailable",
                "Couldn't reach Google Play. Check your connection and try again.", false);
            if (IapStore.Instance != null)
                IapStore.Instance.EnsureReady();
            finishPurchaseUI();
        }
    }

    public void BuyItem(Product product)
    {
        if (IapStore.Instance != null)
            IapStore.Instance.Purchase(product);
    }

    // The scene still has old "BuyButton"s wired to these, with amounts, inside sections that
    // are switched off (Content/Bolts, /Diamonds, /Double Points, /Chances, /Other/Ads). They
    // used to add the amount for free. Real grants now happen only in IapStore, so these only
    // refresh the counters and ignore the amount, in case one of those sections is ever
    // switched back on.
    public void BuyDiamonds(int amount) { ignoredGrant(amount); }
    public void BuyDoublePoints(int amount) { ignoredGrant(amount); }
    public void BuyBolts(int amount) { ignoredGrant(amount); }
    public void BuyChances(int amount) { ignoredGrant(amount); }
    public void BuyBoxes(int amount) { ignoredGrant(amount); }

    private void ignoredGrant(int amount)
    {
        if (amount != 0)
            Debug.LogWarning("PurchaseMenu: a legacy Buy button tried to grant " + amount +
                             " for free. Ignored - grants only come from IapStore.");
        updatePickUpsCount();
    }

    private void OnPurchaseGranted(string productId)
    {
        updatePickUpsCount();
        finishPurchaseUI();
    }

    private void OnPurchaseFailed(string productId, PurchaseFailureReason reason)
    {
        finishPurchaseUI();
        if (reason == PurchaseFailureReason.UserCancelled)
            confirmationDialog.setConfirmationDialog("Purchase Canceled", "Purchase canceled by the user", false);
        else
            confirmationDialog.setConfirmationDialog("Purchase Failed", "Purchase failed for some reason, please try again later.", false);
    }

    private void OnPurchaseDeferred(string productId)
    {
        finishPurchaseUI();
        confirmationDialog.setConfirmationDialog("Purchase Pending",
            "Your payment is being processed. You will receive your purchase as soon as it completes.", false);
    }

    private void finishPurchaseUI()
    {
        Action done = OnPurchaseCompleted;
        OnPurchaseCompleted = null;
        done?.Invoke();
    }

    /// <summary>The mystery box odds, next to the box packs: Play policy wants them shown before purchase.</summary>
    public void ShowMysteryBoxOdds()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;
        confirmationDialog.setInfoDialog("Mystery Box Odds",
            MysteryBoxPrizes.OddsText(FindAnyObjectByType<MaterialsManager>()), 30f);
    }

    public void RestorePurchases()
    {
        if (IapStore.Instance == null)
        {
            confirmationDialog.setConfirmationDialog("Restoration failed", "Couldn't restore your purchases.", false);
            return;
        }

        IapStore.Instance.RestorePurchases(ok =>
        {
            if (ok)
                // This does not mean anything was restored, merely that the restoration
                // process succeeded.
                confirmationDialog.setConfirmationDialog("Restoration succeeded", "Your purchases have been restored.", false);
            else
                confirmationDialog.setConfirmationDialog("Restoration failed", "Couldn't restore your purchases.", false);
        });
    }
}
