using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Subcore Ripscanner enhancements.
	/// With a High subcore installed, organs are harvested from the subject.
	///
	/// Approach: Capture harvestable organs from the living pawn (Prefix),
	/// then after vanilla kills the pawn, spawn organs and remove them from the corpse (Postfix).
	/// </summary>
	[StaticConstructorOnStartup]
	public static class SubcoreRipscannerPatches
	{
		// Body part def names for harvestable organs
		private static readonly HashSet<string> HarvestableOrganDefs = new HashSet<string>
		{
			"Heart",
			"Liver",
			"Kidney",
			"Lung"
		};

		static SubcoreRipscannerPatches()
		{
			if (!Core.SubcoreAutomationMod.Settings.ripscannerFeaturesEnabled)
			{
				return;
			}
		}

		/// <summary>
		/// Checks if a pawn has the Deathless gene.
		/// </summary>
		public static bool IsDeathless(Pawn pawn)
		{
			if (!ModsConfig.BiotechActive)
				return false;

			return pawn.genes != null && pawn.genes.HasActiveGene(GeneDefOf.Deathless);
		}

		/// <summary>
		/// Gets all harvestable organs from a living pawn.
		/// Returns body parts that are present, healthy (no hediffs), and have a spawnThingOnRemoved.
		/// </summary>
		public static List<BodyPartRecord> GetHarvestableOrgans(Pawn pawn)
		{
			var result = new List<BodyPartRecord>();
			if (pawn?.health?.hediffSet == null || pawn.RaceProps.body == null)
				return result;

			var hediffSet = pawn.health.hediffSet;
			var notMissingParts = hediffSet.GetNotMissingParts().ToList();

			foreach (var part in notMissingParts)
			{
				// Only consider organs with spawnThingOnRemoved (natural organs)
				if (part.def.spawnThingOnRemoved == null)
					continue;

				// Check if this is one of our target organs
				if (!HarvestableOrganDefs.Contains(part.def.defName))
					continue;

				// Check if the organ is clean (no hediffs = healthy)
				if (!MedicalRecipesUtility.IsClean(pawn, part))
					continue;

				// Skip if pawn is an animal
				if (pawn.RaceProps.Animal)
					continue;

				// Check for mutant with non-droppable parts
				if (pawn.IsMutant && !pawn.mutant.Def.partsCleanAndDroppable)
					continue;

				result.Add(part);
			}

			return result;
		}
	}

	/// <summary>
	/// Patch for Building_SubcoreScanner.EjectContents to harvest organs.
	/// Uses Prefix to capture harvestable organs from living pawn,
	/// then Postfix to spawn organs and remove them from the corpse.
	/// </summary>
	[HarmonyPatch(typeof(Building_SubcoreScanner), nameof(Building_SubcoreScanner.EjectContents))]
	public static class Patch_RipscannerEjectContents
	{
		// Data captured in Prefix for use in Postfix
		public class HarvestData
		{
			public List<BodyPartRecord> Organs;
			public bool IsDeathless;
			public IntVec3 Position;
			public Map Map;
			public string PawnLabel;
		}

		public static bool Prepare() => ModsConfig.BiotechActive && Core.SubcoreAutomationMod.Settings.ripscannerFeaturesEnabled;

		/// <summary>
		/// Prefix: Capture harvestable organs from the LIVING pawn before vanilla kills them.
		/// </summary>
		public static void Prefix(Building_SubcoreScanner __instance, out HarvestData __state)
		{
			__state = null;

			// Only for ripscanners (destroyBrain = true)
			if (!__instance.DestroyOccupantBrain)
				return;

			var pawn = __instance.Occupant;
			if (pawn == null || pawn.Dead)
				return;

			// Check if this ripscanner has a subcore installed
			var subcoreComp = __instance.TryGetComp<Core.CompSubcoreAutomationBase>();
			if (subcoreComp == null || !subcoreComp.HasSubcoreInstalled)
				return;

			// Capture harvestable organs while pawn is still alive
			var organs = SubcoreRipscannerPatches.GetHarvestableOrgans(pawn);
			if (organs.Count == 0)
				return;

			__state = new HarvestData
			{
				Organs = organs,
				IsDeathless = SubcoreRipscannerPatches.IsDeathless(pawn),
				Position = __instance.InteractionCell,
				Map = __instance.Map,
				PawnLabel = pawn.LabelShort
			};
		}

		/// <summary>
		/// Postfix: After vanilla has killed the pawn and created a corpse,
		/// spawn the organs and remove them from the corpse.
		/// </summary>
		public static void Postfix(Building_SubcoreScanner __instance, HarvestData __state)
		{
			if (__state == null || __state.Organs == null || __state.Organs.Count == 0)
				return;

			// Find the corpse at or near the interaction cell
			Corpse corpse = null;
			foreach (var thing in __state.Position.GetThingList(__state.Map))
			{
				if (thing is Corpse c)
				{
					corpse = c;
					break;
				}
			}

			// If not at exact position, check nearby cells
			if (corpse == null)
			{
				foreach (var cell in GenAdj.CellsAdjacent8Way(__state.Position, Rot4.North, IntVec2.One))
				{
					if (!cell.InBounds(__state.Map))
						continue;
					foreach (var thing in cell.GetThingList(__state.Map))
					{
						if (thing is Corpse c)
						{
							corpse = c;
							break;
						}
					}
					if (corpse != null)
						break;
				}
			}

			// Determine how many organs to harvest
			int maxOrgans = Core.SubcoreAutomationMod.Settings.ripscannerOrganCount;
			int organsToHarvest = __state.IsDeathless ? __state.Organs.Count : System.Math.Min(maxOrgans, __state.Organs.Count);

			// Shuffle and select organs
			var selectedOrgans = __state.Organs.InRandomOrder().Take(organsToHarvest).ToList();
			var spawnedOrgans = new List<Thing>();

			foreach (var part in selectedOrgans)
			{
				var thingDef = part.def.spawnThingOnRemoved;
				if (thingDef == null)
					continue;

				// Spawn the organ
				Thing organ = ThingMaker.MakeThing(thingDef);
				GenPlace.TryPlaceThing(organ, __state.Position, __state.Map, ThingPlaceMode.Near);
				spawnedOrgans.Add(organ);

				// Remove the organ from the corpse (add missing part hediff)
				if (corpse?.InnerPawn?.health != null)
				{
					var corpsePawn = corpse.InnerPawn;
					Hediff_MissingPart missingPart = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, corpsePawn);
					missingPart.IsFresh = true;
					missingPart.lastInjury = HediffDefOf.SurgicalCut;
					corpsePawn.health.AddHediff(missingPart, part);
				}
			}

			// Show message
			if (spawnedOrgans.Count > 0)
			{
				string organList = string.Join(", ", spawnedOrgans.Select(o => o.Label));
				if (__state.IsDeathless)
				{
					Messages.Message(
						"SubcoreAutomation_RipscannerHarvestedDeathless".Translate(__state.PawnLabel, spawnedOrgans.Count, organList),
						spawnedOrgans.FirstOrDefault(),
						MessageTypeDefOf.PositiveEvent);
				}
				else
				{
					Messages.Message(
						"SubcoreAutomation_RipscannerHarvested".Translate(__state.PawnLabel, spawnedOrgans.Count, organList),
						spawnedOrgans.FirstOrDefault(),
						MessageTypeDefOf.PositiveEvent);
				}
			}
		}
	}
}
