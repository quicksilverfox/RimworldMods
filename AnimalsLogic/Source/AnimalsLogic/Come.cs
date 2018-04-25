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
     * Animals react to zone restriction changes and master draft/fieldwork instantly.
     */

    class Come
    {
        // Try to find allowed area immediately after restrictions are changed
        protected static FieldInfo Pawn_PlayerSettings_pawn = null;

        // Does not work. Setter is inlined by JIT compiler. See workaround below.
        /*
        [HarmonyPatch(typeof(Pawn_PlayerSettings), "set_AreaRestriction", new Type[] { typeof(Area) })]
        static class Pawn_PlayerSettings_set_AreaRestriction_Patch
        {
            static void Postfix(Pawn_PlayerSettings __instance, Area value)
            {
                if (Pawn_PlayerSettings_pawn == null)
                    Pawn_PlayerSettings_pawn = typeof(Pawn_PlayerSettings).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

                ValidateArea((Pawn)Pawn_PlayerSettings_pawn.GetValue(__instance));
            }
        }
        */

        // Workaround for set_AreaRestriction - patching calling instances. Not the best idea, but probably safe. Probably.
        #region AreaRestriction
        // AFAIK Harmony can't patch methods in batch, so have to apply the same patch for each methid.
        [HarmonyPatch(typeof(AreaAllowedGUI), "DoAreaSelector")]
        static class AreaAllowedGUI_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RunTranspiler(instructions);
            }
        }
        [HarmonyPatch(typeof(PawnColumnWorker_AllowedArea), "HeaderClicked")]
        static class PawnColumnWorker_AllowedArea_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RunTranspiler(instructions);
            }
        }
        [HarmonyPatch]
        static class InspectPaneFiller_Patch
        {
            static MethodInfo TargetMethod()
            {
                // Inner method of the private method of the internal class. Delightful.
                MethodInfo method = typeof(Pawn).Assembly.GetType("RimWorld.InspectPaneFiller").GetNestedTypes(AccessTools.all).First(
                        inner_class => inner_class.Name.Contains("<DrawAreaAllowed>")
                    ).GetMethods(AccessTools.all).First(
                        m => m.Name.Contains("<>m__")
                    );

                if (method == null)
                    Log.Error("Animal Logic is unable to detect InspectPaneFiller inner method.");

                return method;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RunTranspiler(instructions);
            }
        }

        // Common tool for patching each one of the methods
        private static IEnumerable<CodeInstruction> RunTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo set_AreaRestriction = typeof(Pawn_PlayerSettings).GetMethod("set_AreaRestriction");

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                // find: callvirt instance void RimWorld.Pawn_PlayerSettings::set_AreaRestriction(class Verse.Area)
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == set_AreaRestriction)
                {
                    // add after: ValidateArea call
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Come).GetMethod("ValidateArea", new Type[] { typeof(Pawn) })));

                    // add before: duplicate argument to use with ValidateArea
                    codes.Insert(i - 2, new CodeInstruction(OpCodes.Dup));
                    break;
                }
            }

            return codes.AsEnumerable();
        }
        #endregion

        public static void ValidateArea(Pawn p)
        {
            if (ForbidUtility.InAllowedArea(p.Position, p))
            {
                return;
            }

            if (p.CurJob != null && !p.CurJob.def.casualInterruptible)
            {
                return;
            }

            p.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
        }

        //////////////////////////////////////////////////////////////////

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

                IEnumerable<Pawn> animals = from p in Find.VisibleMap.mapPawns.AllPawns
                                            where p.RaceProps.Animal && p.Faction == Faction.OfPlayer && p.playerSettings != null && p.playerSettings.master == pawn && p.playerSettings.followFieldwork
                                            select p;

                foreach (var animal in animals)
                {
                    if (animal.CurJob != null && animal.CurJob.def != JobDefOf.WaitCombat && animal.CurJob.def != JobDefOf.Rescue && animal.CurJob.def != JobDefOf.AttackMelee && animal.CurJob.def != JobDefOf.AttackStatic && animal.CurJob.def.casualInterruptible)
                    {
                        animal.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                    }
                }
            }
        }

        //protected static FieldInfo Pawn_DraftController_draftedInt = null;

        //[HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted", new Type[] { typeof(bool) })]
        //static class Pawn_DraftController_Drafted_Patch
        //{
        //    static void Postfix(ref Pawn_DraftController __instance)
        //    {
        //        if (__instance == null || __instance.pawn == null || __instance.pawn.Faction != Faction.OfPlayer || Find.VisibleMap == null || Find.VisibleMap.mapPawns == null)
        //        {
        //            return;
        //        }

        //        if (Pawn_DraftController_draftedInt == null)
        //            Pawn_DraftController_draftedInt = typeof(Pawn_DraftController).GetField("draftedInt", BindingFlags.NonPublic | BindingFlags.Instance);

        //        if (!(bool)Pawn_DraftController_draftedInt.GetValue(__instance))
        //        {
        //            return;
        //        }


        //        Pawn pawn = __instance.pawn;

        //        IEnumerable<Pawn> animals = from p in Find.VisibleMap.mapPawns.AllPawns
        //                                    where p.RaceProps.Animal && p.Faction == Faction.OfPlayer && p.playerSettings != null && p.playerSettings.master == pawn && p.playerSettings.followDrafted
        //                                    select p;

        //        foreach (var animal in animals)
        //        {
        //            if (animal.CurJob != null && animal.CurJob.def != JobDefOf.WaitCombat && animal.CurJob.def != JobDefOf.Rescue && animal.CurJob.def != JobDefOf.AttackMelee && animal.CurJob.def != JobDefOf.AttackStatic && animal.CurJob.def.casualInterruptible)
        //            {
        //                animal.jobs.StopAll();
        //                animal.jobs.JobTrackerTick();
        //            }
        //        }
        //    }
        //}
    }
}
