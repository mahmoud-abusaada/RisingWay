# The path-rendering bug — investigation log

**Status: ROOT CAUSE FOUND - see the section below.** The history that follows is kept because
three hypotheses were eliminated along the way and should not be re-walked.

---

# ROOT CAUSE FOUND (2026-09-15)

**URP 17 strips both custom fade shaders down to ZERO compiled variants.** The build log says it
outright:

```
Compiling shader "Universal Render Pipeline/Custom/Lit Fade when Close"
  Pass "StandardLit" (vp)
    Target graphics API: gles3
    Full variant space:         9.216
    After settings filtering:   9.216
    After built-in stripping:   288
    After scriptable stripping: 0        <-- nothing ships
```

Stock `Universal Render Pipeline/Lit` keeps 28 variants through the same pass. Both
`Lit Fade when Close` and `Lit Fade when Away` end at 0.

So on device the path parts hold a perfectly valid material pointing at a shader the GPU has
nothing compiled for, and nothing is drawn. The material is fine; the shader program does not
exist.

## Why every earlier observation fits

| Observation | Explained by |
|---|---|
| Editor renders correctly | The Editor compiles variants on demand; stripping only happens at build |
| GLES3 **and** Vulkan builds both broken | Variant stripping is API-independent |
| Diagnostic reported everything healthy | It reads *material* state - alpha, queue, shader name. A missing compiled variant is invisible to it |
| Shader/materials/camera byte-identical to `pre-unity6` | Nothing in the project changed. URP 17's stripper is far more aggressive than URP 12's |
| "Make it opaque" would have appeared to fix it | It swaps to a shader that *does* have variants - masking the cause rather than fixing it |

## The trigger

`Assets/UniversalRenderPipelineGlobalSettings.asset`:

```
m_URPShaderStrippingSetting:
  m_StripUnusedVariants: 1
```

URP decides which variants are reachable by analysing materials at build time. It **cannot see a
shader assigned at runtime**, and `PathMaker.setPartMaterial` does exactly that through
`MaterialsManager.getFadeMaterial`, which sets `m.shader = fadeShader` during play. URP therefore
concludes nothing uses these shaders and strips them completely.

This is a behaviour regression from the URP 12 -> 17 upgrade even though no project file changed.

## Fix

**Immediate / proof:** Project Settings > Graphics > URP Global Settings > Shader Stripping >
untick **Strip Unused Variants**, rebuild, verify on device. Costs build size and build time,
because it relaxes stripping for every shader.

