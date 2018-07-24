using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace Leeani
{
    class JobDriver_FillFermentingVat : JobDriver
    {
        private const TargetIndex VatInd = TargetIndex.A;

        private const TargetIndex InputInd = TargetIndex.B;

        private const int Duration = 200;

        protected Building_FermentingVat Vat
        {
            get
            {
                return (Building_FermentingVat)base.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected Thing InputThing
        {
            get
            {
                return base.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.Vat;
            Job job = this.job;
            bool arg_59_0;
            if (pawn.Reserve(target, job, 1, -1, null, errorOnFailed))
            {
                pawn = this.pawn;
                target = this.InputThing;
                job = this.job;
                arg_59_0 = pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            }
            else
            {
                arg_59_0 = false;
            }
            return arg_59_0;
        }


        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            base.AddEndCondition(() => (this.Vat.SpaceLeftForInput > 0) ? JobCondition.Ongoing : JobCondition.Succeeded);
            yield return Toils_General.DoAtomic(delegate
            {
                this.job.count = this.Vat.SpaceLeftForInput;
            });
            Toil reserveWort = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return reserveWort;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveWort, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return new Toil
            {
                initAction = delegate
                {
                    this.Vat.AddInput(InputThing);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}
