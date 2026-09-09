using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Sample;
using System;
// P1-05: the GoogleMobileAds.Mediation.* namespaces came from the seven mediation adapter
// packages, which were removed during the GMA 9.1.0 -> 11.5.0 upgrade so the core plugin could
// be brought up in isolation. Adapters get re-added deliberately once the build is green -
// see docs/p1-05-sdk-update.md.

public class AdmobManager : MonoBehaviour
{
    // Always use test ads.
    // https://developers.google.com/admob/unity/test-ads
    internal static List<string> TestDeviceIds = new List<string>()
        {
            AdRequest.TestDeviceSimulator,
#if UNITY_IPHONE
            "96e23e80653bb28980d3f40beb58915c",
#elif UNITY_ANDROID
            "702815ACFC14FF222DA1DC767672A573"
#endif
        };

    // The Google Mobile Ads Unity plugin needs to be run only once.
    private static bool? _isInitialized;
    public static AdmobManager Instance;

    private BannerViewController bannerViewController;
    private InterstitialAdController interstitialController;
    private MysteryBoxAdController mysteryBoxAdController;
    private ReviveAdController reviveAdController;

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
        // EEA/UK and a Google Play policy problem. Real consent must come from the UMP flow -
        // GoogleMobileAdsConsentController already exists and is wired up in P2-07.
        //
        // MobileAds.SetiOSAppPauseOnBackground was also dropped; it is deprecated and was the
        // reason this method carried an [Obsolete] attribute.

        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TestDeviceIds = TestDeviceIds
        };
        MobileAds.SetRequestConfiguration(requestConfiguration);

        InitializeGoogleMobileAds();
    }

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
        MobileAds.Initialize((InitializationStatus initstatus) =>
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
        });
    }

    public void LoadBannerAd()
    {
        if (PlayerStats.Instance.isAdEnabled())
            bannerViewController.LoadAd();
    }

    public void ShowBannerAd()
    {
        if (PlayerStats.Instance.isAdEnabled())
            bannerViewController.ShowAd();
    }

    public void HideBannerAd()
    {
        if (PlayerStats.Instance.isAdEnabled())
            bannerViewController.HideAd();
    }

    public void LoadInterstitialAd()
    {
        if (PlayerStats.Instance.isAdEnabled())
            interstitialController.LoadAd();
    }

    public void ShowInterstitialAd(Action doOnClose)
    {
        if (PlayerStats.Instance.isAdEnabled())
            interstitialController.ShowAd(doOnClose);
        else
            doOnClose?.Invoke();
    }

    public void LoadMysteryBoxAd()
    {
        mysteryBoxAdController.LoadAd();
    }

    public void ShowMysteryBoxAd()
    {
        mysteryBoxAdController.ShowAd();
    }

    public void LoadReviveAd()
    {
        reviveAdController.LoadAd();
    }

    public void ShowReviveAd(Action doOnFail)
    {
        reviveAdController.ShowAd(doOnFail);
    }

    public bool CanShowReviveAd()
    {
        return reviveAdController.CanShowAd();
    }
}
