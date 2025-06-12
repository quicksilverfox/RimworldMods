using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace AnimalsLogic
{
    /**
     *  Changes animals in caravan froming screen and trade screen to contain more important info: bond, pregnancy, training.
     */

    [HarmonyPatch(typeof(Tradeable_Pawn), "get_Label", new Type[0])]
    static class Patch_Tradeable_Pawn_Label
    {
        static void Postfix(ref String __result, Tradeable_Pawn __instance)
        {
            if (!Settings.trade_tags)
                return;

            Pawn p = (Pawn)__instance.AnyThing;
            if (!p.RaceProps.Animal) return;


            String e = AnimalImportantInfoUtil.AnimalImportantInfo(p);
            if (e.Length > 0)
                __result = "[" + e + "] " + __result;
        }
    }

    [HarmonyPatch(typeof(TransferableOneWay), "get_Label", new Type[0])]
    static class Patch_TransferableOneWay_Label
    {
        static void Postfix(ref String __result, TransferableOneWay __instance)
        {
            if (!Settings.trade_tags)
                return;

            if (__instance.AnyThing == null || !(__instance.AnyThing is Pawn)) return;

            Pawn p = (Pawn)__instance.AnyThing;
            if (!p.RaceProps.Animal) return;

            String e = AnimalImportantInfoUtil.AnimalImportantInfo(p, true);
            if (e.Length > 0)
                __result = "[" + e + "] " + __result;
        }
    }

    static class AnimalImportantInfoUtil
    {
        public static string AnimalImportantInfo(Pawn p, bool gender = false)
        {
            String e = "";

            if (!Settings.trade_tags)
                return e;

            // M/F
            if (gender && p.RaceProps.hasGenders)
            {
                if (e.Length > 0)
                    e += ";";
                e += p.gender.ToString().Substring(0, 1);
            }

            // [T]rained
            if (p.training != null)
            {
                int trained = 0;
                foreach (TrainableDef current2 in DefDatabase<TrainableDef>.AllDefs)
                {
                    if (p.training.HasLearned(current2))
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

            // [W]ool
            CompShearable wool = p.TryGetComp<CompShearable>();
            if (wool != null && wool.Fullness > 0.05)
            {
                if (e.Length > 0)
                    e += ";";
                e += "W" + wool.Fullness.ToStringPercent();
            }

            return e;
        }
    }

    public static class ShowAnimalRelations
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                AccessTools.Method(typeof(SocialCardUtility), "ShouldShowPawnRelations"),
                postfix: new HarmonyMethod(typeof(ShowAnimalRelations).GetMethod(nameof(ShouldShowPawnRelations_Postfix)))
                );
        }

        [HarmonyPostfix]
        public static void ShouldShowPawnRelations_Postfix(ref bool __result, Pawn pawn, Pawn selPawnForSocialInfo)
        {

            if (__result || !Settings.always_show_relations)
                return;

            if (pawn.relations.everSeenByPlayer && pawn.RaceProps.Animal && (pawn.Name == null || pawn.Name.Numerical))
                __result = true;

            return;
        }
    }
}