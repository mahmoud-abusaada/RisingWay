# abusaada.com — what the game actually needs from it

This folder is the whole website the game depends on. It is deliberately **static**.

## Why not the two PHP files

The endpoint is fetched with `UnityWebRequest.Get` and parsed with
`JsonUtility.FromJson<UpdateVersions>` (`MainMenu.cs:72-105`). It takes no parameters, returns no
per-user data, and touches no database. The PHP was echoing a JSON literal.

Cloudflare cannot run PHP anyway — Pages is static and Workers are JavaScript — but even on PHP
hosting a static file is the better answer here:

- **It is a kill switch.** `androidForceUpdateVersion` drives an update dialog the player cannot
  dismiss. A static file in git has no injection surface, no interpreter to patch, and every
  change is reviewable in history. A PHP file on shared hosting has all three problems.
- Free, and served from Cloudflare's edge.

## The path

The file sits at **`/rising/version.php`**, matching the URL serialized into the scene.

It was originally kept there because installs from the old Play listing poll that exact URL and
can never be repointed. That is no longer a requirement — those users are explicitly out of
scope — so the path is now just the cheapest option: changing it would mean editing the
serialized `URL` on two components in `SampleScene.unity` for no functional gain.

The `.php` extension is meaningless. This is a static file that happens to be named that;
`_headers` sets the correct `Content-Type`, and Unity does not care either way since it parses
`downloadHandler.text`.

## Deploying to Cloudflare Pages

1. Cloudflare dashboard → **Workers & Pages** → **Create** → **Pages** → connect this repo
   (or drag-and-drop the `web/` folder for a one-off upload).
2. Build command: **none**. Build output directory: **`web`**.
3. Custom domains → add **`abusaada.com`**. DNS is already at Cloudflare, so this needs no
   nameserver change.
4. Verify: `curl https://abusaada.com/rising/version.php` must return the JSON below.

## The contract

All eight fields must be present. `UpdateVersions.cs` defaults ints to `0` and strings to `""`,
so a field you omit silently becomes empty — and the four link fields are **persisted to
PlayerPrefs permanently** (`PlayerStats.cs:416-443`). Shipping `{}` would wipe every player's
social links with no way to restore them except a later good response.

| Field | Type | Meaning |
|---|---|---|
| `androidUpdateVersion` | int | Optional-update prompt if `> versionCode` |
| `androidForceUpdateVersion` | int | Same dialog; this is the hard one |
| `iosUpdateVersion` / `iosForceUpdateVersion` | int | The iOS equivalents |
| `facebookLink` / `youtubeLink` / `instagramLink` / `xLink` | string | Social buttons |

## Do not set the version fields above the live build

`ShowUpdateDialog()` (`MainMenu.cs:107-115`) shows the dialog when either value exceeds the
installed `versionCode`, and the dialog has no dismiss path. Setting
`androidForceUpdateVersion` higher than the newest build on Play makes every installed copy
show an undismissable dialog — including builds that have nothing to update to.

The current build is **versionCode 20**, so the committed file uses `20`. Raise these only
*after* a higher version is actually live on the store.

## The client now fails open (fixed)

This endpoint used to be able to brick the game. It no longer can. Three faults combined:

1. `FetchData()` treated only `Result.ConnectionError` as failure, so a 404 or 500 —
   a `ProtocolError` — fell into the success branch and was handed to `JsonUtility.FromJson`,
   which throws on HTML and killed the coroutine part-way through.
2. The dialog was then shown from `updateVersion` / `forceUpdateVersion` cached in PlayerPrefs by
   an earlier successful fetch, so a stale value kept gating startup after the server went away.
3. `Cancel()` called `Application.Quit()` whenever that stale value applied — and in the Editor
   `Quit()` is a no-op, which is why the dialog looked frozen instead of closing.

A force-update is now honoured **only** from a fetch that succeeded in the current session
(`Utility.remoteConfigLoaded`, deliberately not persisted). Any failure hides the dialog and
lets the player straight into the game.

Worth knowing: the dialog's buttons are wired in the scene to **`UpdateHandler`**, not to
`MainMenu` which does the fetching. Both classes were fixed; fixing only `MainMenu` would have
left the Cancel button broken.

The server side still matters for correctness — a partial response blanks the social links, see
the contract above — but no response, or a broken one, is now harmless.

## Still to add

- **A privacy policy page.** Its absence when this domain expired is what got the app delisted.
  It is not referenced from inside the game — it is a field in the Play Console and App Store
  Connect listings, so any stable path works once the page exists.
