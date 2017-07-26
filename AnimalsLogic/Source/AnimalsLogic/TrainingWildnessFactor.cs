using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace AnimalsLogic
{
    class TrainingWildnessFactor
    {
        [HarmonyPatch]
        public static class Toils_Interpersonal_TryTrain_Patch
        {
            static MethodInfo TargetMethod()
            {
                return typeof(Toils_Interpersonal).GetNestedType("<TryTrain>c__AnonStorey26F", AccessTools.all).GetMethod("<>m__E1", AccessTools.all);
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                MethodInfo LerpDouble = typeof(GenMath).GetMethod("LerpDouble");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call)
                    {
                        MethodInfo operand = (MethodInfo)codes[i].operand;
                        if (operand == LerpDouble)
                        {
                            //codes[i - 5] = new CodeInstruction(OpCodes.Ldsfld, typeof(Settings).GetField("training_wildeness_effect_from"));
                            codes[i - 4] = new CodeInstruction(OpCodes.Ldsfld, typeof(Settings).GetField("training_wildeness_effect_to"));
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }
    }
}
