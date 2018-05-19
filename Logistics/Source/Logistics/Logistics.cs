using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace Logistics
{
    [StaticConstructorOnStartup]
    class Logistics : Mod
    {
#pragma warning disable 0649
        public static Settings Settings;
#pragma warning restore 0649

        public Logistics(ModContentPack content) : base(content)
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.logistics");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.GetSettings<Settings>();
        }

        public void Save()
        {
            LoadedModManager.GetMod<Logistics>().GetSettings<Settings>().Write();
        }

        public override string SettingsCategory()
        {
            return "Logistics".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }

    [HarmonyPatch(typeof(WorldPathGrid), "CalculatedCostAt", new Type[] { typeof(int), typeof(bool), typeof(float) })]
    static class WorldPathGrid_CalculatedCostAt_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo CostFromTileHilliness = typeof(WorldPathGrid).GetMethod("CostFromTileHilliness", AccessTools.all);

            for (int i = 0; i < codes.Count; i++)
            {
                //	IL_00e1: call int32 RimWorld.Planet.WorldPathGrid::CostFromTileHilliness(valuetype RimWorld.Planet.Hilliness)
                if (codes[i].opcode == OpCodes.Call && codes[i].operand == CostFromTileHilliness)
                {
                    codes.RemoveAt(i + 1); // removes [add]
                    codes.InsertRange(i + 1, new List<CodeInstruction>() {
                        // biomeCost already on stack
                        // hillnessCost already on stack
                        new CodeInstruction(OpCodes.Ldarg_0), // push tile id to stack
                        new CodeInstruction(OpCodes.Ldarg_2), // push original value of yearPercent to stack
                        new CodeInstruction(OpCodes.Call, typeof(WorldPathGrid_CalculatedCostAt_Patch).GetMethod(nameof(CheckSnowAndSettlementMods)))
                        // now corrected value is on stack
                    });

                    break;
                }
            }

            return codes.AsEnumerable();
        }

        public static int CheckSnowAndSettlementMods(int biomeCost, int hillnessCost, int tile, float yearPercent)
        {
            if (yearPercent == -1f) // -1 is used for current time, other values are for abstract calculations. Only flat modifier is applied fot abstracts.
            {
                // no snow - no seasonal penalty
                if (Settings.snow_mod && GenTemperature.GetTemperatureAtTile(tile) > 0)
                    biomeCost = Find.WorldGrid[tile].biome.pathCost_summer;
            }

            biomeCost = (int)(biomeCost * Settings.biome_time_modifier);
            hillnessCost = (int)(hillnessCost * Settings.hillness_time_modifier);

            // check settlement factor
            if (Settings.settlement_mod)
                return GetSettlementMoveModifier(tile, biomeCost + hillnessCost);

            return biomeCost + hillnessCost;
        }

        // Even non-friendly settlement has some roads and you can use them.
        public static int GetSettlementMoveModifier(int tile, int calculatedCost)
        {
            Settlement settlement = Find.WorldObjects.SettlementAt(tile);

            if (Faction.OfPlayerSilentFail == null) // map generation stage - no change
                return calculatedCost;

            if (settlement != null && (settlement.Faction == Faction.OfPlayerSilentFail || settlement.Visitable))
                return 0; // Friendly or player-controlled settlements ignore all terrain costs. Home, sweet home!

            float mod = 1f;

            if (settlement != null)
                mod = Math.Min(0.5f, mod); // hostile settlements have half cost

            // Due to how settlements are handled, this stuff is very CPU-heavy. Cut out for now.
            //else if (Find.WorldObjects.AnySettlementAtOrAdjacent(tile))
            //{
            //    mod = Math.Min(0.35f, mod);
            //}
            return (int)(calculatedCost * mod);
        }
    }
}
