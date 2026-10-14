// Karel Kroeze
// HarmonyPatch_GenerateImpliedDefs_PreResolve.cs
// 2017-05-07

using HarmonyLib;
using RimWorld;
using Verse;

namespace StuffedFloors {
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public class HarmonyPatch_GenerateImpliedDefs_PreResolve {
        public static void Prefix(bool hotReload) {
            foreach (FloorTypeDef floortype in DefDatabase<FloorTypeDef>.AllDefsListForReading) {
                foreach (ThingDef stuff in Controller.GetStuffDefsFor(floortype.stuffCategories)) {
                    // create and prepare def
                    TerrainDef terrain = floortype.GetStuffedTerrainDef( stuff );

                    // add to databases (same path as vanilla implied defs: PostLoad, short hash, skip on hot reload if present)
                    DefGenerator.AddImpliedDef(terrain, hotReload);
                    Controller.GeneratedTerrains.Add(terrain);
                    if (!hotReload || !floortype.terrains.Any(t => t.defName == terrain.defName)) {
                        floortype.terrains.Add(terrain);
                    }
                }
            }
        }
    }
}
