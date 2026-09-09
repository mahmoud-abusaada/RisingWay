using System;
using UnityEngine;
using GoogleMobileAds.Api;

public class ReviveAdController : MonoBehaviour
{
    /// <summary>
    /// UI element activated when an ad is ready to show.
    /// </summary>
    // public GameObject AdLoadedStatus;

    // These ad units are configured to always serve test ads.
#if UNITY_IPHONE
    private const string _adUnitId = "ca-app-pub-4724664365967541/4040909713";
#else
    private const string _adUnitId = "ca-app-pub-4724664365967541/3724453207";
#endif

    private RewardedAd _rewardedAd;
    private ReviveMenu reviveMenu;
    private Action doOnFail;

    private ReviveMenu GetReviveMenu()
    {
        if (reviveMenu == null)
            reviveMenu = FindObjectOfType<ReviveMenu>();

        return reviveMenu;
    }

    /// <summary>
    /// Loads the ad.
    /// </summary>
    public void LoadAd()
    {
        // Clean up the old ad before loading a new one.
        if (_rewardedAd != null)
        {
            if (CanShowAd())
                return;
            DestroyAd();
        }

        Debug.Log("Loading rewarded ad.");

        // Create our request used to load the ad.
        var adRequest = new AdRequest();

        // Send the request to load the ad.
        RewardedAd.Load(_adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            // If the operation failed with a reason.
            if (error != null)
            {
                Debug.LogError("Rewarded ad failed to load an ad with error : " + error);
                return;
            }
            // If the operation failed for unknown reasons.
            // This is an unexpected error, please report this bug if it happens.
            if (ad == null)
            {
                Debug.LogError("Unexpected error: Rewarded load event fired with null ad and null error.");
                return;
            }

            // The operation completed successfully.
            Debug.Log("Rewarded ad loaded with response : " + ad.GetResponseInfo());
            _rewardedAd = ad;

            // Register to ad events to extend functionality.
            RegisterEventHandlers(ad);

            // Inform the UI that the ad is ready.
            // AdLoadedStatus?.SetActive(true);
        });
    }

    /// <summary>
    /// Shows the ad.
    /// </summary>
    public void ShowAd(Action doOnFail)
    {
        this.doOnFail = doOnFail;
        if (_rewardedAd != null && _rewardedAd.CanShowAd())
        {
            Debug.Log("Showing rewarded ad.");
            _rewardedAd.Show((Reward reward) =>
            {
                Debug.Log(String.Format("Rewarded ad granted a reward: {0} {1}",
                                        reward.Amount,
                                        reward.Type));
                GetReviveMenu().RevivePlayer();
                // P2-02: must clear the FIELD, not the parameter. The parameter of the same
                // name shadows it, so "doOnFail = null" only nulled the captured local while
                // this.doOnFail stayed set. OnAdFullScreenContentClosed then invoked the field,
                // scheduling CancelRevive() and ending the run the player had just paid for
                // with a completed rewarded ad.
                this.doOnFail = null;
            });
        }
        else
        {
            Debug.LogError("Rewarded ad is not ready yet.");
            LoadAd();
            doOnFail?.Invoke();
        }

        // Inform the UI that the ad is not ready.
        // AdLoadedStatus?.SetActive(false);
    }

    /// <summary>
    /// Destroys the ad.
    /// </summary>
    public void DestroyAd()
    {
        if (_rewardedAd != null)
        {
            Debug.Log("Destroying rewarded ad.");
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }

        // Inform the UI that the ad is not ready.
        // AdLoadedStatus?.SetActive(false);
    }

    /// <summary>
    /// Logs the ResponseInfo.
    /// </summary>
    public void LogResponseInfo()
    {
        if (_rewardedAd != null)
        {
            var responseInfo = _rewardedAd.GetResponseInfo();
            UnityEngine.Debug.Log(responseInfo);
        }
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked.");
        };
        // Raised when the ad opened full screen content.
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Rewarded ad full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded ad full screen content closed.");
            LoadAd();
            doOnFail?.Invoke();
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded ad failed to open full screen content with error : "
                + error);
            LoadAd();
            doOnFail?.Invoke();
        };
    }

    public bool CanShowAd()
    {
        if (_rewardedAd != null)
            return _rewardedAd.CanShowAd();
        return false;
    }
}
