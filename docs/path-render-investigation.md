# The path-rendering bug — investigation log

**Status: root cause NOT yet confirmed. Three hypotheses eliminated, one major factual error
corrected.** This file exists so the eliminated ground is not re-walked.

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
