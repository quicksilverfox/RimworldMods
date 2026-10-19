using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Patches
{
    /// <summary>
    /// Makes obedient animals come to the handler when called, instead of the handler having to chase them.
    /// Used when a handler starts training (StartJob postfix below) and by <see cref="Recreation.JobDriver_PlayWithAnimal"/>.
    /// The animal drops whatever it is doing unless that is something that must not be interrupted (see <see cref="CanInterruptCurrentJob"/>).
    /// </summary>
    public static class ComeWhenCalled
    {
        public const float FollowRadius = 3f;
        public const int ExpiryInterval = 200;

        public static bool TrySummonAnimal(Pawn animal, Pawn handler)
        {
            if (!Settings.come_when_called)
                return false;

            if (animal?.training == null || !animal.training.HasLearned(TrainableDefOf.Obedience))
                return false;

            if (handler == null || !animal.Spawned || !handler.Spawned || animal.Map != handler.Map)
                return false;

            if (animal.Position.DistanceTo(handler.Position) < FollowRadius)
                return false;

            if (IsAlreadyAttendingTo(animal, handler))
                return false;

            if (!RestUtility.Awake(animal))
                return false;

            if (animal.RaceProps.Roamer)
                return false;

            Area animalArea = animal.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (animalArea != null && !animalArea[handler.Position])
                return false;

            Area handlerArea = handler.playerSettings?.EffectiveAreaRestrictionInPawnCurrentMap;
            if (handlerArea != null && !handlerArea[handler.Position])
                return false;

            if (!CanInterruptCurrentJob(animal))
                return false;

            if (!animal.CanReach(handler, PathEndMode.Touch, Danger.Deadly))
                return false;

            MakeAnimalComeToHandler(animal, handler);
            return true;
        }

        /// <summary>
        /// True when the animal's current activity can be dropped for the handler. Mirrors what vanilla itself requires
        /// before a handler interacts with an animal: <see cref="PawnUtility.CanCasuallyInteractNow"/> is the same test the
        /// training work giver runs and the interaction driver re-checks, and vanilla then force-interrupts the animal's job
        /// on arrival regardless of its casualInterruptible flag. On top of that: not downed, not fighting, not fleeing,
        /// not on fire or otherwise not player-interruptible, and not in a player-forced job.
        /// </summary>
        public static bool CanInterruptCurrentJob(Pawn animal)
        {
            if (animal.Dead || animal.Downed || animal.jobs == null)
                return false;

            if (!animal.CanCasuallyInteractNow())
                return false;

            if (animal.IsFighting())
                return false;

            // covers JobDef.playerInterruptible, the driver's PlayerInterruptable and being on fire
            if (!animal.jobs.IsCurrentJobPlayerInterruptible())
                return false;

            Job job = animal.CurJob;
            if (job == null)
                return true;

            if (job.playerForced)
                return false;

            // fleeing is casually interruptible in vanilla, but a fleeing animal must not be called back toward the handler
            return job.def != JobDefOf.Flee && job.def != JobDefOf.FleeAndCower;
        }

        /// <summary>Already coming to, or being held by, this handler: do not restart the job.</summary>
        static bool IsAlreadyAttendingTo(Pawn animal, Pawn handler)
        {
            Job job = animal.CurJob;
            if (job == null || job.targetA.Thing != handler)
                return false;

            return job.def == JobDefOf.FollowClose || job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_MaintainPosture;
        }

        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
        public static class Pawn_JobTracker_StartJob_Patch
        {
            static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField = AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

            [HarmonyPostfix]
            public static void Postfix(Pawn_JobTracker __instance, Job newJob)
            {
                if (!Settings.come_when_called)
                    return;

                if (newJob?.def != JobDefOf.Train)
                    return;

                Pawn handler = PawnField(__instance);
                if (handler == null || !handler.RaceProps.Humanlike || handler.Faction != Faction.OfPlayer)
                    return;

                Pawn animal = newJob.targetA.Pawn;
                if (animal == null || !animal.RaceProps.Animal)
                    return;

                TrySummonAnimal(animal, handler);
            }
        }

        private static void MakeAnimalComeToHandler(Pawn animal, Pawn handler)
        {
            Job comeJob = JobMaker.MakeJob(JobDefOf.FollowClose, handler);
            comeJob.followRadius = FollowRadius;
            comeJob.expiryInterval = ExpiryInterval;
            comeJob.checkOverrideOnExpire = true;
            comeJob.locomotionUrgency = LocomotionUrgency.Jog;

            animal.jobs.StartJob(comeJob, JobCondition.InterruptForced);
        }
    }
}
