using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using System.Reflection.Emit;

namespace AnimalsLogic
{
    class ForgetMeNot
    {
        [HarmonyPatch(typeof(TrainableUtility), "TamenessCanDecay", new Type[] { typeof(ThingDef) })]
        static class TrainableUtility_TamenessCanDecay_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
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
        }

        [HarmonyPatch(typeof(TrainableUtility), "DegradationPeriodTicks", new Type[] { typeof(ThingDef) })]
        static class TrainableUtility_DegradationPeriodTicks_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    //	IL_001b: mul
                    if (codes[i].opcode == OpCodes.Mul) // not checking operand since method is very short
                    {
                        codes.InsertRange(i,
                            new List<CodeInstruction>() {
                                new CodeInstruction(OpCodes.Ldsfld, typeof(Settings).GetField(nameof(Settings.wildness_threshold_for_tameness_decay))),
                                new CodeInstruction(OpCodes.Div)
                            });
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }
    }
}
