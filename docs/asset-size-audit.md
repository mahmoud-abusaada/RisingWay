# Rising Way — Asset & Download Size Audit

**Ticket:** P4-10 (investigation phase)
**Status:** Findings only. No import settings or assets have been changed.
**Method:** Measured from the shipped artifact
`_RisingWay_BuildArchive/Builds/Android/Rise Up Production.aab` (v1.0.19, 31 May 2024),
plus a scan of all 236 texture and 26 audio `.meta` files in the project.

---

## 1. Correction to the audit: the download is ~94 MB, not 124 MB

The technical audit quoted **124 MB**, taken from the AAB file size. That number is the *build
artifact*, not what a player downloads. An AAB contains **every** ABI, and Play delivers only
one to each device.

Measured breakdown of the AAB (compressed, i.e. transfer sizes):

| Component | Compressed | Ships to a device? |
|---|---:|---|
| `base/assets` | 62.16 MB | **Yes — every device** |
| `base/lib/arm64-v8a` | 22.57 MB | Only to 64-bit devices |
| `base/lib/armeabi-v7a` | 21.69 MB | Only to 32-bit devices |
| `base/dex` | 7.79 MB | Yes |
| `base/res` + `resources.pb` + `root` + `manifest` | 1.41 MB | Yes |
| `BUNDLE-METADATA` + `META-INF` | 2.45 MB | No — build metadata |

**Actual download on a modern ARM64 device: ≈ 93.9 MB.**

Still far too large for a tap-to-turn arcade game, and the conclusion from the audit stands —
this is a top-of-funnel loss no in-game improvement can recover. But the target arithmetic
changes: getting under 60 MB means cutting **~34 MB**, not ~64 MB.

> **Corollary:** dropping `armeabi-v7a` would shrink the AAB by 21.69 MB but save a player
> **nothing** — they only ever receive one ABI. It is worth considering for build time and
> store-artifact size, and only after checking 32-bit device share in Play Console. It must
> **not** be counted toward the download target.

---

## 2. Where the weight actually is

`base/assets` is 66% of the download and is dominated by two files:

| File | Compressed | Raw | What it is |
|---|---:|---:|---|
| `bin/Data/sharedassets0.resource` | 31.45 MB | 33.25 MB | Texture + audio binary payload |
| `bin/Data/data.unity3d` | 25.86 MB | 31.39 MB | Scene and serialized assets |
| `bin/Data/Managed/Metadata/global-metadata.dat` | 2.26 MB | 7.65 MB | IL2CPP metadata |
| `assets/audience_network.dex` + `ad-viewer/*` | ~2.1 MB | ~5.5 MB | **Meta Audience Network** |

`sharedassets0.resource` barely compresses (33.25 → 31.45 MB), which confirms it is already
GPU-compressed texture data and encoded audio. **Reducing it requires changing import
settings, not compression.**

Per-device native code (ARM64):

| Library | Compressed |
|---|---:|
| `libil2cpp.so` | 13.43 MB |
| `libunity.so` | 7.29 MB |
| `libFirebaseCppApp-11_6_0.so` | 1.52 MB |
| `libapplovin-native-crash-reporter.so` | 0.24 MB |
| others | 0.09 MB |

`libil2cpp.so` is compiled C# — ours plus every SDK. Managed stripping is currently **Low**;
raising it directly attacks this number.

---

## 3. Textures — the concentrated opportunity

236 textures analysed (excluding third-party packages that do not ship).

- **136 have no Android import override** (86.7 MB of source), building at the default
  `maxTextureSize: 2048`.
- **100 are correctly overridden** (92.9 MB of source) — the 72 ball/planet textures are
  properly set to Android 1024 with ASTC. **The pipeline knowledge exists; it simply was not
  applied to the ambient set.**

### Where the unoverridden textures live

| Folder | Files | Source | Ships? |
|---|---:|---:|---|
| `Assets/Textures/Ambient/Galaxy` | 38 | 68.56 MB | **26 of 38 — yes** |
| `Assets/URP_Flares_Pack/Editor/Textures/UI/Ads` | 12 | 8.98 MB | No — `Editor/` folder |
| `Assets/Textures` (root) | 5 | 3.78 MB | Partly (`back.png` 3.25 MB) |
| `Assets/Textures/RisingWay icon` | 2 | 1.75 MB | No — source art for the store icon |
| `Assets/URP_Flares_Pack/Images/Flare1` | 13 | 1.55 MB | Only if referenced |
| everything else | 66 | ~2.1 MB | mixed |

### The finding that matters

I traced every Galaxy texture's GUID through the scene:

- **26 are referenced** (10 assigned to `clouds[]`, 25 to `galaxy[]`, with overlap) → **these ship**
- **12 are not referenced anywhere** (28 MB of source) → project clutter, zero build impact

So the shipping target is **26 textures, ~40.5 MB of source, all at 2048 with no Android
override**.

