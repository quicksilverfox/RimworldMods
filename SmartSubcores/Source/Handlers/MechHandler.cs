using System.Text;
using RimWorld;
using Verse;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for Mechanitor-related machines: MechCharger, MechBooster, and BandNode.
	/// </summary>
	public static class MechHandler
	{
		#region Mech Charger

		public static string GetMechChargerInspectString(CompSubcoreAutomationBase comp)
		{
			// Condensed single-line format: "Automated charger (2x speed, auto-repair, downed repair)"
			return "SubcoreAutomation_MechChargerEnhanced".Translate();
		}

		public static string GetMechChargerBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_ChargerBenefits".Translate();
		}

		#endregion

		#region Mech Booster

		public static string GetMechBoosterInspectString(CompSubcoreAutomationBase comp)
		{
			if (!SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
				return "SubcoreAutomation_AutomatedSimple".Translate();

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_MechBoosterCommandRelay".Translate());

			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_MechBoosterTacticalRelay".Translate());

			if (HasMechanitorInContainer(comp.parent.Map))
			{
				sb.AppendLine();
				sb.Append("  ");
				sb.Append("SubcoreAutomation_MechBoosterRemoteShort".Translate().CapitalizeFirst());
			}

			return sb.ToString();
		}

		private static bool HasMechanitorInContainer(Map map)
		{
			if (map == null) return false;
			foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.ThingHolder))
			{
				if (thing is Building_Enterable enterable)
				{
					var innerContainer = enterable.GetDirectlyHeldThings();
					if (innerContainer == null) continue;
					foreach (Thing innerThing in innerContainer)
					{
						if (innerThing is Pawn pawn && pawn.IsColonist && pawn.mechanitor != null && !pawn.Downed)
							return true;
					}
				}
			}
			return false;
		}

		public static string GetMechBoosterBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_MechBoosterBenefits".Translate();
		}

		#endregion

		#region Band Node

		public static string GetBandNodeInspectString(CompSubcoreAutomationBase comp)
		{
			// Combine into single informative line
			return "SubcoreAutomation_BandNodeEnhanced".Translate() + " (" + "SubcoreAutomation_BandNodeBandwidth".Translate("2") + ")";
		}

		public static string GetBandNodeBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_BandNodeBenefits".Translate();
		}

		#endregion
	}
}
