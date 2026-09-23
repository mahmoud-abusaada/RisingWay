using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Sample;
using GoogleMobileAds.Ump.Api;
using System;
// P1-05: the GoogleMobileAds.Mediation.* namespaces came from the seven mediation adapter
// packages, which were removed during the GMA 9.1.0 -> 11.5.0 upgrade so the core plugin could
// be brought up in isolation. Adapters get re-added deliberately once the build is green -
// see docs/p1-05-sdk-update.md.

public class AdmobManager : MonoBehaviour
{
    // Devices listed here receive Google's test ads instead of live ones. This does NOT make every
    // device a test device - real users get real ads.
    // https://developers.google.com/admob/unity/test-ads
    internal static List<string> TestDeviceIds = new List<string>()
        {
            AdRequest.TestDeviceSimulator,
#if UNITY_IPHONE
            "96e23e80653bb28980d3f40beb58915c",
#elif UNITY_ANDROID
            "702815ACFC14FF222DA1DC767672A573",
            "D3F02A8197FE6BDC6B3E73AF7168125B", // Samsung S20 Ultra (developer's phone), from the SDK's log line
#endif
        };

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    // P2-07: UMP only shows its consent form to users it believes are in a regulated region, so
    // from outside the EEA/UK the form never appears and the flow cannot be tested. Development
    // builds pretend to be in the EEA for the devices listed here.
    //
    // The hashed id is NOT the AdMob test-device id above - UMP prints its own on first run:
    //     "Use new ConsentDebugSettings.Builder().addTestDeviceHashedId("<hash>")"
    // Copy that hash in here. Release builds never compile this block.
    private static readonly List<string> UmpDebugDeviceHashedIds = new List<string>()
    {
        // Pixel_10_Pro emulator (16 KB image). On Android the hash is
        // MD5(Settings.Secure.ANDROID_ID), uppercased:
        //   adb shell settings get secure android_id  ->  md5  ->  toupper
        // It changes if the emulator is wiped.
        "6ACAB0E60A094B35572CDAAD743A9E81",
    };
#endif

    // The Google Mobile Ads Unity plugin needs to be run only once.
    private static bool? _isInitialized;

    // P2-07: true only once consent permits ad requests AND the SDK has finished initialising.
    // Every load and show is gated on this.
    private static bool _adsReady;

    public static AdmobManager Instance;

    private BannerViewController bannerViewController;
    private InterstitialAdController interstitialController;
    private MysteryBoxAdController mysteryBoxAdController;
    private ReviveAdController reviveAdController;

    // P2-07: ad work requested before ads are allowed. MenusController.Start() loads four ad types
    // the moment the game boots; with consent in front of initialisation those calls would
    // otherwise arrive before the user has answered and before the SDK is ready - which breaks
    // Google's consent rules and the SDK's initialise-first rule at the same time. They are held
    // here and run, in order, once _adsReady flips.
    private readonly List<Action> _pendingAdWork = new List<Action>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        bannerViewController = FindObjectOfType<BannerViewController>();
        interstitialController = FindObjectOfType<InterstitialAdController>();
        mysteryBoxAdController = FindObjectOfType<MysteryBoxAdController>();
        reviveAdController = FindObjectOfType<ReviveAdController>();
    }

    void Start()
    {
        // P2-09: the child-directed tag is gone.
        //
        // This used to set TagForChildDirectedTreatment.True and MaxAdContentRating.G for EVERY
        // user, which globally disables personalised advertising and restricts the eligible
        // demand pool to child-safe, non-behavioural inventory. Rising Way is a general-audience
        // game, so that was suppressing ad revenue for no reason. The store listing age rating
        // and Data Safety declarations must stay consistent with this - see P9-02.
        //
        // Also removed: hardcoded consent declarations for six mediation networks
        // (IronSource.SetConsent(true), AppLovin.SetHasUserConsent(true),
        // LiftoffMonetize.SetGDPRStatus(true, ...), InMobi.UpdateGDPRConsent({gdpr: "1"}),
        // Chartboost.AddDataUseConsent(...), plus CCPA/do-not-sell flags). Those declared consent
        // on the user's behalf before any dialog was shown, which is a regulatory exposure in the
        // EEA/UK and a Google Play policy problem. Real consent now comes from the UMP flow below.
        //
        // MobileAds.SetiOSAppPauseOnBackground was also dropped; it is deprecated and was the
        // reason this method carried an [Obsolete] attribute.

        // P2-07: SDK and UMP callbacks are not guaranteed to arrive on Unity's main thread. The old
        // global switch, MobileAds.RaiseAdEventsOnUnityMainThread, is obsolete in GMA 11, and the
        // plugin's CHANGELOG also requires MobileAds.Initialize() itself to be called on the main
        // thread. So every callback below that touches Unity or initialises the SDK is marshalled
        // explicitly with MobileAdsEventExecutor.ExecuteInUpdate - see OnMainThread().

        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TestDeviceIds = TestDeviceIds
        };
        MobileAds.SetRequestConfiguration(requestConfiguration);

        GatherConsentThenInitialize();
    }

    // ================================================================== P2-07 consent
    //
    // Written against the UMP 4.0 API in the installed GMA 11.5.0, not against
    // GoogleMobileAdsConsentController. That class is Google sample code, is not in the scene, and
    // never calls its completion callback when the consent form succeeds - so ads would never have
    // started for a user who consented.

    private void GatherConsentThenInitialize()
    {
        ConsentRequestParameters request = new ConsentRequestParameters
        {
            // General-audience game (P2-09). This is the age-of-consent flag, not a child-directed
            // one, and it must stay false to match the store listing.
            TagForUnderAgeOfConsent = false,
        };

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (UmpDebugDeviceHashedIds.Count > 0)
        {
            request.ConsentDebugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA,
                TestDeviceHashedIds = UmpDebugDeviceHashedIds,
            };
        }
