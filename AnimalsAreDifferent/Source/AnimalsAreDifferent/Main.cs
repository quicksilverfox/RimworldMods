using Harmony;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace AnimalsAreDifferent
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.animalsaredifferent");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(Tradeable_Pawn), "get_Label", new Type[0])]
    static class Patch_Tradeable_Pawn_Label
    {
        static void Postfix(ref String __result, Tradeable_Pawn __instance)
        {
            Pawn p = (Pawn)__instance.AnyThing;
            if (!p.RaceProps.Animal) return;


            String e = Util.AnimalImportantInfo(p);
            if (e.Length > 0)
                __result = "[" + e + "] " + __result;
        }
    }

    [HarmonyPatch(typeof(TransferableOneWay), "get_Label", new Type[0])]
    static class Patch_TransferableOneWay_Label
    {
        static void Postfix(ref String __result, TransferableOneWay __instance)
        {
            if (__instance.AnyThing == null || !(__instance.AnyThing is Pawn)) return;

            Pawn p = (Pawn)__instance.AnyThing;
            if (!p.RaceProps.Animal) return;

            String e = Util.AnimalImportantInfo(p);
            if (e.Length > 0)
                __result = "[" + e + "] " + __result;
        }
    }

    static class Util
    {
        public static string AnimalImportantInfo(Pawn p)
        {
            String e = "";

            // [B]onded
            for (int i = 0; i < p.relations.DirectRelations.Count; i++)
            {
                if (p.relations.DirectRelations[i].def == PawnRelationDefOf.Bond && p.relations.DirectRelations[i].otherPawn.Spawned)
                {
                    //p.relations.DirectRelations[i].otherPawn;
                    e += "B";
                    break;
                }
            }

            // [T]rained
            if (p.training != null)
            {
                int trained = 0;
                foreach (TrainableDef current2 in DefDatabase<TrainableDef>.AllDefs)
                {
                    if (p.training.IsCompleted(current2))
                    {
                        trained++;
                    }
                }
                if (trained > 0)
                {
                    if (e.Length > 0)
                        e += ";";
                    e += "T" + trained;
                }
            }

            // [P]regnant
            if (p.health.hediffSet.HasHediff(HediffDefOf.Pregnant))
            {
                Hediff_Pregnant hediff_Pregnant = (Hediff_Pregnant)p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
                if (hediff_Pregnant.Visible)
                {
                    if (e.Length > 0)
                        e += ";";
                    e += "P" + hediff_Pregnant.GestationProgress.ToStringPercent();
                }
            }
            return e;
        }
    }
}
