using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Subcore Softscanner automation.
	/// When a Standard subcore is installed, scanning sickness duration is reduced by 1 day.
	/// </summary>
	public static class SubcoreSoftscannerPatches
	{
		/// <summary>
		/// Patch EjectContents to reduce scanning sickness severity when subcore is installed.
		/// The hediff is added inside EjectContents, so we postfix to modify it.
		/// </summary>
		[HarmonyPatch(typeof(Building_SubcoreScanner), nameof(Building_SubcoreScanner.EjectContents))]
		public static class Patch_EjectContents
		{
			// Only apply patch if Biotech is active (Subcore Scanners are from Biotech)
			public static bool Prepare() => ModsConfig.BiotechActive;

			public static void Prefix(Building_SubcoreScanner __instance, out Pawn __state)
			{
				// Store the occupant before ejection (will be null after)
				__state = __instance.Occupant;
			}

			public static void Postfix(Building_SubcoreScanner __instance, Pawn __state)
			{
				// Check if feature is enabled
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.softscannerFeaturesEnabled)
					return;

				// Only applies to softscanners (not ripscanners which kill the pawn)
				if (__instance.def.building.destroyBrain)
					return;

				// Check if we had an occupant
				if (__state == null)
					return;

				// Check if subcore is installed
				var subcoreComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp == null || !subcoreComp.HasSubcoreInstalled)
					return;

				// Find the scanning sickness hediff on the pawn
				Hediff hediff = __state.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.ScanningSickness);
				if (hediff == null)
					return;

				// Reduce severity by 1 (equivalent to 1 day less recovery)
				// Normal: 4 -> 3 days, Mechanitor: 2 -> 1 day
				hediff.Severity -= 1f;

				// Make sure we don't go below 0.01 (hediff would disappear immediately)
				if (hediff.Severity < 0.01f)
				{
					hediff.Severity = 0.01f;
				}
			}
		}
	}
}
