using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Patches for Orbital Trade Beacon automation.
	/// When subcore is installed:
	/// - Doubles range (7.9f → 15.8f) and ignores walls
	/// - Purchased items teleport directly instead of drop pods (works under roofs)
	/// </summary>
	[HarmonyPatch]
	public static class OrbitalTradeBeaconPatches
	{
		private const float BaseTradeRadius = 7.9f;
		private const float EnhancedTradeRadius = 15.8f; // Double the range

		// Cached effect definitions (loaded once)
		private static bool _effectsInitialized;
		private static FleckDef _flashFleck;
		private static FleckDef _psychicEffectFleck;
		private static SoundDef _teleportSound;

		private static void InitializeEffects()
		{
			if (_effectsInitialized)
				return;

			_effectsInitialized = true;

			// Try to get teleport/skip flash effects (base game has these)
			_flashFleck = DefDatabase<FleckDef>.GetNamedSilentFail("PsychicConditionCauserFlash");
			if (_flashFleck == null)
				_flashFleck = DefDatabase<FleckDef>.GetNamedSilentFail("LightningGlow");

			// Psychic effect sparkles
			_psychicEffectFleck = DefDatabase<FleckDef>.GetNamedSilentFail("PsychicConditionCauserEffect");

			// Sound - try transport pod arrival first, then shield sounds
			_teleportSound = DefDatabase<SoundDef>.GetNamedSilentFail("DropPod_Open");
			if (_teleportSound == null)
				_teleportSound = DefDatabase<SoundDef>.GetNamedSilentFail("EnergyShield_Reset");
		}

		/// <summary>
		/// Spawns teleportation visual and sound effects at the target location.
		/// </summary>
		private static void SpawnTeleportEffects(IntVec3 cell, Map map)
		{
			InitializeEffects();

			Vector3 loc = cell.ToVector3Shifted();

			// Flash effect
			if (_flashFleck != null)
			{
				FleckMaker.Static(loc, map, _flashFleck, 1.5f);
			}

			// Sparkle/shimmer effects around the area
			if (_psychicEffectFleck != null)
			{
				for (int i = 0; i < 5; i++)
				{
					Vector3 offset = new Vector3(
						Rand.Range(-0.5f, 0.5f),
						0f,
						Rand.Range(-0.5f, 0.5f)
					);
					FleckMaker.Static(loc + offset, map, _psychicEffectFleck, 0.8f);
				}
			}

			// Sound effect
			if (_teleportSound != null)
			{
				_teleportSound.PlayOneShot(new TargetInfo(cell, map));
			}
		}

		#region Teleportation Patch

		/// <summary>
		/// Patches TradeShip.GiveSoldThingToPlayer to teleport items instead of drop pods
		/// when an automated beacon exists on the map.
		/// </summary>
		[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer))]
		[HarmonyPrefix]
		public static bool GiveSoldThingToPlayer_Prefix(TradeShip __instance, Thing toGive, int countToGive, Pawn playerNegotiator)
		{
			if (!SubcoreAutomationMod.Settings.orbitalTradeBeaconFeaturesEnabled)
				return true;

			// Find an automated beacon on the map
			var automatedBeacon = FindAutomatedBeacon(__instance.Map);
			if (automatedBeacon == null)
				return true; // No automated beacon, use normal drop pods

			// Split off the item (same as vanilla)
			Thing thing = toGive.SplitOff(countToGive);
			thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, __instance);

			// Find a spawn cell near the beacon and place item (teleportation instead of drop pod)
			IntVec3 targetCell = automatedBeacon.Position;
			GenPlace.TryPlaceThing(thing, targetCell, __instance.Map, ThingPlaceMode.Near);

			// Spawn teleportation effects at the item's actual location
			if (thing.Spawned)
			{
				SpawnTeleportEffects(thing.Position, __instance.Map);
			}

			return false; // Skip original method
		}

		/// <summary>
		/// Finds an automated orbital trade beacon on the map.
		/// </summary>
		private static Building_OrbitalTradeBeacon FindAutomatedBeacon(Map map)
		{
			if (map == null)
				return null;

			foreach (var beacon in map.listerBuildings.AllBuildingsColonistOfClass<Building_OrbitalTradeBeacon>())
			{
				var subcoreComp = beacon.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp != null && subcoreComp.SubcoreInstalled)
					return beacon;
			}
			return null;
		}

				#endregion

		/// <summary>
		/// Patches TradeableCellsAround to use enhanced range and ignore walls when beacon has subcore.
		/// </summary>
		[HarmonyPatch(typeof(Building_OrbitalTradeBeacon), nameof(Building_OrbitalTradeBeacon.TradeableCellsAround))]
		[HarmonyPrefix]
		public static bool TradeableCellsAround_Prefix(IntVec3 pos, Map map, ref List<IntVec3> __result)
		{
			if (!SubcoreAutomationMod.Settings.orbitalTradeBeaconFeaturesEnabled)
				return true;

			// Find the beacon at this position
			Building_OrbitalTradeBeacon beacon = null;
			var things = map.thingGrid.ThingsListAtFast(pos);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is Building_OrbitalTradeBeacon b)
				{
					beacon = b;
					break;
				}
			}

			if (beacon == null)
				return true;

			// Check if beacon has subcore installed
			var subcoreComp = beacon.TryGetComp<CompSubcoreAutomationBase>();
			if (subcoreComp == null || !subcoreComp.SubcoreInstalled)
				return true;

			// Enhanced mode: double range and ignore walls
			__result = GetEnhancedTradeableCells(pos, map);
			return false;
		}

		/// <summary>
		/// Gets tradeable cells with enhanced radius and no wall restriction.
		/// Does not filter by Standable to avoid visual artifacts (outlines around buildings).
		/// Items can only exist on standable cells anyway, so trading works correctly.
		/// </summary>
		private static List<IntVec3> GetEnhancedTradeableCells(IntVec3 pos, Map map)
		{
			var tradeableCells = new List<IntVec3>();
			
			// Simple distance check for all cells in range (no standable filter)
			int radiusCeil = (int)System.Math.Ceiling(EnhancedTradeRadius);
			
			for (int x = pos.x - radiusCeil; x <= pos.x + radiusCeil; x++)
			{
				for (int z = pos.z - radiusCeil; z <= pos.z + radiusCeil; z++)
				{
					IntVec3 cell = new IntVec3(x, 0, z);
					
					if (!cell.InBounds(map))
						continue;
					
					if (!cell.InHorDistOf(pos, EnhancedTradeRadius))
						continue;
					
					tradeableCells.Add(cell);
				}
			}
			
			return tradeableCells;
		}

		/// <summary>
		/// Checks if a beacon at the given position has enhanced range.
		/// Used by PlaceWorker to show correct radius preview.
		/// </summary>
		public static bool HasEnhancedRange(IntVec3 pos, Map map)
		{
			if (!SubcoreAutomationMod.Settings.orbitalTradeBeaconFeaturesEnabled)
				return false;

			var things = map.thingGrid.ThingsListAtFast(pos);
			for (int i = 0; i < things.Count; i++)
			{
				if (things[i] is Building_OrbitalTradeBeacon beacon)
				{
					var subcoreComp = beacon.TryGetComp<CompSubcoreAutomationBase>();
					return subcoreComp != null && subcoreComp.SubcoreInstalled;
				}
			}
			return false;
		}

		/// <summary>
		/// Gets the trade radius for a beacon (enhanced or base).
		/// </summary>
		public static float GetTradeRadius(Building_OrbitalTradeBeacon beacon)
		{
			if (!SubcoreAutomationMod.Settings.orbitalTradeBeaconFeaturesEnabled)
				return BaseTradeRadius;

			var subcoreComp = beacon.TryGetComp<CompSubcoreAutomationBase>();
			if (subcoreComp != null && subcoreComp.SubcoreInstalled)
				return EnhancedTradeRadius;

			return BaseTradeRadius;
		}
	}
}
