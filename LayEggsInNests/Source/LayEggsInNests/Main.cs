using Harmony;
using RimWorld;
using System;
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
    static class WorldPathGrid_CalculatedCostAt_Patch
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
}
