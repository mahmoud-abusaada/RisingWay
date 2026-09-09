# Rising Way — Analytics Event Schema

**Ticket:** P3-02 (design) → P3-03 (implementation)
**Status:** Draft for review. Nothing implemented yet.
**Platform:** Firebase Analytics (Unity SDK). Unity Analytics is being removed — see P3-04.

---

## 1. Why this exists

Rising Way currently logs **one** analytics event: `FirebaseAnalytics.EventLogin`, fired when
the player opens the *shop*. It is mislabelled and measures nothing useful. There is no crash
reporting at all.

The consequence is that the high user-loss rate that motivated this whole project is
**unobservable**. We cannot see where players quit, whether the economy is reachable, or
whether purchases succeed. Every improvement in Phases 5–7 would be a guess.

This schema exists to answer seven specific questions. Every event below earns its place by
contributing to at least one of them:

| # | Question | Primary events |
|---|---|---|
| Q1 | Where in the first session do players quit? | `tutorial_*`, `run_start`, `run_end` |
| Q2 | Do players come back, and what predicts it? | user properties + `run_start` |
| Q3 | Why do runs end? | `run_end.end_reason` |
| Q4 | Is the diamond economy actually reachable? | `currency_earned`, `currency_spent`, `spend_blocked` |
| Q5 | Do ads load, show and reward correctly? | `ad_*` |
| Q6 | Do purchases work, and where do they fail? | `store_opened.iap_ready`, `iap_*` |
| Q7 | Is the game fast enough on real devices? | `boot_complete`, `perf_sample` |

**Q4 and Q6 have known live defects.** The economy is priced ~2 orders of magnitude out of
reach, and we have a device screenshot proving the store renders fake hardcoded USD prices
when IAP fails to initialise. `spend_blocked` and `store_opened.iap_ready` are designed
specifically to quantify those two.

---

## 2. Conventions

- **snake_case** for event and parameter names.
- Firebase limits: 500 distinct event names, **25 params per event**, param names ≤ 40 chars,
  string values ≤ 100 chars, **25 user properties**. We are well inside all of these.
- **Reserved names we must avoid** (Firebase blocks them): `ad_impression`, `ad_click`,
  `ad_reward`, `ad_query`, `ad_exposure`, `ad_activeview`, `adunit_exposure`, `session_start`,
  `first_open`, `in_app_purchase`, `user_engagement`, `screen_view`, `app_update`,
  `app_remove`, `app_exception`, `os_update`, `notification_*`, and anything prefixed
  `firebase_`, `google_`, `ga_`.
  Our ad events are deliberately named `ad_shown` / `ad_reward_earned` to stay clear of these.
- **Firebase auto-logs** `first_open`, `session_start`, `app_update`, `os_update` and (via the
  AdMob SDK) `ad_impression`. Do not duplicate them.
- `tutorial_begin` and `tutorial_complete` are Firebase *recommended* names — we use them as-is
  so they light up the standard reports.
- **No PII.** No email, no name, no advertising ID, no free-text. Every param below is an enum,
  a bounded integer, or a float.

---

## 3. User properties

Set once and used to segment every event. Max 25; we use 8.

| Property | Type | Values | Why |
|---|---|---|---|
| `control_scheme` | string | `single_tap` \| `tap_lr` \| `swipe_lr` | Q1. Does input choice predict churn? |
| `ads_removed` | string | `true` \| `false` | Segment paying users; also verifies P2-01 held |
| `total_runs` | string (bucket) | `1`, `2-5`, `6-20`, `21-100`, `100+` | Q2. Engagement depth |
| `high_score_bucket` | string | `0-49`, `50-199`, `200-599`, `600+` | Q3. Skill tier. 600 is where difficulty stops |
| `collection_owned` | string (bucket) | `1-2`, `3-10`, `11-30`, `31+` | Q4. Is anyone actually collecting? |
| `save_schema_version` | string | integer as string | Catches data-shaped crashes after P2-04 |
| `device_tier` | string | `low` \| `mid` \| `high` | Q7. Derived from RAM + processor count |
| `install_source` | string | `fresh` \| `returning` | Distinguishes new installs from pre-delisting users |

> Bucket rather than log raw values. Firebase user properties are strings and high-cardinality
> values are useless for segmentation.

---

## 4. Events

### 4.1 Lifecycle

