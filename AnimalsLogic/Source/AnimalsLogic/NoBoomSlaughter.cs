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
            ApplyPatch(typeof(DeathActionWorker_BigExplosion));
            ApplyPatch(typeof(DeathActionWorker_SmallExplosion));

            ApplyPatch(AccessTools.TypeByName("RimWorld.DeathActionWorker_AntigrainExplosion")); // SoS2 Archolope
        }

        static void ApplyPatch(System.Type type)
        {
            if (type != null)
                AnimalsLogic.harmony.Patch(
                    type.GetMethod("PawnDied"),
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
