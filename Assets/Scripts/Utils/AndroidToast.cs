using UnityEngine;

/// <summary>Android's own short message at the bottom of the screen. Does nothing elsewhere.</summary>
public static class AndroidToast
{
    public static void Show(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaClass toast = new AndroidJavaClass("android.widget.Toast");
                toast.CallStatic<AndroidJavaObject>("makeText", activity, text, 0 /* LENGTH_SHORT */).Call("show");
            }));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Toast] " + e.Message);
        }
#else
        Debug.Log("[Toast] " + text);
#endif
    }
}
