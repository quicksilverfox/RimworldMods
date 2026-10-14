using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace AnimalsLogic.Patches
{
    /**
     * Ties animal wildness to animal age, making taming and training young animals much easier.
     * 
     * TODO: make animals remember trainer and build up rapport with them.
     */
    class GetThemYoung
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                typeof(InteractionWorker_RecruitAttempt).GetMethod("Interacted"),
                transpiler: new HarmonyMethod(typeof(GetThemYoung).GetMethod(nameof(Interacted_Transpiler)))
                );

            // Find the TryTrain delegate method dynamically to handle compiler-generated name changes
            var tryTrainMethod = FindTryTrainDelegateMethod();
            if (tryTrainMethod != null)
            {
                AnimalsLogic.harmony.Patch(
                    tryTrainMethod,
                    transpiler: new HarmonyMethod(typeof(GetThemYoung).GetMethod(nameof(TryTrain_Transpiler)))
                    );
            }
            else
            {
                Log.Warning("[AnimalsLogic] Could not find TryTrain delegate method to patch. Young animal taming bonus may not work for training.");
            }
        }

        /// <summary>
        /// Dynamically finds the compiler-generated delegate method used in Toils_Interpersonal.TryTrain
        /// that contains the Wildness stat lookup we need to patch.
        /// </summary>
        private static MethodInfo FindTryTrainDelegateMethod()
        {
            var wildnessField = typeof(StatDefOf).GetField(nameof(StatDefOf.Wildness));
            var getStatMethod = typeof(StatExtension).GetMethod(nameof(StatExtension.GetStatValue));

            // Search all nested types in Toils_Interpersonal for methods that reference Wildness stat
            foreach (var nestedType in typeof(Toils_Interpersonal).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
            {
                foreach (var method in nestedType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    try
                    {
                        var instructions = PatchProcessor.GetCurrentInstructions(method);
                        if (instructions == null) continue;

                        // Look for a method that loads StatDefOf.Wildness and calls GetStatValue
                        bool hasWildness = false;
                        bool hasGetStat = false;
                        foreach (var instr in instructions)
                        {
                            if (instr.LoadsField(wildnessField)) hasWildness = true;
                            if (instr.Calls(getStatMethod)) hasGetStat = true;
                        }

                        if (hasWildness && hasGetStat)
                            return method;
                    }
                    catch
                    {
                        // Skip methods that can't be analyzed
                    }
                }
            }

            return null;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Interacted_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ScaleWildnessByAge(instructions, "InteractionWorker_RecruitAttempt.Interacted");
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TryTrain_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ScaleWildnessByAge(instructions, "Toils_Interpersonal.TryTrain");
        }

        /// <summary>
        /// Finds the first `animal.GetStatValue(StatDefOf.Wildness, ...)` call and multiplies its result by the
        /// age factor. The animal is re-pushed by duplicating whatever instruction loaded it for the original
        /// call (ldarg/ldloc), so this does not depend on local variable numbering of a given game build.
        /// </summary>
        static IEnumerable<CodeInstruction> ScaleWildnessByAge(IEnumerable<CodeInstruction> instructions, string targetName)
        {
            var codes = instructions.ToList();
            var wildnessField = typeof(StatDefOf).GetField(nameof(StatDefOf.Wildness));
            var getStatMethod = typeof(StatExtension).GetMethod(nameof(StatExtension.GetStatValue));
            bool patched = false;

            // Expected shape: <load animal>; ldsfld Wildness; ldc.i4 applyPostProcess; ldc.i4 cacheStaleAfterTicks; call GetStatValue
            for (int i = 4; i < codes.Count; i++)
            {
                if (!codes[i].Calls(getStatMethod) || !codes[i - 3].LoadsField(wildnessField))
                    continue;

                var animalLoad = codes[i - 4];
                if (!IsSimpleLoad(animalLoad.opcode))
                {
                    Log.Error($"AnimalsLogic: Failed to patch {targetName} for WildnessFactor: unexpected instruction '{animalLoad}' before the Wildness lookup.");
                    return codes;
                }

                // Inject immediately after the GetStatValue call, before the result is consumed
                codes.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(animalLoad.opcode, animalLoad.operand), // push the animal again (no labels copied)
                    new CodeInstruction(OpCodes.Call, typeof(GetThemYoung).GetMethod(nameof(WildnessFactor)))
                });
                patched = true;
                break;
            }

            if (!patched)
            {
                Log.Error($"AnimalsLogic: Failed to patch {targetName} for WildnessFactor. Probably the game got an update that broke things.");
            }

            return codes;
        }

        static bool IsSimpleLoad(OpCode op)
        {
            return op == OpCodes.Ldarg_0 || op == OpCodes.Ldarg_1 || op == OpCodes.Ldarg_2 || op == OpCodes.Ldarg_3
                || op == OpCodes.Ldarg || op == OpCodes.Ldarg_S
                || op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3
                || op == OpCodes.Ldloc || op == OpCodes.Ldloc_S;
        }

        public static float WildnessFactor(float wildness, Pawn recipient)
        {
            if (!Settings.taming_age_factor)
                return wildness;

            if (recipient?.def?.race?.lifeStageAges == null || recipient.def.race.lifeStageAges.Empty() || recipient.ageTracker == null)
                return wildness;

            LifeStageAge matureAge = recipient.def.race.lifeStageAges
                .FirstOrFallback(
                    p => p.def.reproductive || p.def.milkable || p.def.shearable,
                    recipient.def.race.lifeStageAges.Last()
                );

            if (matureAge == null || matureAge.minAge <= 0f)
                return wildness;

            float ageFactor = Math.Min(recipient.ageTracker.AgeBiologicalYearsFloat / matureAge.minAge, 1);
            ageFactor *= (float)Math.Pow(ageFactor, 0.33f);
            return wildness * ageFactor;
        }
    }
}
