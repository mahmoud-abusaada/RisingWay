# Owner actions — things only you can do

Everything here needs a console login, a judgement call, or the Unity Editor's UI. Code-side work
is tracked in the plan; this file is the part that cannot be done from the repo.

Items marked **BLOCKS RELEASE** must be done before the Play Store will accept the app.

---

## 1. AdMob console — https://admob.google.com

### 1.1 Create the consent messages — **BLOCKS RELEASE** (P2-07)

The consent code is now in the game, but the UMP SDK only **displays** a message you configure
here. Until one exists, EEA/UK users see no form and **get no ads at all**.

- ✅ **Privacy & messaging → European regulations** — **published and verified.** A debug-EEA run on
  the Pixel 10 Pro emulator shows the form with "Do not consent" on the first layer; both Consent
  and Do not consent initialise ads correctly (declining gives Limited Ads, not zero ads).
- ✅ **Privacy & messaging → US state regulations** - done (2026-09-17).
- You will be asked for a **privacy policy URL** — see 3.1.

### 1.2 Clear "Account not approved yet"

Every ad request currently fails with this. Check, in order:

- **Payments** — tax information and payment profile complete.
- **Policy center** — no open issues.
- **Apps** — the app shows as linked. AdMob reviews an app against its **live** store listing, and
  Rising Way is currently delisted, so full approval may only complete after the relaunch. Test
  ads keep working in the meantime.

### 1.3 app-ads.txt

AdMob → Apps → *Rising Way* → **app-ads.txt** shows one line to publish. It must be served from the
**developer website** set in Play Console, i.e. `https://abusaada.com/app-ads.txt`. Apps without it
receive noticeably less demand.

`web/app-ads.txt` already contains
`google.com, pub-4724664365967541, DIRECT, f08c47fec0942fa0`. Check it matches what AdMob shows,
then make sure **Play Console → Store settings → Website** is set to `https://abusaada.com`,
since AdMob looks for the file on exactly that domain.

---

## 2. Google Play Console — https://play.google.com/console

### 2.1 Privacy policy URL — **BLOCKS RELEASE**

The URL dying is what got the app delisted. Set it under **Policy → App content → Privacy policy**
once the page exists (3.1).

### 2.2 Target audience and content — done for the age groups (P9-02)

✅ **Target age groups: 13+** - done (2026-09-17). The rest of this section still applies.

The child-directed ad tag was removed in P2-09, so the listing must agree:

- **Target age groups**: 13+ only. Do **not** select any under-13 group.
- **Appeals to children**: No.
- If this still says the game targets children, Google will expect child-directed ad handling that
  the build no longer has — a policy mismatch.

### 2.3 Data safety form — **BLOCKS RELEASE**

**"Does it have to list Unity as well as Google?"** The form never asks for company names -
there is nowhere to type "Google" or "Unity". What it asks is which **data types** the app
collects or shares, counting everything its SDKs do, and why. So the answer is: Unity is not
named anywhere, but the data types Unity's SDKs collect must be included in your answers, and
they are the same types Google's SDKs collect, so in practice Unity adds nothing new to tick.

Declare:

