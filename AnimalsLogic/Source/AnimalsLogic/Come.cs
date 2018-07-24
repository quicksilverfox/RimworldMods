using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using System.Reflection;
using Verse.AI;
using System.Reflection.Emit;

namespace AnimalsLogic
{
    /*
     * Pawns react to master fieldwork instantly.
     */

    class Come
    {
        protected static FieldInfo Pawn_JobTracker_pawn = null;

        // public void StartJob(Job newJob, JobCondition lastJobEndCondition = JobCondition.None, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = default(JobTag?));
        [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob", null)]
        static class Pawn_JobTracker_StartJob_Patch
        {
            static void Postfix(Pawn_JobTracker __instance, Job newJob, JobTag? tag)
            {
                if (newJob == null || tag != JobTag.Fieldwork)
                    return;

                if (Pawn_JobTracker_pawn == null)
                    Pawn_JobTracker_pawn = typeof(Pawn_JobTracker).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

                Pawn pawn = (Pawn)Pawn_JobTracker_pawn.GetValue(__instance);

                IEnumerable<Pawn> animals = from p in Find.CurrentMap.mapPawns.AllPawns
                                            where p.RaceProps.Animal && p.Faction == Faction.OfPlayer && p.playerSettings != null && p.playerSettings.Master == pawn && p.playerSettings.followFieldwork
                                            select p;

                foreach (var animal in animals)
                {
                    if (animal.CurJob != null && animal.CurJob.def != JobDefOf.Wait_Combat && animal.CurJob.def != JobDefOf.Rescue && animal.CurJob.def != JobDefOf.AttackMelee && animal.CurJob.def != JobDefOf.AttackStatic && animal.CurJob.def.casualInterruptible)
                    {
                        animal.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                    }
                }
            }
        }
    }
}
