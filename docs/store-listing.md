# Rising Way - Google Play store listing (2026 relaunch)

Written fresh for the relaunch; nothing reused from the 2024 listing. Every claim below is true of
build 1.0.2 - check it again before changing the game (counts come from MaterialsManager in
SampleScene: 72 balls, 12 colour floors + 39 pattern floors).

## App name (30 max)

    Rising Way: Space Ball Run

26 characters. "Space", "ball" and "run" are what people type when looking for this kind of
game; the brand stays first.

## Short description (80 max)

    Tap to turn. Climb an endless neon track through space and collect rare planets.

80 characters.

## Full description (4000 max)

    Roll a glowing ball up an endless neon track floating through space. One tap turns you at the corner - tap too late and you fall into the stars. How high can you climb?

    Rising Way is easy to learn and hard to put down. Every run builds a new path in front of you: long straights, sharp turns, ramps, and - the higher you climb - spirals and tight climbs that test your timing.

    ★ ONE-TAP CONTROLS
    Tap to turn at every corner. Prefer something else? Switch to tap left/right or swipe in Settings.

    ★ POWER-UPS
    • Bolt - blast forward at super speed, turning on its own
    • Double Points - every step counts twice
    • Chances - fall off and get put straight back on the track
    Upgrade them with diamonds: longer bolts, longer double points, more chances.

    ★ COLLECT DIAMONDS
    Grab diamonds on the way up and spend them on new looks and upgrades.

    ★ 70+ BALLS, 50+ TRACKS
    Roll as a colourful ball, a gold-and-silver sphere or a football - or unlock the rare ones: Saturn, Jupiter, the Sun, the Moon and blazing stars. Pick from more than 50 neon and colour tracks to roll on.

    ★ MYSTERY BOXES
    Find mystery boxes on the track and open them for diamonds, power-ups, new tracks and the rarest balls in the game. Every box's odds are listed in the game.

    ★ PLAY ANYWHERE
    No sign-up, no waiting. Runs last a minute or two - perfect for a quick break.

    Beat your best score, share it, and see how far up the Rising Way goes.

Roughly 1,300 characters - deliberately short: players read the first two lines and the headers.

## Store settings

- Category: **Games > Arcade**
- Tags (up to 5): Arcade, Casual, Endless runner, Offline, Single player
- Contains ads: yes. In-app purchases: yes.
- Website: https://abusaada.com - Privacy policy: https://abusaada.com/privacy

## Graphics - what Play requires

| Asset | Size | Format |
|---|---|---|
| App icon | 512 x 512 | 32-bit PNG, up to 1 MB. Play applies its own rounded mask: keep the subject inside the centre ~80%. |
| Feature graphic | 1024 x 500 | JPEG or 24-bit PNG, no transparency |
| Phone screenshots | 2-8; portrait 9:16, at least 1080 px wide | JPEG or 24-bit PNG. Games need at least 4 at 1080p or more to be eligible for promotion on Play. |

The icon and feature graphic stay the ones from the old listing. The six screenshots are in
`Builds/StoreArt/final/` (not committed - the build folder is ignored), in this order:

1. `01_tap_to_turn.jpg` - TAP TO TURN / One tap at every corner
2. `02_bolt.jpg` - BOLT BOOST / Blast up the track at super speed
3. `03_diamonds.jpg` - GRAB DIAMONDS / Unlock new balls and tracks
4. `04_mystery_box.jpg` - MYSTERY BOXES / Win rare planets, stars and moons
5. `05_planets.jpg` - COLLECT PLANETS / Saturn, Mars, Neptune and more
6. `06_stars.jpg` - 70+ BALLS / Blazing stars in every colour

How they are made:
- Gameplay (01-03): rendered headless by `Assets/Scripts/Diagnostics/StoreCapture.cs`.
- Shop shots (04-06): taken on the emulator from a build with the `STORE_CAPTURE` define and the
  `reveal-test` / `unlock-all` markers (see `MysteryBoxRevealTest.cs`).
- All captioned by `Assets/Editor/StoreArtComposer.cs` from `Tools/StoreArt/spec_review.json`,
  `spec_batch3.json` (03 only) and `spec_batch4.json`.

Keep Earth out of every picture until its ball is fixed: it renders broken.
