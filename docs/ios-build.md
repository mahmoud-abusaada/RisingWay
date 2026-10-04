# Building Rising Way for iOS

The project is set up for iOS: player settings, icons, Info.plist entries and an Xcode
post-process step are in `Assets/Editor/IosBuild.cs`. Build from Unity on the Mac. Signing, the upload and the App Store Connect setup happen
there too.

## What the project already has

- **Bundle identifier:** set in Player Settings > iOS (see *Decisions* below). Build number 3,
  version 1.0.2, iOS 15.0 or later, ARM64, IL2CPP.
- **Icons:** every size comes from `Assets/Textures/RisingWay icon/ios_1024.png`, an RGB copy of
  the 1024 icon. The App Store refuses an icon with an alpha channel, even an opaque one.
- **Info.plist**, written after every iOS build:
  - `ITSAppUsesNonExemptEncryption = NO`, so no export-compliance question on each upload;
  - `NSUserTrackingUsageDescription`, the App Tracking Transparency text;
  - `NSPhotoLibraryAddUsageDescription`, for "Save Image" in the share sheet.
- **Xcode settings:** Bitcode off.
- **Notifications:** no permission prompt at launch. The game sends none.
- **Store links:** the "update" button opens the App Store listing, `id6473210923`.
- **Plugins:** AdMob (iOS app ID `ca-app-pub-4724664365967541~8899502658`, iOS ad units in
  `Assets/Scripts/Ads`), Firebase 13.16 and Unity IAP. Their native parts come in through
  CocoaPods.

## Building on the Mac

1. On the Mac, install **Unity 6000.5.7f1** with *iOS Build Support*, **Xcode** (latest, from the
   App Store), and **CocoaPods**. To install CocoaPods, run `sudo gem install cocoapods` or
   `brew install cocoapods`.
2. Copy the project over, or clone it from GitHub.
   - Git LFS is needed: run `git lfs install` before cloning.
   - The keystores are not in the repo, and iOS does not need them.
3. Open the project in Unity Hub. Then go to File > Build Profiles > iOS and click *Switch Platform*.
4. Click **Tools > Rising Way > iOS settings**, once.
5. Click **Build**, choose a folder (for example `Builds/iOS/Xcode`) and wait. Unity runs
   `pod install` itself.
6. Open the **`.xcworkspace`** file, not the `.xcodeproj`. The pods live only in the workspace.

## Not an option: exporting from Windows

`IosBuild.Export` does export an Xcode project on Windows, and the export succeeds. It was done
on 2026-10-03 to check that the project builds for iOS. But Firebase refuses iOS builds on
Windows ("Firebase iOS builds are not supported on Windows"), so that project has no Firebase
pods. Build from Unity on the Mac (above). `Builds/iOS/Xcode` is only that check: do not ship it.

## In Xcode

1. Select the **Unity-iPhone** target. Under *Signing & Capabilities*, tick *Automatically manage
   signing* and choose your **Team**. Do the same for **UnityFramework** if Xcode asks.
2. Add the **In-App Purchase** capability, with the + Capability button. The IAP products need it.
3. Set the device to *Any iOS Device (arm64)*, then choose Product > **Archive**.
4. In the Organizer, choose *Distribute App* > *App Store Connect* > *Upload*.
5. If App Store Connect says the build number has been used, raise *Build* under the target's
   General tab and archive again.

## In App Store Connect (developer account renewed)

- **The app:** if the old record (`id6473210923`) uses `com.abusaada.risingway`, upload to it.
  Otherwise make a new one (see *Decisions*).
- **In-app purchases:** the product IDs must match exactly. The game asks for these 21:
  - `remove_ads` (non-consumable);
  - `diamonds_1`–`_4`, `chances_1`–`_4`, `bolts_1`–`_4`, `double_points_1`–`_4` and `boxes_1`–`_4`
    (consumables).
  If they exist from before, check that each is *Ready to Submit*. Add new ones to the version
  before submitting.
- **App Privacy:** the answers match the Play Data Safety form. AdMob collects device IDs, usage
  data and coarse location for ads; Firebase and Unity collect analytics and crash data. Tracking
  is *Yes* if users allow it. The privacy policy URL is `https://abusaada.com/privacy`.
- **Age rating:** answer the questionnaire again. There are ads, simulated gambling (no) and loot
  boxes. The mystery box is paid and random, and the odds are shown in the game.
- **Screenshots:** you need 6.9" and 6.5" iPhone screenshots, and iPad ones if iPad stays
  supported. The store art tooling can make them (`StoreCapture`, `-shotSize 1320x2868`).

## Decisions

1. **Bundle identifier: `com.abusaada.risingway`**, the same as Android. This was decided on
   2026-10-03. If the old App Store app (`id6473210923`) was made with another bundle ID, it
   cannot take this build. In that case:
   - Make a **new app record** in App Store Connect with `com.abusaada.risingway`, after
     registering the ID under Certificates, Identifiers & Profiles.
   - Create the 21 in-app purchase products in the new record.
   - Put the new App Store ID in three places, in place of `6473210923`:
     `Assets/Scripts/Menu/MainMenu.cs` and `Assets/Scripts/Utils/UpdateHandler.cs` (the
     "update" button), and `Assets/Scripts/Utils/ShareManager.cs` (the share link).
2. **Firebase.** `Assets/GoogleService-Info.plist` is for `com.AbuSada.RisingWay`. Add an
   **iOS app with `com.abusaada.risingway`** in the Firebase console (the same project as
   Android), download its `GoogleService-Info.plist`, and replace the one in `Assets/`. Until then
   Firebase Analytics does not start on iOS (the build logs a bundle ID warning); the game still
   runs.
3. **AdMob.** The iOS AdMob app (`~8899502658`) can stay; link it to the App Store listing once
   the app is live. To show Apple's tracking prompt, publish an *IDFA explainer* message under
   Privacy & messaging. The consent code (UMP) shows it, then Apple's prompt. Without it there is
   no prompt, and the ads are not personalised.
