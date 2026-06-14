using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Jobs
{
	/// <summary>
	/// Work giver for removing subcores from buildings.
	/// </summary>
	public class WorkGiver_RemoveSubcore : WorkGiver_Scanner
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

			if (!comp.RemovalRequested)
				return false;

			if (!comp.SubcoreInstalled)
				return false;

			// Check if we can reach the building
			if (!pawn.CanReserveAndReach(t, PathEndMode.InteractionCell, Danger.Deadly, 1, -1, null, forced))
				return false;

			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			return JobMaker.MakeJob(SubcoreAutomationDefOf.SubcoreAutomation_RemoveSubcore, t);
		}
	}
}
