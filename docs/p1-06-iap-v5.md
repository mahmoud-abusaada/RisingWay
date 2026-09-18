# P1-06 — Unity IAP v4 → v5

**Scope correction, and a downgrade in urgency.** The plan recorded this as a live API break.
It is not. Verified against the installed package, not from memory.

---

## What is actually true

`com.unity.purchasing` is at **5.4.3**. The v4 types were not removed — they were moved into
`Runtime/Purchasing/Legacy/` and marked:

```csharp
[Obsolete(IAPObsoleteMessages.UpgradeToIAPV5, false)]
public interface IStoreListener
```

The second argument is `false`, meaning **warning, not error**. That is why the project compiles
clean today and only emits CS0618 warnings. There is a working compatibility shim underneath.

So this ticket is *not* a build blocker and does not gate the Android build. It is technical debt
with a deadline set by Unity, not by us: the shim goes away in some future major version.

## How much of our code sits on the shim

`PurchaseMenu.cs` is the only `IStoreListener` implementation. Of the ten Purchasing types it
touches, **nine are legacy**:

| Type | Status |
|---|---|
| `IStoreListener` | legacy |
| `IStoreController` | legacy |
| `IExtensionProvider` | legacy |
| `ConfigurationBuilder` | legacy |
| `UnityPurchasing` | legacy |
| `StandardPurchasingModule` | legacy |
| `PurchaseEventArgs` | legacy |
| `PurchaseProcessingResult` | legacy |
| `IAppleExtensions` | legacy |
| `ProductCatalog` | current |

Only the catalog type survives unchanged. This is therefore a **rewrite of `PurchaseMenu`'s
service plumbing**, not a set of find-and-replace edits. `ProductItem.cs` also references
`UnityEngine.Purchasing` and will need to follow.

Upgrade guide, from the obsolete attribute itself:
<https://docs.unity.com/ugs/en-us/manual/iap/manual/upgrade-to-iap-v5>

## What must not regress

The rewrite has to preserve behaviour that was only just fixed or is easy to lose:

1. **P2-03's double-purchase fix.** `CreateProductsUI()` runs from both `OnEnable` and the
   initialise callback, so the `p.OnPurchase -= HandlePurchase;` before `+=` must survive. Losing
   it re-introduces one `InitiatePurchase` per previous store visit from a single tap.
2. **`remove_ads` entitlement.** `ProcessPurchase` maps it to `PlayerStats.setAdsEnabled(false)`
   plus `HideBannerAd()`. This is the one purchase that writes a permanent entitlement, and P2-04
   moved that entitlement into the save file.
3. **The 21-case product switch.** Consumable payouts are hardcoded by product id. The catalog is
   the source of ids; the amounts live only in this switch. Any rewrite must keep the mapping
   exactly, or players get the wrong quantities.
4. **`RestorePurchases`** currently reaches for `IAppleExtensions` and is iOS-only in practice; on
   Android it silently depends on `extensions` being non-null. Worth re-checking rather than
   porting the same shape forward.

## Recommended sequencing

Do this **after** the Android build is green, not before. It touches the revenue path, it cannot
be verified without a store connection, and it is not blocking anything today. Rewriting live
purchase plumbing while the build itself is still failing would make it impossible to tell which
change broke what.

---

# Done — 2026-09-17

Rewritten on the IAP 5 API. `PurchaseMenu` no longer talks to the store at all.

## Shape

**`Assets/Scripts/Menu/IapStore.cs`** (new) owns everything: it connects at app start
(`RuntimeInitializeOnLoadMethod`), fetches the catalog's products, fetches previous purchases, and
decides what each product grants. It raises `ProductsReady`, `PurchaseGranted`, `PurchaseFailed`
and `PurchaseDeferred`.

**`PurchaseMenu`** is now display and input only: prices, counters, dialogs, and asking `IapStore`
to start a purchase or a restore.

## Why it starts at launch, rather than with the menu

`PurchaseMenu` is **inactive in the scene**, so its `Awake` only ran when a player first opened
the store. Until then the store was never connected, which meant:

- a purchase interrupted by the app being killed was not delivered until the player happened to
  open the store again (IAP replays unfinished purchases when it connects), and
- **Remove Ads was not restored after a reinstall** until the same thing happened.

Both now happen at startup, which is also where IAP 5 expects `FetchPurchases()` to be called.

## The four "must not regress" items

1. **P2-03's double-purchase fix** — kept, with the same comment, now driven by `ProductsReady`
   instead of the initialise callback.
2. **`remove_ads`** — granted from confirmed purchases at startup as well as from a fresh
   purchase, and it hides the banner *before* setting the flag, because `HideBannerAd()` checks
   `isAdEnabled()` and does nothing once ads are off. The old order left the banner up until the
   next screen.
3. **The 21 product payouts** — copied across unchanged, and a product with no case now logs an
   error instead of silently giving nothing.
4. **`RestorePurchases`** — now `StoreController.RestoreTransactions`, which is the supported call
   on both stores (on Google Play purchases are already restored at startup; this is for the App
   Store).

## Other changes this made possible

- Grants are saved immediately (`PlayerStats.Flush()`) before the purchase is confirmed to the
  store, so a crash between the two cannot lose a paid-for item.
- Google Play deferred purchases (payment pending) now tell the player instead of appearing to do
  nothing.
- **Codeless auto-initialisation is off** (`IAPProductCatalog.json`). The project has no codeless
  IAP buttons, and leaving it on meant a second, unused store connection at startup.
- The scene still has old free-grant "BuyButton"s (`PurchaseMenu.BuyDiamonds(20000)` and friends)
  in switched-off sections of the purchase menu. They cannot be reached today; they now only
  refresh the counters and log a warning instead of handing out items, so re-enabling one of those
  sections by accident cannot give the shop away.

## Still to verify

The Editor's fake store exercises the flow, but a real purchase has to be tested against Google
Play with a licence-tested account, including: buy a consumable, kill the app mid-purchase and
relaunch, and reinstall with Remove Ads owned.
