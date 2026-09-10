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

## The path must not change

Keep the file at exactly **`/rising/version.php`**. Copies of the game installed from the old
Play Store listing still poll that URL and cannot be updated to a new one. The `.php` extension
is meaningless here — it is a static file that happens to be named that. `_headers` sets the
correct `Content-Type`; Unity does not care either way, since it parses `downloadHandler.text`.

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

## Known client bug this file cannot fix

`FetchData()` only treats `Result.ConnectionError` as failure. A `ProtocolError` (404, 500) or a
`DataProcessingError` falls into the success branch and is handed to `JsonUtility.FromJson`,
which throws on non-JSON and kills the coroutine mid-way. So a misconfigured server is handled
worse than an unreachable one. Fixing that is a client-side ticket; until then, **do not let this
path 404** — an error page is more harmful than downtime.

## Still to add

- **A privacy policy page.** Its absence when this domain expired is what got the app delisted.
  It is not referenced from inside the game — it is a field in the Play Console and App Store
  Connect listings, so any stable path works once the page exists.
