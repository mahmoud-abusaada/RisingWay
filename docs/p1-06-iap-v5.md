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
