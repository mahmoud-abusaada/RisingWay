# Cosmetic ID Contract — FROZEN

**Ticket:** P2-05
**Status:** Frozen as of v1.0.19 (versionCode 20). Generated from the serialized
`MaterialsManager` lists in `Assets/Scenes/SampleScene.unity`.

---

## Read this before editing any cosmetic list

Cosmetic IDs are the **primary key for what a player owns**. `SaveData.ownedBallIds` and
`SaveData.ownedFloorIds` store nothing but these integers.

Once a build ships, these IDs are a permanent contract:

- **Changing an ID hands players a different planet.** Silently. No error, no warning, and no
  way to detect or repair it after the fact, because the save only records the number.
- **Reordering the lists is safe *only* if the `id` fields move with their entries.** The
  `id` is a serialized field on each entry, not the list index, so drag-reordering in the
  inspector is fine. Editing the numbers is not.
- **Adding new cosmetics is safe.** Use the next free ID in the relevant range. Never reuse a
  retired one.
- **Removing a cosmetic is not safe.** Players who own it would hold an ID that resolves to
  nothing. If something must go, leave the entry in place and hide it in the shop instead.

### The trap that made this necessary

`MaterialsManager` used to contain three helpers that assigned IDs by list position:

```csharp
ballMaterials[i].id = i + 1;          // updateBallsPrices()
floorMaterials[i].id = i + 1;         // updateFloorsPrices()
patternFloorMaterials[i].id = i + 101; // updatePatternFloorsPrices()
```

Calling any of them after inserting a single entry would have renumbered everything below it
and reassigned every player's collection. The calls were commented out in `Awake()`, so this
never fired in production — but it was one uncommented line away. **Those ID assignments have
been removed. Do not reintroduce them.**

### What is checked automatically

`MaterialsManager.ValidateCosmeticIds()` runs in the editor and in development builds and
reports duplicate IDs, non-positive IDs, and lists that have shrunk below the shipped baseline.

It **cannot** detect two items having swapped IDs — that produces a structurally valid list
that hands players the wrong planets. That is what the map below is for: diff it whenever these
lists change.

---

## ID ranges

| Category | Field | Range | Count shipped |
|---|---|---|---:|
| Balls | `ballMaterials` | 1–72 | 72 |
| Floors (colour) | `floorMaterials` | 1–12 | 12 |
| Floors (pattern) | `patternFloorMaterials` | 101–140 | 40 |

Ball IDs and floor IDs are **separate spaces** — a ball with id 5 and a colour floor with id 5
are unrelated and stored in different lists.

Colour floors (1–12) and pattern floors (101–140) share the **same** ownership list, which is
why their ranges must never overlap. `getCombinedFloorsList()` merges them and
`isFloorOwned(id)` looks across both.

---

## Frozen map

### Balls — `ballMaterials` (72)

| ID | Material | ID | Material | ID | Material |
|---:|---|---:|---|---:|---|
| 1 | Black | 2 | Grey | 3 | White |
| 4 | Blue | 5 | Dark Blue | 6 | Cyan |
| 7 | Cyan2 | 8 | Green | 9 | Dark Green |
| 10 | Yellow | 11 | Red | 12 | Dark Red |
| 13 | Pink | 14 | Purple | 15 | Black and Grey |
| 16 | Black and White | 17 | Black and Yellow | 18 | Black and Orange |
| 19 | Black and Red | 20 | Black and Pink | 21 | Black and Purple |
| 22 | Black and Blue | 23 | Black and Cyan | 24 | Black and Green |
| 25 | Blue and Pink | 26 | Cyan and Purple | 27 | Red and Green |
| 28 | Brown and Orange | 29 | Black and Grey 2 | 30 | Black and White 2 |
| 31 | Black and Yellow 2 | 32 | Black and Orange 2 | 33 | Black and Red 2 |
| 34 | Black and Pink 2 | 35 | Black and Purple 2 | 36 | Black and Blue 2 |
| 37 | Black and Cyan 2 | 38 | Black and Green 2 | 39 | Gold and Silver |
| 40 | Football | 41 | Transparent and Black | 42 | Transparent and Grey |
| 43 | Transparent and White | 44 | Transparent and Yellow | 45 | Transparent and Orange |
| 46 | Transparent and Red | 47 | Transparent and Pink | 48 | Transparent and Purple |
| 49 | Transparent and Blue | 50 | Transparent and Cyan 2 | 51 | Transparent and Cyan |
| 52 | Transparent and Green | 53 | Earth | 54 | Moon |
| 55 | Sun | 56 | Ceres | 57 | Haumea |
| 58 | Jupiter | 59 | Make Make | 60 | Mars |
| 61 | Mercury | 62 | Uranus | 63 | Saturn |
| 64 | Neptune | 65 | Venus Atmo | 66 | Venus |
| 67 | Bright Star 1 | 68 | Bright Star 2 | 69 | Bright Star 3 |
| 70 | Bright Star 4 | 71 | Bright Star 5 | 72 | Bright Star 6 |

### Floors, colour — `floorMaterials` (12)

| ID | Material | ID | Material | ID | Material |
|---:|---|---:|---|---:|---|
| 1 | Dark Grey | 2 | Green | 3 | Grey |
| 4 | Dark Red | 5 | Blue | 6 | Cyan |
| 7 | Orange | 8 | Purple | 9 | Pink |
| 10 | Red | 11 | Yellow | 12 | White |

### Floors, pattern — `patternFloorMaterials` (40)

Four patterns x ten colours. Every pattern's `landItem` material is named `LandItem`, so
these are identified by their folder path under `Assets/Materials/Floors/Patterns/`.

| ID | Pattern | Colour | ID | Pattern | Colour |
|---:|---|---|---:|---|---|
| 101 | TwoLines | Grey | 102 | TwoLines | White |
| 103 | TwoLines | Yellow | 104 | TwoLines | Orange |
| 105 | TwoLines | Red | 106 | TwoLines | Pink |
| 107 | TwoLines | Purple | 108 | TwoLines | Blue |
| 109 | TwoLines | Cyan | 110 | TwoLines | Green |
| 111 | Street | Grey | 112 | Street | White |
| 113 | Street | Yellow | 114 | Street | Orange |
| 115 | Street | Red | 116 | Street | Pink |
| 117 | Street | Purple | 118 | Street | Blue |
| 119 | Street | Cyan | 120 | Street | Green |
| 121 | Circles | Grey | 122 | Circles | White |
| 123 | Circles | Yellow | 124 | Circles | Orange |
| 125 | Circles | Red | 126 | Circles | Pink |
| 127 | Circles | Purple | 128 | Circles | Blue |
| 129 | Circles | Cyan | 130 | Circles | Green |
| 131 | Bricks | Grey | 132 | Bricks | White |
| 133 | Bricks | Yellow | 134 | Bricks | Orange |
| 135 | Bricks | Red | 136 | Bricks | Pink |
| 137 | Bricks | Purple | 138 | Bricks | Blue |
| 139 | Bricks | Cyan | 140 | Bricks | Green |

---

*Regenerate by re-reading the `ballMaterials`, `floorMaterials` and `patternFloorMaterials`
arrays on the MaterialsManager component in `SampleScene.unity` and resolving each entry's
material GUID against the `.mat.meta` files. Diff against this file before committing any
change to those lists.*
