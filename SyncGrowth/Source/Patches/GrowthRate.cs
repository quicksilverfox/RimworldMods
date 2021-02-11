using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;

namespace SyncGrowth.Patches
{
    /**
     * Patch that actually does stuff.
     */
    [HarmonyPatch(typeof(Plant), "GrowthRate", MethodType.Getter)]
    [HarmonyPriority(Priority.VeryLow)]
    static class Plant_GrowthRate_Patch
    {
        public static void Postfix(ref float __result, Plant __instance)
        {
            if (!Settings.mod_enabled)
                return;

            var v = GroupsUtils.GetGrowthMultiplierFor(__instance);
            __result *= v;
        }
    }
}
