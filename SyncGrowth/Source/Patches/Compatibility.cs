using HarmonyLib;
using System;

namespace SyncGrowth.Patches
{
    /**
     * Patches for other mods
     */
    class Compatibility
    {
        public static void Patch()
        {
            // Orassans - their plants that grow at negative temperatures use extension class
            Type orassanPlants = AccessTools.TypeByName("Orassans.FrostPlant");
            if (orassanPlants != null)
                SyncGrowth.harmony.Patch(
                    orassanPlants.GetMethod("get_GrowthRate"),
                    postfix: new HarmonyMethod(typeof(Plant_GrowthRate_Patch).GetMethod(nameof(Plant_GrowthRate_Patch.Postfix)))
                    );
        }
    }
}
