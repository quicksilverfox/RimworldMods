using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Status of backup power generator.
	/// </summary>
	public enum BackupPowerStatus
	{
		Standby,  // Ready to turn on when needed
		Running,  // Currently producing power
		NoFuel,   // Cannot run - out of fuel
		Error     // Cannot run - broken down
	}

	/// <summary>
	/// Subcore automation component for power machines.
	/// Handles: WoodFired/Chemfuel/Watermill/Fuel Generators, PowerSwitch.
	/// </summary>
	public class CompPowerAutomation : CompSubcoreAutomationBase
	{
		#region Power-Specific State

		// Backup power control
		private float _backupPowerBatteryMin = 0.25f;
		private float _backupPowerBatteryMax = 0.75f;
		private bool _backupPowerRunOnBatteriesOnly = true;
		private int _lastGeneratorOnTick;

		#endregion

		#region Properties

		public float BackupPowerBatteryMin
		{
			get => _backupPowerBatteryMin;
			set => _backupPowerBatteryMin = value;
		}

		public float BackupPowerBatteryMax
		{
			get => _backupPowerBatteryMax;
			set => _backupPowerBatteryMax = value;
		}

		public bool BackupPowerRunOnBatteriesOnly
		{
			get => _backupPowerRunOnBatteriesOnly;
			set => _backupPowerRunOnBatteriesOnly = value;
		}

		/// <summary>
		/// Copy backup power settings to another comp.
		/// </summary>
		public void CopyBackupPowerSettingsTo(CompPowerAutomation other)
		{
			other._backupPowerBatteryMin = _backupPowerBatteryMin;
			other._backupPowerBatteryMax = _backupPowerBatteryMax;
			other._backupPowerRunOnBatteriesOnly = _backupPowerRunOnBatteriesOnly;
		}

		public bool IsGenerator => PowerComp != null && PowerComp.Props is CompProperties_Power powerProps && powerProps.PowerConsumption < 0;

		/// <summary>
		/// Check if the generator can be turned off (respects minimum on time).
		/// </summary>
		public bool CanTurnOffGenerator() => PowerHandler.CanTurnOff(this);
		
		/// <summary>
		/// Turn the generator on.
		/// </summary>
		public void TurnOnGenerator() => PowerHandler.TurnOn(this);
		
		/// <summary>
		/// Turn the generator off.
		/// </summary>
		public void TurnOffGenerator() => PowerHandler.TurnOff(this);

		public BackupPowerStatus GeneratorStatus
		{
			get
			{
				if (!IsGenerator || !_subcoreInstalled)
					return BackupPowerStatus.Standby;

				if (_cachedBreakdownable?.BrokenDown ?? false)
					return BackupPowerStatus.Error;

				if (_cachedRefuelable != null && !_cachedRefuelable.HasFuel)
					return BackupPowerStatus.NoFuel;

				if (_cachedFlickable != null && _cachedFlickable.SwitchIsOn)
					return BackupPowerStatus.Running;

				return BackupPowerStatus.Standby;
			}
		}

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			// Register with power broker if this is a generator with subcore installed
			if (IsGenerator && _subcoreInstalled)
			{
				MapComponent_PowerBroker.RegisterGenerator(this);
			}
		}

		protected override void DoMachineSpecificTick()
		{
			// Backup power logic handled by MapComponent_PowerBroker
		}

		protected override void DoMachineSpecificTickRare()
		{
			// Backup power logic handled by MapComponent_PowerBroker
		}

		protected override void OnSubcoreInstalledRegistrations(bool respawningAfterLoad = false)
		{
			// Register with power broker when subcore is installed
			if (IsGenerator)
			{
				MapComponent_PowerBroker.RegisterGenerator(this);
			}
		}

		protected override void OnSubcoreRemovedRegistrations()
		{
			// Deregister from power broker when subcore is removed
			if (IsGenerator)
			{
				MapComponent_PowerBroker.DeregisterGenerator(this);
			}
		}

		protected override void ExposeDataMachineSpecific()
		{
			Scribe_Values.Look(ref _backupPowerBatteryMin, "backupPowerBatteryMin", 0.25f);
			Scribe_Values.Look(ref _backupPowerBatteryMax, "backupPowerBatteryMax", 0.75f);
			Scribe_Values.Look(ref _backupPowerRunOnBatteriesOnly, "backupPowerRunOnBatteriesOnly", true);
			Scribe_Values.Look(ref _lastGeneratorOnTick, "lastGeneratorOnTick", 0);
		}

		protected override void OnDestroyMachineSpecific(DestroyMode mode, Map previousMap)
		{
			if (IsGenerator)
			{
				MapComponent_PowerBroker.DeregisterGenerator(this);
				PowerHandler.Cleanup(this);
			}
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (IsGenerator && SubcoreAutomationMod.Settings.backupPowerEnabled)
			{
				string statusKey = GeneratorStatus switch
				{
					BackupPowerStatus.Running => "SubcoreAutomation_StatusRunning",
					BackupPowerStatus.Standby => "SubcoreAutomation_StatusStandby",
					BackupPowerStatus.NoFuel => "SubcoreAutomation_StatusNoFuel",
					BackupPowerStatus.Error => "SubcoreAutomation_StatusError",
					_ => "SubcoreAutomation_StatusStandby"
				};
				return "SubcoreAutomation_BackupPowerStatus".Translate(statusKey.Translate());
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			if (IsGenerator && SubcoreAutomationMod.Settings.backupPowerEnabled)
			{
				foreach (var gizmo in PowerHandler.GetGizmos(this))
				{
					yield return gizmo;
				}
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (IsGenerator)
			{
				return PowerHandler.GetBenefitsDescription();
			}
			// Power switch (non-generator) benefits
			return "\n\n" + "SubcoreAutomation_PowerSwitchBenefits".Translate();
		}

		#endregion
	}
}
