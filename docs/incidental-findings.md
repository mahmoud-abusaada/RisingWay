# Incidental findings

Things noticed while working other tickets that are worth acting on but do not belong to the
ticket that surfaced them. Each entry says what was *observed* versus what is *inferred*, so a
later reader can tell how much to trust it without re-deriving the evidence.

---

## IF-01 — The new Input System is installed but nothing uses it

**Confirmed.** Surfaced by a build warning during P1-05:

```
PlayerSettings->Active Input Handling is set to Both, this is unsupported on Android
and might cause issues with input and application performance.
```

Evidence gathered:

| Check | Result |
|---|---|
| `ProjectSettings.asset` | `activeInputHandler: 2` — "Both" |
| `Packages/manifest.json` | `com.unity.inputsystem: 1.20.0` |
| `packages-lock.json` | `depth: 0` — a direct project dependency, **not** pulled in by another package |
| `UnityEngine.InputSystem` references in `Assets/` | **none** |
| `.inputactions` assets | **none** |
| `InputSystemUIInputModule` in any scene or prefab | **none** |
| Main scene's EventSystem module | script GUID `4f231c4fb786f3946a6b90b886c48677` = legacy `StandaloneInputModule` (fields `m_HorizontalAxis`, `m_SubmitButton`, `m_ForceModuleActive`) |
| Legacy `Input.*` call sites in `Assets/Scripts` | 19 |

So every input path in the game — gameplay and UI both — is legacy `Input`/`StandaloneInputModule`.
The package is inert weight.

**Why "Both" is the costly setting.** It is not merely a redundant checkbox: it initialises and
runs *both* backends. The Input System polls and processes connected devices every frame and
allocates to do it, on a game that never reads a single value from it. Unity's own warning calls
it unsupported on Android.

**Proposed fix**, in increasing order of commitment:

1. Set Active Input Handling to **Input Manager (Old)** — `activeInputHandler: 0`. Clears the
   warning and stops the second backend. Nothing in the project reads the new API, so this is
   behaviour-preserving on the evidence above.
2. Remove `com.unity.inputsystem` from `manifest.json` as well, dropping the package from the
   build entirely.

**Caveat / assumption.** Step 1 requires an Editor restart to take effect and Unity will prompt
for it. Step 2 is safe only while nothing references the package; if the input rework ever
becomes a ticket in its own right, the *right* direction is forward onto the new Input System
rather than back — so do step 1 now and treat step 2 as optional tidying, not a milestone.

**Not done yet** — it is a Player Settings change, and it was found mid-build.

---

## IF-02 — The committed keystore path points at a file that is not there

**Confirmed.** `ProjectSettings/ProjectSettings.asset` carries:

```
AndroidKeystoreName: '{inproject}: RisingWay.keystore'
AndroidKeyaliasName: risingway
androidUseCustomKeystore: 1
```

`{inproject}` resolves relative to the project root. The project root contains
`RisingWayTestingAnotherOne.keystore` — **not** `RisingWay.keystore`. The real one lives outside
the project, in `C:\Work\Unity Projects\Keys\`, which did not move when the project moved.

So a signed release build from a clean checkout fails on a missing keystore. This predates the
move; the move did not cause it, and moving the project did not change the relationship (the
path was already outside the project).

This is entangled with the still-open decision on whether custom keystore signing belongs in
committed settings at all, so it is deliberately **not** fixed here.

**Worth raising separately:** `Keys\RisingWay.keystore` is the Play upload/signing key. If it is
lost, the listing can never be updated again under the same package name. Its only known copy is
on this machine. An off-machine backup is a matter for the owner, not something to automate.

---

## IF-03 — Adaptive Performance is installed, unused, and version-mismatched

**Confirmed.** Noticed because the first green build rewrote `ProjectSettings.asset` and cleared
`preloadedAssets`, dropping both Adaptive Performance settings assets:

```diff
-  preloadedAssets:
-  - {fileID: 11400000, guid: 6cc5a8338722f4e4cbea929bdf339bef, type: 2}   # Samsung Android Provider Settings
-  - {fileID: -4538413754872887865, guid: 728ef4451f2a23b41b417c81aedc887d, type: 2}  # AdaptivePerformanceGeneralSettings
+  preloadedAssets: []
```

Both assets still exist on disk. Unity de-registered them anyway, and the likely reason is a
version mismatch:

| Package | Version | Depth | Note |
|---|---|---|---|
| `com.unity.adaptiveperformance.samsung.android` | **5.0.0-pre.2** | 0 | a **prerelease** from 2022; declares a dependency on `com.unity.adaptiveperformance: 5.0.0-pre.2` |
| `com.unity.adaptiveperformance` | **6.0.0** | 1 | builtin; what actually resolved |

So a 2022 prerelease Samsung provider is running against a major version it was never built for.
`AdaptivePerformance` has **zero** references anywhere in `Assets/` — the game never calls the
API, so its only role would have been automatic thermal/throttling management on Samsung devices.

**This change was deliberately excluded from the path-fix commit.** It is an unexplained side
effect unrelated to that work, and committing it would silently disable Adaptive Performance
without a decision having been made. `ProjectSettings.asset` was restored to its committed state.

**Expect it to recur** on the next build — Unity will clear the list again. The real fix is a
decision, not a revert:

1. **Remove the package** (`com.unity.adaptiveperformance.samsung.android` from `manifest.json`)
   and delete `Assets/Adaptive Performance/`. Justified by it being unused, prerelease, and
   mismatched. Simplest, and drops a package from the build.
2. **Update the provider** to a version built for Adaptive Performance 6.x and keep the feature,
   if automatic thermal management on Samsung hardware is wanted.

Option 1 is the honest default given nothing references it, but it is a package removal and
therefore the owner's call.
