# P1-04 — Android Build Findings

**Status:** Gradle chain modernised and working. **Blocked on P1-05 (SDK updates).**
**Method:** Five real build attempts via the Unity CLI, peeling one failure at a time.

---

## The headline

**The Gradle rewrite works.** Not one failure across five attempts came from AGP, Gradle,
jcenter, Jetifier, Java version or the templates. Every remaining blocker is a **third-party SDK
that predates Gradle 9** — which is exactly what P1-05 was scoped to fix.

The strongest evidence: attempt 3 reached `:launcher:minifyReleaseWithR8`. Reaching R8 means
**dependency resolution fully succeeded** — every Firebase artifact, all seven mediation
adapters, play-services-ads, UMP. The new `settingsTemplate.gradle` repositories are correct.

For reference, the build reports its own configuration:

```
targetSdk=AndroidApiLevel35  minSdk=AndroidApiLevel26
scripting=IL2CPP  architectures=ARMv7, ARM64
```

Script compilation and IL2CPP for both architectures complete without error.

---

## The failure chain

| # | Failure | Cause | Status |
|---|---|---|---|
| 1 | `unable to resolve class groovy.util.XmlSlurper` | Unity 6 ships Gradle 9.1.0 / Groovy 4, which moved `XmlSlurper` to `groovy.xml`. GMA's `validate_dependencies.gradle` still imports the old package. | **Fixed** (committed) |
| 2 | `Resolution of configuration ':unityLibrary:aarArtifacts' was attempted without an exclusive lock` | The same GMA script resolves a configuration inside `gradle.projectsEvaluated`. Gradle 9 forbids this. Not patchable — architectural. | **Needs GMA update** |
| 3 | `Failed to install ... platforms;android-35 ... licences have not been accepted` | Unity 6000.5.7f1 bundles platforms **34, 36, 37 — not 35**, and no licences are accepted. | **Decision needed** |
| 4 | `meta-data#android.adservices.AD_SERVICES_CONFIG was tagged ... to remove other declarations but no other declaration present` | `AndroidManifest.xml` removes a `<meta-data>` with that name, but the GMA AAR declares it as a `<property>`. The directive matches nothing, and AGP 9 turned that from a warning into an error. | **Needs fixing with P1-05** |
| 5 | `minSdkVersion (26) is greater than targetSdkVersion (9)` in `jetified-googlemobileads-unity` | **`googlemobileads-unity.aar` declares `targetSdkVersion 9`** — Android 2.3 era. | **Needs GMA update** |

Three of the five are the same root cause: **GMA 9.1.0 is too old.** No amount of build
configuration gets past a vendored AAR declaring `targetSdkVersion 9`.

### What this settles about the AD_SERVICES_CONFIG workaround

The audit flagged the `tools:node="remove"` in `AndroidManifest.xml` as a suspicious hack. It is
actually **half of Google's own documented GMA v8.6.0 workaround**, applied twice:

- `AndroidManifest.xml` removes a `<meta-data>` node
- `validate_dependencies.gradle` unzips the play-services-ads-lite AAR, rewrites its
  `<property>` node to a comment, and rezips it

They target **different element types**, which is why the manifest one now matches nothing. Both
should be re-evaluated together during the GMA update rather than patched in isolation — newer
GMA versions handle this natively.

---

## Two decisions needed

### 1. targetSdk 35 or 36?

Play requires **at least** 35, so either satisfies it.

- **35** — matches the plan, but Unity 6 does not bundle platform 35. Installing it means
  accepting Android SDK licence agreements. **That is a legal agreement and is yours to accept,
  not mine** — so it was left alone.
- **36** — already installed, no download, no licence step. Verified in attempt 4 that it gets
  past this blocker. But targeting Android 16 brings more OS behaviour changes to validate,
  notably edge-to-edge enforcement, which interacts with `androidRenderOutsideSafeArea: 1`
  already flagged in the audit.

Committed value is currently **35**. Neither is wrong; 36 buys more runway, 35 is less
compatibility work now.

### 2. Custom keystore policy

`androidUseCustomKeystore: 1` with `AndroidKeystoreName: '{inproject}: RisingWay.keystore'`, but
the keystores now live outside the project. Every local build therefore needs the password.

Common practice is to keep custom signing **off** in committed settings and supply it only at
release time via CLI flags or CI secrets, keeping paths and passwords out of the repo entirely.
The Unity CLI supports this directly (`--android-keystore-base64`, `--android-key-alias`, ...).

Setting untouched — it was temporarily disabled for the test builds and reverted.

---

## Tooling added

`Assets/Editor/RisingWayBuilder.cs` — Unity has no built-in command-line Android build; only
desktop targets build without an `-executeMethod`. This provides:

```bash
unity build "<project>" --target Android --execute-method RisingWayBuilder.BuildAndroidApk -o out.apk
unity build "<project>" --target Android --execute-method RisingWayBuilder.BuildAndroidAab -o out.aab
```

It logs resolved build settings, dumps per-step timings on success and every error message on
failure, and **exits non-zero on failure** — a Unity batch-mode build otherwise exits 0 even
when the build failed, which would silently pass in CI. This becomes the CI entry point in
P8/P9.

---

## Incidental findings

- **Unity IAP is already at 5.4.3.** The engine upgrade bumped it, so the v4 → v5 API break is
  live now, not a future step. Those 15 `CS0618` warnings are P1-06 and it is no longer optional.
- **`Assets/Resources/PerformanceTestRunInfo.json`** is generated by
  `com.unity.test-framework.performance` on every build. It sits under `Resources/`, so it
  **ships unconditionally**, and it embeds the full package manifest into the app. Now
  gitignored; removing it from builds is a P4-10 item.
- **The iOS module is installed** (`Android, Android SDK & NDK Tools, iOS, OpenJDK, Web`), so
  the `Google.IOSResolver` / `Firebase.Editor` assembly-load errors seen in the spike are
  probably already resolved. Confirm on the next Editor open.
- **Licensing handshake warnings** appear in every batch build
  (`Failed to handshake to channel: "LicenseClient-Mahmo"`). They are noise — the client
  relaunches and the build proceeds. Ignore unless a build fails with no other explanation.

---

## Next

1. **P1-05 — update the SDKs, GMA first.** It is the hard prerequisite. Blockers 2, 4 and 5 all
   dissolve with it, and it should also retire both AD_SERVICES_CONFIG workarounds and the
   local Groovy patch.
2. Answer the targetSdk question.
3. Re-run the build loop. Everything is now scripted, so each iteration is one command.
