using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Logistics
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.logistics");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), "CalculatedCostAt", new Type[] { typeof(int), typeof(bool), typeof(float) })]
    static class WorldPathGrid_CalculatedCostAt_Patch
    {
        static bool Prefix(ref int __result, int tile, bool perceivedStatic, float yearPercent = -1f)
        {
            Settlement settlement = Find.WorldObjects.SettlementAt(tile);
            if (settlement != null && Faction.OfPlayerSilentFail != null && (settlement.Faction == Faction.OfPlayerSilentFail || settlement.Visitable))
            {
                __result = 0; // Friendly or player-controlled settlements ignore all terrain costs. Home, sweet home!
                return false;
            }
            int num = 0;
            Tile tile2 = Find.WorldGrid[tile];
            if (tile2.biome.impassable)
            {
                __result = 1000000;
                return false;
            }
            bool current = yearPercent == -1f;
            if (yearPercent < 0f)
            {
                yearPercent = (float)DayOfYearAt0Long / 60f;
            }
            float num2 = yearPercent;
            if (Find.WorldGrid.LongLatOf(tile).y < 0f)
            {
                num2 = (num2 + 0.5f) % 1f;
            }
            // If we are calculating path for now, don't apply snow penalty if there are... Well, you know, no snow.
            if (current && GenTemperature.GetTemperatureAtTile(tile) >= 0)
            {
                num2 = 0.33f;
            }
            num += Mathf.RoundToInt(tile2.biome.pathCost.Evaluate(num2) * GetSettlementMoveModifier(tile));
            if (tile2.hilliness == Hilliness.Impassable)
            {
                __result = 1000000;
                return false;
            }
            num += Mathf.RoundToInt(CostFromTileHilliness(tile2.hilliness) * GetSettlementMoveModifier(tile));
            __result = num;
            return false;
        }

        // Even non-friendly settlement has some roads and you can use them.
        private static float GetSettlementMoveModifier(int tile)
        {
            float mod = 1f;
            if (Find.WorldObjects.AnySettlementAt(tile))
            {
                mod = Math.Min(0.3f, mod);
            }
            if (Find.WorldObjects.AnyDestroyedFactionBaseAt(tile))
            {
                mod = Math.Min(0.4f, mod);
            }
            if (Find.WorldObjects.AnySettlementAtOrAdjacent(tile))
            {
                mod = Math.Min(0.5f, mod);
            }
            return mod;
        }

        // copy/paste from WorldPathGrid due to private
        private static int DayOfYearAt0Long
        {
            get
            {
                return GenDate.DayOfYear((long)GenTicks.TicksAbs, 0f);
            }
        }

        // copy/paste from WorldPathGrid due to private
        private static int CostFromTileHilliness(Hilliness hilliness)
        {
            switch (hilliness)
            {
                case Hilliness.Flat:
                    return 0;
                case Hilliness.SmallHills:
                    return 2000;
                case Hilliness.LargeHills:
                    return 6000;
                case Hilliness.Mountainous:
                    return 30000;
                case Hilliness.Impassable:
                    return 30000;
                default:
                    return 0;
            }
        }
    }
}
