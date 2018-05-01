using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Leeani
{
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = HarmonyInstance.Create("Leeani");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    /*[HarmonyPatch(typeof(JobDriver_PlantWork))]
    [HarmonyPatch("MakeNewToils")]
    [HarmonyPatch(new Type[] {})]
    static class _Patch
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TranspilerMethod(JobDriver_PlantWork __instance, IEnumerable<Toil> __result, MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilgenerator)
        {
            //
            //Original
            //
            List<CodeInstruction> iloriginal = new List<CodeInstruction>(instructions);



            //
            //Override
            //

            return iloriginal;
        }
    }*/
}
