# P1-01 — Unity 6 Spike (throwaway)

**Goal:** find out what visually breaks when Rising Way moves from Unity 2021.3.18f1 / URP 12
to Unity 6 / URP 17 — **before** committing 12–18 days to the real upgrade.

**This is disposable.** Nothing from the spike gets merged. The output is a go/no-go decision
and a list of what needs fixing.

**Timebox: 2 days.** If it's clearly going badly before then, stop early — that is a successful
result, not a failure.

---

## Setup (already done for you)

The spike lives in a **separate cloned project** so your working 2021.3 project and its 10.6 GB
Library are never touched:

```
C:\Work\Unity Projects\Rising Way          <- untouched, stays on 2021.3.18f1
C:\Work\Unity Projects\RisingWay-U6Spike   <- the spike, on branch spike/unity6
```

The clone has full git history, so `git diff` there shows exactly what Unity changed during the
upgrade. Unity `6000.5.7f1` is already installed on this machine, so there's no download.

> **Do not open the spike folder in Unity 2021.3, and do not open the original in Unity 6.**
> That's the whole point of the separation.

---

## Step 0 — The "before" reference already exists

**You do not need to install Unity 2021.3.** The project already contains 46 screenshots of the
shipped game, captured with Unity Recorder at three device sizes:

```
Recordings/Mobile/Phone/Big     18 images  (1290x2796, iPhone Pro Max class)
Recordings/Mobile/Phone/Small   14 images
Recordings/Mobile/Tablet        14 images
```

These are the reference set. Two of them alone cover all four spike questions:

| Reference image | Covers |
|---|---|
| `Phone/Big/image_003_0000.jpg` | **Q1** path fade gradient into the distance, and **Q3** render-queue layering across several parts at a turn |
| `Phone/Big/image_009_0000.jpg` | **Q2** shop stencil masking (each ball clipped to its cell), and **Q4** the white QuickOutline rims |

Browse the rest and pick any others showing something specific you care about — a Saturn with
rings and moons, the Floors tab, a high-altitude night sky.

> `Recordings/` is gitignored, so these are not in the repo and exist only on this machine.
> Copy the ones you plan to use somewhere safe before starting.

**Optional, higher fidelity:** `_RisingWay_BuildArchive/Builds/Android/Rising Way.apk`
(123.6 MB, dated 31 May 2024 — the same day as the shipped AAB) can be sideloaded onto an
Android phone for a live reference you can compare against interactively. Worth doing only if a
still turns out to be ambiguous.

When you take the "after" shots in Unity 6, match the framing of the reference images as
closely as you reasonably can — same screen, same rough camera position.

---

## Step 1 — Open the spike in Unity 6

1. Unity Hub → **Add project from disk** → `C:\Work\Unity Projects\RisingWay-U6Spike`
2. Set its editor version to **6000.5.7f1** and open.
3. Accept the upgrade prompt.

**Expect this to take a long time.** It is a full reimport of 4,186 assets including ~680 MB of
textures and audio. Anywhere from 20 minutes to well over an hour. Let it finish — do not kill
it partway, or you'll get a half-migrated asset database that produces misleading results.

While it runs, keep the Console open and **turn off "Collapse"** so repeated errors are visible.

---

## Step 2 — Capture the Console before touching anything

As soon as the import settles, before fixing anything:

1. Console → clear filters, ensure Error / Warning / Log are all enabled.
2. Right-click the Console → **Export** (or select all and copy) into
   `_SpikeReference\console-after-import.txt`.

This raw list is the single most useful artefact of the spike. Send it to me.

Pay particular attention to anything mentioning:
- `LitFadeWhenClose`, `Stencilled`, `StencilMask`, `AlwaysVisible`
- `Shader error` / `Shader warning`
- `Outlines`, `UnlitColor`, `ViewSpaceNormals` (the Shader Graphs)
- `QuickOutline`
- `GoogleMobileAds`, `Firebase`, `Purchasing` (API breaks)
- `obsolete` / `deprecated`

---