#endif

        ConsentInformation.Update(request, (FormError updateError) => OnMainThread(() =>
        {
            if (updateError != null)
            {
                // Not fatal: a user who consented on a previous launch is still allowed ads.
                Debug.LogWarning("UMP consent update failed (" + updateError.ErrorCode + "): " +
                                 updateError.Message);
                TryInitializeAds();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) => OnMainThread(() =>
            {
                if (formError != null)
                {
                    Debug.LogWarning("UMP consent form failed (" + formError.ErrorCode + "): " +
                                     formError.Message);
                }
                // Runs whether the form was shown, skipped, or failed. CanRequestAds() is the
                // single source of truth for what happens next.
                TryInitializeAds();
            }));
        }));

        // Google's recommended pattern: a returning user who already consented should not wait for
        // the network round trip above. InitializeGoogleMobileAds() guards against running twice.
        TryInitializeAds();
    }

    private void TryInitializeAds()
    {
        if (ConsentInformation.CanRequestAds())
        {
            InitializeGoogleMobileAds();
        }
        else
        {
            Debug.Log("Consent does not permit ad requests yet - ads stay off.");
        }
    }

    /// <summary>
    /// True when the user is in a region where they must be able to change their consent choice,
    /// and so the Settings menu has to offer a "Privacy options" entry.
    /// </summary>
    public bool IsPrivacyOptionsRequired =>
        ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

    /// <summary>
    /// Lets the user revisit their consent choice. Google requires this to be reachable from within
    /// the app whenever IsPrivacyOptionsRequired is true.
    /// </summary>
    public void ShowPrivacyOptionsForm(Action onClosed = null)
    {
        ConsentForm.ShowPrivacyOptionsForm((FormError error) => OnMainThread(() =>
        {
            if (error != null)
            {
                Debug.LogWarning("UMP privacy options form failed (" + error.ErrorCode + "): " +
                                 error.Message);
            }
            // The user may have just granted consent they previously refused.
            TryInitializeAds();
            onClosed?.Invoke();
        }));
    }

    private static void OnMainThread(Action work)
    {
        MobileAdsEventExecutor.ExecuteInUpdate(work);
    }

    // ================================================================== initialisation

    private void InitializeGoogleMobileAds()
    {
        // The Google Mobile Ads Unity plugin needs to be run only once and before loading any ads.
        if (_isInitialized.HasValue)
        {
            return;
        }

        _isInitialized = false;

        // Initialize the Google Mobile Ads Unity plugin.
        Debug.Log("Google Mobile Ads Initializing.");
        MobileAds.Initialize((InitializationStatus initstatus) => OnMainThread(() =>
        {
            if (initstatus == null)
            {
                Debug.LogError("Google Mobile Ads initialization failed.");
                _isInitialized = null;
                return;
            }

            // If you use mediation, you can check the status of each adapter.
            var adapterStatusMap = initstatus.getAdapterStatusMap();
            if (adapterStatusMap != null)
            {
                foreach (var item in adapterStatusMap)
                {
                    Debug.Log(string.Format("Adapter {0} is {1}",
                        item.Key,
                        item.Value.InitializationState));
                }
            }

            Debug.Log("Google Mobile Ads initialization complete.");
            _isInitialized = true;
            _adsReady = true;
            RunPendingAdWork();
        }));
    }

    private void WhenAdsReady(Action work)
    {
        if (_adsReady)
        {
            work();
        }
        else
        {
            _pendingAdWork.Add(work);
        }
    }

    private void RunPendingAdWork()
    {
        if (_pendingAdWork.Count == 0)
        {
            return;
        }

        Debug.Log("Running " + _pendingAdWork.Count + " ad request(s) held until ads were allowed.");
        // Copy first: a queued action may itself queue more work.
        List<Action> work = new List<Action>(_pendingAdWork);
        _pendingAdWork.Clear();
        foreach (Action action in work)
        {
            action();
        }
    }

    // ================================================================== public API
    //
    // Signatures are unchanged. isAdEnabled() is evaluated when the work actually RUNS, not when
    // it was requested - a "remove_ads" purchase made while a load was still queued must win.

    public void LoadBannerAd()
    {
        WhenAdsReady(() =>
        {
            if (PlayerStats.Instance.isAdEnabled())
                bannerViewController.LoadAd();
        });
    }

    public void ShowBannerAd()
    {
        WhenAdsReady(() =>
        {
            if (PlayerStats.Instance.isAdEnabled())
                bannerViewController.ShowAd();
        });
    }

    public void HideBannerAd()
    {
        // Nothing can be on screen before ads are ready, so there is nothing to hide.
        if (_adsReady && PlayerStats.Instance.isAdEnabled())
            bannerViewController.HideAd();
    }

    public void LoadInterstitialAd()
    {
        WhenAdsReady(() =>
        {
            if (PlayerStats.Instance.isAdEnabled())
                interstitialController.LoadAd();
        });
    }

    // Show methods are NOT queued: they are the player's action right now, and the game waits on
    // the callback. If ads are not allowed yet the callback fires immediately so play continues.
    // They also must not reach the controllers early - InterstitialAdController.ShowAd() quietly
    // calls LoadAd() when nothing is loaded, which would bypass the consent gate.

    /// <summary>
    /// No interstitials for a new player's first runs. The 2024 release showed one every 4-7
    /// deaths from the very first run - runs are short, so a first-time player could meet a
    /// full-screen ad within a couple of minutes, before deciding whether they liked the game.
    /// Rewarded ads (revive, mystery box) are the player's choice and are not held back.
    /// </summary>
    public const int AD_FREE_RUNS = 10;

    public void ShowInterstitialAd(Action doOnClose)
    {
        int runs = PlayerStats.Instance.getTimesPlayed();
        if (runs <= AD_FREE_RUNS)
        {
            GameAnalytics.AdSkippedForNewPlayer(runs);
            doOnClose?.Invoke();
            return;
        }
        if (_adsReady && PlayerStats.Instance.isAdEnabled())
        {
            GameAnalytics.AdShown("interstitial", "game_over");
            interstitialController.ShowAd(doOnClose);
        }
        else
            doOnClose?.Invoke();
    }

    public void LoadMysteryBoxAd()
    {
        WhenAdsReady(() => mysteryBoxAdController.LoadAd());
    }

    public void ShowMysteryBoxAd()
    {
        if (_adsReady)
        {
            GameAnalytics.AdShown("rewarded", "mystery_box");
            mysteryBoxAdController.ShowAd();
        }
    }

    public void LoadReviveAd()
    {
        WhenAdsReady(() => reviveAdController.LoadAd());
    }

    public void ShowReviveAd(Action doOnFail)
    {
        if (_adsReady)
        {
            GameAnalytics.AdShown("rewarded", "revive");
            reviveAdController.ShowAd(doOnFail);
        }
        else
            doOnFail?.Invoke();
    }

    public bool CanShowReviveAd()
    {
        return _adsReady && reviveAdController.CanShowAd();
    }
}
