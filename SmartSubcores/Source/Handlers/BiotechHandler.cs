using System.Text;
using RimWorld;
using Verse;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for Biotech DLC machines: Biosculpter, GeneExtractor, GrowthVat, and scanners.
	/// </summary>
	public static class BiotechHandler
	{
		#region Biosculpter

		public static string GetBiosculpterInspectString(CompSubcoreAutomationBase comp)
		{
			return "SubcoreAutomation_BiosculpterUniversal".Translate();
		}

		public static string GetBiosculpterBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_BiosculpterBenefits".Translate();
		}

		#endregion

		#region Gene Extractor

		public static string GetGeneExtractorInspectString(CompSubcoreAutomationBase comp)
		{
			// Check if feature is enabled
			if (!SubcoreAutomationMod.Settings.geneExtractorFeaturesEnabled)
			{
				return "SubcoreAutomation_AutomatedSimple".Translate();
			}

			// Condensed: "Enhanced extraction (stasis, min recovery, targeted)"
			return "SubcoreAutomation_GeneExtractorEnhanced".Translate();
		}

		public static string GetGeneExtractorBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_GeneExtractorBenefits".Translate();
		}

		#endregion

		#region Growth Vat

		public static string GetGrowthVatInspectString(CompSubcoreAutomationBase comp)
		{
			// Check if vat is growing embryo or child
			if (comp.parent is Building_GrowthVat vat)
			{
				if (vat.selectedEmbryo != null)
				{
					// Condensed: "Enhanced vat (embryo, 85% birth quality)"
					return "SubcoreAutomation_GrowthVatEmbryoEnhanced".Translate();
				}
				else if (vat.SelectedPawn != null)
				{
					// Condensed: "Enhanced vat (accelerated learning, passion focus)"
					return "SubcoreAutomation_GrowthVatChildEnhanced".Translate();
				}
			}

			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		public static string GetGrowthVatBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_GrowthVatBenefits".Translate();
		}

		#endregion

		#region Subcore Softscanner

		public static string GetSoftscannerInspectString(CompSubcoreAutomationBase comp)
		{
			// Check if feature is enabled
			if (!SubcoreAutomationMod.Settings.softscannerFeaturesEnabled)
			{
				return "SubcoreAutomation_AutomatedSimple".Translate();
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_AutomatedSimple".Translate());

			// Show reduced sickness duration
			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_SoftscannerReducedSickness".Translate());

			return sb.ToString();
		}

		public static string GetSoftscannerBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_SoftscannerBenefits".Translate();
		}

		#endregion

		#region Subcore Ripscanner

		public static string GetRipscannerInspectString(CompSubcoreAutomationBase comp)
		{
			// Check if feature is enabled
			if (!SubcoreAutomationMod.Settings.ripscannerFeaturesEnabled)
			{
				return "SubcoreAutomation_AutomatedSimple".Translate();
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_AutomatedSimple".Translate());

			// Show organ harvesting info
			sb.AppendLine();
			sb.Append("  ");
			sb.Append("SubcoreAutomation_RipscannerOrganHarvest".Translate());

			return sb.ToString();
		}

		public static string GetRipscannerBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_RipscannerBenefits".Translate();
		}

		#endregion
	}
}
