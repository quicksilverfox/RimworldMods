using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Patches
{
    /// <summary>
    /// Makes obedient animals come to the handler when called, instead of the handler having to chase them, and keeps
    /// the animal in place while the handler is actually talking to it.
    ///
    /// Vanilla's <see cref="JobDriver_InteractAnimal"/> is a chain of "walk to the animal, talk for 270 ticks" steps and
    /// only force-waits the animal while feeding it, so between talks the animal wanders off and the handler has to chase
    /// it again. The <see cref="JobDriver_InteractAnimal_MakeNewToils_Patch"/> below hooks every toil of that driver:
    /// at each toil start (and periodically while walking) the animal is summoned if it is away, and for each talk toil
    /// it is held with the same forced wait vanilla uses for feeding. Also used by <see cref="Recreation.JobDriver_PlayWithAnimal"/>.
    /// The animal never drops something that must not be interrupted (see <see cref="CanInterruptCurrentJob"/>).
    /// </summary>
    public static class ComeWhenCalled
    {
        public const float FollowRadius = 3f;
        public const int ExpiryInterval = 200;

        /// <summary>Calls an obedient animal that is away from the handler over to them.</summary>
        public static bool TrySummonAnimal(Pawn animal, Pawn handler)
        {
            if (!Settings.come_when_called)
                return false;

            if (animal?.training == null || !animal.training.HasLearned(TrainableDefOf.Obedience))
                return false;

            if (!SameMap(animal, handler))
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
        /// Keeps an animal that is already next to the handler in place for <paramref name="ticks"/>, facing the handler.
        /// This is what vanilla does while feeding the animal; obedience is not required because the handler is already
        /// interacting with it (and Obedience is the first thing that gets trained).
        /// </summary>
        public static bool TryHoldAnimal(Pawn animal, Pawn handler, int ticks)
        {
            if (!Settings.come_when_called || ticks <= 0)
                return false;

            if (animal == null || !SameMap(animal, handler))
                return false;

            if (animal.Position.DistanceTo(handler.Position) >= FollowRadius)
                return false;

            if (!RestUtility.Awake(animal))
                return false;

            if (!CanInterruptCurrentJob(animal))
                return false;

            PawnUtility.ForceWait(animal, ticks, handler);
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

        static bool SameMap(Pawn animal, Pawn handler)
        {
            return handler != null && animal.Spawned && handler.Spawned && animal.Map == handler.Map;
        }

        /// <summary>Already coming to, or being held by, this handler: do not restart the job.</summary>
        static bool IsAlreadyAttendingTo(Pawn animal, Pawn handler)
        {
            Job job = animal.CurJob;
            if (job == null || job.targetA.Thing != handler)
                return false;

            return job.def == JobDefOf.FollowClose || job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_MaintainPosture;
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

        /// <summary>
        /// Wraps the toils of every animal interaction driver (training, taming) so the animal is called over at each step
        /// and held in place during each talk. Subclasses such as <see cref="JobDriver_Train"/> go through the base method,
        /// so their toils get wrapped as well.
        /// </summary>
        [HarmonyPatch(typeof(JobDriver_InteractAnimal), "MakeNewToils")]
        public static class JobDriver_InteractAnimal_MakeNewToils_Patch
        {
            const string TalkToilName = "TalkToAnimal";

            [HarmonyPostfix]
            public static void Postfix(JobDriver_InteractAnimal __instance, ref IEnumerable<Toil> __result)
            {
                __result = KeepAnimalClose(__instance, __result);
            }

            static IEnumerable<Toil> KeepAnimalClose(JobDriver driver, IEnumerable<Toil> toils)
            {
                foreach (Toil toil in toils)
                {
                    Toil current = toil;
                    current.AddPreInitAction(() => OnToilStarted(driver, current));
                    current.AddPreTickIntervalAction(delta =>
                    {
                        if (driver.pawn.IsHashIntervalTick(ExpiryInterval, delta))
                            TrySummonAnimal(HandledAnimal(driver), driver.pawn);
                    });
                    yield return current;
                }
            }

            static void OnToilStarted(JobDriver driver, Toil toil)
            {
                Pawn animal = HandledAnimal(driver);
                if (animal == null)
                    return;

                if (toil.debugName == TalkToilName && TryHoldAnimal(animal, driver.pawn, toil.defaultDuration))
                    return;

                TrySummonAnimal(animal, driver.pawn);
            }

            /// <summary>The animal being handled, or null when this driver is not a player handler working on an animal.</summary>
            static Pawn HandledAnimal(JobDriver driver)
            {
                if (!Settings.come_when_called)
                    return null;

                Pawn handler = driver.pawn;
                if (handler == null || !handler.RaceProps.Humanlike || handler.Faction != Faction.OfPlayer)
                    return null;

                Pawn animal = driver.job?.targetA.Pawn;
                if (animal == null || !animal.RaceProps.Animal)
                    return null;

                return animal;
            }
        }
    }
}
