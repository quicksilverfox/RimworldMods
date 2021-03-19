using HarmonyLib;
using RimWorld;

namespace SyncGrowth
{
	public static class GroupsUtils
	{
		public static float GetGrowthMultiplierFor(Plant plant)
		{
			return plant.GetGrowthMultiplier();
		}

		public static Group GroupOf(Plant plant)
		{
			var comp = plant.Map.GetComponent<MapCompGrowthSync>();

			return comp.GroupOf(plant);
		}

		public static bool HasGroup(Plant plant)
		{
			var comp = plant.Map.GetComponent<MapCompGrowthSync>();
			return comp.allPlantsInGroup.Contains(plant);
		}

		public static float TicksUntilFullyGrown(this Plant plant)
		{
			return (int)typeof(Plant).GetProperty("TicksUntilFullyGrown", AccessTools.all).GetValue(plant, null);
		}

		public static float GrowthPerTick(this Plant plant)
		{
			return (int)typeof(Plant).GetProperty("GrowthPerTick", AccessTools.all).GetValue(plant, null);
		}

	}
}
