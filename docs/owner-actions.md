# Owner actions — things only you can do

Everything here needs a console login, a judgement call, or the Unity Editor's UI. Code-side work
is tracked in the plan; this file is the part that cannot be done from the repo.

Items marked **BLOCKS RELEASE** must be done before the Play Store will accept the app.

---

## 1. AdMob console — https://admob.google.com

### 1.1 Create the consent messages — **BLOCKS RELEASE** (P2-07)

The consent code is now in the game, but the UMP SDK only **displays** a message you configure
here. Until one exists, EEA/UK users see no form and **get no ads at all**.

- **Privacy & messaging → European regulations** → Create message → attach the Rising Way app →
  publish.
- **Privacy & messaging → US state regulations** → Create message → publish. Several US states now
  require an opt-out; the SDK handles it once this exists.
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

### 2.2 Target audience and content — **BLOCKS RELEASE** (P9-02)

The child-directed ad tag was removed in P2-09, so the listing must agree:

- **Target age groups**: 13+ only. Do **not** select any under-13 group.
- **Appeals to children**: No.
- If this still says the game targets children, Google will expect child-directed ad handling that
  the build no longer has — a policy mismatch.

### 2.3 Data safety form — **BLOCKS RELEASE**

Declare what the app actually collects:

| Data | Collected | Shared | Why |
|---|---|---|---|
| Device or other IDs (advertising ID) | Yes | Yes (Google AdMob, Firebase, Unity) | Advertising, analytics |
| App interactions | Yes | Yes (Google Firebase, Unity Analytics) | Analytics |
| Crash logs / diagnostics | Yes | Yes (Unity crash reporting) | Analytics, app stability |
| Purchase history | Yes | No | App functionality (in-app purchases) |

**Unity is a data recipient too, not just Google.** `UnityConnectSettings.asset` has Unity
Analytics enabled and initialising on startup, and Unity crash reporting enabled;
`com.unity.services.analytics` is installed and `UnityServices.InitializeAsync()` runs. The P3-02
plan says Unity Analytics is being removed, but it is still switched on. Until it is actually
removed, it must be declared here and it is named in the privacy policy.

Data is encrypted in transit: **Yes**. Users can request deletion: answer honestly — there is no
account system, so data is tied to the device, not a login.

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

### 4.1 Add the Privacy Options button — **BLOCKS RELEASE** (P2-07)

Google requires EEA/UK users to be able to change consent from inside the app. The code is ready;
the button is not, because hand-editing scene YAML to create UI is unsafe.

1. Open **SampleScene**, select the **Settings** panel.
2. Duplicate an existing button, rename it **Privacy Options**, set its label.
3. On the button's **OnClick**, add the Settings panel object → `SettingsMenu.OpenPrivacyOptions`.
4. On the **SettingsMenu** component, drag the new button into **Privacy Options Button**.
5. Save the scene.

It hides itself automatically for users outside regulated regions, so you will not see it from
home — that is expected.

### 4.2 After any Unity session, check `git status` for `.mat` changes

Two known problems rewrite material files behind your back:

- Playing in the Editor can write a runtime render queue into a material **asset** —
  `Dark Grey.mat` went from 3000 to 2010.
- URP re-applies *Preserve Specular Lighting* when it re-saves a transparent material —
  `Earth Water.mat` gained it, which is the white-sky bug again.

If either file shows up modified and you did not change it, discard the change.

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
