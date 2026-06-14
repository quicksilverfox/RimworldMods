using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using SubcoreAutomation.Patches;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for miscellaneous machines.
	/// Handles: CommsConsole, OrbitalTradeBeacon, SleepAccelerator, PilotConsole.
	/// </summary>
	public class CompMiscAutomation : CompSubcoreAutomationBase
	{
		#region Misc-Specific State

		private bool _isCommsConsole;
		private bool _isTradeBeacon;
		private bool _isSleepAccelerator;
		private bool _isPilotConsole;
		private bool _isResearchBench;
		private bool _isMultiAnalyzer;

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			string defName = parent.def.defName;
			_isCommsConsole = defName == MachineDefNames.CommsConsole;
			_isTradeBeacon = defName == MachineDefNames.OrbitalTradeBeacon;
			_isSleepAccelerator = defName == MachineDefNames.SleepAccelerator;
			_isPilotConsole = defName == MachineDefNames.PilotConsole || defName == MachineDefNames.ShipPilotSeat || defName == MachineDefNames.ShuttlePilotSeat;
			_isResearchBench = defName == MachineDefNames.HiTechResearchBench;
			_isMultiAnalyzer = defName == "MultiAnalyzer";
		}

		protected override void DoMachineSpecificTick()
		{
			// CommsConsole: Bonus orbital traders
			if (_isCommsConsole)
			{
				CommsConsoleHandler.HandleAutomation(parent, this, Props.automatedSpeedFactor);
				return;
			}
			
			// ResearchBench: Automated research
			if (_isResearchBench && parent.IsHashIntervalTick(250))
			{
				ResearchBenchHandler.HandleAutomation(parent, this, Props.automatedSpeedFactor);
				return;
			}
			
			// Other misc machines provide passive bonuses via Harmony patches:
			// - TradeBeacon: Extended range
			// - SleepAccelerator: Enhanced rest gain
			// - PilotConsole: Better shuttle control
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (_isCommsConsole)
			{
				return "SubcoreAutomation_CommsConsoleEnhanced".Translate();
			}
			if (_isTradeBeacon)
			{
				return "SubcoreAutomation_TradeBeaconEnhanced".Translate();
			}
			if (_isSleepAccelerator)
			{
				return "SubcoreAutomation_SleepAcceleratorEnhanced".Translate();
			}
			if (_isPilotConsole)
			{
				return "SubcoreAutomation_PilotConsoleEnhanced".Translate();
			}
			if (_isMultiAnalyzer)
			{
				return "SubcoreAutomation_AutomatedSimple".Translate();
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			if (_isPilotConsole)
			{
				foreach (var gizmo in PilotConsoleHandler.GetAutopilotGizmos(parent, this))
				{
					yield return gizmo;
				}
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (_isCommsConsole)
			{
				return "\n\n" + "SubcoreAutomation_CommsConsoleBenefits".Translate();
			}
			if (_isTradeBeacon)
			{
				return "\n\n" + "SubcoreAutomation_OrbitalTradeBeaconBenefits".Translate();
			}
			if (_isSleepAccelerator)
			{
				return "\n\n" + "SubcoreAutomation_SleepAcceleratorBenefits".Translate();
			}
			if (_isPilotConsole)
			{
				return "\n\n" + "SubcoreAutomation_PilotConsoleBenefits".Translate();
			}
			if (_isMultiAnalyzer)
			{
				return "\n\n" + "SubcoreAutomation_MultiAnalyzerBenefits".Translate();
			}
			// Generic misc automation fallback
			return "\n\n" + "SubcoreAutomation_FallbackBenefits".Translate();
		}

		#endregion
	}
}
