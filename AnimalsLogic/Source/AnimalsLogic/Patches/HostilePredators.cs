using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace AnimalsLogic.Patches
{
    /*
     * Adds more awareness about predators. Predator hunting one of your pawns (humanlike or animal) is considered hostile to whole your faction, like manhunter would be.
     */
    class HostilePredators
    {
        public static void Patch()
        {
            // fixes turrets vs predators - vanilla method only works for pawns because it has an explicit check if both actors are pawns
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
            if (!predator.Spawned || !predator.RaceProps.predator)
                return false;

            if (!(targetThing is Pawn) || !targetThing.Spawned || targetThing.Faction == null)
                return false;

            return
                targetThing.Faction.HasPredatorRecentlyAttackedAnyone(predator) ||
                GetPreyOfMyFaction(predator, targetThing.Faction) != null;
        }

        private static bool CheckHostile(Thing who, Thing to)
        {
            if (!(who is Pawn) || to.Faction == null)
            {
                return false;
            }

            Pawn agressor = who as Pawn;

            if (!agressor.Spawned || !(agressor.RaceProps?.predator ?? false) || !to.Spawned)
            {
                return false;
            }

            if (to.Faction.HasPredatorRecentlyAttackedAnyone(agressor) || GetPreyOfMyFaction(agressor, to.Faction) != null)
            {
                return true;
            }

            return false;
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
