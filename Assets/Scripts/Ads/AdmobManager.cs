using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Sample;
using System;
using GoogleMobileAds.Mediation.IronSource.Api;
using GoogleMobileAds.Mediation.LiftoffMonetize.Api;
using GoogleMobileAds.Mediation.Chartboost.Api;
using GoogleMobileAds.Mediation.AppLovin.Api;
using GoogleMobileAds.Mediation.InMobi.Api;

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

    [Obsolete]
    void Start()
    {
        // On Android, Unity is paused when displaying interstitial or rewarded video.
        // This setting makes iOS behave consistently with Android.
        MobileAds.SetiOSAppPauseOnBackground(true);

        // Configure your RequestConfiguration with Child Directed Treatment
        // and the Test Device Ids.
        RequestConfiguration requestConfiguration = new RequestConfiguration
        {
            TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
            MaxAdContentRating = MaxAdContentRating.G,
            TestDeviceIds = TestDeviceIds
        };
        MobileAds.SetRequestConfiguration(requestConfiguration);

        IronSource.SetConsent(true);
        IronSource.SetMetaData("do_not_sell", "true");

        LiftoffMonetize.SetCCPAStatus(true);
        LiftoffMonetize.SetGDPRStatus(true, "v1.0.0");
        #if UNITY_IPHONE
            LiftoffMonetize.SetGDPRMessageVersion("v1.0.0");
        #endif

        Chartboost.AddDataUseConsent(CBGDPRDataUseConsent.NonBehavioral);

        AppLovin.SetHasUserConsent(true);
        AppLovin.SetIsAgeRestrictedUser(true);
        AppLovin.SetDoNotSell(true);

        Dictionary<string, string> consentObject = new Dictionary<string, string>();
        consentObject.Add("gdpr_consent_available", "true");
        consentObject.Add("gdpr", "1");

        InMobi.UpdateGDPRConsent(consentObject);

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
