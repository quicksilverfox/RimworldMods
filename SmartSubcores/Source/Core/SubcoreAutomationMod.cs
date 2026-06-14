using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Main mod class for Smart Subcores.
	/// </summary>
	public class SubcoreAutomationMod : Mod
	{
		public static SubcoreAutomationSettings Settings { get; private set; }

		/// <summary>
		/// Whether Combat Extended is loaded. Turret features are disabled when CE is present.
		/// </summary>
		public static bool CombatExtendedLoaded { get; private set; }

		/// <summary>
		/// Whether Turn It On and Off - RePowered is loaded. Power consumption logic adjusts for compatibility.
		/// </summary>
		public static bool TurnItOnAndOffLoaded { get; private set; }

		private Vector2 _scrollPosition;
		private string _searchFilter = "";

		public SubcoreAutomationMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<SubcoreAutomationSettings>();

			// Detect Combat Extended
			CombatExtendedLoaded = ModsConfig.IsActive("CETeam.CombatExtended");
			
			// Detect Turn It On and Off - RePowered
			TurnItOnAndOffLoaded = ModsConfig.IsActive("Mlie.TurnOnOffRePowered");
		}

		public override string SettingsCategory()
		{
			return "Smart Subcores";
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			// Search bar at top (outside scrollview)
			Rect searchRect = new Rect(inRect.x, inRect.y, inRect.width - 80f, 24f);
			Rect clearRect = new Rect(searchRect.xMax + 5f, inRect.y, 70f, 24f);

			_searchFilter = Widgets.TextField(searchRect, _searchFilter);
			if (string.IsNullOrEmpty(_searchFilter))
			{
				GUI.color = Color.gray;
				Text.Anchor = TextAnchor.MiddleLeft;
				Widgets.Label(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height), "Search settings...");
				Text.Anchor = TextAnchor.UpperLeft;
				GUI.color = Color.white;
			}
			if (Widgets.ButtonText(clearRect, "Clear"))
			{
				_searchFilter = "";
			}

			// Adjust content rect to account for search bar
			Rect contentRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f);

			Listing_Standard listing = new Listing_Standard();
			string filter = _searchFilter?.ToLower() ?? "";

			// Get all loaded machines from DefDatabase
			var machines = AutomatableMachineDef.AllLoaded
				.Where(m => !m.isTurret || !CombatExtendedLoaded)
				.ToList();

			// Calculate content height for scrolling
			float contentHeight = 650f;
			contentHeight += 280f; // Turret tier settings
			foreach (var machine in machines)
			{
				if (machine.isTurret)
					continue;
				contentHeight += 90f;
			}

			Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, contentHeight);

			Widgets.BeginScrollView(contentRect, ref _scrollPosition, viewRect);
			listing.Begin(viewRect);

			// Main Header
			Text.Font = GameFont.Medium;
			listing.Label("Smart Subcores Settings");
			Text.Font = GameFont.Small;
			listing.GapLine();

			// Draw each settings category
			DrawGeneralSettings(listing, filter);
			DrawBiotechSettings(listing, filter);
			DrawProductionSettings(listing, filter);
			DrawTurretSettings(listing, filter);
			DrawPowerSettings(listing, filter);
			DrawHighRiskPatchSettings(listing, filter);
			DrawCompatibilitySettings(listing, filter);
			DrawPerMachineSettings(listing, filter, machines);

			listing.End();
			Widgets.EndScrollView();
		}

				private void DrawWorkBuildingSettings(Listing_Standard listing, AutomatableMachineDef machine, MachineSettings settings)
		{
			// Binary machines (efficiency <= 0) don't have adjustable efficiency
			if (machine.defaultEfficiency <= 0f)
				return;

			// Use custom efficiency or default
			float displayEfficiency = settings.efficiency >= 0f ? settings.efficiency : machine.defaultEfficiency;

			Rect sliderRect = listing.GetRect(24f);
			Rect labelRect = sliderRect.LeftPart(0.4f);
			Rect valueRect = sliderRect.RightPart(0.58f);

			Widgets.Label(labelRect, "  Efficiency: " + displayEfficiency.ToStringPercent());
			TooltipHandler.TipRegion(sliderRect, 
				"Work speed multiplier when automated.\n\n" +
				"At 100%, the machine works at full speed without a pawn.\n" +
				"Lower values simulate slower unmanned operation.\n\n" +
				$"Default: {machine.defaultEfficiency.ToStringPercent()}");

			float newEfficiency = Widgets.HorizontalSlider(
				valueRect,
				displayEfficiency,
				0.1f,
				1.0f,
				middleAlignment: true,
				leftAlignedLabel: "10%",
				rightAlignedLabel: "100%",
				roundTo: 0.05f
			);

			// Only store if different from default (or if already customized)
			if (newEfficiency != displayEfficiency || settings.efficiency >= 0f)
			{
				settings.efficiency = newEfficiency;
			}

			// Reset to default button
			listing.Gap(2f);
			Rect resetRect = listing.GetRect(20f);
			resetRect.width = 120f;
			resetRect.x += 10f;
			if (Widgets.ButtonText(resetRect, "Reset to default"))
			{
				settings.efficiency = -1f; // Will use XML default
			}
			TooltipHandler.TipRegion(resetRect, $"Reset efficiency to default value ({machine.defaultEfficiency.ToStringPercent()})");
		}

		private void DrawTurretTierSettings(Listing_Standard listing, string tierName, 
			ref float accuracy, ref float warmup, float defaultAccuracy, float defaultWarmup)
		{
			// Accuracy slider
			Rect accSliderRect = listing.GetRect(24f);
			Rect accLabelRect = accSliderRect.LeftPart(0.4f);
			Rect accValueRect = accSliderRect.RightPart(0.58f);

			Widgets.Label(accLabelRect, "  Accuracy bonus: +" + accuracy.ToStringPercent());
			TooltipHandler.TipRegion(accSliderRect,
				$"Accuracy bonus for all turrets using {tierName} subcores.\n\nDefault: +{defaultAccuracy.ToStringPercent()}");

			float newAccuracy = Widgets.HorizontalSlider(
				accValueRect, accuracy, 0.05f, 0.50f,
				middleAlignment: true, leftAlignedLabel: "5%", rightAlignedLabel: "50%", roundTo: 0.05f);
			if (newAccuracy != accuracy)
				accuracy = newAccuracy;

			listing.Gap(2f);

			// Warmup slider
			Rect warmSliderRect = listing.GetRect(24f);
			Rect warmLabelRect = warmSliderRect.LeftPart(0.4f);
			Rect warmValueRect = warmSliderRect.RightPart(0.58f);

			Widgets.Label(warmLabelRect, "  Aim speed bonus: +" + warmup.ToStringPercent());
			TooltipHandler.TipRegion(warmSliderRect,
				$"Aiming speed bonus for all turrets using {tierName} subcores.\n\nDefault: +{defaultWarmup.ToStringPercent()}");

			float newWarmup = Widgets.HorizontalSlider(
				warmValueRect, warmup, 0.05f, 0.50f,
				middleAlignment: true, leftAlignedLabel: "5%", rightAlignedLabel: "50%", roundTo: 0.05f);
			if (newWarmup != warmup)
				warmup = newWarmup;
		}

		

		/// <summary>
		/// Gets a tooltip description for a machine based on its type and features.
		/// </summary>
		private string GetMachineTooltip(AutomatableMachineDef machine)
		{
			if (machine.isTurret)
			{
				return $"Install a subcore to enhance this turret:\n\n" +
					$"• <color=white>Accuracy bonus:</color> +{machine.defaultAccuracyBonus.ToStringPercent()} hit chance\n" +
					$"• <color=white>Aim speed:</color> +{machine.defaultWarmupReduction.ToStringPercent()} faster targeting\n" +
					$"• <color=white>Friendly fire prevention:</color> Projectiles avoid allies\n\n" +
					$"Requires: {GetSubcoreLabel(machine.subcoreDef)}\n" +
					$"(Bonuses scale with subcore tier - can be adjusted below)";
			}
			
			// Work building tooltips based on defName
			switch (machine.targetDefName)
			{
				case "BasicRecharger":
				case "StandardRecharger":
					return $"Install a subcore to enhance this mech charger:\n\n" +
						$"• <color=white>Faster charging:</color> 2x charge speed\n" +
						$"• <color=white>Auto-repair:</color> Repairs mech damage while charging\n" +
						$"• <color=white>Downed mech support:</color> Repairs incapacitated mechs\n\n" +
						$"Requires: {GetSubcoreLabel(machine.subcoreDef)}";
				
				case "DeepDrill":
					return $"Install a subcore to automate this deep drill:\n\n" +
						$"• <color=white>Unmanned operation:</color> Works without a pawn\n" +
						$"• <color=white>Efficiency:</color> {machine.defaultEfficiency.ToStringPercent()} work speed\n\n" +
						$"Requires: {GetSubcoreLabel(machine.subcoreDef)}";
				
				case "FabricationBench":
				case "BiofuelRefinery":
				case "ElectricSmelter":
					return $"Install a subcore to automate this workbench:\n\n" +
						$"• <color=white>Unmanned operation:</color> Works without a pawn\n" +
						$"• <color=white>Efficiency:</color> {machine.defaultEfficiency.ToStringPercent()} work speed\n\n" +
						$"Requires: {GetSubcoreLabel(machine.subcoreDef)}";
				
				case "GrowthVat":
					return $"Install a subcore to enhance this growth vat:\n\n" +
						$"• <color=white>Embryo birth quality:</color> 85% instead of 70%\n" +
						$"• <color=white>Passion-focused learning:</color> Targets skills with passions\n" +
						$"• <color=white>Daily learning:</color> XP gain every day instead of 3 days\n\n" +
						$"Requires: {GetSubcoreLabel(machine.subcoreDef)}";
				
				default:
					return $"Install a subcore to automate this machine:\n\n" +
						$"• <color=white>Efficiency:</color> {machine.defaultEfficiency.ToStringPercent()} work speed\n\n" +
						$"Requires: {GetSubcoreLabel(machine.subcoreDef)}";
			}
		}

		/// <summary>
		/// Gets a human-readable label for a subcore def.
		/// </summary>

		#region Category Drawing Methods

		private void DrawGeneralSettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "general", "permanent", "installation", "subcore", "scanner", "rarity", "scaling"))
				return;

			DrawCategoryHeader(listing, "General Settings");

			// Permanent installation toggle
			DrawToggle(listing, ref Settings.permanentSubcoreInstallation, "Permanent subcore installation",
				"When enabled, subcores cannot be removed once installed.\n\n" +
				"The subcore becomes a permanent part of the machine and is destroyed if the building is deconstructed.\n\n" +
				"<color=yellow>This affects all machines - existing installed subcores also become permanent.</color>");

			listing.Gap(4f);

		}

		private void DrawBiotechSettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "biotech", "mech", "charger", "gene", "extractor", "softscanner", "ripscanner", "booster"))
				return;

			DrawCategoryHeader(listing, "Biotech Features", "Features for Biotech DLC buildings. Requires game restart for changes.");

			// Mech charger toggle
			DrawToggle(listing, ref Settings.mechChargerPatchesEnabled, "Mech charger enhancements",
				"Adds faster charging (2x), auto-repair, and downed mech support to automated mech chargers.\n\n" +
				"<color=yellow>Requires game restart to apply changes.</color>\n\n" +
				"Disable if you experience errors or conflicts with other mech-related mods.");

			listing.Gap(4f);

			// Gene extractor toggle
			DrawToggle(listing, ref Settings.geneExtractorFeaturesEnabled, "Gene extractor enhancements",
				"With a High subcore installed, Gene Extractors provide:\n" +
				"• Pawn hibernation during extraction (no need degradation)\n" +
				"• Recovery time always minimum 12 days\n" +
				"• Targeted extraction prioritizes new genes not in gene banks\n\n" +
				"Disable if you experience errors or conflicts with other gene-related mods.");

			listing.Gap(4f);

			// Subcore softscanner toggle
			DrawToggle(listing, ref Settings.softscannerFeaturesEnabled, "Subcore softscanner enhancements",
				"With a Standard subcore installed, Subcore Softscanners provide:\n" +
				"• Reduced scanning sickness duration (-1 day)\n\n" +
				"Normal pawns: 4 days → 3 days\n" +
				"Mechanitors: 2 days → 1 day\n\n" +
				"Disable if you experience errors or conflicts with other mods.");

			listing.Gap(4f);

			// Subcore ripscanner toggle
			DrawToggle(listing, ref Settings.ripscannerFeaturesEnabled, "Subcore ripscanner enhancements",
				"With a High subcore installed, Subcore Ripscanners provide:\n" +
				"• Organ harvesting from subjects before death\n\n" +
				"The subcore's precision allows salvaging healthy organs during the scan.\n" +
				"Deathless pawns provide all 6 organs (they survive the procedure).\n\n" +
				"Disable if you experience errors or conflicts with other mods.");

			// Ripscanner organ count slider (conditional)
			if (Settings.ripscannerFeaturesEnabled)
			{
				listing.Gap(4f);
				int newOrganCount = (int)DrawSlider(listing, Settings.ripscannerOrganCount,
					"  Organs harvested: " + Settings.ripscannerOrganCount,
					"Number of random healthy organs to harvest from each subject.\n\n" +
					"Available organs: heart, liver, 2 kidneys, 2 lungs (6 total)\n" +
					"Only undamaged organs without disease can be harvested.\n\n" +
					"<color=yellow>Deathless pawns always provide all 6 organs regardless of this setting.</color>",
					1f, 6f, "1", "6", 1f);
				if (newOrganCount != Settings.ripscannerOrganCount)
					Settings.ripscannerOrganCount = newOrganCount;
			}

			// Scanner UI sorting toggle
			DrawToggle(listing, ref Settings.scannerUISortingEnabled, "Scanner pawn list sorting",
				"When enabled, the pawn selection menu for Subcore Scanners will:\n" +
				"• Sort pawns by category (colonists first, then slaves, then prisoners)\n" +
				"• Color-code pawn names using vanilla's color convention\n\n" +
				"This makes it easier to find specific pawns when you have many colonists.");

			listing.Gap(4f);

			// Mech shared UFT crafting toggle
			DrawToggle(listing, ref Settings.mechSharedUftCraftingEnabled, "Shared mechanoid crafting",
				"When enabled, any mechanoid capable of a recipe can pick up and finish an unfinished\n" +
				"crafting item started by another mechanoid (e.g. a second Fabricor continues work\n" +
				"that the first one started). Vanilla restricts the work to the original creator.\n\n" +
				"Disable to keep vanilla one-creator-per-item behavior.");

			listing.Gap(4f);

			// Mech booster toggle
			DrawToggle(listing, ref Settings.mechBoosterFeaturesEnabled, "Mech booster enhancements",
				"With a High subcore installed, Mech Boosters provide:\n" +
				"• Command range relay (mechanitor can command mechs near booster)\n" +
				"• Tactical relay hediff: +10 shooting accuracy, -15% aim delay,\n" +
				"  +5 melee hit/dodge for friendly mechs in range\n\n" +
				"The subcore processes tactical data and relays commands.\n\n" +
				"Disable if you experience errors or conflicts with other mods.");
		}

		private void DrawProductionSettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "production", "climate", "hydroponics", "comms", "trader", "beacon", "sleep", "pilot", "gravship", "filtering", "ingredients", "dispenser", "feeder"))
				return;

			DrawCategoryHeader(listing, "Production & Climate");

			// Advanced filtering toggle (vanilla NPD + VNPE feeder)
			DrawToggle(listing, ref Settings.advancedFilteringEnabled, "Advanced ingredient filtering",
				"With a subcore installed, automated nutrient paste dispensers (vanilla and VNPE feeders) can strip ingredient records from dispensed meals.\n\n" +
				"• Pawns no longer get ingredient-specific mood penalties (e.g. carnivore ate vegetables, ate raw meal)\n" +
				"• Meals stack regardless of which ingredients produced them\n\n" +
				"Each building has its own gizmo toggle so you can disable per-NPD/feeder.");

			listing.Gap(4f);

			// Any-storage input for automated NPD
			DrawToggle(listing, ref Settings.npdAnyStorageInputEnabled, "NPD: accept any adjacent storage",
				"When enabled, automated Nutrient Paste Dispensers pull feedstock from any adjacent cardinal cell (stockpiles, shelves, etc.), not just specialized hoppers.\n\n" +
				"Useful for compact builds where placing a hopper isn't practical.");

			listing.Gap(4f);

			// Hydroponics basin toggle
			DrawToggle(listing, ref Settings.hydroponicsFeaturesEnabled, "Hydroponics automation",
				"With a Basic subcore installed, Hydroponics Basins provide:\n" +
				"• Automatic sowing when cells are empty\n" +
				"• Automatic harvesting when crops are ready\n" +
				"• Built-in sun lamp (toggleable per basin)\n\n" +
				"Disable if you experience errors or conflicts with other mods.");

			// Hydroponics default sun lamp toggle (conditional)
			if (Settings.hydroponicsFeaturesEnabled)
			{
				listing.Gap(4f);
				DrawToggle(listing, ref Settings.hydroponicsDefaultSunLamp, "Default sun lamp on",
					"When enabled, newly automated hydroponics basins will have their built-in sun lamp turned on by default.\n\n" +
					"Individual basins can still toggle the sun lamp on/off via gizmo.", true);
			}

			listing.Gap(4f);

			// Deep drill: full deposit extraction toggle
			DrawToggle(listing, ref Settings.deepDrillFullDepositEnabled, "Deep drill: full deposit extraction",
				"When enabled, a deep drill that can reach any tile of a deep resource deposit will extract the entire connected deposit, even cells outside its normal 21-cell radius.\n\n" +
				"Mining proceeds outside-in (farthest tile first) so the remaining deposit stays connected.");

			if (Settings.deepDrillFullDepositEnabled)
			{
				listing.Gap(4f);
				DrawToggle(listing, ref Settings.deepDrillFullDepositAutomatedOnly, "  Automated drills only",
					"When enabled, full-deposit extraction applies only to drills with an installed subcore.\n\n" +
					"Disable to extend the behavior to every deep drill on the map.", true);
			}

			listing.Gap(4f);

			// Comms Console toggle
			DrawToggle(listing, ref Settings.commsConsoleFeaturesEnabled, "Comms console trader boost",
				"With a Regular subcore installed, Comms Consoles spawn bonus orbital traders.\n\n" +
				"Vanilla orbital traders arrive roughly every 15 days.\n" +
				"The bonus trader frequency is configurable below.\n\n" +
				"Multiple consoles on the same map share a 24-hour cooldown.");

			// Bonus trader frequency slider (conditional)
			if (Settings.commsConsoleFeaturesEnabled)
			{
				listing.Gap(4f);
				float newDays = DrawSlider(listing, Settings.commsConsoleBonusTraderDays,
					"  Bonus trader every: " + Settings.commsConsoleBonusTraderDays.ToString("F0") + " days",
					"Average number of days between bonus orbital traders.\n\n" +
					"Vanilla traders arrive roughly every 15 days.\n" +
					"Setting this to 15 days doubles your trader frequency.\n" +
					"Setting to 3 days gives ~6x more traders.\n\n" +
					"A 24-hour cooldown prevents multiple traders spawning in quick succession.",
					3f, 30f, "3", "30", 1f, 0.5f);
				if (newDays != Settings.commsConsoleBonusTraderDays)
					Settings.commsConsoleBonusTraderDays = newDays;
			}

			listing.Gap(4f);

			// Orbital Trade Beacon toggle
			DrawToggle(listing, ref Settings.orbitalTradeBeaconFeaturesEnabled, "Orbital trade beacon range boost",
				"With a Basic subcore installed, Orbital Trade Beacons get double range (7.9 → 15.8 tiles) and ignore walls.\n\n" +
				"Items anywhere in the extended range can be traded, regardless of room boundaries.");

			listing.Gap(4f);

			// Sleep Accelerator toggle
			DrawToggle(listing, ref Settings.sleepAcceleratorFeaturesEnabled, "Sleep accelerator lucid dreams",
				"With a High subcore installed, Sleep Accelerators provide lucid dreams.\n\n" +
				"Sleeping pawns gain recreation without building tolerance, and wake with a mood boost.\n\n" +
				"Requires Ideology DLC.");

			listing.Gap(4f);

			// Piloting Console toggle
			DrawToggle(listing, ref Settings.pilotConsoleFeaturesEnabled, "Piloting console automation",
				"With a High subcore installed, Piloting Consoles provide:\n\n" +
				"• Autopilot: Launch gravship without a pawn (150% quality)\n" +
				"• Assisted: AI may intervene to prevent landing mishaps\n" +
				"• No ritual required for autopilot launch\n\n" +
				"Requires Odyssey DLC.");

			// Intervention chance slider (conditional)
			if (Settings.pilotConsoleFeaturesEnabled)
			{
				listing.Gap(4f);
				float newChance = DrawSlider(listing, Settings.pilotConsoleInterventionChance,
					"  Mishap intervention chance: " + Settings.pilotConsoleInterventionChance.ToStringPercent(),
					"When a landing mishap is about to occur during an assisted (ritual) launch, " +
					"the subcore AI has this chance to intervene and prevent it.\n\n" +
					"Set to 0% to disable intervention (mishaps happen normally).\n" +
					"Set to 100% to always prevent mishaps.\n\n" +
					"Does not affect autopilot launches (which never have mishaps).",
					0f, 1.0f, "0%", "100%", 0.05f, 0.5f);
				if (newChance != Settings.pilotConsoleInterventionChance)
					Settings.pilotConsoleInterventionChance = newChance;
			}
		}

		private void DrawTurretSettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "turret", "combat", "accuracy", "warmup", "aim", "friendly", "fire"))
				return;

			DrawCategoryHeader(listing, "Turrets & Combat");

			// Turret toggle
			Rect turretRect = listing.GetRect(24f);
			bool turretEnabled = Settings.turretPatchesEnabled;
			if (CombatExtendedLoaded)
			{
				GUI.color = Color.gray;
				Widgets.CheckboxLabeled(turretRect, "Turret enhancements (disabled - Combat Extended detected)", ref turretEnabled);
				GUI.color = Color.white;
				turretEnabled = false;
			}
			else
			{
				Widgets.CheckboxLabeled(turretRect, "Turret enhancements", ref turretEnabled);
			}
			TooltipHandler.TipRegion(turretRect,
				"Adds accuracy bonus, faster aiming, and friendly fire prevention to automated turrets.\n\n" +
				"Bonuses are based on the subcore tier installed in the turret.\n\n" +
				"<color=yellow>Requires game restart to apply changes.</color>\n\n" +
				"Automatically disabled when Combat Extended is detected.");
			if (turretEnabled != Settings.turretPatchesEnabled && !CombatExtendedLoaded)
				Settings.turretPatchesEnabled = turretEnabled;

			// Tier-based turret settings (only show if turrets enabled and no CE)
			if (Settings.turretPatchesEnabled && !CombatExtendedLoaded)
			{
				listing.Gap(8f);

				// Basic tier
				GUI.color = new Color(0.7f, 0.85f, 1f);
				listing.Label("Basic Subcore Turrets (e.g., mini-turret)");
				GUI.color = Color.white;
				DrawTurretTierSettings(listing, "Basic",
					ref Settings.turretBasicAccuracy, ref Settings.turretBasicWarmup,
					0.10f, 0.10f);

				listing.Gap(4f);

				// Regular tier
				GUI.color = new Color(0.7f, 1f, 0.7f);
				listing.Label("Regular Subcore Turrets (e.g., autocannon, sniper)");
				GUI.color = Color.white;
				DrawTurretTierSettings(listing, "Regular",
					ref Settings.turretRegularAccuracy, ref Settings.turretRegularWarmup,
					0.15f, 0.10f);

				listing.Gap(4f);

				// High tier
				GUI.color = new Color(1f, 0.85f, 0.7f);
				listing.Label("High Subcore Turrets (e.g., rocketswarm launcher)");
				GUI.color = Color.white;
				DrawTurretTierSettings(listing, "High",
					ref Settings.turretHighAccuracy, ref Settings.turretHighWarmup,
					0.20f, 0.15f);

				listing.Gap(4f);

				// Friendly fire prevention (global)
				DrawToggle(listing, ref Settings.turretFriendlyFirePrevention, "Friendly fire prevention (all tiers)",
					"When enabled, automated turret projectiles will not hit friendly pawns or allied faction members.\n\n" +
					"The subcore identifies friendlies and adjusts trajectories to avoid them.");
			}
		}

		private void DrawPowerSettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "power", "generator", "backup", "battery", "toxifier", "wastepack", "polux", "moisture", "pump"))
				return;

			DrawCategoryHeader(listing, "Power & Generators");

			// Backup power toggle
			DrawToggle(listing, ref Settings.backupPowerEnabled, "Backup power control",
				"With a subcore installed, fuel-powered generators can automatically:\n" +
				"• Turn on when battery levels drop below threshold\n" +
				"• Turn off when batteries are sufficiently charged\n\n" +
				"This saves fuel by only running generators when needed.");

			listing.Gap(4f);

			// Toxifier wastepack toggle
			DrawToggle(listing, ref Settings.toxifierWastepackEnabled, "Toxifier wastepack production",
				"With a subcore installed, Toxifier Generators produce wastepacks instead of polluting terrain.\n\n" +
				"• Power reduced by 100W (1400W → 1300W)\n" +
				"• 1 wastepack per 3 days\n" +
				"• Wastepacks dropped on ground for haulers");

			listing.Gap(4f);

			// Polux tree toggle
			DrawToggle(listing, ref Settings.poluxTreeFeaturesEnabled, "Polux tree wastepack consumption",
				"Polux trees can preserve and consume wastepacks placed nearby.\n\n" +
				"• Wastepacks within radius won't dissolve or deteriorate\n" +
				"• Trees consume 1 wastepack every 2.5 days\n" +
				"• Ground pollution is cleaned first (vanilla behavior)\n\n" +
				"Requires Biotech DLC.");

			listing.Gap(4f);

			// Moisture pump speed slider
			float newPumpSpeed = DrawSlider(listing, Settings.moisturePumpSpeedMultiplier,
				"Moisture pump speed: " + Settings.moisturePumpSpeedMultiplier.ToString("F1") + "x",
				"Work speed multiplier for automated moisture pumps.\n\n" +
				"1x = vanilla speed (145 tiles in 60 days)\n" +
				"10x = 10 times faster\n\n" +
				"Higher values make pumps complete work faster.",
				1f, 10f, "1x", "10x", 0.5f, 0.5f);
			if (newPumpSpeed != Settings.moisturePumpSpeedMultiplier)
				Settings.moisturePumpSpeedMultiplier = newPumpSpeed;

			listing.Gap(4f);

			// Multiple research benches toggle
			DrawToggle(listing, ref Settings.allowMultipleResearchBenches, "Allow multiple automated research benches",
				"By default, only one automated research bench works at a time (~150 research/day).\n\n" +
				"Enable this to allow all automated benches to work simultaneously.\n\n" +
				"<color=yellow>Warning: Multiple benches can make research trivially fast.</color>");
		}

		private void DrawHighRiskPatchSettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "high", "risk", "patch", "growth", "vat", "embryo", "tv", "television", "joy", "vitals", "surgery", "biosculpter", "cooler"))
				return;

			DrawCategoryHeader(listing, "High-Risk Patches", null, true);
			GUI.color = Color.gray;
			listing.Label("These patches heavily modify vanilla behavior. Disable if you experience issues.");
			GUI.color = Color.white;
			listing.Gap(4f);

			// Growth vat embryo patch
			DrawToggle(listing, ref Settings.growthVatEmbryoPatchEnabled, "Growth vat embryo quality boost",
				"With a subcore installed, Growth Vats produce higher quality children from embryos.\n\n" +
				"• Embryo birth quality: 70% → 85%\n\n" +
				"<color=red>HIGH RISK:</color> Replaces vanilla EmbryoBirth method.\n" +
				"<color=yellow>Requires game restart to apply changes.</color>");

			listing.Gap(4f);

			// Vitals monitor surgery patch
			DrawToggle(listing, ref Settings.vitalsMonitorSurgeryPatchEnabled, "Guaranteed surgery success",
				"Automated Vitals Monitors guarantee surgery success for patients in adjacent beds.\n\n" +
				"<color=orange>MEDIUM RISK:</color> Modifies surgery success calculations.\n" +
				"<color=yellow>Requires game restart to apply changes.</color>");

			listing.Gap(4f);

			// Biosculpter tuning patch
			DrawToggle(listing, ref Settings.biosculpterTuningPatchEnabled, "Skip biosculpter tuning",
				"Automated Biosculpter Pods skip the nutrition requirement for tuning.\n\n" +
				"<color=orange>MEDIUM RISK:</color> Modifies biosculpter tuning behavior.\n" +
				"<color=yellow>Requires game restart to apply changes.</color>");

			listing.Gap(4f);

			// Cooler inverter patch
			DrawToggle(listing, ref Settings.coolerInverterPatchEnabled, "Cooler inverter mode",
				"Automated Coolers can heat rooms when the temperature is below the target.\n\n" +
				"<color=green>LOW RISK:</color> Adds heating capability to coolers.\n" +
				"<color=yellow>Requires game restart to apply changes.</color>");
		}

		private void DrawCompatibilitySettings(Listing_Standard listing, string filter)
		{
			if (!MatchesFilter(filter, "compatibility", "fallback", "flickable", "modded", "turret"))
				return;

			DrawCategoryHeader(listing, "Compatibility & Fallbacks");

			// Fallback automation toggle
			DrawToggle(listing, ref Settings.fallbackAutomationEnabled, "Fallback automation for all flickable buildings",
				"When enabled, adds basic subcore automation to ALL powered flickable buildings that don't have explicit automation defined.\n\n" +
				"• Remote on/off control for any flickable machine\n" +
				"• Generators also get backup power controls\n\n" +
				"<color=yellow>Requires game restart to apply changes.</color>\n\n" +
				"Use this to enable remote control for modded machines or vanilla buildings not explicitly supported.");

			listing.Gap(4f);

			// Fallback turret automation toggle
			DrawToggle(listing, ref Settings.fallbackTurretAutomationEnabled, "Fallback automation for all turrets",
				"When enabled, adds subcore automation to ALL Building_TurretGun turrets that don't have explicit automation defined.\n\n" +
				"• Accuracy bonus, warmup reduction, friendly fire prevention\n" +
				"• Automatically excludes decoy/fake turrets (0 damage)\n" +
				"• Excludes modded turrets with custom classes (e.g., SOS2 ship turrets)\n\n" +
				"<color=yellow>Requires game restart to apply changes.</color>\n\n" +
				"Use this to enable turret enhancements for modded turrets.");

			listing.Gap(4f);

			// Non-Biotech fallback mode toggle (only show if Biotech is installed)
			if (ModsConfig.BiotechActive)
			{
				DrawToggle(listing, ref Settings.noBiotechFallbackMode, "Use component materials instead of subcores",
					"When enabled, subcores are replaced with crafting materials:\n\n" +
					"• Basic: 2 components + 50 steel\n" +
					"• Standard: 2 components + 1 advanced component + 50 steel\n" +
					"• High: 2 advanced components + 50 plasteel\n\n" +
					"Research: Microelectronics (instead of Basic Mechtech)\n\n" +
					"<color=green>This mode is automatically enabled when Biotech DLC is not installed.</color>");
			}
		}

		private void DrawPerMachineSettings(Listing_Standard listing, string filter, List<AutomatableMachineDef> machines)
		{
			listing.GapLine();
			Text.Font = GameFont.Medium;
			listing.Label("Per-Machine Settings");
			Text.Font = GameFont.Small;
			listing.Gap(4f);
			GUI.color = Color.gray;
			listing.Label("Enable or disable automation for specific machines. Turret settings are in the Turrets & Combat section above.");
			GUI.color = Color.white;
			listing.Gap(8f);

			foreach (var machine in machines)
			{
				// Skip turrets - they use tier-based settings in the Turrets & Combat section
				if (machine.isTurret)
					continue;

				// Skip machines that don't match the search filter
				if (!MatchesFilter(filter, machine.label, machine.targetDefName))
					continue;

				var settings = Settings.GetSettings(machine.targetDefName);

				// Machine header with toggle
				Rect rowRect = listing.GetRect(24f);
				bool wasEnabled = settings.enabled;
				Widgets.CheckboxLabeled(rowRect, machine.label, ref settings.enabled);

				// Add tooltip for machine toggle
				string machineTooltip = GetMachineTooltip(machine);
				if (!string.IsNullOrEmpty(machineTooltip))
					TooltipHandler.TipRegion(rowRect, machineTooltip);

				// Initialize defaults when first enabled
				if (settings.enabled && !wasEnabled)
				{
					if (settings.efficiency < 0f)
						settings.efficiency = machine.defaultEfficiency;
				}

				if (settings.enabled)
				{
					listing.Gap(4f);
					DrawWorkBuildingSettings(listing, machine, settings);
				}
				else
				{
					listing.Gap(4f);
					listing.Label("  (Automation disabled - subcore can still be ejected)");
				}

				listing.GapLine();
			}
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Helper method to draw a checkbox toggle with tooltip.
		/// </summary>
		private void DrawToggle(Listing_Standard listing, ref bool setting, string label, string tooltip, bool indent = false)
		{
			Rect rect = listing.GetRect(24f);
			string displayLabel = indent ? "  " + label : label;
			Widgets.CheckboxLabeled(rect, displayLabel, ref setting);
			if (!string.IsNullOrEmpty(tooltip))
				TooltipHandler.TipRegion(rect, tooltip);
		}

		/// <summary>
		/// Helper method to draw a horizontal slider with label and tooltip.
		/// </summary>
		private float DrawSlider(Listing_Standard listing, float value, string label, string tooltip,
			float min, float max, string leftLabel, string rightLabel, float roundTo = 0.05f, float labelWidth = 0.4f)
		{
			Rect sliderRect = listing.GetRect(24f);
			Rect labelRect = sliderRect.LeftPart(labelWidth);
			Rect valueRect = sliderRect.RightPart(1f - labelWidth - 0.02f);

			Widgets.Label(labelRect, label);
			if (!string.IsNullOrEmpty(tooltip))
				TooltipHandler.TipRegion(sliderRect, tooltip);

			return Widgets.HorizontalSlider(
				valueRect, value, min, max,
				middleAlignment: true,
				leftAlignedLabel: leftLabel,
				rightAlignedLabel: rightLabel,
				roundTo: roundTo);
		}

		/// <summary>
		/// Draws a category header in the settings UI.
		/// </summary>
		private void DrawCategoryHeader(Listing_Standard listing, string label, string tooltip = null, bool isWarning = false)
		{
			listing.Gap(12f);
			Text.Font = GameFont.Medium;
			if (isWarning)
				GUI.color = new Color(1f, 0.8f, 0.3f); // Warning yellow
			Rect headerRect = listing.GetRect(26f);
			Widgets.Label(headerRect, label);
			if (tooltip != null)
				TooltipHandler.TipRegion(headerRect, tooltip);
			GUI.color = Color.white;
			Text.Font = GameFont.Small;
			listing.Gap(4f);
		}

		private string GetSubcoreLabel(string subcoreDef)
		{
			switch (subcoreDef)
			{
				case "SubcoreBasic": return "Basic subcore";
				case "SubcoreRegular": return "Regular subcore";
				case "SubcoreHigh": return "High subcore";
				default: return subcoreDef ?? "Unknown subcore";
			}
		}

		/// <summary>
		/// Gets an AutomatableMachineDef by its target defName.
		/// </summary>
		public static AutomatableMachineDef GetMachineDef(string targetDefName)
		{
			return AutomatableMachineDef.GetByTargetDefName(targetDefName);
		}

		/// <summary>
		/// Checks if any keyword matches the search filter.
		/// </summary>
		private bool MatchesFilter(string filter, params string[] keywords)
		{
			if (string.IsNullOrEmpty(filter))
				return true;
			foreach (var keyword in keywords)
			{
				if (keyword != null && keyword.ToLower().Contains(filter))
					return true;
			}
			return false;
		}

		#endregion
	}
}
