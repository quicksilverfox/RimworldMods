using UnityEngine;
using Verse;
using System;

namespace AnimalsLogic
{
    class Settings : ModSettings
    {
        public static bool prevent_eating_stuff = true;
        public static bool hostile_predators = true;
        public static bool hostile_vermins = true;
        public static bool fight_back = true;
        public static bool convert_ruined_eggs = true;
        public static bool tastes_like_chicken = false;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);

            listing_Standard.CheckboxLabeled("Prevent animals from eating random stuff", ref prevent_eating_stuff, "Note, it mostly prevents your animals from eating drugs without a nutrition and stuff outside their allowed zones. They would still eat your food if it is in their allowed zone.");
            listing_Standard.CheckboxLabeled("Predators hunting your pawns are hostile to all your faction", ref hostile_predators, "Note, this does not change threat response of your pawns, it only makes them recognize a threat.");
            //listing_Standard.CheckboxLabeled("Wild animals eating your crops are hostile to all your faction", ref hostile_vermins, "Note, this does not change threat response of your pawns, it only makes them recognize a threat. Targets designated for taming are ignored!");
            listing_Standard.CheckboxLabeled("Convert eggs ruined by temperature into unfertilized chicken eggs", ref convert_ruined_eggs, "Note, this does not affect already ruined eggs.");
            listing_Standard.CheckboxLabeled("Convert any generic animal meat into chicken meat upon butchering", ref tastes_like_chicken, "Note, this does not affect already butchered meat.");
            listing_Standard.CheckboxLabeled("More fighting back against melee threats", ref fight_back, "Note, this applies to both wild and tamed animals. If you are using a mod which makes your animals hunt for food, you may want to disable this to avoid spending a lot of meds healing minor wounds on your pets.");

            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref prevent_eating_stuff, "prevent_eating_stuff", true, false);
            Scribe_Values.Look<bool>(ref hostile_predators, "hostile_predators", true, false);
            //Scribe_Values.Look<bool>(ref hostile_vermins, "hostile_vermins", false, false);
            Scribe_Values.Look<bool>(ref convert_ruined_eggs, "convert_ruined_eggs", true, false);
            Scribe_Values.Look<bool>(ref fight_back, "fight_back", true, false);
            Scribe_Values.Look<bool>(ref tastes_like_chicken, "tastes_like_chicken", false, false);
        }
    }
}
