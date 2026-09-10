# P1-05 — SDK Updates

**Status:** GMA and EDM4U done. Firebase outstanding but no longer blocking. The project-path
blocker is RESOLVED — the Android build is green.

---

## Done

### Google Mobile Ads 9.1.0 → 11.5.0

This was blocking three of the five P1-04 build failures, and updating it resolved all three:

| Old blocker | Why it is gone |
|---|---|
| `validate_dependencies.gradle` fails on Groovy 4 | The file no longer exists. Google removed it in v10 — the `AD_SERVICES_CONFIG` issue it worked around was fixed in GMA Android SDK 24.0.0 |
| `aarArtifacts` resolved without an exclusive lock | Same file, same removal |
| `googlemobileads-unity.aar` declares `targetSdkVersion 9` | New AAR |

Dependency deltas after re-resolution:

- `play-services-ads` 23.1.0 → **25.4.0**
- `user-messaging-platform` 2.2.0 → **4.0.0**
- added `androidx.fragment:1.7.1`, `androidx.lifecycle:lifecycle-process:2.6.2`
- iOS: `unity-plugin-library.a` → `unity-plugin-library.xcframework`

**The `AD_SERVICES_CONFIG` question from the audit is now closed.** It was half of Google's own
GMA v8.6.0 workaround, applied twice — once as a `<meta-data>` removal in the app manifest, once
by rewriting a `<property>` node inside the AAR. Both are obsolete and both are gone.

### EDM4U 1.2.179 → 1.2.188

Bundled with GMA, so it came along for free. Two things worth knowing:

1. **It understands the Unity 6 Gradle layout.** It injected its repositories into
   `dependencyResolutionManagement` inside `settingsTemplate.gradle` — confirming that file was
   genuinely required, not just tidier.
2. **It re-injects `android.enableJetifier` on every resolve** unless told not to.
   `GooglePlayServices.UseJetifier` is now `False` in `ProjectSettings/GvhProjectSettings.xml`.
   Ticking the Jetifier checkbox in Android Resolver settings will silently undo the P1-04 fix
   and break the build — the properties template carries a warning to that effect.

### GoogleMobileAdsSettings restored

GMA 10+ injects the AdMob App ID at build time from `GoogleMobileAdsSettings.asset` rather than
hardcoding it in the plugin manifest. The old asset was deleted with the old plugin, so both IDs
were recovered from git history:

- Android `ca-app-pub-4724664365967541~1578717732`
- iOS `ca-app-pub-4724664365967541~8899502658`

**Without this the app builds fine and then crashes on launch**, because GMA refuses to
initialise with no app ID. Worth re-checking in the Editor under
*Assets > Google Mobile Ads > Settings*.

### P2-09 — child-directed tag removed

Forced by this update (the mediation namespaces disappeared) and previously agreed:

- Removed `TagForChildDirectedTreatment.True` and `MaxAdContentRating.G`, which applied
  child-directed treatment to **every** user and suppressed ad revenue on a general-audience game
- Removed hardcoded consent declarations for six mediation networks — `IronSource.SetConsent`,
  `AppLovin.SetHasUserConsent`, `LiftoffMonetize.SetGDPRStatus`, `InMobi.UpdateGDPRConsent`,
  `Chartboost.AddDataUseConsent`, plus CCPA/do-not-sell flags. These declared consent on the
  user's behalf before any dialog was shown
- Dropped the deprecated `MobileAds.SetiOSAppPauseOnBackground`

**Two follow-ups this creates.** P2-07 must wire up the real UMP consent flow —
`GoogleMobileAdsConsentController` already exists and is still unattached. And P9-02 must align
the store listing age rating and Data Safety declarations with the general-audience decision.

---

## Deliberately deferred: mediation adapters

All seven (AppLovin, Chartboost, InMobi, IronSource, Liftoff Monetize, Meta Audience Network,
Unity Ads) were removed so the core plugin could be brought up in isolation. They were built
against GMA 9.x, each pulls its own Android dependencies, and P0-04 never established which of
them actually earn.

**To restore:** revert the Mediation deletions in commit `537bc05`. Better: re-add current
versions once the build is green, guided by whatever AdMob revenue data exists.

