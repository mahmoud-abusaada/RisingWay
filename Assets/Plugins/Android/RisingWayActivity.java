package com.abusaada.risingway;

import android.os.Build;
import android.os.Bundle;
import android.window.OnBackInvokedCallback;
import android.window.OnBackInvokedDispatcher;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

/**
 * The game's activity: Unity's own, plus one thing - every back press goes to the game.
 *
 * Unity only hears back as the Escape key, and on a Galaxy S20 (Android 13, gesture navigation)
 * the press never reached it from the Settings screen: the system took it as unhandled and sent
 * the whole app to the background (logcat: BackNavigationInfo TYPE_RETURN_TO_HOME, then
 * moveTaskToBack). Here the activity itself takes the press - through an OnBackInvokedCallback on
 * Android 13 and later (the manifest opts in with enableOnBackInvokedCallback="true"), and through
 * onBackPressed before that - and hands it to InputManager.OnAndroidBack on the GameManager object,
 * which does what the menus decide (MenusController.handleSystemBack). The app now leaves only
 * when the game says so (press back twice on the main menu).
 */
public class RisingWayActivity extends UnityPlayerActivity {
    private OnBackInvokedCallback backCallback;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (Build.VERSION.SDK_INT >= 33) {
            backCallback = this::sendBackToGame;
            getOnBackInvokedDispatcher().registerOnBackInvokedCallback(OnBackInvokedDispatcher.PRIORITY_DEFAULT, backCallback);
        }
    }

    @Override
    protected void onDestroy() {
        if (Build.VERSION.SDK_INT >= 33 && backCallback != null)
            getOnBackInvokedDispatcher().unregisterOnBackInvokedCallback(backCallback);
        super.onDestroy();
    }

    @Override
    @SuppressWarnings("deprecation")
    public void onBackPressed() {
        sendBackToGame();
    }

    private void sendBackToGame() {
        UnityPlayer.UnitySendMessage("GameManager", "OnAndroidBack", "");
    }
}
