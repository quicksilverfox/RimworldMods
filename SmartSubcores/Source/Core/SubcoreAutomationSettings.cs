using System.Collections.Generic;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Per-machine automation settings.
	/// </summary>
	public class MachineSettings : IExposable
	{
		public bool enabled = true;
		public float efficiency = -1f; // -1 means use default from XML

		// Turret-specific settings
		public float accuracyBonus = -1f;      // -1 means use default
		public float warmupReduction = -1f;    // -1 means use default
		public bool friendlyFirePrevention = true;

		// Generator-specific settings (per-instance overrides stored in comp)
		// These are defaults that can be overridden per-building

		public void ExposeData()
		{
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref efficiency, "efficiency", -1f);
			Scribe_Values.Look(ref accuracyBonus, "accuracyBonus", -1f);
			Scribe_Values.Look(ref warmupReduction, "warmupReduction", -1f);
			Scribe_Values.Look(ref friendlyFirePrevention, "friendlyFirePrevention", true);
		}
	}

	/// <summary>
	/// Mod settings for Smart Subcores.
	/// </summary>
	public class SubcoreAutomationSettings : ModSettings
	{
		/// <summary>
		/// Global toggle for mech charger patches (downed mech repair, faster charging).
		/// Disabling this completely skips all mech charger patch code for compatibility.
		/// </summary>
		public bool mechChargerPatchesEnabled = true;

		/// <summary>
		/// Global toggle for turret patches (accuracy, warmup, friendly fire).
		/// Disabling this completely skips all turret patch code for compatibility.
		/// </summary>
		public bool turretPatchesEnabled = true;

		// ============================================
		// TIER-BASED TURRET SETTINGS
		// Bonuses apply to all turrets using that subcore tier
		// ============================================

		/// <summary>
		/// Accuracy bonus for turrets using Basic subcores (e.g., mini-turret).
		/// </summary>
		public float turretBasicAccuracy = 0.10f;

		/// <summary>
		/// Warmup/cooldown reduction for turrets using Basic subcores.
		/// </summary>
		public float turretBasicWarmup = 0.10f;

		/// <summary>
		/// Accuracy bonus for turrets using Regular subcores (e.g., autocannon, sniper).
		/// </summary>
		public float turretRegularAccuracy = 0.15f;

		/// <summary>
		/// Warmup/cooldown reduction for turrets using Regular subcores.
		/// </summary>
		public float turretRegularWarmup = 0.10f;

		/// <summary>
		/// Accuracy bonus for turrets using High subcores (e.g., rocketswarm launcher).
		/// </summary>
		public float turretHighAccuracy = 0.20f;

		/// <summary>
		/// Warmup/cooldown reduction for turrets using High subcores.
		/// </summary>
		public float turretHighWarmup = 0.15f;

		/// <summary>
		/// Whether friendly fire prevention is enabled for all automated turrets.
		/// </summary>
		public bool turretFriendlyFirePrevention = true;

		/// <summary>
		/// Global toggle for backup power feature on generators.
		/// </summary>
		public bool backupPowerEnabled = true;

		/// <summary>
		/// Interval in ticks between power balance updates (default 60 = 1 second).
		/// </summary>
		public int backupPowerUpdateInterval = 60;

		/// <summary>
		/// Minimum time in ticks a generator stays on after activation (default 300 = 5 seconds).
		/// Prevents flickering on/off.
		/// </summary>
		public int backupPowerMinimumOnTime = 300;

		/// <summary>
		/// Global toggle for Toxifier generator wastepack production feature.
		/// When enabled, Toxifiers with subcores produce wastepacks instead of polluting.
		/// </summary>
		public bool toxifierWastepackEnabled = true;

		/// <summary>
		/// Global toggle for Gene Extractor enhanced features.
		/// When enabled: pawns hibernate during extraction, recovery time is minimum 12 days,
		/// and targeted extraction prefers new genes not already in gene banks.
		/// </summary>
		public bool geneExtractorFeaturesEnabled = true;

		/// <summary>
		/// Global toggle for Subcore Softscanner enhanced features.
		/// When enabled: scanning sickness duration is reduced by 1 day.
		/// </summary>
		public bool softscannerFeaturesEnabled = true;

		/// <summary>
		/// Global toggle for Subcore Ripscanner enhanced features.
		/// When enabled: organs are harvested from the subject before death.
		/// </summary>
		public bool ripscannerFeaturesEnabled = true;

		/// <summary>
		/// Number of organs to harvest from ripscanner subjects (1-6).
		/// Deathless pawns always give all 6 organs regardless of this setting.
		/// </summary>
		public int ripscannerOrganCount = 1;

		/// <summary>
		/// Toggle for scanner UI sorting and color-coding.
		/// When enabled: pawn selection menu is sorted by category (colonists, slaves, prisoners)
		/// and color-coded using vanilla's pawn name colors.
		/// </summary>
		public bool scannerUISortingEnabled = true;

		/// <summary>
		/// Allow any mechanoid to pick up and finish an unfinished crafting item
		/// started by another mechanoid, instead of vanilla's restriction that only
		/// the original creator can resume the work.
		/// </summary>
		public bool mechSharedUftCraftingEnabled = true;

		/// <summary>
		/// Global toggle for Mech Booster enhanced features.
		/// When enabled: extends command range, boosts combat mech stats.
		/// </summary>
		public bool mechBoosterFeaturesEnabled = true;

		/// <summary>
		/// Combat stat bonus percentage for mechs in boosted booster range.
		/// Applied to accuracy, aiming speed, melee hit chance, dodge chance.
		/// </summary>
		public float mechBoosterCombatBonus = 0.15f;

		/// <summary>
		/// Global toggle for Hydroponics Basin enhanced features.
		/// When enabled: automatic sowing and harvesting, built-in sun lamp.
		/// </summary>
		public bool hydroponicsFeaturesEnabled = true;

		/// <summary>
		/// Default state for the built-in sun lamp toggle on hydroponics basins.
		/// When true, sun lamp is enabled by default when subcore is installed.
		/// </summary>
		public bool hydroponicsDefaultSunLamp = true;

		/// <summary>
		/// When enabled, deep drills extract the entire connected deposit (BFS over the
		/// deep resource grid for same-def neighbors), mining outside-in to keep the
		/// remaining region connected. Disabled = vanilla 21-cell radius only.
		/// </summary>
		public bool deepDrillFullDepositEnabled = true;

		/// <summary>
		/// If true, full-deposit extraction applies only to drills with an installed
		/// subcore. If false, it applies to every deep drill on the map.
		/// </summary>
		public bool deepDrillFullDepositAutomatedOnly = true;

		/// <summary>
		/// Global toggle for Comms Console enhanced features.
		/// When enabled: attracts 50% more orbital traders.
		/// </summary>
		public bool commsConsoleFeaturesEnabled = true;

		/// <summary>
		/// Average days between bonus orbital traders from automated Comms Console.
		/// Vanilla orbital traders appear roughly every 15 days.
		/// Default 15 means ~100% more traders (doubled frequency).
		/// Range: 3-30 days.
		/// </summary>
		public float commsConsoleBonusTraderDays = 15f;

		/// <summary>
		/// Global toggle for Orbital Trade Beacon enhanced features.
		/// When enabled: doubles range and ignores walls.
		/// </summary>
		public bool orbitalTradeBeaconFeaturesEnabled = true;

		/// <summary>
		/// Global toggle for Sleep Accelerator enhanced features.
		/// When enabled: sleeping provides lucid dreams that satisfy recreation
		/// and boost mood without gaining tolerance.
		/// </summary>
		public bool sleepAcceleratorFeaturesEnabled = true;

		/// <summary>
		/// Global toggle for Piloting Console enhanced features (Odyssey DLC).
		/// When enabled: autopilot launch and mishap intervention.
		/// </summary>
		public bool pilotConsoleFeaturesEnabled = true;

		/// <summary>
		/// Chance for the subcore AI to intervene and prevent a landing mishap (0.0 to 1.0).
		/// Default is 50%. Only applies to assisted (ritual) launches, not autopilot.
		/// </summary>
		public float pilotConsoleInterventionChance = 0.5f;

		/// <summary>
		/// Global toggle for advanced filtering on automated nutrient paste buildings.
		/// When enabled: vanilla NPDs and VNPE feeders with the per-building toggle on
		/// will strip CompIngredients from dispensed meals so pawns don't get mood penalties
		/// for ingredient sources, and meals stack regardless of origin.
		/// </summary>
		public bool advancedFilteringEnabled = true;

		/// <summary>
		/// When enabled, automated Nutrient Paste Dispensers pull feedstock from any
		/// adjacent storage (stockpile, shelf, etc.), not just specialized hoppers.
		/// </summary>
		public bool npdAnyStorageInputEnabled = true;

		/// <summary>
		/// Global toggle for VNPE Nutrient Paste Grinder features.
		/// When enabled: hoppers connected to the grinder are refrigerated.
		/// </summary>
		public bool vnpeGrinderFeaturesEnabled = true;

		/// <summary>
		/// Global toggle for VNPE Nutrient Paste Feeder features.
		/// When enabled: output meals are refrigerated and smart dispense is active.
		/// </summary>
		public bool vnpeFeederFeaturesEnabled = true;

		/// <summary>
		/// Global toggle for Polux Tree wastepack consumption features.
		/// When enabled: Polux trees preserve and slowly consume nearby wastepacks.
		/// </summary>
		public bool poluxTreeFeaturesEnabled = true;

		/// <summary>
		/// Moisture pump work speed multiplier (1x = vanilla, up to 10x faster).
		/// </summary>
		public float moisturePumpSpeedMultiplier = 2f;

		/// <summary>
		/// Allow multiple automated research benches to work simultaneously.
		/// Default is false (only one bench works at a time to prevent stacking).
		/// </summary>
		public bool allowMultipleResearchBenches = false;

		// ============================================
		// HIGH-RISK PATCH TOGGLES
		// These patches modify core game systems and may conflict with other mods.
		// Disable if you experience issues.
		// ============================================

		/// <summary>
		/// Global toggle for Growth Vat embryo enhancement patches.
		/// When enabled: band nodes can provide lessons to embryos.
		/// HIGH RISK: Replaces vanilla EmbryoBirth logic.
		/// </summary>
		public bool growthVatEmbryoPatchEnabled = true;

		/// <summary>
		/// Global toggle for Vitals Monitor surgery guarantee patches.
		/// When enabled: automated vitals monitors guarantee surgery success.
		/// MEDIUM RISK: Modifies surgery failure checks.
		/// </summary>
		public bool vitalsMonitorSurgeryPatchEnabled = true;

		/// <summary>
		/// Global toggle for Biosculpter tuning bypass patches.
		/// When enabled: automated biosculpters skip tuning nutrition requirement.
		/// MEDIUM RISK: Modifies biosculpter state logic.
		/// </summary>
		public bool biosculpterTuningPatchEnabled = true;

		/// <summary>
		/// Global toggle for Cooler inverter mode patches.
		/// When enabled: automated coolers can heat when room is too cold.
		/// LOW RISK: Modifies cooler tick behavior.
		/// </summary>
		public bool coolerInverterPatchEnabled = true;

		/// <summary>
		/// When enabled, subcores cannot be removed once installed.
		/// This makes the installation permanent (destroyed with the building).
		/// </summary>
		public bool permanentSubcoreInstallation = false;

		/// <summary>
		/// When enabled, adds basic subcore automation to all powered flickable buildings
		/// that don't have explicit automation defined. Provides remote flick control.
		/// Generators also get backup power controls.
		/// Requires game restart to take effect.
		/// </summary>
		public bool fallbackAutomationEnabled = false;

		/// <summary>
		/// When enabled, adds turret enhancements to all Building_TurretGun turrets
		/// that don't have explicit automation defined. Excludes decoy turrets.
		/// Requires game restart to take effect.
		/// </summary>
		public bool fallbackTurretAutomationEnabled = false;

		/// <summary>
		/// When enabled, uses crafting materials instead of subcores for installation.
		/// Automatically enabled when Biotech DLC is not installed.
		/// Basic: 2 components + 50 steel
		/// Regular: 2 components + 1 advanced component + 50 steel  
		/// High: 2 advanced components + 50 plasteel
		/// Research requirement: Microelectronics (instead of Basic Mechtech)
		/// </summary>
		public bool noBiotechFallbackMode = false;

		/// <summary>
		/// When enabled, targeting a specific mineral in the scanner takes longer
		/// <summary>
		/// Per-machine settings keyed by defName.
		/// </summary>
		public Dictionary<string, MachineSettings> machineSettings = new Dictionary<string, MachineSettings>();

		/// <summary>
		/// Gets or creates settings for a specific machine.
		/// </summary>
		public MachineSettings GetSettings(string defName)
		{
			if (!machineSettings.TryGetValue(defName, out var settings))
			{
				settings = new MachineSettings();
				machineSettings[defName] = settings;
			}
			return settings;
		}

		/// <summary>
		/// Checks if automation is enabled for a machine.
		/// </summary>
		public bool IsAutomationEnabled(string defName)
		{
			if (machineSettings.TryGetValue(defName, out var settings))
			{
				return settings.enabled;
			}
			return true; // Default enabled
		}

		/// <summary>
		/// Gets the efficiency override for a machine, or -1 if using default.
		/// </summary>
		public float GetEfficiencyOverride(string defName)
		{
			if (machineSettings.TryGetValue(defName, out var settings))
			{
				return settings.efficiency;
			}
			return -1f; // Use default
		}

		/// <summary>
		/// Checks if friendly fire prevention is enabled for a turret.
		/// Now uses global tier-based setting.
		/// </summary>
		public bool IsFriendlyFirePreventionEnabled(string defName)
		{
			return turretFriendlyFirePrevention;
		}

		/// <summary>
		/// Gets the accuracy bonus for a turret based on its subcore tier.
		/// </summary>
		public float GetTurretAccuracyByTier(string subcoreDef)
		{
			switch (subcoreDef)
			{
				case "SubcoreBasic": return turretBasicAccuracy;
				case "SubcoreRegular": return turretRegularAccuracy;
				case "SubcoreHigh": return turretHighAccuracy;
				default: return turretBasicAccuracy;
			}
		}

		/// <summary>
		/// Gets the warmup reduction for a turret based on its subcore tier.
		/// </summary>
		public float GetTurretWarmupByTier(string subcoreDef)
		{
			switch (subcoreDef)
			{
				case "SubcoreBasic": return turretBasicWarmup;
				case "SubcoreRegular": return turretRegularWarmup;
				case "SubcoreHigh": return turretHighWarmup;
				default: return turretBasicWarmup;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref mechChargerPatchesEnabled, "mechChargerPatchesEnabled", true);
			Scribe_Values.Look(ref turretPatchesEnabled, "turretPatchesEnabled", true);
			// Tier-based turret settings
			Scribe_Values.Look(ref turretBasicAccuracy, "turretBasicAccuracy", 0.10f);
			Scribe_Values.Look(ref turretBasicWarmup, "turretBasicWarmup", 0.10f);
			Scribe_Values.Look(ref turretRegularAccuracy, "turretRegularAccuracy", 0.15f);
			Scribe_Values.Look(ref turretRegularWarmup, "turretRegularWarmup", 0.10f);
			Scribe_Values.Look(ref turretHighAccuracy, "turretHighAccuracy", 0.20f);
			Scribe_Values.Look(ref turretHighWarmup, "turretHighWarmup", 0.15f);
			Scribe_Values.Look(ref turretFriendlyFirePrevention, "turretFriendlyFirePrevention", true);
			Scribe_Values.Look(ref backupPowerEnabled, "backupPowerEnabled", true);
			Scribe_Values.Look(ref backupPowerUpdateInterval, "backupPowerUpdateInterval", 60);
			Scribe_Values.Look(ref backupPowerMinimumOnTime, "backupPowerMinimumOnTime", 300);
			Scribe_Values.Look(ref toxifierWastepackEnabled, "toxifierWastepackEnabled", true);
			Scribe_Values.Look(ref geneExtractorFeaturesEnabled, "geneExtractorFeaturesEnabled", true);
			Scribe_Values.Look(ref softscannerFeaturesEnabled, "softscannerFeaturesEnabled", true);
			Scribe_Values.Look(ref ripscannerFeaturesEnabled, "ripscannerFeaturesEnabled", true);
			Scribe_Values.Look(ref ripscannerOrganCount, "ripscannerOrganCount", 1);
			Scribe_Values.Look(ref scannerUISortingEnabled, "scannerUISortingEnabled", true);
			Scribe_Values.Look(ref mechSharedUftCraftingEnabled, "mechSharedUftCraftingEnabled", true);
			Scribe_Values.Look(ref mechBoosterFeaturesEnabled, "mechBoosterFeaturesEnabled", true);
			Scribe_Values.Look(ref mechBoosterCombatBonus, "mechBoosterCombatBonus", 0.15f);
			Scribe_Values.Look(ref hydroponicsFeaturesEnabled, "hydroponicsFeaturesEnabled", true);
			Scribe_Values.Look(ref hydroponicsDefaultSunLamp, "hydroponicsDefaultSunLamp", true);
			Scribe_Values.Look(ref deepDrillFullDepositEnabled, "deepDrillFullDepositEnabled", true);
			Scribe_Values.Look(ref deepDrillFullDepositAutomatedOnly, "deepDrillFullDepositAutomatedOnly", true);
			Scribe_Values.Look(ref commsConsoleFeaturesEnabled, "commsConsoleFeaturesEnabled", true);
			Scribe_Values.Look(ref commsConsoleBonusTraderDays, "commsConsoleBonusTraderDays", 15f);
			Scribe_Values.Look(ref orbitalTradeBeaconFeaturesEnabled, "orbitalTradeBeaconFeaturesEnabled", true);
			Scribe_Values.Look(ref sleepAcceleratorFeaturesEnabled, "sleepAcceleratorFeaturesEnabled", true);
			Scribe_Values.Look(ref pilotConsoleFeaturesEnabled, "pilotConsoleFeaturesEnabled", true);
			Scribe_Values.Look(ref pilotConsoleInterventionChance, "pilotConsoleInterventionChance", 0.5f);
			Scribe_Values.Look(ref advancedFilteringEnabled, "advancedFilteringEnabled", true);
			Scribe_Values.Look(ref npdAnyStorageInputEnabled, "npdAnyStorageInputEnabled", true);
			Scribe_Values.Look(ref vnpeGrinderFeaturesEnabled, "vnpeGrinderFeaturesEnabled", true);
			Scribe_Values.Look(ref vnpeFeederFeaturesEnabled, "vnpeFeederFeaturesEnabled", true);
			Scribe_Values.Look(ref poluxTreeFeaturesEnabled, "poluxTreeFeaturesEnabled", true);
			Scribe_Values.Look(ref moisturePumpSpeedMultiplier, "moisturePumpSpeedMultiplier", 2f);
			Scribe_Values.Look(ref allowMultipleResearchBenches, "allowMultipleResearchBenches", false);
			Scribe_Values.Look(ref permanentSubcoreInstallation, "permanentSubcoreInstallation", false);
			// High-risk patch toggles
			Scribe_Values.Look(ref growthVatEmbryoPatchEnabled, "growthVatEmbryoPatchEnabled", true);
			Scribe_Values.Look(ref vitalsMonitorSurgeryPatchEnabled, "vitalsMonitorSurgeryPatchEnabled", true);
			Scribe_Values.Look(ref biosculpterTuningPatchEnabled, "biosculpterTuningPatchEnabled", true);
			Scribe_Values.Look(ref coolerInverterPatchEnabled, "coolerInverterPatchEnabled", true);
			Scribe_Values.Look(ref fallbackAutomationEnabled, "fallbackAutomationEnabled", false);
			Scribe_Values.Look(ref fallbackTurretAutomationEnabled, "fallbackTurretAutomationEnabled", false);
			Scribe_Values.Look(ref noBiotechFallbackMode, "noBiotechFallbackMode", false);
			Scribe_Collections.Look(ref machineSettings, "machineSettings", LookMode.Value, LookMode.Deep);
			
			// Ensure dictionary is not null after loading
			if (machineSettings == null)
			{
				machineSettings = new Dictionary<string, MachineSettings>();
			}
		}
	}
}
