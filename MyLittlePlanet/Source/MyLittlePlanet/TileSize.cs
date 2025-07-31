using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace WorldGenRules
{
    class WorldGenRules
    {
        public static int subcount = 10;
        
        [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents", new Type[] { typeof(Rect) })]
        static class Page_CreateWorldParams_DoWindowContents_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    // Insert before:
                    // IL_0541: call bool RimWorld.TutorSystem::get_TutorialMode()
                    //FieldInfo searchTarget = AccessTools.Field(typeof(Page_CreateWorldParams), "seedString");
#pragma warning disable CS0252 // Comparation works as intended
                    if (codes[i].opcode == OpCodes.Ldstr && codes[i].operand == "PlanetCoverageTip")
#pragma warning restore CS0252 // 
                    {
                        codes.InsertRange(i + 2, new List<CodeInstruction>(){
                            // this increments vertical offset variable
                            new CodeInstruction(OpCodes.Ldloc_S, 7),
                            new CodeInstruction(OpCodes.Ldc_R4, 40f), // if you use 40 instead of 40f it would push 0 instead...
                            new CodeInstruction(OpCodes.Add),
                            new CodeInstruction(OpCodes.Stloc_S, 7),
                            // loading vertical offset variable and passing it to custom function that draws slider
                            new CodeInstruction(OpCodes.Ldloc_S, 7),
                            new CodeInstruction(OpCodes.Ldloc_S, 8),
                            new CodeInstruction(OpCodes.Call, typeof(Page_CreateWorldParams_DoWindowContents_Patch).GetMethod(nameof(DrawPlanetSizeSlider)))
                        });
                        break;
                    }
                }
                return codes.AsEnumerable();
            }

            public static void DrawPlanetSizeSlider(float num, float width2)
            {
                Widgets.Label(new Rect(0f, num, width2, 30f), "MLPWorldPlanetSize".Translate());
                Rect rect = new Rect(200f, num, width2, 30f);
                subcount = Mathf.RoundToInt(Widgets.HorizontalSlider(rect, subcount, 6f, 10f, true, null, "MLPWorldTiny".Translate(), "MLPWorldDefault".Translate(), 1f));
                PlanetLayerSettingsDefOf.Surface.settings.subdivisions = subcount;
            }
        }
    }
}
