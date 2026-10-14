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
                bool patched = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    // Anchor: ldstr "PlanetCoverageTip"; call TooltipHandler.TipRegionByKey(...)
                    // Insert right after the call, before the vanilla "num += 40f".
                    // Compare by value: on Harmony 3 / CoreCLR the operand is not the interned literal,
                    // so a reference comparison never matches.
                    if (codes[i].Is(OpCodes.Ldstr, "PlanetCoverageTip"))
                    {
                        codes.InsertRange(i + 2, new List<CodeInstruction>(){
                            // this increments vertical offset variable (local 7)
                            CodeInstruction.LoadLocal(7),
                            new CodeInstruction(OpCodes.Ldc_R4, 40f), // if you use 40 instead of 40f it would push 0 instead...
                            new CodeInstruction(OpCodes.Add),
                            CodeInstruction.StoreLocal(7),
                            // loading vertical offset (local 7) and width (local 8) and passing them to custom function that draws slider
                            CodeInstruction.LoadLocal(7),
                            CodeInstruction.LoadLocal(8),
                            CodeInstruction.Call(typeof(Page_CreateWorldParams_DoWindowContents_Patch), nameof(DrawPlanetSizeSlider))
                        });
                        patched = true;
                        break;
                    }
                }
                if (!patched)
                {
                    Log.Error("[My Little Planet] Could not find the PlanetCoverageTip anchor in Page_CreateWorldParams.DoWindowContents; the planet size slider will not be shown.");
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
