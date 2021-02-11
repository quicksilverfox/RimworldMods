using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	public class Group
	{
		class PlantEntry
		{
			internal readonly Plant Plant;
			internal float multiplier = 1f;

			internal PlantEntry(Plant plant)
			{
				this.Plant = plant;
			}
		}
		readonly List<PlantEntry> plants;

		public Group(IEnumerable<Plant> plants)
		{
			this.plants = plants.Select((Plant arg) => new PlantEntry(arg)).ToList();
			//this.plants.SortBy((arg) => arg.Plant.Growth);
			this.RefreshRates();
		}

		public int Count
		{
			get
			{
				return (plants.Count);
			}
		}

		public IEnumerable<Plant> Plants
		{
			get
			{
				return (plants.Select((arg) => arg.Plant).AsEnumerable());
			}
		}

		public ThingDef PlantDef
		{
			get
			{
				if (!plants.Any())
					return (null);
				return (plants.First().Plant.def);
			}
		}

		public void RefreshRates()
		{
			var averageGrowth = Plants.Average((Plant arg) => arg.Growth);

			foreach (var item in this.plants)
			{
				item.multiplier = CalculateRateFor(item.Plant, averageGrowth);
			}
		}

		float CalculateRateFor(Plant plant, float averageGrowth)
		{
			float mult = 1;
			//float avgticksUntilFullyGrown = Mathf.FloorToInt(averageGrowth / plant.GrowthRate);
			//int avgLongTicksUntilFullyGrown = Mathf.CeilToInt(avgticksUntilFullyGrown / 2000f);
			int longTicksUntilFullyGrown = Mathf.CeilToInt(plant.TicksUntilFullyGrown() / 2000f);

			if (plant.GrowthRate > 0 && longTicksUntilFullyGrown > 0)
			{
				mult += ((averageGrowth - plant.Growth) / longTicksUntilFullyGrown) * 100;
			}

			return (mult);
		}

		internal float GetGrowthMultiplierFor(Plant plant)
		{
			var plantEntry = plants.FirstOrDefault((PlantEntry arg) => arg.Plant == plant);

			if (plantEntry != null)
				return (plantEntry.multiplier);

			return (1f);
		}

		static readonly Color[] colors =
		{
			Color.red,
			Color.yellow,
			Color.blue,
			Color.black
		};

		public void Draw(int colorSeed)
		{
			Color color = colors[(colorSeed % colors.Length)];
			var cells = this.Plants.Select((Plant arg) => arg.Position).ToList();

			GenDraw.DrawFieldEdges(cells, color);
		}
	}
}