These are the billboarded cloud and distant-galaxy sprites spawned by
`AmbientEffectsController.spawnClouds()` and `spawnGalaxy()`. They render far from camera,
semi-transparent, and are blurry by design. **2048 → 1024, or even 512, should be visually
free on these**, and it is the lowest-risk large saving available.

> **Do not touch the 72 ball/planet textures.** They are already correctly configured, and
> they are the thing players collect. Cut everywhere else first.

---

## 4. Audio — a settings problem, not a content problem

Every audio clip in the project, **including both music tracks**, is imported as:

- `loadType: 0` — **Decompress On Load**
- `preloadAudioData: 1` — loaded at scene load
- `compressionFormat: 1` — Vorbis
- `quality: 1` — **100%**

Three separate problems:

1. **Decompress On Load on multi-minute music.** `Track1 V3.mp3` (3.6 MB) and
   `Track 2 Last Version V4.mp3` (4.6 MB) are expanded to PCM in RAM at load, on the main
   thread. Music of this length should be **Streaming**. This is the memory and boot cost
   already flagged as P4-04.
2. **`preloadAudioData` on everything** means every clip loads before the menu appears,
   contributing to the boot stall.
3. **Vorbis at 100% quality** is maximum size. For mobile SFX, 30–50% is typically
   indistinguishable and roughly halves the encoded payload.

Also present: `Assets/Audio/Old/` contains legacy clips including an **8.16 MB
`SuperSpeedLoop.wav`**. None are referenced by the scene. `SoundManager` holds all live clip
references and is in the scene, so scene-reference is a sound test here — but confirm against
prefabs before deleting.

---

## 5. Other findings

**Meta Audience Network ships a web ad viewer.** `assets/audience_network.dex` (1.58 MB) plus
`assets/ad-viewer/` — an entire JS ad renderer with ~40 localisation bundles. This is the
adapter most likely to be dead already (Meta withdrew Audience Network for many publishers).
Removing it is a clean ~2 MB with, pending P0-04, probably zero revenue cost.

**Project clutter that does not affect the build.** Worth cleaning for repo hygiene, but do not
expect download savings:
- 12 unreferenced Galaxy textures — 28 MB
- `URP_Flares_Pack/Editor/` textures — 9 MB (Editor folders never ship)
- `Assets/Audio/Old/` — ~9.5 MB
- JMO Cartoon FX demo scenes — `CFX Free Demo.unity` alone is 4.33 MB, **larger than the
  actual game scene** (`SampleScene.unity`, 4.05 MB)

Demo scenes only ship if listed in Build Settings or referenced. They are not. Deleting them
shrinks the repo and speeds imports; it will not shrink the download.

---

## 6. Estimated savings and a realistic target

Ranked by confidence. **These are estimates from import settings, not from a test build** —
the only way to confirm is to change settings and rebuild, which is P4-10 execution work.

| Action | Est. saving | Confidence | Risk |
|---|---:|---|---|
| 26 Galaxy textures → Android override, 1024 or 512 | 10–18 MB | Medium | Low — background sprites |
| Music → Streaming; SFX → Compressed In Memory; Vorbis quality → 0.4 | 3–6 MB | Medium-high | Low |
| Remove Meta Audience Network adapter | ~2 MB | High | Pending P0-04 |
| Remove 2–3 further dead mediation adapters | 3–8 MB | Low | Pending P0-04 |
| Managed stripping Low → Medium | 2–4 MB | Medium | Needs `link.xml` for IAP + reflection |
| `back.png` (3.25 MB, 2048, no override) | 1–2 MB | Medium | Check where it is used first |

**Plausible landing zone: 94 MB → 65–75 MB.**

Reaching the plan's **under 60 MB** target likely needs one of:
- **Play Asset Delivery** for the 124-item cosmetic set, so the base module carries only the
  starter planets and the rest install on demand. This is the option that preserves all
  content and is my recommendation.
- Cutting cosmetic content, which I would resist — the collection is the game's best asset.
- Accepting ~70 MB, which is still a large improvement over 94 MB.

---

## 7. What to do next

This ticket is investigation-only and is now complete. Execution should happen in P4, **after**
the Unity 6 upgrade (P1), because the upgrade changes texture compression defaults and the URP
asset — doing it before means doing it twice.

Order when P4 begins:

1. Apply Android/iOS overrides to the 26 shipping Galaxy textures. Rebuild, measure.
2. Fix the audio import settings. Rebuild, measure.
3. Remove dead mediation adapters once P0-04 answers which ever earned.
4. Raise managed stripping, verify IAP and reflection paths still work.
5. Re-measure the AAB with the same method used here, and only then decide whether Play Asset
   Delivery is needed.

**Measure after each step.** The estimates above have wide error bars, and the whole point of
starting from the shipped artifact is to keep this evidence-driven.
