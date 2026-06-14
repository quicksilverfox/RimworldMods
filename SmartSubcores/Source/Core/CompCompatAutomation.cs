using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Compat;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for mod compatibility machines.
	/// Handles: VNPE_NutrientPasteGrinder, VNPE_NutrientPasteFeeder.
	/// </summary>
	public class CompCompatAutomation : CompSubcoreAutomationBase
	{
		#region Compat-Specific State

		private bool _isVNPEGrinder;
		private bool _isVNPEFeeder;
		private bool _advancedFilteringEnabled = true;

		#endregion

		#region Properties

		public bool AdvancedFilteringEnabled
		{
			get => _advancedFilteringEnabled;
			set => _advancedFilteringEnabled = value;
		}

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			string defName = parent.def.defName;
			_isVNPEGrinder = defName == MachineDefNames.VNPE_NutrientPasteGrinder;
			_isVNPEFeeder = defName == MachineDefNames.VNPE_NutrientPasteFeeder;
		}

		protected override void DoMachineSpecificTick()
		{
			if (_isVNPEGrinder)
			{
				VNPEPatches.HandleGrinderAutomation(parent, this, Props.automatedSpeedFactor);
				return;
			}
			if (_isVNPEFeeder)
			{
				VNPEPatches.HandleFeederAutomation(parent, this, Props.automatedSpeedFactor);
			}
		}

		protected override void OnDestroyMachineSpecific(DestroyMode mode, Map previousMap)
		{
			if (_isVNPEGrinder)
				VNPEPatches.UnregisterGrinder(parent);
			else if (_isVNPEFeeder)
				VNPEPatches.UnregisterFeeder(parent);
		}

		protected override void ExposeDataMachineSpecific()
		{
			Scribe_Values.Look(ref _advancedFilteringEnabled, "advancedFilteringEnabled", true);
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (_isVNPEGrinder)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("SubcoreAutomation_VNPEGrinderEnhanced".Translate());
				if (_advancedFilteringEnabled && SubcoreAutomationMod.Settings != null && SubcoreAutomationMod.Settings.advancedFilteringEnabled)
				{
					sb.AppendLine();
					sb.Append("  ");
					sb.Append("SubcoreAutomation_AdvancedFilteringActive".Translate());
				}
				return sb.ToString();
			}
			if (_isVNPEFeeder)
			{
				return "SubcoreAutomation_VNPEFeederEnhanced".Translate();
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			if (_isVNPEGrinder && SubcoreAutomationMod.Settings != null && SubcoreAutomationMod.Settings.advancedFilteringEnabled)
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

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (_isVNPEGrinder)
			{
				return "\n\n" + "SubcoreAutomation_VNPEGrinderBenefits".Translate();
			}
			if (_isVNPEFeeder)
			{
				return "\n\n" + "SubcoreAutomation_VNPEFeederBenefits".Translate();
			}
			return "\n\n" + "SubcoreAutomation_FallbackBenefits".Translate();
		}

		#endregion
	}
}
