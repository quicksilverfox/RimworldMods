using System;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
    class Settings : ModSettings
    {
        public static bool mod_enabled = true;
        public static bool draw_overlay = true;
        public static bool zone_mode = false;
        public static float max_gap = 0.08f;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);

            listing_Standard.CheckboxLabeled("SyncGrowthEnabledLabel".Translate(), ref mod_enabled, "SyncGrowthEnabledTooltip".Translate());
            listing_Standard.CheckboxLabeled("SyncGrowthZoneModeLabel".Translate(), ref zone_mode, "SyncGrowthZoneModeTooltip".Translate());
            listing_Standard.CheckboxLabeled("SyncGrowthDrawOverlayLabel".Translate(), ref draw_overlay, "SyncGrowthDrawOverlayTooltip".Translate());

            listing_Standard.Label("SyncGrowthMaxGapLabel".Translate(((int)Math.Round(max_gap, 3) * 100).ToString()), -1, "SyncGrowthMaxGapTooltip".Translate());
            max_gap = listing_Standard.Slider(max_gap, 0.01f, 1f);

            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref mod_enabled, "mod_enabled", true, false);
            Scribe_Values.Look<bool>(ref draw_overlay, "draw_overlay", true, false);
            Scribe_Values.Look<bool>(ref zone_mode, "zone_mode", false, false);
            Scribe_Values.Look<float>(ref max_gap, "max_gap", 0.08f, false);
        }
    }
}
