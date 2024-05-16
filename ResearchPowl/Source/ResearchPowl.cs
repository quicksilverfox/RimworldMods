// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;
using static ResearchPowl.ResourceBank.String;
using static ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
	[StaticConstructorOnStartup]
	static class Setup
	{
		static Setup()
		{
			if (readOnlyModeSkip) return;
			foreach (var packageID in ModsConfig.activeModsHashSet)
			{
				if (packageID.Contains("randomresearch"))
				{
					readOnlyMode = true;
					return;
				}
			}
		}
	}

	public class ResearchPowl : Mod
	{
		public ResearchPowl( ModContentPack content ) : base( content )
		{
			new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
			base.GetSettings<ModSettings_ResearchPowl>();
		}
		public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
		{
			Rect rectLeftColumn = inRect.LeftPart(0.46f).Rounded();
			Rect rectRightColumn = inRect.RightPart(0.46f).Rounded();

			Listing_Standard listLeft = new Listing_Standard(GameFont.Small);
			listLeft.ColumnWidth = rectLeftColumn.width;
			listLeft.Begin(rectLeftColumn);

			if (Prefs.DevMode && listLeft.ButtonText(ResetTreeLayout))
			{
				SoundDefOf.Click.PlayOneShotOnCamera();
				if (Tree.ResetLayout()) Messages.Message(LayoutRegenerated, MessageTypeDefOf.CautionInput, false);
			}

			if (Prefs.DevMode) listLeft.CheckboxLabeled(DontIgnoreHiddenPrerequisites, ref dontIgnoreHiddenPrerequisites, DontIgnoreHiddenPrerequisitesTip);
			listLeft.CheckboxLabeled(ShouldSeparateByTechLevels, ref shouldSeparateByTechLevels, ShouldSeparateByTechLevelsTip);
			listLeft.CheckboxLabeled(AlignCloserToAncestors, ref alignToAncestors, AlignCloserToAncestorsTip);

			listLeft.Label(GroupBy, -1f, null);
			if (listLeft.RadioButton(PlaceNothingSeparately, !placeTabsSeparately && !placeModTechSeparately, 0f, PlaceSeparatelyTip, null))
			{
				placeModTechSeparately = placeTabsSeparately = false;
			}
			if (listLeft.RadioButton(PlaceTabsSeparately, placeTabsSeparately, 0f, null, null))
			{
				placeTabsSeparately = true;
				placeModTechSeparately = false;
			}
			if (listLeft.RadioButton(PlaceModTechSeparately, placeModTechSeparately, 0f, null, null))
			{
				placeModTechSeparately = true;
				placeTabsSeparately = false;
			}

			if (placeModTechSeparately)
			{
				listLeft.Label(MinimumSeparateModTech, -1, MinimumSeparateModTechTip);
				string buffer = largeModTechCount.ToString();
				listLeft.IntEntry(ref largeModTechCount, ref buffer);
			}
			listLeft.CheckboxLabeled(SearchByDescription, ref searchByDescription, SearchByDescriptionTip);
			listLeft.Gap();

			listLeft.CheckboxLabeled(ShouldPauseOnOpen, ref shouldPause, ShouldPauseOnOpenTip);
			listLeft.CheckboxLabeled(ShouldResetOnOpen, ref shouldReset, ShouldResetOnOpenTip);
			listLeft.Gap();

			listLeft.CheckboxLabeled(ProgressTooltip, ref progressTooltip, ProgressTooltipTip);
			listLeft.CheckboxLabeled(AlwaysDisplayProgress, ref alwaysDisplayProgress, AlwaysDisplayProgressTip);
			listLeft.CheckboxLabeled(ShowIndexOnQueue, ref showIndexOnQueue, ShowIndexOnQueueTip);
			listLeft.CheckboxLabeled(DisableShortcutManual, ref disableShortcutManual);
			listLeft.CheckboxLabeled(SwapZoomMode, ref swapZoomMode, SwapZoomModeTip);

			listLeft.Gap();

			if (Prefs.DevMode) listLeft.CheckboxLabeled("ResearchPal.VerboseLogging".Translate(), ref verboseDebug, "ResearchPal.VerboseLoggingTip".Translate());

			listLeft.Gap();

			listLeft.CheckboxLabeled( ResourceBank.String.useVanillaResearchFinishedMessage, ref ModSettings_ResearchPowl.useVanillaResearchFinishedMessage, useVanillaResearchFinishedMessageTip);

			listLeft.End();


			Listing_Standard listRight = new Listing_Standard(GameFont.Small);
			listRight.ColumnWidth = rectRightColumn.width;
			listRight.Begin(rectRightColumn);

			listRight.Label("ResearchPal.ScrollSpeedMultiplier".Translate() + string.Format(" {0:0.00}", scrollingSpeedMultiplier), -1, "ResearchPal.ScrollSpeedMultiplierTip".Translate());
			scrollingSpeedMultiplier = listRight.Slider(scrollingSpeedMultiplier, 0.1f, 5);
			listRight.Label( "ResearchPal.ZoomingSpeedMultiplier".Translate() + string.Format(" {0:0.00}", zoomingSpeedMultiplier), -1, "ResearchPal.ZoomingSpeedMultiplierTip".Translate());
			zoomingSpeedMultiplier = listRight.Slider(zoomingSpeedMultiplier, 0.1f, 5);
			listRight.Label( "ResearchPal.DraggingDisplayDelay".Translate() + string.Format(": {0:0.00}s", draggingDisplayDelay), -1, "ResearchPal.DraggingDisplayDelayTip".Translate());
			draggingDisplayDelay = listRight.Slider(draggingDisplayDelay, 0, 1);
			TechLevel tmp = (TechLevel)maxAllowedTechLvl;
			listRight.Label("ResearchPal.MaxAllowedTechLvl".Translate(tmp.ToStringHuman().CapitalizeFirst()), -1f, "ResearchPal.MaxAllowedTechLvlTip".Translate());
			maxAllowedTechLvl = (int)listRight.Slider(maxAllowedTechLvl, 1, 7);
			if (maxAllowedTechLvl < 7) listRight.CheckboxLabeled("ResearchPal.DontShowUnallowedTech".Translate(), ref dontShowUnallowedTech);
			else dontShowUnallowedTech = false;
			listRight.CheckboxLabeled("ResearchPal.ReadOnlyMode".Translate(), ref readOnlyMode, "ResearchPal.ReadOnlyModeTip".Translate());
			listRight.End();
		}
		public override string SettingsCategory()
		{
			return "ResearchPal".Translate();
		}
		public override void WriteSettings()
		{
			if (!readOnlyMode && !readOnlyModeSkip)
			{
				foreach (var packageID in ModsConfig.activeModsHashSet)
				{
					if (packageID.Contains("randomresearch"))
					{
						readOnlyModeSkip = true;
						break;
					}
				}
			}
			
			base.WriteSettings();
			Tree.ResetLayout();
		}
	}

	public class ModSettings_ResearchPowl : ModSettings
	{
		public override void ExposeData()
		{
			Scribe_Values.Look(ref shouldSeparateByTechLevels, "ShouldSeparateByTechLevels", true);
			Scribe_Values.Look(ref shouldPause, "ShouldPauseOnOpen", true);
			Scribe_Values.Look(ref shouldReset, "ShouldResetOnOpen");
			Scribe_Values.Look(ref alignToAncestors, "AlignCloserToAncestors");
			Scribe_Values.Look(ref placeModTechSeparately, "placeModTechsSeparately");
			Scribe_Values.Look(ref placeTabsSeparately, "placeTabsSeparately", true);
			Scribe_Values.Look(ref largeModTechCount, "MinimumSeparateModTech", 5);
			Scribe_Values.Look(ref maxAllowedTechLvl, "maxAllowedTechLvl", 7);
			Scribe_Values.Look(ref searchByDescription, "SearchByDescription");
			Scribe_Values.Look(ref progressTooltip, "ProgressTooltip");
			Scribe_Values.Look(ref alwaysDisplayProgress, "AlwaysDisplayProgress");
			Scribe_Values.Look(ref showIndexOnQueue, "ShowQueuePositionOnQueue");
			Scribe_Values.Look(ref disableShortcutManual, "DisableShortcutManual");
			Scribe_Values.Look(ref dontIgnoreHiddenPrerequisites, "dontIgnoreHiddenPrerequisites", true);
			Scribe_Values.Look(ref scrollingSpeedMultiplier, "ScrollingSpeedMultiplier", 1);
			Scribe_Values.Look(ref zoomingSpeedMultiplier, "zoomingSpeedMultiplier", 1);
			Scribe_Values.Look(ref draggingDisplayDelay, "draggingDisplayDelay", 0.25f);
			Scribe_Values.Look(ref verboseDebug, "verboseLogging");
			Scribe_Values.Look(ref swapZoomMode, "swapZoomMode");
			Scribe_Values.Look(ref dontShowUnallowedTech, "dontShowUnallowedTech");
			Scribe_Values.Look(ref useVanillaResearchFinishedMessage, "useVanillaResearchFinishedMessage", true);
			Scribe_Values.Look(ref readOnlyMode, "readOnlyMode");
			Scribe_Values.Look(ref readOnlyModeSkip, "readOnlyModeSkip");
		}

		public static bool shouldPause,
			shouldReset,
			alignToAncestors,
			searchByDescription,
			showIndexOnQueue,
			disableShortcutManual,
			verboseDebug,
			swapZoomMode,
			dontShowUnallowedTech,
			useVanillaResearchFinishedMessage = true, 
			dontIgnoreHiddenPrerequisites = true,
			alwaysDisplayProgress = true, 
			progressTooltip = true,
			shouldSeparateByTechLevels = true, 
			placeModTechSeparately,
			placeTabsSeparately = true,
			readOnlyMode,
			readOnlyModeSkip = false;
		public static float scrollingSpeedMultiplier = 1f,
			zoomingSpeedMultiplier = 1f,
			draggingDisplayDelay = 0.25f;
		public static int largeModTechCount = 5,
			maxAllowedTechLvl = 7;
	}
}