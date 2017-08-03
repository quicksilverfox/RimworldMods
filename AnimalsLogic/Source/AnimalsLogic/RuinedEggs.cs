using System;
using Harmony;
using RimWorld;
using Verse;

namespace AnimalsLogic
{
    /*
     * Adds new item in "unfertilized eggs" category: ruined egg. Any fertilized egg which is ruined by temperature becomes one.
     */

    class RuinedEggs
    {
        [HarmonyPatch(typeof(CompTemperatureRuinable), "DoTicks", new Type[] { typeof(int) })]
        static class CompTemperatureRuinable_DoTicks_Patch
        {
            static bool Prefix(ref bool __state, ref CompTemperatureRuinable __instance)
            {
                __state = __instance.Ruined;
                return true;
            }

            static void Postfix(ref bool __state, ref CompTemperatureRuinable __instance)
            {
                if (Settings.convert_ruined_eggs && !__state && __instance.Ruined) // Thing is ruined after this tick
                {
                    ThingWithComps thing = __instance.parent;
                    foreach (var item in thing.def.comps)
                    {
                        if (item.GetType() == typeof(CompProperties_Hatcher))
                        {
                            thing.def = DefDatabase<ThingDef>.GetNamed("EggChickenUnfertilized");
                            return;
                        }
                    }
                }
            }
        }
    }
}
