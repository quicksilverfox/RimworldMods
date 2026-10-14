using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	internal static class GroupMaker
	{
		/// <summary>
		/// Default mode: flood-fills groups of adjacent plants of the same kind, starting from every
		/// plant that sits in a growing zone or a plant grower building. Wild plants are never visited.
		/// </summary>
		public static void AdjacencyMode(MapCompGrowthSync comp)
		{
			foreach (var seed in CandidatePlants(comp.map))
			{
				if (comp.HasGroup(seed))
					continue;

				var plants = Collect(comp, seed, ignoreExisting: false, flash: false);
				if (plants.Count > 0)
					comp.AddGroup(new Group(plants));
			}
		}

		/// <summary>
		/// Zone mode: every growing zone is one group, limited to plants within max gap of the most grown one.
		/// </summary>
		public static void ZoneMode(MapCompGrowthSync comp)
		{
			var map = comp.map;
			foreach (var zone in map.zoneManager.AllZones)
			{
				if (!(zone is Zone_Growing growingZone))
					continue;

				ThingDef plantDef = growingZone.GetPlantDefToGrow();
				var plants = new List<Plant>();
				float maxGrowth = 0f;

				foreach (var cell in growingZone.Cells)
				{
					Plant plant = cell.GetPlant(map);
					if (plant == null || plant.def != plantDef || plant.LifeStage != PlantLifeStage.Growing)
						continue;
					plants.Add(plant);
					maxGrowth = Math.Max(maxGrowth, plant.Growth);
				}

				if (plants.Count == 0)
					continue;

				if (Settings.max_gap < 1f)
					plants.RemoveAll(p => p.Growth < maxGrowth - Settings.max_gap);

				if (plants.Count > 0)
					comp.AddGroup(new Group(plants));
			}
		}

		/// <summary>Dev-mode visualisation: flood-fills from the plant and flashes the cells it would group.</summary>
		public static void FlashGroup(MapCompGrowthSync comp, Plant seed)
		{
			Collect(comp, seed, ignoreExisting: true, flash: true);
		}

		static IEnumerable<Plant> CandidatePlants(Map map)
		{
			foreach (var zone in map.zoneManager.AllZones)
			{
				if (!(zone is Zone_Growing growingZone))
					continue;
				foreach (var cell in growingZone.Cells)
				{
					Plant plant = cell.GetPlant(map);
					if (plant != null)
						yield return plant;
				}
			}

			foreach (var building in map.listerBuildings.allBuildingsColonist)
			{
				if (!(building is IPlantToGrowSettable))
					continue;
				foreach (var cell in building.OccupiedRect())
				{
					Plant plant = cell.GetPlant(map);
					if (plant != null)
						yield return plant;
				}
			}
		}

		/// <summary>
		/// Breadth-first flood fill over 4-way adjacent cells. The growth range of the whole group is
		/// tracked, so no group ever spans more than max gap regardless of traversal order.
		/// </summary>
		static List<Plant> Collect(MapCompGrowthSync comp, Plant seed, bool ignoreExisting, bool flash)
		{
			var map = comp.map;
			var result = new List<Plant>();

			if (!CanHaveGroup(comp, seed, ignoreExisting))
				return result;

			var visited = new HashSet<Plant> { seed };
			var queue = new Queue<Plant>();
			queue.Enqueue(seed);
			float minGrowth = seed.Growth;
			float maxGrowth = seed.Growth;

			while (queue.Count > 0)
			{
				Plant plant = queue.Dequeue();
				result.Add(plant);

				if (flash)
					map.debugDrawer.FlashCell(plant.Position, (result.Count % 10) / 10f, "#" + result.Count, 300);

				foreach (var cell in CellsAdjacent4Way(plant.Position))
				{
					if (!cell.InBounds(map))
						continue;

					Plant neighbour = cell.GetPlant(map);
					if (neighbour == null || visited.Contains(neighbour))
						continue;
					if (!CanGroupTogether(plant, neighbour) || !CanHaveGroup(comp, neighbour, ignoreExisting))
						continue;

					float newMin = Math.Min(minGrowth, neighbour.Growth);
					float newMax = Math.Max(maxGrowth, neighbour.Growth);
					if (newMax - newMin > Settings.max_gap)
					{
						if (flash)
							map.debugDrawer.FlashCell(neighbour.Position, 0f, "max gap", 300);
						continue;
					}

					minGrowth = newMin;
					maxGrowth = newMax;
					visited.Add(neighbour);
					queue.Enqueue(neighbour);
				}
			}

			return result;
		}

		static IEnumerable<IntVec3> CellsAdjacent4Way(IntVec3 seed)
		{
			yield return seed + IntVec3.North;
			yield return seed + IntVec3.East;
			yield return seed + IntVec3.South;
			yield return seed + IntVec3.West;
		}

		/// <summary>A plant can be synced when it is the crop its growing zone or grower building is set to.</summary>
		static bool CanHaveGroup(MapCompGrowthSync comp, Plant plant, bool ignoreExisting)
		{
			if (!ignoreExisting && comp.HasGroup(plant))
				return false;

			IPlantToGrowSettable host = plant.Position.GetPlantToGrowSettable(plant.Map);
			return host != null && host.GetPlantDefToGrow() == plant.def;
		}

		static bool CanGroupTogether(Plant alpha, Plant beta)
		{
			return alpha.def == beta.def
				&& Mathf.Approximately(alpha.GrowthRateFactor_Light, beta.GrowthRateFactor_Light)
				&& Mathf.Approximately(alpha.GrowthRateFactor_Fertility, beta.GrowthRateFactor_Fertility);
		}
	}
}
