using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SyncGrowth
{
	public class MapCompGrowthSync : MapComponent
	{
		const int RebuildInterval = 2000;

		readonly List<Group> groups = new List<Group>();
		readonly Dictionary<Plant, Group> groupByPlant = new Dictionary<Plant, Group>();

		// Negative until the first tick, so a freshly loaded map gets its groups right away instead of
		// waiting for the next interval boundary. Maps are then offset from each other to spread the work.
		int nextRebuildTick = -1;

		public MapCompGrowthSync(Map map) : base(map)
		{
		}

		public static MapCompGrowthSync For(Map map)
		{
			return map?.GetComponent<MapCompGrowthSync>();
		}

		public int Count => groups.Count;

		public bool HasGroup(Plant plant)
		{
			return groupByPlant.ContainsKey(plant);
		}

		public Group GroupOf(Plant plant)
		{
			return groupByPlant.TryGetValue(plant, out Group group) ? group : null;
		}

		public float GrowthMultiplierOf(Plant plant)
		{
			return groupByPlant.TryGetValue(plant, out Group group) ? group.MultiplierOf(plant) : 1f;
		}

		internal void AddGroup(Group group)
		{
			groups.Add(group);
			foreach (var plant in group.Plants)
			{
				groupByPlant[plant] = group;
			}
		}

		void Clear()
		{
			groups.Clear();
			groupByPlant.Clear();
		}

		public override void MapComponentTick()
		{
			if (!Settings.mod_enabled)
			{
				if (groups.Count > 0)
					Clear();
				return;
			}

			int ticks = Find.TickManager.TicksGame;
			if (nextRebuildTick < 0)
				nextRebuildTick = ticks + (map.uniqueID % 16) * 60;
			if (ticks < nextRebuildTick)
				return;
			nextRebuildTick = ticks + RebuildInterval;

			Clear();
			if (Settings.zone_mode)
				GroupMaker.ZoneMode(this);
			else
				GroupMaker.AdjacencyMode(this);
		}

		/// <summary>
		/// Outlines the group of the selected plant. In dev mode the Misc1 key re-runs the flood fill
		/// from the selected plant and flashes the visited cells.
		/// </summary>
		public override void MapComponentUpdate()
		{
			if (!Settings.mod_enabled || Settings.zone_mode || !Settings.draw_overlay)
				return;
			if (Find.CurrentMap != map || !WorldRendererUtility.DrawingMap)
				return;
			if (!(Find.Selector.SingleSelectedThing is Plant plant) || plant.Map != map)
				return;

			if (Prefs.DevMode && KeyBindingDefOf.Misc1.JustPressed)
				GroupMaker.FlashGroup(this, plant);

			GroupOf(plant)?.Draw(0);
		}
	}
}
