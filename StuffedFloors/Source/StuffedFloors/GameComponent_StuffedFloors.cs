// GameComponent_StuffedFloors.cs
//
// Records which terrain short-hash layout a save was written with, and from scheme 2 on the complete
// layout itself: the short hash of every TerrainDef at the time of saving (see TerrainShortHashes).

using System.Collections.Generic;
using Verse;

namespace StuffedFloors {
    public class GameComponent_StuffedFloors : GameComponent {
        public int terrainHashScheme;

        // defName -> short hash of every TerrainDef when the save was written (scheme 2+)
        public Dictionary<string, int> savedTerrainHashes;

        public GameComponent_StuffedFloors(Game game) {
            // Created while a save is being read means the save had no marker: an older mod version wrote it.
            terrainHashScheme = Scribe.mode == LoadSaveMode.LoadingVars ? 0 : TerrainShortHashes.CurrentScheme;
        }

        public override void ExposeData() {
            if (Scribe.mode == LoadSaveMode.Saving) {
                // terrain is always written with the current hashes; record them so the next load does not
                // depend on how hashes get handed out in that session
                terrainHashScheme = TerrainShortHashes.CurrentScheme;
                savedTerrainHashes = TerrainShortHashes.CaptureCurrentLayout();
            }
            Scribe_Values.Look(ref terrainHashScheme, "terrainHashScheme", 0);
            Scribe_Collections.Look(ref savedTerrainHashes, "terrainHashes", LookMode.Value, LookMode.Value);
        }

        public override void LoadedGame() {
            // only needed while the maps were being read
            savedTerrainHashes = null;
        }
    }
}
