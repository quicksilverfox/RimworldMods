namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore quality tier. Decoupled from the concrete subcore ThingDef so the mod
	/// works without Biotech: the tier determines both which Biotech subcore is used
	/// (when present) and which crafted-component fallback materials are required.
	/// </summary>
	public enum SubcoreTier
	{
		Basic,
		Regular,
		High
	}
}
