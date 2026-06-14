using System.Collections.Generic;
using System.Text;
using RimWorld;
using SubcoreAutomation.Core;
using SubcoreAutomation.Patches;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for utility automation - Door, Dispenser (NPD), and Vitals Monitor.
	/// </summary>
	public static class UtilityHandler
	{
		#region Dispense State

		private static readonly Dictionary<CompUtilityAutomation, DispenseState> _dispenseStates =
			new Dictionary<CompUtilityAutomation, DispenseState>();

		private class DispenseState
		{
			public int LastAttemptTick;
		}

		private static DispenseState GetOrCreateDispenseState(CompUtilityAutomation comp)
		{
			if (!_dispenseStates.TryGetValue(comp, out var state))
			{
				state = new DispenseState();
				_dispenseStates[comp] = state;
			}
			return state;
		}

		/// <summary>
		/// Cleans up dispense state when the comp is destroyed.
		/// </summary>
		public static void Cleanup(CompUtilityAutomation comp)
		{
			_dispenseStates.Remove(comp);
		}

		#endregion

		#region Door

		/// <summary>
		/// Returns the inspect string for an automated door.
		/// </summary>
		public static string GetDoorInspectString(CompUtilityAutomation comp)
		{
			return new InspectStringBuilder("SubcoreAutomation_AutomatedSimple".Translate())
				.AppendFeatureIf(comp.LockForNonColony, "SubcoreAutomation_DoorLockdownActive".Translate())
				.ToString();
		}

		/// <summary>
		/// Returns the gizmos for an automated door.
		/// </summary>
		public static IEnumerable<Gizmo> GetDoorGizmos(CompUtilityAutomation comp)
		{
			// Lockdown toggle gizmo
			yield return new Command_Toggle
			{
				defaultLabel = "SubcoreAutomation_DoorLockdown".Translate(),
				defaultDesc = "SubcoreAutomation_DoorLockdownDesc".Translate(),
				icon = TexCommand.ForbidOn,
				isActive = () => comp.LockForNonColony,
				toggleAction = delegate
				{
					comp.LockForNonColony = !comp.LockForNonColony;
				}
			};
		}

		/// <summary>
		/// Returns the benefits description for door automation tooltip.
		/// </summary>
		public static string GetDoorBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_DoorBenefits".Translate();
		}

		#endregion

		#region Dispenser (NPD)

		/// <summary>
		/// Attempts to dispense a single meal from the NPD.
		/// </summary>
		/// <returns>True if a meal was successfully dispensed and placed.</returns>
		public static bool TryDispenseMeal(CompUtilityAutomation comp)
		{
			var dispenser = comp.Dispenser;
			if (dispenser == null)
				return false;

			if (!dispenser.CanDispenseNow)
				return false;

			Thing meal = dispenser.TryDispenseFood();
			if (meal == null)
				return false;

			if (!GenPlace.TryPlaceThing(meal, dispenser.InteractionCell, dispenser.Map, ThingPlaceMode.Near))
			{
				// Failed to place - destroy the meal to avoid ghost items
				meal.Destroy();
				return false;
			}

			return true;
		}

		/// <summary>
		/// Continuous dispense logic - dispense meals whenever the interaction cell is empty.
		/// Called from CompTick when continuous dispense mode is enabled.
		/// </summary>
		public static void TryContinuousDispense(CompUtilityAutomation comp)
		{
			var state = GetOrCreateDispenseState(comp);

			// Rate limit: only try every 60 ticks (1 second)
			if (Find.TickManager.TicksGame - state.LastAttemptTick < 60)
				return;

			state.LastAttemptTick = Find.TickManager.TicksGame;

			var dispenser = comp.Dispenser;
			if (dispenser == null || !dispenser.CanDispenseNow)
				return;

			// Check if interaction cell has a meal already
			IntVec3 interactionCell = dispenser.InteractionCell;
			Map map = dispenser.Map;
			if (map == null)
				return;

			// Check for existing meal at interaction cell
			ThingDef mealDef = ThingDefOf.MealNutrientPaste;
			foreach (Thing thing in interactionCell.GetThingList(map))
			{
				if (thing.def == mealDef)
					return; // Already has a meal, don't dispense
			}

			// Try to dispense
			TryDispenseMeal(comp);
		}

		/// <summary>
		/// Returns the inspect string for an automated nutrient paste dispenser.
		/// </summary>
		public static string GetDispenserInspectString(CompUtilityAutomation comp)
		{
			return new InspectStringBuilder("SubcoreAutomation_AutomatedSimple".Translate())
				.AppendFeature("SubcoreAutomation_HopperRefrigeration".Translate())
				.AppendFeatureIf(comp.ContinuousDispenseMode, "SubcoreAutomation_ContinuousDispenseActive".Translate())
				.ToString();
		}

		/// <summary>
		/// Returns the gizmos for an automated nutrient paste dispenser.
		/// </summary>
		public static IEnumerable<Gizmo> GetDispenserGizmos(CompUtilityAutomation comp)
		{
			// Dispense single meal gizmo
			Command_Action dispenseCmd = new Command_Action
			{
				defaultLabel = "SubcoreAutomation_DispenseMeal".Translate(),
				defaultDesc = "SubcoreAutomation_DispenseMealDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("Things/Item/Meal/NutrientPaste/NutrientPaste_a", false) ?? TexCommand.DesirePower,
				action = delegate
				{
					if (!comp.TryDispenseMeal())
					{
						SoundDefOf.ClickReject.PlayOneShotOnCamera();
					}
				}
			};

			if (comp.Dispenser != null && !comp.Dispenser.CanDispenseNow)
			{
				dispenseCmd.Disable("SubcoreAutomation_CannotDispense".Translate());
			}

			yield return dispenseCmd;

			// Continuous dispense toggle
			yield return new Command_Toggle
			{
				defaultLabel = "SubcoreAutomation_ContinuousDispense".Translate(),
				defaultDesc = "SubcoreAutomation_ContinuousDispenseDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", false) ?? TexCommand.ForbidOff,
				isActive = () => comp.ContinuousDispenseMode,
				toggleAction = delegate
				{
					comp.ContinuousDispenseMode = !comp.ContinuousDispenseMode;
				}
			};
		}

		/// <summary>
		/// Returns the benefits description for NPD automation tooltip.
		/// </summary>
		public static string GetDispenserBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_NPDBenefits".Translate();
		}

		#endregion

		#region Vitals Monitor

		/// <summary>
		/// Returns the inspect string for an automated vitals monitor.
		/// </summary>
		public static string GetVitalsMonitorInspectString(CompUtilityAutomation comp)
		{
			return "SubcoreAutomation_VitalsMonitorEnhanced".Translate();
		}

		/// <summary>
		/// Returns the benefits description for vitals monitor automation tooltip.
		/// </summary>
		public static string GetVitalsMonitorBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_VitalsMonitorBenefits".Translate();
		}

		#endregion
	}
}
