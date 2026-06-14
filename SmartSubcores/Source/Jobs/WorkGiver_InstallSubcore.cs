using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Jobs
{
	/// <summary>
	/// Work giver for installing subcores into buildings.
	/// </summary>
	public class WorkGiver_InstallSubcore : WorkGiver_Scanner
	{
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

		public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			// Allow Constructoids (mechs that can do construction work)
			if (pawn.RaceProps.IsMechanoid)
			{
				if (pawn.workSettings == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction))
					return true;
			}

			return base.ShouldSkip(pawn, forced);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (t.Faction != Faction.OfPlayer)
				return false;

			CompSubcoreAutomationBase comp = t.TryGetComp<CompSubcoreAutomationBase>();
			if (comp == null)
				return false;

			if (!comp.InstallationRequested)
				return false;

			if (comp.SubcoreInstalled)
				return false;

			// Check if we can reach the building
			if (!pawn.CanReserveAndReach(t, PathEndMode.InteractionCell, Danger.Deadly, 1, -1, null, forced))
				return false;

			// Fallback mode: check if materials are available
			if (SubcoreFallback.IsActive)
			{
				return SubcoreFallback.HasEnoughMaterials(t.Map, comp.Props.subcoreDef);
			}

			// Normal mode: check if there's a subcore available
			Thing subcore = comp.FindSubcoreOnMap(comp.Props.subcoreDef);
			if (subcore == null)
				return false;

			if (!pawn.CanReserveAndReach(subcore, PathEndMode.ClosestTouch, Danger.Deadly, 1, -1, null, forced))
				return false;

			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			CompSubcoreAutomationBase comp = t.TryGetComp<CompSubcoreAutomationBase>();
			if (comp == null)
				return null;

			// Fallback mode: find materials and create job with hauling
			if (SubcoreFallback.IsActive)
			{
				var materials = SubcoreFallback.FindMaterialsOnMap(t.Map, comp.Props.subcoreDef, pawn);
				if (materials == null || materials.Count == 0)
					return null;

				Job fallbackJob = JobMaker.MakeJob(SubcoreAutomationDefOf.SubcoreAutomation_InstallFallback, t);
				fallbackJob.targetQueueB = new List<LocalTargetInfo>();
				fallbackJob.countQueue = new List<int>();

				foreach (var (thing, count) in materials)
				{
					fallbackJob.targetQueueB.Add(thing);
					fallbackJob.countQueue.Add(count);
				}

				return fallbackJob;
			}

			// Normal mode: haul subcore to building
			Thing subcore = comp.FindSubcoreOnMap(comp.Props.subcoreDef);
			if (subcore == null)
				return null;

			Job installJob = JobMaker.MakeJob(SubcoreAutomationDefOf.SubcoreAutomation_InstallSubcore, t, subcore);
			installJob.count = 1;
			return installJob;
		}
	}
}
