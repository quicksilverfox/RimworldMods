using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Handles hydroponics basin automation features:
	/// - Automatic sowing when no plant is present
	/// - Automatic harvesting when plants are ready
	/// - Built-in sun lamp with toggle
	/// - Blocks colonist sowing/harvesting jobs on automated basins
	/// </summary>
	[StaticConstructorOnStartup]
	public static class HydroponicsPatches
	{
		// Cache of automated hydroponics cells per map - O(1) lookup for WorkGiver patches
		private static readonly Dictionary<int, HashSet<IntVec3>> _automatedCells = new Dictionary<int, HashSet<IntVec3>>();
		
		static HydroponicsPatches()
		{
			var harmony = new Harmony("SubcoreAutomation.HydroponicsPatches");

			// Patch WorkGiver_GrowerSow.JobOnCell to skip automated hydroponics
			var sowJobOnCell = AccessTools.Method(typeof(WorkGiver_GrowerSow), "JobOnCell");
			if (sowJobOnCell != null)
				harmony.Patch(sowJobOnCell, prefix: new HarmonyMethod(typeof(HydroponicsPatches), nameof(JobOnCell_Sow_Prefix)));
			else
				Log.Error("[SubcoreAutomation] Hydroponics patches BROKEN: WorkGiver_GrowerSow.JobOnCell not found!");

			// Patch WorkGiver_GrowerHarvest.HasJobOnCell to skip automated hydroponics
			var harvestHasJobOnCell = AccessTools.Method(typeof(WorkGiver_GrowerHarvest), "HasJobOnCell");
			if (harvestHasJobOnCell != null)
				harmony.Patch(harvestHasJobOnCell, prefix: new HarmonyMethod(typeof(HydroponicsPatches), nameof(HasJobOnCell_Harvest_Prefix)));
			else
				Log.Error("[SubcoreAutomation] Hydroponics patches BROKEN: WorkGiver_GrowerHarvest.HasJobOnCell not found!");
		}
		
		#region Automated Cell Cache
		
		/// <summary>
		/// Registers cells of an automated hydroponics basin.
		/// Called when subcore is installed.
		/// </summary>
		public static void RegisterAutomatedCells(Building_PlantGrower grower)
		{
			if (grower?.Map == null)
				return;
			
			int mapId = grower.Map.uniqueID;
			if (!_automatedCells.TryGetValue(mapId, out var cells))
			{
				cells = new HashSet<IntVec3>();
				_automatedCells[mapId] = cells;
			}
			
			foreach (IntVec3 cell in grower.OccupiedRect())
			{
				cells.Add(cell);
			}
		}
		
		/// <summary>
		/// Unregisters cells of a hydroponics basin.
		/// Called when subcore is removed or building despawns.
		/// </summary>
		public static void UnregisterAutomatedCells(Building_PlantGrower grower)
		{
			if (grower?.Map == null)
				return;
			
			int mapId = grower.Map.uniqueID;
			if (!_automatedCells.TryGetValue(mapId, out var cells))
				return;
			
			foreach (IntVec3 cell in grower.OccupiedRect())
			{
				cells.Remove(cell);
			}
		}

		/// <summary>
		/// O(1) check if a cell is in an automated hydroponics basin.
		/// </summary>
		private static bool IsAutomatedCell(IntVec3 c, Map map)
		{
			if (map == null)
				return false;
			
			return _automatedCells.TryGetValue(map.uniqueID, out var cells) && cells.Contains(c);
		}
		
		#endregion
		
		#region WorkGiver Patches
		
		/// <summary>
		/// Prevents colonists from sowing on automated hydroponics basins.
		/// Uses cached cell lookup for O(1) performance.
		/// </summary>
		public static bool JobOnCell_Sow_Prefix(Pawn pawn, IntVec3 c, ref Job __result)
		{
			if (!SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
				return true;
			
			if (pawn?.Map == null)
				return true;
			
			// O(1) cached lookup
			if (IsAutomatedCell(c, pawn.Map))
			{
				__result = null;
				return false;
			}
			
			return true;
		}
		
		/// <summary>
		/// Prevents colonists from harvesting on automated hydroponics basins.
		/// Uses cached cell lookup for O(1) performance.
		/// </summary>
		public static bool HasJobOnCell_Harvest_Prefix(Pawn pawn, IntVec3 c, ref bool __result)
		{
			if (!SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
				return true;
			
			if (pawn?.Map == null)
				return true;
			
			// O(1) cached lookup
			if (IsAutomatedCell(c, pawn.Map))
			{
				__result = false;
				return false;
			}
			
			return true;
		}
		
		#endregion
		// Sun lamp power consumption when active (proportional to basin size vs full sun lamp)
		// Full sun lamp: 2900W for 96 cells = 30.2W/cell
		// Basin covers 4 cells, so ~120W is proportional
		public const int SunLampPowerConsumption = 120;

		// Growing hours (matches vanilla sun lamp schedule)
		public const float GrowingStartHour = 0.25f; // 6 AM (day percent)

		/// <summary>
		/// Checks if it's currently growing hours (6 AM to midnight).
		/// </summary>
		public static bool IsGrowingHours(Map map)
		{
			if (map == null)
				return false;
			float dayPercent = GenLocalDate.DayPercent(map);
			return dayPercent >= GrowingStartHour;
		}

		/// <summary>
		/// Processes hydroponics automation for a building with subcore installed.
		/// Called from CompSubcoreAutomation.CompTick.
		/// </summary>
		public static void ProcessHydroponics(Building_PlantGrower grower, IProductionAutomation comp)
		{
			if (!SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
				return;

			if (grower?.Map == null || !grower.Spawned)
				return;

			// Check power
			var powerComp = grower.GetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return;

			// Get plant to grow
			ThingDef plantDef = grower.GetPlantDefToGrow();
			if (plantDef == null)
				return;

			// Process each cell in the grower
			foreach (IntVec3 cell in grower.OccupiedRect())
			{
				ProcessCell(grower, cell, plantDef);
			}
		}

		private static void ProcessCell(Building_PlantGrower grower, IntVec3 cell, ThingDef plantDef)
		{
			Map map = grower.Map;
			Plant existingPlant = cell.GetPlant(map);

			if (existingPlant != null)
			{
				// Check if this is the wrong plant type (designated plant changed)
				if (existingPlant.def != plantDef)
				{
					// If the wrong plant is mature enough to harvest, take the yield first.
					// Otherwise just cut it.
					if (existingPlant.def.plant.harvestedThingDef != null &&
					    existingPlant.HarvestableNow)
					{
						AutoHarvest(existingPlant, grower);
					}
					else
					{
						AutoCut(existingPlant);
					}
					AutoSow(grower, cell, plantDef);
				}
				// Harvest when plant is mature and harvestable (matches vanilla WorkGiver_GrowerHarvest logic)
				// LifeStage.Mature requires growth > 0.999f, HarvestableNow checks harvestMinGrowth
				// Using both ensures we match vanilla behavior and support modded plant classes
				else if (existingPlant.def.plant.harvestedThingDef != null &&
				         existingPlant.HarvestableNow &&
				         existingPlant.LifeStage == PlantLifeStage.Mature)
				{
					AutoHarvest(existingPlant, grower);
				}
			}
			else
			{
				// No plant - auto sow if conditions allow
				AutoSow(grower, cell, plantDef);
			}
		}

		private static void AutoHarvest(Plant plant, Building_PlantGrower grower)
		{
			int yield = plant.YieldNow();
			ThingDef harvestDef = plant.def.plant.harvestedThingDef;

			if (yield > 0 && harvestDef != null)
			{
				// Create harvested items
				Thing harvested = ThingMaker.MakeThing(harvestDef);
				harvested.stackCount = yield;

				// Spawn near the grower's interaction cell
				GenPlace.TryPlaceThing(harvested, grower.InteractionCell, grower.Map, ThingPlaceMode.Near);
			}

			// Destroy the plant
			plant.PlantCollected(null, PlantDestructionMode.Cut);
		}

		private static void AutoCut(Plant plant)
		{
			// Simply destroy the plant without harvesting (wrong type, likely not mature)
			plant.PlantCollected(null, PlantDestructionMode.Cut);
		}

		private static void AutoSow(Building_PlantGrower grower, IntVec3 cell, ThingDef plantDef)
		{
			Map map = grower.Map;

			// Check growth season (temperature)
			if (!PlantUtility.GrowthSeasonNow(cell, map, plantDef))
				return;

			// Check if anything is blocking the cell
			List<Thing> things = map.thingGrid.ThingsListAt(cell);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i].def.BlocksPlanting())
					return;
			}

			// Create and spawn plant
			Plant newPlant = (Plant)ThingMaker.MakeThing(plantDef);
			newPlant.Growth = 0.0001f; // Just sprouted
			newPlant.sown = true;

			GenSpawn.Spawn(newPlant, cell, map);
		}

		/// <summary>
		/// Gets the current power consumption for hydroponics sun lamp.
		/// </summary>
		public static int GetSunLampPower(IProductionAutomation comp)
		{
			if (!comp.HasSubcoreInstalled || !comp.SunLampEnabled)
				return 0;

			if (!SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
				return 0;

			// Only consume power during growing hours
			if (comp.Parent?.Map != null && IsGrowingHours(comp.Parent.Map))
				return SunLampPowerConsumption;

			return 0;
		}
	}
}
