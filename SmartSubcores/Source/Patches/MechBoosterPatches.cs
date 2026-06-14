using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Mech Booster enhancements.
	/// With a High subcore installed:
	/// - Extends mechanitor command range (booster acts as relay)
	/// - Boosts combat mech stats (accuracy, aiming, melee, dodge)
	/// </summary>
	[StaticConstructorOnStartup]
	public static class MechBoosterPatches
	{
		// Cache of boosted mech boosters per map for performance
		private static Dictionary<int, List<Thing>> _boostedBoosterCache = new Dictionary<int, List<Thing>>();
		private static int _lastCacheUpdateTick = -1;
		private const int CacheUpdateInterval = 60; // Update every second

		static MechBoosterPatches()
		{
			if (!ModsConfig.BiotechActive)
				return;

			// Apply InMechanitorCommandRange patch manually to avoid type resolution issues
			try
			{
				// Try multiple approaches to find MechanitorUtility
				var mechanitorUtilityType = AccessTools.TypeByName("RimWorld.MechanitorUtility");

				// Fallback: search in the same assembly as Pawn_MechanitorTracker
				if (mechanitorUtilityType == null)
				{
					var trackerType = AccessTools.TypeByName("RimWorld.Pawn_MechanitorTracker");
					if (trackerType != null)
					{
						mechanitorUtilityType = trackerType.Assembly.GetType("RimWorld.MechanitorUtility");
					}
				}

				// Fallback: try without namespace
				if (mechanitorUtilityType == null)
				{
					mechanitorUtilityType = AccessTools.TypeByName("MechanitorUtility");
				}

				if (mechanitorUtilityType == null)
				{
					Log.Error("[SubcoreAutomation] Mech Booster relay BROKEN: MechanitorUtility type not found!");
					return;
				}

				var targetMethod = AccessTools.Method(mechanitorUtilityType, "InMechanitorCommandRange");
				if (targetMethod == null)
				{
					var methods = mechanitorUtilityType.GetMethods(BindingFlags.Public | BindingFlags.Static);
					Log.Error($"[SubcoreAutomation] Mech Booster relay BROKEN: InMechanitorCommandRange not found! Available: {string.Join(", ", methods.Select(m => m.Name).Take(10))}");
					return;
				}

				var harmony = new Harmony("SubcoreAutomation.MechBoosterPatches.InMechanitorCommandRange");
				harmony.Patch(targetMethod,
					postfix: new HarmonyMethod(typeof(Patch_InMechanitorCommandRange), nameof(Patch_InMechanitorCommandRange.Postfix)));
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Mech Booster relay BROKEN: {ex}");
			}
		}

		/// <summary>
		/// Gets all mech boosters with subcores installed on a map.
		/// Results are cached for performance.
		/// </summary>
		public static List<Thing> GetBoostedBoostersOnMap(Map map)
		{
			if (map == null)
				return new List<Thing>();

			int currentTick = Find.TickManager.TicksGame;
			if (currentTick - _lastCacheUpdateTick > CacheUpdateInterval)
			{
				_boostedBoosterCache.Clear();
				_lastCacheUpdateTick = currentTick;
			}

			int mapId = map.uniqueID;
			if (!_boostedBoosterCache.TryGetValue(mapId, out var boosters))
			{
				boosters = new List<Thing>();

				for (int i = 0; i < MachineDefNames.AllMechBoosters.Length; i++)
				{
					var boosterDef = DefDatabase<ThingDef>.GetNamed(MachineDefNames.AllMechBoosters[i], false);
					if (boosterDef == null)
						continue;

					var allBoosters = map.listerThings.ThingsOfDef(boosterDef);
					foreach (var booster in allBoosters)
					{
						var subcoreComp = booster.TryGetComp<CompSubcoreAutomationBase>();
						if (subcoreComp == null || !subcoreComp.HasSubcoreInstalled)
							continue;
						var powerComp = booster.TryGetComp<CompPowerTrader>();
						if (powerComp != null && !powerComp.PowerOn)
							continue;
						boosters.Add(booster);
					}
				}

				_boostedBoosterCache[mapId] = boosters;
			}

			return boosters;
		}

		/// <summary>
		/// Checks if a target cell is within range of any boosted mech booster.
		/// </summary>
		public static bool IsInBoostedBoosterRange(Map map, IntVec3 target)
		{
			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
				return false;

			var boosters = GetBoostedBoostersOnMap(map);

			foreach (var booster in boosters)
			{
				float range = GetBoosterRange(booster);
				if (booster.Position.DistanceToSquared(target) <= range * range)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns the effect radius of a mech booster, read from its CompCauseHediff_AoE
		/// (e.g. vanilla MechBooster = 9.9, BfG floor booster = 18.9). Falls back to the
		/// vanilla value when the comp is missing.
		/// </summary>
		public static float GetBoosterRange(Thing booster)
		{
			var aoeComp = booster.TryGetComp<CompCauseHediff_AoE>();
			if (aoeComp != null && aoeComp.range > 0f)
				return aoeComp.range;
			return 9.9f;
		}

		/// <summary>
		/// Checks if a pawn (mech) is within range of any boosted mech booster.
		/// </summary>
		public static bool IsMechInBoostedBoosterRange(Pawn mech)
		{
			if (mech?.Map == null)
				return false;

			return IsInBoostedBoosterRange(mech.Map, mech.Position);
		}

	}

	/// <summary>
	/// Patch for Pawn_MechanitorTracker.CanControlMechs to allow control when mechanitor is away
	/// (in caravan, container, or another map) if there's a boosted mech booster on any mech's map.
	/// This enables the relay functionality - boosted boosters act as command relay stations.
	/// Applied via HarmonyPatchAll - Prepare() prevents application when Biotech isn't active.
	/// </summary>
	[HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanControlMechs), MethodType.Getter)]
	public static class Patch_CanControlMechs
	{
		public static bool Prepare()
		{
			return ModsConfig.BiotechActive;
		}

		public static void Postfix(Pawn_MechanitorTracker __instance, ref AcceptanceReport __result)
		{
			// Only intervene if vanilla already rejected control
			if (__result.Accepted)
				return;

			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
				return;

			if (__instance == null)
				return;

			Pawn mechanitor = __instance.Pawn;
			if (mechanitor == null)
				return;

			// Don't allow if dead
			if (mechanitor.Dead)
				return;

			// Don't allow if imprisoned (enemy has control)
			if (mechanitor.IsPrisoner)
				return;

			// Don't allow if in mental state (unpredictable behavior)
			if (mechanitor.InMentalState)
				return;

			// Allow remote control even when mechanitor is downed, in caravan,
			// in transport pod, on another map, etc, as long as any boosted booster
			// exists on any active map. The relay provides autonomous coordination.
			if (HasAnyBoostedBoosterAvailable())
			{
				__result = true;
			}
		}

		/// <summary>
		/// Check if there's any boosted mech booster anywhere (relay extends globally).
		/// We can't rely on ControlledPawns having valid Map values — when both
		/// mechanitor and mechs are in transport pods / caravans / world pawns,
		/// mech.Map is null and the previous per-mech-map scan returned false.
		/// </summary>
		private static bool HasAnyBoostedBoosterAvailable()
		{
			var maps = Find.Maps;
			if (maps == null)
				return false;

			for (int i = 0; i < maps.Count; i++)
			{
				var boosters = MechBoosterPatches.GetBoostedBoostersOnMap(maps[i]);
				if (boosters.Count > 0)
					return true;
			}

			return false;
		}
	}

	/// <summary>
	/// Patch for Pawn_MechanitorTracker.CanCommandTo to allow commanding through boosted boosters.
	/// This is the critical patch for relay functionality - it allows commands to targets
	/// that are within range of a boosted mech booster, even when the mechanitor is far away.
	/// </summary>
	[HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.CanCommandTo))]
	public static class Patch_CanCommandTo
	{
		public static bool Prepare()
		{
			return ModsConfig.BiotechActive;
		}

		public static void Postfix(Pawn_MechanitorTracker __instance, LocalTargetInfo target, ref bool __result)
		{
			if (__result)
				return;

			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
				return;

			var tracker = __instance;
			if (tracker == null)
				return;

			// Primary approach: use the currently viewed map
			Map targetMap = Find.CurrentMap;
			if (targetMap != null && target.Cell.InBounds(targetMap))
			{
				if (MechBoosterPatches.IsInBoostedBoosterRange(targetMap, target.Cell))
				{
					__result = true;
					return;
				}
			}

			// Fallback: check all maps where mechanitor's mechs are located
			var mechs = tracker.ControlledPawns;
			if (mechs == null)
				return;

			foreach (var mech in mechs)
			{
				if (mech?.Map == null)
					continue;

				if (!target.Cell.InBounds(mech.Map))
					continue;

				if (MechBoosterPatches.IsInBoostedBoosterRange(mech.Map, target.Cell))
				{
					__result = true;
					return;
				}
			}
		}
	}

	/// <summary>
	/// Patch for MechanitorUtility.InMechanitorCommandRange to allow cross-map commands via boosted boosters.
	/// This is the critical patch - vanilla checks if mech and mechanitor are on the same map BEFORE
	/// checking CanCommandTo, causing cross-map relay to fail.
	/// Applied manually from MechBoosterPatches static constructor to avoid type resolution issues.
	/// </summary>
	public static class Patch_InMechanitorCommandRange
	{
		public static void Postfix(Pawn mech, LocalTargetInfo target, ref bool __result)
		{
			if (__result)
				return;

			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
				return;

			if (mech?.Map == null)
				return;

			// Allow command if mech is within booster range (can receive commands via relay)
			if (MechBoosterPatches.IsMechInBoostedBoosterRange(mech))
			{
				__result = true;
				return;
			}

			// Also allow if target is within booster range
			if (target.Cell.InBounds(mech.Map) && MechBoosterPatches.IsInBoostedBoosterRange(mech.Map, target.Cell))
			{
				__result = true;
			}
		}
	}

	/// <summary>
	/// Patch for SelectionDrawer.DrawSelectionOverlays to draw command ranges when mechs are selected.
	/// This runs every frame regardless of mechanitor position, ensuring relay ranges are visible
	/// even when the mechanitor is on another map or in a caravan.
	/// Shows booster relay ranges for any selected colony mech (not just drafted ones).
	/// </summary>
	[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
	public static class Patch_DrawSelectionOverlays
	{
		// Cyan/teal color for relay-extended range
		private static readonly Color RelayRangeColor = new Color(0.4f, 0.9f, 0.9f, 1f);

		// Vanilla mechanitor command range
		private const float VanillaMechCommandRange = 24.9f;

		// Booster relay range (matches vanilla mech booster effect radius)
		private const float BoosterRange = 9.9f;

		// Cached cells for drawing
		private static List<IntVec3> _relayCells = new List<IntVec3>();
		private static HashSet<IntVec3> _relayCellsSet = new HashSet<IntVec3>();

		public static bool Prepare()
		{
			return ModsConfig.BiotechActive;
		}

		public static void Postfix()
		{
			if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechBoosterFeaturesEnabled)
				return;

			// Find any selected drafted colony mech
			Pawn selectedMech = null;
			Pawn mechanitor = null;

			foreach (object obj in Find.Selector.SelectedObjects)
			{
				if (obj is Pawn pawn && pawn.IsColonyMech && pawn.Drafted)
				{
					selectedMech = pawn;
					mechanitor = pawn.GetOverseer();
					break;
				}
			}

			if (selectedMech?.Map == null)
				return;

			// If mechanitor is null, the mech might be disconnected - still show booster ranges
			// to help player understand where they could command from if reconnected
			Pawn_MechanitorTracker tracker = mechanitor?.mechanitor;

			// Determine mechanitor's map situation
			Map mechanitorMap = mechanitor?.MapHeld;
			bool mechanitorSpawned = mechanitor?.Spawned ?? false;
			bool mechanitorOnSameMap = mechanitorMap == selectedMech.Map;
			bool mechanitorInContainer = !mechanitorSpawned && mechanitorMap != null;
			bool mechanitorOffMap = mechanitorMap == null || !mechanitorOnSameMap;

			// Check if command range is unlimited (mod compatibility)
			// Only relevant if mechanitor is on the same map
			bool rangeIsUnlimited = false;
			if (mechanitorSpawned && mechanitorOnSameMap && tracker != null)
			{
				rangeIsUnlimited = IsCommandRangeUnlimited(tracker, mechanitor);
			}

			// Draw mechanitor's command range circle if they're off-screen but on the same map
			// (vanilla handles it when on-screen via DrawCommandRadius)
			if (!rangeIsUnlimited && mechanitorSpawned &&
			    mechanitorOnSameMap && !IsOnScreen(mechanitor))
			{
				GenDraw.DrawRadiusRing(mechanitor.Position, VanillaMechCommandRange, Color.white,
					(IntVec3 c) => tracker.CanCommandTo(c));
			}

			// If range is unlimited and mechanitor is on same map, no need for relay visualization
			if (rangeIsUnlimited && mechanitorOnSameMap && mechanitorSpawned)
				return;

			// Get boosted boosters on the mech's map
			var boosters = MechBoosterPatches.GetBoostedBoostersOnMap(selectedMech.Map);
			if (boosters.Count == 0)
				return;

			// Collect all cells in relay range
			// When mechanitor is off-map, show ALL booster coverage (entire relay area is useful)
			// When mechanitor is on-map, only show areas outside their direct range
			_relayCells.Clear();
			_relayCellsSet.Clear();
			float mechRangeSquared = VanillaMechCommandRange * VanillaMechCommandRange;

			foreach (var booster in boosters)
			{
				float boosterRange = MechBoosterPatches.GetBoosterRange(booster);
				int numCells = GenRadial.NumCellsInRadius(boosterRange);
				for (int i = 0; i < numCells; i++)
				{
					IntVec3 cell = booster.Position + GenRadial.RadialPattern[i];
					if (!cell.InBounds(selectedMech.Map))
						continue;

					// Skip cells already in mechanitor's direct range (only if mechanitor is spawned on same map)
					if (mechanitorSpawned && mechanitorOnSameMap && !rangeIsUnlimited &&
					    mechanitor.Position.DistanceToSquared(cell) <= mechRangeSquared)
						continue;

					// Skip cells already added
					if (_relayCellsSet.Contains(cell))
						continue;

					_relayCells.Add(cell);
					_relayCellsSet.Add(cell);
				}
			}

			// Draw field edges around relay cells
			if (_relayCells.Count > 0)
			{
				GenDraw.DrawFieldEdges(_relayCells, RelayRangeColor);
			}
		}

		/// <summary>
		/// Checks if command range is effectively unlimited (mod compatibility).
		/// Tests by checking if a very distant cell can be commanded to.
		/// </summary>
		private static bool IsCommandRangeUnlimited(Pawn_MechanitorTracker tracker, Pawn mechanitor)
		{
			if (mechanitor?.Map == null || tracker == null)
				return false;

			// Test a cell 100 tiles away from mechanitor
			IntVec3 distantCell = mechanitor.Position + new IntVec3(100, 0, 0);

			// Make sure test cell is valid
			if (!distantCell.InBounds(mechanitor.Map))
			{
				// Try opposite direction
				distantCell = mechanitor.Position - new IntVec3(100, 0, 0);
				if (!distantCell.InBounds(mechanitor.Map))
				{
					// Map is too small, assume range is limited
					return false;
				}
			}

			// If we can command to a cell 100 tiles away, range is effectively unlimited
			return tracker.CanCommandTo(distantCell);
		}

		/// <summary>
		/// Checks if a pawn is currently visible on screen.
		/// </summary>
		private static bool IsOnScreen(Pawn pawn)
		{
			if (pawn?.Map == null || !pawn.Spawned)
				return false;

			// Get the visible rect of the current camera view
			CellRect visibleRect = Find.CameraDriver.CurrentViewRect;
			return visibleRect.Contains(pawn.Position);
		}
	}

}