## Step 3 — Answer the four questions

These four are what the spike exists for. Take the same seven screenshots from Step 0 and
compare side by side.

### Q1 — Does `LitFadeWhenClose.shader` still work?
This is the path fade, applied to every track part via
`MaterialsManager.getFadeMaterial()`. It's the highest-risk item: a hand-written URP shader
crossing five URP major versions.

**Look for:** magenta (shader failed to compile), no fade at all, fade in the wrong direction,
or hard edges where there should be a gradient. Compare against `Phone/Big/image_003_0000.jpg`.

### Q2 — Does `Stencilled.shader` still mask the shop?
Used by 9 materials and applied to every shop list item via `getStencilledMaterial()`.

**Look for:** planets bleeding outside their list cells, items drawing on top of the scroll
view, or the mystery-box reveal rendering incorrectly. Compare against `Phone/Big/image_009_0000.jpg`.

### Q3 — Does the per-part render-queue sorting still layer the track?
`PathMaker.updatePartsRenderQueue()` assigns `2000 + index` to every part's mesh and start/end
blocks so they layer correctly during the fade. URP majors are known to disturb manual sorting.

**Look for:** track parts drawing in the wrong order, z-fighting, parts popping in front of
nearer ones. Compare against `Phone/Big/image_003_0000.jpg`, which shows a turn with several parts visible.

### Q4 — Do the Shader Graphs and QuickOutline survive?
Two graphs (`Outlines`, `UnlitColor`) plus `ViewSpaceNormals`, and QuickOutline's
command-buffer outline on the menu/shop ball.

**Look for:** missing or doubled outlines on the player ball. Compare against `Phone/Big/image_009_0000.jpg` — every ball in that list has a white rim.

---

## Step 4 — Record what Unity changed

In the spike folder:

```bash
cd "C:/Work/Unity Projects/RisingWay-U6Spike"
git status --short > "../_SpikeReference/git-status.txt"
git --no-pager diff --stat > "../_SpikeReference/git-diff-stat.txt"
git --no-pager diff Packages/ ProjectSettings/ > "../_SpikeReference/git-diff-settings.txt"
```

That tells us exactly which packages Unity bumped and which project settings it rewrote —
useful input for planning the real P1-02 and P1-04.

---

## Step 5 — Try to build (only if Steps 1–4 went well)

Don't sink time here if the shaders are already broken.

`File > Build Settings > Android > Build`. We are **not** trying to ship this — we only want to
know whether the Gradle chain fails, and how. The project still carries AGP 4.0.1, `jcenter()`
and Jetifier, so a failure is *expected*; what matters is **which** failure comes first, because
that orders the P1-04 work.

Capture the full Gradle error into `_SpikeReference\gradle-error.txt`.

---

## What "go" and "no-go" look like

**GO — proceed with Unity 6 (P1-02 onward)**
- Shaders compile, or fail in ways with obvious fixes (include paths, CBUFFER declarations)
- Track layering intact or fixable by adjusting the render-queue offsets
- Shop stencil masking works or needs minor changes
- SDK errors are the expected API breaks we already planned for

**NO-GO — fall back to the two-step route via 2022.3**
- Path fade or stencil masking fundamentally broken with no clear cause
- Render-queue sorting no longer behaves and the fix would mean redesigning how the track draws
- Reimport corrupts assets or the scene fails to open

Either result is a good outcome. Two days spent here beats discovering it three weeks into the
real upgrade.

---

## When you're done

Send me:
1. `console-after-import.txt`
2. The after-screenshots alongside the before ones
3. The three git diff files from Step 4
4. `gradle-error.txt` if you got that far

I'll go through them and turn it into the concrete P1 task list — or into the fallback plan.

---

## Cleanup

The spike folder is disposable. When we're finished:

```bash
rm -rf "C:/Work/Unity Projects/RisingWay-U6Spike"
```

Your real project at `C:\Work\Unity Projects\Rising Way` is untouched throughout and stays on
2021.3.18f1 until we deliberately upgrade it in P1-02.
