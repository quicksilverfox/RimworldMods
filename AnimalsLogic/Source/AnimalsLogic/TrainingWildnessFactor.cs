using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using System.Reflection;
using System.Reflection.Emit;

namespace AnimalsLogic
{
    /*
     * Ajust training difficulty for wild animals.
     */

    class TrainingWildnessFactor
    {
        [HarmonyPatch]
        public static class Toils_Interpersonal_TryTrain_Patch
        {
            static MethodInfo TargetMethod()
            {
                var toils_inner = typeof(Toils_Interpersonal).GetNestedTypes(AccessTools.all);
                foreach (var inner_class in toils_inner)
                {
                    if (!inner_class.Name.Contains("<TryTrain>"))
                        continue;

                    var methods = inner_class.GetMethods(AccessTools.all);
                    foreach (var method in methods)
                    {
                        if (method.Name.Contains("<>m__"))
                            return method;
                    }
                }
                Log.Error("Animal Logic is unable to detect TryTrain method.");
                return null;
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
