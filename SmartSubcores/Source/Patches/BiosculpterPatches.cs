using System;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Biosculpter Pod automation.
	/// With High subcore installed:
	/// - Pod accepts any pawn (no tuning required)
	/// - Cycle speed is as if pod is tuned to the occupant
	/// </summary>
	[StaticConstructorOnStartup]
	public static class BiosculpterPatches
	{
		static BiosculpterPatches()
		{
			try
			{
				// Check if Ideology is active (Biosculpter is from Ideology)
				if (!ModsConfig.IdeologyActive)
				{
					// Ideology not active, skipping biosculpter patches
					return;
				}

				// Check if feature is enabled in settings
				if (!SubcoreAutomationMod.Settings.biosculpterTuningPatchEnabled)
				{
					// Biosculpter tuning patch disabled in settings
					return;
				}

				var harmony = new Harmony("SubcoreAutomation.BiosculpterPatches");

				// Patch CanAcceptOnceCycleChosen to bypass tuning check
				var canAcceptMethod = AccessTools.Method(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.CanAcceptOnceCycleChosen));
				if (canAcceptMethod != null)
				{
					var canAcceptPostfix = new HarmonyMethod(typeof(BiosculpterPatches), nameof(CanAcceptOnceCycleChosen_Postfix));
					canAcceptPostfix.priority = Priority.Low;
					harmony.Patch(canAcceptMethod, postfix: canAcceptPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Biosculpter patches BROKEN: CompBiosculpterPod.CanAcceptOnceCycleChosen not found!");

				// Patch BiotunedSpeedFactor getter to return tuned speed when automated
				var speedGetter = AccessTools.PropertyGetter(typeof(CompBiosculpterPod), "BiotunedSpeedFactor");
				if (speedGetter != null)
				{
					var speedPostfix = new HarmonyMethod(typeof(BiosculpterPatches), nameof(BiotunedSpeedFactor_Postfix));
					speedPostfix.priority = Priority.Low;
					harmony.Patch(speedGetter, postfix: speedPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Biosculpter patches BROKEN: CompBiosculpterPod.BiotunedSpeedFactor getter not found!");

				// Patch CannotUseNowPawnReason to not show "biotuned to another" message
				var reasonMethod = AccessTools.Method(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.CannotUseNowPawnReason));
				if (reasonMethod != null)
				{
					var reasonPostfix = new HarmonyMethod(typeof(BiosculpterPatches), nameof(CannotUseNowPawnReason_Postfix));
					reasonPostfix.priority = Priority.Low;
					harmony.Patch(reasonMethod, postfix: reasonPostfix);
				}
				else
					Log.Error("[SubcoreAutomation] Biosculpter patches BROKEN: CompBiosculpterPod.CannotUseNowPawnReason not found!");

				// Patch SetBiotuned to prevent biotuning when automated
				var setBiotunedMethod = AccessTools.Method(typeof(CompBiosculpterPod), nameof(CompBiosculpterPod.SetBiotuned));
				if (setBiotunedMethod != null)
				{
					var setBiotunedPrefix = new HarmonyMethod(typeof(BiosculpterPatches), nameof(SetBiotuned_Prefix));
					setBiotunedPrefix.priority = Priority.Low;
					harmony.Patch(setBiotunedMethod, prefix: setBiotunedPrefix);
				}
				else
					Log.Error("[SubcoreAutomation] Biosculpter patches BROKEN: CompBiosculpterPod.SetBiotuned not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply biosculpter patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if a biosculpter pod has an active subcore automation.
		/// </summary>
		private static bool HasActiveSubcore(CompBiosculpterPod pod)
		{
			if (pod?.parent == null)
				return false;

			var automationComp = pod.parent.TryGetComp<CompSubcoreAutomationBase>();
			return automationComp != null && automationComp.HasSubcoreInstalled && automationComp.IsAutomationEnabled;
		}

		/// <summary>
		/// Postfix for CanAcceptOnceCycleChosen - allow any pawn when subcore installed.
		/// Bypasses the "biotuned to another pawn" restriction.
		/// </summary>
		public static void CanAcceptOnceCycleChosen_Postfix(CompBiosculpterPod __instance, Pawn pawn, ref bool __result)
		{
			// Only intervene if vanilla rejected due to tuning
			if (__result)
				return;

			// Check if automated
			if (!HasActiveSubcore(__instance))
				return;

			// Re-check other conditions (power, state) but skip tuning check
			if (__instance.State != BiosculpterPodState.SelectingCycle)
				return;

			var powerComp = __instance.parent.TryGetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return;

			// Subcore bypasses tuning requirement
			__result = true;
		}

		/// <summary>
		/// Postfix for BiotunedSpeedFactor - return tuned speed factor when subcore installed.
		/// This ensures the pod operates at full tuned speed for any occupant.
		/// </summary>
		public static void BiotunedSpeedFactor_Postfix(CompBiosculpterPod __instance, ref float __result)
		{
			// Only intervene if not already getting tuned bonus
			if (__result > 1f)
				return;

			// Check if automated
			if (!HasActiveSubcore(__instance))
				return;

			// Return the tuned speed factor from props
			__result = __instance.Props.biotunedCycleSpeedFactor;
		}

		/// <summary>
		/// Postfix for CannotUseNowPawnReason - don't show "biotuned to another" when automated.
		/// </summary>
		public static void CannotUseNowPawnReason_Postfix(CompBiosculpterPod __instance, Pawn p, ref string __result)
		{
			// Only intervene if the reason is about biotuning
			if (__result == null)
				return;

			// Check if automated
			if (!HasActiveSubcore(__instance))
				return;

			// Clear the tuning-related rejection reason
			// The actual translation key is "BiosculpterBiotunedToAnother"
			if (__result.Contains("biotuned") || __result.Contains("Biotuned"))
			{
				__result = null;
			}
		}

		/// <summary>
		/// Prefix for SetBiotuned - prevent biotuning when automated.
		/// Vanilla calls this after cycle completion, which would lock the pod to one pawn.
		/// </summary>
		public static bool SetBiotuned_Prefix(CompBiosculpterPod __instance, Pawn newBiotunedTo)
		{
			// Allow clearing biotuning (null)
			if (newBiotunedTo == null)
				return true;

			// Check if automated - skip biotuning
			if (HasActiveSubcore(__instance))
				return false; // Skip vanilla method

			return true; // Run vanilla method
		}
	}
}
