using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Gene Extractor automation.
	/// When a subcore is installed:
	/// - Pawns hibernate during extraction (needs are suspended)
	/// - Recovery time is reduced to minimum 12 days
	/// - Gene extraction is targeted to prefer new genes not already in gene banks
	/// </summary>
	public static class GeneExtractorPatches
	{
		/// <summary>
		/// Patch IsContentsSuspended to return true when subcore is installed.
		/// This causes the pawn to hibernate during extraction, preventing need degradation.
		/// </summary>
		[HarmonyPatch(typeof(Building_GeneExtractor), nameof(Building_GeneExtractor.IsContentsSuspended), MethodType.Getter)]
		public static class Patch_IsContentsSuspended
		{
			// Only apply patch if Biotech is active
			public static bool Prepare() => ModsConfig.BiotechActive;

			public static void Postfix(Building_GeneExtractor __instance, ref bool __result)
			{
				// Check if feature is enabled
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.geneExtractorFeaturesEnabled)
					return;

				// If already suspended, don't change
				if (__result)
					return;

				// Check if subcore is installed
				var subcoreComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp != null && subcoreComp.HasSubcoreInstalled)
				{
					__result = true;
				}
			}
		}

		/// <summary>
		/// Prefix patch for GeneUtility.ExtractXenogerm to modify the regrowth duration.
		/// Reduces the random range to always be the minimum (12 days) when subcore is installed.
		/// </summary>
		[HarmonyPatch(typeof(GeneUtility), nameof(GeneUtility.ExtractXenogerm))]
		public static class Patch_ExtractXenogerm
		{
			// Only apply patch if Biotech is active
			public static bool Prepare() => ModsConfig.BiotechActive;

			// Store the modified duration before the call
			private static int? _modifiedDuration;

			public static void Prefix(Pawn pawn, int overrideDurationTicks)
			{
				// Check if feature is enabled
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.geneExtractorFeaturesEnabled)
				{
					_modifiedDuration = null;
					return;
				}

				// Check if this pawn is in a gene extractor with subcore
				if (pawn?.ParentHolder is Building_GeneExtractor extractor)
				{
					var subcoreComp = extractor.TryGetComp<CompSubcoreAutomationBase>();
					if (subcoreComp != null && subcoreComp.HasSubcoreInstalled)
					{
						// Calculate minimum duration (12 days in ticks)
						int minDuration = Mathf.RoundToInt(60000f * 12f);
						
						// If the passed duration is greater than minimum, we want to reduce it
						// But we can't change the parameter directly, so we'll handle this in postfix
						// by modifying the hediff after it's applied
						if (overrideDurationTicks > minDuration)
						{
							_modifiedDuration = minDuration;
						}
						else
						{
							_modifiedDuration = null;
						}
					}
					else
					{
						_modifiedDuration = null;
					}
				}
				else
				{
					_modifiedDuration = null;
				}
			}

			public static void Postfix(Pawn pawn)
			{
				// If we need to modify the duration, find the hediff and adjust it
				if (_modifiedDuration.HasValue && pawn?.health?.hediffSet != null)
				{
					Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating);
					if (hediff != null)
					{
						HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
						if (disappears != null)
						{
							disappears.ticksToDisappear = _modifiedDuration.Value;
						}
					}
					_modifiedDuration = null;
				}
			}
		}

		/// <summary>
		/// Helper method to get all genes currently stored in gene banks on the map.
		/// </summary>
		public static HashSet<GeneDef> GetKnownGenes(Map map)
		{
			HashSet<GeneDef> knownGenes = new HashSet<GeneDef>();

			if (map == null)
				return knownGenes;

			// Find all gene bank buildings on the map
			foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
			{
				CompGenepackContainer container = thing.TryGetComp<CompGenepackContainer>();
				if (container != null)
				{
					foreach (Genepack genepack in container.ContainedGenepacks)
					{
						if (genepack?.GeneSet?.GenesListForReading != null)
						{
							foreach (GeneDef gene in genepack.GeneSet.GenesListForReading)
							{
								knownGenes.Add(gene);
							}
						}
					}
				}
			}

			// Also check genepacks on the ground or in storage
			foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDefOf.Genepack))
			{
				if (thing is Genepack genepack && genepack.GeneSet?.GenesListForReading != null)
				{
					foreach (GeneDef gene in genepack.GeneSet.GenesListForReading)
					{
						knownGenes.Add(gene);
					}
				}
			}

			return knownGenes;
		}

		/// <summary>
		/// Gets extractable genes from a pawn that are not already known.
		/// </summary>
		public static List<GeneDef> GetNewExtractableGenes(Pawn pawn, HashSet<GeneDef> knownGenes)
		{
			List<GeneDef> newGenes = new List<GeneDef>();

			if (pawn?.genes?.GenesListForReading == null)
				return newGenes;

			foreach (Gene gene in pawn.genes.GenesListForReading)
			{
				// Skip non-extractable genes (same criteria as vanilla)
				if (gene.def.biostatArc > 0) // Archite genes
					continue;
				if (gene.def.endogeneCategory == EndogeneCategory.Melanin) // Melanin genes
					continue;
				if (!gene.def.passOnDirectly) // Non-heritable genes
					continue;

				// Check if this gene is new
				if (!knownGenes.Contains(gene.def))
				{
					newGenes.Add(gene.def);
				}
			}

			return newGenes;
		}

		/// <summary>
		/// Postfix patch for Building_GeneExtractor's private Finish method.
		/// Modifies the extracted genes to prefer new ones not already in gene banks.
		/// </summary>
		[HarmonyPatch(typeof(Building_GeneExtractor), "Finish")]
		public static class Patch_Finish
		{
			// Only apply patch if Biotech is active
			public static bool Prepare() => ModsConfig.BiotechActive;

			public static void Prefix(Building_GeneExtractor __instance, out HashSet<GeneDef> __state)
			{
				__state = null;

				// Check if feature is enabled
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.geneExtractorFeaturesEnabled)
					return;

				// Get the contained pawn
				Pawn pawn = __instance.innerContainer.FirstOrDefault() as Pawn;
				if (pawn == null)
					return;

				// Check if subcore is installed
				var subcoreComp = __instance.TryGetComp<CompSubcoreAutomationBase>();
				if (subcoreComp == null || !subcoreComp.HasSubcoreInstalled)
					return;

				// Get known genes for targeted extraction
				__state = GetKnownGenes(__instance.Map);
			}

			public static void Postfix(Building_GeneExtractor __instance, HashSet<GeneDef> __state)
			{
				// If we didn't have state (no subcore), skip
				if (__state == null)
					return;

				// Find the genepack that was just created (should be near the interaction cell)
				IntVec3 dropCell = __instance.def.hasInteractionCell ? __instance.InteractionCell : __instance.Position;
				Genepack createdPack = null;

				foreach (Thing thing in dropCell.GetThingList(__instance.Map))
				{
					if (thing is Genepack gp)
					{
						createdPack = gp;
						break;
					}
				}

				// Also check nearby cells
				if (createdPack == null)
				{
					foreach (IntVec3 cell in GenRadial.RadialCellsAround(dropCell, 2f, true))
					{
						if (!cell.InBounds(__instance.Map))
							continue;

						foreach (Thing thing in cell.GetThingList(__instance.Map))
						{
							if (thing is Genepack gp)
							{
								createdPack = gp;
								break;
							}
						}
						if (createdPack != null)
							break;
					}
				}

				if (createdPack?.GeneSet?.GenesListForReading == null)
					return;

				// Check if all genes in the pack are already known
				List<GeneDef> packGenes = createdPack.GeneSet.GenesListForReading.ToList();
				bool allKnown = packGenes.All(g => __state.Contains(g));

				if (!allKnown)
					return; // At least one new gene, no modification needed

				// Get the pawn that was just ejected
				Pawn pawn = null;
				foreach (IntVec3 cell in GenRadial.RadialCellsAround(dropCell, 3f, true))
				{
					if (!cell.InBounds(__instance.Map))
						continue;

					foreach (Thing thing in cell.GetThingList(__instance.Map))
					{
						if (thing is Pawn p && p.RaceProps.Humanlike)
						{
							pawn = p;
							break;
						}
					}
					if (pawn != null)
						break;
				}

				if (pawn == null)
					return;

				// Get new genes from this pawn
				List<GeneDef> newGenes = GetNewExtractableGenes(pawn, __state);

				if (newGenes.Count == 0)
					return; // No new genes available from this pawn

				// Replace one of the pack's genes with a new one
				GeneDef newGene = newGenes.RandomElement();
				
				// Find a gene to replace (prefer genes that won't break biostat constraints)
				GeneDef geneToReplace = null;
				foreach (GeneDef gene in packGenes)
				{
					// Check if replacing this gene with the new one would keep valid biostats
					int currentMet = packGenes.Sum(g => g.biostatMet);
					int newMet = currentMet - gene.biostatMet + newGene.biostatMet;
					
					if (newMet >= GeneTuning.BiostatRange.min && newMet <= GeneTuning.BiostatRange.max)
					{
						geneToReplace = gene;
						break;
					}
				}

				// If no valid replacement found, just replace the first one
				if (geneToReplace == null && packGenes.Count > 0)
				{
					geneToReplace = packGenes[0];
				}

				if (geneToReplace == null)
					return;

				// Replace the old genepack with a new one containing the modified gene list
				List<GeneDef> newPackGenes = packGenes.ToList();
				newPackGenes.Remove(geneToReplace);
				newPackGenes.Add(newGene);

				// Destroy old genepack and create new one
				IntVec3 packPos = createdPack.Position;
				Map packMap = createdPack.Map;
				createdPack.Destroy();

				// Create new genepack with modified genes
				Genepack newPack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
				foreach (GeneDef gene in newPackGenes)
				{
					newPack.GeneSet.AddGene(gene);
				}
				GenPlace.TryPlaceThing(newPack, packPos, packMap, ThingPlaceMode.Near);
				createdPack = newPack;

				// Send a message about the targeted extraction
				Messages.Message(
					"SubcoreAutomation_GeneExtractorTargeted".Translate(newGene.label),
					createdPack,
					MessageTypeDefOf.PositiveEvent);
			}
		}
	}
}
