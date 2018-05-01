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



        public override bool TryMakePreToilReservations()
        {
            return this.pawn.Reserve(this.Vat, this.job, 1, -1, null) && this.pawn.Reserve(this.InputThing, this.job, 1, -1, null);
        }


        protected override IEnumerable<Toil> MakeNewToils()
        {
            /*this.FailOnDestroyedOrNull(VatInd);

            yield return Toils_Reserve.Reserve(InputInd);
            yield return Toils_Reserve.Reserve(VatInd);

            yield return Toils_Goto.Goto(InputInd, PathEndMode.ClosestTouch);
            yield return Toils_Reserve.Release(InputInd);
            yield return Toils_Haul.StartCarryThing(InputInd, false, true);

            yield return Toils_Haul.CarryHauledThingToCell(VatInd);
            Toil toil_release = Toils_Reserve.Release(VatInd);
            toil_release.AddFinishAction(delegate ()
            {
                Vat.AddInput(InputThing);
            });
            yield return toil_release.WithProgressBarToilDelay(VatInd, Duration);*/

            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            yield return Toils_Reserve.Reserve(TargetIndex.A, 1);
            Toil reserveThing = Toils_Reserve.Reserve(TargetIndex.B, 1);
            yield return reserveThing;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveThing, TargetIndex.B, TargetIndex.None, false, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return new Toil
            {
                initAction = delegate
                {
                    //this.<> f__this.Barrel.AddWort(this.<> f__this.Wort);
                    Vat.AddInput(InputThing);
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
        }
    }
}
