using RimWorld;
using UnityEngine;
using Verse;

namespace HousekeeperCat
{
	public class IncidentWorker_HousekeeperCatWander : IncidentWorker
	{
		protected override bool CanFireNowSub(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			if (!map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(DefOfCousekeeperCatThingDef.HousekeeperCat))
			{
				return false;
			}
			IntVec3 cell;
			return TryFindEntryCell(map, out cell);
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			if (!TryFindEntryCell(map, out var cell))
			{
				return false;
			}
			PawnKindDef cat = DefOfCousekeeperCatPawnDef.HousekeeperCat;
			IntVec3 result = IntVec3.Invalid;
			if (!RCellFinder.TryFindRandomCellOutsideColonyNearTheCenterOfTheMap(cell, map, 10f, out result))
			{
				result = IntVec3.Invalid;
			}
			
			IntVec3 loc = CellFinder.RandomClosewalkCellNear(cell, map, 10);
			Pawn pawn = PawnGenerator.GeneratePawn(cat);
			GenSpawn.Spawn(pawn, loc, map, Rot4.Random);
			if (result.IsValid)
			{
				pawn.mindState.forcedGotoPosition = CellFinder.RandomClosewalkCellNear(result, map, 10);
			}

			if (FloatRange.ZeroToOne.RandomInRange < 0.25f && !map.PlayerPawnsForStoryteller.EnumerableNullOrEmpty())
			{ // chance to self-tame if there are player pawns present
				pawn.SetFaction(Faction.OfPlayer);
				pawn.training.Train(TrainableDefOf.Obedience, null, true);
				SendStandardLetter("LetterLabelHousekeeperCatJoin".Translate().CapitalizeFirst(), "LetterHousekeeperCatJoin".Translate(), LetterDefOf.PositiveEvent, parms, pawn);
			}
			else
				SendStandardLetter("LetterLabelHousekeeperCatWandersIn".Translate().CapitalizeFirst(), "LetterHousekeeperCatWandersIn".Translate(), LetterDefOf.NeutralEvent, parms, pawn);
			return true;
		}

		private bool TryFindEntryCell(Map map, out IntVec3 cell)
		{
			return RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal + 0.2f);
		}
	}
}