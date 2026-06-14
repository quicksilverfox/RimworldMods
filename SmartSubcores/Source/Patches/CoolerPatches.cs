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
	/// Harmony patches for Cooler and Heater automation.
	/// Cooler: Inverter mode - heats when room is too cold.
	/// Heater: Detonator mode - scatter flames and detonate nearby explosives.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class CoolerPatches
	{
		private static FieldInfo _operatingAtHighPowerField;

		static CoolerPatches()
		{
			try
			{
				// Check if feature is enabled in settings
				if (!SubcoreAutomationMod.Settings.coolerInverterPatchEnabled)
				{
					// Cooler inverter patch disabled in settings
					return;
				}

				var harmony = new Harmony("SubcoreAutomation.CoolerPatches");

				// Cache reflection - use central manifest if available
				_operatingAtHighPowerField = ReflectionManifest.CompTempControl_operatingAtHighPower 
					?? AccessTools.Field(typeof(CompTempControl), "operatingAtHighPower");

				if (_operatingAtHighPowerField == null)
				{
					Log.Warning("[SubcoreAutomation] Cooler patches: operatingAtHighPower field not found, inverter may not work correctly.");
				}

				// Patch Building_Cooler.TickRare
				var tickRare = AccessTools.Method(typeof(Building_Cooler), "TickRare");
				if (tickRare != null)
					harmony.Patch(tickRare, prefix: new HarmonyMethod(typeof(CoolerPatches), nameof(Cooler_TickRare_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Cooler patches BROKEN: TickRare method not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply cooler patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Checks if a flickable is effectively on (current state or desired state is on).
		/// </summary>
		private static bool IsFlickableEffectivelyOn(CompFlickable flickable)
		{
			if (flickable == null)
				return true;

			// If no pending flick, just check current state
			if (!flickable.WantsFlick())
				return flickable.SwitchIsOn;

			// There's a pending flick - check what user wants via wantSwitchOn field
			if (SubcoreAutomationUtils.FlickableWantSwitchOnField != null)
			{
				object wantOn = SubcoreAutomationUtils.FlickableWantSwitchOnField.GetValue(flickable);
				if (wantOn != null)
					return (bool)wantOn;
			}

			// Fallback: assume toggle means opposite of current
			return !flickable.SwitchIsOn;
		}

		/// <summary>
		/// Prefix for cooler tick - handle heating when room is too cold.
		/// In inverter mode: heat the blue side, cool the exhaust side.
		/// </summary>
		public static bool Cooler_TickRare_Prefix(Building_Cooler __instance)
		{
			try
			{
				// Check if this cooler is automated
				var comp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (comp == null || !comp.SubcoreInstalled || !comp.IsAutomationEnabled)
					return true; // Let vanilla handle it

				// Get required comps
				CompPowerTrader power = __instance.TryGetComp<CompPowerTrader>();
				CompTempControl tempControl = __instance.TryGetComp<CompTempControl>();
				CompFlickable flickable = __instance.TryGetComp<CompFlickable>();
				
				if (power == null || tempControl == null)
					return true;

				// Check flickable - if building is toggled off, let vanilla handle
				if (flickable != null && !flickable.SwitchIsOn)
					return true;

				// Check power - if no power available, let vanilla handle
				if (!power.PowerOn)
					return true;

				// Get the cells on both sides of the cooler
				// Blue side (normally cooled) = front of the cooler
				// Exhaust side (normally heated) = back of the cooler
				IntVec3 blueSide = __instance.Position + IntVec3.South.RotatedBy(__instance.Rotation);
				IntVec3 exhaustSide = __instance.Position + IntVec3.North.RotatedBy(__instance.Rotation);

				if (!blueSide.InBounds(__instance.Map) || !exhaustSide.InBounds(__instance.Map))
					return true;

				float blueSideTemp = blueSide.GetTemperature(__instance.Map);
				float targetTemp = tempControl.targetTemperature;

				// If blue side is at or above target, set low power and let vanilla handle cooling
				if (blueSideTemp >= targetTemp)
				{
					if (_operatingAtHighPowerField != null)
						_operatingAtHighPowerField.SetValue(tempControl, false);
					return true;
				}

				// INVERTER MODE: Blue side is too cold, we need to heat it
				float exhaustSideTemp = exhaustSide.GetTemperature(__instance.Map);

				// Can only pump heat if exhaust side has extractable heat
				if (exhaustSideTemp < -40f)
				{
					if (_operatingAtHighPowerField != null)
						_operatingAtHighPowerField.SetValue(tempControl, false);
					return false;
				}

				// Calculate heating power - use the cooler's capacity
				float heatingPower = -tempControl.Props.energyPerSecond;

				// Calculate max energy we can transfer per TickRare (250 ticks = 4.16666651 seconds)
				float maxEnergyPerTick = heatingPower * 4.16666651f;

				float tempDiff = targetTemp - blueSideTemp;

				// Use full power when below target (like vanilla cooling uses full power when above target)
				// Only scale down in the last 2 degrees to avoid overshoot
				float energyNeeded;
				if (tempDiff > 2f)
				{
					// Far from target - use full heating capacity
					energyNeeded = maxEnergyPerTick;
				}
				else
				{
					// Close to target - scale down to avoid overshoot
					energyNeeded = maxEnergyPerTick * (tempDiff / 2f);
				}

				// Ensure positive
				energyNeeded = Mathf.Max(energyNeeded, 0f);

				bool isOperating = energyNeeded > 0.1f;

				if (isOperating)
				{
					// Push heat to blue side (heating it)
					GenTemperature.PushHeat(blueSide, __instance.Map, energyNeeded);

					// Pull heat from exhaust side (cooling it)
					// Heat pumps move ~125% of the energy they consume
					GenTemperature.PushHeat(exhaustSide, __instance.Map, -energyNeeded * 1.25f);
				}

				// Mark operating state
				if (_operatingAtHighPowerField != null)
					_operatingAtHighPowerField.SetValue(tempControl, isOperating);

				return false; // Skip vanilla - we handled it
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in Cooler_TickRare_Prefix: {ex.Message}", 93827470);
				return true;
			}
		}

		/// <summary>
		/// Detonate a heater - scatter flames and trigger nearby explosives.
		/// </summary>
		public static void DetonateHeater(Building heater)
		{
			if (heater?.Map == null)
				return;

			Map map = heater.Map;
			IntVec3 position = heater.Position;

			// Create a small flame explosion using GenExplosion
			GenExplosion.DoExplosion(
				center: position,
				map: map,
				radius: 3.9f,
				damType: DamageDefOf.Flame,
				instigator: null,
				damAmount: 10,
				armorPenetration: 0f,
				explosionSound: null,
				weapon: null,
				projectile: null,
				intendedTarget: null,
				postExplosionSpawnThingDef: ThingDefOf.Filth_Fuel,
				postExplosionSpawnChance: 0.3f,
				postExplosionSpawnThingCount: 1,
				postExplosionGasType: null,
				applyDamageToExplosionCellsNeighbors: false,
				preExplosionSpawnThingDef: null,
				preExplosionSpawnChance: 0f,
				preExplosionSpawnThingCount: 1,
				chanceToStartFire: 0.7f,
				damageFalloff: true
			);

			// Find and detonate nearby explosives
			float detonateRadius = 2.9f;
			List<Thing> toDetonate = new List<Thing>();

			foreach (IntVec3 cell in GenRadial.RadialCellsAround(position, detonateRadius, true))
			{
				if (!cell.InBounds(map))
					continue;

				foreach (Thing thing in cell.GetThingList(map))
				{
					// Check for IEDs
					if (thing.def.thingClass == typeof(Building_TrapExplosive) || 
						thing.def.defName.Contains("IED"))
					{
						toDetonate.Add(thing);
						continue;
					}

					// Check for mortar shells and other explosives
					CompExplosive compExplosive = thing.TryGetComp<CompExplosive>();
					if (compExplosive != null)
					{
						toDetonate.Add(thing);
						continue;
					}

					// Check for artillery shells by category
					if (thing.def.thingCategories != null)
					{
						foreach (var cat in thing.def.thingCategories)
						{
							if (cat.defName.Contains("Shell") || cat.defName.Contains("Mortar"))
							{
								toDetonate.Add(thing);
								break;
							}
						}
					}
				}
			}

			// Detonate found explosives
			foreach (Thing thing in toDetonate)
			{
				if (thing.Destroyed)
					continue;

				CompExplosive compExplosive = thing.TryGetComp<CompExplosive>();
				if (compExplosive != null)
				{
					compExplosive.StartWick();
				}
				else if (thing is Building_TrapExplosive trap)
				{
					// Force trigger the trap
					trap.Spring(null);
				}
			}

			// Destroy the heater
			heater.Destroy(DestroyMode.KillFinalize);
		}
	}
}
