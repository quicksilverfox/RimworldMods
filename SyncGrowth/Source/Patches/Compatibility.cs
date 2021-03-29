using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SyncGrowth.Patches
{
    /**
     * Patches for other mods. Probably should instead go over all assembly and patch all extending Plant type.
     */
    class Compatibility
    {
        public static void Patch()
        {
            // List of qualified names of classes that extend RimWorld.Plant and should be patched
            string[] Overrides = new string[]
            {
                // Orassans - their plants that grow at negative temperatures use extension class
                "Orassans.FrostPlant",

                // VanillaPlantsExpanded has a lot of dirty overrides
                "VanillaPlantsExpanded.Plant_ChecksRiver",
                "VanillaPlantsExpanded.Plant_HighTempAffected",
                "VanillaPlantsExpanded.Plant_RainAffected",
                "VanillaPlantsExpanded.Plant_StopsGrowingInToxins",
                "VanillaPlantsExpanded.Plant_WinterBlooming", // does not actually have any overrides to patch but whatever
                "VanillaPlantsExpanded.Plant_WinterResistant",

                // Mushrooms
                "Caveworld_Flora_Unleashed.FruitingBody", // it won't show inspect string for them but should work
            };

            // Applying patches to get_GrowthRate and/or GetInspectString
            foreach (String ovr in Overrides)
            {
                Type plants = AccessTools.TypeByName(ovr);
                if (plants != null)
                {
                    MethodInfo method = AccessTools.DeclaredMethod(plants, "get_GrowthRate");
                    if (method != null && method.DeclaringType == plants)
                    SyncGrowth.harmony.Patch(
                        method,
                        postfix: new HarmonyMethod(typeof(Plant_GrowthRate_Patch).GetMethod(nameof(Plant_GrowthRate_Patch.Postfix)))
                        );

                    method = AccessTools.DeclaredMethod(plants, "GetInspectString");
                    if (method != null)
                        SyncGrowth.harmony.Patch(
                            method,
                            postfix: new HarmonyMethod(typeof(Plant_GetInspectString_Patch).GetMethod(nameof(Plant_GetInspectString_Patch.Postfix)))
                            );
                }
            }
        }
    }
}
