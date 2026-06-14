using System.Linq;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Handler for automated research benches.
	/// Only one automated research bench works at a time per map (unless setting enabled).
	/// </summary>
	[StaticConstructorOnStartup]
	public static class ResearchBenchHandler
	{
		private static readonly ThingDef _multiAnalyzerDef;
		private static readonly ThingDef _hiTechResearchBenchDef;

		static ResearchBenchHandler()
		{
			_multiAnalyzerDef = DefDatabase<ThingDef>.GetNamedSilentFail("MultiAnalyzer");
			_hiTechResearchBenchDef = DefDatabase<ThingDef>.GetNamedSilentFail("HiTechResearchBench");
		}

		/// <summary>
		/// Checks if this bench is the primary (first active) automated research bench on the map.
		/// Only one automated bench works at a time unless allowMultipleResearchBenches is enabled.
		/// </summary>
		private static bool IsPrimaryResearchBench(Thing building, Map map)
		{
			// If multiple benches allowed, all are "primary"
			if (SubcoreAutomationMod.Settings?.allowMultipleResearchBenches == true)
				return true;

			if (_hiTechResearchBenchDef == null)
				return true;

			foreach (var bench in map.listerBuildings.AllBuildingsColonistOfDef(_hiTechResearchBenchDef))
			{
				var benchComp = bench.TryGetComp<CompSubcoreAutomationBase>();
				if (benchComp != null && benchComp.SubcoreInstalled && benchComp.IsAutomationEnabled)
				{
					// First active automated bench found - is it us?
					return bench == building;
				}
			}
			return true; // No other active bench found
		}

		/// <summary>
		/// Handler for automated research benches.
		/// If linked to an automated MultiAnalyzer, can research projects requiring it and gets +10% bonus.
		/// Only one automated bench works at a time per map (unless setting enabled).
		/// </summary>
		public static bool HandleAutomation(Thing building, CompSubcoreAutomationBase comp, float speedFactor)
		{
			ResearchManager researchManager = Find.ResearchManager;
			if (researchManager == null)
				return false;

			ResearchProjectDef currentProject = researchManager.GetProject();
			if (currentProject == null)
				return false;

			// Only one automated research bench works at a time (unless setting enabled)
			if (!IsPrimaryResearchBench(building, building.Map))
				return false;

			// Check if this bench can research the current project (hi-tech bench required)
			if (currentProject.requiredResearchBuilding != null &&
			    currentProject.requiredResearchBuilding != building.def)
				return false;

			// Check for linked automated multi-analyzer
			bool hasAutomatedMultiAnalyzer = false;
			CompAffectedByFacilities facilityComp = building.TryGetComp<CompAffectedByFacilities>();
			if (facilityComp != null && _multiAnalyzerDef != null)
			{
				foreach (Thing facility in facilityComp.LinkedFacilitiesListForReading)
				{
					if (facility.def == _multiAnalyzerDef)
					{
						CompSubcoreAutomationBase analyzerComp = facility.TryGetComp<CompSubcoreAutomationBase>();
						if (analyzerComp != null && analyzerComp.HasSubcoreInstalled && analyzerComp.IsAutomationEnabled)
						{
							hasAutomatedMultiAnalyzer = true;
							break;
						}
					}
				}
			}

			// Check if project requires multi-analyzer
			if (currentProject.requiredResearchFacilities != null &&
			    currentProject.requiredResearchFacilities.Contains(_multiAnalyzerDef))
			{
				// Need automated multi-analyzer to research this project
				if (!hasAutomatedMultiAnalyzer)
					return false;
			}

			// Calculate research amount
			// At speedFactor 0.55, yields ~150 research/day (based on old 0.15 = ~40/day)
			float researchAmount = 0.5f * speedFactor * 250f;

			// Apply multi-analyzer bonus (+10%)
			if (hasAutomatedMultiAnalyzer)
			{
				researchAmount *= 1.10f;
			}

			researchManager.ResearchPerformed(researchAmount, null);
			return true;
		}
	}

	/// <summary>
	/// Patch scanner work givers to skip if the building is automated and automation is enabled.
	/// </summary>
	[HarmonyPatch(typeof(WorkGiver_OperateScanner), nameof(WorkGiver_OperateScanner.HasJobOnThing))]
	public static class WorkGiver_OperateScanner_HasJobOnThing_Patch
	{
		public static bool Prefix(Thing t, ref bool __result, Pawn pawn)
		{
			// Block mechanoids from operating scanners
			if (pawn.RaceProps.IsMechanoid)
			{
				__result = false;
				return false;
			}

			// If the scanner is automated and automation is enabled, skip it
			CompSubcoreAutomationBase automation = t.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	/// <summary>
	/// Patch deep drill work giver to skip if the building is automated and automation is enabled.
	/// </summary>
	[HarmonyPatch(typeof(WorkGiver_DeepDrill), nameof(WorkGiver_DeepDrill.HasJobOnThing))]
	public static class WorkGiver_DeepDrill_HasJobOnThing_Patch
	{
		public static bool Prefix(Thing t, ref bool __result, Pawn pawn)
		{
			// Block mechanoids from operating deep drills
			if (pawn.RaceProps.IsMechanoid)
			{
				__result = false;
				return false;
			}

			// If the drill is automated and automation is enabled, skip it
			CompSubcoreAutomationBase automation = t.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	/// <summary>
	/// Patch researcher work giver to skip if the bench is automated and automation is enabled.
	/// </summary>
	[HarmonyPatch(typeof(WorkGiver_Researcher), nameof(WorkGiver_Researcher.HasJobOnThing))]
	public static class WorkGiver_Researcher_HasJobOnThing_Patch
	{
		public static bool Prefix(Thing t, ref bool __result, Pawn pawn)
		{
			// Block mechanoids from using research benches
			if (pawn.RaceProps.IsMechanoid)
			{
				__result = false;
				return false;
			}

			// If the research bench is automated and automation is enabled, skip it
			CompSubcoreAutomationBase automation = t.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
			{
				__result = false;
				return false;
			}

			return true;
		}
	}

	/// <summary>
	/// Ensure automated scanners still show they are active.
	/// </summary>
	[HarmonyPatch(typeof(CompScanner), nameof(CompScanner.CanUseNow), MethodType.Getter)]
	public static class CompScanner_CanUseNow_Patch
	{
		public static void Postfix(CompScanner __instance, ref AcceptanceReport __result)
		{
			// If we can't use now but the building is automated (and automation enabled), it's still working
			if (!__result.Accepted)
			{
				CompSubcoreAutomationBase automation = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
				if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled && automation.CanOperate())
				{
					__result = AcceptanceReport.WasAccepted;
				}
			}
		}
	}

	/// <summary>
	/// Suppress flick designations for automated buildings - they toggle instantly.
	/// </summary>
	[HarmonyPatch(typeof(FlickUtility), nameof(FlickUtility.UpdateFlickDesignation))]
	public static class FlickUtility_UpdateFlickDesignation_Patch
	{
		public static bool Prefix(Thing t)
		{
			CompSubcoreAutomationBase automation = t.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled)
			{
				// Don't create a flick designation - the automation handles it instantly
				// Just remove any existing designation
				t.Map?.designationManager?.TryRemoveDesignationOn(t, DesignationDefOf.Flick);
				return false;
			}
			return true;
		}
	}


	/// <summary>
	/// Suppress vanilla scanner inspect string when automated - our comp provides its own.
	/// Using postfix with Priority.Last to run after other mods' patches for compatibility,
	/// then clearing output to avoid flickering from time-based calculations.
	/// </summary>
	[HarmonyPatch(typeof(CompScanner), nameof(CompScanner.CompInspectStringExtra))]
	public static class CompScanner_CompInspectStringExtra_Patch
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(CompScanner __instance, ref string __result)
		{
			CompSubcoreAutomationBase automation = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
			{
				// Clear vanilla inspect string - CompSubcoreAutomation provides its own
				// Using postfix with Priority.Last ensures other mods' patches run first
				__result = null;
			}
		}
	}

	/// <summary>
	/// Suppress vanilla deep drill inspect string when automated (we show our own progress).
	/// </summary>
	[HarmonyPatch(typeof(CompDeepDrill), nameof(CompDeepDrill.CompInspectStringExtra))]
	public static class CompDeepDrill_CompInspectStringExtra_Patch
	{
		[HarmonyPostfix]
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(CompDeepDrill __instance, ref string __result)
		{
			CompSubcoreAutomationBase automation = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
			{
				// Clear vanilla inspect string - CompSubcoreAutomation provides its own
				// Using postfix with Priority.Last ensures other mods' patches run first
				__result = null;
			}
		}
	}

	/// <summary>
	/// Prevent vanilla from forbidding automated drills when resource is exhausted.
	/// Vanilla forbids drills in TryProducePortion when no resources remain - we want to
	/// turn them off via CompFlickable instead.
	/// </summary>
	[HarmonyPatch(typeof(CompDeepDrill), "TryProducePortion")]
	public static class CompDeepDrill_TryProducePortion_Patch
	{
		[HarmonyPostfix]
		public static void Postfix(CompDeepDrill __instance)
		{
			CompSubcoreAutomationBase automation = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
			if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
			{
				// Unforbid the drill if vanilla just forbade it
				var forbiddable = __instance.parent.TryGetComp<CompForbiddable>();
				if (forbiddable != null && forbiddable.Forbidden)
				{
					forbiddable.Forbidden = false;
				}
			}
		}
	}

	/// <summary>
	/// Fix power consumption being reset when PowerNetManager processes delayed actions.
	/// SetUpPowerVars is called again after our PostSpawnSetup, resetting the power to default.
	/// This postfix re-applies our power modifications.
	/// </summary>
	[HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.SetUpPowerVars))]
	public static class CompPowerTrader_SetUpPowerVars_Patch
	{
		public static void Postfix(CompPowerTrader __instance)
		{
			CompSubcoreAutomationBase automation = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
			automation?.ReapplyPowerConsumption();
		}
	}

	/// <summary>
	/// Thread-local storage for scanner target mineral override during DoFind.
	/// Set by DoFind prefix, cleared by postfix, read by ChooseLumpThingDef prefix.
	/// </summary>
	public static class ScannerTargetOverride
	{
		[System.ThreadStatic]
		public static ThingDef OverrideMineral;
	}

	/// <summary>
	/// Prefix/Postfix for CompDeepScanner.DoFind to set up target mineral override.
	/// When automated with a target mineral, this ensures ChooseLumpThingDef returns it.
	/// </summary>
	[HarmonyPatch(typeof(CompDeepScanner), "DoFind")]
	public static class CompDeepScanner_DoFind_Patch
	{
		public static void Prefix(CompDeepScanner __instance)
		{
			CompScannerAutomation automation = __instance.parent.TryGetComp<CompScannerAutomation>();
			if (automation?.SubcoreInstalled == true && automation.TargetMineral != null)
			{
				ScannerTargetOverride.OverrideMineral = automation.TargetMineral;
			}
		}

		public static void Postfix()
		{
			ScannerTargetOverride.OverrideMineral = null;
		}
	}

	/// <summary>
	/// Override mineral selection when a target is set via automation.
	/// Only affects automated scanners with explicit target set.
	/// </summary>
	[HarmonyPatch(typeof(CompDeepScanner), "ChooseLumpThingDef")]
	public static class CompDeepScanner_ChooseLumpThingDef_Patch
	{
		public static bool Prefix(ref ThingDef __result)
		{
			if (ScannerTargetOverride.OverrideMineral != null)
			{
				__result = ScannerTargetOverride.OverrideMineral;
				return false; // Skip original random selection
			}
			return true; // Use vanilla random selection
		}
	}

	/// <summary>
	/// Patch for Alert_CannotBeUsedRoofed.GetReport to exclude automated scanners.
	/// Automated scanners can work under roofs, so they shouldn't trigger the alert.
	/// </summary>

	/// <summary>
	/// Auto-complete flicks for automated buildings when the toggle is clicked.
	/// This patches the FlickDesignator to immediately complete the flick instead of waiting for a pawn.
	/// </summary>
	[HarmonyPatch(typeof(CompFlickable), nameof(CompFlickable.DoFlick))]
	public static class CompFlickable_DoFlick_Patch
	{
		// This runs after DoFlick - nothing to do here, but we need the patch target
	}

	/// <summary>
	/// Patch CompFlickable's gizmo to auto-complete flicks for automated buildings.
	/// When the toggle gizmo action runs, we postfix to immediately call DoFlick if automated.
	/// </summary>
	[HarmonyPatch(typeof(CompFlickable), nameof(CompFlickable.CompGetGizmosExtra))]
	public static class CompFlickable_CompGetGizmosExtra_Patch
	{
		public static System.Collections.Generic.IEnumerable<Gizmo> Postfix(
			System.Collections.Generic.IEnumerable<Gizmo> gizmos, 
			CompFlickable __instance)
		{
			foreach (var gizmo in gizmos)
			{
				// Check if this is the toggle gizmo and wrap its action
				if (gizmo is Command_Toggle toggle)
				{
					var originalAction = toggle.toggleAction;
					toggle.toggleAction = () =>
					{
						// Call original action first (sets wantSwitchOn)
						originalAction?.Invoke();
						
						// Auto-complete flick for automated buildings
						CompSubcoreAutomationBase automation = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
						if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
						{
							// Immediately complete the flick
							if (__instance.WantsFlick())
							{
								__instance.DoFlick();
							}
						}
					};
				}
				yield return gizmo;
			}
		}
	}

	[HarmonyPatch(typeof(Alert_CannotBeUsedRoofed), nameof(Alert_CannotBeUsedRoofed.GetReport))]
	public static class Alert_CannotBeUsedRoofed_GetReport_Patch
	{
		public static void Postfix(ref AlertReport __result)
		{
			if (!__result.active)
				return;

			// Filter out automated scanners from the culprits list
			if (__result.culpritsThings != null)
			{
				for (int i = __result.culpritsThings.Count - 1; i >= 0; i--)
				{
					Thing thing = __result.culpritsThings[i];
					if (thing != null)
					{
						CompSubcoreAutomationBase automation = thing.TryGetComp<CompSubcoreAutomationBase>();
						if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
						{
							__result.culpritsThings.RemoveAt(i);
						}
					}
				}

				// If no culprits remain, disable the alert
				if (__result.culpritsThings.Count == 0)
				{
					__result = AlertReport.Inactive;
				}
			}
		}
	}
}
