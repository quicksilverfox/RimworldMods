using Verse;
using RimWorld;

namespace ResearchPowl
{
	public static class ResourceBank
	{
		public static class String
		{
			const string PREFIX = "ResearchPal.";
			static string TL(string s) => (PREFIX + s).Translate();
			static string TL(string s, params NamedArgument[] args) => (PREFIX + s).Translate(args);
			public static string AllowsBuildingX(string x) => TL("AllowsBuildingX", x);
			public static string AllowsCraftingX(string x) => TL("AllowsCraftingX", x);
			public static string AllowsPlantingX(string x) => TL("AllowsPlantingX", x);
			public static string OtherPrerequisites(string x) => TL("OtherPrerequisites", x);
			public static string AllowGeneralX(string x) => TL("AllowGeneralX", x);
			public static string MissingFacilities(string list) => TL("MissingFacilities", list);
			public static string FinishedResearch(string label) => TL("ResearchFinished", label);
			public static string NextInQueue(string label) => TL("NextInQueue", label);
			public static string TechLevelTooLow(TechLevel techlevel, float multiplier, int baseCost)
			{
				return "ResearchPal.TechLevelTooLow".Translate(techlevel.ToStringHuman(), multiplier.ToString(), baseCost.ToString());
			}

			public static readonly string ResetTreeLayout = TL("ResetLayout"),
				LayoutRegenerated = TL("LayoutRegenerated"),
				ShouldSeparateByTechLevels = TL("ShouldSeparateByTechLevels"),
				ShouldSeparateByTechLevelsTip = TL("ShouldSeparateByTechLevelsTip"),
				ShouldPauseOnOpen = TL("ShouldPauseOnOpen"),
				ShouldPauseOnOpenTip = TL("ShouldPauseOnOpenTip"),
				ShouldResetOnOpen = TL("ShouldResetOnOpen"),
				ShouldResetOnOpenTip = TL("ShouldResetOnOpenTip"),
				PlaceModTechSeparately = TL("GroupModTechs"),
				PlaceTabsSeparately = TL("GroupTabs"),
				PlaceNothingSeparately = TL("GroupNothing"),
				PlaceSeparatelyTip = TL("PlaceSeparatelyTip"),
				AlignCloserToAncestors = TL("AlignCloserToAncestors"),
				AlignCloserToAncestorsTip = TL("AlignCloserToAncestorsTip"),
				MinimumSeparateModTech = TL("MinimumSeparateModTech"),
				MinimumSeparateModTechTip = TL("MinimumSeparateModTechTip"),
				SearchByDescription = TL("SearchByDescription"),
				SearchByDescriptionTip = TL("SearchByDescriptionTip"),
				DelayLayoutGeneration = TL("DelayLayoutGeneration"),
				DelayLayoutGenerationTip = TL("DelayLayoutGenerationTip"),
				AsyncLoadingOnStartup = TL("AsyncLoadingOnStartup"),
				AsyncLoadingOnStartupTip = TL("AsyncLoadingOnStartupTip"),
				ProgressTooltip = TL("ProgressTooltip"),
				ProgressTooltipTip = TL("ProgressTooltipTip"),
				AlwaysDisplayProgress = TL("AlwaysDisplayProgress"),
				AlwaysDisplayProgressTip = TL("AlwaysDisplayProgressTip"),
				ShowIndexOnQueue = TL("ShowQueueIndexOnQueue"),
				ShowIndexOnQueueTip = TL("ShowQueueIndexOnQueueTip"),
				DisableShortcutManual = TL("DisableShortcutManual"),
				SwapZoomMode = TL("SwapZoomMode"),
				SwapZoomModeTip = TL("SwapZoomModeTip"),
				DontIgnoreHiddenPrerequisites = TL("DontIgnoreHiddenPrerequisites"),
				GroupBy = TL("GroupBy"),
				DontIgnoreHiddenPrerequisitesTip = TL("DontIgnoreHiddenPrerequisitesTip"),
				useVanillaResearchFinishedMessage = TL("ShowVanillaResearchFinishedMessage"),
				useVanillaResearchFinishedMessageTip = TL("ShowVanillaResearchFinishedMessageTip"),
				LClickReplaceQueue = TL("LClickReplaceQueue"),
				LClickRemoveFromQueue = TL("LClickRemoveFromQueue"),
				SLClickAddToQueue = TL("SLClickAddToQueue"),
				ALClickAddToQueue = TL("ALClickAddToQueue"),
				CLClickDebugInstant = TL("CLClickDebugInstant"),
				RClickHighlight = TL("RightClickNode"),
				RClickIcon = TL("RightClickIcon"),
				Drag = TL("Drag"),
				ShiftForShortcutManual = TL("ShortcutManual"),
				TechLevelOfResearch = TL("TechLevel"),
				NoResearchFound = TL("NoResearchFound"),
				NothingQueued = TL("NothingQueued"),
				AllPacks = TL("AllPacks");
		}
	}
}
