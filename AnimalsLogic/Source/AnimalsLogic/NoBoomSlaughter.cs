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
            ApplyPatch(typeof(DeathActionWorker_BigExplosion)); // vanilla boomalope
            ApplyPatch(typeof(DeathActionWorker_SmallExplosion)); // vanilla boombat

            ApplyPatch(AccessTools.TypeByName("RimWorld.DeathActionWorker_AntigrainExplosion")); // SoS2 Archolope

            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_BiggerExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_EMPExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_Eggxplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_FrostExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_GargantuanExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_HairballExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_PsionicExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_SmallBomb"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_SmallHairballExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_StunningExplosion"));
            ApplyPatch(AccessTools.TypeByName("GeneticRim.DeathActionWorker_ToxicExplosion"));

            ApplyPatch(AccessTools.TypeByName("MorrowRim.DeathActionWorker_RetchingNetch"));
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
