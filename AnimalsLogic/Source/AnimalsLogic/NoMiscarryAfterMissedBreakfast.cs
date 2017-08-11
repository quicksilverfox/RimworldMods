using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using System.Reflection.Emit;

namespace AnimalsLogic
{
    class NoMiscarryAfterMissedBreakfast
    {
        [HarmonyPatch(typeof(Hediff_Pregnant), "Tick", new Type[0])]
        static class Hediff_Pregnant_Tick_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // search for this block to insert after it:
                //IL_0038: ldarg.0
                //IL_0039: ldfld class Verse.Pawn Verse.Hediff::pawn
                //IL_003e: ldfld class RimWorld.Pawn_NeedsTracker Verse.Pawn::needs
                //IL_0043: ldfld class RimWorld.Need_Food RimWorld.Pawn_NeedsTracker::food
                //IL_0048: callvirt instance valuetype RimWorld.HungerCategory RimWorld.Need_Food::get_CurCategory()
                //IL_004d: ldc.i4.3
                //IL_004e: bne.un IL_00c2

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Bne_Un)
                    {
                        // insert this stuff:
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0)); // (Hediff)this
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldfld, typeof(Hediff).GetField("pawn")));
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Call, typeof(NoMiscarryAfterMissedBreakfast).GetMethod("IsSeriouslyStarving", new Type[] { typeof(Pawn) })));
                        codes.Insert(i + 4, new CodeInstruction(OpCodes.Brfalse, codes[i].operand));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        public static bool IsSeriouslyStarving(Pawn p)
        {
            Hediff firstHediffOfDef = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition, false);
            if (firstHediffOfDef == null)
            {
                return false;
            }
            return firstHediffOfDef.Severity >= 0.6;
        }
    }
}
