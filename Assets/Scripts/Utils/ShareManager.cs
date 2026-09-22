using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ShareManager : MonoBehaviour
{
    public static ShareManager Instance;
    // The Android link follows the package name, so a rename cannot leave it pointing at the old
    // listing (it did: the package became com.abusaada.risingway for the 2026 relaunch).
    private string gameLink = "\nAndroid: https://play.google.com/store/apps/details?id=" + Application.identifier + "\n" +
                              "iOS: https://apps.apple.com/us/app/rising-way/id6473210923";
    private bool isProcessing = false;

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
    }

    public void Share(string title, string text = "")
    {
        if (!isProcessing)
        {
            StartCoroutine(ShareForMobile(title, text));
            // #if UNITY_ANDROID
            //             StartCoroutine(ShareForAndroid(title, text));
            // #elif UNNITY_IPHONE

            // #else

            // #endif
        }
    }

    private IEnumerator ShareForMobile(string title, string text = "")
    {
        yield return new WaitForEndOfFrame();

        Texture2D ss = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        ss.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        ss.Apply();

        string filePath = Path.Combine(Application.temporaryCachePath, "RisingWay.png");
        File.WriteAllBytes(filePath, ss.EncodeToPNG());

        // To avoid memory leaks
        Destroy(ss);

        new NativeShare().AddFile(filePath)
            .SetSubject(title).SetText(text).SetUrl(gameLink)
            .SetCallback((result, shareTarget) => Debug.Log("Share result: " + result + ", selected app: " + shareTarget))
            .Share();
        Debug.Log("Sharing");
        // Share on WhatsApp only, if installed (Android only)
        //if( NativeShare.TargetExists( "com.whatsapp" ) )
        //	new NativeShare().AddFile( filePath ).AddTarget( "com.whatsapp" ).Share();
    }

    IEnumerator ShareAndroidText(string title, string text)
    {
        isProcessing = true;
        yield return new WaitForEndOfFrame();
        //execute the below lines if being run on a Android device
        //Reference of AndroidJavaClass class for intent
        AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        //Reference of AndroidJavaObject class for intent
        AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
        //call setAction method of the Intent object created
        intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
        //set the type of sharing that is happening
        intentObject.Call<AndroidJavaObject>("setType", "text/plain");
        //add data to be passed to the other activity i.e., the data to be sent
        intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TITLE"), title);
        intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text + " " + gameLink);
        //get the current activity
        AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        //start the activity by sending the intent data
        AndroidJavaObject jChooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share Via");
        currentActivity.Call("startActivity", jChooser);

        // ScreenCapture.CaptureScreenshot("RisingWay.png");
        // string destination = Path.Combine(Application.persistentDataPath, "RisingWay.png");

        // if (!Application.isEditor)
        // {
        //     AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        //     AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
        //     intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
        //     AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
        //     AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("parse", "file://" + destination);
        //     intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject);
        //     intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text + " " + gameLink);
        //     intentObject.Call<AndroidJavaObject>("setType", "image/jpeg");
        //     AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        //     AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        //     AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser",
        //         intentObject, "Share your new score");
        //     currentActivity.Call("startActivity", chooser);

        // }
        isProcessing = false;
    }

    public IEnumerator ShareForAndroid(string title, string text)
    {
        isProcessing = true;
        // wait for graphics to render
        yield return new WaitForEndOfFrame();

        string screenshotName = "RisingWay.png";
        string screenShotPath = Application.persistentDataPath + "/" + screenshotName;
        ScreenCapture.CaptureScreenshot(screenshotName, 1);
        yield return new WaitForSeconds(0.5f);

        if (!Application.isEditor)
        {
            //current activity context
            AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");

            //Create intent for action send
            AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
            AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
            intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));

            //old code which is not allowed in Android 8 or above
            //create image URI to add it to the intent
            //AndroidJavaClass uriClass = new AndroidJavaClass ("android.net.Uri");
            //AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject> ("parse", "file://" + screenShotPath);

            //create file object of the screenshot captured
            AndroidJavaObject fileObject = new AndroidJavaObject("java.io.File", screenShotPath);

            //create FileProvider class object
            AndroidJavaClass fileProviderClass = new AndroidJavaClass("androidx.core.content.FileProvider");

            object[] providerParams = new object[3];
            providerParams[0] = currentActivity;
            // The manifest declares this provider as "<application id>.provider", so read it from
            // there rather than writing the package name out again.
            providerParams[1] = Application.identifier + ".provider";
            providerParams[2] = fileObject;

            //instead of parsing the uri, will get the uri from file using FileProvider
            AndroidJavaObject uriObject = fileProviderClass.CallStatic<AndroidJavaObject>("getUriForFile", providerParams);

            //put image and string extra
            intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), uriObject);
            intentObject.Call<AndroidJavaObject>("setType", "image/png");
            intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), title);
            intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);

            //additionally grant permission to read the uri
            intentObject.Call<AndroidJavaObject>("addFlags", intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));

            AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share your high score");
            currentActivity.Call("startActivity", chooser);
        }

        // yield return new WaitUntil(() => isFocus);
        isProcessing = false;
    }
}
