using AnimalsLogic.Patches;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Recreation
{
    public class JobDriver_PlayWithAnimal : JobDriver
    {
        private const int PlayDurationTicks = 2500;
        private const float BaseXPPerTick = 0.02f;
        private const float BondChancePerChat = 0.004f;

        private Job animalWaitJob;

        private Pawn Animal => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        private bool IsWildAnimal => Animal?.Faction == null;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Animal, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);

            Toil summon = ToilMaker.MakeToil("SummonAnimal");
            summon.initAction = delegate
            {
                if (!IsWildAnimal)
                    ComeWhenCalled.TrySummonAnimal(Animal, pawn);
            };
            summon.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return summon;

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil playToil = ToilMaker.MakeToil("PlayWithAnimal");
            playToil.initAction = delegate
            {
                PawnUtility.ForceWait(Animal, PlayDurationTicks, pawn);
                animalWaitJob = Animal.CurJob;
            };
            playToil.defaultCompleteMode = ToilCompleteMode.Delay;
            playToil.defaultDuration = PlayDurationTicks;
            playToil.FailOn(() => !pawn.Position.AdjacentTo8WayOrInside(Animal.Position));
            playToil.tickIntervalAction = delegate (int delta)
            {
                pawn.rotationTracker.FaceTarget(Animal);

                float xpGain = CalculateXPGain() * delta;
                pawn.skills?.GetSkill(SkillDefOf.Animals)?.Learn(xpGain, false);

                JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob, JoyFactor());

                if (pawn.IsHashIntervalTick(240, delta))
                {
                    if (pawn.interactions?.TryInteractWith(Animal, InteractionDefOf.AnimalChat) == true
                        && !IsWildAnimal)
                    {
                        RelationsUtility.TryDevelopBondRelation(pawn, Animal, BondChancePerChat);
                    }
                }

                if (pawn.IsHashIntervalTick(300, delta))
                {
                    FleckMaker.ThrowMetaIcon(Animal.Position, Animal.Map, FleckDefOf.Heart);
                }
            };
            playToil.AddFinishAction(delegate
            {
                if (Animal != null && animalWaitJob != null && Animal.CurJob == animalWaitJob)
                {
                    Animal.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                animalWaitJob = null;

                if (IsWildAnimal && Animal != null && Animal.Spawned
                    && !TameUtility.TriedToTameTooRecently(Animal))
                {
                    pawn.interactions?.TryInteractWith(Animal, InteractionDefOf.TameAttempt);
                }
            });
            playToil.activeSkill = () => SkillDefOf.Animals;
            playToil.socialMode = RandomSocialMode.Normal;
            yield return playToil;
        }

        private float CalculateXPGain()
        {
            if (Animal == null)
                return 0f;

            float minSkill = Animal.GetStatValue(StatDefOf.MinimumHandlingSkill);
            float wildness = Animal.GetStatValue(StatDefOf.Wildness);

            float difficulty = (minSkill / 10f) + wildness;
            return BaseXPPerTick * (1f + difficulty);
        }

        private float JoyFactor()
        {
            if (Animal == null)
                return 1f;

            float factor = 1f;

            if (pawn.relations?.DirectRelationExists(PawnRelationDefOf.Bond, Animal) == true)
            {
                factor *= 1.5f;
            }

            factor *= 1f + Animal.RaceProps.petness * 0.5f;

            return factor;
        }
    }
}
