using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Replaces Hediff_BandNode.RecacheBandNodes so the cached count already
	/// includes the +1 automation bonus per band node that has a subcore
	/// installed and automation enabled — i.e. each automated band node provides
	/// 2 MechBandwidth (vanilla 1 + bonus 1) instead of 1.
	///
	/// This intentionally folds the bonus into vanilla's persisted
	/// cachedTunedBandNodesCount (saved via Scribe) rather than computing it
	/// live via a StatPart. The StatPart approach deflated bandwidth right
	/// after save load and during gravship transit, because StatPart queries
	/// CompPowerTrader.PowerOn at the moment MechanitorTracker.Notify_BandwidthChanged
	/// fires from PostSpawnSetup — before the power net is rebuilt — returning 0
	/// bonus and disconnecting mechs that were previously in bandwidth.
	/// Using the cached count means bandwidth survives those windows unchanged
	/// because the saved value already accounts for the bonus.
	/// </summary>
	[HarmonyPatch(typeof(Hediff_BandNode), nameof(Hediff_BandNode.RecacheBandNodes))]
	public static class Patch_HediffBandNode_RecacheBandNodes
	{
		private static readonly FieldInfo CachedCountField =
			AccessTools.Field(typeof(Hediff_BandNode), "cachedTunedBandNodesCount");
		private static readonly FieldInfo CurStageField =
			AccessTools.Field(typeof(Hediff_BandNode), "curStage");

		public static bool Prepare() => ModsConfig.BiotechActive;

		public static bool Prefix(Hediff_BandNode __instance)
		{
			if (CachedCountField == null)
				return true; // reflection broke — fall back to vanilla

			int oldCount = (int)CachedCountField.GetValue(__instance);
			int newCount = 0;
			Pawn pawn = __instance.pawn;
			var maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				foreach (Building b in maps[i].listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.BandNode))
				{
					var cbn = b.TryGetComp<CompBandNode>();
					if (cbn?.tunedTo != pawn)
						continue;
					var power = b.TryGetComp<CompPowerTrader>();
					if (power == null || !power.PowerOn)
						continue;

					newCount++; // vanilla contribution

					var auto = b.TryGetComp<CompSubcoreAutomationBase>();
					if (auto != null && auto.SubcoreInstalled && auto.IsAutomationEnabled)
						newCount++; // automation bonus
				}
			}

			CachedCountField.SetValue(__instance, newCount);
			if (oldCount != newCount)
			{
				CurStageField?.SetValue(__instance, null);
				pawn.mechanitor?.Notify_BandwidthChanged();
			}
			return false; // skip vanilla — we did the full recompute
		}
	}
}
