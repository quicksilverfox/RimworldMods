using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;
using Verse.Sound;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for utility machines.
	/// Handles: Doors (Auto/Security), NPD, TVs (3 tiers), VitalsMonitor.
	/// </summary>
	public class CompUtilityAutomation : CompSubcoreAutomationBase
	{
		#region Utility-Specific State

		// Door state
		private bool _lockForNonColony;

		// NPD state
		private Building_NutrientPasteDispenser _cachedDispenser;
		private bool _continuousDispenseMode;
		private int _lastDispenseAttemptTick;
		private bool _advancedFilteringEnabled = true;

		// Vitals monitor state
		private bool _isVitalsMonitor;

		#endregion

		#region Properties

		public bool LockForNonColony
		{
			get => _lockForNonColony;
			set => _lockForNonColony = value;
		}

		public Building_NutrientPasteDispenser Dispenser => _cachedDispenser;

		public bool ContinuousDispenseMode
		{
			get => _continuousDispenseMode;
			set => _continuousDispenseMode = value;
		}

		public int LastDispenseAttemptTick
		{
			get => _lastDispenseAttemptTick;
			set => _lastDispenseAttemptTick = value;
		}

		public bool AdvancedFilteringEnabled
		{
			get => _advancedFilteringEnabled;
			set => _advancedFilteringEnabled = value;
		}

		public bool IsDoor => parent is Building_Door;
		public bool IsDispenser => _cachedDispenser != null;

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			_cachedDispenser = parent as Building_NutrientPasteDispenser;
			_isVitalsMonitor = parent.def.defName == MachineDefNames.VitalsMonitor;
		}

		protected override void DoMachineSpecificTick()
		{
			// NPD continuous dispense
			if (_cachedDispenser != null && _continuousDispenseMode)
			{
				TryContinuousDispense();
			}
		}

		protected override void ExposeDataMachineSpecific()
		{
			Scribe_Values.Look(ref _lockForNonColony, "lockForNonColony", false);
			Scribe_Values.Look(ref _continuousDispenseMode, "continuousDispenseMode", false);
			Scribe_Values.Look(ref _lastDispenseAttemptTick, "lastDispenseAttemptTick", 0);
			Scribe_Values.Look(ref _advancedFilteringEnabled, "advancedFilteringEnabled", true);
		}

		protected override void OnDestroyMachineSpecific(DestroyMode mode, Map previousMap)
		{
			if (IsDispenser)
			{
				UtilityHandler.Cleanup(this);
			}
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (IsDoor)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("SubcoreAutomation_AutomatedSimple".Translate());
				if (_lockForNonColony)
				{
					sb.AppendLine();
					sb.Append("  ");
					sb.Append("SubcoreAutomation_DoorLockdownActive".Translate());
				}
				return sb.ToString();
			}
			if (IsDispenser)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("SubcoreAutomation_HopperRefrigeration".Translate());
				if (_continuousDispenseMode)
				{
					sb.AppendLine();
					sb.Append("SubcoreAutomation_ContinuousDispenseActive".Translate());
				}
				if (_advancedFilteringEnabled && SubcoreAutomationMod.Settings != null && SubcoreAutomationMod.Settings.advancedFilteringEnabled)
				{
					sb.AppendLine();
					sb.Append("SubcoreAutomation_AdvancedFilteringActive".Translate());
				}
				return sb.ToString();
			}
			if (_isVitalsMonitor)
			{
				return "SubcoreAutomation_VitalsMonitorEnhanced".Translate();
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			if (IsDoor)
			{
				yield return new Command_Toggle
				{
					defaultLabel = "SubcoreAutomation_DoorLockdown".Translate(),
					defaultDesc = "SubcoreAutomation_DoorLockdownDesc".Translate(),
					icon = TexCommand.ForbidOn,
					isActive = () => _lockForNonColony,
					toggleAction = delegate { _lockForNonColony = !_lockForNonColony; }
				};
			}
			else if (IsDispenser)
			{
				var dispenseCmd = new Command_Action
				{
					defaultLabel = "SubcoreAutomation_DispenseMeal".Translate(),
					defaultDesc = "SubcoreAutomation_DispenseMealDesc".Translate(),
					icon = ContentFinder<UnityEngine.Texture2D>.Get("Things/Item/Meal/NutrientPaste/NutrientPaste_a", false) ?? TexCommand.DesirePower,
					action = delegate
					{
						if (!TryDispenseMeal())
						{
							SoundDefOf.ClickReject.PlayOneShotOnCamera();
						}
					}
				};

				if (_cachedDispenser != null && !_cachedDispenser.CanDispenseNow)
				{
					dispenseCmd.Disable("SubcoreAutomation_CannotDispense".Translate());
				}
				yield return dispenseCmd;

				yield return new Command_Toggle
				{
					defaultLabel = "SubcoreAutomation_ContinuousDispense".Translate(),
					defaultDesc = "SubcoreAutomation_ContinuousDispenseDesc".Translate(),
					icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/LoadTransporter", false) ?? TexCommand.ForbidOff,
					isActive = () => _continuousDispenseMode,
					toggleAction = delegate { _continuousDispenseMode = !_continuousDispenseMode; }
				};

				if (SubcoreAutomationMod.Settings != null && SubcoreAutomationMod.Settings.advancedFilteringEnabled)
				{
					yield return new Command_Toggle
					{
						defaultLabel = "SubcoreAutomation_AdvancedFiltering".Translate(),
						defaultDesc = "SubcoreAutomation_AdvancedFilteringDesc".Translate(),
						icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Commands/AdvancedFiltering", false) ?? TexCommand.ForbidOff,
						isActive = () => _advancedFilteringEnabled,
						toggleAction = delegate { _advancedFilteringEnabled = !_advancedFilteringEnabled; }
					};
				}
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (IsDoor)
			{
				return UtilityHandler.GetDoorBenefitsDescription();
			}
			if (IsDispenser)
			{
				return UtilityHandler.GetDispenserBenefitsDescription();
			}
			if (_isVitalsMonitor)
			{
				return UtilityHandler.GetVitalsMonitorBenefitsDescription();
			}
			return "";
		}

		#endregion

		#region Methods

		/// <summary>
		/// Attempts to dispense a single meal from the NPD.
		/// </summary>
		public bool TryDispenseMeal()
		{
			if (_cachedDispenser == null || !_cachedDispenser.CanDispenseNow)
				return false;

			Thing meal = _cachedDispenser.TryDispenseFood();
			if (meal == null)
				return false;

			if (!GenPlace.TryPlaceThing(meal, _cachedDispenser.InteractionCell, _cachedDispenser.Map, ThingPlaceMode.Near))
			{
				meal.Destroy();
				return false;
			}
			return true;
		}

		private void TryContinuousDispense()
		{
			// Rate limit: only try every 60 ticks (1 second)
			if (Find.TickManager.TicksGame - _lastDispenseAttemptTick < 60)
				return;

			_lastDispenseAttemptTick = Find.TickManager.TicksGame;

			if (_cachedDispenser == null || !_cachedDispenser.CanDispenseNow)
				return;

			// Check if interaction cell has a meal already
			IntVec3 interactionCell = _cachedDispenser.InteractionCell;
			Map map = _cachedDispenser.Map;
			if (map == null)
				return;

			ThingDef mealDef = ThingDefOf.MealNutrientPaste;
			foreach (Thing thing in interactionCell.GetThingList(map))
			{
				if (thing.def == mealDef)
					return; // Already has a meal, don't dispense
			}

			TryDispenseMeal();
		}

		#endregion
	}
}
