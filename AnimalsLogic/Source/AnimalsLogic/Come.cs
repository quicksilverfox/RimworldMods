using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using System.Reflection;
using Verse.AI;

namespace AnimalsLogic
{
    /*
     * Animals react to zone restriction changes and master draft/fieldwork instantly.
     */

    class Come
    {
        // Try to find allowed area immediately after restrictions are changed
        protected static FieldInfo Pawn_PlayerSettings_pawn = null;
        protected static FieldInfo Pawn_PlayerSettings_areaAllowedInt = null;
        protected static JobGiver_SeekAllowedArea thinkNode_JobGiver = null;

        [HarmonyPatch(typeof(Pawn_PlayerSettings), "set_AreaRestriction", new Type[] { typeof(Area) })]
        static class Pawn_PlayerSettings_set_AreaRestriction_Patch
        {
            static void Postfix(ref Pawn_PlayerSettings __instance)
            {
                if (Pawn_PlayerSettings_pawn == null)
                    Pawn_PlayerSettings_pawn = typeof(Pawn_PlayerSettings).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);
                if (Pawn_PlayerSettings_areaAllowedInt == null)
                    Pawn_PlayerSettings_areaAllowedInt = typeof(Pawn_PlayerSettings).GetField("areaAllowedInt", BindingFlags.NonPublic | BindingFlags.Instance);

                Pawn p = (Pawn)Pawn_PlayerSettings_pawn.GetValue(__instance);
                Area a = (Area)Pawn_PlayerSettings_areaAllowedInt.GetValue(__instance);
                
                if (ForbidUtility.InAllowedArea(p.Position, p))
                {
                    return;
                }
                
                if (p.CurJob != null && !p.CurJob.def.casualInterruptible)
                {
                    return;
                }

                if (thinkNode_JobGiver == null)
                    thinkNode_JobGiver = (JobGiver_SeekAllowedArea)Activator.CreateInstance(typeof(JobGiver_SeekAllowedArea));
                ThinkResult thinkResult = thinkNode_JobGiver.TryIssueJobPackage(p, default(JobIssueParams));

                if (thinkResult.Job != null)
                {
                    p.jobs.StopAll();
                    p.jobs.StartJob(thinkResult.Job, JobCondition.None, null, false, true, null, null);
                }
            }
        }

        protected static FieldInfo Pawn_JobTracker_pawn = null;

        // public void StartJob(Job newJob, JobCondition lastJobEndCondition = JobCondition.None, ThinkNode jobGiver = null, bool resumeCurJobAfterwards = false, bool cancelBusyStances = true, ThinkTreeDef thinkTree = null, JobTag? tag = default(JobTag?));
        [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob", null)]
        static class Pawn_JobTracker_StartJob_Patch
        {
            static void Postfix(ref Pawn_JobTracker __instance, Job newJob, JobTag? tag)
            {
                if (newJob == null || tag != JobTag.Fieldwork)
                    return;

                if (Pawn_JobTracker_pawn == null)
                    Pawn_JobTracker_pawn = typeof(Pawn_JobTracker).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

                Pawn pawn = (Pawn)Pawn_JobTracker_pawn.GetValue(__instance);

                IEnumerable<Pawn> animals = from p in Find.VisibleMap.mapPawns.AllPawns
                                            where p.RaceProps.Animal && p.Faction == Faction.OfPlayer && p.playerSettings != null && p.playerSettings.master == pawn && p.playerSettings.followFieldwork
                                            select p;

                foreach (var animal in animals)
                {
                    if (animal.CurJob != null && animal.CurJob.def != JobDefOf.WaitCombat && animal.CurJob.def != JobDefOf.Rescue && animal.CurJob.def != JobDefOf.AttackMelee && animal.CurJob.def != JobDefOf.AttackStatic && animal.CurJob.def.casualInterruptible)
                    {
                        animal.jobs.StopAll();
                        animal.jobs.JobTrackerTick();
                    }
                }
            }
        }

        protected static FieldInfo Pawn_DraftController_draftedInt = null;

        [HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted", new Type[] { typeof(bool) })]
        static class Pawn_DraftController_Drafted_Patch
        {
            static void Postfix(ref Pawn_DraftController __instance)
            {
                if (Pawn_DraftController_draftedInt == null)
                    Pawn_DraftController_draftedInt = typeof(Pawn_DraftController).GetField("draftedInt", BindingFlags.NonPublic | BindingFlags.Instance);

                if (!(bool)Pawn_DraftController_draftedInt.GetValue(__instance))
                {
                    return;
                }


                Pawn pawn = __instance.pawn;

                IEnumerable<Pawn> animals = from p in Find.VisibleMap.mapPawns.AllPawns
                                            where p.RaceProps.Animal && p.Faction == Faction.OfPlayer && p.playerSettings != null && p.playerSettings.master == pawn && p.playerSettings.followDrafted
                                            select p;

                foreach (var animal in animals)
                {
                    if (animal.CurJob != null && animal.CurJob.def != JobDefOf.WaitCombat && animal.CurJob.def != JobDefOf.Rescue && animal.CurJob.def != JobDefOf.AttackMelee && animal.CurJob.def != JobDefOf.AttackStatic && animal.CurJob.def.casualInterruptible)
                    {
                        animal.jobs.StopAll();
                        animal.jobs.JobTrackerTick();
                    }
                }
            }
        }
    }
}
