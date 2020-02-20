using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsLogic
{
    /*
     * Replaces all meat gained from butchery with chicken meat for generic animals, human meat for humanlikes and insect meat for insects.
     */

    class TastesLikeChicken
    {
        // Verse.Pawn
        // public override IEnumerable<Thing> ButcherProducts(Pawn butcher, float efficiency)
        [HarmonyPatch(typeof(Pawn), "ButcherProducts", new Type[] { typeof(Pawn), typeof(float) })]
        static class Pawn_ButcherProducts_Patch
        {
            static void Postfix(ref IEnumerable<Thing> __result, ref Pawn __instance)
            {
                if (!Settings.tastes_like_chicken || __result == null || !__result.Any())
                {
                    return;
                }

                List<Thing> result = new List<Thing>(__result);
                Thing meat = result.Find(x => x.def.IsIngestible && x.def.ingestible.foodType == FoodTypeFlags.Meat);

                if (meat == null)
                {
                    return;
                }

                if (meat.def.defName.Contains("RawCHFood")) // Cosmic Horrors mod semi-support
                {
                    return; // do nothing
                }
                else if (__instance.RaceProps.Humanlike)
                {
                    meat.def = DefDatabase<ThingDef>.GetNamed("Meat_Human");
                }
                else if (__instance.RaceProps.FleshType == FleshTypeDefOf.Insectoid)
                {
                    meat.def = DefDatabase<ThingDef>.GetNamed("Meat_Megaspider");
                }
                else if (__instance.RaceProps.FleshType == FleshTypeDefOf.Normal)
                {
                    meat.def = DefDatabase<ThingDef>.GetNamed("Meat_Chicken");
                }

                __result = result.AsEnumerable();
            }
        }
    }
}
