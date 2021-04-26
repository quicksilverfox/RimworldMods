using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace AnimalsLogic
{
    /*
     * Changes egg laying logic to try find a sleeping spot to lay egg there instead of leaving it who knows where. Prevents forbidding of the egg if spot is not found.
     */
    class LayEggsInNests
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                AccessTools.Method(typeof(JobGiver_LayEgg), "TryGiveJob"),
                transpiler: new HarmonyMethod(AccessTools.Method(typeof(LayEggsInNests), nameof(JobGiver_LayEgg_TryGiveJob_Patch)))
                );

            AnimalsLogic.harmony.Patch(
                AccessTools.FirstMethod(typeof(JobDriver_LayEgg), m => m.Name.Contains("<MakeNewToils>")),
                transpiler: new HarmonyMethod(typeof(LayEggsInNests).GetMethod(nameof(JobDriver_LayEgg_MakeNewToils_Patch)))
                );
        }

        [HarmonyTranspiler]
        public static List<CodeInstruction> JobGiver_LayEgg_TryGiveJob_Patch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            MethodInfo target = typeof(RCellFinder).GetMethod("RandomWanderDestFor");

            for (int i = 0; i < codes.Count; i++)
            {
#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
                if (codes[i].opcode == OpCodes.Call && codes[i].operand == target)
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
                {
                    codes[i].operand = typeof(LayEggsInNests).GetMethod(nameof(FindBedOrSpot)); // substitutes original function with mine that tries to find bed instead of any nearby spot
                    return codes;
                }
            }

            Log.Error("Animal Logic is unable to patch JobGiver_LayEgg.TryGiveJob method.");
            return codes;
        }

        public static IntVec3 FindBedOrSpot(Pawn pawn, IntVec3 root, float radius, Func<Pawn, IntVec3, IntVec3, bool> validator, Danger maxDanger) // it replaces call of RCellFinder.RandomWanderDestFor and mimics its arguments - it is both easier and safer than cutting them entirely
        {
            IntVec3 c;
            Building_Bed bed = RestUtility.FindBedFor(pawn);
            if (bed != null)
                c = bed.Position;
            else
                c = RCellFinder.RandomWanderDestFor(pawn, pawn.Position, 5f, null, Danger.Some);
            return c;
        }

        [HarmonyTranspiler]
        public static List<CodeInstruction> JobDriver_LayEgg_MakeNewToils_Patch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            MethodInfo target = typeof(ForbidUtility).GetMethod("SetForbiddenIfOutsideHomeArea");

            for (int i = 0; i < codes.Count; i++)
            {
#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
                if (codes[i].opcode == OpCodes.Call && codes[i].operand == target)
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
                {
                    codes[i].operand = typeof(LayEggsInNests).GetMethod(nameof(ForbidIfNotPlayerFaction)); // substitutes original function with mine that tries to find bed instead of any nearby spot
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0)); // put pawn that laid an egg on top of the stack to pass it to my replacement function
                    return codes;
                }
            }

            Log.Error("Animal Logic is unable to patch JobGiver_LayEgg.MakeNewToils method.");
            return codes;
        }

        public static void ForbidIfNotPlayerFaction(Thing egg, Pawn actor)
        {
            if (actor.Faction == null || actor.Faction != Faction.OfPlayerSilentFail)
            {
                egg.SetForbiddenIfOutsideHomeArea();
            }
        }
    }
}
