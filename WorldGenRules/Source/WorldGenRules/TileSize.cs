using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Reflection;
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
                float num = 250; // magic numba!
                Widgets.Label(new Rect(0f, num, 200f, 30f), "Planet Size");
                Rect rect7 = new Rect(200f, num, 200f, 30f);
                subcount = Mathf.RoundToInt(Widgets.HorizontalSlider(rect7, subcount, 6f, 11f, true, null, "Small", "Large", 1f));
                //Settings.subdivisionsCount = Mathf.RoundToInt(Widgets.HorizontalSlider(rect7, Settings.subdivisionsCount, 6f, 11f, true, null, "Small", "Large", 1f));
                GUI.EndGroup();
            }
        }
    }
}
