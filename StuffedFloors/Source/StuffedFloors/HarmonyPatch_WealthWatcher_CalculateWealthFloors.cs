// HarmonyPatch_WealthWatcher_CalculateWealthFloors.cs
// Copyright Karel Kroeze, 2018-2018

using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StuffedFloors {
    /**
     * The vanilla game stores cached market values in WealthWatcher.cachedTerrainMarketValue,
     * and indexes this list by def.index. Somehow, even though this cache is initialized after
     * our terrains are generated, these indexes are out of sync. I suspect this is caused by
     * subclassing TerrainDef with FloorTypeDef.
     *
     * Regardless, the cache is fairly pointless, as we don't need to check value for every cell
     * like vanilla does, we only need to check it for each unique terrainDef. In addition, this
     * routine is performed once every 5000 ticks, so less than once per minute. Looping over a
     * map isn't _that_ expensive, so the below approach is simplified.
     *
     *      - Fluffy.
     *
     * Since 1.6 vanilla also counts the foundation layer (gravship substructure etc.), and the
     * top grid may contain nulls; mirror that here.
     */
    [HarmonyPatch(typeof(WealthWatcher), "CalculateWealthFloors")]
    public class HarmonyPatch_WealthWatcher_CalculateWealthFloors {
        public static bool Prefix(Map ___map, ref float __result) {
            TerrainDef[] topGrid = ___map.terrainGrid.topGrid;
            TerrainDef[] foundationGrid = ___map.terrainGrid.foundationGrid;
            FogGrid fogGrid = ___map.fogGrid;
            int n = topGrid.Length;

            Dictionary<TerrainDef, int> counts = new();
            float total = 0f;

            // note that an argument for checking for ownership could be made, but that doesn't
            // exist for floors, so it's a moot point.
            for (int i = 0; i < n; i++) {
                TerrainDef top = topGrid[i];
                if (top != null && !fogGrid.IsFogged(i)) {
                    counts[top] = counts.GetValueSafe(top) + 1;
                }
                TerrainDef foundation = foundationGrid?[i];
                if (foundation != null) {
                    counts[foundation] = counts.GetValueSafe(foundation) + 1;
                }
            }

            foreach (KeyValuePair<TerrainDef, int> terrainCount in counts) {
                total += terrainCount.Key.GetStatValueAbstract(StatDefOf.MarketValue) * terrainCount.Value;
            }

            __result = total;

            return false;
        }
    }
}
