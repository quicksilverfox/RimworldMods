using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
            // 1. Patch known types (your existing list)
            var knownTypes = new[]
            {
                typeof(DeathActionWorker_BigExplosion),
                typeof(DeathActionWorker_SmallExplosion),
                typeof(DeathActionWorker_ToxCloud),
                AccessTools.TypeByName("RimWorld.DeathActionWorker_AntigrainExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_BiggerExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_EMPExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_Eggxplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_FrostExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_GargantuanExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_HairballExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_PsionicExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_SmallBomb"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_SmallHairballExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_StunningExplosion"),
                AccessTools.TypeByName("GeneticRim.DeathActionWorker_ToxicExplosion"),
                AccessTools.TypeByName("MorrowRim.DeathActionWorker_RetchingNetch"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_AcidExplosion"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_ExplodeAndSpawnEggs"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_GargantuanExplosion"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_LuciferiumExplosion"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_MouseFission"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_RedAcidExplosion"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_SmallRedAcidExplosion"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_SummonEclipse"),
                AccessTools.TypeByName("AlphaBehavioursAndEvents.DeathActionWorker_SummonFlashstorm"),
            };

            var patched = new HashSet<System.Type>();

            foreach (var type in knownTypes)
            {
                if (type != null && patched.Add(type))
                    ApplyPatch(type);
            }

            // 2. Dynamically patch any DeathActionWorker using GenExplosion in PawnDied
            var deathWorkerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(DeathActionWorker).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in deathWorkerTypes)
            {
                if (patched.Contains(type))
                    continue;

                var method = type.GetMethod("PawnDied", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                    continue;

                // Check if method body references GenExplosion
                var body = method.GetMethodBody();
                if (body == null)
                    continue;

                var il = method.GetMethodBody().GetILAsByteArray();
                if (il == null)
                    continue;

                // Simple string check for "GenExplosion" in method's declaring type or referenced types
                if (method.ToString().Contains("GenExplosion") || type.ToString().Contains("Explosion"))
                {
                    ApplyPatch(type);
                }
            }
        }

        static void ApplyPatch(System.Type type)
        {
            if (type == null)
                return;

            if (!typeof(DeathActionWorker).IsAssignableFrom(type))
            {
                Log.Error("Animals Logic: " + type + " is not DeathActionWorker.");
                return;
            }

            var method = type.GetMethod("PawnDied", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                Log.Error("Animals Logic: " + type + " is DeathActionWorker but can't find PawnDied method.");
                return;
            }

            AnimalsLogic.harmony.Patch(
                method,
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
