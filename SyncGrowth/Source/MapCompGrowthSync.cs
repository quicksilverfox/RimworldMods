using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using Verse;

namespace SyncGrowth
{
    public class MapCompGrowthSync : MapComponent
    {
        public readonly List<Group> groups = new List<Group>();
        public readonly List<Plant> allPlantsInGroup = new List<Plant>();

        public int Count
        {
            get
            {
                return (groups.Count);
            }
        }

        public MapCompGrowthSync(Map map) : base(map)
        {
        }

        public float GetGrowthMultiplierFor(Plant plant)
        {
            var group = GroupOf(plant);

            if (group == null)
                return (1f);

            return group.GetGrowthMultiplierFor(plant);
        }

        public Group GroupOf(Plant plant)
        {
            var result = (this.groups.FirstOrDefault((obj) => obj.Plants.Contains(plant)));
            return (result);
        }

        public override void MapComponentTick()
        {
            if (!Settings.mod_enabled)
                return;

            if (Find.TickManager.TicksGame % 2000 != 0)
                return;

            groups.Clear();
            allPlantsInGroup.Clear();

            if (Settings.zone_mode)
            {
                GroupMaker.ZoneMode(this);
                return;
            }

            var plants = this.map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);

            foreach (Plant item in plants)
            {
#if DEBUG
                var timer = Stopwatch.StartNew();
#endif
                var group = GroupMaker.TryCreateGroup(this, item);

                if (group != null)
                {
                    groups.Add(group);
#if DEBUG
                    timer.Stop();
                    Log.Message("Created group of " + group.Count + " " + group.PlantDef + " (" + timer.Elapsed.TotalMilliseconds.ToString("0.000 ms") + ")");
#endif
                }
            }
        }
    }
}
