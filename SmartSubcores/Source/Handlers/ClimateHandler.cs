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
	/// Handler for climate control automation - Heater and Cooler.
	/// </summary>
	public static class ClimateHandler
	{
		/// <summary>
		/// Returns the inspect string for an automated heater.
		/// </summary>
		public static string GetHeaterInspectString(CompSubcoreAutomationBase comp)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_AutomatedSimple".Translate());

			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_DetonatorMode".Translate());

			return sb.ToString();
		}

		/// <summary>
		/// Returns the inspect string for an automated cooler.
		/// Shows current inverter mode (heating/cooling) when the feature is enabled.
		/// </summary>
		public static string GetCoolerInspectString(CompSubcoreAutomationBase comp)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_AutomatedSimple".Translate());

			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.coolerInverterPatchEnabled)
				return sb.ToString();

			var tempControl = comp.parent.TryGetComp<CompTempControl>();
			var building = comp.parent as Building;
			if (tempControl == null || building == null || building.Map == null)
				return sb.ToString();

			IntVec3 blueSide = building.Position + IntVec3.South.RotatedBy(building.Rotation);
			if (!blueSide.InBounds(building.Map))
				return sb.ToString();

			float blueTemp = blueSide.GetTemperature(building.Map);
			bool heating = blueTemp < tempControl.targetTemperature;

			sb.AppendLine();
			sb.Append("  ");
			sb.Append((heating ? "SubcoreAutomation_InverterModeHeating" : "SubcoreAutomation_InverterModeCooling").Translate());

			return sb.ToString();
		}

		/// <summary>
		/// Returns the gizmos for an automated heater.
		/// </summary>
		public static IEnumerable<Gizmo> GetHeaterGizmos(CompSubcoreAutomationBase comp)
		{
			yield return new Command_Action
			{
				defaultLabel = "SubcoreAutomation_Detonate".Translate(),
				defaultDesc = "SubcoreAutomation_DetonateDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/Detonate", false) ?? TexCommand.Attack,
				action = delegate
				{
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"SubcoreAutomation_DetonateConfirm".Translate(),
						() => CoolerPatches.DetonateHeater(comp.parent as Building),
						destructive: true));
				}
			};
		}

		/// <summary>
		/// Returns the benefits description for heater automation tooltip.
		/// </summary>
		public static string GetHeaterBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_HeaterBenefits".Translate();
		}

		/// <summary>
		/// Returns the benefits description for cooler automation tooltip.
		/// </summary>
		public static string GetCoolerBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_CoolerBenefits".Translate();
		}
	}
}
