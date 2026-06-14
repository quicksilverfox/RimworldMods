using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// CompProperties for climate control machines (Heater, Cooler).
	/// </summary>
	public class CompProperties_ClimateAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_ClimateAutomation()
		{
			compClass = typeof(CompClimateAutomation);
		}
	}

	/// <summary>
	/// CompProperties for defense machines (Turrets, ProximityDetector).
	/// </summary>
	public class CompProperties_DefenseAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_DefenseAutomation()
		{
			compClass = typeof(CompDefenseAutomation);
		}
	}

	/// <summary>
	/// CompProperties for production machines (DeepDrill, Hydroponics, MoisturePump, Toxifier).
	/// </summary>
	public class CompProperties_ProductionAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_ProductionAutomation()
		{
			compClass = typeof(CompProductionAutomation);
		}
	}

	/// <summary>
	/// CompProperties for utility machines (Doors, NPD, VitalsMonitor).
	/// </summary>
	public class CompProperties_UtilityAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_UtilityAutomation()
		{
			compClass = typeof(CompUtilityAutomation);
		}
	}

	/// <summary>
	/// CompProperties for power machines (Generators, PowerSwitch).
	/// </summary>
	public class CompProperties_PowerAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_PowerAutomation()
		{
			compClass = typeof(CompPowerAutomation);
		}
	}

	/// <summary>
	/// CompProperties for mech machines (MechCharger, MechBooster, BandNode).
	/// </summary>
	public class CompProperties_MechAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_MechAutomation()
		{
			compClass = typeof(CompMechAutomation);
		}
	}

	/// <summary>
	/// CompProperties for Biotech machines (Biosculpter, GrowthVat, GeneExtractor, Scanners).
	/// </summary>
	public class CompProperties_BiotechAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_BiotechAutomation()
		{
			compClass = typeof(CompBiotechAutomation);
		}
	}

	/// <summary>
	/// CompProperties for scanner machines (GroundPenetratingScanner, LongRangeMineralScanner).
	/// </summary>
	public class CompProperties_ScannerAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_ScannerAutomation()
		{
			compClass = typeof(CompScannerAutomation);
		}
	}

	/// <summary>
	/// CompProperties for misc machines (CommsConsole, TradeBeacon, SleepAccelerator, PilotConsole).
	/// </summary>
	public class CompProperties_MiscAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_MiscAutomation()
		{
			compClass = typeof(CompMiscAutomation);
		}
	}

	/// <summary>
	/// CompProperties for compat machines (VNPE Grinder/Feeder).
	/// </summary>
	public class CompProperties_CompatAutomation : CompProperties_SubcoreAutomation
	{
		public CompProperties_CompatAutomation()
		{
			compClass = typeof(CompCompatAutomation);
		}
	}
}
