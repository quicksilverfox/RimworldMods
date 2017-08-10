using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;

namespace AnimalsLogic
{
    class TastesLikeChicken
    {
        // Verse.Pawn
        // public override IEnumerable<Thing> ButcherProducts(Pawn butcher, float efficiency)
        [HarmonyPatch(typeof(Pawn), "ButcherProducts", new Type[] { typeof(Pawn), typeof(float) })]
        static class Pawn_ButcherProducts_Patch
        {
            static void Postfix(ref IEnumerable<Thing> __result, ref Pawn __instance)
            {
                if (!Settings.tastes_like_chicken)
                {
                    return;
                }

                List<Thing> result = new List<Thing>(__result);
                Thing meat = result.Find(x => x.def.IsIngestible && x.def.ingestible.foodType == FoodTypeFlags.Meat);

                if (meat.def.defName.Contains("StrangeFlesh")) // Cosmic Horrors mod semi-support
                {
                    // do nothing
                }
                else if (__instance.RaceProps.Humanlike)
                {
                    meat.def = DefDatabase<ThingDef>.GetNamed("Human_Meat");
                }
                else if (__instance.RaceProps.FleshType == FleshTypeDefOf.Insectoid)
                {
                    meat.def = DefDatabase<ThingDef>.GetNamed("Megaspider_Meat");
                }
                else if (__instance.RaceProps.FleshType == FleshTypeDefOf.Insectoid)
                {
                    meat.def = DefDatabase<ThingDef>.GetNamed("Chicken_Meat");
                }

                __result = result.AsEnumerable();
            }
        }
    }
}
