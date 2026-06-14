using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for turret enhancements when a subcore is installed.
	/// These patches are completely skipped if Combat Extended is loaded.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class TurretPatches
	{
		/// <summary>
		/// Cache of turret thing IDs that have fired a projectile, used for friendly fire tracking.
		/// </summary>
		private static readonly Dictionary<int, (string defName, bool ffEnabled)> _projectileTurretMap = new Dictionary<int, (string, bool)>();

		/// <summary>
		/// Tracks the current turret being used for shot calculations (thread-local would be better but this works for single-threaded RimWorld).
		/// </summary>
		private static Building_TurretGun _currentShootingTurret;
		
		// Reusable collections to avoid GC allocations in hot paths
		private static readonly List<int> _cleanupTempList = new List<int>();
		private static readonly HashSet<int> _cleanupTempSet = new HashSet<int>();

		// Reflection for turret internals
		private static readonly FieldInfo TurretTopField;
		private static readonly FieldInfo BurstWarmupTicksLeftField;

		static TurretPatches()
		{
			// Get reflection fields
			TurretTopField = AccessTools.Field(typeof(Building_TurretGun), "top");
			BurstWarmupTicksLeftField = AccessTools.Field(typeof(Building_TurretGun), "burstWarmupTicksLeft");

			// Skip all turret patches if Combat Extended is loaded or if disabled in settings
			if (SubcoreAutomationMod.CombatExtendedLoaded)
			{
				// Skipping turret patches - Combat Extended detected
				return;
			}

			if (SubcoreAutomationMod.Settings != null && !SubcoreAutomationMod.Settings.turretPatchesEnabled)
			{
				// Skipping turret patches - disabled in settings
				return;
			}

			try
			{
				var harmony = new Harmony("SubcoreAutomation.TurretPatches");

				// Use LOW priority for combat patches to run after other combat mods
				// This reduces conflicts with mods that modify combat calculations

				// Patch for accuracy bonus - modify hit chance in ShotReport
				// HIGH RISK: Modifies core combat calculations
				var hitReportFor = AccessTools.Method(typeof(ShotReport), "HitReportFor", 
					new Type[] { typeof(Thing), typeof(Verb), typeof(LocalTargetInfo) });
				if (hitReportFor != null)
				{
					var shotPrefix = new HarmonyMethod(typeof(TurretPatches), nameof(ShotReport_HitReportFor_Prefix));
					shotPrefix.priority = Priority.Low;
					var shotPostfix = new HarmonyMethod(typeof(TurretPatches), nameof(ShotReport_HitReportFor_Postfix));
					shotPostfix.priority = Priority.Low;

					harmony.Patch(hitReportFor, prefix: shotPrefix, postfix: shotPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: ShotReport.HitReportFor not found!");

				// Patch for warmup reduction - modify warmup when turret starts targeting
				var tryStartShootSomething = AccessTools.Method(typeof(Building_TurretGun), "TryStartShootSomething");
				if (tryStartShootSomething != null)
				{
					var warmupPostfix = new HarmonyMethod(typeof(TurretPatches), nameof(Building_TurretGun_TryStartShootSomething_Postfix));
					warmupPostfix.priority = Priority.Low;
					harmony.Patch(tryStartShootSomething, postfix: warmupPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: Building_TurretGun.TryStartShootSomething not found!");

				// Patch for cooldown reduction (BurstCooldownTime is a method returning float seconds)
				var burstCooldownTime = AccessTools.Method(typeof(Building_TurretGun), "BurstCooldownTime");
				if (burstCooldownTime != null)
				{
					var cooldownPostfix = new HarmonyMethod(typeof(TurretPatches), nameof(Building_TurretGun_BurstCooldownTime_Postfix));
					cooldownPostfix.priority = Priority.Low;
					harmony.Patch(burstCooldownTime, postfix: cooldownPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: Building_TurretGun.BurstCooldownTime method not found!");

				// Patch for tracking projectile source (for friendly fire)
				var verbTryCastShot = AccessTools.Method(typeof(Verb_LaunchProjectile), "TryCastShot");
				if (verbTryCastShot != null)
				{
					var verbPostfix = new HarmonyMethod(typeof(TurretPatches), nameof(Verb_TryCastShot_Postfix));
					verbPostfix.priority = Priority.Low;
					harmony.Patch(verbTryCastShot, postfix: verbPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: Verb_LaunchProjectile.TryCastShot not found!");

				// Patch for friendly fire prevention
				var projectileCanHit = AccessTools.Method(typeof(Projectile), "CanHit");
				if (projectileCanHit != null)
				{
					var canHitPrefix = new HarmonyMethod(typeof(TurretPatches), nameof(Projectile_CanHit_Prefix));
					canHitPrefix.priority = Priority.Low;
					harmony.Patch(projectileCanHit, prefix: canHitPrefix);
				}
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: Projectile.CanHit not found!");

				// Patch CanSetForcedTarget to enable forced targeting for turrets with subcores
				var canSetForcedTarget = AccessTools.PropertyGetter(typeof(Building_TurretGun), "CanSetForcedTarget");
				if (canSetForcedTarget != null)
					harmony.Patch(canSetForcedTarget, postfix: new HarmonyMethod(typeof(TurretPatches), nameof(Building_TurretGun_CanSetForcedTarget_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: Building_TurretGun.CanSetForcedTarget getter not found!");

				// Patch CanToggleHoldFire to enable hold fire toggle for turrets with subcores
				var canToggleHoldFire = AccessTools.PropertyGetter(typeof(Building_TurretGun), "CanToggleHoldFire");
				if (canToggleHoldFire != null)
					harmony.Patch(canToggleHoldFire, postfix: new HarmonyMethod(typeof(TurretPatches), nameof(Building_TurretGun_CanToggleHoldFire_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Turret patches BROKEN: Building_TurretGun.CanToggleHoldFire getter not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply turret patches: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// Check if turret patches are globally enabled in settings.
		/// </summary>
		private static bool ArePatchesEnabled()
		{
			if (SubcoreAutomationMod.CombatExtendedLoaded)
				return false;
			return SubcoreAutomationMod.Settings?.turretPatchesEnabled ?? true;
		}

		/// <summary>
		/// Gets the subcore automation comp from a turret, if it has one installed and enabled.
		/// Returns null if patches are globally disabled.
		/// </summary>
		public static CompSubcoreAutomationBase GetTurretComp(Building_TurretGun turret)
		{
			if (!ArePatchesEnabled())
				return null;

			if (turret == null)
				return null;

			var comp = turret.TryGetComp<CompSubcoreAutomationBase>();
			if (comp == null || !comp.SubcoreInstalled || !comp.IsAutomationEnabled)
				return null;

			return comp;
		}

		/// <summary>
		/// Gets the accuracy bonus based on the installed subcore tier.
		/// Uses tier-based settings from mod configuration.
		/// </summary>
		public static float GetAccuracyBonusForSubcore(CompSubcoreAutomationBase comp, string defName)
		{
			float bonus;
			// Use tier-based settings
			if (comp?.Props?.subcoreDef != null && SubcoreAutomationMod.Settings != null)
			{
				bonus = SubcoreAutomationMod.Settings.GetTurretAccuracyByTier(comp.Props.subcoreDef.defName);
			}
			else
			{
				// Fallback to default
				bonus = GetDefaultAccuracyBonus(defName);
			}
			return Mathf.Clamp01(bonus);
		}

		/// <summary>
		/// Gets the warmup reduction based on the installed subcore tier.
		/// Uses tier-based settings from mod configuration.
		/// </summary>
		public static float GetWarmupReductionForSubcore(CompSubcoreAutomationBase comp, string defName)
		{
			float reduction;
			// Use tier-based settings
			if (comp?.Props?.subcoreDef != null && SubcoreAutomationMod.Settings != null)
			{
				reduction = SubcoreAutomationMod.Settings.GetTurretWarmupByTier(comp.Props.subcoreDef.defName);
			}
			else
			{
				// Fallback to default
				reduction = GetDefaultWarmupReduction(defName);
			}
			return Mathf.Clamp01(reduction);
		}

		/// <summary>
		/// Gets whether friendly fire prevention is enabled for a turret.
		/// </summary>
		public static bool IsFriendlyFirePreventionEnabled(string defName)
		{
			if (SubcoreAutomationMod.Settings == null)
				return true;

			return SubcoreAutomationMod.Settings.IsFriendlyFirePreventionEnabled(defName);
		}

		private static float GetDefaultAccuracyBonus(string defName)
		{
			var machine = SubcoreAutomationMod.GetMachineDef(defName);
			return machine?.defaultAccuracyBonus ?? 0.15f;
		}

		private static float GetDefaultWarmupReduction(string defName)
		{
			var machine = SubcoreAutomationMod.GetMachineDef(defName);
			return machine?.defaultWarmupReduction ?? 0.10f;
		}

		#region Harmony Patches

		/// <summary>
		/// Prefix for ShotReport.HitReportFor - track the turret for accuracy bonus.
		/// </summary>
		public static void ShotReport_HitReportFor_Prefix(Thing caster)
		{
			_currentShootingTurret = caster as Building_TurretGun;
		}

		/// <summary>
		/// Postfix for ShotReport.HitReportFor - apply accuracy bonus to the shot report.
		/// Accuracy is now tier-based on the installed subcore type.
		/// </summary>
		public static void ShotReport_HitReportFor_Postfix(ref ShotReport __result, Thing caster)
		{
			try
			{
				if (_currentShootingTurret != null)
				{
					var comp = GetTurretComp(_currentShootingTurret);
					if (comp != null)
					{
						// Use tier-based accuracy bonus based on installed subcore
						float accuracyBonus = GetAccuracyBonusForSubcore(comp, _currentShootingTurret.def.defName);
						
						// Modify the hit chance by accessing the private field
						// ShotReport is a struct, so we need to modify it carefully
						var factorFromShooterField = ReflectionManifest.ShotReport_factorFromShooterAndDist;
						
						if (factorFromShooterField != null)
						{
							float currentFactor = (float)factorFromShooterField.GetValue(__result);
							float newFactor = Mathf.Min(1f, currentFactor + accuracyBonus);
							
							// Box to modify struct
							object boxed = __result;
							factorFromShooterField.SetValue(boxed, newFactor);
							__result = (ShotReport)boxed;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in ShotReport postfix: {ex.Message}", 83729470);
			}
			finally
			{
				_currentShootingTurret = null;
			}
		}

		/// <summary>
		/// Postfix for TryStartShootSomething - reduce warmup time after turret starts targeting.
		/// Uses tier-based warmup reduction.
		/// </summary>
		public static void Building_TurretGun_TryStartShootSomething_Postfix(Building_TurretGun __instance)
		{
			try
			{
				var comp = GetTurretComp(__instance);
				if (comp != null && BurstWarmupTicksLeftField != null)
				{
					int currentWarmup = (int)BurstWarmupTicksLeftField.GetValue(__instance);
					if (currentWarmup > 0)
					{
						float reduction = GetWarmupReductionForSubcore(comp, __instance.def.defName);
						int newWarmup = Mathf.Max(1, (int)(currentWarmup * (1f - reduction)));
						BurstWarmupTicksLeftField.SetValue(__instance, newWarmup);
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in TryStartShootSomething postfix: {ex.Message}", 83729471);
			}
		}

		/// <summary>
		/// Enable forced targeting for turrets with subcores installed.
		/// This unlocks the vanilla "Attack" gizmo that allows targeting specific enemies.
		/// </summary>
		public static void Building_TurretGun_CanSetForcedTarget_Postfix(Building_TurretGun __instance, ref bool __result)
		{
			try
			{
				// If already true, no need to change
				if (__result)
					return;

				// Enable forced targeting for turrets with subcores
				var comp = GetTurretComp(__instance);
				if (comp != null)
				{
					__result = true;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in CanSetForcedTarget postfix: {ex.Message}", 83729472);
			}
		}

		/// <summary>
		/// Enable hold fire toggle for turrets with subcores installed.
		/// </summary>
		public static void Building_TurretGun_CanToggleHoldFire_Postfix(Building_TurretGun __instance, ref bool __result)
		{
			try
			{
				// If already true, no need to change
				if (__result)
					return;

				// Enable hold fire for turrets with subcores, or swappable turrets (always-on by design)
				if (__instance is SubcoreAutomation.Buildings.Building_SwappableTurret)
				{
					__result = true;
					return;
				}
				var comp = GetTurretComp(__instance);
				if (comp != null)
				{
					__result = true;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in CanToggleHoldFire postfix: {ex.Message}", 83729473);
			}
		}

		/// <summary>
		/// Reduce turret cooldown time when subcore is installed.
		/// Uses tier-based warmup reduction.
		/// BurstCooldownTime() returns float seconds.
		/// </summary>
		public static void Building_TurretGun_BurstCooldownTime_Postfix(Building_TurretGun __instance, ref float __result)
		{
			try
			{
				var comp = GetTurretComp(__instance);
				if (comp != null)
				{
					float reduction = GetWarmupReductionForSubcore(comp, __instance.def.defName);
					__result = Mathf.Max(0.1f, __result * (1f - reduction));
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in BurstCooldownTime postfix: {ex.Message}", 83729463);
			}
		}

		/// <summary>
		/// Postfix for TryCastShot - track projectile for friendly fire prevention.
		/// </summary>
		public static void Verb_TryCastShot_Postfix(Verb_LaunchProjectile __instance, bool __result)
		{
			try
			{
				if (!__result)
					return;

				if (__instance.caster is Building_TurretGun turret)
				{
					var comp = GetTurretComp(turret);
					bool isSwap = turret is SubcoreAutomation.Buildings.Building_SwappableTurret;
					if (comp != null || isSwap)
					{
						string defName = turret.def.defName;
						// Swappable turrets default to friendly fire prevention; comp-driven turrets honor settings per def
						bool ffEnabled = isSwap ? true : IsFriendlyFirePreventionEnabled(defName);

						// Find projectiles from this turret - iterate directly without LINQ allocations
						var allProjectiles = turret.Map?.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
						if (allProjectiles != null)
						{
							for (int i = 0; i < allProjectiles.Count; i++)
							{
								if (allProjectiles[i] is Projectile proj && proj.Launcher == turret)
								{
									if (!_projectileTurretMap.ContainsKey(proj.thingIDNumber))
									{
										_projectileTurretMap[proj.thingIDNumber] = (defName, ffEnabled);
									}
								}
							}
						}

						// Clean up old entries periodically
						if (_projectileTurretMap.Count > 100)
						{
							CleanupProjectileMap(turret.Map);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in TryCastShot postfix: {ex.Message}", 83729462);
			}
		}

		/// <summary>
		/// Prevent projectiles from hitting friendly pawns when friendly fire prevention is enabled.
		/// </summary>
		public static bool Projectile_CanHit_Prefix(Projectile __instance, Thing thing, ref bool __result)
		{
			try
			{
				if (_projectileTurretMap.TryGetValue(__instance.thingIDNumber, out var turretInfo))
				{
					if (turretInfo.ffEnabled && thing is Pawn pawn)
					{
						Faction turretFaction = __instance.Launcher?.Faction;
						if (turretFaction != null && pawn.Faction != null)
						{
							if (!turretFaction.HostileTo(pawn.Faction))
							{
								__result = false;
								return false;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in CanHit prefix: {ex.Message}", 83729465);
			}

			return true;
		}

		#endregion

		/// <summary>
		/// Clean up stale projectile tracking entries.
		/// </summary>
		private static void CleanupProjectileMap(Map map)
		{
			if (map == null)
				return;

			// Use pre-allocated collections to avoid GC allocations
			_cleanupTempList.Clear();
			_cleanupTempSet.Clear();
			
			// Build set of existing projectile IDs
			var projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
			for (int i = 0; i < projectiles.Count; i++)
			{
				_cleanupTempSet.Add(projectiles[i].thingIDNumber);
			}

			// Find keys to remove
			foreach (var key in _projectileTurretMap.Keys)
			{
				if (!_cleanupTempSet.Contains(key))
				{
					_cleanupTempList.Add(key);
				}
			}

			// Remove stale entries
			for (int i = 0; i < _cleanupTempList.Count; i++)
			{
				_projectileTurretMap.Remove(_cleanupTempList[i]);
			}
		}
	}
}
