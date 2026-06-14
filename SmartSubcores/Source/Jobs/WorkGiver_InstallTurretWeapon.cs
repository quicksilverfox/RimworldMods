using RimWorld;
using SubcoreAutomation.Buildings;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Jobs
{
	/// <summary>
	/// Issues vanilla HaulToContainer jobs to deliver the pending weapon into a Building_SwappableTurret.
	/// </summary>
	public class WorkGiver_InstallTurretWeapon : WorkGiver_Scanner
	{
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

		public override PathEndMode PathEndMode => PathEndMode.Touch;

		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
			if (pawn.RaceProps.IsMechanoid)
			{
				if (pawn.workSettings == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
					return true;
			}
			return base.ShouldSkip(pawn, forced);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (!(t is Building_SwappableTurret turret)) return false;
			if (turret.Faction != Faction.OfPlayer) return false;
			if (!turret.InstallRequested) return false;
			if (turret.PendingWeapon == null || turret.PendingWeapon.Destroyed || !turret.PendingWeapon.Spawned) return false;
			if (turret.PendingWeapon.IsForbidden(pawn)) return false;
			if (!pawn.CanReserveAndReach(turret.PendingWeapon, PathEndMode.ClosestTouch, Danger.Deadly, 1, -1, null, forced)) return false;
			if (!pawn.CanReserveAndReach(turret, PathEndMode.Touch, Danger.Deadly, 1, 1, null, forced)) return false;
			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			Building_SwappableTurret turret = (Building_SwappableTurret)t;
			Job job = JobMaker.MakeJob(JobDefOf.HaulToContainer, turret.PendingWeapon, turret);
			job.count = 1;
			return job;
		}
	}
}
