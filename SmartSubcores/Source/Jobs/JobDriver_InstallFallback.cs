using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Jobs
{
	/// <summary>
	/// Job driver for installing automation using fallback materials (no Biotech).
	/// Materials are consumed from the map when installation completes.
	/// </summary>
	public class JobDriver_InstallFallback : JobDriver
	{
		private const TargetIndex BuildingIndex = TargetIndex.A;
		private const TargetIndex IngredientIndex = TargetIndex.B;

		private Thing Building => job.GetTarget(BuildingIndex).Thing;
		private CompSubcoreAutomationBase AutomationComp => Building?.TryGetComp<CompSubcoreAutomationBase>();

		// Track hauled materials for consumption
		private List<Thing> hauledMaterials = new List<Thing>();

		public override string GetReport()
		{
			Thing building = Building;
			if (building != null)
				return "SubcoreAutomation_InstallingSubcore".Translate(building.LabelShort);
			return base.GetReport();
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (!pawn.Reserve(Building, job, 1, -1, null, errorOnFailed))
				return false;

			// Reserve all materials in the queue
			if (job.targetQueueB != null)
			{
				for (int i = 0; i < job.targetQueueB.Count; i++)
				{
					var target = job.targetQueueB[i];
					int count = (job.countQueue != null && i < job.countQueue.Count) ? job.countQueue[i] : 1;
					
					if (!pawn.Reserve(target, job, 1, count, null, errorOnFailed))
						return false;
				}
			}

			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			// Fail conditions
			this.FailOnDespawnedNullOrForbidden(BuildingIndex);
			this.FailOnBurningImmobile(BuildingIndex);
			this.FailOn(() => AutomationComp == null || !AutomationComp.InstallationRequested);

			// Haul all materials from queue to building
			Toil extractFromQueue = Toils_JobTransforms.ExtractNextTargetFromQueue(IngredientIndex);
			yield return extractFromQueue;

			Toil gotoMaterial = Toils_Goto.GotoThing(IngredientIndex, PathEndMode.ClosestTouch)
				.FailOnDespawnedNullOrForbidden(IngredientIndex);
			yield return gotoMaterial;

			// Use vanilla StartCarryThing - it properly uses job.count set by ExtractNextTargetFromQueue
			yield return Toils_Haul.StartCarryThing(IngredientIndex, putRemainderInQueue: true, subtractNumTakenFromJobCount: false, failIfStackCountLessThanJobCount: true);

			yield return Toils_Goto.GotoThing(BuildingIndex, PathEndMode.InteractionCell);

			// Drop carried material near building and track it
			Toil dropMaterial = new Toil();
			dropMaterial.initAction = delegate
			{
				Thing carried = pawn.carryTracker.CarriedThing;
				if (carried != null)
				{
					pawn.carryTracker.TryDropCarriedThing(Building.Position, ThingPlaceMode.Near, out Thing dropped);
					if (dropped != null)
						hauledMaterials.Add(dropped);
				}
			};
			dropMaterial.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return dropMaterial;

			// Loop back if more materials in queue
			yield return Toils_Jump.JumpIf(extractFromQueue, () => !job.targetQueueB.NullOrEmpty());

			// Do installation work
			float workAmount = AutomationComp?.Props.installWorkAmount ?? 2000f;
			Toil installToil = Toils_General.Wait((int)workAmount, BuildingIndex);
			installToil.WithProgressBarToilDelay(BuildingIndex);
			installToil.FailOnCannotTouch(BuildingIndex, PathEndMode.InteractionCell);
			yield return installToil;

			// Complete installation - consume hauled materials
			Toil completeToil = new Toil();
			completeToil.initAction = delegate
			{
				CompSubcoreAutomationBase comp = AutomationComp;
				if (comp != null)
				{
					// Destroy all hauled materials
					foreach (Thing mat in hauledMaterials)
					{
						if (mat != null && !mat.Destroyed)
							mat.Destroy();
					}
					hauledMaterials.Clear();

					// Complete the installation
					comp.CompleteInstallation();
				}
			};
			completeToil.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return completeToil;
		}
	}
}
