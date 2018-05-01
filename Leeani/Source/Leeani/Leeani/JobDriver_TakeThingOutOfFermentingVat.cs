using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace Leeani
{
    public class JobDriver_TakeThingOutOfFermentingVat : JobDriver
    {
        private const TargetIndex VatInd = TargetIndex.A;

        private const TargetIndex ThingToHaulInd = TargetIndex.B;

        private const TargetIndex StorageCellInd = TargetIndex.C;

        private const int Duration = 200;

        protected Building_FermentingVat Vat
        {
            get
            {
                return (Building_FermentingVat)base.job.GetTarget(TargetIndex.A).Thing;
            }
        }

        protected Thing VatProduct
        {
            get
            {
                return base.job.GetTarget(TargetIndex.B).Thing;
            }
        }

        public override bool TryMakePreToilReservations()
        {
            return this.pawn.Reserve(this.Vat, this.job, 1, -1, null);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            /*this.FailOnDestroyedOrNull(VatInd);

            yield return Toils_Reserve.Reserve(VatInd);

            Toil toil_goto = Toils_Goto.Goto(VatInd, PathEndMode.ClosestTouch);
            yield return toil_goto;

            Toil toil_release = Toils_Reserve.Release(VatInd);
            toil_release.AddFinishAction(delegate ()
            {
                ThingContainer container = new ThingContainer();
                container.TryAdd(Barrel.TakeOutThing());
                container.TryDropAll(pawn.Position, pawn.Map, ThingPlaceMode.Near);
            });
            yield return toil_release.WithProgressBarToilDelay(VatInd, Duration);*/

            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            yield return Toils_Reserve.Reserve(TargetIndex.A, 1);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOn(() => !Vat.Fermented).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            yield return new Toil
            {
                initAction = delegate
                {
                    Thing thing = Vat.TakeOutThing();
                    GenPlace.TryPlaceThing(thing, pawn.Position, Map, ThingPlaceMode.Near, null);
                    StoragePriority currentPriority = HaulAIUtility.StoragePriorityAtFor(thing.Position, thing);
                    IntVec3 c;
                    if (StoreUtility.TryFindBestBetterStoreCellFor(thing, pawn, Map, currentPriority, pawn.Faction, out c, true))
                    {
                        job.SetTarget(TargetIndex.C, c);
                        job.SetTarget(TargetIndex.B, thing);
                        job.count = thing.stackCount;
                    }
                    else
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return Toils_Reserve.Reserve(TargetIndex.B, 1);
            yield return Toils_Reserve.Reserve(TargetIndex.C, 1);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false);
            Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, carryToCell, true);
        }
    }
}
