using HarmonyLib;
using RimWorld;

namespace SyncGrowth.Patches
{
	[HarmonyPatch(typeof(Plant), "GrowthRate", MethodType.Getter)]
	static class Plant_GrowthRate_Patch
	{
		static void Postfix(ref float __result, Plant __instance)
        {
            if (!Settings.mod_enabled)
                return;

            var v = GroupsUtils.GetGrowthMultiplierFor(__instance);
			__result *= v;
		}
	}
}
