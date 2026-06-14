using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.Sound;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Toxifier generator integration.
	/// When a subcore is installed, the Toxifier produces wastepacks instead of polluting terrain.
	/// Power output is reduced by 100W as a trade-off for cleaner operation.
	/// </summary>
	public static class ToxifierPatches
	{
		// Power reduction when subcore is installed (100W less output)
		private const float ToxifierPowerReduction = 100f;

		/// <summary>
		/// Prefix patch for CompToxifier.PolluteNextCell.
		/// Intercepts pollution and produces wastepacks instead when subcore is installed.
		/// </summary>
		[HarmonyPatch(typeof(CompToxifier), "PolluteNextCell")]
		public static class Patch_CompToxifier_PolluteNextCell
		{
			// Only apply patch if Biotech is active (Toxifier is from Biotech)
			public static bool Prepare() => ModsConfig.BiotechActive;

			public static bool Prefix(CompToxifier __instance)
			{
				// Check if feature is enabled
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.toxifierWastepackEnabled)
				{
					return true; // Feature disabled, normal behavior
				}

				// Check if parent has our comp with subcore installed
				var subcoreComp = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp == null || !subcoreComp.HasSubcoreInstalled)
				{
					return true; // Normal behavior
				}

				// Produce a wastepack (drop on ground)
				ProduceWastepack(__instance.parent);

				// Skip the original pollution method
				return false;
			}
		}

		/// <summary>
		/// Postfix patch on CompPowerPlant.UpdateDesiredPowerOutput to reduce Toxifier output.
		/// Called after the base power output is calculated.
		/// </summary>
		[HarmonyPatch(typeof(CompPowerPlant), "UpdateDesiredPowerOutput")]
		public static class Patch_CompPowerPlant_UpdateDesiredPowerOutput
		{
			// Only apply patch if Biotech is active (Toxifier is from Biotech)
			public static bool Prepare() => ModsConfig.BiotechActive;

			public static void Postfix(CompPowerPlant __instance)
			{
				// Check if feature is enabled
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.toxifierWastepackEnabled)
					return;

				// Only for Toxifier generators
				if (__instance.parent.def.defName != "ToxifierGenerator")
					return;

				// Check if subcore is installed
				var subcoreComp = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp == null || !subcoreComp.HasSubcoreInstalled)
					return;

				// Reduce power output by 100W
				// PowerOutput is POSITIVE for generators (e.g., +1400 means producing 1400W)
				// We subtract to reduce output
				__instance.PowerOutput -= ToxifierPowerReduction;
			}
		}

		/// <summary>
		/// Produces a toxic wastepack and drops it near the generator.
		/// </summary>
		private static void ProduceWastepack(ThingWithComps parent)
		{
			// Create the wastepack
			Thing wastepack = ThingMaker.MakeThing(ThingDefOf.Wastepack);
			wastepack.stackCount = 1;

			// Drop it near the generator
			GenPlace.TryPlaceThing(wastepack, parent.Position, parent.Map, ThingPlaceMode.Near);

			// Play effects similar to normal pollution
			FleckMaker.Static(parent.TrueCenter(), parent.Map, FleckDefOf.Fleck_ToxifierPollutionSource);
			SoundDefOf.Toxifier_Pollute.PlayOneShot(parent);
		}
	}
}
