using Harmony;
using RimWorld;
using System;
using Verse;

namespace AnimalsLogic
{
    /**
     *  Makes pack-enabled animals in NPC caravans to be considered a chattel if they are not loaded.
     */

    [HarmonyPatch(typeof(TraderCaravanUtility), "GetTraderCaravanRole", new Type[] { typeof(Pawn) })]
    static class Patch_TraderCaravanUtility_GetTraderCaravanRole
    {
        static bool Prefix(ref Pawn p, ref Pawn __state)
        {
            __state = p;
            return true;
        }

        static void Postfix(ref TraderCaravanRole __result, ref Pawn __state)
        {
            if (__state != null && __state.kindDef.RaceProps.packAnimal && !__state.inventory.innerContainer.Any)
            {
                __result = TraderCaravanRole.Chattel;
            }
        }
    }
}
