using System.Collections.Generic;
using System.Text;
using RimWorld;
using SubcoreAutomation.Core;
using SubcoreAutomation.UI;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for generator automation - backup power control.
	/// </summary>
	public static class PowerHandler
	{
		#region Generator State

		// Per-instance state for generator tracking
		private static readonly Dictionary<CompPowerAutomation, GeneratorState> _generatorStates =
			new Dictionary<CompPowerAutomation, GeneratorState>();

		private class GeneratorState
		{
			public int LastOnTick;
		}

		#endregion

		#region Generator Methods

		/// <summary>
		/// Check if the generator can be turned off (respects minimum on time).
		/// </summary>
		public static bool CanTurnOff(CompPowerAutomation comp)
		{
			if (!_generatorStates.TryGetValue(comp, out var state))
				return true;
			
			return state.LastOnTick + SubcoreAutomationMod.Settings.backupPowerMinimumOnTime < Find.TickManager.TicksGame;
		}

		/// <summary>
		/// Turn the generator on.
		/// </summary>
		public static void TurnOn(CompPowerAutomation comp)
		{
			var flickable = comp.CachedFlickable;
			if (flickable == null) return;

			// Get or create state
			if (!_generatorStates.TryGetValue(comp, out var state))
			{
				state = new GeneratorState();
				_generatorStates[comp] = state;
			}
			state.LastOnTick = Find.TickManager.TicksGame;

			Core.SubcoreAutomationUtils.ForceFlickable(flickable, true);
		}

		/// <summary>
		/// Turn the generator off.
		/// </summary>
		public static void TurnOff(CompPowerAutomation comp)
		{
			var flickable = comp.CachedFlickable;
			if (flickable == null)
			{
				Log.Warning($"[SubcoreAutomation] TurnOff: flickable is null for {comp.parent.LabelCap}");
				return;
			}

			if (Prefs.DevMode)
			{
				Log.Message($"[SubcoreAutomation] TurnOff: Before - SwitchIsOn={flickable.SwitchIsOn}");
			}

			Core.SubcoreAutomationUtils.ForceFlickable(flickable, false);

			if (Prefs.DevMode)
			{
				Log.Message($"[SubcoreAutomation] TurnOff: After - SwitchIsOn={flickable.SwitchIsOn}");
			}
		}

		/// <summary>
		/// Performs backup power automation tick.
		/// Checks battery levels and power balance on the network to turn generator on/off.
		/// Logic based on original BackupPower mod by Fluffy.
		/// </summary>
		public static void DoBackupPowerTick(CompPowerAutomation comp)
		{
			if (!comp.IsGenerator || !comp.SubcoreInstalled)
				return;

			var power = comp.PowerComp;
			if (power?.PowerNet == null)
				return;

			// Skip if broken down or out of fuel
			if (comp.GeneratorStatus == BackupPowerStatus.Error || comp.GeneratorStatus == BackupPowerStatus.NoFuel)
				return;



			// Calculate battery state on the network
			float storedEnergy = 0f;
			float maxEnergy = 0f;
			bool hasBatteries = false;

			foreach (var battery in power.PowerNet.batteryComps)
			{
				storedEnergy += battery.StoredEnergy;
				maxEnergy += battery.Props.storedEnergyMax;
				hasBatteries = true;
			}

			float batteryPercent = maxEnergy > 0 ? storedEnergy / maxEnergy : 0f;

			// Calculate production vs consumption on the network
			float totalProduction = 0f;
			float totalConsumption = 0f;
			float thisGeneratorOutput = 0f;

			foreach (var powerComp in power.PowerNet.powerComps)
			{
				float output = powerComp.PowerOutput;
				if (output > 0)
				{
					totalProduction += output;
					// Track this generator's contribution
					if (powerComp == power)
						thisGeneratorOutput = output;
				}
				else if (output < 0)
				{
					totalConsumption += -output;
				}
			}

			bool isRunning = comp.GeneratorStatus == BackupPowerStatus.Running;

			// Debug logging
			if (Prefs.DevMode && comp.parent.IsHashIntervalTick(250))
			{
				Log.Message($"[SubcoreAutomation] {comp.parent.LabelCap}: isRunning={isRunning}, batteryPercent={batteryPercent:P0}, max={comp.BackupPowerBatteryMax:P0}, production={totalProduction}, consumption={totalConsumption}, thisOutput={thisGeneratorOutput}");
			}

			if (isRunning)
			{
				// Turn off conditions (from original BackupPower mod):
				// 1. This generator's output is not needed (production - thisOutput still > consumption)
				//    OR we're in runOnBatteriesOnly mode
				// 2. AND batteries are above max threshold (or no batteries and not runOnBatteriesOnly)
				// 3. AND minimum on-time has passed

				bool outputNotNeeded = (totalProduction - thisGeneratorOutput) >= totalConsumption;
				bool batteryConditionMet = hasBatteries 
					? batteryPercent >= comp.BackupPowerBatteryMax 
					: !comp.BackupPowerRunOnBatteriesOnly;

				// In runOnBatteriesOnly mode, only battery level matters (not production balance)
				bool shouldTurnOff = comp.BackupPowerRunOnBatteriesOnly
					? (hasBatteries && batteryPercent >= comp.BackupPowerBatteryMax)
					: (outputNotNeeded && batteryConditionMet);

				bool canTurnOff = CanTurnOff(comp);

				// Debug logging
				if (Prefs.DevMode && comp.parent.IsHashIntervalTick(250))
				{
					Log.Message($"[SubcoreAutomation] {comp.parent.LabelCap}: shouldTurnOff={shouldTurnOff}, canTurnOff={canTurnOff}, runOnBatteriesOnly={comp.BackupPowerRunOnBatteriesOnly}, outputNotNeeded={outputNotNeeded}, batteryConditionMet={batteryConditionMet}");
				}

				if (shouldTurnOff && canTurnOff)
				{
					Log.Message($"[SubcoreAutomation] Turning OFF {comp.parent.LabelCap}");
					TurnOff(comp);
				}
			}
			else
			{
				// Turn on conditions:
				// In runOnBatteriesOnly mode: only battery level matters
				// Otherwise: turn on when batteries are low OR there's a production shortfall
				
				bool shouldTurnOn;
				if (comp.BackupPowerRunOnBatteriesOnly)
				{
					// Only turn on when batteries fall below min threshold
					shouldTurnOn = hasBatteries && batteryPercent <= comp.BackupPowerBatteryMin;
				}
				else
				{
					// Turn on when batteries are low OR production can't meet consumption
					bool batteryLow = hasBatteries && batteryPercent <= comp.BackupPowerBatteryMin;
					bool productionShortfall = totalProduction < totalConsumption;
					shouldTurnOn = batteryLow || productionShortfall;
				}

				if (shouldTurnOn)
				{
					TurnOn(comp);
				}
			}
		}



		/// <summary>
		/// Clean up generator state when comp is destroyed.
		/// </summary>
		public static void Cleanup(CompPowerAutomation comp)
		{
			_generatorStates.Remove(comp);
		}

		#endregion

		#region Inspect String and Gizmos

		/// <summary>
		/// Returns the inspect string for an automated generator.
		/// </summary>
		public static string GetInspectString(CompPowerAutomation comp)
		{
			StringBuilder sb = new StringBuilder();
			
			// Show status on first line
			string statusKey = comp.GeneratorStatus switch
			{
				BackupPowerStatus.Running => "SubcoreAutomation_StatusRunning",
				BackupPowerStatus.Standby => "SubcoreAutomation_StatusStandby",
				BackupPowerStatus.NoFuel => "SubcoreAutomation_StatusNoFuel",
				BackupPowerStatus.Error => "SubcoreAutomation_StatusError",
				_ => "SubcoreAutomation_StatusStandby"
			};
			sb.Append("SubcoreAutomation_BackupPowerStatus".Translate(statusKey.Translate()));
			
			// Combine thresholds into single line
			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_BackupPowerThresholds".Translate(
				comp.BackupPowerBatteryMin.ToStringPercent(), 
				comp.BackupPowerBatteryMax.ToStringPercent()));

			return sb.ToString();
		}

		/// <summary>
		/// Returns the gizmos for an automated generator with backup power.
		/// </summary>
		public static IEnumerable<Gizmo> GetGizmos(CompPowerAutomation comp)
		{
			// Battery range control gizmo
			yield return new Command_BatteryRange(comp);

			// Run on batteries only toggle
			yield return new Command_Toggle
			{
				defaultLabel = "SubcoreAutomation_BackupPower_RunOnBatteries".Translate(),
				defaultDesc = "SubcoreAutomation_BackupPower_RunOnBatteriesDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/BackupPower/Battery", false)
					?? DefDatabase<ThingDef>.GetNamed("Battery", false)?.uiIcon
					?? TexCommand.DesirePower,
				isActive = () => comp.BackupPowerRunOnBatteriesOnly,
				toggleAction = delegate
				{
					comp.BackupPowerRunOnBatteriesOnly = !comp.BackupPowerRunOnBatteriesOnly;
					// When disabling "run on batteries only", immediately check if generator should turn on
					if (!comp.BackupPowerRunOnBatteriesOnly)
					{
						DoBackupPowerTick(comp);
					}
				}
			};
		}

		/// <summary>
		/// Returns the benefits description for generator automation tooltip.
		/// </summary>
		public static string GetBenefitsDescription()
		{
			if (SubcoreAutomationMod.Settings.backupPowerEnabled)
			{
				return "\n\n" + "SubcoreAutomation_GeneratorBenefits".Translate();
			}
			return "";
		}

		#endregion
	}
}
