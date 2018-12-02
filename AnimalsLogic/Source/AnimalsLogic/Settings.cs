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
        public static bool convert_ruined_eggs = true;
        public static bool tastes_like_chicken = false;
        public static bool medical_alerts = true;

        public static float wildness_threshold_for_tameness_decay = 0.101f;
        public static float training_decay_factor = 1.0f;
        public static float haul_mtb = 1.5f;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);

            listing_Standard.CheckboxLabeled("Prevent animals from eating random stuff", ref prevent_eating_stuff, "Note, it mostly prevents your animals from eating drugs without a nutrition and stuff outside their allowed zones. They would still eat your food if it is in their allowed zone.");
            listing_Standard.CheckboxLabeled("Predators hunting your pawns are hostile to all your faction", ref hostile_predators, "Note, this does not change threat response of your pawns, it only makes them recognize a threat.");
            //listing_Standard.CheckboxLabeled("Wild animals eating your crops are hostile to all your faction", ref hostile_vermins, "Note, this does not change threat response of your pawns, it only makes them recognize a threat. Targets designated for taming are ignored!");
            listing_Standard.CheckboxLabeled("Convert eggs ruined by temperature into unfertilized chicken eggs", ref convert_ruined_eggs, "Note, this does not affect already ruined eggs.");
            listing_Standard.CheckboxLabeled("Convert any generic animal meat into chicken meat upon butchering", ref tastes_like_chicken, "Note, this does not affect already butchered meat.");
            listing_Standard.CheckboxLabeled("Medical alerts for animals", ref medical_alerts, "Note, shows right-hand alerts for when colony animals are injured, need rescuing or in critical medical condition.");

            listing_Standard.Label("Wildness threshold for tameness decay " + ((float)Math.Round(wildness_threshold_for_tameness_decay, 3) * 100).ToString() + "%. Vanilla: 10.1%.", -1, "Set to 100% to prevent losing tameness for all animals.");
            wildness_threshold_for_tameness_decay = listing_Standard.Slider(wildness_threshold_for_tameness_decay, 0f, 1f);

            listing_Standard.Label("Training decay speed " + ((float)Math.Round(training_decay_factor, 3)).ToStringPercent() + ". Vanilla: 100%.", -1, "Set to 1% to make it as slow as possible.");
            training_decay_factor = listing_Standard.Slider(training_decay_factor, 0.01f, 2f);
            
            listing_Standard.Label("Hauling MTB " + ((float)Math.Round(Math.Round(haul_mtb * 2) / 2f, 1)) + "h. Vanilla: 1.5h.", -1, "'MTB' stands for 'Mean Time Between'");
            haul_mtb = listing_Standard.Slider(haul_mtb, 0.0f, 3f);
            
            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref prevent_eating_stuff, "prevent_eating_stuff", true, false);
            Scribe_Values.Look<bool>(ref hostile_predators, "hostile_predators", true, false);
            //Scribe_Values.Look<bool>(ref hostile_vermins, "hostile_vermins", false, false);
            Scribe_Values.Look<bool>(ref convert_ruined_eggs, "convert_ruined_eggs", true, false);
            Scribe_Values.Look<bool>(ref tastes_like_chicken, "tastes_like_chicken", false, false);
            Scribe_Values.Look<float>(ref wildness_threshold_for_tameness_decay, "wildness_threshold_for_tameness_decay", 0.101f, false);
            Scribe_Values.Look<float>(ref training_decay_factor, "training_decay_factor", 1.0f, false);
            Scribe_Values.Look<float>(ref haul_mtb, "haul_mtb", 1.5f, false);
        }
    }
}
