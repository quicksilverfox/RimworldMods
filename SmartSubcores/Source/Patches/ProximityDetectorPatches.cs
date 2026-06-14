using System;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using SubcoreAutomation.Handlers;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Helpers and Harmony patches that let an automated proximity detector
	/// "spot" psychologically invisible creatures: they become targetable to colonists,
	/// but shots at them have a large accuracy penalty (the marker is the last known position).
	/// </summary>
	[StaticConstructorOnStartup]
	public static class ProximityDetectorPatches
	{
		// Multiplier applied to the shooter accuracy factor when shooting a tracked invisible.
		// 0.3f = 70% accuracy penalty.
		private const float TrackedInvisibleAccuracyMultiplier = 0.3f;

		static ProximityDetectorPatches()
		{
			try
			{
				var harmony = new Harmony("SubcoreAutomation.ProximityDetectorPatches");

				// Treat tracked invisibles as visible (so they can be force-attacked).
				var isInvisibleMethod = AccessTools.Method(typeof(InvisibilityUtility), nameof(InvisibilityUtility.IsPsychologicallyInvisible));
				if (isInvisibleMethod != null)
					harmony.Patch(isInvisibleMethod, postfix: new HarmonyMethod(typeof(ProximityDetectorPatches), nameof(IsPsychologicallyInvisible_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Proximity detector patches BROKEN: InvisibilityUtility.IsPsychologicallyInvisible not found!");

				// Apply accuracy penalty when shooting at a tracked invisible.
				var hitReportFor = AccessTools.Method(typeof(ShotReport), nameof(ShotReport.HitReportFor),
					new[] { typeof(Thing), typeof(Verb), typeof(LocalTargetInfo) });
				if (hitReportFor != null)
					harmony.Patch(hitReportFor, postfix: new HarmonyMethod(typeof(ProximityDetectorPatches), nameof(HitReportFor_Postfix)));
				else
					Log.Error("[SubcoreAutomation] Proximity detector patches BROKEN: ShotReport.HitReportFor not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply proximity detector patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Returns true if pawn carries an invisibility hediff comp AND is currently tracked
		/// by an automated proximity detector on its map.
		/// </summary>
		public static bool IsTrackedInvisible(Pawn pawn)
		{
			if (pawn?.Map == null)
				return false;
			if (pawn.GetInvisibilityComp() == null)
				return false;

			foreach (Building building in pawn.Map.listerBuildings.allBuildingsColonist)
			{
				if (building.def.defName != "ProximityDetector")
					continue;

				var comp = building.GetComp<CompDefenseAutomation>();
				if (comp == null || !comp.IsProximityDetector || !comp.HasSubcoreInstalled || !comp.IsAutomationEnabled)
					continue;

				var tracked = comp.TrackedInvisibles;
				for (int i = 0; i < tracked.Count; i++)
				{
					if (tracked[i].pawn == pawn)
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Postfix for InvisibilityUtility.IsPsychologicallyInvisible.
		/// Treats tracked invisibles as not-invisible so colonists can target them.
		/// </summary>
		public static void IsPsychologicallyInvisible_Postfix(Pawn pawn, ref bool __result)
		{
			if (!__result)
				return;
			if (IsTrackedInvisible(pawn))
				__result = false;
		}

		/// <summary>
		/// Postfix for ShotReport.HitReportFor.
		/// Multiplies the shooter accuracy factor by TrackedInvisibleAccuracyMultiplier
		/// when the target is a tracked invisible (you only have the last known position).
		/// </summary>
		public static void HitReportFor_Postfix(ref ShotReport __result, LocalTargetInfo target)
		{
			if (!(target.Thing is Pawn pawn))
				return;
			if (!IsTrackedInvisible(pawn))
				return;

			var factorField = ReflectionManifest.ShotReport_factorFromShooterAndDist;
			if (factorField == null)
				return;

			try
			{
				float currentFactor = (float)factorField.GetValue(__result);
				float newFactor = Mathf.Max(currentFactor * TrackedInvisibleAccuracyMultiplier, 0.0201f);
				object boxed = __result;
				factorField.SetValue(boxed, newFactor);
				__result = (ShotReport)boxed;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Proximity detector accuracy patch failed: {ex.Message}", 0x1A2B3C);
			}
		}
	}
}
