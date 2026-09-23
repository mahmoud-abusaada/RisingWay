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

    /// <summary>
    /// How much each pack gives. The one place that says so: Grant() pays it out and the shop
    /// labels it (ProductItem.Setup), so a price change can never leave the shop advertising an
    /// amount the game does not hand over. The prices these go with live in Play Console, and in
    /// Resources/IAPProductCatalog.json for the Editor's fake store.
    ///
    /// Re-scaled on 2026-09-23, before the relaunch listed its products: each tier used to be
    /// worth only ~37% more per pound than the entry pack, so there was no reason to buy above the
    /// cheapest. Now the top tier is worth twice the entry one, on the usual 0.99 / 4.99 / 9.99 /
    /// 19.99 price points.
    /// </summary>
    public static readonly Dictionary<string, int> PackAmounts = new Dictionary<string, int>
    {
        { "chances_1", 5 },        { "chances_2", 30 },        { "chances_3", 75 },        { "chances_4", 200 },
        { "bolts_1", 5 },          { "bolts_2", 30 },          { "bolts_3", 75 },          { "bolts_4", 200 },
        { "double_points_1", 5 },  { "double_points_2", 30 },  { "double_points_3", 75 },  { "double_points_4", 200 },
        { "diamonds_1", 5000 },    { "diamonds_2", 35000 },    { "diamonds_3", 80000 },    { "diamonds_4", 200000 },
        { "boxes_1", 3 },          { "boxes_2", 20 },          { "boxes_3", 50 },          { "boxes_4", 120 },
    };

    private static void Grant(string productId)
    {
        if (productId == REMOVE_ADS)
        {
            GrantRemoveAds();
            return;
        }

        int amount;
        PlayerStats stats = PlayerStats.Instance;
        if (PackAmounts.TryGetValue(productId, out amount))
        {
            if (productId.StartsWith("chances")) { stats.addChances(amount); return; }
            if (productId.StartsWith("bolts")) { stats.addBolts(amount); return; }
            if (productId.StartsWith("double_points")) { stats.addDoublePoints(amount); return; }
            if (productId.StartsWith("diamonds")) { stats.addDiamonds(amount); return; }
            if (productId.StartsWith("boxes")) { stats.addBoxes(amount); return; }
        }

        Debug.LogError("IAP: no grant defined for product '" + productId + "'. The player paid and got nothing.");
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
