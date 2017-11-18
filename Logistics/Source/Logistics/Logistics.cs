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
    class Logistics
    {
        static Logistics()
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.logistics");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), "CalculatedCostAt", new Type[] { typeof(int), typeof(bool), typeof(float) })]
    static class WorldPathGrid_CalculatedCostAt_Patch
    {
        // This is a pretty dirty way to go, since it prevents original function from invoking, as well as other mods which can use it. But it has too many inline modifications to use Prefix/Postfix pair or Transpiler.
        static bool Prefix(ref int __result, int tile, bool perceivedStatic, float yearPercent = -1f)
        {
            int num = 0;
            Tile tile2 = Find.WorldGrid[tile];
            if (tile2.biome.impassable || tile2.hilliness == Hilliness.Impassable) // Impassable Map Maker would override this in Postfix, and it won't be affected by this mod.
            {
                __result =  1000000;
            }

            Settlement settlement;
            Faction playerFaction = Faction.OfPlayerSilentFail;
            if (playerFaction != null && (settlement = Find.WorldObjects.SettlementAt(tile)) != null && (settlement.Faction == playerFaction || settlement.Visitable))
            {
                __result = 0; // Friendly or player-controlled settlements ignore all terrain costs. Home, sweet home!
                return false;
            }
            if (yearPercent < 0f)
            {
                yearPercent = (float)DayOfYearAt0Long / 60f;
            }
            float num2;
            float num3;
            float num4;
            float num5;
            float num6;
            float num7;
            SeasonUtility.GetSeason(yearPercent, Find.WorldGrid.LongLatOf(tile).y, out num2, out num3, out num4, out num5, out num6, out num7);
            num += Mathf.RoundToInt((float)tile2.biome.pathCost_spring * num2 + (float)tile2.biome.pathCost_summer * num3 + (float)tile2.biome.pathCost_fall * num4 + (float)tile2.biome.pathCost_winter * num5 + (float)tile2.biome.pathCost_summer * num6 + (float)tile2.biome.pathCost_winter * num7);
            __result = Mathf.RoundToInt((num + CostFromTileHilliness(tile2.hilliness)) * GetSettlementMoveModifier(tile));
            
            // Old calculations.
            //bool calculateForCurrentTime = yearPercent == -1f;
            //if (yearPercent < 0f)
            //{
            //    yearPercent = (float)DayOfYearAt0Long / 60f;
            //}
            //float num2 = yearPercent;
            //if (Find.WorldGrid.LongLatOf(tile).y < 0f)
            //{
            //    num2 = (num2 + 0.5f) % 1f;
            //}

            //// If we are calculating path for now, don't apply snow penalty if there are... Well, you know, no snow.
            //if (calculateForCurrentTime && GenTemperature.GetTemperatureAtTile(tile) >= 0)
            //{
            //    num2 = 0.33f;
            //}
            //num += Mathf.RoundToInt(tile2.biome.pathCost.Evaluate(num2) * GetSettlementMoveModifier(tile));
            //num += Mathf.RoundToInt(CostFromTileHilliness(tile2.hilliness) * GetSettlementMoveModifier(tile));
            //__result = num;

            return false;
        }

        // Even non-friendly settlement has some roads and you can use them.
        private static float GetSettlementMoveModifier(int tile)
        {
            float mod = 1f;
            // Due to how settlements are handled, this stuff is vey CPU-heavy. Cut out for now.
            //if (Find.WorldObjects.AnySettlementAt(tile))
            //{
            //    mod = Math.Min(0.3f, mod);
            //}
            //if (Find.WorldObjects.AnyDestroyedFactionBaseAt(tile))
            //{
            //    mod = Math.Min(0.4f, mod);
            //}
            //if (Find.WorldObjects.AnySettlementAtOrAdjacent(tile))
            //{
            //    mod = Math.Min(0.5f, mod);
            //}
            return mod;
        }

        // Copy/paste from WorldPathGrid due to private. Easier and faster than messing with reflection, even if not as safe.
        private static int DayOfYearAt0Long
        {
            get
            {
                return GenDate.DayOfYear((long)GenTicks.TicksAbs, 0f);
            }
        }

        // Copy/paste from WorldPathGrid due to private. Easier and faster than messing with reflection, even if not as safe.
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
