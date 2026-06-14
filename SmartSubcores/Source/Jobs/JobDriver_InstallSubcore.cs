using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Jobs
{
	/// <summary>
	/// Job driver for installing a subcore into a building.
	/// </summary>
	public class JobDriver_InstallSubcore : JobDriver
	{
		private const TargetIndex BuildingIndex = TargetIndex.A;
		private const TargetIndex SubcoreIndex = TargetIndex.B;

		private Thing Building => job.GetTarget(BuildingIndex).Thing;
		private Thing Subcore => job.GetTarget(SubcoreIndex).Thing;
		private CompSubcoreAutomationBase AutomationComp => Building?.TryGetComp<CompSubcoreAutomationBase>();

		public override string GetReport()
		{
			Thing building = Building;
			if (building != null)
				return "SubcoreAutomation_InstallingSubcore".Translate(building.LabelShort);
			return base.GetReport();
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Building, job, 1, -1, null, errorOnFailed) &&
				   pawn.Reserve(Subcore, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// Global fail conditions - only for things that should always cause failure
			this.FailOnDespawnedNullOrForbidden(BuildingIndex);
			this.FailOnBurningImmobile(BuildingIndex);
			this.FailOn(() => AutomationComp == null || !AutomationComp.InstallationRequested);

			// Go to subcore - ONLY check subcore validity BEFORE picking it up
			Toil gotoSubcore = Toils_Goto.GotoThing(SubcoreIndex, PathEndMode.ClosestTouch);
			gotoSubcore.FailOnDespawnedNullOrForbidden(SubcoreIndex);
			yield return gotoSubcore;

			// Pick up subcore
			yield return Toils_Haul.StartCarryThing(SubcoreIndex, putRemainderInQueue: false, subtractNumTakenFromJobCount: false);

			// Go to building
			yield return Toils_Goto.GotoThing(BuildingIndex, PathEndMode.InteractionCell);

			// Do installation work
			float workAmount = AutomationComp?.Props.installWorkAmount ?? 2000f;
			Toil installToil = Toils_General.Wait((int)workAmount, BuildingIndex);
			installToil.WithProgressBarToilDelay(BuildingIndex);
			installToil.FailOnCannotTouch(BuildingIndex, PathEndMode.InteractionCell);
			yield return installToil;

			// Complete installation
			Toil completeToil = new Toil();
			completeToil.initAction = delegate
			{
				CompSubcoreAutomationBase comp = AutomationComp;
				if (comp != null)
				{
					// Destroy the carried subcore
					Thing carried = pawn.carryTracker.CarriedThing;
					if (carried != null)
					{
						carried.Destroy();
					}

					// Complete the installation
					comp.CompleteInstallation();
				}
			};
			completeToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return completeToil;
		}
	}
}
