using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	public class Group
	{
		readonly List<Plant> plants;

		public Group(IEnumerable<Plant> plants)
		{
			this.plants = plants.ToList();
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
				return plants.AsEnumerable();
			}
		}

		public ThingDef PlantDef
		{
			get
			{
				if (!plants.Any())
					return (null);
				return (plants.First().def);
			}
		}

		public void RefreshRates()
		{
			var averageGrowth = Plants.Average((Plant arg) => arg.Growth);

			foreach (var item in this.plants)
			{
				CalculateRateFor(item, averageGrowth);
			}
		}

		void CalculateRateFor(Plant plant, float averageGrowth)
		{
			float mult = 1;
			//float avgticksUntilFullyGrown = Mathf.FloorToInt(averageGrowth / plant.GrowthRate);
			//int avgLongTicksUntilFullyGrown = Mathf.CeilToInt(avgticksUntilFullyGrown / 2000f);
			int longTicksUntilFullyGrown = Mathf.CeilToInt(plant.TicksUntilFullyGrown() / 2000f);

			if (plant.GrowthRate > 0 && longTicksUntilFullyGrown > 0)
			{
				mult += ((averageGrowth - plant.Growth) / longTicksUntilFullyGrown) * 100;
			}

			plant.SetGrowthMultiplier(mult);
		}

		internal float GetGrowthMultiplierFor(Plant plant)
		{
			return plant.GetGrowthMultiplier();
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
	public static class PlantExtension
	{
		//ConditionalWeakTable is available in .NET 4.0+
		//if you use an older .NET, you have to create your own CWT implementation (good luck with that!)
		static readonly ConditionalWeakTable<Plant, GrowthDataEntry> GrowthData = new ConditionalWeakTable<Plant, GrowthDataEntry>();

		public static float GetGrowthMultiplier(this Plant plant) {
            GrowthDataEntry growthDataEntry = GrowthData.GetOrCreateValue(plant);
			if (growthDataEntry.lastUpdate < Find.TickManager.TicksGame - 4000) // never was in group or lost its group
				return 1f;
            return growthDataEntry.Multiplier; 
		}

		public static void SetGrowthMultiplier(this Plant plant, float newValue) {
            GrowthDataEntry growthDataEntry = GrowthData.GetOrCreateValue(plant);
            growthDataEntry.Multiplier = newValue;
			growthDataEntry.lastUpdate = Find.TickManager.TicksGame;
		}

		class GrowthDataEntry
		{
			public float Multiplier = 1f;
			//public Group Group = null;
			public int lastUpdate = -1;
		}
	}
}
