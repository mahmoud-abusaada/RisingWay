using System.Collections;
using System.Collections.Generic;
using MobileVersionCode;
using UnityEngine;
using UnityEngine.Networking;

// P8-03: this class owns the update dialog's BUTTONS, even though MainMenu owns the fetch.
// The dialog's "Update!" and "Cancel" are wired in the scene to UpdateHandler.UpdateTheGame and
// UpdateHandler.Cancel, so fixing MainMenu alone would have left the stuck-dialog bug in place.
// Both classes now agree via Utility.remoteConfigLoaded.
public class UpdateHandler : MonoBehaviour
{
    [SerializeField] private string URL;
    [SerializeField] private Canvas updateDialog;

    void Start()
    {
        // Deliberately not fetching here: MainMenu.GetData() is the live path, and two
        // independent fetchers writing the same PlayerPrefs keys would race.
        // GetData();
    }

    public void GetData()
    {
        StartCoroutine(FetchData());
    }

    public IEnumerator FetchData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(URL))
        {
            yield return request.SendWebRequest();

            // Fail OPEN. Only Result.Success counts - a 404 or 500 is a ProtocolError, and
            // feeding an error page to JsonUtility.FromJson throws and kills this coroutine.
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Remote config unavailable ({request.result}): {request.error}. Continuing without it.");
                HideUpdateDialog();
                yield break;
            }

            UpdateVersions updateVersions = null;
            try
            {
                updateVersions = JsonUtility.FromJson<UpdateVersions>(request.downloadHandler.text);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Remote config was not valid JSON, ignoring it: {e.Message}");
            }

            if (updateVersions == null)
            {
                HideUpdateDialog();
                yield break;
            }

#if UNITY_IPHONE
            PlayerStats.Instance.setUpdateVersion(updateVersions.iosUpdateVersion);
            PlayerStats.Instance.setForceUpdateVersion(updateVersions.iosForceUpdateVersion);
#else
            PlayerStats.Instance.setUpdateVersion(updateVersions.androidUpdateVersion);
            PlayerStats.Instance.setForceUpdateVersion(updateVersions.androidForceUpdateVersion);
#endif

            Utility.remoteConfigLoaded = true;
            ShowUpdateDialog();
        }
    }

    private void HideUpdateDialog()
    {
        updateDialog.gameObject.SetActive(false);
    }

    public void ShowUpdateDialog()
    {
        if (!Utility.remoteConfigLoaded)
        {
            HideUpdateDialog();
            return;
        }

        int updateVersion = PlayerStats.Instance.getUpdateVersion();
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();

        updateDialog.gameObject.SetActive(updateVersion > currentVersion || forceUpdateVersion > currentVersion);
    }

    public void UpdateTheGame()
    {
#if UNITY_ANDROID
        Application.OpenURL(string.Format("market://details?id=" + Application.identifier));
#elif UNITY_IPHONE
        Application.OpenURL("itms-apps://apps.apple.com/app/id6473210923"); // the App Store id, not the bundle id
#endif
    }

    // This is the method the dialog's Cancel button actually calls.
    //
    // It used to read forceUpdateVersion straight from PlayerPrefs and call Application.Quit()
    // whenever a cached value exceeded the installed build - so once the domain lapsed, Cancel
    // quit the game instead of closing the dialog. In the Editor Quit() does nothing at all,
    // which is why the dialog appeared frozen rather than doing anything.
    //
    // Now a force-update is only honoured when a fetch actually succeeded this session.
    public void Cancel()
    {
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();

        if (Utility.remoteConfigLoaded && forceUpdateVersion > currentVersion)
            Application.Quit();
        else
            HideUpdateDialog();
    }
}
