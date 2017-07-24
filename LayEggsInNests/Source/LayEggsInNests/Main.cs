using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;

namespace LayEggsInNests
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.layeggsinnests");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(JobGiver_LayEgg), "TryGiveJob", new Type[] { typeof(Pawn) })]
    static class JobGiver_LayEgg_TryGiveJob_Patch
    {
        static bool Prefix(ref Job __result, Pawn pawn)
        {
            CompEggLayer compEggLayer = pawn.TryGetComp<CompEggLayer>();
            if (compEggLayer == null || !compEggLayer.CanLayNow)
            {
                return false;
            }
            IntVec3 c;
            Building_Bed bed = RestUtility.FindBedFor(pawn);
            if (bed!=null)
                c = bed.Position;
            else
                c = RCellFinder.RandomWanderDestFor(pawn, pawn.Position, 5f, null, Danger.Some);
            __result = new Job(JobDefOf.LayEgg, c);
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_LayEgg), "MakeNewToils", new Type[0])]
    static class JobDriver_LayEgg_MakeNewToils_Patch
    {
        static bool Prefix(ref IEnumerable<Toil> __result, JobDriver_LayEgg __instance)
        {
            __result = new List<Toil>
            {
                Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell),
                new Toil
                {
                    defaultCompleteMode = ToilCompleteMode.Delay,
                    defaultDuration = 500
                },
                new Toil
                {
                    initAction = delegate
                    {
                        Pawn actor = __instance.pawn;
                        Thing forbiddenIfOutsideHomeArea =GenSpawn.Spawn(actor.GetComp<CompEggLayer>().ProduceEgg(), actor.Position, __instance.pawn.Map);
                        if (actor.Faction == null || !actor.Faction.IsPlayer)
                        {
                            forbiddenIfOutsideHomeArea.SetForbiddenIfOutsideHomeArea();
                        }
                    },
                    defaultCompleteMode = ToilCompleteMode.Instant
                }
            };
            return false;
        }
    }
}
