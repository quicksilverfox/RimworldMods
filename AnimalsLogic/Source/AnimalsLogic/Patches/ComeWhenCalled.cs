using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Patches
{
    /// <summary>
    /// Makes trained animals come to the trainer when being called for training,
    /// instead of the trainer having to chase them.
    /// Only applies to animals that have completed Obedience training.
    /// </summary>
    public static class ComeWhenCalled
    {
        private const float FollowRadius = 3f;
        private const int ExpiryInterval = 200;

        public static bool TrySummonAnimal(Pawn animal, Pawn handler)
        {
            if (animal?.training == null || !animal.training.HasLearned(TrainableDefOf.Obedience))
                return false;

            if (handler == null || !animal.Spawned || !handler.Spawned || animal.Map != handler.Map)
                return false;

            if (animal.Position.DistanceTo(handler.Position) < FollowRadius)
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

            if (animal.CurJob != null && !CanInterruptForTraining(animal.CurJob))
                return false;

            if (!animal.CanReach(handler, PathEndMode.Touch, Danger.Deadly))
                return false;

            MakeAnimalComeToHandler(animal, handler);
            return true;
        }

        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
        public static class Pawn_JobTracker_StartJob_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn_JobTracker __instance, Job newJob)
            {
                if (!Settings.come_when_called)
                    return;

                if (newJob?.def != JobDefOf.Train)
                    return;

                Pawn handler = (Pawn)AccessTools.Field(typeof(Pawn_JobTracker), "pawn").GetValue(__instance);
                if (handler == null || !handler.RaceProps.Humanlike || handler.Faction != Faction.OfPlayer)
                    return;

                Pawn animal = newJob.targetA.Pawn;
                if (animal == null || !animal.RaceProps.Animal)
                    return;

                TrySummonAnimal(animal, handler);
            }
        }

        private static bool CanInterruptForTraining(Job currentJob)
        {
            if (currentJob.def == JobDefOf.FleeAndCower)
                return false;
            if (currentJob.def == JobDefOf.Flee)
                return false;
            if (!currentJob.def.casualInterruptible)
                return false;

            return true;
        }

        private static void MakeAnimalComeToHandler(Pawn animal, Pawn handler)
        {
            Job comeJob = JobMaker.MakeJob(JobDefOf.FollowClose, handler);
            comeJob.followRadius = FollowRadius;
            comeJob.expiryInterval = ExpiryInterval;
            comeJob.checkOverrideOnExpire = true;
            comeJob.locomotionUrgency = LocomotionUrgency.Jog;

            animal.jobs?.StartJob(comeJob, JobCondition.InterruptForced);
        }
    }
}
