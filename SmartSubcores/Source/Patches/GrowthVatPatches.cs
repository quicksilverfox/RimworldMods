using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Growth Vat automation.
	/// - Embryo birth quality: 70% -> 85%
	/// - Vat learning: 8000 XP/3 days random -> 4000 XP/day passion-focused
	/// </summary>
	[StaticConstructorOnStartup]
	public static class GrowthVatPatches
	{
		private const float AutomatedBirthQuality = 0.85f;
		private const float AutomatedXPPerLearn = 4000f;

		static GrowthVatPatches()
		{
			if (!ModsConfig.BiotechActive)
				return;

			try
			{
				var harmony = new Harmony("SubcoreAutomation.GrowthVatPatches");
				int patchedCount = 0;

				// Patch EmbryoBirth to improve birth quality when automated
				// Only if setting enabled - HIGH RISK patch that replaces vanilla logic
				if (SubcoreAutomationMod.Settings.growthVatEmbryoPatchEnabled)
				{
					var embryoBirth = AccessTools.Method(typeof(Building_GrowthVat), "EmbryoBirth");
					if (embryoBirth != null)
					{
						harmony.Patch(embryoBirth, prefix: new HarmonyMethod(typeof(GrowthVatPatches), nameof(EmbryoBirth_Prefix)));
						patchedCount++;
					}
					else
						Log.Error("[SubcoreAutomation] Growth Vat patches BROKEN: EmbryoBirth method not found!");
				}

				// Patch Hediff_VatLearning.Learn to prefer passions and award different XP
				var learn = AccessTools.Method(typeof(Hediff_VatLearning), "Learn");
				if (learn != null)
				{
					harmony.Patch(learn, prefix: new HarmonyMethod(typeof(GrowthVatPatches), nameof(Learn_Prefix)));
					patchedCount++;
				}
				else
					Log.Error("[SubcoreAutomation] Growth Vat patches BROKEN: Hediff_VatLearning.Learn method not found!");

				// Patch severity gain rate for VatLearning hediff (0.33 -> 1.0 per day when automated)
				var severityChange = AccessTools.Method(typeof(HediffComp_SeverityPerDay), "SeverityChangePerDay");
				if (severityChange != null)
				{
					harmony.Patch(severityChange, postfix: new HarmonyMethod(typeof(GrowthVatPatches), nameof(SeverityChangePerDay_Postfix)));
					patchedCount++;
				}
				else
					Log.Error("[SubcoreAutomation] Growth Vat patches BROKEN: SeverityChangePerDay method not found!");

				// Band node bandwidth boost is implemented by Patch_HediffBandNode_RecacheBandNodes
				// (see Source/Patches/BandNodePatches.cs), which folds the +1 automation
				// bonus into vanilla's persisted cachedTunedBandNodesCount.
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Growth Vat patches BROKEN: {ex}");
			}
		}

		/// <summary>
		/// Check if a pawn in growth vat has automated vat.
		/// </summary>
		private static bool IsInAutomatedVat(Pawn pawn)
		{
			if (pawn?.ParentHolder is Building_GrowthVat vat)
			{
				var automationComp = vat.TryGetComp<CompSubcoreAutomationBase>();
				return automationComp != null && automationComp.SubcoreInstalled && automationComp.IsAutomationEnabled;
			}
			return false;
		}

		/// <summary>
		/// Prefix for EmbryoBirth - use improved birth quality when automated.
		/// Replaces the entire method to use 0.85f instead of 0.7f.
		/// </summary>
		public static bool EmbryoBirth_Prefix(Building_GrowthVat __instance)
		{
			try
			{
				// Check if automated
				var automationComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
				{
					return true; // Let vanilla handle non-automated vats (uses 0.7f)
				}

				// Access private fields via reflection
				var selectedEmbryoField = AccessTools.Field(typeof(Building_GrowthVat), "selectedEmbryo");
				var innerContainerField = AccessTools.Field(typeof(Building_Enterable), "innerContainer");
				var startTickField = AccessTools.Field(typeof(Building_Enterable), "startTick");
				var embryoStarvationField = AccessTools.Field(typeof(Building_GrowthVat), "embryoStarvation");

				if (selectedEmbryoField == null || innerContainerField == null || startTickField == null || embryoStarvationField == null)
					return true; // Reflection failed, let vanilla handle

				HumanEmbryo selectedEmbryo = selectedEmbryoField.GetValue(__instance) as HumanEmbryo;
				ThingOwner innerContainer = innerContainerField.GetValue(__instance) as ThingOwner;
				int startTick = startTickField.GetValue(__instance) is int st ? st : 0;
				float embryoStarvation = embryoStarvationField.GetValue(__instance) is float es ? es : 0f;

				if (selectedEmbryo == null || !innerContainer.Contains(selectedEmbryo) || startTick > Find.TickManager.TicksGame)
				{
					return true; // Conditions not met, let vanilla handle
				}

				// Perform birth with improved quality (0.85f instead of 0.7f)
				Precept_Ritual ritual = Faction.OfPlayer.ideos?.PrimaryIdeo?.GetPrecept(PreceptDefOf.ChildBirth) as Precept_Ritual;
				
				var birthWorker = (RitualOutcomeEffectWorker_ChildBirth)RitualOutcomeEffectDefOf.ChildBirth.GetInstance();
				RitualOutcomePossibility outcome = birthWorker.GetOutcome(AutomatedBirthQuality, null);

				Thing thing = PregnancyUtility.ApplyBirthOutcome(
					outcome,
					AutomatedBirthQuality,
					ritual,
					selectedEmbryo?.GeneSet?.GenesListForReading,
					selectedEmbryo.Mother,
					__instance,
					selectedEmbryo.Father
				);

				// Apply biostarvation if needed (same as vanilla)
				if (thing != null && embryoStarvation > 0f)
				{
					Pawn pawn = (thing is Corpse corpse) ? corpse.InnerPawn : (Pawn)thing;
					Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.BioStarvation, pawn);
					hediff.Severity = UnityEngine.Mathf.Lerp(0f, HediffDefOf.BioStarvation.maxSeverity, embryoStarvation);
					pawn.health.AddHediff(hediff);
				}

				// Growth vat birth with enhanced quality (85%)

				return false; // Skip vanilla
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Error in EmbryoBirth_Prefix: {ex.Message}\n{ex.StackTrace}");
				return true; // Fall back to vanilla on error
			}
		}

		/// <summary>
		/// Prefix for Hediff_VatLearning.Learn - use passion-focused learning for automated vats.
		/// </summary>
		public static bool Learn_Prefix(Hediff_VatLearning __instance)
		{
			try
			{
				Pawn pawn = __instance.pawn;
				if (pawn?.skills == null)
					return true;

				if (!IsInAutomatedVat(pawn))
					return true; // Let vanilla handle non-automated vats

				// Get non-disabled skills
				var availableSkills = pawn.skills.skills.Where(x => !x.TotallyDisabled).ToList();
				if (availableSkills.Count == 0)
					return true;

				// Prefer skills with passions: major > minor > none
				SkillRecord selectedSkill = null;

				// First try major passion
				var majorPassionSkills = availableSkills.Where(x => x.passion == Passion.Major).ToList();
				if (majorPassionSkills.Count > 0)
				{
					selectedSkill = majorPassionSkills.RandomElement();
				}
				else
				{
					// Then try minor passion
					var minorPassionSkills = availableSkills.Where(x => x.passion == Passion.Minor).ToList();
					if (minorPassionSkills.Count > 0)
					{
						selectedSkill = minorPassionSkills.RandomElement();
					}
					else
					{
						// Fall back to random
						selectedSkill = availableSkills.RandomElement();
					}
				}

				if (selectedSkill != null)
				{
					selectedSkill.Learn(AutomatedXPPerLearn, direct: true);
				}

				// Reset severity
				__instance.Severity = __instance.def.initialSeverity;

				return false; // Skip vanilla
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in Learn_Prefix: {ex.Message}", 93827490);
				return true;
			}
		}

		/// <summary>
		/// Postfix for HediffComp_SeverityPerDay.SeverityChangePerDay - accelerate VatLearning for automated vats.
		/// Changes from 0.33/day to 1.0/day (triggers daily instead of every 3 days).
		/// </summary>
		public static void SeverityChangePerDay_Postfix(HediffComp_SeverityPerDay __instance, ref float __result)
		{
			try
			{
				// Only affect VatLearning hediff
				if (__instance.parent.def != HediffDefOf.VatLearning)
					return;

				Pawn pawn = __instance.Pawn;
				if (!IsInAutomatedVat(pawn))
					return;

				// Accelerate severity gain: 0.33/day -> 1.0/day (3x faster)
				// This makes learning trigger daily instead of every 3 days
				__result *= 3f;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in SeverityChangePerDay_Postfix: {ex.Message}", 93827491);
			}
		}

	}
}
