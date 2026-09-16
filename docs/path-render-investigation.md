# The path-rendering bug — investigation log

**Status: FIXED and VERIFIED on a physical device** (Galaxy S20 Ultra, Android 13, Adreno) on
2026-09-16, commit `0b6602b`. The path renders. The history that follows is kept because
three hypotheses were eliminated along the way and should not be re-walked.

---

# ROOT CAUSE (2026-09-16): a one-word typo in the SubShader tag

```
URP 17 Lit.shader:          "RenderPipeline" = "UniversalPipeline"
LitFadeWhenClose.shader:    "RenderPipeline" = "UniversalRenderPipeline"     <-- not a valid value
```

`UniversalRenderPipeline` is not a tag value URP recognises. URP 17 silently strips **every
variant** of a SubShader whose `RenderPipeline` tag it does not recognise, so the shader shipped
with zero programs and the path rendered as nothing - on device only, because the Editor compiles
on demand and never strips.

## The evidence is a clean correlation

Variant counts from one build, before the fix:

| Shader | RenderPipeline tag | Variants shipped | Matches |
|---|---|---|---|
| `StencilledLit` | `UniversalPipeline` | **16** | stencil worked in the shop |
| `AlwaysVisible` | `UniversalPipeline` | **8** | worked |
| `Lit Fade when Close` | `UniversalRenderPipeline` | **0** | path invisible |
| `Lit Fade when Away` | `UniversalRenderPipeline` | **0** | - |

Every shader with the correct tag ships. Both with the wrong tag ship nothing. It also matches an
observation made months earlier and never connected: the shop's stencil effect worked while the
path did not.

After fixing the tag:

```
                              before -> after
After settings filtering:      9.216 -> 192
After built-in stripping:        288 -> 6
After scriptable stripping:        0 -> 6
```

Note the middle line. Once URP recognises the shader it applies proper settings filtering, so the
fix costs **six variants**, not a larger build.

## The comment that caused it

The URP 7 template these shaders were copied from says:

```
// In case you want your subshader to only run in LWRP set the tag to
// "UniversalRenderPipeline"
```

That instruction names the wrong value. The comment in each affected shader has been corrected and
now explains the failure mode.

## Four other shaders had the same bug

`LitFadeWhenClose2`, `GlassFade` and `Black Hole Shader` carried the same wrong tag and would have
been silently stripped in any build. All fixed.

## A wrong turn worth recording

An earlier revision of this document named `m_StripUnusedVariants: 1` as the root cause. That was
the mechanism, not the cause - the setting was behaving correctly, and it was correct to strip a
SubShader it could not identify. Turning it off did make the shaders survive, but it also removed
filtering from everything else:

| Shader | Variants compiled with stripping off | Used by this game |
|---|---|---|
| `UberPost` | 9.216 x 2 APIs | post-processing |
| `TerrainEngine/.../WavingDoublePass` | 13.824 | **no terrain in this game** |
| `FinalPost` | 1.248 x 2 APIs | post-processing |

That build was heading for 4+ hours and was abandoned. `Always Included Shaders` was also tried
and does **not** work: it prevents the shader being dropped, but URP's scriptable stripper removes
the variants independently. Modernising the URP 7-era `#pragma` block was tried too - it changed
the variant space but still ended at 0, and was reverted once the tag turned out to be the cause.

The lesson: `After scriptable stripping: 0` means URP rejected the *SubShader*, not the variants.
Compare against a shader that works before assuming the stripping settings are at fault.

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
