using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Jobs
{
	/// <summary>
	/// Job driver for removing a subcore from a building.
	/// </summary>
	public class JobDriver_RemoveSubcore : JobDriver
	{
		private const TargetIndex BuildingIndex = TargetIndex.A;

		private Thing Building => job.GetTarget(BuildingIndex).Thing;
		private CompSubcoreAutomationBase AutomationComp => Building?.TryGetComp<CompSubcoreAutomationBase>();

		public override string GetReport()
		{
			Thing building = Building;
			if (building != null)
				return "SubcoreAutomation_RemovingSubcore".Translate(building.LabelShort);
			return base.GetReport();
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Building, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// Fail conditions
			this.FailOnDespawnedNullOrForbidden(BuildingIndex);
			this.FailOnBurningImmobile(BuildingIndex);
			this.FailOn(() => AutomationComp == null || !AutomationComp.RemovalRequested);

			// Go to building
			yield return Toils_Goto.GotoThing(BuildingIndex, PathEndMode.InteractionCell);

			// Do removal work (half the time of installation)
			float workAmount = (AutomationComp?.Props.installWorkAmount ?? 2000f) * 0.5f;
			Toil removeToil = Toils_General.Wait((int)workAmount, BuildingIndex);
			removeToil.WithProgressBarToilDelay(BuildingIndex);
			removeToil.FailOnCannotTouch(BuildingIndex, PathEndMode.InteractionCell);
			yield return removeToil;

			// Complete removal - spawn subcore on ground
			Toil completeToil = new Toil();
			completeToil.initAction = delegate
			{
				CompSubcoreAutomationBase comp = AutomationComp;
				if (comp != null)
				{
					// Complete the removal and get the subcore
					Thing subcore = comp.CompleteRemoval();
					
					// Place subcore on ground (will be hauled separately)
					if (subcore != null)
					{
						GenPlace.TryPlaceThing(subcore, Building.Position, pawn.Map, ThingPlaceMode.Near);
					}
				}
			};
			completeToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return completeToil;
		}
	}
}
