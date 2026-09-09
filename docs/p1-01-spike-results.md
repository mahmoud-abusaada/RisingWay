# P1-01 Unity 6 Spike — Results

**Verdict: GO — CLOSED.** All four questions verified on screen, not just compiled. Proceed with
Unity 6 LTS; the two-step fallback via 2022.3 is not needed.

**Ran:** Unity `6000.5.7f1`, clone at `RisingWay-U6Spike`, branch `spike/unity6`, from commit
`d7a8cfd` (pre-P2-04 code).

---

## 1. The four questions

| # | Question | Compiles | Verified on screen |
|---|---|---|---|
| Q1 | Does `LitFadeWhenClose.shader` still fade the path? | Clean | **Yes** |
| Q2 | Does `Stencilled.shader` still mask the shop? | Clean | **Yes** |
| Q3 | Does the per-part render-queue sorting still layer correctly? | Clean | **Yes** — checked with Saturn (ring + 8 moons), the heaviest case |
| Q4 | Do the Shader Graphs and QuickOutline survive? | Clean | **Yes** |

**Zero compile errors. Zero shader errors. Zero exceptions.** This was the biggest unknown in
the whole plan and it came through. Every warning is a known, scoped work item.

## 1b. P2 code verified in the same pass

The spike clone was taken at `d7a8cfd`, so it already contained P2-01/02/03; the P2-04/05/06
files were copied in afterwards. All of it compiles clean under Unity 6.

P2-04/05/06 are also **functionally** verified, not just compiled. After play-testing,
`%USERPROFILE%/AppData/LocalLow/Abu Sa'da/Rising Way/risingway.save.json` contained:

```json
"schemaVersion": 1, "diamonds": 41, "bolts": 3,
"highScore": 115, "timesPlayed": 7, "tutorialsOn": false,
"ownedBallIds": [1], "ownedFloorIds": [1]
```

That confirms: the versioned save is created, the clean-slate default is applied (legacy
PlayerPrefs correctly ignored), currency and progress persist across sessions, tutorial
completion sticks, atomic write plus `.bak` rotation work, and hand-editing the file to unlock
content works — which is how the Saturn render-queue test above was set up. Under the old
PlayerPrefs system that would have required registry editing.

---

## 2. The one real regression: white sky

The menu rendered with a solid white background instead of the star field.

**Cause — confirmed, not inferred.** URP 14+ added *Preserve Specular Lighting* for transparent
surfaces. The material upgrader defaulted it **on**, which forces premultiplied-alpha blending:

```diff
+  - _ALPHAPREMULTIPLY_ON
+    - _BlendModePreserveSpecular: 1
-    - _SrcBlend: 5      # SrcAlpha
+    - _SrcBlend: 1      # One
```

`Stars9.png` is white RGB with the stars carried in *straight* alpha. Under premultiplied
blending the source is added at full intensity regardless of alpha (`src x 1`), so the whole
cylinder renders opaque white.

Note the material is left internally inconsistent: `_Blend` still reads `0` ("Alpha") while the
blend factors say Premultiply.

### Scope: two materials

Of 251 modified `.mat` files, only **two** had `_SrcBlend` change:

- `Assets/Textures/Ambient/Galaxy/Materials/Stars.mat` (GalaxyCylinder)
- `Assets/Textures/Ambient/Galaxy/Materials/MilkeyWay.mat`

Every other material change was additive — new URP 17 fields (`_SrcBlendAlpha`,
`_DstBlendAlpha`, `_BlendModePreserveSpecular`) written at defaults that do not alter rendering.

### Fix

Per material, in the Inspector under **Surface Options**, uncheck **Preserve Specular
Lighting**. That restores `_SrcBlend = SrcAlpha` and clears `_ALPHAPREMULTIPLY_ON`.

### What was ruled out

Worth recording, since these were plausible and are now eliminated:

- Texture alpha was **not** lost. `runtime format: DXT5`, `sourceHasAlpha: True`.
- Shader keywords were **not** missing. `_SURFACE_TYPE_TRANSPARENT` present throughout.
- Render Pipeline Asset is assigned; camera is Solid Color / black as intended.
- Not a colour-space problem, though the project is still **Gamma** — see below.

---

## 3. Everything else the upgrade changed

`git status` in the spike: **306 modified, 36 added, 13 deleted.**

**Deleted (13)** — 12 `*.pdb.meta` files under Firebase and EDM4U, plus
`ProjectSettings/boot.config`. The pdb metas are debug-symbol metadata and harmless. `boot.config`
is regenerated at build time.

