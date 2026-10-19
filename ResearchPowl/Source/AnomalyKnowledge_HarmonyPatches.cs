// AnomalyKnowledge_HarmonyPatches.cs
// Anomaly knowledge is gathered per knowledge category (basic / advanced) and flows into the project selected for that
// category. When no project is selected, vanilla pushes the points into the overflow category and, failing that, drops
// them. This patch silently selects a project the moment knowledge arrives for an empty slot, so nothing is wasted.

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
	[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.ApplyKnowledge), new[] { typeof(KnowledgeCategoryDef), typeof(float) })]
	public static class Patch_AutoAssignAnomalyResearch
	{
		static void Prefix(ResearchManager __instance, KnowledgeCategoryDef category, float amount)
		{
			if (!Settings.autoAssignAnomalyResearch || !ModsConfig.AnomalyActive || category == null || amount <= 0f) return;
			if (__instance.GetProject(category) != null) return;

			var pick = Pick(category);
			if (pick == null) return;
			__instance.SetCurrentProject(pick);
			Log.Debug("No {0} knowledge project selected, auto-assigned {1}", category.defName, pick.label);
		}

		// A startable project of the category; ones that already hold some knowledge are preferred, random otherwise.
		// Rimedieval and tech-level rules are about regular research, so only the generic hooks apply here.
		public static ResearchProjectDef Pick(KnowledgeCategoryDef category)
		{
			if (Settings.readOnlyMode) return null;
			var candidates = new List<ResearchProjectDef>();
			foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
			{
				if (def.knowledgeCategory != category || !def.CanStartNow || !CompatibilityHooks.PassCustomUnlockRequirements(def)) continue;
				candidates.Add(def);
			}
			if (candidates.Count == 0) return null;

			var manager = Find.ResearchManager;
			var started = candidates.Where(d => manager.GetKnowledge(d) > 0f).ToList();
			return (started.Count > 0 ? started : candidates).RandomElement();
		}
	}
}
