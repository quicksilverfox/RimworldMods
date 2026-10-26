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

            if (!IsSafeEnoughTarget(hunter, animal))
                return false;

            if (animal.IsForbidden(hunter))
                return false;

            // Must be reachable
            if (hunter.Map != animal.Map || !hunter.CanReach(animal, PathEndMode.Touch, Danger.Deadly))
                return false;

            return true;
        }

        public enum Armament { Unarmed, Melee, Ranged }

        /// <summary>
        /// Highest <see cref="DangerOf"/> a hunter with the given armament will go after. This is a major break, not an
        /// extreme one, so the pawn stays well clear of anything that could kill it. Calibrated against vanilla
        /// (danger = combat power x (0.5 + revenge chance)): deer 25, boar 27, muffalo 60, lynx 80, cassowary 90,
        /// wolf 112, cougar 120, warg 240, grizzly 200, elephant 260, thrumbo 750.
        /// </summary>
        public const float MaxDangerRanged = 80f;
        public const float MaxDangerMelee = 45f;
        public const float MaxDangerUnarmed = 25f;

        public static Armament ArmamentOf(Pawn hunter)
        {
            ThingWithComps primary = hunter.equipment?.Primary;
            if (primary == null)
                return Armament.Unarmed;

            // vanilla's own hunting weapon test: ranged, harms health, no explosive projectiles
            if (WorkGiver_HunterHunt.HasHuntingWeapon(hunter))
                return Armament.Ranged;

            return primary.def.IsMeleeWeapon ? Armament.Melee : Armament.Unarmed;
        }

        public static float MaxDangerFor(Armament armament)
        {
            switch (armament)
            {
                case Armament.Ranged: return MaxDangerRanged;
                case Armament.Melee: return MaxDangerMelee;
                default: return MaxDangerUnarmed;
            }
        }

        /// <summary>
        /// How much trouble attacking this animal is likely to cause. Combat power already folds in health, damage and
        /// speed; the revenge chance (difficulty-adjusted, like vanilla's hunt designator shows it) decides whether the
        /// animal fights back at all instead of fleeing, which is what actually gets a lone hunter hurt.
        /// </summary>
        public static float DangerOf(Pawn animal)
        {
            float revengeChance = PawnUtility.GetManhunterOnDamageChance(animal.def);
            return animal.kindDef.combatPower * (0.5f + revengeChance);
        }

        /// <summary>
        /// Not already enraged, not something that explodes when killed up close, and not beyond what the hunter's
        /// current weapon can reasonably handle. Re-evaluated every tick, so dropping the weapon mid-spree
        /// makes the pawn abandon targets it can no longer take on.
        /// </summary>
        public static bool IsSafeEnoughTarget(Pawn hunter, Pawn animal)
        {
            if (animal.InAggroMentalState)
                return false;

            Armament armament = ArmamentOf(hunter);

            // same flag vanilla's hunt driver consults before closing to melee (boomalopes, boomrats, toxic cloud deaths)
            if (armament != Armament.Ranged && animal.RaceProps.DeathActionWorker?.DangerousInMelee == true)
                return false;

            return DangerOf(animal) <= MaxDangerFor(armament);
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
