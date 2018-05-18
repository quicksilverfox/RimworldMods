using UnityEngine;
using Verse;
using System;

namespace Logistics
{
    class Settings : ModSettings
    {
        public static bool settlement_mod = true;
        public static bool snow_mod = true;
        public static float biome_time_modifier = 1f;
        public static float hillness_time_modifier = 1f;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);

            listing_Standard.CheckboxLabeled("Bypass terrain cost on friendly settlements", ref settlement_mod, "Settlement is considered friendly if you can visit it. Player-controlled settlements are always friendly.");
            listing_Standard.CheckboxLabeled("Suppress winter penaly on tiles without snow", ref snow_mod, "Winter penalty will be applied only for tiles with negative temperature.");

            listing_Standard.Label("Biome-added movement time factor. Current value: " + ((float)Math.Round(biome_time_modifier, 2)).ToStringPercent() + ".");
            biome_time_modifier = listing_Standard.Slider((float)Math.Round(biome_time_modifier, 2), 0f, 2f);

            listing_Standard.Label("Hillness-added movement time factor. Current value: " + ((float)Math.Round(hillness_time_modifier, 2)).ToStringPercent() + ".");
            hillness_time_modifier = listing_Standard.Slider((float)Math.Round(hillness_time_modifier, 2), 0f, 2f);

            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref settlement_mod, "settlement_mod", true, false);
            Scribe_Values.Look<bool>(ref snow_mod, "snow_mod", true, false);
            Scribe_Values.Look<float>(ref biome_time_modifier, "biome_time_modifier", 1f, false);
            Scribe_Values.Look<float>(ref hillness_time_modifier, "hillness_time_modifier", 1f, false);
        }
    }
}
