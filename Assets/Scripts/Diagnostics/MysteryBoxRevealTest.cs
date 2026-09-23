// Development builds only: makes the mystery box reveal testable on a device that has no boxes
// (and no box ads to earn one from).
//
// Active only while the marker file "reveal-test" exists in the app's persistent data folder:
//   adb shell run-as com.abusaada.risingway touch files/reveal-test
// Then every time the shop opens with fewer than 5 boxes it tops them up to 30, and boxes give a
// fixed cycle of prizes, one per rarity, so each version of the reveal can be recorded:
//   diamonds jackpot, a box-only ball, a pattern floor, 5 bolts, 200 diamonds.
// The prizes are real grants - this is for test devices.
//
// Deliberately excluded from release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using System.Linq;
using UnityEngine;

public static class MysteryBoxRevealTest
{
    private const string MARKER_FILE = "reveal-test";
    private static int next;

    public static void Apply(PlayerStats stats)
    {
        if (!File.Exists(Path.Combine(Application.persistentDataPath, MARKER_FILE)))
        {
            MysteryBoxPrizes.DebugOverride = null;
            return;
        }
        if (stats.getBoxesCount() < 5)
            stats.addBoxes(30 - stats.getBoxesCount());
        MysteryBoxPrizes.DebugOverride = Cycle;
        Debug.Log("[MysteryBoxRevealTest] active: boxes topped up, prizes cycle through each rarity");
    }

    private static MysteryBoxPrize? Cycle(MaterialsManager materials)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int step = next++ % 5;
            switch (step)
            {
                case 0: return new MysteryBoxPrize { kind = PrizeKind.Diamonds, amount = 2500, rarity = PrizeRarity.Jackpot };
                case 1:
                {
                    var ball = MysteryBoxPrizes.BallPool(materials).FirstOrDefault();
                    if (ball == null) continue;
                    return new MysteryBoxPrize { kind = PrizeKind.Ball, cosmetic = ball,
                        rarity = MysteryBoxPrizes.IsBoxExclusive(ball) ? PrizeRarity.Jackpot : PrizeRarity.Epic };
                }
                case 2:
                {
                    var floor = materials.getLockedFloorsList().OfType<PatternMaterial>().FirstOrDefault();
                    if (floor == null) continue;
                    return new MysteryBoxPrize { kind = PrizeKind.Floor, cosmetic = floor, rarity = PrizeRarity.Epic };
                }
                case 3: return new MysteryBoxPrize { kind = PrizeKind.Bolts, amount = 5, rarity = PrizeRarity.Rare };
                default: return new MysteryBoxPrize { kind = PrizeKind.Diamonds, amount = 200, rarity = PrizeRarity.Common };
            }
        }
        return null;
    }
}
#endif
