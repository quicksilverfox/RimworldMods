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
            Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Filth), PathEndMode.OnCell, TraverseParms.For(pawn, Danger.Some, TraverseMode.ByPawn, false), 100f, predicate);
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
            bool result;
            if (pawn.Faction != Faction.OfPlayer)
            {
                result = false;
            }
            else
            {
                Filth filth = t as Filth;
                if (filth == null)
                {
                    result = false;
                }
                else if (!filth.Map.areaManager.Home[filth.Position] || !ForbidUtility.InAllowedArea(filth.Position, pawn))
                {
                    result = false;
                }
                else
                {
                    LocalTargetInfo target = t;
                    result = (pawn.CanReserve(target, 1, -1, null, forced) && filth.TicksSinceThickened >= this.MinTicksSinceThickened);
                }
            }
            return result;
        }

        public Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = new Job(JobDefOf.Clean);
            job.AddQueuedTarget(TargetIndex.A, t);
            int num = 15;
            Map map = t.Map;
            Room room = t.GetRoom(RegionType.Set_Passable);
            for (int i = 0; i < 100; i++)
            {
                IntVec3 intVec = t.Position + GenRadial.RadialPattern[i];
                if (intVec.InBounds(map) && intVec.GetRoom(map, RegionType.Set_Passable) == room)
                {
                    List<Thing> thingList = intVec.GetThingList(map);
                    for (int j = 0; j < thingList.Count; j++)
                    {
                        Thing thing = thingList[j];
                        if (this.HasJobOnThing(pawn, thing, forced) && thing != t)
                        {
                            job.AddQueuedTarget(TargetIndex.A, thing);
                        }
                    }
                    if (job.GetTargetQueue(TargetIndex.A).Count >= num)
                    {
                        break;
                    }
                }
            }
            if (job.targetQueueA != null && job.targetQueueA.Count >= 5)
            {
                job.targetQueueA.SortBy((LocalTargetInfo targ) => targ.Cell.DistanceToSquared(pawn.Position));
            }
            return job;
        }
    }
}
