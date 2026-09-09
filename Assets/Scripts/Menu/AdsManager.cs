// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Advertisements;

// public class AdsManager : MonoBehaviour, IUnityAdsShowListener, IUnityAdsLoadListener
// {
//     private readonly string myGameIdIOS = "5392267";
//     private readonly string myGameIdAndroid = "5392266";
//     private readonly string interstitialPlacementId = "Interstitial_";
//     private readonly string bannerPlacementId = "Banner_";
//     private readonly string revivePlacementId = "Revive_Rewarded_";
//     private readonly string mysteryBoxPlacementId = "MysteryBoxRewarded_";
//     private string platform = "Android";
//     private bool testMode = false;

//     void Awake()
//     {
//         InitializeAds();
//     }

//     public void InitializeAds()
//     {
// #if UNITY_IOS
//         Advertisement.Initialize(myGameIdIOS, testMode);
//         platform = "iOS";
// #elif UNITY_ANDROID
//         Advertisement.Initialize(myGameIdAndroid, testMode);
//         platform = "Android";
// #elif UNITY_EDITOR
//         Advertisement.Initialize(myGameIdAndroid, testMode);
//         platform = "Android";
// #endif

//         Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
//     }

//     public void LoadMysteryBoxAd()
//     {
//         Advertisement.Load(mysteryBoxPlacementId + platform, this);
//     }

//     public void ShowMysteryBoxAd()
//     {
//         Advertisement.Show(mysteryBoxPlacementId + platform, this);
//     }

//     public void LoadReviveAd()
//     {
//         Advertisement.Load(revivePlacementId + platform, this);
//     }

//     public void ShowReviveAd()
//     {
//         Advertisement.Show(revivePlacementId + platform, this);
//     }

//     public void LoadBannerAd()
//     {
//         // Set up options to notify the SDK of load events:
//         BannerLoadOptions options = new BannerLoadOptions
//         {
//             loadCallback = OnBannerLoaded,
//             errorCallback = OnBannerError
//         };

//         // Load the Ad Unit with banner content:
//         Advertisement.Banner.Load(bannerPlacementId + platform, options);
//     }

//     public void ShowBannerAd()
//     {

//         BannerOptions options = new BannerOptions
//         {
//             clickCallback = OnBannerClicked,
//             hideCallback = OnBannerHidden,
//             showCallback = OnBannerShown
//         };

//         Advertisement.Banner.Show(bannerPlacementId + platform, options);
//     }

//     // Implement code to execute when the loadCallback event triggers:
//     void OnBannerLoaded()
//     {
//         Debug.Log("Banner loaded");
//     }

//     // Implement code to execute when the load errorCallback event triggers:
//     void OnBannerError(string message)
//     {
//         Debug.Log($"Banner Error: {message}");
//         // Optionally execute additional code, such as attempting to load another ad.
//     }
//     void OnBannerClicked() { }
//     void OnBannerShown() { }
//     void OnBannerHidden() { }

//     public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
//     {
//         if (showCompletionState.Equals(UnityAdsShowCompletionState.COMPLETED))
//         {
//             if (placementId.Contains(mysteryBoxPlacementId))
//             {
//                 Debug.Log("Unity Ads Rewarded Ad Completed, placement = " + placementId);
//                 // Grant a reward.
//             }
//             if (placementId.Contains(revivePlacementId))
//             {
//                 Debug.Log("Unity Ads Rewarded Ad Completed, placement = " + placementId);
//                 // Grant a reward.
//             }
//         }
//     }

//     public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
//     {
//         Debug.Log("Unity Ads Rewarded Ad Failure");
//     }

//     public void OnUnityAdsShowStart(string placementId) { }
//     public void OnUnityAdsShowClick(string placementId) { }
//     public void OnUnityAdsAdLoaded(string placementId) { }
//     public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message) { }
// }
