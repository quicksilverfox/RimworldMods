using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsLogic
{
    /*
     * Transforms any ruined egg into unfertilized chicken egg.
     */
    /*
    class RuinedEggs
    {
        [HarmonyPatch(typeof(CompTemperatureRuinable), "CompTick", new Type[] {})]
        static class CompTemperatureRuinable_CompTick_Patch
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
                    Map map = thing.Map;
                    if(thing.AllComps.Any(x => x.props.GetType() == typeof(CompProperties_Hatcher)))
                    {
                        thing.DeSpawn();
                        thing.def = DefDatabase<ThingDef>.GetNamed("EggChickenUnfertilized");
                        thing.AllComps.Remove(__instance);
                        thing.AllComps.RemoveWhere(x => x.props.GetType() == typeof(CompProperties_Hatcher));
                        thing.SpawnSetup(map, true);
                    }
                }
            }
        }
    }
    */
}
