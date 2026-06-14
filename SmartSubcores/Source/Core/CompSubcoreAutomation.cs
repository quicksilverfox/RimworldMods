using System.Collections.Generic;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Generic subcore automation comp for machines without specialized behavior.
	/// Used by fallback automation and any def that wants the simplest comp.
	/// For specific machine types, use dedicated comp classes instead.
	/// </summary>
	public class CompSubcoreAutomation : CompSubcoreAutomationBase
	{
		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
		}

		protected override void DoMachineSpecificTick()
		{
		}

		protected override string GetMachineSpecificInspectString()
		{
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			yield break;
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_FallbackBenefits".Translate();
		}

		protected override void ExposeDataMachineSpecific()
		{
		}
	}
}
