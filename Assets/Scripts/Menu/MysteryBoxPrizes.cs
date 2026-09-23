using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public enum PrizeKind { Diamonds, Bolts, DoublePoints, Chances, Floor, Ball }

/// <summary>How special a prize is. Drives the reveal's colour and size, nothing else.</summary>
public enum PrizeRarity { Common, Rare, Epic, Jackpot }

public struct MysteryBoxPrize
{
    public PrizeKind kind;
    public int amount;             // diamonds or pickups; 0 for a cosmetic
    public BaseMaterial cosmetic;  // the ball or floor, for Ball / Floor
    public PrizeRarity rarity;
    public bool fromPity;          // the guarantee paid out rather than the roll
}

/// <summary>
/// What a mystery box gives, and the odds shown to players. The single source for both: the
/// odds text is generated from the same tables the roll uses, so they cannot disagree.
///
/// Rewritten on 2026-09-23. The old roll lived inside the reveal animation in ShopMenu and had
/// two faults:
///   - its final pick could not repeat the item the animation showed just before it, and was
///     re-rolled when it did. That quietly moved probability from diamonds and pickups onto
///     floors and balls (about 3.6% each instead of the stated 3%), so published odds would
///     have been wrong.
///   - a ball prize came from every locked ball, most of which are sold for diamonds, so the
///     20 balls only a box can give came up about 0.8% of the time.
/// </summary>
public static class MysteryBoxPrizes
{
    // ---- Tuning ------------------------------------------------------------------------------
    // Percent chance of each kind of prize. A kind that has nothing left to give (every ball or
    // every floor owned) drops out, and the rest keep their proportions.
    public static readonly (PrizeKind kind, int weight)[] KindWeights =
    {
        (PrizeKind.Diamonds, 66),
        (PrizeKind.Bolts, 10),
        (PrizeKind.DoublePoints, 10),
        (PrizeKind.Chances, 8),
        (PrizeKind.Floor, 3),
        (PrizeKind.Ball, 3),
    };

    // Diamond prizes, weighted. Averages 495, about what the old flat 100-900 averaged (500), but
    // most boxes give a little and a few give a lot.
    public static readonly (int amount, int weight)[] DiamondAmounts =
    {
        (100, 20), (200, 20), (300, 20), (500, 20), (1000, 15), (2500, 5),
    };

    public const int PICKUP_MIN = 1, PICKUP_MAX = 5; // bolts, double points, chances: equal chance

    /// <summary>
    /// The guarantee: this many boxes in a row without a ball or floor, and the next one gives
    /// one (a ball or a floor, even chance, from whatever is still locked).
    ///
    /// 20, not 10: balls are meant to stay rare. At 20 about 8.5% of boxes give a ball or floor
    /// (6% without any guarantee); at 10 it was 13%.
    /// </summary>
    public const int PITY_BOXES = 20;

    // ---- Roll ----------------------------------------------------------------------------------

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    /// <summary>Development builds only: when set, replaces the roll (for recording each rarity).</summary>
    public static Func<MaterialsManager, MysteryBoxPrize?> DebugOverride;
#endif

    /// <param name="boxesSinceCosmetic">Boxes opened since the last ball or floor (the pity count).</param>
    public static MysteryBoxPrize Roll(MaterialsManager materials, int boxesSinceCosmetic)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (DebugOverride != null)
        {
            MysteryBoxPrize? forced = DebugOverride(materials);
            if (forced.HasValue)
                return forced.Value;
        }
#endif
        List<ColorMaterial> balls = BallPool(materials);
        List<BaseMaterial> floors = materials.getLockedFloorsList();

        if (boxesSinceCosmetic >= PITY_BOXES - 1 && (balls.Count > 0 || floors.Count > 0))
        {
            bool ball = floors.Count == 0 || (balls.Count > 0 && UnityEngine.Random.value < 0.5f);
            MysteryBoxPrize p = ball ? Make(PrizeKind.Ball, balls, floors) : Make(PrizeKind.Floor, balls, floors);
            p.fromPity = true;
            return p;
        }

