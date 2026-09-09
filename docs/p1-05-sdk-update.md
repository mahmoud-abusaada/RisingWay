# P1-05 — SDK Updates

**Status:** GMA and EDM4U done. Firebase outstanding. **Blocked on a project-path issue** that
is unrelated to any SDK.

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

## Current blocker: spaces in the project path

```
Execution failed for task ':launcher:checkReleaseDuplicateClasses'.
> Cannot convert URI 'file:///C:/Work/Unity Projects/Rising Way/Assets/GeneratedLocalRepo/Firebase/m2repository' to a file.
```

Firebase ships its Android artifacts as a **local Maven repository** rather than pulling from a
remote. EDM4U declares it as a `file:///` URL built by string concatenation:

```groovy
def unityProjectPath = $/file:///**DIR_UNITYPROJECT**/$.replace("\\", "/")
maven { url (unityProjectPath + "/Assets/GeneratedLocalRepo/Firebase/m2repository") }
```

The project path is `C:\Work\Unity Projects\Rising Way` — **two spaces**. Unencoded spaces make
an invalid URI, and Gradle 9 refuses to convert it to a file where older Gradle tolerated it.

This is **not** an SDK-version problem. It is an EDM4U/Gradle-9 incompatibility triggered by the
path, and EDM4U 1.2.188 is already the newest version.

### Options

1. **Move the project to a path with no spaces** — e.g. `C:\Work\RisingWay`. Permanent, fixes it
   for every SDK that ships a local Maven repo, and costs nothing at build time. Disruptive once:
   Unity Hub entry, any absolute paths, this repo's location. **Recommended.**
2. **Update Firebase** — worth doing regardless, but newer Firebase Unity SDKs still ship an
   m2repository, so this probably does not fix it on its own.
3. **Patch the generated Gradle** to percent-encode the URI — EDM4U rewrites that block on every
   resolve, so it would not survive.

Option 1 is the only durable fix that does not depend on a third party changing their generator.

---

## Next

1. Decide on the project path.
2. Update Firebase 11.6.0 → current.
3. Re-run the build. It currently completes the full IL2CPP compile for both architectures and
   reaches `:launcher:checkReleaseDuplicateClasses`, so remaining failures should be shallow.
4. Re-add mediation adapters deliberately, at current versions.
