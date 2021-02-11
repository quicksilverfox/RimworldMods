using System.Text;
using Verse;
using System.Text.RegularExpressions;
using RimWorld;
using HarmonyLib;

namespace SyncGrowth.Patches
{
	[HarmonyPatch(typeof(Plant), "GetInspectString")]
	static class Plant_GetInspectString_Patch
	{
		static void Postfix(ref string __result, Plant __instance)
        {
            if (!Settings.mod_enabled)
                return;

            if (!GroupsUtils.HasGroup(__instance))
				return;

			//var stringBuilder = new StringBuilder(__result);
			var regex = new Regex(("GrowthRate".Translate()) + ": [0-9]+%");
			var delta = (GroupsUtils.GetGrowthMultiplierFor(__instance) - 1f) * 100f;
			//var shownDelta = Mathf.Round()

			if (delta >= 0.5 || delta <= -0.5)
				if (regex.IsMatch(__result))
				{
					var replace = "$0 (" + delta.ToString("+##;-##") + "%)";
					__result = regex.Replace(__result, replace);
				}
#if DEBUG
				else
				{
					__result += "\n(regex error)";
				}
			__result += "\nRaw delta = " + delta.ToString();
			__result += "\nCanHaveGroup() = " + GroupMaker.CanHaveGroup(__instance, true);
#endif
		}
	}
}
