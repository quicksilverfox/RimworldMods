using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using System.Reflection.Emit;

namespace AnimalsLogic
{
    class NoAutoMasterAssignment
    {
        // public void Train(TrainableDef td, Pawn trainer)
        [HarmonyPatch(typeof(Pawn_TrainingTracker), "Train", new Type[] { typeof(TrainableDef), typeof(Pawn) })]
        static class Pawn_TrainingTracker_Train_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // search for this block to insert after it:
                //IL_0027: ldarg.1
                //IL_0028: ldsfld class RimWorld.TrainableDef RimWorld.TrainableDefOf::Obedience
                //IL_002d: bne.un IL_0043

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Bne_Un)
                    {
                        // insert this stuff:
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldsfld, typeof(Settings).GetField("auto_assign_master")));
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Brfalse, codes[i].operand));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }
    }
}
