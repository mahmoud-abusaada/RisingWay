using System.Collections;
using System.Collections.Generic;
using MobileVersionCode;
using UnityEngine;
using UnityEngine.Networking;

public class UpdateHandler : MonoBehaviour
{
    [SerializeField] private string URL;
    [SerializeField] private Canvas updateDialog;

    void Start()
    {
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
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.Log(request.error);
                ShowUpdateDialog();
            }
            else
            {
                UpdateVersions updateVersions = new UpdateVersions();
                updateVersions = JsonUtility.FromJson<UpdateVersions>(request.downloadHandler.text);
#if UNITY_IPHONE
                PlayerStats.Instance.setUpdateVersion(updateVersions.iosUpdateVersion);
                PlayerStats.Instance.setForceUpdateVersion(updateVersions.iosForceUpdateVersion);
#else
                PlayerStats.Instance.setUpdateVersion(updateVersions.androidUpdateVersion);
                PlayerStats.Instance.setForceUpdateVersion(updateVersions.androidForceUpdateVersion);
#endif
                Debug.Log(request.downloadHandler.text);
                Debug.Log(updateVersions.iosUpdateVersion);
                ShowUpdateDialog();
            }
        }
    }

    public void ShowUpdateDialog()
    {
        int updateVersion = PlayerStats.Instance.getUpdateVersion();
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();
        Debug.Log("Current version = " + VersionCode.GetVersionCode());

        updateDialog.gameObject.SetActive(updateVersion > currentVersion || forceUpdateVersion > currentVersion);
    }

    public void UpdateTheGame()
    {
#if UNITY_ANDROID
        Application.OpenURL(string.Format("market://details?id=" + Application.identifier));
#elif UNITY_IPHONE
        Application.OpenURL("itms-apps://itunes.apple.com/app/" + Application.identifier);
#endif
    }

    public void Cancel()
    {
        int forceUpdateVersion = PlayerStats.Instance.getForceUpdateVersion();
        int currentVersion = VersionCode.GetVersionCode();

        if (forceUpdateVersion > currentVersion)
            Application.Quit();
        else
            updateDialog.gameObject.SetActive(false);
    }
}
