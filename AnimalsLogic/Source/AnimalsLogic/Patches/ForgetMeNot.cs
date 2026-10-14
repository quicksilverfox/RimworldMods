using AnimalsLogic.Patches;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace AnimalsLogic
{
    class ForgetMeNot
    {
        static readonly MethodInfo TamenessCanDecay_Def
            = AccessTools.Method(typeof(TrainableUtility),
                                 "TamenessCanDecay",
                                 new[] { typeof(ThingDef) });

        static readonly MethodInfo TamenessCanDecay_Pawn
            = AccessTools.Method(typeof(TrainableUtility),
                                 "TamenessCanDecay",
                                 new[] { typeof(Pawn) });

        public static void Patch()
        {
            // There are two methods with the same name
            AnimalsLogic.harmony.Patch(
                TamenessCanDecay_Def,
                transpiler: new HarmonyMethod(typeof(ForgetMeNot), nameof(TrainableUtility_TamenessCanDecay_Patch))
                );
            AnimalsLogic.harmony.Patch(
                TamenessCanDecay_Pawn,
                transpiler: new HarmonyMethod(typeof(ForgetMeNot), nameof(TrainableUtility_TamenessCanDecay_Patch))
                );


            AnimalsLogic.harmony.Patch(
                AccessTools.Method(typeof(TrainableUtility),
                                 "DegradationPeriodTicks",
                                 new[] { typeof(ThingDef) }),
                transpiler: new HarmonyMethod(typeof(ForgetMeNot), nameof(TrainableUtility_DegradationPeriodTicks_Patch))
                );
            AnimalsLogic.harmony.Patch(
                AccessTools.Method(typeof(TrainableUtility),
                                 "DegradationPeriodTicks",
                                 new[] { typeof(Pawn) }),
                transpiler: new HarmonyMethod(typeof(ForgetMeNot), nameof(TrainableUtility_DegradationPeriodTicks_Patch))
                );
        }

        // This function is actually inlined and this patch is not working
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TrainableUtility_TamenessCanDecay_Patch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                //ldc.r4 0.101
                if (codes[i].opcode == OpCodes.Ldc_R4) // not checking operand since method is very short
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldsfld, typeof(Settings).GetField(nameof(Settings.wildness_threshold_for_tameness_decay)));
                    break;
                }
            }

            return codes.AsEnumerable();
        }

        // this is workaround
        [HarmonyPatch(typeof(Pawn_TrainingTracker), "TrainingTrackerTickRare")]
        static class Pawn_TrainingTracker_TrainingTrackerTickRare_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PatchTamenessDecay(instructions);
            }
        }
        [HarmonyPatch(typeof(StatWorker_Wildness), "GetExplanationFinalizePart")]
        static class StatWorker_Wildness_GetExplanationFinalizePart_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return PatchTamenessDecay(instructions);
            }
        }

        static IEnumerable<CodeInstruction> PatchTamenessDecay(IEnumerable<CodeInstruction> instructions)
        {

            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for (int i = 0; i < codes.Count; i++)
            {
                // Compare by value (Calls), not by reference: on Harmony 3 / CoreCLR operands are not guaranteed
                // to be the same MethodInfo instance. A method may call both overloads (StatWorker_Wildness does),
                // so every call is redirected instead of stopping at the first one.
                if (codes[i].Calls(TamenessCanDecay_Pawn))
                {
                    codes[i].operand = typeof(ForgetMeNot).GetMethod(nameof(TamenessCanDecay_Pawn_Detour));
                    found = true;
                }
                else if (codes[i].Calls(TamenessCanDecay_Def))
                {
                    codes[i].operand = typeof(ForgetMeNot).GetMethod(nameof(TamenessCanDecay_Thing_Detour));
                    found = true;
                }
            }

            if (!found) Log.Error("[AnimalsLogic] Unable to patch tameness decay: could not find call to TrainableUtility.TamenessCanDecay.");

            return codes.AsEnumerable();
        }

        public static bool TamenessCanDecay_Thing_Detour(ThingDef def)
        {
            if (def.race.FenceBlocked)
            {
                return false;
            }
            return def.GetStatValueAbstract(StatDefOf.Wildness) > Settings.wildness_threshold_for_tameness_decay;
        }

        public static bool TamenessCanDecay_Pawn_Detour(Pawn pawn)
        {
            if (pawn.FenceBlocked) // 1.7 vanilla checks the pawn (roamer + current job), not just the race
            {
                return false;
            }
            return pawn.GetStatValue(StatDefOf.Wildness, applyPostProcess: true, 1) > Settings.wildness_threshold_for_tameness_decay;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TrainableUtility_DegradationPeriodTicks_Patch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var fi = AccessTools.Field(typeof(Settings), nameof(Settings.training_decay_factor));

            var found = false;
            for (int i = 0; i < codes.Count; i++)
            {
                //	Insert before the result is casted to int
                if (codes[i].Calls(AccessTools.Method(typeof(UnityEngine.Mathf), nameof(UnityEngine.Mathf.RoundToInt))))
                {
                    codes.InsertRange(i,
                        new List<CodeInstruction>() {
                                new CodeInstruction(OpCodes.Ldsfld, typeof(Settings).GetField(nameof(Settings.training_decay_factor))),
                                new CodeInstruction(OpCodes.Div)
                        });
                    found = true;
                    break;
                }
            }

            if(!found) Log.Error("[AnimalsLogic] Unable to patch TrainableUtility: could not find call to RoundToInt.");

            return codes.AsEnumerable();
        }
    }
}
