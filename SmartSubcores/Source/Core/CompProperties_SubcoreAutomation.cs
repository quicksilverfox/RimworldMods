using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Properties for the SubcoreAutomation component.
	/// </summary>
	public class CompProperties_SubcoreAutomation : CompProperties
	{
		/// <summary>
		/// The type of subcore required for automation.
		/// </summary>
		public ThingDef subcoreDef;

		/// <summary>
		/// Speed multiplier when running automated (0.5 = 50% speed).
		/// </summary>
		public float automatedSpeedFactor = 0.5f;

		/// <summary>
		/// Additional power consumption when running automated.
		/// </summary>
		public int automatedPowerConsumption = 200;

		/// <summary>
		/// Work amount required to install the subcore.
		/// </summary>
		public float installWorkAmount = 2000f;

		public CompProperties_SubcoreAutomation()
		{
			compClass = typeof(CompSubcoreAutomation);
		}
	}
}
