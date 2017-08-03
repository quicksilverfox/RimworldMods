﻿using UnityEngine;
using Verse;
using System;

namespace AnimalsLogic
{
    class Settings : ModSettings
    {
        public static bool prevent_eating_stuff = true;
        public static bool hostile_predators = true;
        public static bool fight_back = true;
        public static bool convert_ruined_eggs = true;
        public static float training_wildeness_effect_to = 0;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);

            listing_Standard.CheckboxLabeled("Prevent animals from eating random stuff", ref prevent_eating_stuff);
            listing_Standard.CheckboxLabeled("Predators hunting your pawns are hostile to all your faction", ref hostile_predators);
            listing_Standard.CheckboxLabeled("Convert eggs ruined by temperature into unfertilized chicken eggs", ref convert_ruined_eggs, "Note, this does not affect already ruined eggs.");
            listing_Standard.CheckboxLabeled("More fighting back against melee threats", ref fight_back, "Note, this applies to both melee and tamed animals.");

            listing_Standard.Label("Wildness effect on training. Vanilla — 100%, recommended — 85%, current — " + ((float)Math.Round((1 - training_wildeness_effect_to) / 2 + 0.5, 2)).ToStringPercent() + ".");
            training_wildeness_effect_to = 1 - listing_Standard.Slider(1 - training_wildeness_effect_to, 0, 1);

            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref prevent_eating_stuff, "prevent_eating_stuff", true, false);
            Scribe_Values.Look<bool>(ref hostile_predators, "hostile_predators", true, false);
            Scribe_Values.Look<bool>(ref convert_ruined_eggs, "convert_ruined_eggs", true, false);
            Scribe_Values.Look<bool>(ref convert_ruined_eggs, "fight_back", true, false);
            Scribe_Values.Look<float>(ref training_wildeness_effect_to, "training_wildeness_effect_to", 0, false);
        }
    }
}