        var available = KindWeights.Where(k => IsAvailable(k.kind, balls, floors)).ToArray();
        return Make(Weighted(available.Select(k => (k.kind, k.weight)).ToArray()), balls, floors);
    }

    /// <summary>Balls a box can give: the box-only ones first, the diamond ones once those are all owned.</summary>
    public static List<ColorMaterial> BallPool(MaterialsManager materials)
    {
        List<ColorMaterial> locked = materials.getLockedBallsList();
        List<ColorMaterial> exclusive = locked.Where(IsBoxExclusive).ToList();
        return exclusive.Count > 0 ? exclusive : locked;
    }

    public static bool IsBoxExclusive(BaseMaterial m) { return m.price == -1; }

    private static bool IsAvailable(PrizeKind kind, List<ColorMaterial> balls, List<BaseMaterial> floors)
    {
        if (kind == PrizeKind.Ball) return balls.Count > 0;
        if (kind == PrizeKind.Floor) return floors.Count > 0;
        return true;
    }

    private static MysteryBoxPrize Make(PrizeKind kind, List<ColorMaterial> balls, List<BaseMaterial> floors)
    {
        MysteryBoxPrize p = new MysteryBoxPrize { kind = kind };
        switch (kind)
        {
            case PrizeKind.Diamonds:
                p.amount = Weighted(DiamondAmounts);
                p.rarity = p.amount >= 2500 ? PrizeRarity.Jackpot : p.amount >= 500 ? PrizeRarity.Rare : PrizeRarity.Common;
                break;
            case PrizeKind.Bolts:
            case PrizeKind.DoublePoints:
            case PrizeKind.Chances:
                p.amount = UnityEngine.Random.Range(PICKUP_MIN, PICKUP_MAX + 1);
                p.rarity = p.amount >= 4 ? PrizeRarity.Rare : PrizeRarity.Common;
                break;
            case PrizeKind.Floor:
                p.cosmetic = floors[UnityEngine.Random.Range(0, floors.Count)];
                p.rarity = p.cosmetic is PatternMaterial ? PrizeRarity.Epic : PrizeRarity.Rare;
                break;
            case PrizeKind.Ball:
                p.cosmetic = balls[UnityEngine.Random.Range(0, balls.Count)];
                p.rarity = IsBoxExclusive(p.cosmetic) ? PrizeRarity.Jackpot : PrizeRarity.Epic;
                break;
        }
        return p;
    }

    private static T Weighted<T>((T value, int weight)[] table)
    {
        int total = 0;
        foreach (var e in table) total += e.weight;
        int r = UnityEngine.Random.Range(0, total);
        foreach (var e in table)
        {
            if (r < e.weight) return e.value;
            r -= e.weight;
        }
        return table[table.Length - 1].value;
    }

    public static bool IsCosmetic(PrizeKind kind) { return kind == PrizeKind.Ball || kind == PrizeKind.Floor; }

    // ---- Odds disclosure -----------------------------------------------------------------------

    /// <summary>
    /// The odds as players see them (Play policy: randomised items must show their odds before
    /// purchase). Generated from the tables above; reflects what this player can still win.
    /// </summary>
    public static string OddsText(MaterialsManager materials)
    {
        List<ColorMaterial> balls = BallPool(materials);
        List<BaseMaterial> floors = materials.getLockedFloorsList();
        var available = KindWeights.Where(k => IsAvailable(k.kind, balls, floors)).ToArray();
        float total = available.Sum(k => k.weight);
        int diamondTotal = DiamondAmounts.Sum(d => d.weight);

        StringBuilder sb = new StringBuilder();
        foreach (var k in available)
        {
            float pct = 100f * k.weight / total;
            sb.Append(Label(k.kind)).Append(": ").Append(Pct(pct));
            if (k.kind == PrizeKind.Diamonds)
            {
                // One line per distinct chance: "100, 200, 300, 500: 13.2% each".
                foreach (var group in DiamondAmounts.GroupBy(d => d.weight))
                {
                    var amounts = group.Select(d => Utility.getFormatedNumber(d.amount)).ToArray();
                    sb.Append("\n   ").Append(string.Join(", ", amounts)).Append(": ")
                      .Append(Pct(pct * group.Key / diamondTotal)).Append(amounts.Length > 1 ? " each" : "");
                }
            }
            else if (!IsCosmetic(k.kind))
            {
                sb.Append(" (" + PICKUP_MIN + " to " + PICKUP_MAX + ")");
            }
            sb.Append('\n');
        }
        int exclusive = materials.getLockedBallsList().Count(IsBoxExclusive);
        if (balls.Count > 0)
            sb.Append(exclusive > 0 ? "Balls come from the " + exclusive + " box-only balls first.\n"
                                    : "You own every box-only ball; balls now come from the shop's.\n");
        sb.Append("No ball or floor in " + (PITY_BOXES - 1) + " boxes? The next box gives one.");
        return sb.ToString();
    }

    private static string Pct(float p) { return p.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"; } // exact to 0.1: these are published odds

    public static string Label(PrizeKind kind)
    {
        switch (kind)
        {
            case PrizeKind.Diamonds: return "Diamonds";
            case PrizeKind.Bolts: return "Bolts";
            case PrizeKind.DoublePoints: return "Double points";
            case PrizeKind.Chances: return "Chances";
            case PrizeKind.Floor: return "Floor";
            default: return "Ball";
        }
    }
}
