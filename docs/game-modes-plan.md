# Game modes, day/night and Explore — plan (for approval, nothing built yet)

Decisions from the discussion with Mahmoud on 2026-10-04.

## 1. Three modes

| | Chill | Standard | Insane |
|---|---|---|---|
| Who | Relaxed play, not focused, new to the genre | Today's game | Fast reactions, competitive |
| Speed | Lower top speed, slow build-up | As now | Higher top speed, reached sooner |
| Turn help | Wider early-tap window | As now | None — no early-tap grace, no one-tap direction help |
| Turn patterns | The easier ones only | Unlock by score, as now | Hard patterns from the start |
| Falling | Unlimited revives (below) | As now | As now |
| Score | No best score: the run's height and "longest without falling" | Best score (today's best carries over) | Its own best score |
| Diamonds | Half rate | ×1 | ×2 |
| Pickups | Diamonds, bolt, double points; no chance; fewer mystery boxes | As now | As now |
| Sound | Calm, close to the menu ambient, softer plucks | As now | Pulse louder, pitch climbs higher and faster |
| Sky | Slow day/night cycle (B) | Dawn near a planet to deep space as you climb (A) | Same as Standard (A) |
| Unlock | Always | Always | Standard best ≥ **300** |

### Chill revive
On a fall the ball is lifted back up and **floats in place with the revive effect looping** — like a chance,
paused halfway. Nothing moves until the player taps; then the new path comes in and the ball drops onto
it. Unlimited, no chance pickup needed. (Standard/Insane revives keep their "never pause" rule; here the
pause is the player's choice.)

### Ads in Chill
- An interstitial **on a fall**, at most once every 3 minutes and never in the first 2 minutes, shown
  as the ball falls — before "tap to continue" appears, so no game tap lands on the ad.
- At the end, an optional rewarded "double your diamonds".

### Separate scores
Best score per mode in the save file (today's best becomes Standard's). Score posts and challenges name
the mode.

### Suggestions (positive, never "you're bad")
- Insane unlocked (Standard best reaches 300): a celebration — "You've unlocked Insane: no help, pure
  reflex. Ready?"
- Chill: only after 3 runs in a row under 20, as an invitation — "Want a relaxed run? Chill: no pressure,
  unlimited revives." Shown once; never again if dismissed.

## 2. Picking the mode — main menu carousel
Swipe left/right between the three modes on the main menu, with:
- the mode's name large, dots under it, arrows at the sides (works without swiping too);
- each mode's own sky tint and ambient, crossfading as you swipe;
- Insane shown locked with its requirement ("Reach 300 in Standard") until it opens.
The last mode played is remembered. New players start on Standard.

## 3. Day/night (built 2026-10-05: AtmosphereSky.cs + Atmosphere.shader)
A new sky, replacing the old blue background and sprite clouds: a planet's air and the planet itself
below the horizon (cloud bands, haze, a glowing rim), drawn over the galaxy. The planet is also a wall
in the depth buffer, so the Sun and planets set behind it.
- **A (Standard, Insane):** dawn just above a planet, the real Sun rising on its horizon. Climbing
  (camera height 20 -> 700) darkens the sky from the top down, drops the horizon and leaves the planet
  as a ball under the track with a glowing rim: deep space. Insane's dawn is red.
- **B (Chill):** a 4-minute day on the clock (morning, noon, sunset, night with stars, sunrise); the
  solar system is lifted and sunk so the Sun really rises and sets.
- Menus and "stay in space": space only, as before.
- Editor checks: StoreCapture `-shotMode <mode> -skyClimb 0..1` (A) or `-skyDay 0..1` (B: 0.25 noon,
  0.5 sunset, 0.75 midnight).

## 4. Explore (later)
An Explore button on the main menu: free camera — drag to orbit round the ball and the solar system,
pinch to zoom, tap a planet for its name and "unlock in the mystery box". Owned balls up close. A clear
way in and out; nothing starts a run by accident. The natural stage for the black hole campaign.

## Order
1. Modes (save format, tuning per mode, Chill revive, scores, rewards, ads, suggestions).
2. The main menu carousel.
3. Day/night A and B.
4. Explore.

## Open questions for later
- Exact numbers per mode (speeds, grace windows, pattern sets) — tuned with test runs on the S20.
- Leaderboards: yes, one per mode — see section 5.

## 5. Leaderboards (code in, waiting on setup)
One board per mode: **Chill — longest climb without falling**, **Standard — best score**, **Insane — best
score** (higher is better, all three). Code: `Assets/Scripts/Utils/Leaderboards.cs`; quiet sign-in at
launch, every run's score sent at game over, the main menu's best chip (with a trophy) opens the board of
the mode on screen. Nothing shows or runs until the boards are set up.

### Android — Google Play Games Services (plugin v2.3.0 in Assets/GooglePlayGames)
Owner steps in the Play Console (Rising Way: Space Ball Run):
1. **Grow users > Play Games Services > Setup and management > Configuration**: create a Play Games
   Services project for the game ("No, my game doesn't use Google APIs" is fine).
2. **Credentials**: add an Android credential — package `com.abusaada.risingway`, the **app signing**
   key's SHA-1 (Play Console > Setup > App signing) and also the **upload** key's SHA-1 for test builds.
3. **Leaderboards**: create three — *Chill: Longest climb*, *Standard: Best*, *Insane: Best* — format
   Numeric, order "Larger is better". Optional icons.
4. **Leaderboards > Get resources**: copy the Android XML and send it to me. I paste it into the plugin's
   setup (it holds the app ID and the three leaderboard IDs) and fill the IDs in Leaderboards.cs.
5. **Testers**: add your Google account under Play Games Services > Testers, then **Publish** the Play
   Games Services configuration (it can be published before the next release).

### Status (2026-10-05)
- Play Games project **Rising Way**, app ID **601087029487** (Google Cloud project `rising-way`).
  Testers come from the app's release tracks.
- Leaderboards made (draft): Standard: Best `CgkI7-HQnL8REAIQAA`, Chill: Longest climb
  `CgkI7-HQnL8REAIQAQ`, Insane: Best `CgkI7-HQnL8REAIQAg` — Number, largest first, tamper protection on.
  IDs in Leaderboards.cs; app ID in `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` and the
  manifest in `Plugins/Android/GooglePlayGamesManifest.androidlib`.
- Done 2026-10-05: OAuth consent screen (External, **in production**; privacy https://abusaada.com/privacy,
  home https://abusaada.com/ - `web/index.html`, to upload; authorised domain abusaada.com; no logo, so
  no verification). Three Android OAuth clients in Google Cloud and three Play Games credentials
  (anti-piracy off, so sideloaded builds can sign in), one per key:
  - app signing `5A:F6:8B:7A:2A:E6:FC:B9:D9:7C:E7:9D:60:9F:65:50:41:EB:52:BF` (Play installs);
  - upload `E2:04:6F:5F:03:89:9D:C4:EC:F9:87:7B:30:55:E8:DA:1C:60:6B:7A`;
  - debug `6C:F3:33:C9:B2:A5:AE:BB:82:43:B8:7C:7B:92:52:B8:9B:80:E5:9D` (the test APKs built here).
  Properties: name "Rising Way: Space Ball Run", Arcade, the store icon and feature graphic.
- Left: **Publish** on Play Games services > Publishing (the owner's click).

### iOS — Game Center (built into Unity; after the Apple membership is renewed)
In App Store Connect > the app > **Game Center**: enable it, then add three leaderboards (Classic,
High score, larger is better) with these IDs (already in the code):
`risingway.chill.longest_climb`, `risingway.standard.best`, `risingway.insane.best`.
