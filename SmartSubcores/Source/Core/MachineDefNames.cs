namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Centralized constants for machine defNames used throughout the mod.
	/// Prevents magic strings and enables compile-time checking.
	/// </summary>
	public static class MachineDefNames
	{
		// Climate Control
		public const string Heater = "Heater";
		public const string Cooler = "Cooler";

		// Biotech
		public const string GeneExtractor = "GeneExtractor";
		public const string SubcoreSoftscanner = "SubcoreSoftscanner";
		public const string SubcoreRipscanner = "SubcoreRipscanner";

		// Mechanitor
		public const string BasicRecharger = "BasicRecharger";
		public const string StandardRecharger = "StandardRecharger";
		public const string MechBooster = "MechBooster";
		public const string BfG_MechBooster = "BfG_MechBooster"; // Biotech for Gravship: floor-mounted variant
		public const string BandNode = "BandNode";

		/// <summary>
		/// All ThingDef defNames treated as mech boosters (vanilla + supported mods).
		/// Add new variants here — CompMechAutomation and MechBoosterPatches read this list.
		/// </summary>
		public static readonly string[] AllMechBoosters =
		{
			MechBooster,
			BfG_MechBooster,
		};

		public static bool IsMechBooster(string defName)
		{
			for (int i = 0; i < AllMechBoosters.Length; i++)
			{
				if (AllMechBoosters[i] == defName)
					return true;
			}
			return false;
		}

		// Misc Buildings
		public const string CommsConsole = "CommsConsole";
		public const string OrbitalTradeBeacon = "OrbitalTradeBeacon";
		public const string SleepAccelerator = "SleepAccelerator";
		public const string PilotConsole = "PilotConsole";
		public const string ShipPilotSeat = "ShipPilotSeat";
		public const string ShuttlePilotSeat = "ShuttlePilotSeat";
		public const string HiTechResearchBench = "HiTechResearchBench";

		// Defense
		public const string ProximityDetector = "ProximityDetector";
		public const string TurretRocketswarmLauncher = "Turret_RocketswarmLauncher";

		// Production
		public const string MoisturePump = "MoisturePump";
		public const string ToxifierGenerator = "ToxifierGenerator";

		// Medical
		public const string VitalsMonitor = "VitalsMonitor";

		// VNPE Compatibility
		public const string VNPE_NutrientPasteGrinder = "VNPE_NutrientPasteGrinder";
		public const string VNPE_NutrientPasteFeeder = "VNPE_NutrientPasteFeeder";

		// Resources
		public const string ComponentSpacer = "ComponentSpacer";
	}
}
