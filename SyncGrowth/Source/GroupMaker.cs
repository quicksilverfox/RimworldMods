using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SyncGrowth
{
	internal static class GroupMaker
	{
		static List<ThingDef> defsBlacklist = new List<ThingDef>
		{
			ThingDefOf.Plant_Grass,
            ThingDef.Named("Plant_Berry")
		};

		internal static Group TryCreateGroup(MapCompGrowthSync mapComp, Plant crop, bool flashcells = false)
		{
			if (!flashcells && GroupsUtils.HasGroup(crop))
				return (null);

			var list = new HashSet<Plant>();

			try
			{
				if (!GroupsUtils.HasGroup(crop))
					Iterate(mapComp, crop, list, crop.Growth, crop.Growth, flashcells, IntVec3.Invalid);
			}
			catch (Exception ex)
			{
				Log.Warning("failed to create group from crop " + crop + " at " + crop.Position + " : " + ex.Message + ". " + ex.StackTrace);
				return (null);
			}

			if (flashcells || list.Count <= 0)
				return (null);

			return (new Group(list));
		}

		static void Iterate(MapCompGrowthSync mapComp, Plant crop, ICollection<Plant> list, float minGrowth, float maxGrowth, bool flashcells, IntVec3 previous)
		{
			if (!CanHaveGroup(crop, flashcells) || list.Contains(crop))
				return;

			if (Settings.draw_overlay && flashcells)
			{
				var count = crop.Map.GetComponent<MapCompGrowthSync>().Count;
				crop.Map.debugDrawer.FlashCell(crop.Position, list.Count * 10000000, "#" + list.Count, 300);
			}

			list.Add(crop);
			mapComp.allPlantsInGroup.Add(crop);

			foreach (IntVec3 cell in CellsAdjacent4Way(crop.Position))
			{
				if (cell == previous || !cell.InBounds(crop.Map))
					continue;
				Plant plantAtCell = cell.GetPlant(crop.Map);

				if (plantAtCell != null && CanGroupTogether(crop, plantAtCell))
				{
					if (Math.Max(plantAtCell.Growth, maxGrowth) - Math.Min(plantAtCell.Growth, minGrowth) > Settings.max_gap)
					{
						if (Settings.draw_overlay && flashcells)
						{
							crop.Map.debugDrawer.FlashCell(crop.Position, float.MinValue, "max gap", 300);
						}

						continue;
					}

					maxGrowth = Math.Max(plantAtCell.Growth, maxGrowth);
					minGrowth = Math.Min(plantAtCell.Growth, minGrowth);
					Iterate(mapComp, plantAtCell, list, minGrowth, maxGrowth, flashcells, crop.Position);
				}
			}
		}

		public static IEnumerable<IntVec3> CellsAdjacent4Way(IntVec3 seed)
		{
			yield return (seed + IntVec3.North);
			yield return (seed + IntVec3.East);
			yield return (seed + IntVec3.South);
			yield return (seed + IntVec3.West);
        }

        public static bool CanHaveGroup(Plant plant, bool flashcells)
		{
			var ingestible = plant.def.ingestible;
			var plantprops = plant.def.plant;

			if (ingestible == null || ingestible.foodType == FoodTypeFlags.Tree)
				return false;
			if (plantprops == null || !plantprops.Sowable /*|| plantprops.IsTree*/)
				return false;
			if (plant.LifeStage != PlantLifeStage.Growing)
				return false;
			if (defsBlacklist.Contains(plant.def))
				return false;
			if (!flashcells && GroupsUtils.HasGroup(plant))
				return false;

			return true;
		}

		static bool CanGroupTogether(Plant alpha, Plant beta)
		{
			return (alpha.def == beta.def &&
					//(alpha.GrowthRateFactor_Light == beta.GrowthRateFactor_Light) &&
					//(alpha.GrowthRateFactor_Fertility == beta.GrowthRateFactor_Fertility));
					Math.Abs(alpha.GrowthRateFactor_Light - beta.GrowthRateFactor_Light) < float.Epsilon &&
					Math.Abs(alpha.GrowthRateFactor_Fertility - beta.GrowthRateFactor_Fertility) < float.Epsilon);
		}
	}
}