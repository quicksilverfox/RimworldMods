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
     * Ties animal wildness toanimal age, making taming and training young animals much easier.
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

            AnimalsLogic.harmony.Patch(
                typeof(Toils_Interpersonal) // I really should make a method to automatically go through nested classes
                                            // instead of manually fixing it every time compiler changes its mind
                    .GetNestedType("<>c__DisplayClass10_0", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetMethod("<TryTrain>b__0", BindingFlags.NonPublic | BindingFlags.Instance),
                transpiler: new HarmonyMethod(typeof(GetThemYoung).GetMethod(nameof(TryTrain_Transpiler)))
                );
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Interacted_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var wildnessField = typeof(StatDefOf).GetField(nameof(StatDefOf.Wildness));
            var getStatMethod = typeof(StatExtension).GetMethod(nameof(StatExtension.GetStatValue));
            bool patched = false;

            for (int i = 4; i < codes.Count; i++)
            {
                if (
#pragma warning disable CS0252 // If it is != we got our answer anyway
                    codes[i - 3].opcode == OpCodes.Ldsfld && codes[i - 3].operand == wildnessField &&
                    codes[i].opcode == OpCodes.Call && codes[i].operand == getStatMethod
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
                )
                {
                    // Inject immediately after the GetStatValue call at index i, before the result is written into variable
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Call, typeof(GetThemYoung).GetMethod(nameof(WildnessFactor)))
                    });
                    patched = true;
                    break;
                }
            }

            if(!patched)
            {
                Log.Error("AnimalsLogic: Failed to patch InteractionWorker_RecruitAttempt.Interacted method for WildnessFactor. Probably the game got an update that broke things.");
            }   

            return codes;
        }

        // currently both transpilers are the same, not always the case with different game versions
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TryTrain_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var wildnessField = typeof(StatDefOf).GetField(nameof(StatDefOf.Wildness));
            var getStatMethod = typeof(StatExtension).GetMethod(nameof(StatExtension.GetStatValue));
            bool patched = false;

            for (int i = 4; i < codes.Count; i++)
            {
                if (
#pragma warning disable CS0252 // If it is != we got our answer anyway
                    codes[i - 3].opcode == OpCodes.Ldsfld && codes[i - 3].operand == wildnessField &&
                    codes[i].opcode == OpCodes.Call && codes[i].operand == getStatMethod
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
                )
                {
                    codes.InsertRange(i + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1), // push pawn
                        new CodeInstruction(OpCodes.Call, typeof(GetThemYoung).GetMethod(nameof(WildnessFactor)))
                    });

                    patched = true;
                    break;
                }
            }

            if (!patched)
            {
                Log.Error("AnimalsLogic: Failed to patch Toils_Interpersonal.TryTrain method for WildnessFactor. Probably the game got an update that broke things.");
            }

            return codes;
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
