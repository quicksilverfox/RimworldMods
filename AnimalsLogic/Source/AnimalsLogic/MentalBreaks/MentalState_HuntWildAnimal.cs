using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AnimalsLogic.MentalBreaks
{
    /// <summary>
    /// Mental state where a pawn goes to hunt wild animals on the map.
    /// The state only tracks the current target; the job itself is handed out by
    /// <see cref="JobGiver_HuntWildAnimal"/> through the MentalStateNonCritical think subtree
    /// (see 1.7/Patches/ThinkTree_MentalState.xml), the same way vanilla aggro states work.
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

            if (!IsValidHuntTarget(pawn, targetAnimal))
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
        }

        /// <summary>
        /// Current target if it is still huntable, otherwise picks a new one. Null when nothing is left to hunt.
        /// </summary>
        public Pawn GetOrFindTarget()
        {
            if (!IsValidHuntTarget(pawn, targetAnimal))
                TryFindNewTarget();
            return targetAnimal;
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
                if (!IsValidHuntTarget(pawn, animal))
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

        /// <summary>
        /// Shared with <see cref="MentalStateWorker_HuntWildAnimal"/> so the break is only offered when a target exists.
        /// </summary>
        public static bool IsValidHuntTarget(Pawn hunter, Pawn animal)
        {
            if (animal == null || animal.Dead || !animal.Spawned || animal == hunter)
                return false;

            if (!animal.RaceProps.Animal)
                return false;

            // Must be wild (no faction) - don't hunt colony animals
            if (animal.Faction != null)
                return false;

            // Must not be manhunter (too dangerous even for mental break)
            if (animal.InMentalState && animal.MentalState.def == MentalStateDefOf.Manhunter)
                return false;

            if (animal.IsForbidden(hunter))
                return false;

            // Must be reachable
            if (hunter.Map != animal.Map || !hunter.CanReach(animal, PathEndMode.Touch, Danger.Deadly))
                return false;

            return true;
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