Note this means the current build has **no mediation demand** — AdMob direct only.

---

## Outstanding: Firebase 11.6.0

Untouched. Still resolving `firebase-analytics:21.3.0`, `firebase-app-unity:11.6.0`,
`firebase-common:20.3.3`. It is also the source of the current blocker below.

---

## RESOLVED: spaces in the project path

The build failed at `:launcher:checkReleaseDuplicateClasses` with:

```
Cannot convert URI 'file:///C:/Work/Unity Projects/Rising Way/Assets/GeneratedLocalRepo/Firebase/m2repository' to a file.
```

Firebase ships its Android artifacts as a **local Maven repository** rather than pulling from a
remote, and EDM4U declares it as a `file:///` URL built by string concatenation:

```groovy
def unityProjectPath = $/file:///**DIR_UNITYPROJECT**/$.replace("\\", "/")
maven { url (unityProjectPath + "/Assets/GeneratedLocalRepo/Firebase/m2repository") }
```

The old path `C:\Work\Unity Projects\Rising Way` contained two spaces. Unencoded spaces make an
invalid URI, and Gradle 9 refuses to convert it where older Gradle tolerated it.

### The hypothesis was tested before acting

Rather than move the project on a hunch, the two paths were fed to Unity's own bundled Gradle
9.1.0 directly:

```
FAIL -> file:///C:/Work/Unity Projects/Rising Way/...
        URISyntaxException: Illegal character in path at index 21
OK   -> file:///C:/Work/RisingWay/...
```

Worth stating plainly, because it is counter-intuitive: Unity's *own* default project location is
`C:\Users\<name>\Documents\Unity Projects`, which also contains spaces. This is not a
long-standing rule anyone should have known — Gradle 9.1.0 is new and tightened URI validation,
so this likely bites everyone on Unity 6.5 + Firebase + a spaced path.

### The cheaper fix was tried first, and did not work

EDM4U exposes `useFullCustomMavenRepoPathWhenExport` / `...WhenNotExport`, which sound like they
would produce a relative path. Setting them achieved nothing, and the reason is worth recording so
nobody tries it again: `projectExportEnabled` is `False` for a normal Unity build, so the
governing setting was `...WhenNotExport`, and that was **already** `False`. Neither flag avoids an
absolute URI — the `**DIR_UNITYPROJECT**` token expands to an absolute path either way.

### Resolution

The project was moved to **`C:\Work\RisingWay`**. Same-volume rename, so it completed in 0.1s and
the git repository travelled with it intact. Nothing tracked in the repo hardcoded the old path.

The stale generated Gradle project under `Library/Bee/Android/Prj/IL2CPP/Gradle` had the old path
baked in and was deleted so it would regenerate; the 6.9 GB IL2CPP artifact cache was kept.

**Verified.** The regenerated `settings.gradle` now reads:

```groovy
def unityProjectPath = $/file:///C:/Work/RisingWay/$.replace("\\", "/")
```

and the build produced a **124 MB APK** — the first green Android build since the Unity 6
migration. The `Cannot convert URI` error is gone.

**Consequences for everyone else:**

- The project must be re-added in Unity Hub at its new location.
- `C:\Work\Unity Projects\Keys` did **not** move. See IF-02 in `incidental-findings.md`.
- Never put this project back on a path containing spaces.

---

## Side effects observed on the first green build

Two things surfaced that are unrelated to the SDK work and are recorded in
`incidental-findings.md` rather than fixed here:

- **IF-01** — Active Input Handling is "Both" while nothing uses the new Input System.
- **IF-03** — the build cleared `preloadedAssets`, dropping Adaptive Performance's settings.
  `ProjectSettings.asset` was restored; expect the clear to recur until a decision is made.

---

## Next

1. ~~Decide on the project path.~~ Done — moved to `C:\Work\RisingWay`.
2. Update Firebase 11.6.0 → current. **Needs approval**: it means downloading the SDK from Google.
   Note the build is green *without* this, so it is no longer urgent.
3. Re-add mediation adapters deliberately, at current versions.
4. Install the APK on a device and confirm it actually launches. A green build is not a working
   game — GMA needs its App ID present at runtime, and that has not been exercised yet.
