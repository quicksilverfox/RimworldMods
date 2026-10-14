using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SyncGrowth.Patches
{
	/// <summary>
	/// Appends the sync adjustment to the growth rate line of the inspect pane, e.g. "Growth rate: 85% (+12%)".
	/// </summary>
	[HarmonyPatch(typeof(Plant), nameof(Plant.GetInspectString))]
	[HarmonyPriority(Priority.VeryLow)]
	static class Plant_GetInspectString_Patch
	{
		static Regex cachedRegex;
		static string cachedLabel;

		public static void Postfix(ref string __result, Plant __instance)
		{
			if (!Settings.mod_enabled || string.IsNullOrEmpty(__result))
				return;

			var comp = MapCompGrowthSync.For(__instance.Map);
			if (comp == null)
				return;

			float delta = (comp.GrowthMultiplierOf(__instance) - 1f) * 100f;
			if (Math.Abs(delta) < 0.5f)
				return;

			__result = RateRegex().Replace(__result, "$0 (" + delta.ToString("+0;-0") + "%)", 1);
		}

		static Regex RateRegex()
		{
			string label = "GrowthRate".Translate();
			if (cachedRegex == null || cachedLabel != label)
			{
				cachedLabel = label;
				// The negative lookahead keeps the annotation from being added twice when a subclass
				// override of GetInspectString calls the (already patched) base method.
				cachedRegex = new Regex(Regex.Escape(label) + @": [0-9]+%(?! \()", RegexOptions.Compiled);
			}
			return cachedRegex;
		}
	}
}
