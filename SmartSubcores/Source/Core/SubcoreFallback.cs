using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Handles fallback mode when Biotech DLC is not installed.
	/// Substitutes subcores with crafting materials.
	/// </summary>
	public static class SubcoreFallback
	{
		// Cached ThingDefs for materials
		private static ThingDef _steel;
		private static ThingDef _plasteel;
		private static ThingDef _component;
		private static ThingDef _advancedComponent;
		
		// Cached ResearchProjectDef
		private static ResearchProjectDef _microelectronics;
		
		private static bool _initialized;

		/// <summary>
		/// Whether fallback mode is active (no Biotech or manually enabled).
		/// </summary>
		public static bool IsActive
		{
			get
			{
				// Always use fallback if Biotech is not installed
				if (!ModsConfig.BiotechActive)
					return true;
				
				// Otherwise, check user setting (allows manual override even with Biotech)
				return SubcoreAutomationMod.Settings?.noBiotechFallbackMode ?? false;
			}
		}

		/// <summary>
		/// Initialize cached defs.
		/// </summary>
		private static void Initialize()
		{
			if (_initialized)
				return;

			_steel = ThingDefOf.Steel;
			_plasteel = ThingDefOf.Plasteel;
			_component = ThingDefOf.ComponentIndustrial;
			_advancedComponent = ThingDefOf.ComponentSpacer;
			_microelectronics = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("Microelectronics");
			
			_initialized = true;
		}

		/// <summary>
		/// Gets the fallback material requirements for a subcore tier.
		/// Basic: 2 components + 50 steel
		/// Regular: 2 components + 1 advanced component + 50 steel
		/// High: 2 advanced components + 50 plasteel
		/// </summary>
		public static List<ThingDefCountClass> GetFallbackMaterials(ThingDef subcoreDef)
		{
			Initialize();
			
			if (subcoreDef == null)
				return GetBasicMaterials();

			switch (subcoreDef.defName)
			{
				case "SubcoreBasic":
					return GetBasicMaterials();
				case "SubcoreRegular":
					return GetRegularMaterials();
				case "SubcoreHigh":
					return GetHighMaterials();
				default:
					return GetBasicMaterials();
			}
		}

		/// <summary>
		/// Gets fallback materials for Basic tier: 2 components + 50 steel
		/// </summary>
		private static List<ThingDefCountClass> GetBasicMaterials()
		{
			return new List<ThingDefCountClass>
			{
				new ThingDefCountClass(_component, 2),
				new ThingDefCountClass(_steel, 50)
			};
		}

		/// <summary>
		/// Gets fallback materials for Regular tier: 2 components + 1 advanced component + 50 steel
		/// </summary>
		private static List<ThingDefCountClass> GetRegularMaterials()
		{
			return new List<ThingDefCountClass>
			{
				new ThingDefCountClass(_component, 2),
				new ThingDefCountClass(_advancedComponent, 1),
				new ThingDefCountClass(_steel, 50)
			};
		}

		/// <summary>
		/// Gets fallback materials for High tier: 2 advanced components + 50 plasteel
		/// </summary>
		private static List<ThingDefCountClass> GetHighMaterials()
		{
			return new List<ThingDefCountClass>
			{
				new ThingDefCountClass(_advancedComponent, 2),
				new ThingDefCountClass(_plasteel, 50)
			};
		}

		/// <summary>
		/// Gets the required research for fallback mode (Microelectronics instead of Basic Mechtech).
		/// </summary>
		public static ResearchProjectDef GetFallbackResearch()
		{
			Initialize();
			return _microelectronics;
		}

		/// <summary>
		/// Checks if the required research is complete for fallback mode.
		/// </summary>
		public static bool IsFallbackResearchComplete()
		{
			var research = GetFallbackResearch();
			return research == null || research.IsFinished;
		}

		/// <summary>
		/// Gets a human-readable description of the fallback materials.
		/// </summary>
		public static string GetMaterialsDescription(ThingDef subcoreDef)
		{
			var materials = GetFallbackMaterials(subcoreDef);
			var parts = new List<string>();
			
			foreach (var mat in materials)
			{
				parts.Add($"{mat.count}x {mat.thingDef.label}");
			}
			
			return string.Join(", ", parts);
		}

		/// <summary>
		/// Checks if the map has enough materials for the fallback installation.
		/// </summary>
		public static bool HasEnoughMaterials(Map map, ThingDef subcoreDef)
		{
			var materials = GetFallbackMaterials(subcoreDef);
			
			foreach (var mat in materials)
			{
				int available = map.resourceCounter.GetCount(mat.thingDef);
				if (available < mat.count)
					return false;
			}
			
			return true;
		}

		/// <summary>
		/// Finds reservable material Things on the map for the fallback installation.
		/// Returns a list of (Thing, count) tuples representing what to haul.
		/// </summary>
		public static List<(Thing thing, int count)> FindMaterialsOnMap(Map map, ThingDef subcoreDef, Pawn pawn)
		{
			var requirements = GetFallbackMaterials(subcoreDef);
			var result = new List<(Thing, int)>();

			foreach (var req in requirements)
			{
				int remaining = req.count;
				
				foreach (Thing thing in map.listerThings.ThingsOfDef(req.thingDef))
				{
					if (remaining <= 0)
						break;

					if (thing.IsForbidden(pawn))
						continue;

					if (!pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Deadly))
						continue;

					int toTake = System.Math.Min(remaining, thing.stackCount);
					result.Add((thing, toTake));
					remaining -= toTake;
				}

				// If we couldn't find enough of this material, fail
				if (remaining > 0)
					return null;
			}

			return result;
		}

	}
}
