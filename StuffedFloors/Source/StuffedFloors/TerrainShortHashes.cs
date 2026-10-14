// TerrainShortHashes.cs
//
// Map terrain is saved as TerrainDef short hashes. The game assigns those hashes in one pass over all
// TerrainDefs sorted by defName, bumping a def to the next free value when its hash is already taken.
// Our generated terrains (hundreds, with names like "AB_BlocksMudstone_StuffedStoneRoughCobblestones")
// therefore used to steal hashes from vanilla/other-mod terrains that sort after them: "Gravel" (stony
// soil) collides with that very def, so adding this mod to an existing save turned every gravel cell
// into mudstone rough cobblestones.
//
// Two layers of protection:
//  1. FloorTypeDefs and generated stuffed terrains are skipped in the normal pass and hashed afterwards,
//     so this mod never changes the hash of a terrain it did not create. This covers saves written
//     without the mod.
//  2. GameComponent_StuffedFloors stores the hash of every TerrainDef in the save. While the terrain
//     grids of such a save are read, every def temporarily carries its saved hash, so terrain loading no
//     longer depends on which defs exist in the current session or how the game handed out hashes.
//     Saves from older mod versions (no table) are read with the single-pass layout those versions used.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace StuffedFloors {
    public static class TerrainShortHashes {
        // 0: legacy, all terrains hashed in a single pass (mod versions up to 1.6)
        // 1: stuffed terrains hashed after every other terrain
        // 2: as 1, and the save carries the full terrain hash table
        public const int CurrentScheme = 2;

        private static Dictionary<Type, HashSet<ushort>> TakenHashesPerDeftype =>
            AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver), "takenHashesPerDeftype");
        private static readonly MethodInfo giveShortHash = AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash");

        internal static bool assigningDeferred;

        public static bool IsDeferred(Def def) {
            return def is FloorTypeDef || (def is TerrainDef terrain && Controller.GeneratedTerrains.Contains(terrain));
        }

        // Called after ShortHashGiver.GiveAllShortHashes: hash our terrains with whatever is still free.
        public static void AssignDeferred() {
            if (!TakenHashesPerDeftype.TryGetValue(typeof(TerrainDef), out HashSet<ushort> taken)) {
                taken = new HashSet<ushort>();
                TakenHashesPerDeftype[typeof(TerrainDef)] = taken;
            }

            List<TerrainDef> deferred = DefDatabase<TerrainDef>.AllDefsListForReading
                .Where(d => d.shortHash == 0 && IsDeferred(d))
                .OrderBy(d => d.defName)
                .ToList();

            assigningDeferred = true;
            try {
                foreach (TerrainDef terrain in deferred) {
                    giveShortHash.Invoke(null, new object[] { terrain, typeof(TerrainDef), taken });
                }
            } finally {
                assigningDeferred = false;
            }

            DefDatabase<TerrainDef>.InitializeShortHashDictionary();
            // the first pass registered our (then unhashed) defs under 0; drop that stale entry
            AccessTools.StaticFieldRefAccess<Dictionary<ushort, TerrainDef>>(typeof(DefDatabase<TerrainDef>), "defsByShortHash").Remove(0);
        }

        // What gets written into the save: the hash every terrain def has right now.
        public static Dictionary<string, int> CaptureCurrentLayout() {
            Dictionary<string, int> layout = new();
            foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefsListForReading) {
                layout[terrain.defName] = terrain.shortHash;
            }
            return layout;
        }

        // The layout older mod versions produced: one pass over all terrains, mirroring ShortHashGiver.GiveShortHash.
        private static Dictionary<TerrainDef, ushort> _legacyLayout;
        public static Dictionary<TerrainDef, ushort> LegacyLayout {
            get {
                if (_legacyLayout != null) {
                    return _legacyLayout;
                }
                _legacyLayout = new Dictionary<TerrainDef, ushort>();
                HashSet<ushort> taken = new();
                foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefsListForReading.OrderBy(d => d.defName)) {
                    ushort hash = (ushort)(GenText.StableStringHash(terrain.defName) % 65535);
                    while (hash == 0 || taken.Contains(hash)) {
                        hash++;
                    }
                    taken.Add(hash);
                    _legacyLayout[terrain] = hash;
                }
                return _legacyLayout;
            }
        }

        // The layout a save's table describes, completed for defs that did not exist when it was written.
        public static Dictionary<TerrainDef, ushort> LayoutFromTable(Dictionary<string, int> table) {
            // every hash in the table is reserved, including those of defs that no longer exist: their cells
            // must fall back the way vanilla handles a missing terrain, not silently become some other def
            HashSet<ushort> reserved = new();
            foreach (int saved in table.Values) {
                if (saved > 0 && saved <= ushort.MaxValue) {
                    reserved.Add((ushort)saved);
                }
            }

            Dictionary<TerrainDef, ushort> layout = new();
            HashSet<ushort> assigned = new();
            List<TerrainDef> unknown = new();
            foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefsListForReading) {
                if (table.TryGetValue(terrain.defName, out int saved) && saved > 0 && saved <= ushort.MaxValue && assigned.Add((ushort)saved)) {
                    layout[terrain] = (ushort)saved;
                } else {
                    unknown.Add(terrain);
                }
            }

            // Defs added since the save was written cannot occur in it. They only need a hash no saved def
            // uses, so the lookup the game builds while reading stays free of duplicates.
            ushort next = 1;
            foreach (TerrainDef terrain in unknown) {
                ushort hash = terrain.shortHash;
                if (hash == 0 || reserved.Contains(hash) || assigned.Contains(hash)) {
                    while (reserved.Contains(next) || assigned.Contains(next)) {
                        next++;
                    }
                    hash = next;
                }
                assigned.Add(hash);
                layout[terrain] = hash;
            }
            return layout;
        }

        // The layout the save currently being read was written with, or null when the live hashes are right.
        public static Dictionary<TerrainDef, ushort> LayoutForCurrentLoad(out string reason) {
            reason = null;
            if (Scribe.mode != LoadSaveMode.LoadingVars) {
                return null;
            }
            GameComponent_StuffedFloors marker = Current.Game?.GetComponent<GameComponent_StuffedFloors>();
            if (marker == null) {
                return null;
            }
            if (marker.terrainHashScheme >= 2) {
                if (marker.savedTerrainHashes == null) {
                    return null;
                }
                reason = "the terrain hash table stored in the save";
                return LayoutFromTable(marker.savedTerrainHashes);
            }
            if (marker.terrainHashScheme == 1) {
                // deferred layout without a table: the live hashes are the best guess we have
                return null;
            }
            // a save without our marker only used the legacy layout if this mod was active when it was written
            List<string> savedMods = ScribeMetaHeaderUtility.loadedModIdsList;
            string packageId = Controller.Instance?.Content?.PackageId;
            if (savedMods == null || packageId == null
                || !savedMods.Any(id => string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase))) {
                return null;
            }
            reason = "the single-pass layout of older Stuffed Floors versions";
            return LegacyLayout;
        }
    }

    [HarmonyPatch(typeof(ShortHashGiver), "GiveShortHash")]
    public static class HarmonyPatch_ShortHashGiver_GiveShortHash {
        public static bool Prefix(Def def) {
            return TerrainShortHashes.assigningDeferred || !TerrainShortHashes.IsDeferred(def);
        }
    }

    [HarmonyPatch(typeof(ShortHashGiver), nameof(ShortHashGiver.GiveAllShortHashes))]
    public static class HarmonyPatch_ShortHashGiver_GiveAllShortHashes {
        public static void Postfix() {
            TerrainShortHashes.AssignDeferred();
        }
    }

    // While the terrain grids of a save are read, every TerrainDef temporarily carries the hash that save used.
    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.ExposeData))]
    public static class HarmonyPatch_TerrainGrid_ExposeData {
        private static Dictionary<TerrainDef, ushort> _restore;

        public static void Prefix() {
            Dictionary<TerrainDef, ushort> layout = TerrainShortHashes.LayoutForCurrentLoad(out string reason);
            if (layout == null) {
                return;
            }
            Log.Message($"[StuffedFloors] Reading terrain with {reason}.");
            _restore = new Dictionary<TerrainDef, ushort>();
            foreach (KeyValuePair<TerrainDef, ushort> entry in layout) {
                _restore[entry.Key] = entry.Key.shortHash;
                entry.Key.shortHash = entry.Value;
            }
        }

        public static void Finalizer() {
            if (_restore == null) {
                return;
            }
            foreach (KeyValuePair<TerrainDef, ushort> current in _restore) {
                current.Key.shortHash = current.Value;
            }
            _restore = null;
        }
    }
}
