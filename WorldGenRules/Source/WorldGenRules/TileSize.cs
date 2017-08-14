using Harmony;
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
    class RulesOverrider : GameComponent
    {
        static int subcount = 10;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref subcount, "subcount", 10, true);
        }

        // Empty constructors are necessary to load
        public RulesOverrider()
        { }

        public RulesOverrider(Game game)
        { }

        [HarmonyPatch(typeof(PlanetShapeGenerator), "DoGenerate", new Type[] { })]
        static class PlanetShapeGenerator_DoGenerate_Patch
        {
            static bool Prefix()
            {
                FieldInfo subdivisionsCount = typeof(PlanetShapeGenerator).GetField("subdivisionsCount", AccessTools.all);
                //subdivisionsCount.SetValue(null, Settings.subdivisionsCount);
                subdivisionsCount.SetValue(null, subcount);
                return true;
            }
        }

        /// <summary>
        ///  Should use transpiler, TBH
        /// </summary>
        [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents", new Type[] { typeof(Rect) })]
        static class Page_CreateWorldParams_DoWindowContents_Patch
        {
            static bool Prefix(Rect rect, ref Rect __state)
            {
                __state = rect;
                return true;
            }

            static void Postfix(ref Rect __state, ref Page_CreateWorldParams __instance)
            {
                GUI.BeginGroup(__state);
                float num = 260; // magic numba!
                Widgets.Label(new Rect(0f, num, 200f, 30f), "Planet Size");
                Rect rect7 = new Rect(200f, num, 200f, 30f);
                subcount = Mathf.RoundToInt(Widgets.HorizontalSlider(rect7, subcount, 6f, 11f, true, null, "Small", "Large", 1f));
                //Settings.subdivisionsCount = Mathf.RoundToInt(Widgets.HorizontalSlider(rect7, Settings.subdivisionsCount, 6f, 11f, true, null, "Small", "Large", 1f));
                GUI.EndGroup();
            }
        }

        // anti-fluckering stuff - changing world layers distance to not overlap with sphere


        [HarmonyPatch]
        static class WorldLayer_Hills_Regenerate_Patch
        {
            static MethodInfo TargetMethod()
            {
                var toils_inner = typeof(WorldLayer_Hills).GetNestedTypes(AccessTools.all);
                foreach (var inner_class in toils_inner)
                {
                    if (!inner_class.Name.Contains("<Regenerate>"))
                        continue;

                    var methods = inner_class.GetMethods(AccessTools.all);
                    foreach (var method in methods)
                    {
                        if (method.Name.Contains("MoveNext"))
                            return method;
                    }
                }
                Log.Error("WorldGenRules.WorldLayer_Hills is unable to detect WorldLayer_Roads inner method.");
                return null;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    // WorldRendererUtility.PrintQuadTangentialToPlanet(pos, origPos, WorldLayer_Hills.BaseSizeRange.RandomInRange * grid.averageTileSize, 0.005f, subMesh, false, true, false);
                    if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.005f)
                    {
                        codes[i].operand = 0.1f;
                        break;
                    }
                }
                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(WorldLayer_Rivers), "FinalizePoint", null)]
        static class WorldLayer_Rivers_FinalizePoint_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    // return inp + inp.normalized * 0.008f;
                    if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.008f)
                    {
                        codes[i].operand = 0.11f;
                        break;
                    }
                }
                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(WorldLayer_Roads), "FinalizePoint", null)]
        static class WorldLayer_Roads_FinalizePoint_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    // return inp + inp.normalized * 0.012f;
                    if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.012f)
                    {
                        codes[i].operand = 0.12f;
                        break;
                    }
                }
                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch]
        static class WorldLayer_SingleTile_Regenerate_Patch
        {
            static MethodInfo TargetMethod()
            {
                var toils_inner = typeof(WorldLayer_SingleTile).GetNestedTypes(AccessTools.all);
                foreach (var inner_class in toils_inner)
                {
                    if (!inner_class.Name.Contains("<Regenerate>"))
                        continue;

                    var methods = inner_class.GetMethods(AccessTools.all);
                    foreach (var method in methods)
                    {
                        if (method.Name.Contains("MoveNext"))
                            return method;
                    }
                }
                Log.Error("WorldGenRules.WorldLayer_SingleTile is unable to detect WorldLayer_Roads inner method.");
                return null;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    // subMesh.verts.Add(this.verts[i] + this.verts[i].normalized * 0.012f);
                    if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.012f)
                    {
                        codes[i].operand = 0.12f;
                        break;
                    }
                }
                return codes.AsEnumerable();
            }
        }
    }
}
