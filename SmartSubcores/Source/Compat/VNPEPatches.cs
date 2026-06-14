using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Compat
{
	/// <summary>
	/// Harmony patches for Vanilla Nutrient Paste Expanded integration.
	/// Provides hopper refrigeration for Grinder and smart dispense for Feeder.
	/// Uses temperature-based refrigeration like vanilla NPD.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class VNPEPatches
	{
		private static bool _vnpeLoaded;

		// Cached reflection - initialized once
		private static Type _compResourceType;
		private static Type _compRegisterIngredientsType;
		private static Type _buildingNutrientGrinderType;
		private static Type _compRegisterToGrinderType;
		private static PropertyInfo _pipeNetProperty;
		private static FieldInfo _storagesField;
		private static FieldInfo _ingredientsField;
		private static FieldInfo _cachedHoppersField;
		private static bool _reflectionInitialized;

		// ThreadStatic context: when non-null, CompIngredients.RegisterIngredient is being
		// called from inside a filtering grinder's TryProducePaste, so registration is skipped.
		[System.ThreadStatic] private static bool _suppressIngredientRegistration;

		// Cache of refrigerated cells (per map) - same approach as vanilla NPD
		private static readonly Dictionary<int, HashSet<IntVec3>> _refrigeratedCells = new Dictionary<int, HashSet<IntVec3>>();

		// Track which buildings have registered their cells (avoid re-registering every tick)
		private static readonly HashSet<int> _registeredGrinders = new HashSet<int>();
		private static readonly HashSet<int> _registeredFeeders = new HashSet<int>();

		// Cache for hopper cells around grinders
		private static readonly Dictionary<int, List<IntVec3>> _hopperCellCache = new Dictionary<int, List<IntVec3>>();
		private static readonly Dictionary<int, int> _hopperCacheExpiry = new Dictionary<int, int>();
		private const int HopperCacheLifetime = 2500;

		// Cache for feeder output cells
		private static readonly Dictionary<int, List<IntVec3>> _feederOutputCache = new Dictionary<int, List<IntVec3>>();

		// Cache for ingredient conflict check (avoid creating test meal every frame)
		private static readonly Dictionary<int, bool> _ingredientConflictCache = new Dictionary<int, bool>();
		private static readonly Dictionary<int, int> _ingredientConflictExpiry = new Dictionary<int, int>();
		private const int IngredientConflictCacheLifetime = 60; // Check every second

		static VNPEPatches()
		{
			_vnpeLoaded = ModsConfig.IsActive("VanillaExpanded.VNutrientE");
			if (!_vnpeLoaded)
				return;

			try
			{
				InitializeReflection();

				var harmony = new Harmony("SubcoreAutomation.VNPEPatches");

				// Patch GenTemperature.GetTemperatureForCell for refrigeration
				harmony.Patch(
					AccessTools.Method(typeof(GenTemperature), "GetTemperatureForCell", new[] { typeof(IntVec3), typeof(Map) }),
					prefix: new HarmonyMethod(typeof(VNPEPatches), nameof(GetTemperatureForCell_Prefix))
				);

				// Patch grinder paste production to gate ingredient registration on the
				// per-grinder advanced filtering toggle. Uses a ThreadStatic context so
				// CompIngredients.RegisterIngredient only skips when called from a grinder.
				if (_buildingNutrientGrinderType != null)
				{
					var tryProducePaste = AccessTools.Method(_buildingNutrientGrinderType, "TryProducePaste");
					if (tryProducePaste != null)
					{
						harmony.Patch(tryProducePaste,
							prefix: new HarmonyMethod(typeof(VNPEPatches), nameof(TryProducePaste_Prefix)),
							postfix: new HarmonyMethod(typeof(VNPEPatches), nameof(TryProducePaste_Postfix)));
					}
					else
					{
						Log.Error("[SubcoreAutomation] VNPE patches BROKEN: Building_NutrientGrinder.TryProducePaste not found!");
					}
				}

				var registerIngredient = AccessTools.Method(typeof(CompIngredients), nameof(CompIngredients.RegisterIngredient));
				if (registerIngredient != null)
				{
					harmony.Patch(registerIngredient, prefix: new HarmonyMethod(typeof(VNPEPatches), nameof(RegisterIngredient_Prefix)));
				}
				else
				{
					Log.Error("[SubcoreAutomation] VNPE patches BROKEN: CompIngredients.RegisterIngredient not found!");
				}

				Log.Message("[SubcoreAutomation] VNPE integration patches applied.");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply VNPE patches: {ex}");
			}
		}

		private static void InitializeReflection()
		{
			if (_reflectionInitialized)
				return;

			_compResourceType = AccessTools.TypeByName("PipeSystem.CompResource");
			_compRegisterIngredientsType = AccessTools.TypeByName("VNPE.CompRegisterIngredients");
			_buildingNutrientGrinderType = AccessTools.TypeByName("VNPE.Building_NutrientGrinder");
			_compRegisterToGrinderType = AccessTools.TypeByName("VNPE.CompRegisterToGrinder");

			if (_compResourceType != null)
			{
				_pipeNetProperty = AccessTools.Property(_compResourceType, "PipeNet");
			}

			if (_buildingNutrientGrinderType != null)
			{
				_cachedHoppersField = AccessTools.Field(_buildingNutrientGrinderType, "cachedHoppers");
			}

			_ingredientsField = AccessTools.Field(typeof(CompIngredients), "ingredients");

			_reflectionInitialized = true;
		}

		#region Temperature Refrigeration

		/// <summary>
		/// Prefix that returns freezing temperature for refrigerated cells.
		/// O(1) dictionary + hashset lookup.
		/// </summary>
		public static bool GetTemperatureForCell_Prefix(IntVec3 c, Map map, ref float __result)
		{
			if (map == null)
				return true;

			if (_refrigeratedCells.TryGetValue(map.uniqueID, out HashSet<IntVec3> cells) && cells.Contains(c))
			{
				__result = -10f;
				return false;
			}

			return true;
		}

		private static void RegisterRefrigeratedCells(Map map, IEnumerable<IntVec3> cells)
		{
			if (map == null)
				return;

			int mapId = map.uniqueID;
			if (!_refrigeratedCells.TryGetValue(mapId, out HashSet<IntVec3> cellSet))
			{
				cellSet = new HashSet<IntVec3>();
				_refrigeratedCells[mapId] = cellSet;
			}

			foreach (IntVec3 cell in cells)
			{
				cellSet.Add(cell);
			}
		}

		private static void UnregisterRefrigeratedCells(Map map, IEnumerable<IntVec3> cells)
		{
			if (map == null)
				return;

			int mapId = map.uniqueID;
			if (!_refrigeratedCells.TryGetValue(mapId, out HashSet<IntVec3> cellSet))
				return;

			foreach (IntVec3 cell in cells)
			{
				cellSet.Remove(cell);
			}
		}

		#endregion

		#region Grinder Automation

		public static bool HandleGrinderAutomation(Thing building, CompSubcoreAutomationBase comp, float speedFactor)
		{
			if (!SubcoreAutomationMod.Settings.vnpeGrinderFeaturesEnabled)
				return false;

			if (!comp.SubcoreInstalled)
				return false;

			var powerComp = building.TryGetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return false;

			// Only register cells once per building (until cache expires)
			int buildingId = building.thingIDNumber;
			if (!_registeredGrinders.Contains(buildingId))
			{
				List<IntVec3> hopperCells = GetHopperCells(building);
				if (hopperCells != null && hopperCells.Count > 0)
				{
					RegisterRefrigeratedCells(building.Map, hopperCells);
					_registeredGrinders.Add(buildingId);
				}
			}

			return true;
		}

		public static void UnregisterGrinder(Thing building)
		{
			int buildingId = building.thingIDNumber;
			if (_registeredGrinders.Remove(buildingId))
			{
				List<IntVec3> hopperCells = GetHopperCells(building);
				if (hopperCells != null)
				{
					UnregisterRefrigeratedCells(building.Map, hopperCells);
				}
			}
		}

		private static List<IntVec3> GetHopperCells(Thing building)
		{
			int buildingId = building.thingIDNumber;
			int currentTick = GenTicks.TicksGame;

			if (_hopperCellCache.TryGetValue(buildingId, out List<IntVec3> cached))
			{
				if (_hopperCacheExpiry.TryGetValue(buildingId, out int expiry) && currentTick < expiry)
				{
					return cached;
				}
				// Cache expired - need to re-register
				_registeredGrinders.Remove(buildingId);
			}

			List<IntVec3> hopperCells = new List<IntVec3>();
			Map map = building.Map;

			if (map != null)
			{
				// Try to get hoppers from the grinder's cachedHoppers field (VNPE uses custom hoppers)
				if (_cachedHoppersField != null && _buildingNutrientGrinderType != null &&
					_buildingNutrientGrinderType.IsInstanceOfType(building))
				{
					var cachedHoppers = _cachedHoppersField.GetValue(building) as System.Collections.IList;
					if (cachedHoppers != null)
					{
						foreach (var hopper in cachedHoppers)
						{
							if (hopper is Thing hopperThing && hopperThing.Spawned)
							{
								hopperCells.Add(hopperThing.Position);
							}
						}
					}
				}
				
				// Fallback: check adjacent cells for buildings with CompRegisterToGrinder or isHopper
				if (hopperCells.Count == 0)
				{
					foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(building))
					{
						if (!cell.InBounds(map))
							continue;

						Building hopper = cell.GetFirstBuilding(map);
						if (hopper == null)
							continue;

						// Check for VNPE custom hopper (CompRegisterToGrinder)
						if (_compRegisterToGrinderType != null)
						{
							var twc = hopper as ThingWithComps;
							if (twc != null)
							{
								foreach (var comp in twc.AllComps)
								{
									if (_compRegisterToGrinderType.IsInstanceOfType(comp))
									{
										hopperCells.Add(cell);
										break;
									}
								}
							}
						}
						
						// Also check vanilla hoppers
						if (!hopperCells.Contains(cell) && 
							hopper.def.building != null && hopper.def.building.isHopper)
						{
							hopperCells.Add(cell);
						}
					}
				}
			}

			_hopperCellCache[buildingId] = hopperCells;
			_hopperCacheExpiry[buildingId] = currentTick + HopperCacheLifetime;
			return hopperCells;
		}

		#endregion

		#region Feeder Automation

		public static bool HandleFeederAutomation(Thing building, CompSubcoreAutomationBase comp, float speedFactor)
		{
			if (!SubcoreAutomationMod.Settings.vnpeFeederFeaturesEnabled)
				return false;

			if (!comp.SubcoreInstalled)
				return false;

			var powerComp = building.TryGetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return false;

			// Only register cells once per building
			int buildingId = building.thingIDNumber;
			if (!_registeredFeeders.Contains(buildingId))
			{
				List<IntVec3> outputCells = GetFeederOutputCells(building);
				if (outputCells != null && outputCells.Count > 0)
				{
					RegisterRefrigeratedCells(building.Map, outputCells);
					_registeredFeeders.Add(buildingId);
				}
			}

			return true;
		}

		public static void UnregisterFeeder(Thing building)
		{
			int buildingId = building.thingIDNumber;
			if (_registeredFeeders.Remove(buildingId))
			{
				List<IntVec3> outputCells = GetFeederOutputCells(building);
				if (outputCells != null)
				{
					UnregisterRefrigeratedCells(building.Map, outputCells);
				}
			}
			_feederOutputCache.Remove(buildingId);
			_ingredientConflictCache.Remove(buildingId);
			_ingredientConflictExpiry.Remove(buildingId);
		}

		private static List<IntVec3> GetFeederOutputCells(Thing building)
		{
			int buildingId = building.thingIDNumber;

			if (_feederOutputCache.TryGetValue(buildingId, out List<IntVec3> cached))
				return cached;

			List<IntVec3> outputCells = new List<IntVec3>();
			Map map = building.Map;

			if (map != null)
			{
				// The Feeder is a storage building - refrigerate its occupied cells
				// For a 1x1 building, this is just the building's position
				foreach (IntVec3 cell in building.OccupiedRect())
				{
					if (cell.InBounds(map))
						outputCells.Add(cell);
				}
			}

			_feederOutputCache[buildingId] = outputCells;
			return outputCells;
		}

		/// <summary>
		/// Checks if there are meals on the output that wouldn't stack with new dispensed meals.
		/// Result is cached for 60 ticks to avoid creating test meals every frame.
		/// </summary>
		public static bool HasIngredientConflict(Thing feeder)
		{
			int buildingId = feeder.thingIDNumber;
			int currentTick = GenTicks.TicksGame;

			// Check cache
			if (_ingredientConflictCache.TryGetValue(buildingId, out bool cachedResult))
			{
				if (_ingredientConflictExpiry.TryGetValue(buildingId, out int expiry) && currentTick < expiry)
				{
					return cachedResult;
				}
			}

			// Calculate and cache
			bool result = CheckIngredientConflictInternal(feeder);
			_ingredientConflictCache[buildingId] = result;
			_ingredientConflictExpiry[buildingId] = currentTick + IngredientConflictCacheLifetime;
			return result;
		}

		private static bool CheckIngredientConflictInternal(Thing feeder)
		{
			Map map = feeder.Map;
			if (map == null)
				return false;

			IntVec3 outputCell = feeder.InteractionCell;
			if (!outputCell.InBounds(map))
				return false;

			// Find existing meal on output cell
			Thing existingMeal = null;
			List<Thing> things = outputCell.GetThingList(map);
			for (int i = 0; i < things.Count; i++)
			{
				Thing thing = things[i];
				if (thing.def.category == ThingCategory.Item && thing.def.IsNutritionGivingIngestible)
				{
					existingMeal = thing;
					break;
				}
			}

			if (existingMeal == null)
				return false;

			Thing testMeal = TryCreateTestMeal(feeder);
			if (testMeal == null)
				return false;

			return !existingMeal.CanStackWith(testMeal);
		}

		private static Thing TryCreateTestMeal(Thing feeder)
		{
			try
			{
				if (_compResourceType == null || _pipeNetProperty == null)
					return null;

				// Find CompResource on feeder
				ThingComp compResource = null;
				if (feeder is ThingWithComps twc)
				{
					for (int i = 0; i < twc.AllComps.Count; i++)
					{
						if (_compResourceType.IsInstanceOfType(twc.AllComps[i]))
						{
							compResource = twc.AllComps[i];
							break;
						}
					}
				}

				if (compResource == null)
					return null;

				object pipeNet = _pipeNetProperty.GetValue(compResource);
				if (pipeNet == null)
					return null;

				// Cache storages field lookup
				if (_storagesField == null)
				{
					_storagesField = AccessTools.Field(pipeNet.GetType(), "storages");
					if (_storagesField == null)
						return null;
				}

				var storages = _storagesField.GetValue(pipeNet) as System.Collections.IList;
				if (storages == null || storages.Count == 0)
					return null;

				// Create test meal
				Thing testMeal = ThingMaker.MakeThing(ThingDefOf.MealNutrientPaste);
				CompIngredients testIngredients = testMeal.TryGetComp<CompIngredients>();
				if (testIngredients == null || _compRegisterIngredientsType == null || _ingredientsField == null)
					return testMeal;

				// Add ingredients from storage tanks
				for (int i = 0; i < storages.Count; i++)
				{
					if (storages[i] is ThingComp storageComp && storageComp.parent != null)
					{
						for (int j = 0; j < storageComp.parent.AllComps.Count; j++)
						{
							var comp = storageComp.parent.AllComps[j];
							if (_compRegisterIngredientsType.IsInstanceOfType(comp))
							{
								var ingredients = _ingredientsField.GetValue(comp) as List<ThingDef>;
								if (ingredients != null)
								{
									for (int k = 0; k < ingredients.Count; k++)
									{
										testIngredients.RegisterIngredient(ingredients[k]);
									}
								}
								break;
							}
						}
					}
				}

				return testMeal;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error creating test meal: {ex.Message}", 483922);
				return null;
			}
		}

		#endregion

		#region Advanced Filtering (per-grinder)

		/// <summary>
		/// Prefix on Building_NutrientGrinder.TryProducePaste. If this grinder is automated
		/// AND has the advanced-filtering toggle on AND the master setting is on, sets a
		/// ThreadStatic flag that suppresses ingredient registration during paste production.
		/// </summary>
		public static void TryProducePaste_Prefix(ThingWithComps __instance)
		{
			_suppressIngredientRegistration = false;

			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.advancedFilteringEnabled)
				return;

			var compatComp = __instance.TryGetComp<CompCompatAutomation>();
			if (compatComp == null || !compatComp.SubcoreInstalled || !compatComp.IsAutomationEnabled || !compatComp.AdvancedFilteringEnabled)
				return;

			_suppressIngredientRegistration = true;
		}

		/// <summary>
		/// Postfix on Building_NutrientGrinder.TryProducePaste. Always clears the ThreadStatic
		/// flag so it can never leak to unrelated callers.
		/// </summary>
		public static void TryProducePaste_Postfix()
		{
			_suppressIngredientRegistration = false;
		}

		/// <summary>
		/// Prefix on CompIngredients.RegisterIngredient. When the ThreadStatic flag is set
		/// (a filtering grinder's TryProducePaste is currently running), skip registration.
		/// </summary>
		public static bool RegisterIngredient_Prefix()
		{
			return !_suppressIngredientRegistration;
		}

		#endregion
	}
}