| Data type (Play's wording) | Collected | Shared | Purposes | Who actually collects it |
|---|---|---|---|---|
| Device or other IDs | Yes | Yes | Advertising or marketing, Analytics, Fraud prevention | AdMob (advertising ID), Firebase Analytics (app instance ID, advertising ID), Unity (device ids) |
| Location → Approximate location | Yes | Yes | Advertising or marketing, Analytics | AdMob and Firebase derive a coarse location from the IP address |
| App activity → App interactions | Yes | Yes | Advertising or marketing, Analytics | AdMob (ad interactions), Firebase (screens, sessions) |
| App info and performance → Crash logs | Yes | No | Analytics | Unity crash reporting (Cloud Diagnostics) |
| App info and performance → Diagnostics | Yes | No | Analytics | Unity crash reporting, AdMob SDK performance data |
| Financial info → Purchase history | Yes | No | App functionality, Analytics | Firebase logs in-app purchase events automatically |

Notes on the two columns people get wrong:

- **Shared** means sent to another company that uses it for their own purposes. Advertising data
  sent to Google for ad selection is the clear case, so the rows above that feed advertising are
  marked shared. Unity's crash reporting is a service provider processing on your behalf, which
  Play's definition excludes from "shared" - Unity's own guidance says shared: No.
- **Processed ephemerally**: no. **Data is encrypted in transit**: yes (both Google and Unity
  state this). **Users can request deletion**: there is no account system, so the honest answer
  is that data is tied to the device; a player can reset their advertising ID or uninstall.

Sources: Google's AdMob data disclosure page, Google Analytics for Firebase's automatic
collection list, and Unity's Cloud Diagnostics data safety page.

**What Unity actually collects in this build** (checked in code, not assumed):

- **Unity crash reporting (Cloud Diagnostics)** is ON (`UnityConnectSettings.asset`,
  `m_EnableCloudDiagnosticsReporting: 1`) - crash logs, diagnostics, device identifiers. This is
  the only Unity data collection left, and it is worth keeping for the relaunch.
- **Unity Analytics is gone** (2026-09-18). Legacy Analytics is switched off and both analytics
  packages are removed: the legacy service **stopped accepting data on 1 February 2024**, and the
  UGS Analytics package collected nothing because the game never started it. Firebase Analytics is
  the game's analytics.
- So the "App interactions" row is AdMob and Firebase only; the crash and diagnostics rows are
  Unity crash reporting.

### 2.4 Ads declaration

**Policy → App content → Ads** → "Yes, my app contains ads".

### 2.5 Content rating

Complete the IARC questionnaire if the old rating is stale.

### 2.6 Release signing

Committed settings no longer sign builds (decision B). When cutting a release:

- **Player Settings → Publishing Settings → Custom Keystore** → select
  `C:\Work\Unity Projects\Keys\RisingWay.keystore`, alias `risingway`, enter both passwords.
- Build the **AAB** (`RisingWayBuilder.BuildAndroidAab`), not the APK.
- Untick Custom Keystore again afterwards so it does not get committed.

---

## 3. Website — abusaada.com (Cloudflare)

Parked by choice, but 2.1 and 1.3 both depend on it.

### 3.1 Privacy policy page — **BLOCKS RELEASE**

**Written:** `web/privacy.html`, served at **`https://abusaada.com/privacy`** — use that URL in
AdMob and Play Console. It was written from what the code actually does and names Google AdMob,
Google UMP, Firebase Analytics, Unity Analytics / Gaming Services, Unity crash reporting and the
store billing systems.

Contact email is set to `mahmoudr1996@gmail.com`. **Ready to deploy.**

It is not legal advice. Read it through once before publishing.

Optional: a personal address on a public page attracts spam. Since the domain is on Cloudflare,
**Email Routing** (free) can create `privacy@abusaada.com` forwarding to that inbox, and the page
can list that instead.

### 3.2 Deploy `web/` to Cloudflare Pages

`web/` now holds only public files: `privacy.html`, `app-ads.txt`, `_headers`,
`rising/version.php`. Upload it as described in `docs/website.md`.

`app-ads.txt` is already written, using the publisher ID from the project's AdMob app IDs
(`pub-4724664365967541`, shared by the Android and iOS apps). Compare it once against the line
AdMob shows you in 1.3.

---

## 4. Unity Editor — needs the GUI

### 4.1 Add the Privacy Options button — ✅ done (2026-09-17, commit 1e2f1ef)

The menu command below was run and the button is in the scene; it is described here in case the
Settings panel is rebuilt.


Google requires EEA/UK users to be able to change consent from inside the app, and **the
published consent form already promises this button exists** - it tells users to "look for a link
or button in the app menu to manage or withdraw consent". Shipping without it breaks a statement
the app itself makes.

**This is now a menu command, not hand-editing.** In the Editor:

**Rising Way → Add Privacy Options Button**

It copies the Settings panel's Back button, puts it in a new row at the bottom of the "Other"
section, labels it PRIVACY OPTIONS, wires it to `SettingsMenu.OpenPrivacyOptions`, and saves the
scene. Running it twice does nothing the second time.

It hides itself for users outside regulated regions, and the section only grows by that row when
it is shown, so you will not see it from home - that is expected. To see it, run a development
build with the EEA debug setting (the emulator's hashed id is already in `AdmobManager`).

### 4.2 After any Unity session, check `git status` — mostly fixed

The cause of the recurring `.mat` changes is fixed (IF-05): runtime code was editing the floor
material **assets** in place, so `Dark Grey.mat` came back with a render queue of 2010 after every
session. Materials now get a runtime copy.

Two reasons to still glance at `git status`:

- **`Assets/UniversalRenderPipelineAsset.asset`** - `GraphicsManager` still writes `renderScale`
  and `supportsHDR` onto the pipeline asset when the density slider or HDR toggle is used, so
  playing in the Editor can rewrite that file. Not yet fixed: the right fix is a decision, not a
  mechanical change.
- **`Earth Water.mat`** - URP re-applies *Preserve Specular Lighting* when it re-saves a
  transparent material, which is the white-sky bug again.
- **`Assets/GoogleMobileAds/Editor/Resources/PlaceholderAds/*.prefab`** - Unity re-saves these
  third-party prefabs into the current format whenever the Editor shows a placeholder ad. Harmless,
  but noise.

If any of those show up modified and you did not change them, discard the change.

### 4.3 Close Unity before asking for a build

Command-line builds cannot run while the Editor holds the project open.

---

## 5. Keystore

- **Back up `C:\Work\Unity Projects\Keys\RisingWay.keystore` off this machine.** Losing it means
  the Play listing can never be updated under the same package name.
- Store its **password and alias password** in a password manager. A backed-up keystore without
  its password is equally unusable.

---

## 6. Decisions made — do not revisit

- **Unity Analytics is removed, Unity crash reporting stays** (2026-09-18). Legacy Unity Analytics
  stopped accepting data on 1 February 2024, and the UGS Analytics package was never started by
  the game, so neither collected anything. Firebase Analytics covers analytics; Cloud Diagnostics
  is the only crash reporting the game has, so it stays until Firebase Crashlytics replaces it.

- **Package name stays `com.AbuSada.RisingWay`.** On Google Play the package name *is* the app;
  it cannot be renamed, and a new one is a brand-new app. It is also bound to
  `google-services.json`, all 21 IAP products and existing Remove Ads purchases. Lowercase would
  be purely cosmetic.

## 7. Before the iOS build (on the Mac)

- **The iOS bundle ID does not match Firebase.** Unity's iOS identifier is
  `com.Abu-Sa-da.Rising-Way`, but `Assets/GoogleService-Info.plist` is registered for
  `com.AbuSada.RisingWay`. Firebase will not initialise on iOS until they agree. Check which
  bundle ID the live App Store app (`id6473210923`) actually uses: keep that one, and download a
  matching `GoogleService-Info.plist` from the Firebase console if needed. Do **not** change the
  App Store app's bundle ID - that has the same can't-rename rule as Android.

## 8. Optional

- **Hibernation wake-up**: the PC woke three seconds after hibernating. From an elevated prompt,
  `powercfg /waketimers`, then consider disabling wake on the Realtek network adapter
  (Device Manager → adapter → Power Management).
