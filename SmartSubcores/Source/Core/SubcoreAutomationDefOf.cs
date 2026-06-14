using RimWorld;
using Verse;

namespace SubcoreAutomation.Core
{
	[DefOf]
	public static class SubcoreAutomationDefOf
	{
		public static JobDef SubcoreAutomation_InstallSubcore;
		public static JobDef SubcoreAutomation_InstallFallback;
		public static JobDef SubcoreAutomation_RemoveSubcore;

		public static HediffDef SubcoreAutomation_MechBoosterBoost;

		static SubcoreAutomationDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(SubcoreAutomationDefOf));
		}
	}
}
