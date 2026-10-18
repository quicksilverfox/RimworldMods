using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsLogic.MentalBreaks
{
    /// <summary>
    /// Hands out the hunt job while a pawn is in <see cref="MentalState_HuntWildAnimal"/>.
    /// Wired into the MentalStateNonCritical think subtree by 1.7/Patches/ThinkTree_MentalState.xml.
    /// </summary>
    public class JobGiver_HuntWildAnimal : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.MentalState is MentalState_HuntWildAnimal state))
                return null;

            Pawn target = state.GetOrFindTarget();
            if (target == null || !pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly))
                return null;

            Job job = JobMaker.MakeJob(JobDefOf.Hunt, target);
            // Without this the vanilla hunt driver fails on its first tick because the animal has no Hunt designation.
            job.ignoreDesignations = true;
            job.killIncappedTarget = true;
            return job;
        }
    }
}
