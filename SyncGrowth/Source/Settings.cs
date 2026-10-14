using System;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	class Settings : ModSettings
	{
		public const float DefaultMaxGap = 0.20f;

		public static bool mod_enabled = true;
		public static bool draw_overlay = true;
		public static bool zone_mode = false;
		public static float max_gap = DefaultMaxGap;

		public static void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(inRect);

			listing.CheckboxLabeled("SyncGrowthEnabledLabel".Translate(), ref mod_enabled, "SyncGrowthEnabledTooltip".Translate());
			listing.CheckboxLabeled("SyncGrowthZoneModeLabel".Translate(), ref zone_mode, "SyncGrowthZoneModeTooltip".Translate());
			listing.CheckboxLabeled("SyncGrowthDrawOverlayLabel".Translate(), ref draw_overlay, "SyncGrowthDrawOverlayTooltip".Translate());

			listing.Label(
				"SyncGrowthMaxGapLabel".Translate(((float)Math.Round(max_gap, 3)).ToStringPercent(), DefaultMaxGap.ToStringPercent()),
				-1,
				"SyncGrowthMaxGapTooltip".Translate());
			max_gap = listing.Slider(max_gap, 0.01f, 1f);

			listing.End();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref mod_enabled, "mod_enabled", true);
			Scribe_Values.Look(ref draw_overlay, "draw_overlay", true);
			Scribe_Values.Look(ref zone_mode, "zone_mode", false);
			Scribe_Values.Look(ref max_gap, "max_gap", DefaultMaxGap);
		}
	}
}
