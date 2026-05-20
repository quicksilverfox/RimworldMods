using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Recreation
{
    /// <summary>
    /// Joy giver that allows pawns with animal handling skills to play with animals.
    /// Prioritizes bonded animals, then animals with high petness.
    /// High-skill handlers may interact with wild animals for a chance to tame.
    /// </summary>
    public class JoyGiver_PlayWithAnimal : JoyGiver
    {
        // Minimum handling skill to attempt wild animal interaction
        private const int MinSkillForWildInteraction = 15;
        
        // Maximum wildness for wild animal interaction
        private const float MaxWildnessForInteraction = 0.6f;

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!Settings.play_with_animals)
                return null;

            if (pawn == null || !pawn.RaceProps.Humanlike)
                return null;

            // Pawn must be capable of animals work
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                return null;

            // Get pawn's handling skill
            int handlingSkill = pawn.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;

            // Try to find a suitable animal
            Pawn targetAnimal = FindTargetAnimal(pawn, handlingSkill);
            if (targetAnimal == null)
                return null;

            // Check reachability
            if (!pawn.CanReserveAndReach(targetAnimal, PathEndMode.Touch, Danger.None))
                return null;

            return JobMaker.MakeJob(ALJobDefOf.AL_PlayWithAnimal, targetAnimal);
        }

        private Pawn FindTargetAnimal(Pawn pawn, int handlingSkill)
        {
            Map map = pawn.Map;
            if (map == null)
                return null;

            List<Pawn> candidates = new List<Pawn>();
            List<float> weights = new List<float>();

            // First, check for colony animals
            foreach (Pawn animal in map.mapPawns.SpawnedColonyAnimals)
            {
                if (!IsValidColonyAnimal(pawn, animal, handlingSkill))
                    continue;

                float weight = CalculateAnimalWeight(pawn, animal);
                candidates.Add(animal);
                weights.Add(weight);
            }

            // For high-skill handlers, also consider wild animals
            if (handlingSkill >= MinSkillForWildInteraction && Rand.Chance(0.15f))
            {
                foreach (Pawn animal in map.mapPawns.AllPawnsSpawned)
                {
                    if (!IsValidWildAnimal(pawn, animal, handlingSkill))
                        continue;

                    // Lower weight for wild animals (more rare choice)
                    float weight = CalculateWildAnimalWeight(animal) * 0.3f;
                    candidates.Add(animal);
                    weights.Add(weight);
                }
            }

            if (candidates.Count == 0)
                return null;

            // Weighted random selection
            return candidates.RandomElementByWeight(a => weights[candidates.IndexOf(a)]);
        }

        private bool IsValidColonyAnimal(Pawn pawn, Pawn animal, int handlingSkill)
        {
            if (animal == null || animal == pawn || animal.Dead || !animal.Spawned)
                return false;

            if (!animal.RaceProps.Animal)
                return false;

            // Check if animal is downed or in mental state
            if (animal.Downed || animal.InMentalState)
                return false;

            // Animal position must not be forbidden to pawn (allowed-area check).
            // FailOnDespawnedNullOrForbidden in driver would otherwise loop the job.
            if (animal.IsForbidden(pawn))
                return false;

            // Check handling skill requirement
            float minSkillRequired = animal.GetStatValue(StatDefOf.MinimumHandlingSkill);
            if (handlingSkill < minSkillRequired)
                return false;

            // Check reachability
            if (!pawn.CanReserveAndReach(animal, PathEndMode.Touch, Danger.None))
                return false;

            return true;
        }

        private bool IsValidWildAnimal(Pawn pawn, Pawn animal, int handlingSkill)
        {
            if (animal == null || animal == pawn || animal.Dead || !animal.Spawned)
                return false;

            if (!animal.RaceProps.Animal)
                return false;

            // Must be wild (no faction)
            if (animal.Faction != null)
                return false;

            // Must not be manhunter or in mental state
            if (animal.InMentalState || animal.IsFighting())
                return false;

            // Must not be a predator that could attack the pawn
            if (animal.RaceProps.predator && animal.RaceProps.baseBodySize >= pawn.RaceProps.baseBodySize * 0.5f)
                return false;

            if (animal.IsForbidden(pawn))
                return false;

            // Check wildness - only relatively tame wild animals
            float wildness = animal.GetStatValue(StatDefOf.Wildness);
            if (wildness > MaxWildnessForInteraction)
                return false;

            // Check handling skill requirement (with some buffer for wild)
            float minSkillRequired = animal.GetStatValue(StatDefOf.MinimumHandlingSkill);
            if (handlingSkill < minSkillRequired + 2)
                return false;

            // Check reachability
            if (!pawn.CanReserveAndReach(animal, PathEndMode.Touch, Danger.Some))
                return false;

            return true;
        }

        private float CalculateAnimalWeight(Pawn pawn, Pawn animal)
        {
            float weight = 1f;

            if (pawn.relations?.DirectRelationExists(PawnRelationDefOf.Bond, animal) == true)
            {
                weight *= 5f;
            }

            bool obedient = animal.training?.HasLearned(TrainableDefOf.Obedience) == true;
            if (obedient && animal.playerSettings?.Master == pawn)
            {
                weight *= 4f;
            }
            else if (obedient)
            {
                weight *= 1.5f;
            }

            float petness = animal.RaceProps.petness;
            weight *= 1f + petness * 2f;

            float distance = pawn.Position.DistanceTo(animal.Position);
            weight *= 1f / (1f + distance * 0.01f);

            return weight;
        }

        private float CalculateWildAnimalWeight(Pawn animal)
        {
            float weight = 1f;

            // Prefer less wild animals
            float wildness = animal.GetStatValue(StatDefOf.Wildness);
            weight *= 1f - wildness;

            // Prefer smaller animals (safer)
            weight *= 1f / (1f + animal.RaceProps.baseBodySize * 0.5f);

            return weight;
        }
    }
}
