using System;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace AnimalsLogic
{
    /*
     * Makes animals less submissive to their fate, so they would more agressively react to melee threats.
     */

    class AnimalsFightBack
    {
        [HarmonyPatch(typeof(Pawn_MindState), "Notify_DamageTaken", new Type[] { typeof(DamageInfo) })]
        public static class Pawn_MindState_Notify_DamageTaken_Patch
        {
            static bool Prefix(ref DamageInfo __state, DamageInfo dinfo)
            {
                __state = dinfo;
                return true;
            }

            static void Postfix(ref DamageInfo __state, ref Pawn_MindState __instance)
            {
                Pawn defender = __instance.pawn;
                Pawn attacker = __state.Instigator as Pawn;

                if (defender == null || attacker == null || !defender.RaceProps.Animal || defender.Dead || defender.Downed /*|| defender.InMentalState*/ || defender.mindState.meleeThreat == null)
                    return;

                if (defender.CurJob != null && (defender.CurJob.def == JobDefOf.AttackMelee || defender.CurJob.def == JobDefOf.AttackStatic || defender.CurJob.def == JobDefOf.PredatorHunt))
                    return;

                // chance to try fight back any attacker
                bool fight = Rand.Chance(defender.RaceProps.manhunterOnDamageChance);
                //Log.Message("Debug: " + defender + " is attacked. Manhunter chance: " + defender.RaceProps.manhunterOnDamageChance + ", result: " + fight);

                // chance to fight back attacker who is not much stronger
                if (!fight)
                {
                    float powerRatio = 10 * (defender.kindDef.combatPower * defender.health.summaryHealth.SummaryHealthPercent * defender.ageTracker.CurLifeStage.bodySizeFactor)
                        / (attacker.kindDef.combatPower * attacker.health.summaryHealth.SummaryHealthPercent * attacker.ageTracker.CurLifeStage.bodySizeFactor);
                    fight = Rand.Chance(powerRatio);
                    //Log.Message("Debug: " + defender + " is failed to manhunter. Power ratio: " + powerRatio + ", result: " + fight);
                }

                if (fight)
                {
                    //Messages.Message("Debug: " + defender + " is fighting back.", defender, MessageSound.Silent);
                    defender.jobs.StopAll();
                    defender.jobs.StartJob(
                        new Job(JobDefOf.AttackMelee, attacker)
                        {
                            maxNumMeleeAttacks = 1,
                            expiryInterval = 200
                        });
                }
            }
        }
    }
}
