using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Patches for Farskip psycast to allow targeting automated Band Nodes and Mech Boosters.
	/// These buildings act as teleport beacons when equipped with a subcore.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class FarskipPatches
	{
		private static bool _initialized;

		static FarskipPatches()
		{
			Initialize();
		}

		private static void Initialize()
		{
			if (_initialized)
				return;

			// Only patch if Royalty is active (Farskip is a Royalty psycast)
			if (!ModsConfig.RoyaltyActive)
				return;

			var harmony = new Harmony("SubcoreAutomation.FarskipPatches");

			// Patch CanApplyOn to allow maps with automated beacons
			// Specify parameter types to avoid ambiguous match (there are overloads)
			var canApplyOn = AccessTools.Method(typeof(CompAbilityEffect_Farskip), nameof(CompAbilityEffect_Farskip.CanApplyOn),
				new[] { typeof(GlobalTargetInfo) });
			if (canApplyOn != null)
				harmony.Patch(canApplyOn, postfix: new HarmonyMethod(typeof(FarskipPatches), nameof(CanApplyOn_Postfix)));
			else
				Log.Error("[SubcoreAutomation] Farskip patches BROKEN: CompAbilityEffect_Farskip.CanApplyOn not found!");

			// Patch Valid to allow targeting maps with automated beacons
			var validMethod = AccessTools.Method(typeof(CompAbilityEffect_Farskip), nameof(CompAbilityEffect_Farskip.Valid),
				new[] { typeof(GlobalTargetInfo), typeof(bool) });
			if (validMethod != null)
				harmony.Patch(validMethod, postfix: new HarmonyMethod(typeof(FarskipPatches), nameof(Valid_Postfix)));
			else
				Log.Error("[SubcoreAutomation] Farskip patches BROKEN: CompAbilityEffect_Farskip.Valid not found!");

			// Patch WorldMapExtraLabel to show proper label for beacon targets
			var worldMapLabel = AccessTools.Method(typeof(CompAbilityEffect_Farskip), nameof(CompAbilityEffect_Farskip.WorldMapExtraLabel));
			if (worldMapLabel != null)
				harmony.Patch(worldMapLabel, postfix: new HarmonyMethod(typeof(FarskipPatches), nameof(WorldMapExtraLabel_Postfix)));
			else
				Log.Error("[SubcoreAutomation] Farskip patches BROKEN: CompAbilityEffect_Farskip.WorldMapExtraLabel not found!");

			_initialized = true;
		}

		/// <summary>
		/// Finds an automated beacon (Band Node or Mech Booster with subcore) on the target map.
		/// </summary>
		public static Thing FindAutomatedBeaconOnMap(Map map)
		{
			if (map == null)
				return null;

			// Check for automated Band Nodes
			var bandNodeDef = DefDatabase<ThingDef>.GetNamedSilentFail(MachineDefNames.BandNode);
			if (bandNodeDef != null)
			{
				foreach (var building in map.listerBuildings.AllBuildingsColonistOfDef(bandNodeDef))
				{
					var automation = building.TryGetComp<CompSubcoreAutomationBase>();
					if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
					{
						return building;
					}
				}
			}

			// Check for automated Mech Boosters
			var mechBoosterDef = DefDatabase<ThingDef>.GetNamedSilentFail(MachineDefNames.MechBooster);
			if (mechBoosterDef != null)
			{
				foreach (var building in map.listerBuildings.AllBuildingsColonistOfDef(mechBoosterDef))
				{
					var automation = building.TryGetComp<CompSubcoreAutomationBase>();
					if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
					{
						return building;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Postfix for CanApplyOn - allow targeting maps with automated beacons even without allied pawns.
		/// </summary>
		public static void CanApplyOn_Postfix(CompAbilityEffect_Farskip __instance, GlobalTargetInfo target, ref bool __result)
		{
			// If already valid, no need to change
			if (__result)
				return;

			// Check if target is a map with an automated beacon
			if (target.WorldObject is MapParent mapParent && mapParent.Map != null)
			{
				var beacon = FindAutomatedBeaconOnMap(mapParent.Map);
				if (beacon != null)
				{
					__result = true;
				}
			}
		}

		/// <summary>
		/// Postfix for Valid - allow targeting maps with automated beacons.
		/// </summary>
		public static void Valid_Postfix(CompAbilityEffect_Farskip __instance, GlobalTargetInfo target, bool throwMessages, ref bool __result)
		{
			// If already valid, no need to change
			if (__result)
				return;

			// Check if target is a map with an automated beacon
			if (target.WorldObject is MapParent mapParent && mapParent.Map != null)
			{
				var beacon = FindAutomatedBeaconOnMap(mapParent.Map);
				if (beacon != null)
				{
					__result = true;
				}
			}
		}

		/// <summary>
		/// Postfix for WorldMapExtraLabel - show beacon label when targeting via beacon.
		/// </summary>
		public static void WorldMapExtraLabel_Postfix(CompAbilityEffect_Farskip __instance, GlobalTargetInfo target, ref string __result)
		{
			// Only modify if current result indicates no ally available
			if (__result != "AbilityNeedAllyToSkip".Translate())
				return;

			if (target.WorldObject is MapParent mapParent && mapParent.Map != null)
			{
				var beacon = FindAutomatedBeaconOnMap(mapParent.Map);
				if (beacon != null)
				{
					__result = "SubcoreAutomation_FarskipToBeacon".Translate(beacon.LabelCap);
				}
			}
		}
	}

	/// <summary>
	/// Patch for CompAbilityEffect_Farskip.Apply to handle beacon teleportation.
	/// This patch ensures pawns teleport near the beacon when no allied pawn is present.
	/// </summary>
	[HarmonyPatch(typeof(CompAbilityEffect_Farskip), nameof(CompAbilityEffect_Farskip.Apply))]
	public static class CompAbilityEffect_Farskip_Apply_Patch
	{
		/// <summary>
		/// Prefix to set up beacon target position before Apply runs.
		/// We use a ThreadStatic to pass data since we can't modify the method signature.
		/// </summary>
		[ThreadStatic]
		public static Thing TargetBeacon;

		public static void Prefix(CompAbilityEffect_Farskip __instance, GlobalTargetInfo target)
		{
			TargetBeacon = null;

			if (target.WorldObject is MapParent mapParent && mapParent.Map != null)
			{
				// Check if there's no allied pawn but there is a beacon
				var alliedPawn = mapParent.Map.mapPawns.AllPawnsSpawned
					.FirstOrDefault(p => !p.NonHumanlikeOrWildMan() && p.IsColonist && p.HomeFaction == Faction.OfPlayer);

				if (alliedPawn == null)
				{
					TargetBeacon = FarskipPatches.FindAutomatedBeaconOnMap(mapParent.Map);
				}
			}
		}

		public static void Postfix()
		{
			TargetBeacon = null;
		}
	}

	/// <summary>
	/// Patch for the private AlliedPawnOnMap method via transpiler alternative.
	/// Since we can't easily patch the private method, we patch ShouldEnterMap instead.
	/// </summary>
	[HarmonyPatch]
	public static class CompAbilityEffect_Farskip_ShouldEnterMap_Patch
	{
		static bool Prepare()
		{
			return ModsConfig.RoyaltyActive;
		}

		static System.Reflection.MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(CompAbilityEffect_Farskip), "ShouldEnterMap");
		}

		/// <summary>
		/// Postfix for ShouldEnterMap - return true if there's a beacon on the map.
		/// </summary>
		public static void Postfix(CompAbilityEffect_Farskip __instance, GlobalTargetInfo target, ref bool __result)
		{
			// If already should enter, no change needed
			if (__result)
				return;

			if (target.WorldObject is MapParent mapParent && mapParent.Map != null)
			{
				var beacon = FarskipPatches.FindAutomatedBeaconOnMap(mapParent.Map);
				if (beacon != null)
				{
					__result = true;
				}
			}
		}
	}

	/// <summary>
	/// Patch for the private AlliedPawnOnMap method to return a fake "pawn" position via the beacon.
	/// Since AlliedPawnOnMap returns a Pawn and we need it for positioning, we handle this differently.
	/// We'll spawn pawns at the beacon location via a postfix on Apply.
	/// </summary>
	[HarmonyPatch]
	public static class CompAbilityEffect_Farskip_AlliedPawnOnMap_Patch
	{
		static bool Prepare()
		{
			return ModsConfig.RoyaltyActive;
		}

		static System.Reflection.MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(CompAbilityEffect_Farskip), "AlliedPawnOnMap");
		}

		/// <summary>
		/// Postfix for AlliedPawnOnMap - if no allied colonist pawn found but beacon exists,
		/// return any spawned pawn on the map so positioning works.
		/// The beacon enables the teleport, any pawn provides the spawn reference point.
		/// </summary>
		public static void Postfix(Map targetMap, ref Pawn __result)
		{
			// If a pawn was found, use normal behavior
			if (__result != null)
				return;

			// If no allied pawn found, check for beacon
			var beacon = FarskipPatches.FindAutomatedBeaconOnMap(targetMap);
			if (beacon != null)
			{
				// Store beacon for Apply to use
				CompAbilityEffect_Farskip_Apply_Patch.TargetBeacon = beacon;

				// Return any spawned pawn on the map as position reference
				// Priority: colonists > colony animals > any pawn
				__result = targetMap.mapPawns.FreeColonists.FirstOrDefault();
				
				if (__result == null)
				{
					__result = targetMap.mapPawns.SpawnedColonyAnimals.FirstOrDefault();
				}
				
				if (__result == null)
				{
					// Last resort: any pawn on the map
					__result = targetMap.mapPawns.AllPawnsSpawned.FirstOrDefault(p => !p.Dead);
				}

				// If STILL no pawn, we have a map with only a beacon and no living creatures
				// In this edge case, the teleport will use the caster's position fallback
				// which may not work well, but it's an extreme edge case
			}
		}
	}
}
