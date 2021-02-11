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
        readonly List<Group> groups = new List<Group>();
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
                ZoneMode();
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

        private void ZoneMode()
        {
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                if (zone is Zone_Growing zoneGrowing)
                {
                    ThingDef plantDef = zoneGrowing.GetPlantDefToGrow();

                    IEnumerable<Thing> allContainedThings = zoneGrowing.AllContainedThings;
                    List<Plant> plantList = new List<Plant>();

                    float max_grown = 0;
                    foreach (Thing thing in allContainedThings)
                    {
                        if (thing is Plant plant)
                        {
                            if (/*plant.IsCrop && */plant.def == plantDef && plant.LifeStage == PlantLifeStage.Growing)
                            {
                                plantList.Add(plant);
                                max_grown = Math.Max(plant.Growth, max_grown);
                            }
                        }
                    }
#if DEBUG
                    Log.Message("Zone report: ");
                    Log.Message("plantDef " + plantDef);
                    Log.Message("Count " + plantList.Count);
                    Log.Message("max_grown " + max_grown);
#endif
                    if (plantList.NullOrEmpty())
                        break;

                    if (Settings.max_gap < 1)
                        plantList.RemoveAll(p => p.Growth < max_grown - Settings.max_gap);
#if DEBUG
                    Log.Message("Filtered " + plantList.Count);
#endif
                    allPlantsInGroup.AddRange(plantList);
                    var group = new Group(plantList);
#if DEBUG
                    Log.Message("Group " + group.Count);
                    plantList.SortBy(p => p.Growth);
                    Log.Message("Max mult " + group.GetGrowthMultiplierFor(plantList.First()));
                    Log.Message("Group valid " + GroupsUtils.HasGroup(plantList.First()));
#endif

                    groups.Add(group);
                }
            }

            // todo: find all plantable edifices and run old handler on them
        }
    }
}
