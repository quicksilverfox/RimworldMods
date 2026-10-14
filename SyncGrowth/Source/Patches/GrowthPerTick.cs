using HarmonyLib;
using RimWorld;

namespace SyncGrowth.Patches
{
	/// <summary>
	/// The patch that does the actual work. Plant.GrowthPerTick is non-virtual and is the only path
	/// through which growth is applied, so patching it covers every Plant subclass, including those from
	/// other mods that override the virtual GrowthRate getter.
	/// </summary>
	[HarmonyPatch(typeof(Plant), "GrowthPerTick", MethodType.Getter)]
	[HarmonyPriority(Priority.VeryLow)]
	static class Plant_GrowthPerTick_Patch
	{
		public static void Postfix(ref float __result, Plant __instance)
		{
			if (!Settings.mod_enabled || __result <= 0f)
				return;

			var comp = MapCompGrowthSync.For(__instance.Map);
			if (comp != null)
				__result *= comp.GrowthMultiplierOf(__instance);
		}
	}
}
