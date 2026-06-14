using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation
{
	/// <summary>
	/// Initializes Harmony patching for the mod.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class HarmonyInit
	{
		static HarmonyInit()
		{
			var harmony = new Harmony("quicksilverfox.SubcoreAutomation");
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			// Add generator support to all power plants with flickable comp
			AddGeneratorSupportToAllPowerPlants();
			
			// Optional: Add fallback automation to all powered+flickable buildings
			if (SubcoreAutomationMod.Settings?.fallbackAutomationEnabled == true)
			{
				AddFallbackAutomationToFlickables();
			}
			
			// Optional: Add fallback turret automation
			if (SubcoreAutomationMod.Settings?.fallbackTurretAutomationEnabled == true)
			{
				AddFallbackAutomationToTurrets();
			}

		}

		/// <summary>
		/// Dynamically adds CompProperties_SubcoreAutomation to any ThingDef that:
		/// 1. Has CompPowerPlant (or a subclass)
		/// 2. Has CompFlickable
		/// 3. Has CompRefuelable (backup power only makes sense for fuel-based generators)
		/// 4. Doesn't already have our comp
		/// This provides universal backup power support for vanilla and modded generators.
		/// </summary>
		private static void AddGeneratorSupportToAllPowerPlants()
		{
			int added = 0;

			foreach (var def in DefDatabase<ThingDef>.AllDefs)
			{
				if (def.comps == null)
					continue;

				// Check if already has our comp
				if (def.comps.Any(c => c is CompProperties_SubcoreAutomation))
					continue;

				// Check for CompPowerPlant (or subclass)
				bool hasPowerPlant = def.comps.Any(c => 
					c.compClass != null && typeof(CompPowerPlant).IsAssignableFrom(c.compClass));

				// Check for CompFlickable
				bool hasFlickable = def.comps.Any(c => 
					c.compClass != null && typeof(CompFlickable).IsAssignableFrom(c.compClass));

				// Check for CompRefuelable - backup power only makes sense for fuel-based generators
				bool hasRefuelable = def.comps.Any(c => 
					c.compClass != null && typeof(CompRefuelable).IsAssignableFrom(c.compClass));

				if (hasPowerPlant && hasFlickable && hasRefuelable)
				{
					// Determine appropriate subcore based on power output
					var powerProps = def.comps.FirstOrDefault(c => 
						c.compClass != null && typeof(CompPowerPlant).IsAssignableFrom(c.compClass)) as CompProperties_Power;
					
					SubcoreTier tier = GetSubcoreForPowerOutput(powerProps?.PowerConsumption ?? 0f);
					float workAmount = GetInstallWorkForPowerOutput(powerProps?.PowerConsumption ?? 0f);

					var props = new CompProperties_PowerAutomation
					{
						tier = tier,
						automatedSpeedFactor = 0f, // Generators don't use speed factor
						automatedPowerConsumption = 0, // No additional power draw
						installWorkAmount = workAmount
					};

					def.comps.Add(props);
					added++;
				}
			}

			if (added > 0)
			{
				// Added backup power support to generators
			}
		}

		/// <summary>
		/// Returns Basic subcore for all generators.
		/// Backup power is a simple on/off job that doesn't warrant higher tier subcores.
		/// </summary>
		private static SubcoreTier GetSubcoreForPowerOutput(float powerOutput)
		{
			return SubcoreTier.Basic;
		}

		/// <summary>
		/// Returns standard installation work for generators.
		/// </summary>
		private static float GetInstallWorkForPowerOutput(float powerOutput)
		{
			return 1500f;
		}

		/// <summary>
		/// Adds basic subcore automation to all powered flickable buildings that don't already have it.
		/// This provides remote on/off control for any flickable machine.
		/// Only runs if fallbackAutomationEnabled is true in settings.
		/// </summary>
		private static void AddFallbackAutomationToFlickables()
		{
			int added = 0;

			foreach (var def in DefDatabase<ThingDef>.AllDefs)
			{
				if (def.comps == null)
					continue;

				// Skip if already has our comp
				if (def.comps.Any(c => c is CompProperties_SubcoreAutomation))
					continue;

				// Skip swappable-turret buildings: their subcore is part of the build cost, not an installable comp
				if (def.thingClass != null && typeof(SubcoreAutomation.Buildings.Building_SwappableTurret).IsAssignableFrom(def.thingClass))
					continue;

				// Must have CompFlickable
				bool hasFlickable = def.comps.Any(c =>
					c.compClass != null && typeof(CompFlickable).IsAssignableFrom(c.compClass));
				if (!hasFlickable)
					continue;

				// Must have CompPower (consumer or trader)
				bool hasPower = def.comps.Any(c => 
					c.compClass != null && typeof(CompPower).IsAssignableFrom(c.compClass));
				if (!hasPower)
					continue;

				// Skip generators - they're handled by AddGeneratorSupportToAllPowerPlants
				bool isPowerPlant = def.comps.Any(c => 
					c.compClass != null && typeof(CompPowerPlant).IsAssignableFrom(c.compClass));
				if (isPowerPlant)
					continue;

				// Add basic automation comp
				var props = new CompProperties_SubcoreAutomation
				{
					tier = SubcoreTier.Basic,
					automatedSpeedFactor = 0f, // Binary on/off - no efficiency shown
					automatedPowerConsumption = 0, // No additional power draw
					installWorkAmount = 500f // Quick installation for simple remote control
				};

				def.comps.Add(props);
				added++;
			}

			if (added > 0)
			{
				Log.Message($"[SubcoreAutomation] Fallback automation: Added remote control to {added} powered flickable buildings.");
			}
		}

		/// <summary>
		/// Adds turret subcore automation to all Building_TurretGun turrets that don't already have it.
		/// Excludes decoy/fake turrets based on damage checks.
		/// Only runs if fallbackTurretAutomationEnabled is true in settings.
		/// Subcore tier is based on turret size and whether it requires spacer components:
		/// - 1x1 turrets: Basic subcore (Regular if uses ComponentSpacer)
		/// - 2x2+ turrets: Regular subcore (High if uses ComponentSpacer)
		/// </summary>
		private static void AddFallbackAutomationToTurrets()
		{
			int added = 0;

			foreach (var def in DefDatabase<ThingDef>.AllDefs)
			{
				// Must be a Building_TurretGun (exact class or subclass)
				if (def.thingClass == null || !typeof(Building_TurretGun).IsAssignableFrom(def.thingClass))
					continue;

				// Skip swappable-turret buildings: their subcore is part of the build cost, not an installable comp
				if (typeof(SubcoreAutomation.Buildings.Building_SwappableTurret).IsAssignableFrom(def.thingClass))
					continue;

				// Skip if already has our comp
				if (def.comps != null && def.comps.Any(c => c is CompProperties_SubcoreAutomation))
					continue;

				// Safety check: Get the turret's gun def
				var turretGunDef = GetTurretGunDef(def);
				if (turretGunDef == null)
					continue;

				// Exclude decoy/fake turrets: check for 0 damage weapons
				if (IsDecoyTurret(turretGunDef))
					continue;
				
				// Skip turrets that can't be built (obtained through other means)
				if (def.designationCategory == null && def.minifiedDef == null)
					continue;

				// Ensure comps list exists
				if (def.comps == null)
					def.comps = new List<CompProperties>();

				// Determine subcore tier based on size and tech level
				SubcoreTier tier = GetSubcoreForTurret(def);
				float workAmount = GetInstallWorkForTurret(def);

				// Add turret automation comp
				var props = new CompProperties_DefenseAutomation
				{
					tier = tier,
					automatedSpeedFactor = 0f, // Turrets don't use speed factor
					automatedPowerConsumption = 0,
					installWorkAmount = workAmount
				};

				def.comps.Add(props);
				added++;
			}

			if (added > 0)
			{
				Log.Message($"[SmartSubcores] Fallback turret automation: Added enhancements to {added} turrets.");
			}
		}

		/// <summary>
		/// Determines appropriate subcore tier for a turret based on size and build cost.
		/// Turrets requiring ComponentSpacer are considered advanced tech.
		/// </summary>
		private static SubcoreTier GetSubcoreForTurret(ThingDef turretDef)
		{
			// Calculate turret size (area)
			int area = turretDef.size.x * turretDef.size.z;
			bool isLarge = area >= 4; // 2x2 or larger

			// Check if turret requires spacer components (reliable indicator of advanced tech)
			bool isAdvancedTech = RequiresSpacerComponents(turretDef);

			// Base tier: Basic for small (1x1), Regular for large (2x2+)
			// Bump up one tier for advanced tech (uses ComponentSpacer)
			if (isLarge)
			{
				// Large turret: Regular, or High if advanced
				return isAdvancedTech ? SubcoreTier.High : SubcoreTier.Regular;
			}

			// Small turret: Basic, or Regular if advanced
			return isAdvancedTech ? SubcoreTier.Regular : SubcoreTier.Basic;
		}

		/// <summary>
		/// Checks if a ThingDef requires ComponentSpacer in its build cost.
		/// </summary>
		private static bool RequiresSpacerComponents(ThingDef def)
		{
			if (def.costList == null)
				return false;

			foreach (var cost in def.costList)
			{
				if (cost.thingDef?.defName == MachineDefNames.ComponentSpacer)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Determines installation work amount for a turret based on size.
		/// </summary>
		private static float GetInstallWorkForTurret(ThingDef turretDef)
		{
			int area = turretDef.size.x * turretDef.size.z;
			if (area >= 4)
				return 2000f; // Large turrets take longer
			return 1500f; // Standard work for small turrets
		}

		/// <summary>
		/// Gets the gun ThingDef for a turret.
		/// </summary>
		private static ThingDef GetTurretGunDef(ThingDef turretDef)
		{
			// Check building.turretGunDef field
			if (turretDef.building?.turretGunDef != null)
				return turretDef.building.turretGunDef;

			return null;
		}

		/// <summary>
		/// Checks if a turret is a decoy/fake based on its weapon properties.
		/// </summary>
		private static bool IsDecoyTurret(ThingDef gunDef)
		{
			if (gunDef == null)
				return true; // No gun = can't enhance

			// Check verbs for damage
			if (gunDef.Verbs != null && gunDef.Verbs.Count > 0)
			{
				var mainVerb = gunDef.Verbs[0];

				// Check if projectile has 0 damage
				if (mainVerb.defaultProjectile != null)
				{
					var projDef = mainVerb.defaultProjectile;
					if (projDef.projectile != null)
					{
						// Check base damage (using 1.0 multiplier, no weapon)
						int baseDamage = projDef.projectile.GetDamageAmount(1f, null);
						if (baseDamage <= 0)
							return true; // Decoy - 0 damage
					}
				}
			}

			return false;
		}
	}
}
