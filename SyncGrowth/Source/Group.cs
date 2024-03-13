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
            //float longTicksUntilFullyGrown = plant.TicksUntilFullyGrown() / 2000f;

            //if (plant.GrowthRate > 0 && longTicksUntilFullyGrown > 0)
            //{
            //	mult += ((averageGrowth - plant.Growth) / longTicksUntilFullyGrown) * 200;
            //}

            if (plant.GrowthRate > 0 && plant.LifeStage == PlantLifeStage.Growing)
			{
				mult = (1 - plant.Growth) / (1 - averageGrowth);
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

	// Basically this thing simulated adding a new field to Plant things to store group info
	// This thing is quite slow, probably an array tat mimics the map grid would be WAY faster, but it does not seem to be a bottleneck so meh
	public static class PlantExtension
	{
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

		public class GrowthDataEntry
		{
			//public WeakReference<Plant> Plant = null;
            //public WeakReference<Group> Group = null;
            public float Multiplier = 1f;
			public int lastUpdate = -1;
		}
	}
}
