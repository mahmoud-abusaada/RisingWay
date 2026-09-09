using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;

public class PurchaseMenu : MonoBehaviour, IStoreListener
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
    private IStoreController controller;
    private IExtensionProvider extensions;
    private Action OnPurchaseCompleted;

    async void Awake()
    {
        InitializationOptions options = new InitializationOptions()
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        .SetEnvironmentName("test");
#else
        .SetEnvironmentName("production");
#endif
        await UnityServices.InitializeAsync(options);
        ResourceRequest operation = Resources.LoadAsync<TextAsset>("IAPProductCatalog");
        operation.completed += HandleIAPCatalogLoaded;
    }

    private void HandleIAPCatalogLoaded(AsyncOperation Operation)
    {
        ResourceRequest request = Operation as ResourceRequest;

        ProductCatalog catalog = JsonUtility.FromJson<ProductCatalog>((request.asset as TextAsset).text);

#if UNITY_ANDROID
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
#elif UNITY_IOS
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.AppleAppStore));
#else
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.NotSpecified));
#endif

        foreach (ProductCatalogItem item in catalog.allProducts)
        {
            builder.AddProduct(item.id, item.type);
            Debug.Log(item.id);
        }

        UnityPurchasing.Initialize(this, builder);
    }

    void Start()
    {
        menusController = FindObjectOfType<MenusController>();
    }

    void OnEnable()
    {
        updatePickUpsCount();
        CreateProductsUI();
    }

    private void updatePickUpsCount()
    {
        BuyDiamonds(0);
        BuyDoublePoints(0);
        BuyBolts(0);
        BuyChances(0);
        BuyBoxes(0);
    }

    public void Back()
    {
        if (!MultiClickHandler.Instance.CanClick()) return;

        menusController.hideCurrentMenu(false);
    }

    private void CreateProductsUI()
    {
        // Chances
        foreach (ProductItem p in products)
        {
            p.OnPurchase += HandlePurchase;
            p.Setup(controller?.products.WithID(p.productId));
        }
        // loadingText.gameObject.SetActive(false);
        // contentRect.gameObject.SetActive(true);
    }

    private void HandlePurchase(Product product, Action OnPurchaseCompleted)
    {
        if (product == null)
        {
            confirmationDialog.setConfirmationDialog("Purchase Failed", "Couldn't find the IAP product.", false);
            OnPurchaseCompleted?.Invoke();
            OnPurchaseCompleted = null;
        }
        else
        {
            this.OnPurchaseCompleted = OnPurchaseCompleted;
            controller.InitiatePurchase(product);
        }
    }

    public void BuyItem(Product product)
    {
        controller.InitiatePurchase(product);
    }

    public void BuyDiamonds(int amount)
    {
        PlayerStats.Instance.addDiamonds(amount);
        diamondsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getDiamondsCount());
    }

    public void BuyDoublePoints(int amount)
    {
        PlayerStats.Instance.addDoublePoints(amount);
        doublePointsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getDoublePointsCount());
    }

    public void BuyBolts(int amount)
    {
        PlayerStats.Instance.addBolts(amount);
        boltsOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getBoltsCount());
    }

    public void BuyChances(int amount)
    {
        PlayerStats.Instance.addChances(amount);
        chancesOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getChancesCount());
    }

    public void BuyBoxes(int amount)
    {
        PlayerStats.Instance.addBoxes(amount);
        boxesOwned.text = Utility.getFormatedNumber(PlayerStats.Instance.getBoxesCount());
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError($"Error Initializing IAP because of {error}." +
            $"\r\nShow a message to the player depending on the error.");
        // loadingText.text = "Couldn't load products!";
        // CreateProductsUI();
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"Error Initializing IAP because of {error}." +
            $"\r\nShow a message to the player depending on the error.");
        // loadingText.text = "Couldn't load products!";
        // CreateProductsUI();
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
    {
        Debug.Log($"Successfully purchased {purchaseEvent.purchasedProduct.definition.id}");
        OnPurchaseCompleted?.Invoke();
        OnPurchaseCompleted = null;

        // TODO
        // Debug.Log("Quantity = " + purchaseEvent.purchasedProduct.definition.payouts.ToList().Count);

        var productId = purchaseEvent.purchasedProduct.definition.id;
        switch (productId)
        {
            case "remove_ads":
                AdmobManager.Instance.HideBannerAd();
                PlayerStats.Instance.setAdsEnabled(false);
                break;

            case "chances_1":
                BuyChances(5);
                break;
            case "chances_2":
                BuyChances(25);
                break;
            case "chances_3":
                BuyChances(50);
                break;
            case "chances_4":
                BuyChances(100);
                break;

            case "bolts_1":
                BuyBolts(5);
                break;
            case "bolts_2":
                BuyBolts(25);
                break;
            case "bolts_3":
                BuyBolts(50);
                break;
            case "bolts_4":
                BuyBolts(100);
                break;

            case "double_points_1":
                BuyDoublePoints(5);
                break;
            case "double_points_2":
                BuyDoublePoints(25);
                break;
            case "double_points_3":
                BuyDoublePoints(50);
                break;
            case "double_points_4":
                BuyDoublePoints(100);
                break;

            case "diamonds_1":
                BuyDiamonds(5000);
                break;
            case "diamonds_2":
                BuyDiamonds(25000);
                break;
            case "diamonds_3":
                BuyDiamonds(100000);
                break;
            case "diamonds_4":
                BuyDiamonds(200000);
                break;

            case "boxes_1":
                BuyBoxes(5);
                break;
            case "boxes_2":
                BuyBoxes(25);
                break;
            case "boxes_3":
                BuyBoxes(50);
                break;
            case "boxes_4":
                BuyBoxes(100);
                break;
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log($"Failed to purchase {product.definition.id} because {failureReason}");
        OnPurchaseCompleted?.Invoke();
        OnPurchaseCompleted = null;
        switch (failureReason)
        {
            case PurchaseFailureReason.UserCancelled:
                confirmationDialog.setConfirmationDialog("Purchase Canceled", "Purchase canceled by the user", false);
                break;
            default:
                confirmationDialog.setConfirmationDialog("Purchase Failed", "Purchase failed for some reason, please try again later.", false);
                break;
        }
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        this.controller = controller;
        this.extensions = extensions;
        CreateProductsUI();
    }

    public void RestorePurchases()
    {
        if (extensions == null)
        {
            confirmationDialog.setConfirmationDialog("Restoration failed", "Couldn't restore your purchases.", false);
        }
        else
        {
            extensions.GetExtension<IAppleExtensions>().RestoreTransactions((result, str) =>
            {
                if (result)
                {
                    // This does not mean anything was restored,
                    // merely that the restoration process succeeded.
                    confirmationDialog.setConfirmationDialog("Restoration succeeded", "Your purchases have been restored.", false);
                }
                else
                {
                    // Restoration failed.
                    confirmationDialog.setConfirmationDialog("Restoration failed", "Couldn't restore your purchases.", false);
                }
            });
        }
    }
}