**Added (36)** — notable ones:
- `Assets/DefaultVolumeProfile.asset` — URP 17 requires a default volume profile
- `Assets/Adaptive Performance/Settings/Basic Provider Settings.asset`
- `Assets/Resources/XboxCloudSettings.asset` — created unprompted; **delete it**, it has no
  business in a mobile build and anything under `Resources/` ships unconditionally
- TextMesh Pro: new shaders (`SDFFunctions.hlsl`, SpaceWarp variants) and `Examples & Extras`
  re-imported. **Delete `Examples & Extras`** — pure ship weight, feeds P4-10.

---

## 4. Console findings that become P1 work

| Finding | Count | Ticket |
|---|---:|---|
| `FindObjectOfType` obsolete | 112 | P1-02, mechanical |
| Unity IAP v4 API obsolete | 15 | P1-06, the IAP 5 rewrite |
| `Resolution.refreshRate` obsolete | 2 | P1-02, and touches the P4-04 frame-cap bug |
| `Queue` / `Hashtable` not serializable (UAC1009) | 17 | Cosmetic. Public fields Unity was never serializing anyway |
| `TurnDirection?` skipped by serialization (UAC1001) | 1 | Same nullable behind the P5-01 crash risk |
| Assigned-but-unused fields | 15 | Cleanup, P8-06 |

**New P1 items discovered:**

1. **Install iOS Build Support** for `6000.5.7f1`. Without `UnityEditor.iOS.Extensions.Xcode`,
   both `Google.IOSResolver.dll` and `Firebase.Editor.dll` fail to load, which breaks EDM4U
   dependency resolution.
2. **Adaptive Performance fails to initialise** — no provider selected for this platform. Either
   configure it or drop the package. It is currently inert either way, since
   `m_UseAdaptivePerformance` is `0` on the URP asset.
3. **Decide on colour space.** The project is **Gamma**. URP strongly prefers Linear, and Gamma
   support is being wound down. Switching is not cosmetic — it changes how every material,
   light and post-effect resolves, so it needs its own visual pass. Flagging, not scheduling.

---

## 5. Revamp candidate #1: the sky

The white sky was a symptom, but the underlying approach is worth replacing:

- A 2048x2048 texture that is ~99% empty, tiled 10x5
- On a **lit** cylinder — full PBR lighting on a background shell that should never be lit
- Stars in the alpha channel, material colour animated from `FixedUpdate`
- Transparent overdraw across the entire screen, every frame
- Duplicated in spirit by three separate 8000-particle star systems

It broke on one engine upgrade because it depends on ambient lighting, compression format and
blend state all agreeing. Options, in increasing order of effort:

1. **URP/Unlit shader** — removes the lighting dependency entirely, much cheaper, likely fixes
   this outright and is more robust than unchecking a box
2. **Procedural starfield in Shader Graph** — no texture, no memory, any density
3. **A real skybox** — zero geometry, zero overdraw
4. Decide whether the cylinder and the particle systems both need to exist

---

## 6. Still unverified

- **Q3 visually.** The track layering was never seen in motion — the run was blocked by the
  update dialog, then by the white sky. Re-check after the material fix.
- **The build.** Step 5 of the spike (an Android build attempt) was not reached. AGP 4.0.1,
  `jcenter()` and Jetifier are all still in place, so it is expected to fail; what matters is
  which failure comes first, because that orders P1-04.
- **P2-01 through P2-06.** All written after the spike clone was taken, so none of it has been
  through a compiler. Merging `main` into the spike clone is a free compile check.

---

## 7. Aside: the update dialog is an unrecoverable trap

Play Mode opened on an undismissable update dialog. Both buttons are dead:

```csharp
public void Cancel() {
    if (forceUpdateVersion > currentVersion)
        Application.Quit();      // no-op in the Editor
    else
        updateDialog.SetActive(false);
}
```

`UpdateTheGame()` is wrapped in `#if UNITY_ANDROID` / `#elif UNITY_IPHONE`, so on a Standalone
target its body is empty.

It triggered because PlayerPrefs on Windows are keyed by company + product name in the registry,
so the spike clone **shares PlayerPrefs with the real project** and read a stale
`forceUpdateVersion` cached while `abusaada.com` was still alive.

In a real build `Application.Quit()` works — so whoever registers that expired domain can
force-quit every remaining install on launch, permanently. This is the risk already flagged for
P8-03, now demonstrated rather than theorised.
