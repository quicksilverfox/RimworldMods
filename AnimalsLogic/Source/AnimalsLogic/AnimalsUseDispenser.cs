using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace AnimalsLogic
{
    class AnimalsUseDispenser
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                typeof(FoodUtility).GetMethod("BestFoodSourceOnMap"),
                transpiler: new HarmonyMethod(typeof(AnimalsUseDispenser).GetMethod(nameof(BestFoodSourcePatch)))
                );

            AnimalsLogic.harmony.Patch(
                typeof(FoodUtility).GetMethod("TryFindBestFoodSourceFor"),
                transpiler: new HarmonyMethod(typeof(AnimalsUseDispenser).GetMethod(nameof(BestFoodSourcePatch)))
                );

            AnimalsLogic.harmony.Patch(
                System.Array.Find(
                    typeof(JobDriver_Ingest).GetMethods(AccessTools.all),
                    p => p.Name.Equals("PrepareToIngestToils_Dispenser") // private method
                    ),
                prefix: new HarmonyMethod(typeof(AnimalsUseDispenser).GetMethod(nameof(PrepareToIngestToils_DispenserPrefix)))
                );
        }

        /**
         * Allows non-intelligent pawns to use dispenser. Seriously, even pidgeons are smart enough to understand "press button for food" technology. Well, ok, coalas aren't smart enough, but they are just too dumb to live.
         */
        [HarmonyTranspiler]
        public static List<CodeInstruction> BestFoodSourcePatch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            MethodInfo target = typeof(RaceProperties).GetMethod("get_ToolUser");

            for (int i = 0; i < codes.Count; i++)
            {
#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand == target)
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
                {
                    codes[i].operand = typeof(AnimalsUseDispenser).GetMethod(nameof(ToolUser)); // substitutes original function with mine
                    return codes;
                }
            }

            Log.Error("Animal Logic is unable to patch FoodUtility method.");
            return codes;
        }

        public static bool ToolUser(RaceProperties prop)
        {
            return prop.ToolUser || Settings.use_dispenser;
        }

        /**
         * A dirty, detouring patch, but too much work to transpile.
         */
        [HarmonyPrefix]
        public static bool PrepareToIngestToils_DispenserPrefix(ref JobDriver_Ingest __instance, ref IEnumerable<Toil> __result)
        {
            if (!__instance.pawn.RaceProps.ToolUser)
            {
                __result = PrepareToIngestToils_DispenserOverride(__instance.pawn);
                return false;
            }
            return true;
        }

        /**
         * Damn enumerators
         */
        static private IEnumerable<Toil> PrepareToIngestToils_DispenserOverride(Pawn pawn)
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, pawn);
            //yield return Toils_Ingest.CarryIngestibleToChewSpot(pawn, TargetIndex.A).FailOnDestroyedNullOrForbidden(TargetIndex.A);
            //yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        }
    }
}
