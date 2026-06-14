using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Nutrient Paste Dispenser automation features:
	/// - Refrigeration of hoppers attached to automated dispensers
	/// - Advanced filtering: clears CompIngredients on dispensed meals
	///
	/// Note: Animal access to food is handled via continuous dispense mode,
	/// which places meals on the ground for any pawn to eat.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class DispenserPatches
	{
		static DispenserPatches()
		{
			ApplyPatches(new Harmony("SubcoreAutomation.DispenserPatches"));
		}

		// Cache of hopper cells that should be refrigerated (per map)
		private static Dictionary<int, HashSet<IntVec3>> _refrigeratedCells = new Dictionary<int, HashSet<IntVec3>>();

		public static void ApplyPatches(Harmony harmony)
		{
			try
			{
				// Patch GenTemperature.GetTemperatureForCell for hopper refrigeration
				var getTempMethod = AccessTools.Method(typeof(GenTemperature), "GetTemperatureForCell", new[] { typeof(IntVec3), typeof(Map) });
				if (getTempMethod != null)
					harmony.Patch(getTempMethod, prefix: new HarmonyMethod(typeof(DispenserPatches), nameof(GetTemperatureForCell_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Dispenser patches BROKEN: GenTemperature.GetTemperatureForCell not found!");

				// Patch Building_NutrientPasteDispenser.TryDispenseFood for advanced filtering
				var tryDispense = AccessTools.Method(typeof(Building_NutrientPasteDispenser), nameof(Building_NutrientPasteDispenser.TryDispenseFood));
				if (tryDispense != null)
					harmony.Patch(tryDispense, postfix: new HarmonyMethod(typeof(DispenserPatches), nameof(TryDispenseFood_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Dispenser patches BROKEN: Building_NutrientPasteDispenser.TryDispenseFood not found!");

				// Patch FindFeedInAnyHopper / HasEnoughFeedstockInHoppers so automated NPDs
				// can pull feedstock from any adjacent storage, not just hoppers.
				var findFeed = AccessTools.Method(typeof(Building_NutrientPasteDispenser), nameof(Building_NutrientPasteDispenser.FindFeedInAnyHopper));
				if (findFeed != null)
					harmony.Patch(findFeed, prefix: new HarmonyMethod(typeof(DispenserPatches), nameof(FindFeedInAnyHopper_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Dispenser patches BROKEN: Building_NutrientPasteDispenser.FindFeedInAnyHopper not found!");

				var hasEnough = AccessTools.Method(typeof(Building_NutrientPasteDispenser), nameof(Building_NutrientPasteDispenser.HasEnoughFeedstockInHoppers));
				if (hasEnough != null)
					harmony.Patch(hasEnough, prefix: new HarmonyMethod(typeof(DispenserPatches), nameof(HasEnoughFeedstockInHoppers_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Dispenser patches BROKEN: Building_NutrientPasteDispenser.HasEnoughFeedstockInHoppers not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply dispenser patches: {ex.Message}");
			}
		}

		#region Advanced Filtering

		/// <summary>
		/// Postfix that clears CompIngredients from meals dispensed by automated NPDs
		/// when the per-building advanced filtering toggle is on.
		/// </summary>
		public static void TryDispenseFood_Postfix(Building_NutrientPasteDispenser __instance, Thing __result)
		{
			if (__result == null)
				return;
			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.advancedFilteringEnabled)
				return;

			var comp = __instance.TryGetComp<CompUtilityAutomation>();
			if (comp == null || !comp.SubcoreInstalled || !comp.IsAutomationEnabled || !comp.AdvancedFilteringEnabled)
				return;

			var ingredientsComp = __result.TryGetComp<CompIngredients>();
			ingredientsComp?.ingredients?.Clear();
		}

		#endregion

		#region Any-Storage Input

		/// <summary>
		/// When the dispenser is automated (subcore + automation on) and the setting is enabled,
		/// scan adjacent cardinal cells for acceptable feedstock without requiring a hopper.
		/// </summary>
		public static bool FindFeedInAnyHopper_Prefix(Building_NutrientPasteDispenser __instance, ref Thing __result)
		{
			if (!ShouldUseAnyStorage(__instance))
				return true;

			Map map = __instance.Map;
			if (map == null)
				return true;

			foreach (IntVec3 cell in __instance.AdjCellsCardinalInBounds)
			{
				List<Thing> thingList = cell.GetThingList(map);
				for (int j = 0; j < thingList.Count; j++)
				{
					Thing thing = thingList[j];
					if (Building_NutrientPasteDispenser.IsAcceptableFeedstock(thing.def))
					{
						__result = thing;
						return false;
					}
				}
			}

			__result = null;
			return false;
		}

		/// <summary>
		/// When the dispenser is automated and the setting is enabled, sum nutrition of any
		/// acceptable feedstock in adjacent cells (no hopper required).
		/// </summary>
		public static bool HasEnoughFeedstockInHoppers_Prefix(Building_NutrientPasteDispenser __instance, ref bool __result)
		{
			if (!ShouldUseAnyStorage(__instance))
				return true;

			Map map = __instance.Map;
			if (map == null)
				return true;

			float required = __instance.def.building.nutritionCostPerDispense;
			float total = 0f;

			foreach (IntVec3 cell in __instance.AdjCellsCardinalInBounds)
			{
				List<Thing> thingList = cell.GetThingList(map);
				for (int j = 0; j < thingList.Count; j++)
				{
					Thing thing = thingList[j];
					if (!Building_NutrientPasteDispenser.IsAcceptableFeedstock(thing.def))
						continue;
					total += (float)thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition);
					if (total >= required)
					{
						__result = true;
						return false;
					}
				}
			}

			__result = false;
			return false;
		}

		private static bool ShouldUseAnyStorage(Building_NutrientPasteDispenser dispenser)
		{
			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.npdAnyStorageInputEnabled)
				return false;
			var comp = dispenser.TryGetComp<CompSubcoreAutomationBase>();
			return comp != null && comp.SubcoreInstalled && comp.IsAutomationEnabled;
		}

		#endregion

		#region Hopper Refrigeration

		/// <summary>
		/// Prefix that returns freezing temperature for hopper cells attached to automated dispensers.
		/// </summary>
		public static bool GetTemperatureForCell_Prefix(IntVec3 c, Map map, ref float __result)
		{
			if (map == null)
				return true;

			// Check if this cell is in our refrigerated cells cache
			if (_refrigeratedCells.TryGetValue(map.uniqueID, out HashSet<IntVec3> cells) && cells.Contains(c))
			{
				__result = -10f; // Freezing temperature
				return false;
			}

			return true;
		}

		/// <summary>
		/// Register hopper cells for refrigeration when a dispenser is automated.
		/// Called from CompSubcoreAutomation when NPD gets automated.
		/// </summary>
		public static void RegisterRefrigeratedHoppers(Building_NutrientPasteDispenser dispenser)
		{
			if (dispenser?.Map == null)
				return;

			int mapId = dispenser.Map.uniqueID;
			if (!_refrigeratedCells.TryGetValue(mapId, out HashSet<IntVec3> cells))
			{
				cells = new HashSet<IntVec3>();
				_refrigeratedCells[mapId] = cells;
			}

			// Find all hoppers connected to this dispenser and register their cells
			foreach (IntVec3 hopperCell in GetAdjacentHopperCells(dispenser))
			{
				cells.Add(hopperCell);
			}
		}

		/// <summary>
		/// Unregister hopper cells when dispenser loses automation.
		/// Called from CompSubcoreAutomation when subcore is removed.
		/// </summary>
		public static void UnregisterRefrigeratedHoppers(Building_NutrientPasteDispenser dispenser)
		{
			if (dispenser?.Map == null)
				return;

			int mapId = dispenser.Map.uniqueID;
			if (!_refrigeratedCells.TryGetValue(mapId, out HashSet<IntVec3> cells))
				return;

			// Remove hopper cells for this dispenser
			foreach (IntVec3 hopperCell in GetAdjacentHopperCells(dispenser))
			{
				cells.Remove(hopperCell);
			}
		}

		/// <summary>
		/// Update refrigerated cells for a dispenser (call periodically or when hoppers change).
		/// </summary>
		public static void UpdateRefrigeratedHoppers(Building_NutrientPasteDispenser dispenser, bool isAutomated)
		{
			if (isAutomated)
				RegisterRefrigeratedHoppers(dispenser);
			else
				UnregisterRefrigeratedHoppers(dispenser);
		}

		/// <summary>
		/// Get all cells occupied by hoppers adjacent to the dispenser.
		/// </summary>
		private static IEnumerable<IntVec3> GetAdjacentHopperCells(Building_NutrientPasteDispenser dispenser)
		{
			ThingDef hopperDef = ThingDefOf.Hopper;
			Map map = dispenser.Map;

			// Check cells adjacent to the dispenser
			foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(dispenser))
			{
				if (!cell.InBounds(map))
					continue;

				// Check if there's a hopper at this cell
				Building hopper = cell.GetFirstBuilding(map);
				if (hopper != null && hopper.def == hopperDef)
				{
					// Return all cells occupied by the hopper
					foreach (IntVec3 hopperCell in GenAdj.OccupiedRect(hopper))
					{
						yield return hopperCell;
					}
				}
			}
		}

		#endregion
	}
}
