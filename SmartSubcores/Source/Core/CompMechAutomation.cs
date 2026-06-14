using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for mech-related machines.
	/// Handles: MechCharger (Basic/Standard), MechBooster, BandNode.
	/// </summary>
	public class CompMechAutomation : CompSubcoreAutomationBase
	{
		#region Mech-Specific State

		private bool _isMechCharger;
		private bool _isMechBooster;
		private bool _isBandNode;

		#endregion

		#region Mech Booster Constants

		private const int BoosterScanInterval = 30;
		private const float FallbackBoosterRange = 9.9f;
		private const int BoostHediffRefreshTicks = 180;

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			string defName = parent.def.defName;
			_isMechCharger = defName == MachineDefNames.BasicRecharger || defName == MachineDefNames.StandardRecharger;
			_isMechBooster = MachineDefNames.IsMechBooster(defName);
			_isBandNode = defName == MachineDefNames.BandNode;
		}

		protected override void DoMachineSpecificTick()
		{
			// Charger / BandNode: handled by Harmony patches
			// Booster: scan radius and apply combat-boost hediff to friendly mechs
			if (_isMechBooster)
			{
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
					return;
				if (!parent.IsHashIntervalTick(BoosterScanInterval))
					return;
				ApplyBoosterBoostToNearbyMechs();
			}
		}

		private void ApplyBoosterBoostToNearbyMechs()
		{
			Map map = parent.Map;
			if (map == null)
				return;

			HediffDef boostDef = SubcoreAutomationDefOf.SubcoreAutomation_MechBoosterBoost;
			if (boostDef == null)
				return;

			Faction myFaction = parent.Faction;

			// Per-booster range: vanilla MechBooster = 9.9, modded variants (e.g. BfG
			// floor booster) may declare a larger radius on CompCauseHediff_AoE.
			var aoe = parent.TryGetComp<CompCauseHediff_AoE>();
			float boosterRange = (aoe != null && aoe.range > 0f) ? aoe.range : FallbackBoosterRange;
			int numCells = GenRadial.NumCellsInRadius(boosterRange);
			for (int i = 0; i < numCells; i++)
			{
				IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
				if (!cell.InBounds(map))
					continue;

				List<Thing> things = cell.GetThingList(map);
				for (int j = 0; j < things.Count; j++)
				{
					if (!(things[j] is Pawn pawn))
						continue;
					if (!pawn.RaceProps.IsMechanoid)
						continue;
					if (pawn.Faction != myFaction)
						continue;

					ApplyOrRefreshBoost(pawn, boostDef);
				}
			}
		}

		private static void ApplyOrRefreshBoost(Pawn pawn, HediffDef def)
		{
			Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
			if (existing == null)
			{
				BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
				Hediff h = HediffMaker.MakeHediff(def, pawn, brain);
				pawn.health.AddHediff(h);
				existing = h;
			}

			HediffComp_Disappears disappearComp = existing.TryGetComp<HediffComp_Disappears>();
			if (disappearComp != null)
				disappearComp.ticksToDisappear = BoostHediffRefreshTicks;
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (_isMechCharger)
			{
				return MechHandler.GetMechChargerInspectString(this);
			}
			if (_isMechBooster)
			{
				return MechHandler.GetMechBoosterInspectString(this);
			}
			if (_isBandNode)
			{
				return MechHandler.GetBandNodeInspectString(this);
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			yield break;
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (_isMechCharger)
			{
				return MechHandler.GetMechChargerBenefitsDescription();
			}
			if (_isMechBooster)
			{
				return MechHandler.GetMechBoosterBenefitsDescription();
			}
			if (_isBandNode)
			{
				return MechHandler.GetBandNodeBenefitsDescription();
			}
			return "";
		}

		protected override void OnSubcoreInstalledRegistrations(bool respawningAfterLoad = false)
		{
			// Skip recompute on respawn (save load) and during gravship placement.
			// The cached Hediff_BandNode count is persisted via Scribe and already
			// includes our +1 automation bonus per node (added by
			// Patch_HediffBandNode_RecacheBandNodes). Recomputing here would query
			// CompPowerTrader.PowerOn before the power net is rebuilt, drop the
			// cached count, and cause mech disconnections.
			if (_isBandNode && !respawningAfterLoad && !GravshipPlacementUtility.placingGravship)
				RefreshBandNodeBandwidth();
		}

		protected override void OnSubcoreRemovedRegistrations()
		{
			// Removal is only invoked from live actions (CompleteRemoval / RemoveSubcore),
			// so it's safe to recompute unconditionally.
			if (_isBandNode)
				RefreshBandNodeBandwidth();
		}

		/// <summary>
		/// Force the tuned mechanitor's Hediff_BandNode to recache so the +1 automation
		/// bonus (added by Patch_HediffBandNode_RecacheBandNodes) is reflected immediately
		/// in MechBandwidth — without this, the bonus would only show up on the next
		/// 60-tick interval. Recache itself fires Notify_BandwidthChanged when the count
		/// changes, so no separate notify is needed.
		/// </summary>
		private void RefreshBandNodeBandwidth()
		{
			var bandNode = parent.TryGetComp<CompBandNode>();
			Pawn pawn = bandNode?.tunedTo;
			if (pawn?.health?.hediffSet == null)
				return;
			foreach (Hediff h in pawn.health.hediffSet.hediffs)
			{
				if (h is Hediff_BandNode bandHediff)
				{
					bandHediff.RecacheBandNodes();
					break;
				}
			}
		}

		#endregion
	}
}
