using Harmony;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace AnimalsLogic
{
    /*
     * Pets and bonded animals do not interrupt sleep.
     */

    class HushMyPet
    {
        protected static FieldInfo lastSleepDisturbedTick = null;

        // Verse.Pawn
        // public void HearClamor(Pawn source, ClamorType type)
        [HarmonyPatch(typeof(Pawn), "HearClamor", new Type[] { typeof(Pawn), typeof(ClamorType) })]
        static class Pawn_HearClamor_Patch
        {
            static bool Prefix(ref Pawn __instance, Pawn source, ClamorType type)
            {
                if (lastSleepDisturbedTick == null)
                    lastSleepDisturbedTick = typeof(Pawn).GetField("lastSleepDisturbedTick", BindingFlags.NonPublic | BindingFlags.Instance);

                if (__instance.Dead)
                {
                    return true;
                }

                if (type == ClamorType.Movement && __instance.needs.mood != null && source.RaceProps.Animal && !__instance.Awake() && __instance.Faction == Faction.OfPlayer && Find.TickManager.TicksGame > (int)lastSleepDisturbedTick.GetValue(__instance) + 300)
                {
                    // Is pet-type animal
                    if (source.RaceProps.petness > 0)
                        return false;

                    // Is bonded animal
                    foreach (DirectPawnRelation item in __instance.relations.DirectRelations) // bonded animal
                        if (item.def == PawnRelationDefOf.Bond && item.otherPawn == source)
                            return false;
                }

                return true;
            }
        }
    }
}
