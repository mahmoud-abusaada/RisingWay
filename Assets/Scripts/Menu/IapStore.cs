using System;
using System.Collections.Generic;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// In-app purchases (P1-06): Unity IAP 5, owned by one object that starts with the app.
///
/// Before, PurchaseMenu set IAP up in its Awake(). That menu is inactive in the scene, so the
/// store only connected once a player opened it: a purchase interrupted by the app being killed
/// was not delivered, and "Remove Ads" was not restored after a reinstall, until the player
/// happened to open the store again. This object connects at launch, so both happen on startup.
///
/// It also moves off the Unity IAP 4 API (IStoreListener, UnityPurchasing.Initialize,
/// ProcessPurchase), which IAP 5 only keeps as an obsolete compatibility layer.
///
/// What a purchase grants is decided here, in <see cref="Grant"/>, and nowhere else.
/// PurchaseMenu only displays the products and starts purchases.
/// </summary>
public class IapStore : MonoBehaviour
{
    public const string REMOVE_ADS = "remove_ads";

    public static IapStore Instance { get; private set; }

    /// <summary>Products were fetched from the store; prices can be shown.</summary>
    public event Action ProductsReady;
    /// <summary>A purchase was granted. Argument: product id.</summary>
    public event Action<string> PurchaseGranted;
    /// <summary>A purchase the player started did not complete. Arguments: product id (may be null), reason.</summary>
    public event Action<string, PurchaseFailureReason> PurchaseFailed;
    /// <summary>The store accepted the order but payment is still pending (e.g. cash payment). Argument: product id.</summary>
    public event Action<string> PurchaseDeferred;

    public bool IsReady { get; private set; }

    private StoreController store;
    private List<ProductDefinition> productDefinitions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAtStartup()
    {
        if (Instance != null)
            return;
        GameObject go = new GameObject("IapStore");
        DontDestroyOnLoad(go);
        go.AddComponent<IapStore>();
    }

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Unity Gaming Services are not needed for IAP 5 itself; this keeps the environment the
        // game has always used. A failure here (offline, for example) must not stop purchases.
        try
        {
            InitializationOptions options = new InitializationOptions()
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                .SetEnvironmentName("test");
#else
                .SetEnvironmentName("production");
#endif
            await UnityServices.InitializeAsync(options);
        }
        catch (Exception e)
        {
            Debug.LogWarning("IAP: Unity Gaming Services did not initialise (" + e.Message + "). Continuing without them.");
        }

        productDefinitions = LoadProductDefinitions();
        if (productDefinitions.Count == 0)
        {
            Debug.LogError("IAP: the product catalog (Resources/IAPProductCatalog) is empty or missing.");
            return;
        }

        store = UnityIAPServices.StoreController();
        store.OnStoreConnected += OnStoreConnected;
        store.OnStoreDisconnected += OnStoreDisconnected;
        store.OnProductsFetched += OnProductsFetched;
        store.OnProductsFetchFailed += OnProductsFetchFailed;
        store.OnPurchasesFetched += OnPurchasesFetched;
        store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        store.OnPurchasePending += OnPurchasePending;
        store.OnPurchaseConfirmed += OnPurchaseConfirmed;
        store.OnPurchaseFailed += OnPurchaseFailed;
        store.OnPurchaseDeferred += OnPurchaseDeferred;

