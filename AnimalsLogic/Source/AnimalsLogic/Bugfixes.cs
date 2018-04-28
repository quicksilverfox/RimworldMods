using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace AnimalsLogic
{
    class Bugfixes
    {
        /**
         * Fixes predator/pray power assumption formula to actually account body size. Idea by Mehni, but different (non-detouring) implementation.
         */
        /* I'm not sure this is an actual bug. Stat combatPower seems to be absolute value, not relative to body size. Sure, it means cats and yourkies can't actually hunt, since all small game has nearly same combatPower, but it may screw larger animals.
       [HarmonyPatch(typeof(FoodUtility), "IsAcceptablePreyFor")]
       static class RestUtility_FindPatientBedFor_Patch
       {
           static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
           {
               // Find:
               // ldarg.1
               // ldfld class Verse.Pawn_AgeTracker Verse.Pawn::ageTracker
               // callvirt instance class RimWorld.LifeStageDef Verse.Pawn_AgeTracker::get_CurLifeStage()
               // ldfld float32 RimWorld.LifeStageDef::bodySizeFactor
               // mul

               // Add:
               // ldarg.1
               // callvirt instance class Verse.RaceProperties Verse.Pawn::get_RaceProps()
               // ldfld float32 Verse.RaceProperties::baseBodySize
               // mul

               FieldInfo bodySizeFactor = typeof(LifeStageDef).GetField("bodySizeFactor");

               var codes = new List<CodeInstruction>(instructions);
               for (int i = 0; i < codes.Count; i++)
               {
                   if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == bodySizeFactor)
                   {
                       // i+1 is skipped - we still need that mul there
                       codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_1));
                       codes.Insert(i + 3, new CodeInstruction(OpCodes.Callvirt, typeof(Pawn).GetMethod("get_RaceProps")));
                       codes.Insert(i + 4, new CodeInstruction(OpCodes.Ldfld, typeof(RaceProperties).GetField("baseBodySize")));
                       codes.Insert(i + 5, new CodeInstruction(OpCodes.Mul));

                       // do not break - it should be added in two places!
                   }
               }

               return codes.AsEnumerable();
           }
       }
       */
    }
}
