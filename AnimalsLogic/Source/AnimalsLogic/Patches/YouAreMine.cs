using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsLogic.Patches
{
    /*
     * Makes any purchased eggs hatch already tamed.
     */
    class YouAreMine
    {
        [HarmonyPatch(typeof(ITrader), "GiveSoldThingToPlayer", new Type[] {  typeof(Thing), typeof(int), typeof(Pawn) })]
        static class ITrader_GiveSoldThingToPlayer_Patch
        {
            static void Postfix(Thing toGive, int countToGive, Pawn playerNegotiator)
            {
                if (toGive?.TryGetComp<CompHatcher>() != null)
                {
                    toGive.TryGetComp<CompHatcher>().hatcheeFaction = Faction.OfPlayerSilentFail;
                }
            }
        }
    }
}