        try
        {
            await store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError("IAP: could not connect to the store: " + e.Message);
        }
    }

    private static List<ProductDefinition> LoadProductDefinitions()
    {
        List<ProductDefinition> result = new List<ProductDefinition>();
        TextAsset json = Resources.Load<TextAsset>("IAPProductCatalog");
        if (json == null)
            return result;
        ProductCatalog catalog = JsonUtility.FromJson<ProductCatalog>(json.text);
        foreach (ProductCatalogItem item in catalog.allProducts)
            result.Add(new ProductDefinition(item.id, item.type));
        return result;
    }

    // ---- Public API --------------------------------------------------------------------------

    /// <summary>The fetched product, or null if the store is not ready or does not sell it.</summary>
    public Product GetProduct(string productId)
    {
        return IsReady ? store.GetProductById(productId) : null;
    }

    /// <summary>Starts a purchase. The outcome arrives as PurchaseGranted / PurchaseFailed / PurchaseDeferred.</summary>
    public bool Purchase(Product product)
    {
        if (!IsReady || product == null)
            return false;
        store.PurchaseProduct(product);
        return true;
    }

    /// <summary>
    /// Asks the store to re-deliver previous purchases (the App Store needs this explicitly; on
    /// Google Play they are already restored at startup). Non-consumables come back through
    /// OnPurchasesFetched.
    /// </summary>
    public void RestorePurchases(Action<bool> done)
    {
        if (!IsReady)
        {
            done?.Invoke(false);
            return;
        }
        store.RestoreTransactions((ok, error) =>
        {
            if (!ok)
                Debug.LogWarning("IAP: restore failed: " + error);
            done?.Invoke(ok);
        });
    }

    // ---- Store callbacks ---------------------------------------------------------------------

    private void OnStoreConnected()
    {
        store.FetchProducts(productDefinitions);
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription description)
    {
        IsReady = false;
        Debug.LogWarning("IAP: store disconnected: " + description.message);
    }

    private void OnProductsFetched(List<Product> products)
    {
        IsReady = true;
        ProductsReady?.Invoke();
        // IAP 4 replayed unfinished and owned purchases during initialisation. IAP 5 does it on
        // request: unfinished ones come back through OnPurchasePending, owned non-consumables
        // through OnPurchasesFetched.
        store.FetchPurchases();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogWarning("IAP: " + failure.FailedFetchProducts.Count + " product(s) could not be fetched: " + failure.FailureReason);
        // Products that did load are still usable.
        if (store.GetProducts().Count > 0 && !IsReady)
        {
            IsReady = true;
            ProductsReady?.Invoke();
            store.FetchPurchases();
        }
    }

    private void OnPurchasesFetched(Orders orders)
    {
        // Purchases the store already considers finished. The only one that is an entitlement
        // rather than a one-off grant is Remove Ads, which must survive a reinstall.
        foreach (ConfirmedOrder order in orders.ConfirmedOrders)
        {
            foreach (CartItem item in order.CartOrdered.Items())
            {
                if (item.Product != null && item.Product.definition.id == REMOVE_ADS)
                    GrantRemoveAds();
            }
        }
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
    {
        Debug.LogWarning("IAP: could not fetch previous purchases: " + failure.Message);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        // Paid for, not yet finished. Grant, save, then confirm - if the app dies before the
        // confirmation reaches the store, the order is delivered again on the next launch.
        foreach (CartItem item in order.CartOrdered.Items())
        {
            if (item.Product == null)
                continue;
            string id = item.Product.definition.id;
            Grant(id);
            PurchaseGranted?.Invoke(id);
        }
        PlayerStats.Instance.Flush();
        store.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        FailedOrder failed = order as FailedOrder;
        if (failed != null)
        {
            // The grant has already happened. The store will offer this order again on a later
            // launch; see OnPurchasePending.
            Debug.LogWarning("IAP: the store did not accept the confirmation for " + FirstProductId(order) +
                             ": " + failed.FailureReason + " " + failed.Details);
        }
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        string id = FirstProductId(order);
        Debug.Log("IAP: purchase of " + id + " failed: " + order.FailureReason + " " + order.Details);
        PurchaseFailed?.Invoke(id, order.FailureReason);
    }

    private void OnPurchaseDeferred(DeferredOrder order)
    {
        string id = FirstProductId(order);
        Debug.Log("IAP: purchase of " + id + " is waiting for payment.");
        PurchaseDeferred?.Invoke(id);
    }

    private static string FirstProductId(Order order)
    {
        foreach (CartItem item in order.CartOrdered.Items())
            if (item.Product != null)
                return item.Product.definition.id;
        return null;
    }

    // ---- What each product gives -------------------------------------------------------------

    // The quantities are the ones the game has always granted; the catalog only lists the ids.
    private static void Grant(string productId)
    {
        PlayerStats stats = PlayerStats.Instance;
        switch (productId)
        {
            case REMOVE_ADS: GrantRemoveAds(); break;

            case "chances_1": stats.addChances(5); break;
            case "chances_2": stats.addChances(25); break;
            case "chances_3": stats.addChances(50); break;
            case "chances_4": stats.addChances(100); break;

            case "bolts_1": stats.addBolts(5); break;
            case "bolts_2": stats.addBolts(25); break;
            case "bolts_3": stats.addBolts(50); break;
            case "bolts_4": stats.addBolts(100); break;

            case "double_points_1": stats.addDoublePoints(5); break;
            case "double_points_2": stats.addDoublePoints(25); break;
            case "double_points_3": stats.addDoublePoints(50); break;
            case "double_points_4": stats.addDoublePoints(100); break;

            case "diamonds_1": stats.addDiamonds(5000); break;
            case "diamonds_2": stats.addDiamonds(25000); break;
            case "diamonds_3": stats.addDiamonds(100000); break;
            case "diamonds_4": stats.addDiamonds(200000); break;

            case "boxes_1": stats.addBoxes(5); break;
            case "boxes_2": stats.addBoxes(25); break;
            case "boxes_3": stats.addBoxes(50); break;
            case "boxes_4": stats.addBoxes(100); break;

            default:
                Debug.LogError("IAP: no grant defined for product '" + productId + "'. The player paid and got nothing.");
                break;
        }
    }

    private static void GrantRemoveAds()
    {
        if (!PlayerStats.Instance.isAdEnabled())
            return;
        // Hide first: HideBannerAd() does nothing once ads are disabled.
        if (AdmobManager.Instance != null)
            AdmobManager.Instance.HideBannerAd();
        PlayerStats.Instance.setAdsEnabled(false);
    }
}
