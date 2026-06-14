using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Vitals Monitor automation.
	/// With High subcore installed, provides the missing 2% to ensure surgery never catastrophically fails.
	/// Surgery success is normally capped at 98%. With enhanced monitor, skilled surgeons get 100% success.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class VitalsMonitorPatches
	{
		// Quality threshold at which enhanced monitor guarantees success
		private const float QualityThreshold = 0.98f;

		static VitalsMonitorPatches()
		{
			try
			{
				// Check if feature is enabled in settings
				if (!SubcoreAutomationMod.Settings.vitalsMonitorSurgeryPatchEnabled)
				{
					// Vitals monitor surgery patch disabled in settings
					return;
				}

				var harmony = new Harmony("SubcoreAutomation.VitalsMonitorPatches");

				// Patch SurgeryOutcomeSuccess.Apply to guarantee success when quality is at cap and monitor is enhanced
				// Use LOW priority to run after other surgery mods that may modify success chances
				var applyMethod = AccessTools.Method(typeof(SurgeryOutcomeSuccess), nameof(SurgeryOutcomeSuccess.Apply));
				if (applyMethod != null)
				{
					var prefixMethod = new HarmonyMethod(typeof(VitalsMonitorPatches), nameof(SurgeryOutcomeSuccess_Apply_Prefix));
					prefixMethod.priority = Priority.Low;
					harmony.Patch(applyMethod, prefix: prefixMethod);
				}
				else
					Log.Error("[SubcoreAutomation] Vitals Monitor patches BROKEN: SurgeryOutcomeSuccess.Apply not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply vitals monitor patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if the patient is in a bed with an automated Vitals Monitor linked.
		/// </summary>
		private static bool HasEnhancedVitalsMonitor(Pawn patient)
		{
			if (patient == null)
				return false;

			// Get the bed the patient is in
			Building_Bed bed = patient.CurrentBed();
			if (bed == null)
				return false;

			// Get the facilities affecting this bed
			CompAffectedByFacilities affectedByFacilities = bed.TryGetComp<CompAffectedByFacilities>();
			if (affectedByFacilities == null)
				return false;

			// Check each linked facility for an automated Vitals Monitor
			List<Thing> linkedFacilities = affectedByFacilities.LinkedFacilitiesListForReading;
			if (linkedFacilities == null)
				return false;

			foreach (Thing facility in linkedFacilities)
			{
				// Check if it's a Vitals Monitor
				if (facility.def.defName == MachineDefNames.VitalsMonitor)
				{
					// Check if it has subcore automation with a subcore installed
					CompSubcoreAutomationBase automationComp = facility.TryGetComp<CompSubcoreAutomationBase>();
					if (automationComp != null && automationComp.HasSubcoreInstalled && automationComp.IsAutomationEnabled)
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Prefix for SurgeryOutcomeSuccess.Apply - guarantee success when quality >= 0.98 and enhanced monitor present.
		/// This eliminates the 2% catastrophic failure chance for skilled surgeons.
		/// </summary>
		public static bool SurgeryOutcomeSuccess_Apply_Prefix(
			float quality,
			RecipeDef recipe,
			Pawn surgeon,
			Pawn patient,
			BodyPartRecord part,
			ref bool __result)
		{
			try
			{
				// Only intervene when quality is at or near the cap (98%+)
				if (quality < QualityThreshold)
					return true; // Let vanilla handle normal quality surgeries

				// Check if patient has an enhanced Vitals Monitor
				if (!HasEnhancedVitalsMonitor(patient))
					return true; // No enhanced monitor, let vanilla handle

				// Enhanced monitor present and quality is near cap - guarantee success
				__result = true;
				return false; // Skip vanilla - surgery is guaranteed successful
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in SurgeryOutcomeSuccess_Apply_Prefix: {ex.Message}", 93827500);
				return true; // Fall back to vanilla on error
			}
		}
	}
}