| Event | Params | Notes |
|---|---|---|
| `boot_complete` | `duration_ms` (int), `device_tier` | Fires once when the main menu is first interactive. **Directly measures the 885-object synchronous boot** that P4-02 fixes. This is our before/after number. |
| `app_foregrounded` | `seconds_backgrounded` (int) | Needs the `OnApplicationPause` handler added in P2-06. Also tells us how often players are interrupted mid-run. |

### 4.2 Onboarding funnel — the Q1 events

This is the most important block in the schema. The tutorial currently ends after two turns,
and we have no idea how many players reach even that.

| Event | Params | Notes |
|---|---|---|
| `tutorial_begin` | `control_scheme` | First run, tutorial flag on. |
| `tutorial_step` | `step`, `control_scheme`, `seconds_elapsed` (int) | `step` ∈ `flag_1_shown`, `flag_1_turned`, `flag_2_shown`, `flag_2_turned`, `perfect_shown`, `ready_shown`. Six steps = a six-point drop-off curve. |
| `tutorial_complete` | `control_scheme`, `duration_sec` (int) | Fires where `setTutorialsState(false)` is called. |
| `tutorial_abandon` | `last_step`, `duration_sec` (int) | Fires on app background or quit while the tutorial flag is still on. **This is the single most valuable event in the schema** — it names the exact step where first-session players give up. |
| `control_scheme_chosen` | `scheme`, `source` | `source` ∈ `tutorial_chooser` \| `settings` \| `default`. Measures P5-02 once the chooser is restored. |

### 4.3 Core loop — Q3

| Event | Params | Notes |
|---|---|---|
| `run_start` | `run_number` (int), `control_scheme`, `is_tutorial` (bool), `diamonds_balance` (int), `bolt_level`, `double_points_level`, `chance_level` | |
| `run_end` | `score` (int), `max_altitude` (int), `duration_sec` (int), `diamonds_collected` (int), `turns_taken` (int), `top_speed` (float), `revives_used` (int), `powerups_used` (int), `end_reason` | `end_reason` ∈ `fell` \| `quit_to_menu` \| `restarted` \| `backgrounded`. |
| `run_milestone` | `milestone`, `score`, `duration_sec` | Altitude thresholds. `AmbientEffectsController` already switches behaviour at 50 / 400 / 450 / 600 / 800 — reuse those. Feeds P6-03. |
| `high_score_beaten` | `score`, `previous_score`, `run_number` | Q2. Beating a personal best is the strongest retention moment the game currently has. |

> **Watch `run_end.score` against 600.** That is where speed caps and the pattern pool stops
> growing. If a meaningful share of runs cluster past it, P6-04 is urgent.

### 4.4 Economy — the Q4 events

| Event | Params | Notes |
|---|---|---|
| `currency_earned` | `currency`, `amount` (int), `source`, `balance_after` (int) | `currency` ∈ `diamond` \| `bolt` \| `double_points` \| `chance` \| `mystery_box`. `source` ∈ `pickup` \| `mystery_box` \| `iap` \| `ad_reward` \| `restore`. |
| `currency_spent` | `currency`, `amount` (int), `sink`, `item_id`, `balance_after` (int) | `sink` ∈ `cosmetic_ball` \| `cosmetic_floor` \| `upgrade_bolt` \| `upgrade_double_points` \| `upgrade_chance` \| `powerup_activated`. |
| `spend_blocked` | `sink`, `item_id`, `price` (int), `balance` (int), `shortfall` (int) | **Fires when the player tries to buy something they cannot afford.** Directly measures the "priced out of reach" problem. The distribution of `shortfall` tells us exactly how far off the economy is, and gives P6-01 a target instead of a guess. |
| `item_unlocked` | `item_type`, `item_id`, `method`, `owned_count` (int), `total_count` (int) | `method` ∈ `purchase` \| `mystery_box` \| `default`. `owned_count` / `total_count` feed the "12 of 72" surfacing in P6-02. |
| `upgrade_purchased` | `track`, `new_level` (int), `price` (int) | |

### 4.5 Ads — Q5

One event per state transition, all carrying `placement`.

`placement` ∈ `banner` | `interstitial` | `revive` | `mystery_box` | `app_open` | `rewarded_interstitial`

| Event | Params |
|---|---|
| `ad_requested` | `placement` |
| `ad_loaded` | `placement`, `load_ms` (int) |
| `ad_load_failed` | `placement`, `error_code` (int), `error_domain` |
| `ad_shown` | `placement` |
| `ad_dismissed` | `placement`, `watched_sec` (int) |
| `ad_reward_earned` | `placement` |
| `ad_show_failed` | `placement`, `error_code` (int) |

