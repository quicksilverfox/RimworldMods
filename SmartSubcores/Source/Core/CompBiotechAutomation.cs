using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for Biotech machines.
	/// Handles: Biosculpter, GrowthVat, GeneExtractor, Softscanner, Ripscanner.
	/// </summary>
	public class CompBiotechAutomation : CompSubcoreAutomationBase
	{
		#region Biotech-Specific State

		private CompBiosculpterPod _cachedBiosculpter;
		private Building_GrowthVat _cachedGrowthVat;
		private bool _isGeneExtractor;
		private bool _isSoftscanner;
		private bool _isRipscanner;

		#endregion

		#region Properties

		public CompBiosculpterPod CachedBiosculpter => _cachedBiosculpter;
		public Building_GrowthVat CachedGrowthVat => _cachedGrowthVat;
		public bool IsBiosculpter => _cachedBiosculpter != null;
		public bool IsGrowthVat => _cachedGrowthVat != null;

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			_cachedBiosculpter = parent.TryGetComp<CompBiosculpterPod>();
			_cachedGrowthVat = parent as Building_GrowthVat;

			string defName = parent.def.defName;
			_isGeneExtractor = defName == MachineDefNames.GeneExtractor;
			_isSoftscanner = defName == MachineDefNames.SubcoreSoftscanner;
			_isRipscanner = defName == MachineDefNames.SubcoreRipscanner;
		}

		protected override void DoMachineSpecificTick()
		{
			// Biotech machines get bonuses via Harmony patches:
			// - Biosculpter: Faster cycle speed, reduced nutrition drain
			// - GrowthVat: Faster growth, improved quality
			// - GeneExtractor: Faster extraction
			// - Scanners: Handled by SubcoreRipscannerPatches
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (IsBiosculpter)
			{
				return BiotechHandler.GetBiosculpterInspectString(this);
			}
			if (IsGrowthVat)
			{
				return BiotechHandler.GetGrowthVatInspectString(this);
			}
			if (_isGeneExtractor)
			{
				return BiotechHandler.GetGeneExtractorInspectString(this);
			}
			if (_isSoftscanner)
			{
				return BiotechHandler.GetSoftscannerInspectString(this);
			}
			if (_isRipscanner)
			{
				return BiotechHandler.GetRipscannerInspectString(this);
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			yield break;
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (IsBiosculpter)
			{
				return BiotechHandler.GetBiosculpterBenefitsDescription();
			}
			if (IsGrowthVat)
			{
				return BiotechHandler.GetGrowthVatBenefitsDescription();
			}
			if (_isGeneExtractor)
			{
				return BiotechHandler.GetGeneExtractorBenefitsDescription();
			}
			if (_isSoftscanner)
			{
				return BiotechHandler.GetSoftscannerBenefitsDescription();
			}
			if (_isRipscanner)
			{
				return BiotechHandler.GetRipscannerBenefitsDescription();
			}
			return "";
		}

		#endregion
	}
}
