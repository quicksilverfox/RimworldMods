using HarmonyLib;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace AnimalsLogic
{
    /*
     * Adds more awareness about predators. Predator hunting one of your pawns (humanlike or animal) is considered hostile to whole your faction, like manhunter would be.
     * 
     * TODO: check if fixed in vanilla
     */

    [HarmonyPatch(typeof(GenHostility), "HostileTo", new Type[] { typeof(Thing), typeof(Thing) })]
    static class GenHostility_IsPredatorHostileTo_Patch
    {
        static void Postfix(ref bool __result, Thing a, Thing b)
        {
            if (__result == false)
            {
                if (Settings.hostile_predators)
                {
                    if (CheckHostile(a, b) || CheckHostile(b, a))
                    {
                        __result = true;
                    }
                }
            }

            //if (Settings.hostile_vermins && (CheckVermin(a, b) || CheckVermin(b, a))) __result = true;
        }

        /*
        private static bool CheckVermin(Thing who, Thing to)
        {
            if (!(who is Pawn))
            {
                return false;
            }

            Faction playerFaction = Faction.OfPlayerSilentFail;
            if (playerFaction == null || !(who.Faction == playerFaction && to.Faction == null || who.Faction == null && to.Faction == playerFaction)) // only works for players, 'cuz why
                return false;

            Pawn agressor = who as Pawn;
            if (agressor.CurJob != null && agressor.jobs.curDriver is JobDriver_Ingest)
            {
                if (agressor.Map.designationManager.DesignationOn(agressor, DesignationDefOf.Tame) != null)
                    return false;

                LocalTargetInfo food = agressor.CurJob.targetA;
                Thing foodThing = food.Thing;
                if (!(foodThing is Plant) || foodThing.def == null)
                    return false;

                Zone z = food.Cell.GetZone(agressor.Map);
                if (z != null && z is Zone_Growing && ((IPlantToGrowSettable)z).GetPlantDefToGrow() == foodThing.def)
                    return true;
            }
            return false;
        }
        */

        private static bool CheckHostile(Thing who, Thing to)
        {
            if (!(who is Pawn) || to.Faction == null)
            {
                return false;
            }

            Pawn agressor = who as Pawn;

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
