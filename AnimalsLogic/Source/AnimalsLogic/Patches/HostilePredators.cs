using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Patches
{
    /*
     * Adds more awareness about predators. Predator hunting one of your pawns (humanlike or animal) is considered hostile to whole your faction, like manhunter would be.
     *
     * Vanilla already does this between two pawns (within 12 cells of the prey) and for the Thing-vs-Faction overload, which is
     * what puts the predator into AttackTargetsCache for the player faction. The Thing-vs-Thing overload, however, only runs its
     * predator check when both sides are pawns, so a turret filtering those cached targets (AttackTargetsCache.GetPotentialTargetsFor,
     * AttackTargetFinder) never sees the predator as hostile. This postfix covers any faction-owned thing, not just pawns, and
     * drops the distance limit.
     */
    class HostilePredators
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                AccessTools.Method(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Thing) }),
                postfix: new HarmonyMethod(typeof(HostilePredators).GetMethod(nameof(HostileToThing_Postfix)))
                );
        }

        [HarmonyPostfix]
        public static void HostileToThing_Postfix(ref bool __result, Thing a, Thing b)
        {
            if (__result || !Settings.hostile_predators)
                return;

            bool aIsPredator = a is Pawn pa && pa.RaceProps?.predator == true;
            bool bIsPredator = b is Pawn pb && pb.RaceProps?.predator == true;

            if (aIsPredator && IsPredatorTargetingFaction((Pawn)a, b))
            {
                __result = true;
            }
            else if (bIsPredator && IsPredatorTargetingFaction((Pawn)b, a))
            {
                __result = true;
            }
        }
        private static bool IsPredatorTargetingFaction(Pawn predator, Thing targetThing)
        {
            if (!predator.Spawned)
                return false;

            // targetThing is deliberately not restricted to pawns: turrets are the whole point of this patch
            if (!targetThing.Spawned || targetThing.Faction == null)
                return false;

            return
                targetThing.Faction.HasPredatorRecentlyAttackedAnyone(predator) ||
                GetPreyOfMyFaction(predator, targetThing.Faction) != null;
        }

        // copy-paste from GenHostility
        private static Pawn GetPreyOfMyFaction(Pawn predator, Faction myFaction)
        {
            Job curJob = predator.CurJob;
            if (curJob != null && curJob.def == JobDefOf.PredatorHunt && !predator.jobs.curDriver.ended)
            {
                Pawn pawn = curJob.GetTarget(TargetIndex.A).Thing as Pawn;
                if (pawn != null && pawn.Faction == myFaction)
                {
                    return pawn;
                }
            }
            return null;
        }
    }
}
