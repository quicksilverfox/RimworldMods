using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Mech Charger automation.
	/// - Faster recharge: 2x charge rate (1 day instead of 2 days for full charge)
	/// - Auto-repair: Repairs damage during recharging
	/// - Downed/damaged mech support: Keeps mechs at charger until fully repaired
	/// </summary>
	[StaticConstructorOnStartup]
	public static class MechChargerPatches
	{
		// Vanilla charge rate is 0.00083333335f per tick (50% per day, 2 days for full charge)
		// We double it to 0.00166666f per tick (100% per day, 1 day for full charge)
		private const float VanillaChargePerTick = 0.00083333335f;
		private const float AutomatedChargeMultiplier = 2f;

		// Repair every 60 ticks (1 second in-game)
		private const int RepairIntervalTicks = 60;
		private const int RepairDelta = 1;

		// Check for downed mechs at interaction cell every 120 ticks
		private const int DownedMechCheckInterval = 120;
		
		// Clean up stale entries every 2500 ticks (~42 seconds)
		private const int CleanupInterval = 2500;

		// Cached reflection - initialized once at startup
		private static readonly FieldInfo _currentlyChargingMechField;
		private static readonly FieldInfo _wireExtensionTicksField;
		private static readonly FieldInfo _wasteProducedField;
		private static readonly PropertyInfo _containerProperty;

		// Track mechs we're managing (charger -> mech). Includes downed, damaged, or repairing mechs.
		private static readonly Dictionary<Building_MechCharger, Pawn> _managedMechs = new Dictionary<Building_MechCharger, Pawn>();

		static MechChargerPatches()
		{
			try
			{
				// Check if Biotech is active (Mech Chargers are from Biotech)
				if (!ModsConfig.BiotechActive)
				{
					// Biotech not active, skipping mech charger patches
					return;
				}

				// Check if feature is enabled in settings
				if (SubcoreAutomationMod.Settings != null && !SubcoreAutomationMod.Settings.mechChargerPatchesEnabled)
				{
					// Mech charger patches disabled in settings
					return;
				}

				// Cache ALL reflection upfront for performance
				_currentlyChargingMechField = AccessTools.Field(typeof(Building_MechCharger), "currentlyChargingMech");
				_wireExtensionTicksField = AccessTools.Field(typeof(Building_MechCharger), "wireExtensionTicks");
				_wasteProducedField = AccessTools.Field(typeof(Building_MechCharger), "wasteProduced");
				_containerProperty = AccessTools.Property(typeof(Building_MechCharger), "Container");

				if (_currentlyChargingMechField == null)
				{
					Log.Error("[SubcoreAutomation] Mech Charger patches BROKEN: currentlyChargingMech field not found!");
					return;
				}

				var harmony = new Harmony("SubcoreAutomation.MechChargerPatches");

				// Patch Building_MechCharger.Tick
				var tick = AccessTools.Method(typeof(Building_MechCharger), "Tick");
				if (tick != null)
				{
					harmony.Patch(tick,
						prefix: new HarmonyMethod(typeof(MechChargerPatches), nameof(Tick_Prefix)),
						postfix: new HarmonyMethod(typeof(MechChargerPatches), nameof(Tick_Postfix)));
				}
				else
					Log.Error("[SubcoreAutomation] Mech Charger patches BROKEN: Tick method not found!");

				// Patch StopCharging
				var stopCharging = AccessTools.Method(typeof(Building_MechCharger), "StopCharging");
				if (stopCharging != null)
					harmony.Patch(stopCharging, prefix: new HarmonyMethod(typeof(MechChargerPatches), nameof(StopCharging_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Mech Charger patches BROKEN: StopCharging method not found!");

				// Patch StartCharging
				var startCharging = AccessTools.Method(typeof(Building_MechCharger), "StartCharging");
				if (startCharging != null)
					harmony.Patch(startCharging, prefix: new HarmonyMethod(typeof(MechChargerPatches), nameof(StartCharging_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Mech Charger patches BROKEN: StartCharging method not found!");

				// Patch ShouldAutoRecharge
				var shouldAutoRecharge = AccessTools.Method(typeof(JobGiver_GetEnergy), "ShouldAutoRecharge");
				if (shouldAutoRecharge != null)
					harmony.Patch(shouldAutoRecharge, postfix: new HarmonyMethod(typeof(MechChargerPatches), nameof(ShouldAutoRecharge_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Mech Charger patches BROKEN: ShouldAutoRecharge method not found!");

				// Patch TryGiveJob
				var tryGiveJob = AccessTools.Method(typeof(JobGiver_GetEnergy_Charger), "TryGiveJob");
				if (tryGiveJob != null)
					harmony.Patch(tryGiveJob, postfix: new HarmonyMethod(typeof(MechChargerPatches), nameof(TryGiveJob_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Mech Charger patches BROKEN: TryGiveJob method not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply mech charger patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if a mech reference is still valid (not destroyed, not null, still exists).
		/// </summary>
		private static bool IsMechValid(Pawn mech)
		{
			return mech != null && !mech.Destroyed && mech.Spawned;
		}

		/// <summary>
		/// Check if a charger reference is still valid.
		/// </summary>
		private static bool IsChargerValid(Building_MechCharger charger)
		{
			return charger != null && !charger.Destroyed && charger.Spawned;
		}

		/// <summary>
		/// Periodic cleanup of stale dictionary entries (destroyed chargers/mechs).
		/// </summary>
		private static void CleanupStaleEntries()
		{
			if (_managedMechs.Count == 0)
				return;

			var toRemove = new List<Building_MechCharger>();
			foreach (var kvp in _managedMechs)
			{
				if (!IsChargerValid(kvp.Key) || !IsMechValid(kvp.Value))
				{
					toRemove.Add(kvp.Key);
				}
			}

			foreach (var charger in toRemove)
			{
				_managedMechs.Remove(charger);
			}
		}

		/// <summary>
		/// Check if mech charger patches are globally enabled in settings.
		/// Cached for performance - checked once per tick cycle.
		/// </summary>
		private static bool ArePatchesEnabled()
		{
			return SubcoreAutomationMod.Settings?.mechChargerPatchesEnabled ?? true;
		}

		/// <summary>
		/// Prefix for Building_MechCharger.Tick - handles downed/damaged mechs on automated chargers.
		/// Returns false to skip vanilla tick when we're managing the mech.
		/// </summary>
		public static bool Tick_Prefix(Building_MechCharger __instance, out bool __state)
		{
			__state = false; // Track whether we're managing this tick (used by postfix)
			
			// Early exit if patches are globally disabled in settings
			if (!ArePatchesEnabled())
				return true;
			
			try
			{
				// Periodic cleanup of stale entries
				if (__instance.IsHashIntervalTick(CleanupInterval))
				{
					CleanupStaleEntries();
				}

				// Check if this charger is automated
				var automationComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
					return true; // Let vanilla handle non-automated chargers

				Pawn currentMech = _currentlyChargingMechField?.GetValue(__instance) as Pawn;

				// Check if this mech is already being managed by us
				bool isManagedMech = _managedMechs.TryGetValue(__instance, out Pawn managedMech);

				// Validate managed mech is still valid
				if (isManagedMech && !IsMechValid(managedMech))
				{
					_managedMechs.Remove(__instance);
					isManagedMech = false;
					managedMech = null;
				}

				// If there's a mech that's downed, managed, or needs repair - we handle it
				if (currentMech != null && IsMechValid(currentMech) && 
				    (currentMech.Downed || isManagedMech || MechRepairUtility.CanRepair(currentMech)))
				{
					// Track it if newly needing management
					if (!isManagedMech)
					{
						_managedMechs[__instance] = currentMech;
					}

					// Do our own tick logic
					DoManagedMechCharging(__instance, currentMech, automationComp);

					// Tell postfix we handled this
					__state = true;
					
					// Skip vanilla tick entirely - it would kick out the mech
					return false;
				}

				// Handle case where charger lost reference but we're still managing the mech
				if (currentMech == null && isManagedMech && IsMechValid(managedMech))
				{
					if (managedMech.Position == __instance.InteractionCell && managedMech.Map == __instance.Map)
					{
						// Mech is still there but charger lost reference - restore it
						_currentlyChargingMechField?.SetValue(__instance, managedMech);
						if (managedMech.needs?.energy != null)
						{
							managedMech.needs.energy.currentCharger = __instance;
						}
						DoManagedMechCharging(__instance, managedMech, automationComp);
						__state = true;
						return false;
					}
					else
					{
						// Mech left or changed maps - clean up
						CleanupManagedMech(__instance, managedMech);
					}
				}

				// Check if we should pick up a downed mech at the interaction cell
				if (currentMech == null && __instance.IsHashIntervalTick(DownedMechCheckInterval))
				{
					TryPickUpDownedMechAtInteractionCell(__instance, automationComp);
					
					// If we picked up a mech, handle it now and skip vanilla
					if (_managedMechs.TryGetValue(__instance, out Pawn newMech) && IsMechValid(newMech))
					{
						DoManagedMechCharging(__instance, newMech, automationComp);
						__state = true;
						return false;
					}
				}

				return true; // Let vanilla handle normal mechs
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in Tick_Prefix: {ex.Message}\n{ex.StackTrace}", 93827494);
				return true;
			}
		}

		/// <summary>
		/// Handle charging for a managed mech (downed, damaged, or repairing).
		/// Replaces vanilla tick logic for these mechs.
		/// </summary>
		private static void DoManagedMechCharging(Building_MechCharger charger, Pawn mech, CompSubcoreAutomationBase automationComp)
		{
			// Check power
			CompPowerTrader power = charger.TryGetComp<CompPowerTrader>();
			if (power == null || !power.PowerOn)
			{
				CleanupManagedMech(charger, mech);
				return;
			}

			// FIRST: If mech is not downed but still needs repair, give it a Wait job to stay in place
			if (!mech.Downed && MechRepairUtility.CanRepair(mech))
			{
				EnsureMechStaysInPlace(mech);
			}

			// Check if mech is still at interaction cell and on same map
			if (!mech.Spawned || mech.Position != charger.InteractionCell || mech.Map != charger.Map)
			{
				CleanupManagedMech(charger, mech);
				return;
			}

			// Check if mech is fully repaired (and not downed)
			if (!mech.Downed && !MechRepairUtility.CanRepair(mech))
			{
				// Fully repaired - release mech to resume normal behavior
				CleanupManagedMech(charger, mech);
				return;
			}

			// Check waste - can't charge when full
			if (charger.IsFullOfWaste)
			{
				return;
			}

			// Do charging (2x rate for automated)
			if (mech.needs?.energy != null)
			{
				mech.needs.energy.CurLevel += VanillaChargePerTick * AutomatedChargeMultiplier;
			}

			// Do repair periodically
			if (Find.TickManager.TicksGame % RepairIntervalTicks == 0)
			{
				if (MechRepairUtility.CanRepair(mech))
				{
					MechRepairUtility.RepairTick(mech, RepairDelta);
				}
			}

			// Handle waste production using cached reflection
			HandleWasteProduction(charger, mech);

			// Update wire extension animation
			UpdateWireAnimation(charger);
		}

		/// <summary>
		/// Ensure mech stays in place with a Wait job.
		/// </summary>
		private static void EnsureMechStaysInPlace(Pawn mech)
		{
			if (mech.jobs == null)
				return;

			var curJob = mech.jobs.curJob;
			
			// Only give new Wait job if mech has no job or has a different job type
			// Don't check expiryInterval - just check if it's our Wait job type
			if (curJob == null || curJob.def != JobDefOf.Wait_MaintainPosture)
			{
				try
				{
					Job waitJob = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, 10000);
					mech.jobs.StartJob(waitJob, JobCondition.InterruptForced, null, false, true);
				}
				catch (Exception ex)
				{
					// Job assignment can fail in edge cases - log but don't crash
					Log.WarningOnce($"[SubcoreAutomation] Failed to assign Wait job to mech: {ex.Message}", 93827496);
				}
			}
		}

		/// <summary>
		/// Handle waste production during charging.
		/// </summary>
		private static void HandleWasteProduction(Building_MechCharger charger, Pawn mech)
		{
			if (_wasteProducedField == null || mech.needs?.energy == null)
				return;

			try
			{
				float wasteProduced = (float)_wasteProducedField.GetValue(charger);
				float wastePerTick = mech.GetStatValue(StatDefOf.WastepacksPerRecharge) * 
					(VanillaChargePerTick * AutomatedChargeMultiplier / mech.needs.energy.MaxLevel);
				wasteProduced += wastePerTick;

				if (_containerProperty != null)
				{
					var container = _containerProperty.GetValue(charger) as CompThingContainer;
					int stackLimit = container?.Props?.stackLimit ?? 5;
					wasteProduced = UnityEngine.Mathf.Clamp(wasteProduced, 0f, stackLimit);

					if (wasteProduced >= stackLimit && container != null && !container.innerContainer.Any)
					{
						wasteProduced = 0f;
						charger.GenerateWastePack();
					}
				}

				_wasteProducedField.SetValue(charger, wasteProduced);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error handling waste production: {ex.Message}", 93827497);
			}
		}

		/// <summary>
		/// Update wire extension animation.
		/// </summary>
		private static void UpdateWireAnimation(Building_MechCharger charger)
		{
			if (_wireExtensionTicksField == null)
				return;

			try
			{
				int wireExtension = (int)_wireExtensionTicksField.GetValue(charger);
				if (wireExtension < 70)
				{
					_wireExtensionTicksField.SetValue(charger, wireExtension + 1);
				}
			}
			catch
			{
				// Animation failure is not critical - ignore
			}
		}

		/// <summary>
		/// Try to pick up a downed mech that's at the charger's interaction cell.
		/// </summary>
		private static void TryPickUpDownedMechAtInteractionCell(Building_MechCharger charger, CompSubcoreAutomationBase automationComp)
		{
			if (charger.Map == null)
				return;

			// Check power
			CompPowerTrader power = charger.TryGetComp<CompPowerTrader>();
			if (power == null || !power.PowerOn)
				return;

			if (charger.IsFullOfWaste)
				return;

			IntVec3 interactionCell = charger.InteractionCell;

			// Look for a downed colony mech at the interaction cell
			foreach (Thing thing in interactionCell.GetThingList(charger.Map))
			{
				if (thing is Pawn mech && mech.Downed && mech.IsColonyMech && IsMechValid(mech))
				{
					// Check compatibility
					if (!charger.IsCompatibleWithCharger(mech.kindDef))
						continue;

					// Check if repairable
					if (!MechRepairUtility.CanRepair(mech))
						continue;

					// Track the mech and set up minimal charger state
					_managedMechs[charger] = mech;
					_currentlyChargingMechField?.SetValue(charger, mech);
					
					if (mech.needs?.energy != null)
					{
						mech.needs.energy.currentCharger = charger;
					}
					return;
				}
			}
		}

		/// <summary>
		/// Clean up a managed mech that's done charging/repairing.
		/// </summary>
		private static void CleanupManagedMech(Building_MechCharger charger, Pawn mech)
		{
			// Clear our tracking
			_managedMechs.Remove(charger);

			// Clear the charger's reference (if charger is still valid)
			if (IsChargerValid(charger))
			{
				_currentlyChargingMechField?.SetValue(charger, null);
				
				// Reset wire animation
				if (_wireExtensionTicksField != null)
				{
					try { _wireExtensionTicksField.SetValue(charger, 0); } catch { }
				}
			}

			// Clear the mech's charger reference and end Wait job (if mech is still valid)
			if (IsMechValid(mech))
			{
				if (mech.needs?.energy != null)
				{
					mech.needs.energy.currentCharger = null;
				}

				// End any Wait job we gave the mech so it can resume normal behavior
				if (mech.jobs != null && !mech.Downed)
				{
					var curJob = mech.jobs.curJob;
					if (curJob != null && (curJob.def == JobDefOf.Wait || curJob.def == JobDefOf.Wait_MaintainPosture))
					{
						try
						{
							mech.jobs.EndCurrentJob(JobCondition.Succeeded);
						}
						catch (Exception ex)
						{
							Log.WarningOnce($"[SubcoreAutomation] Failed to end Wait job: {ex.Message}", 93827498);
						}
					}
				}
			}
		}

		/// <summary>
		/// Prefix for StopCharging - prevent vanilla from stopping charging for managed mechs.
		/// NOTE: We don't use a postfix because Harmony postfixes run even when prefix returns false,
		/// which would incorrectly remove our tracking.
		/// </summary>
		public static bool StopCharging_Prefix(Building_MechCharger __instance)
		{
			// Early exit if patches are globally disabled
			if (!ArePatchesEnabled())
				return true;
			
			try
			{
				// Check if this is an automated charger
				var automationComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
					return true; // Let vanilla handle non-automated chargers

				// If we're already managing a mech at this charger, don't let vanilla stop charging
				if (_managedMechs.ContainsKey(__instance))
				{
					return false;
				}

				// Check if the mech being stopped should be managed
				Pawn currentMech = _currentlyChargingMechField?.GetValue(__instance) as Pawn;
				if (currentMech != null && IsMechValid(currentMech) && 
				    currentMech.Position == __instance.InteractionCell && currentMech.Map == __instance.Map)
				{
					// If mech is downed or needs repair, track it and skip vanilla StopCharging
					if (currentMech.Downed || MechRepairUtility.CanRepair(currentMech))
					{
						_managedMechs[__instance] = currentMech;
						return false;
					}
				}

				return true; // Let vanilla handle normally
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in StopCharging_Prefix: {ex.Message}", 93827495);
				return true;
			}
		}

		/// <summary>
		/// Prefix for StartCharging - prevent duplicate charging when we're already managing a mech.
		/// This happens when a colonist tries to "deliver" a mech that's already at the charger.
		/// </summary>
		public static bool StartCharging_Prefix(Building_MechCharger __instance, Pawn mech)
		{
			// Early exit if patches are globally disabled
			if (!ArePatchesEnabled())
				return true;
			
			try
			{
				// If we're already managing a mech at this charger, skip vanilla StartCharging
				if (_managedMechs.TryGetValue(__instance, out Pawn managedMech))
				{
					// If it's the same mech, just skip (already being managed)
					if (managedMech == mech)
					{
						return false;
					}
					
					// Different mech trying to use an occupied charger - let vanilla handle/error
					return true;
				}

				// Check if charger already has a mech set (we might be managing it even if not in dictionary yet)
				Pawn currentMech = _currentlyChargingMechField?.GetValue(__instance) as Pawn;
				if (currentMech != null && currentMech == mech)
				{
					// Same mech already set as charging - skip duplicate StartCharging
					return false;
				}

				return true; // Let vanilla handle normally
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in StartCharging_Prefix: {ex.Message}", 93827499);
				return true;
			}
		}

		/// <summary>
		/// Postfix for Building_MechCharger.Tick - adds faster charging and auto-repair for normal (non-managed) mechs.
		/// Only runs when prefix returned true (didn't handle the mech itself).
		/// </summary>
		public static void Tick_Postfix(Building_MechCharger __instance, bool __state)
		{
			try
			{
				// Skip if patches globally disabled
				if (!ArePatchesEnabled())
					return;
				
				// Skip if prefix already handled this tick (managed mech)
				if (__state)
					return;

				// Check if automated
				var automationComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
					return;

				// Check power
				CompPowerTrader power = __instance.TryGetComp<CompPowerTrader>();
				if (power == null || !power.PowerOn)
					return;

				// Get the currently charging mech
				Pawn chargingMech = _currentlyChargingMechField?.GetValue(__instance) as Pawn;
				if (chargingMech?.needs?.energy == null)
					return;

				// Skip if this mech is managed by us (shouldn't happen if __state is correct, but defensive)
				if (_managedMechs.ContainsKey(__instance))
					return;

				// Skip if mech is downed (shouldn't be charging normally)
				if (chargingMech.Downed)
					return;

				// Faster charging: Add extra charge (vanilla already added 1x, we add 1x more for 2x total)
				chargingMech.needs.energy.CurLevel += VanillaChargePerTick * (AutomatedChargeMultiplier - 1f);

				// Auto-repair: Repair mech damage during charging
				if (Find.TickManager.TicksGame % RepairIntervalTicks == 0)
				{
					if (MechRepairUtility.CanRepair(chargingMech))
					{
						MechRepairUtility.RepairTick(chargingMech, RepairDelta);
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in mech charger Tick_Postfix: {ex.Message}", 93827493);
			}
		}

		#region Damaged Mech Repair-Seeking

		/// <summary>
		/// Postfix for JobGiver_GetEnergy.ShouldAutoRecharge - allows damaged mechs to seek 
		/// automated chargers for repair even when fully charged.
		/// </summary>
		public static void ShouldAutoRecharge_Postfix(JobGiver_GetEnergy __instance, Pawn pawn, ref bool __result)
		{
			// Only modify if the original returned false (mech doesn't need energy)
			if (__result)
				return;

			// Only apply to JobGiver_GetEnergy_Charger (not self-shutdown or other variants)
			if (!(__instance is JobGiver_GetEnergy_Charger))
				return;

			// Early exit if patches disabled
			if (!ArePatchesEnabled())
				return;

			try
			{
				// Check if mech needs repair
				if (!MechRepairUtility.CanRepair(pawn))
					return;

				// Check if there's an automated charger available for this mech
				if (HasAutomatedChargerForRepair(pawn))
				{
					__result = true;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in ShouldAutoRecharge_Postfix: {ex.Message}", 93827500);
			}
		}

		/// <summary>
		/// Postfix for JobGiver_GetEnergy_Charger.TryGiveJob - ensures damaged mechs find
		/// automated chargers specifically when seeking repair at full energy.
		/// </summary>
		public static void TryGiveJob_Postfix(Pawn pawn, ref Job __result)
		{
			// If vanilla already gave a job, don't interfere
			if (__result != null)
				return;

			// Early exit if patches disabled
			if (!ArePatchesEnabled())
				return;

			try
			{
				// Check if mech needs repair (and presumably has full energy since TryGiveJob returned null)
				if (!MechRepairUtility.CanRepair(pawn))
					return;

				// Find an automated charger for repair
				Building_MechCharger charger = GetClosestAutomatedChargerForRepair(pawn);
				if (charger != null)
				{
					__result = JobMaker.MakeJob(JobDefOf.MechCharge, charger);
					__result.overrideFacing = Rot4.South;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in TryGiveJob_Postfix: {ex.Message}", 93827501);
			}
		}

		/// <summary>
		/// Check if there's an automated charger available for a damaged mech to repair at.
		/// </summary>
		private static bool HasAutomatedChargerForRepair(Pawn mech)
		{
			return GetClosestAutomatedChargerForRepair(mech) != null;
		}

		/// <summary>
		/// Find the closest automated charger that can repair this mech.
		/// Only considers chargers with automation enabled.
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
				delegate(Thing t)
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

		#endregion

		#region Public API

		/// <summary>
		/// Check if a charger has a managed mech (downed, damaged, or repairing).
		/// </summary>
		public static bool HasManagedMech(Building_MechCharger charger)
		{
			return charger != null && _managedMechs.ContainsKey(charger);
		}

		/// <summary>
		/// Get the managed mech at a charger, or null if none.
		/// </summary>
		public static Pawn GetManagedMech(Building_MechCharger charger)
		{
			if (charger != null && _managedMechs.TryGetValue(charger, out Pawn mech))
				return mech;
			return null;
		}

		#endregion
	}
}