**Follow-up, once confirmed:** make the two custom shaders URP-17-compliant so they survive
stripping with the flag back on. They are URP-7-era files (the header still says "This shader
works with URP 7.1.x and above") missing pragmas URP 17 expects. That restores the size saving.

**Worth deciding separately:** the fade never actually fires. `_FadeEndDistance` is 8, and the
closest any part came to the camera across two full dumps was **13.22** - the camera sits a fixed
14.32 behind the ball and nearer parts are destroyed. So every part is fully opaque at all times
while still paying transparent blending with `_ZWrite=0`. That is also what makes sorting depend
purely on queue order. Not the bug, but worth revisiting once the path renders again.

Symptom, as reported: *"the path is not showing unless the ball moves, another part shows of the
path."* Visible on a physical device. Never yet reproduced in the Editor or on an emulator.

---

## The correction that matters most

**The path uses `LitFadeWhenClose.shader`. Not `LitFadeWhenAway.shader`.**

Three investigation cycles went into `LitFadeWhenAway` — reading its fade maths, reasoning about
its `_FadeEndDistance` default, and producing a whole diagnostic APK (`fade40`) that raised that
default from 8 to 40. **That build could never have changed anything**, because the game does not
load that shader for path parts. The runtime dump settled it in one line:

```
[RD]   Mesh   shader=Lit Fade when Close   queue=2007
```

The lesson is the same one the white-sky bug taught, and it was not applied: an assumption about
*which* asset is in play is still an assumption. The shader names differ by one word, both files
sit in `Assets/Shaders/`, and nothing but a runtime read distinguishes them.

## The two shaders do OPPOSITE things

| Shader | Formula | Effect |
|---|---|---|
| `LitFadeWhenAway` | `saturate((_FadeEndDistance - dist) / _FadeLength)` | transparent FAR from camera |
| **`LitFadeWhenClose`** (in use) | `saturate((dist - _FadeEndDistance) / _FadeLength)` | transparent NEAR the camera |

`FadeWhenClose` exists so the track directly beneath and behind the player does not block the
view. That is sensible, and it is working.

## The fade is NOT hiding the path

Live values captured during an actual run on a 16 KB Pixel 10 Pro image, OpenGLES3:

```
CAMERA->PLAYER dist: 14.32          (camera local offset {0, 6, -13}; sqrt(6^2+13^2) = 14.32)
_FadeEndDistance = 8.00   _FadeLength = 4.00 or 5.00
```

Applying the correct formula to the parts that were in frustum:

| Part | dist | `saturate((dist-8)/len)` | alpha |
|---|---|---|---|
| part[5] Mesh | 18.71 | 2.14 | **1.00** |
| part[6] Mesh | 18.73 | 2.15 | **1.00** |
| part[7] Mesh | 19.01 | 2.20 | **1.00** |
| part[8] Mesh | 19.77 | 2.35 | **1.00** |

Every part is at **full opacity**. The earlier arithmetic in this investigation — camera 14.32 vs
a cutoff of 8, therefore everything invisible — assumed the `FadeWhenAway` formula and was
backwards. The first version of the diagnostic printed `FADE FACTOR=0.00 <-- INVISIBLE` against
healthy parts for exactly that reason; it has since been corrected to read the shader name and
apply the matching formula.

---

## Eliminated

1. **Render-queue collision / driver draw-order.** A Vulkan-first build behaved identically to
   OpenGLES3 on the device. Not API-dependent.
2. **Fade distance too short.** Disproved above — alpha resolves to 1.00.
3. **A URP 12 -> 17 regression in these assets.** `LitFadeWhenClose.shader`, the floor materials
   and the camera transform are all byte-identical to the `pre-unity6` tag.

## Still standing, unproven

**Every path part is alpha-blended with depth writing off, in the opaque queue range.** Confirmed
from live data on every renderer:

```
_Surface=1   _SrcBlend=5 (SrcAlpha)   _DstBlend=10 (OneMinusSrcAlpha)   _ZWrite=0
queue = 2001..2011      (Unity's opaque/transparent boundary is 2500)
```

`PathMaker` assigns `2000 + index` per part, so parts are composited purely in queue order with
no depth arbitration at all — the highest-numbered part draws last and paints over everything
before it, regardless of where it actually is in 3D. The background shares that range too:
`galaxySky` at 2000, stars at 2001, clouds at 2000, and `Moons`/Saturn ring at 2011.

This is a genuine defect. It is **not yet demonstrated to cause this symptom**, and it should not
be presented as the answer until it is.

---

## Tooling

`Assets/Scripts/Diagnostics/RenderDiagnostics.cs`, gated to `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Build it with `RisingWayBuilder.BuildAndroidApkDev` — a release build strips it out.

Fires on **F1**, a **four-finger tap**, and automatically **1.5s after
`Utility.playerIsInPosition` turns true** — the moment `PathMaker` starts handing parts the fade
material. Reads `sharedMaterial`, never `.material`, so it cannot create an instance and perturb
what it measures. Logs one message per part; a single large `Debug.Log` gets truncated by logcat,
which cost one build cycle to discover.

## Next

1. **Run it in the Editor** (F1 during a run). Still never done, and it is the cheapest remaining
   split: if the path renders correctly there, this is device-only and the difference is the
   driver or the build, not the scene.
2. **Run it on the physical device** with the dev APK and a four-finger tap during a real run,
   and compare the same fields against the emulator capture above.
3. Only then decide whether the `_ZWrite=0` / opaque-queue compositing is the cause.

Note that the emulator has never reproduced the symptom, so it cannot currently falsify anything
— it is only useful for reading live values.
