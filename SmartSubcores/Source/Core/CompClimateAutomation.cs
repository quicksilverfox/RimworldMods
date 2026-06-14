using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for climate control machines.
	/// Handles: Heater, Cooler.
	/// </summary>
	public class CompClimateAutomation : CompSubcoreAutomationBase
	{
		#region Climate-Specific State

		private bool _isHeater;
		private bool _isCooler;

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			// Detect by CompTempControl polarity so mod-added coolers/heaters
			// (anything with a temp control comp) are recognised, not just vanilla defNames.
			var tempControl = parent.TryGetComp<CompTempControl>();
			if (tempControl?.Props != null)
			{
				_isCooler = tempControl.Props.energyPerSecond < 0f;
				_isHeater = tempControl.Props.energyPerSecond > 0f;
			}
			else
			{
				string defName = parent.def.defName;
				_isHeater = defName == MachineDefNames.Heater;
				_isCooler = defName == MachineDefNames.Cooler;
			}
		}

		protected override void DoMachineSpecificTick()
		{
			// Climate machines use CompTempControl for temperature management
			// Subcore provides: remote flick, auto-flick, inverter mode (for coolers)
		}

		/// <summary>
		/// Override power consumption to let vanilla handle it entirely.
		/// Coolers/heaters have dynamic power based on operatingAtHighPower,
		/// and fighting with vanilla's power system causes oscillation issues.
		/// </summary>
		/// <summary>
		/// Override power consumption for climate machines.
		/// When OFF: set power to 0 (same as other machines)
		/// When ON: don't touch power, let vanilla handle dynamic power based on operatingAtHighPower
		/// </summary>
		/// <summary>
		/// Override power consumption for climate machines.
		/// When OFF: set power to 0
		/// When ON: restore base power consumption, let vanilla handle dynamic states
		/// </summary>
		/// <summary>
		/// Override power consumption to let vanilla handle it entirely.
		/// Climate machines (coolers/heaters) work with vanilla's flickable+power system.
		/// </summary>
		public override void UpdatePowerConsumption()
		{
			// Don't touch power - let vanilla's flickable system handle it
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (_isHeater)
			{
				return ClimateHandler.GetHeaterInspectString(this);
			}
			if (_isCooler)
			{
				return ClimateHandler.GetCoolerInspectString(this);
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			if (_isHeater)
			{
				foreach (var gizmo in ClimateHandler.GetHeaterGizmos(this))
				{
					yield return gizmo;
				}
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (_isCooler && SubcoreAutomationMod.Settings.coolerInverterPatchEnabled)
			{
				return ClimateHandler.GetCoolerBenefitsDescription();
			}
			if (_isHeater)
			{
				return ClimateHandler.GetHeaterBenefitsDescription();
			}
			return "\n\n" + "SubcoreAutomation_ClimateBenefits".Translate();
		}

		#endregion
	}
}
