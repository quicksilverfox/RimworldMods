using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Compat
{
	/// <summary>
	/// Compatibility patches for WVC - Work Modes mod.
	/// 
	/// WVC replaces vanilla mech job givers with its own work mode system,
	/// which bypasses our patches on JobGiver_GetEnergy_Charger. This compatibility
	/// layer uses an alternative approach to direct damaged mechs to automated chargers.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class WVCWorkModesPatches
	{
		public static bool WVCLoaded { get; private set; }

		// Check interval for damaged mechs (every 120 ticks = ~2 seconds)
		private const int CheckInterval = 120;

		// Track mechs we've recently sent to repair to avoid spam
		private static readonly Dictionary<int, int> _lastRepairJobTick = new Dictionary<int, int>();
		private const int RepairJobCooldown = 2500; // ~42 seconds

		static WVCWorkModesPatches()
		{
			WVCLoaded = ModsConfig.IsActive("wvc.sergkart.biotech.MoreMechanoidsWorkModes");

			if (!WVCLoaded)
				return;

			if (!ModsConfig.BiotechActive)
				return;

			if (SubcoreAutomationMod.Settings != null && !SubcoreAutomationMod.Settings.mechChargerPatchesEnabled)
				return;

			try
			{
				var harmony = new Harmony("SubcoreAutomation.WVCWorkModesCompat");

				// Patch Pawn.TickRare to periodically check if damaged mechs should seek repair
				// This works independently of job givers
				var tickRare = AccessTools.Method(typeof(Pawn), "TickRare");
				if (tickRare != null)
				{
					harmony.Patch(tickRare,
						postfix: new HarmonyMethod(typeof(WVCWorkModesPatches), nameof(Pawn_TickRare_Postfix))
					);
					Log.Message("[SubcoreAutomation] WVC - Work Modes compatibility patches applied");
				}
				else
					Log.Error("[SubcoreAutomation] WVC Work Modes patches BROKEN: Pawn.TickRare not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply WVC compatibility patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Postfix for Pawn.TickRare - checks if a damaged colony mech should seek an automated charger.
		/// </summary>
		public static void Pawn_TickRare_Postfix(Pawn __instance)
		{
			try
			{
				// Only check periodically to reduce performance impact
				if (!__instance.IsHashIntervalTick(CheckInterval))
					return;

				// Only apply to colony mechs
			if (!__instance.IsColonyMech || !__instance.Spawned || __instance.Downed || __instance.Dead)
				return;

			// Don't interrupt drafted mechs - only idle ones should seek repairs
			if (__instance.Drafted)
				return;

			// Check if mech needs repair
				if (!MechRepairUtility.CanRepair(__instance))
					return;

				// Check if mech is already at or going to a charger
				if (IsAlreadyChargingOrGoingToCharger(__instance))
					return;

				// Check cooldown to avoid spamming repair jobs
				int currentTick = Find.TickManager.TicksGame;
				if (_lastRepairJobTick.TryGetValue(__instance.thingIDNumber, out int lastTick))
				{
					if (currentTick - lastTick < RepairJobCooldown)
						return;
				}

				// Try to find an automated charger for repair
				var charger = GetClosestAutomatedChargerForRepair(__instance);
				if (charger == null)
					return;

				// Give the mech a job to go charge/repair
				Job job = JobMaker.MakeJob(JobDefOf.MechCharge, charger);
				job.overrideFacing = Rot4.South;

				// Try to start the job
				if (__instance.jobs.TryTakeOrderedJob(job, JobTag.Misc))
				{
					_lastRepairJobTick[__instance.thingIDNumber] = currentTick;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in WVC compat Pawn_TickRare_Postfix: {ex.Message}", 93827550);
			}
		}

		/// <summary>
		/// Check if the mech is already at a charger or has a job to go to one.
		/// </summary>
		private static bool IsAlreadyChargingOrGoingToCharger(Pawn mech)
		{
			// Check if currently at a charger
			if (mech.needs?.energy?.currentCharger != null)
				return true;

			// Check current job
			var curJob = mech.jobs?.curJob;
			if (curJob != null)
			{
				// Check if already doing a charge job
				if (curJob.def == JobDefOf.MechCharge)
					return true;

				// Check if doing self-shutdown (which is also a form of "resting")
				if (curJob.def == JobDefOf.SelfShutdown)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Find the closest automated charger that can repair this mech.
		/// </summary>
		private static Building_MechCharger GetClosestAutomatedChargerForRepair(Pawn mech)
		{
			if (!mech.Spawned)
				return null;

			return (Building_MechCharger)GenClosest.ClosestThingReachable(
				mech.Position,
				mech.Map,
				ThingRequest.ForGroup(ThingRequestGroup.MechCharger),
				PathEndMode.InteractionCell,
				TraverseParms.For(mech, Danger.Some),
				9999f,
				delegate (Thing t)
				{
					Building_MechCharger charger = (Building_MechCharger)t;

					// Must be reachable
					if (!mech.CanReach(t, PathEndMode.InteractionCell, Danger.Some))
						return false;

					// Must be reservable
					if (!mech.CanReserve(t, 1, -1, null, false))
						return false;

					// Must not be forbidden
					if (t.IsForbidden(mech))
						return false;

					// Must be able to charge this mech type
					if (!charger.CanPawnChargeCurrently(mech))
						return false;

					// Must be automated (has subcore installed and enabled)
					var automationComp = charger.TryGetComp<CompSubcoreAutomationBase>();
					if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
						return false;

					return true;
				}
			);
		}

	}
}
