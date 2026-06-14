using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;
using Verse.AI;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Allows any mechanoid to pick up and finish an UnfinishedThing started by
	/// another mechanoid (e.g. one Fabricor continues a recipe a different
	/// Fabricor began). Vanilla locks UFTs to their original Creator.
	///
	/// Two patch points in <see cref="WorkGiver_DoBill"/>:
	///   1. ClosestUnfinishedThingForBill — validator filters by Creator == pawn.
	///      We postfix: if vanilla returned null and this pawn is a mechanoid,
	///      re-run the search allowing UFTs whose Creator is also a mechanoid.
	///   2. FinishUftJob — early-out logs an error and returns null when
	///      Creator != pawn. We prefix: if both are mechanoids, transfer the
	///      Creator to the new pawn so the rest of vanilla's job builder works
	///      unchanged.
	///
	/// "Capable of the recipe" check is implicit: WorkGiver_DoBill only invokes
	/// these methods after the pawn has already passed PawnAllowedToStartAnew
	/// for the bill, which validates skill / work-type requirements.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class MechSharedUftPatches
	{
		private static MethodInfo _closestUftMethod;

		static MechSharedUftPatches()
		{
			try
			{
				var harmony = new Harmony("SubcoreAutomation.MechSharedUft");

				_closestUftMethod = AccessTools.Method(typeof(WorkGiver_DoBill), "ClosestUnfinishedThingForBill");
				if (_closestUftMethod != null)
				{
					harmony.Patch(_closestUftMethod,
						postfix: new HarmonyMethod(typeof(MechSharedUftPatches), nameof(ClosestUnfinishedThingForBill_Postfix)));
				}
				else
				{
					Log.Error("[SubcoreAutomation] MechSharedUft BROKEN: WorkGiver_DoBill.ClosestUnfinishedThingForBill not found!");
				}

				var finishUft = AccessTools.Method(typeof(WorkGiver_DoBill), "FinishUftJob");
				if (finishUft != null)
				{
					harmony.Patch(finishUft,
						prefix: new HarmonyMethod(typeof(MechSharedUftPatches), nameof(FinishUftJob_Prefix)));
				}
				else
				{
					Log.Error("[SubcoreAutomation] MechSharedUft BROKEN: WorkGiver_DoBill.FinishUftJob not found!");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] MechSharedUft patch failed: {ex}");
			}
		}

		public static void ClosestUnfinishedThingForBill_Postfix(Pawn pawn, Bill_ProductionWithUft bill, ref UnfinishedThing __result)
		{
			try
			{
				if (__result != null)
					return;
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechSharedUftCraftingEnabled)
					return;
				if (!IsMechanoid(pawn))
					return;
				if (bill?.recipe?.unfinishedThingDef == null)
					return;

				bool Validator(Thing t)
				{
					if (t.IsForbidden(pawn))
						return false;
					UnfinishedThing uft = t as UnfinishedThing;
					if (uft == null)
						return false;
					if (uft.Recipe != bill.recipe)
						return false;
					if (!IsMechanoid(uft.Creator))
						return false;
					if (!uft.ingredients.TrueForAll(x => bill.IsFixedOrAllowedIngredient(x.def)))
						return false;
					if (!pawn.CanReserve(t))
						return false;
					return true;
				}

				__result = (UnfinishedThing)GenClosest.ClosestThingReachable(
					pawn.Position,
					pawn.Map,
					ThingRequest.ForDef(bill.recipe.unfinishedThingDef),
					PathEndMode.InteractionCell,
					TraverseParms.For(pawn, pawn.NormalMaxDanger()),
					9999f,
					Validator);
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in ClosestUnfinishedThingForBill_Postfix: {ex.Message}", 93827494);
			}
		}

		public static void FinishUftJob_Prefix(Pawn pawn, UnfinishedThing uft)
		{
			try
			{
				if (uft == null || pawn == null)
					return;
				if (uft.Creator == pawn)
					return;
				if (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.mechSharedUftCraftingEnabled)
					return;
				if (!IsMechanoid(pawn) || !IsMechanoid(uft.Creator))
					return;

				// Transfer ownership so vanilla's `if (uft.Creator != pawn)` guard passes.
				// Creator setter also updates the displayed creatorName on the inspect string.
				uft.Creator = pawn;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in FinishUftJob_Prefix: {ex.Message}", 93827495);
			}
		}

		private static bool IsMechanoid(Pawn p)
		{
			return p != null && p.RaceProps != null && p.RaceProps.IsMechanoid;
		}
	}
}
