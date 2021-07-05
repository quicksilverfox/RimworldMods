using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimWorld
{
    // Token: 0x02000004 RID: 4
    public class Housekeep_JobGiver_Cleaning : ThinkNode_JobGiver
    {
        private int MinTicksSinceThickened = 600;

        // Token: 0x06000009 RID: 9
        protected override Job TryGiveJob(Pawn pawn)
        {
            Predicate<Thing> predicate = (Thing t) => t.def.category == ThingCategory.Filth && HasJobOnThing(pawn, t);
            Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Filth), PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Some, TraverseMode.ByPawn), 100f, predicate);
            Job result;
            if (thing == null)
            {
                result = null;
            }
            else
            {
                result = JobOnThing(pawn, thing);
            }
            return result;
        }

        // copy-paste from WorkGiver_CleanFilth

        public bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (pawn.Faction != Faction.OfPlayer)
                return false;

            Filth filth = t as Filth;
            if (filth == null)
                return false;
            if (!filth.Map.areaManager.Home[filth.Position])
                return false;
            if (!pawn.CanReserve(t, 1, -1, null, forced))
                return false;
            if (filth.TicksSinceThickened < MinTicksSinceThickened)
                return false;

            return true;
        }

        public Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Clean);
            job.AddQueuedTarget(TargetIndex.A, t);
            int num = 15;
            Map map = t.Map;
            Room room = t.GetRoom();
            for (int i = 0; i < 100; i++)
            {
                IntVec3 c2 = t.Position + GenRadial.RadialPattern[i];
                if (!ShouldClean(c2))
                {
                    continue;
                }
                List<Thing> thingList = c2.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    Thing thing = thingList[j];
                    if (HasJobOnThing(pawn, thing, forced) && thing != t)
                    {
                        job.AddQueuedTarget(TargetIndex.A, thing);
                    }
                }
                if (job.GetTargetQueue(TargetIndex.A).Count >= num)
                {
                    break;
                }
            }
            if (job.targetQueueA != null && job.targetQueueA.Count >= 5)
            {
                job.targetQueueA.SortBy((LocalTargetInfo targ) => targ.Cell.DistanceToSquared(pawn.Position));
            }
            return job;
            bool ShouldClean(IntVec3 c)
            {
                if (!c.InBounds(map))
                {
                    return false;
                }
                Room room2 = c.GetRoom(map);
                if (room == room2)
                {
                    return true;
                }
                Region region = c.GetDoor(map)?.GetRegion(RegionType.Portal);
                if (region != null && !region.links.NullOrEmpty())
                {
                    for (int k = 0; k < region.links.Count; k++)
                    {
                        RegionLink regionLink = region.links[k];
                        for (int l = 0; l < 2; l++)
                        {
                            if (regionLink.regions[l] != null && regionLink.regions[l] != region && regionLink.regions[l].valid && regionLink.regions[l].Room == room)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }
    }
}
