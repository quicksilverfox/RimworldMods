using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Properties for the SubcoreAutomation component.
	/// </summary>
	public class CompProperties_SubcoreAutomation : CompProperties
	{
		/// <summary>
		/// The subcore quality tier required for automation. Set this in XML (Basic/Regular/High)
		/// instead of referencing a concrete subcore def, so the def doesn't have to exist
		/// (e.g. when Biotech is not installed).
		/// </summary>
		public SubcoreTier tier = SubcoreTier.Regular;

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

		/// <summary>
		/// The defName of the Biotech subcore that corresponds to this tier.
		/// </summary>
		public string SubcoreDefName
		{
			get
			{
				switch (tier)
				{
					case SubcoreTier.Basic: return "SubcoreBasic";
					case SubcoreTier.High: return "SubcoreHigh";
					default: return "SubcoreRegular";
				}
			}
		}

		private ThingDef cachedSubcoreDef;
		private bool subcoreDefResolved;

		/// <summary>
		/// The concrete subcore ThingDef for this tier, or null if it doesn't exist
		/// (e.g. Biotech is not installed). Resolved lazily and cached.
		/// </summary>
		public ThingDef SubcoreDef
		{
			get
			{
				if (!subcoreDefResolved)
				{
					cachedSubcoreDef = DefDatabase<ThingDef>.GetNamedSilentFail(SubcoreDefName);
					subcoreDefResolved = true;
				}
				return cachedSubcoreDef;
			}
		}
	}
}
