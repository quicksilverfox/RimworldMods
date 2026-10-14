using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	/// <summary>
	/// A set of plants that are grown towards a common harvest time. Immutable once built: the
	/// per-plant multipliers are computed in the constructor and the map component rebuilds groups
	/// from scratch on a fixed interval.
	/// </summary>
	public class Group
	{
		readonly List<Plant> plants;
		readonly List<IntVec3> cells;
		readonly Dictionary<Plant, float> multipliers;

		public Group(List<Plant> plants)
		{
			this.plants = plants;
			cells = plants.Select(p => p.Position).ToList();
			multipliers = new Dictionary<Plant, float>(plants.Count);

			float averageGrowth = plants.Average(p => p.Growth);
			foreach (var plant in plants)
			{
				multipliers[plant] = CalculateMultiplier(plant, averageGrowth);
			}
		}

		public int Count => plants.Count;

		public IReadOnlyList<Plant> Plants => plants;

		public ThingDef PlantDef => plants.Count > 0 ? plants[0].def : null;

		public float MultiplierOf(Plant plant)
		{
			return multipliers.TryGetValue(plant, out float multiplier) ? multiplier : 1f;
		}

		/// <summary>
		/// Scales growth so that every growing plant in the group needs the same number of ticks to
		/// finish as a plant sitting at the group average would: rate ratio = remaining / average remaining.
		/// </summary>
		static float CalculateMultiplier(Plant plant, float averageGrowth)
		{
			if (plant.LifeStage != PlantLifeStage.Growing)
				return 1f;

			float averageRemaining = 1f - averageGrowth;
			if (averageRemaining <= 0f)
				return 1f;

			return (1f - plant.Growth) / averageRemaining;
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
			GenDraw.DrawFieldEdges(cells, colors[colorSeed % colors.Length]);
		}
	}
}
