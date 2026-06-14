using System.Collections.Generic;
using System.Text;
using RimWorld;
using SubcoreAutomation.Core;
using SubcoreAutomation.Patches;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for production automation - Drill, Hydroponics, Toxifier, and Moisture Pump.
	/// </summary>
	public static class ProductionHandler
	{
		#region Hydroponics

		/// <summary>
		/// Returns the inspect string for an automated hydroponics basin.
		/// </summary>
		public static string GetHydroponicsInspectString(IProductionAutomation comp)
		{
			// Check if feature is enabled
			if (!SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
			{
				return "SubcoreAutomation_AutomatedSimple".Translate();
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_AutomatedSimple".Translate());

			// Show auto-farming status
			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_HydroponicsAutoFarm".Translate());

			// Show sun lamp status
			sb.AppendLine();
			sb.Append("  ");
			if (comp.SunLampEnabled)
			{
				// We always manage power ourselves when subcore is installed,
				// so always show actual power consumption
				int sunLampPower = HydroponicsPatches.GetSunLampPower(comp);
				if (sunLampPower > 0)
				{
					sb.Append("SubcoreAutomation_HydroponicsSunLampActive".Translate(sunLampPower));
				}
				else
				{
					sb.Append("SubcoreAutomation_HydroponicsSunLampNight".Translate());
				}
			}
			else
			{
				sb.Append("SubcoreAutomation_HydroponicsSunLamp".Translate("SubcoreAutomation_Disabled".Translate()));
			}

			return sb.ToString();
		}

		/// <summary>
		/// Returns the gizmos for an automated hydroponics basin.
		/// </summary>
		public static IEnumerable<Gizmo> GetHydroponicsGizmos(IProductionAutomation comp)
		{
			yield return new Command_Toggle
			{
				defaultLabel = "SubcoreAutomation_SunLamp".Translate(),
				defaultDesc = "SubcoreAutomation_SunLampDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("Things/Building/Production/LampSun", false) ?? TexCommand.DesirePower,
				isActive = () => comp.SunLampEnabled,
				toggleAction = delegate
				{
					comp.SunLampEnabled = !comp.SunLampEnabled;
					// Update power consumption
					comp.UpdatePowerConsumption();
					// Force glower to update
					comp.UpdateHydroponicsGlower();
				}
			};
		}

		/// <summary>
		/// Returns the benefits description for hydroponics automation tooltip.
		/// </summary>
		public static string GetHydroponicsBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_HydroponicsBenefits".Translate();
		}

		#endregion

		#region Toxifier

		/// <summary>
		/// Returns the inspect string for an automated toxifier generator.
		/// </summary>
		public static string GetToxifierInspectString(IProductionAutomation comp)
		{
			// Check if feature is enabled
			if (!SubcoreAutomationMod.Settings.toxifierWastepackEnabled)
			{
				return "SubcoreAutomation_AutomatedSimple".Translate();
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_AutomatedSimple".Translate());

			// Show wastepack production info
			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_ToxifierWasteMode".Translate());

			return sb.ToString();
		}

		/// <summary>
		/// Returns the benefits description for toxifier automation tooltip.
		/// </summary>
		public static string GetToxifierBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_ToxifierBenefits".Translate();
		}

		#endregion

		#region Moisture Pump

		/// <summary>
		/// Returns the inspect string for an automated moisture pump.
		/// </summary>
		public static string GetMoisturePumpInspectString(IProductionAutomation comp)
		{
			int remainingCells = comp.CountRemainingWetCells();
			if (remainingCells <= 0)
				return "SubcoreAutomation_SmartPumping".Translate();

			const int PumpIntervalTicks = 24828; // 145 tiles in 60 days
			int currentProgress = 0;
			CompTerrainPump pump = comp.Parent.TryGetComp<CompTerrainPump>();
			if (pump != null)
			{
				var progressField = HarmonyLib.AccessTools.Field(typeof(CompTerrainPump), "progressTicks");
				if (progressField != null && progressField.GetValue(pump) is int p)
					currentProgress = p;
			}

			float speedMultiplier = SubcoreAutomationMod.Settings.moisturePumpSpeedMultiplier;
			int effectiveInterval = (int)(PumpIntervalTicks / speedMultiplier);
			int estimatedTicks = (remainingCells * effectiveInterval) - (int)(currentProgress / speedMultiplier);
			if (estimatedTicks < 0) estimatedTicks = 0;

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_SmartPumping".Translate());
			sb.AppendLine();
			sb.Append("SubcoreAutomation_PumpProgress".Translate(remainingCells, estimatedTicks.ToStringTicksToPeriod()));
			return sb.ToString();
		}

		/// <summary>
		/// Returns the benefits description for moisture pump automation tooltip.
		/// </summary>
		public static string GetMoisturePumpBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_MoisturePumpBenefits".Translate();
		}

		#endregion

		#region Deep Drill

		/// <summary>
		/// Returns the benefits description for deep drill automation tooltip.
		/// </summary>
		public static string GetDrillBenefitsDescription(IProductionAutomation comp)
		{
			var drillSettings = SubcoreAutomationMod.Settings.GetSettings(comp.Parent.def.defName);
			float efficiency = comp.GetEffectiveEfficiency(drillSettings);
			return "\n\n" + "SubcoreAutomation_DrillBenefits".Translate(efficiency.ToStringPercent());
		}

		#endregion
	}
}