**Revive-specific, because it is where the money is:**

| Event | Params | Notes |
|---|---|---|
| `revive_offered` | `score`, `revive_number` (int), `ad_ready` (bool) | |
| `revive_declined` | `score`, `declined_by` | `declined_by` ∈ `timeout` \| `button`. **A high `timeout` share means the slider UI is illegible** — exactly the P5-05 hypothesis, now measurable. |
| `revive_succeeded` | `score`, `revive_number` | Must fire *after* the player is actually back in play. This is the regression test for the P2-02 bug where the reward was granted and then immediately cancelled. |

### 4.6 Store and IAP — Q6

| Event | Params | Notes |
|---|---|---|
| `store_opened` | `source`, `iap_ready` (bool), `diamonds_balance` (int) | `source` ∈ `main_menu` \| `locked_item` \| `insufficient_funds` \| `mystery_box_prompt`. **`iap_ready: false` is the fake-storefront bug** — the player is looking at hardcoded USD prices that will fail on tap. |
| `iap_init_failed` | `reason` | Fires from `OnInitializeFailed`. Currently swallowed entirely. |
| `iap_purchase_started` | `product_id` |  |
| `iap_purchase_completed` | `product_id`, `price` (float), `currency_code` | Named to avoid the reserved `in_app_purchase`. |
| `iap_purchase_failed` | `product_id`, `failure_reason` | |
| `iap_restore_result` | `success` (bool), `entitlements_found` (int) | Covers the P2-08 `remove_ads` re-grant. |

### 4.7 Navigation and settings

| Event | Params | Notes |
|---|---|---|
| `menu_opened` | `menu`, `source` | Replaces the mislabelled `EventLogin`. `menu` matches the `Menus` enum. |
| `setting_changed` | `setting`, `new_value` | Catches tutorial re-enable, control-scheme switching, quality changes. |

### 4.8 Performance — Q7

| Event | Params | Notes |
|---|---|---|
| `perf_sample` | `avg_fps` (int), `min_fps` (int), `device_tier`, `context` | Sampled at the end of each run, not continuously. `context` ∈ `run` \| `shop` \| `menu`. The shop is a known hotspot (126 raycasting Updates, per-moon LineRenderers). |

---

## 5. What this obliges us to declare

Every parameter here becomes a disclosure in **Play Data Safety** and the **iOS privacy
manifest** (P9-01). The schema is deliberately free of PII to keep those declarations narrow:

- **Collected:** app interactions, in-app purchase history, crash and performance data.
- **Not collected:** name, email, precise location, contacts, photos, files, free text.
- **Linked to identity:** no. Firebase's pseudonymous app instance ID only.
- Firebase Analytics still collects coarse device, OS and region data automatically — that must
  be declared regardless of this schema.

Adding any parameter later means revisiting both declarations. Keep it lean on purpose.

---

## 6. Implementation notes

- Route everything through one thin `Analytics` wrapper rather than calling
  `FirebaseAnalytics.LogEvent` from 30 places. It gives us one spot for the "is Firebase
  initialised" guard, offline buffering, and a `#if UNITY_EDITOR` no-op.
- **Set user properties before the first event of a session**, or the first session's events
  land unsegmented.
- Analytics must **never** throw into gameplay. Wrap the wrapper in try/catch and fail silent.
- Firebase initialisation is async and currently unchecked. Events logged before it completes
  are dropped — buffer them.
- `tutorial_abandon` needs the `OnApplicationPause` handler from P2-06, so **P2-06 blocks it.**
- Ship this and let it run long enough for a stable baseline **before** any Phase 5 gameplay
  change lands. Otherwise the before/after comparison the whole plan depends on is worthless.

---

## 7. Open questions

1. **Do we want Firebase Remote Config?** It would let us tune the P6-01 economy without a
   store release, which is valuable given how uncertain the rebalance is. It also replaces the
   dead `abusaada.com/rising/version.php` dependency properly. Adds SDK weight, which fights
   P4-10.
2. **Do we need a `run_abandoned` event** for players who background mid-run and never return?
   `run_end.end_reason = backgrounded` may be enough.
3. **Sampling rate for `perf_sample`** — every run, or 1-in-N? Every run is fine at current
   scale and gives cleaner data.
