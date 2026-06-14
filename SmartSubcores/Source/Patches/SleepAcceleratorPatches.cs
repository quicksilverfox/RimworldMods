using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Patches for Sleep Accelerator automation.
	/// When High subcore is installed: sleeping provides lucid dreams that satisfy
	/// recreation need and boost mood, without gaining recreation tolerance.
	/// </summary>
	[HarmonyPatch]
	public static class SleepAcceleratorPatches
	{
		// Recreation gained per sleep session (roughly equivalent to watching TV for a few hours)
		private const float RecreationGainPerSleep = 0.25f;

		private static ThoughtDef _lucidDreamsThought;
		private static ThoughtDef LucidDreamsThought
		{
			get
			{
				if (_lucidDreamsThought == null)
				{
					_lucidDreamsThought = DefDatabase<ThoughtDef>.GetNamed("SubcoreAutomation_LucidDreams", false);
				}
				return _lucidDreamsThought;
			}
		}

		/// <summary>
		/// Patch FinalizeLayingJob to apply lucid dreams effect when waking up.
		/// </summary>
		[HarmonyPatch(typeof(Toils_LayDown), "FinalizeLayingJob")]
		[HarmonyPostfix]
		public static void FinalizeLayingJob_Postfix(Pawn pawn, Building_Bed bed)
		{
			if (!SubcoreAutomationMod.Settings.sleepAcceleratorFeaturesEnabled)
				return;

			if (pawn == null || bed == null)
				return;

			// Check if pawn has joy need
			if (pawn.needs?.joy == null)
				return;

			// Check if pawn has mood (for thought)
			if (pawn.needs?.mood == null)
				return;

			// Check if bed has a linked sleep accelerator with subcore
			if (!HasEnhancedSleepAccelerator(bed))
				return;

			// Apply lucid dreams effect
			ApplyLucidDreamsEffect(pawn);
		}

		/// <summary>
		/// Checks if the bed has a linked sleep accelerator with subcore installed.
		/// </summary>
		private static bool HasEnhancedSleepAccelerator(Building_Bed bed)
		{
			var facilitiesComp = bed.TryGetComp<CompAffectedByFacilities>();
			if (facilitiesComp == null)
				return false;

			foreach (var facility in facilitiesComp.LinkedFacilitiesListForReading)
			{
				if (facility.def.defName != "SleepAccelerator")
					continue;

				// Check if facility is active (powered, etc.)
				if (!facilitiesComp.IsFacilityActive(facility))
					continue;

				// Check for subcore
				var subcoreComp = facility.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp != null && subcoreComp.SubcoreInstalled)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Applies the lucid dreams effect: recreation gain without tolerance, and mood boost.
		/// </summary>
		private static void ApplyLucidDreamsEffect(Pawn pawn)
		{
			// Add recreation directly to bypass tolerance
			// CurLevel is clamped 0-1 automatically
			float currentJoy = pawn.needs.joy.CurLevel;
			float newJoy = currentJoy + RecreationGainPerSleep;
			pawn.needs.joy.CurLevel = newJoy;

			// Add lucid dreams thought for mood boost
			if (LucidDreamsThought != null)
			{
				pawn.needs.mood.thoughts.memories.TryGainMemory(LucidDreamsThought);
			}
		}
	}
}
