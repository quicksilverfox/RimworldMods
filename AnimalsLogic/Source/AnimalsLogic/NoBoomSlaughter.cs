using HarmonyLib;
using RimWorld;
using Verse;

namespace AnimalsLogic
{
    /**
     * Simple one. If pawn is under anestetic, it would not explode on death. You can safely kill boomalopes using medical bill at cost of one herbal medicine. Or you can make a stone room and shoot it to death for free.
     */
    class NoBoomSlaughter
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                typeof(DeathActionWorker_BigExplosion).GetMethod("PawnDied"),
                prefix: new HarmonyMethod(typeof(NoBoomSlaughter).GetMethod(nameof(Explosion_Prefix)))
                );

            AnimalsLogic.harmony.Patch(
                typeof(DeathActionWorker_SmallExplosion).GetMethod("PawnDied"),
                prefix: new HarmonyMethod(typeof(NoBoomSlaughter).GetMethod(nameof(Explosion_Prefix)))
                );
        }

        [HarmonyPrefix]
        public static bool Explosion_Prefix(Corpse corpse)
        {
            if (corpse.InnerPawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
                return false;
            return true;
        }
    }
}
