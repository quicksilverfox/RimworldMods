using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AnimalsLogic.MentalBreaks
{
    /// <summary>
    /// Mental state where a pawn goes to hunt a random wild animal on the map.
    /// This is a violent mental break that ends when the animal is killed or escapes.
    /// </summary>
    public class MentalState_HuntWildAnimal : MentalState
    {
        private Pawn targetAnimal;
        private int ticksUntilRetarget = 0;
        private const int RetargetIntervalTicks = 500;

        public Pawn TargetAnimal => targetAnimal;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref targetAnimal, "targetAnimal");
            Scribe_Values.Look(ref ticksUntilRetarget, "ticksUntilRetarget", 0);
        }

        public override void PreStart()
        {
            base.PreStart();
            TryFindNewTarget();
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);

            if (targetAnimal == null || targetAnimal.Dead || !targetAnimal.Spawned)
            {
                ticksUntilRetarget -= delta;
                if (ticksUntilRetarget <= 0)
                {
                    if (!TryFindNewTarget())
                    {
                        RecoverFromState();
                        return;
                    }
                    ticksUntilRetarget = RetargetIntervalTicks;
                }
            }

            if (pawn.IsHashIntervalTick(120, delta))
            {
                TryGiveHuntJob();
            }
        }

        private bool TryFindNewTarget()
        {
            targetAnimal = FindRandomWildAnimal();
            return targetAnimal != null;
        }

        private Pawn FindRandomWildAnimal()
        {
            Map map = pawn.Map;
            if (map == null)
                return null;

            List<Pawn> validAnimals = new List<Pawn>();

            foreach (Pawn animal in map.mapPawns.AllPawnsSpawned)
            {
                if (!IsValidHuntTarget(animal))
                    continue;

                validAnimals.Add(animal);
            }

            if (validAnimals.Count == 0)
                return null;

            // Prefer closer animals, but with some randomness
            return validAnimals
                .OrderBy(a => pawn.Position.DistanceTo(a.Position) + Rand.Range(0f, 50f))
                .FirstOrDefault();
        }

        private bool IsValidHuntTarget(Pawn animal)
        {
            if (animal == null || animal.Dead || !animal.Spawned)
                return false;

            if (!animal.RaceProps.Animal)
                return false;

            // Must be wild (no faction) - don't hunt colony animals
            if (animal.Faction != null)
                return false;

            // Must not be manhunter (too dangerous even for mental break)
            if (animal.InMentalState && animal.MentalState.def == MentalStateDefOf.Manhunter)
                return false;

            // Must be reachable
            if (!pawn.CanReach(animal, PathEndMode.Touch, Danger.Deadly))
                return false;

            return true;
        }

        private void TryGiveHuntJob()
        {
            if (targetAnimal == null || targetAnimal.Dead || !targetAnimal.Spawned)
                return;

            // If already hunting this target, don't interrupt
            if (pawn.CurJob?.def == JobDefOf.Hunt && pawn.CurJob?.targetA.Thing == targetAnimal)
                return;

            // If target is not reachable, find new target
            if (!pawn.CanReach(targetAnimal, PathEndMode.Touch, Danger.Deadly))
            {
                TryFindNewTarget();
                return;
            }

            // Create and assign hunt job
            Job huntJob = JobMaker.MakeJob(JobDefOf.Hunt, targetAnimal);
            huntJob.killIncappedTarget = true;
            pawn.jobs.StartJob(huntJob, JobCondition.InterruptForced);
        }

        public override bool ForceHostileTo(Thing t)
        {
            // Hostile to the target animal
            return t == targetAnimal;
        }

        public override bool ForceHostileTo(Faction f)
        {
            // Not hostile to any faction
            return false;
        }

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }

        public override TaggedString GetBeginLetterText()
        {
            if (targetAnimal != null)
            {
                return def.beginLetter.Formatted(
                    pawn.Named("PAWN"),
                    targetAnimal.Named("ANIMAL")).AdjustedFor(pawn).CapitalizeFirst();
            }
            return def.beginLetter.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).CapitalizeFirst();
        }
    }
}
