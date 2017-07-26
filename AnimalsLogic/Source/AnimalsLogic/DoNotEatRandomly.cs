using Harmony;
using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace AnimalsLogic
{
    /*
     * Prevents animals from eating random stuff.
     */

    class DoNotEatRandomly
    {
        [HarmonyPatch(typeof(JobGiver_EatRandom), "TryGiveJob", new Type[] { typeof(Pawn) })]
        static class JobGiver_EatRandom_TryGiveJob_Patch
        {
            static void Postfix(ref Job __result)
            {
                if (Settings.prevent_eating_stuff)
                {
                    __result = null;
                }
            }
        }
    }
}
