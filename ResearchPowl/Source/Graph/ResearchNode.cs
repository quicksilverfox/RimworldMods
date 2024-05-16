// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ResearchPowl.Constants;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
	public enum Painter { Tree = 0, Queue = 1, Drag = 2 }
	public class ResearchNode : Node
	{
		static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache = new Dictionary<ResearchProjectDef, bool>();
		static Dictionary<ResearchProjectDef, string[]> _missingFacilitiesCache = new Dictionary<ResearchProjectDef, string[]>();
		public bool isMatched, _available;
		public static bool availableDirty;
		public ResearchProjectDef Research;
		List<Def> _unlocks;
		Painter _currentPainter;
		HighlightReasonSet _highlightReasons = new HighlightReasonSet();
		public static readonly Dictionary<Def, List<Def>> _unlocksCache = new Dictionary<Def, List<Def>>();

		public ResearchNode(ResearchProjectDef research)
		{
			Research = research;

			// initialize position at vanilla y position, leave x at zero - we'll determine this ourselves
			_pos = new Vector2( 0, research.researchViewY + 1 );
		}
		public bool PainterIs(Painter p)
		{
			return p == _currentPainter;
		}
		List<Def> Unlocks()
		{
			if (_unlocks == null) _unlocks = GetUnlockDefs(Research);
			return _unlocks;

			List<Def> GetUnlockDefs(ResearchProjectDef research)
			{
				if ( _unlocksCache.ContainsKey( research ) )
					return _unlocksCache[research];

				var unlocks = new List<Def>();

				//Was GetThingsUnlocked()
				var thingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
				var length = thingDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = thingDefs[i];
					if (def.researchPrerequisites?.Contains(research) ?? false && def.IconTexture() != null) unlocks.Add(def);
				}

				//Was GetTerrainUnlocked()
				var terrainDefs = DefDatabase<TerrainDef>.AllDefsListForReading;
				length = terrainDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = terrainDefs[i];
					if (def.researchPrerequisites?.Contains(research) ?? false && def.IconTexture() != null) unlocks.Add(def);
				}

				//Was GetRecipesUnlocked()
				var recipeDefs = DefDatabase<RecipeDef>.AllDefsListForReading;
				length = recipeDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = recipeDefs[i];
					if ((def.researchPrerequisite == research || def.researchPrerequisites != null && def.researchPrerequisites.Contains(research)) && 
						def.IconTexture() != null) unlocks.Add(def);
				}

				var plantDefs = DefDatabase<ThingDef>.AllDefsListForReading;
				length = plantDefs.Count;
				for (int i = 0; i < length; i++)
				{
					var def = plantDefs[i];
					if (def.plant?.sowResearchPrerequisites?.Contains(research) ?? false && def.IconTexture() != null) unlocks.Add(def);
				}

				// get unlocks for all descendant research, and remove duplicates.
				_unlocksCache.Add(research, unlocks);
				return unlocks;
			}
		}
		public override bool Highlighted()
		{
			return _highlightReasons.Highlighted();
		}
		public void Highlight(Highlighting.Reason r)
		{
			_highlightReasons.Highlight(r);
		}
		Color HighlightColor()
		{
			return Highlighting.Color(
				_highlightReasons.Current(), Research.techLevel);
		}
		public bool Unhighlight(Highlighting.Reason r)
		{
			return _highlightReasons.Unhighlight(r);
		}
		IEnumerable<Highlighting.Reason> HighlightReasons()
		{
			return _highlightReasons.Reasons();
		}
		public override Color Color
		{
			get
			{
				bool isUnmatchedInSearch = IsUnmatchedInSearch();
				bool highlighted = Highlighted();
				
				//Is it already researched and not being searched for?
				Color color;
				if (Research.IsFinished && (!isUnmatchedInSearch || highlighted)) Assets.ColorCompleted.TryGetValue(Research.techLevel, out color);
				//Is it being highlighted?
				else if (highlighted) color = HighlightColor();
				//Is not what we're searching for?
				else if (isUnmatchedInSearch) Assets.ColorUnmatched.TryGetValue(Research.techLevel, out color);
				//Is it available for research?
				else if (_available) Assets.ColorCompleted.TryGetValue(Research.techLevel, out color);
				//Otherwise assume unavailable
				else Assets.ColorUnavailable.TryGetValue(Research.techLevel, out color);

				return GetFadedColor(color);
			}
		}
		public Color GetFadedColor(Color color, bool text = false)
        {
            if (Tree.filteredOut.Contains(Research.index) && !Highlighted()) color.a = text ? Constants.LessFaded : Constants.Faded;
            return color;
        }
		public bool IsUnmatchedInSearch()
		{
			return MainTabWindow_ResearchTree.Instance._searchActive && !isMatched;
		}
		public bool HighlightInEdge(ResearchNode from)
		{
			foreach (var r1 in HighlightReasons())
			{
				foreach (var r2 in from.HighlightReasons())
				{
					if (Highlighting.Similar(r1, r2)) return true;
				}
			}
			return false;
		}
		public override Color InEdgeColor(ResearchNode from)
		{
			Color color;
			if (HighlightInEdge(from)) color = Assets.NormalHighlightColor;
			else if (MainTabWindow_ResearchTree.Instance._searchActive) Assets.ColorUnmatched.TryGetValue(Research.techLevel, out color);
			else if (Research.IsFinished) Assets.ColorEdgeCompleted.TryGetValue(Research.techLevel, out color);
			else if (_available) Assets.ColorAvailable.TryGetValue(Research.techLevel, out color);
			else Assets.ColorUnavailable.TryGetValue(Research.techLevel, out color);

			return color;
		}
		public List<ResearchNode> Children()
		{
			List<ResearchNode> workingList = new List<ResearchNode>();
            List<Node> list = new List<Node>(OutNodes());
            var length = list.Count;
            for (int i = 0; i < length; i++)
            {
                var node = list[i];
                if (node is DummyNode dNode) workingList.AddRange(dNode.Child());
            }
            return workingList; 
		}
		public override string Label => Research.LabelCap;
		public static void ClearCaches()
		{
			_buildingPresentCache.Clear();
			_missingFacilitiesCache.Clear();
		}
		public int Matches(string query)
		{
			var culture = CultureInfo.CurrentUICulture;
			query = query.ToLower(culture);

			if ((Research.label ?? "").ToLower(culture).Contains(query) ) return 1;

			if (Unlocks().Any(x => (x.label ?? "").ToLower(culture).Contains(query) ) ) return 2;

			if ((Research.modContentPack?.Name.ToLower(culture) ?? "").Contains(query) ) return 3;

			if (Settings.searchByDescription)
			{
				if ((Research.description ?? "").ToLower(culture).Contains(query) ) return 4;
			}
			return 0;
		}
		string[] MissingFacilities(ResearchProjectDef research = null)
		{
			if (research == null) research = Research;
			// try get from cache
			if ( _missingFacilitiesCache.TryGetValue( research, out string[] missing ) ) return missing;

			// get list of all researches required before this
			var thisAndPrerequisites = new List<ResearchProjectDef>() {research};
			var ancestors = research.Ancestors();
			for (int i = ancestors.Count; i-- > 0;)
			{
				var ancestor = ancestors[i];
				if (ancestor.IsFinished) thisAndPrerequisites.Add(ancestor);
			}

			// get list of all available research benches
			List<Building_ResearchBench> availableBenches = new List<Building_ResearchBench>();
			List<ThingDef> otherBenches = new List<ThingDef>();
			List<ThingDef> availableBenchDefs = new List<ThingDef>();
			var maps = Find.Maps;
			for (int j = maps.Count; j-- > 0;)
			{
				var allBuildingsColonist = maps[j].listerBuildings.allBuildingsColonist;
				for (int i = allBuildingsColonist.Count; i-- > 0;)
				{
					var building = allBuildingsColonist[i];
					if (building is Building_ResearchBench building_ResearchBench)
					{
						availableBenches.Add(building_ResearchBench);
						if (!availableBenchDefs.Contains(building.def)) availableBenchDefs.Add(building.def);
					}
					else if (!otherBenches.Contains(building.def)) otherBenches.Add(building.def);
				}
			}
			var workingList = new List<string>();

			// check each for prerequisites
			foreach (var rpd in thisAndPrerequisites)
			{
				//Does this research have any building or facilty requirements
				if (rpd.requiredResearchBuilding == null && rpd.requiredResearchFacilities == null) continue;

				if (rpd.requiredResearchBuilding != null && !availableBenchDefs.Contains(rpd.requiredResearchBuilding)) workingList.Add(rpd.requiredResearchBuilding.LabelCap);

				//Any facilities?
				if (rpd.requiredResearchFacilities.NullOrEmpty())
					continue;

				//Add missing facilities
				foreach (var facility in rpd.requiredResearchFacilities)
				{
					//Is a research bench linked to the facility?
					foreach (var bench in availableBenches) if (HasFacility(bench, facility)) goto facilityFound;
					//Or is it a standalone facility?
					foreach (var bench in otherBenches) if (bench == facility) goto facilityFound;
					//Not found, add
					workingList.Add(facility.LabelCap);
					facilityFound:;
				}
			}

			// add to cache
			var missingFacilities = workingList.Distinct().ToArray();
			_missingFacilitiesCache.Add( research, missingFacilities );
			return missingFacilities;

			bool HasFacility(Building_ResearchBench building, ThingDef facility )
			{
				var comp = building.GetComp<CompAffectedByFacilities>();
				if ( comp == null )
					return false;

				if ( comp.LinkedFacilitiesListForReading.Select( f => f.def ).Contains( facility ) )
					return true;

				return false;
			}
		}
		public override int DefaultPriority()
		{
			return _inEdges.Count + _outEdges.Count;
		}
		void DrawProgressBar()
		{
			// grey out center to create a progress bar effect, completely greying out research not started.
			if ( (MainTabWindow_ResearchTree.Instance._searchActive && isMatched) || !IsUnmatchedInSearch() && _available || Highlighted())
			{
				var progressBarRect = Rect.ContractedBy(3f);

				//was DrawProgressBarImpl(progressBarRect);
				progressBarRect.xMin += Research.ProgressPercent * progressBarRect.width;
				FastGUI.DrawTextureFast(progressBarRect, BaseContent.WhiteTex, this.GetFadedColor(Assets.ColorAvailable.TryGetValue(Research.techLevel)));
			}
		}
		
		bool hasFacilitiesCache = false;
		void HandleTooltips()
		{
			if (PainterIs(Painter.Drag)) return;
			Text.WordWrap = true;

			if (!Settings.disableShortcutManual)
			{
				TooltipHandler.TipRegion(_rect, ShortcutManualTooltip, Research.shortHash + 2);
			}
			// attach description and further info to a tooltip
			if (!Research.TechprintRequirementMet)
			{
				TooltipHandler.TipRegion(_rect, "InsufficientTechprintsApplied".Translate(Research.TechprintsApplied, Research.TechprintCount));
			}
			if (ResearchNode.availableDirty)
			{
				hasFacilitiesCache = Research.requiredResearchBuilding == null || Research.PlayerHasAnyAppropriateResearchBench;
			}
			if (!hasFacilitiesCache || (Research.requiredResearchFacilities != null && Research.requiredResearchBuilding == null))
			{
				var facilityString = MissingFacilities();
				if (!facilityString.NullOrEmpty()) TooltipHandler.TipRegion(_rect, ResourceBank.String.MissingFacilities( string.Join(", ", facilityString)));
			}
			if (!CompatibilityHooks.PassCustomUnlockRequirements(Research))
			{
				foreach (var prompt in CompatibilityHooks.CustomUnlockRequirementPrompts(Research))
				{
					TooltipHandler.TipRegion(_rect, prompt);
				}
			}
			if (Research.techLevel > Current.gameInt.worldInt.factionManager.ofPlayer.def.techLevel)
			{
				TooltipHandler.TipRegion(_rect, TechLevelTooLowTooltip, Research.shortHash + 3);
			}
			if (!Research.PlayerMechanitorRequirementMet)
			{
				TooltipHandler.TipRegion(_rect, "MissingRequiredMechanitor".Translate());
			}

			if (!Research.AnalyzedThingsRequirementsMet)
			{
				var length = Research.requiredAnalyzed.Count;
				var workingList = new string[length];
				for (int i = 0; i < length; i++) workingList[i] = ("NotStudied".Translate(Research.requiredAnalyzed[i].LabelCap));
				
				TooltipHandler.TipRegion(_rect, workingList.ToLineList("", false));
			}

			if (ModCompatibility.UsingVanillaVehiclesExpanded)
			{
				var valueArray = new object[] { Research, null };
				if ((bool)ModCompatibility.IsDisabledMethod.Invoke(null, valueArray))
				{
					var wreck = (ThingDef)valueArray[1];
					if (wreck != null)
					{
						TooltipHandler.TipRegion(_rect, "VVE_WreckNotRestored".Translate(wreck.LabelCap));
					}
				}
			}

			if (ModCompatibility.UsingVanillaExpanded)
			{
				var boolResult = (bool)ModCompatibility.TechLevelAllowedMethod.Invoke(null, new object[] { Research.techLevel });
				if (!boolResult)
				{
					TooltipHandler.TipRegion(_rect, "ResearchPal.StorytellerDoesNotAllow".Translate());
				}
			}

			if (ModCompatibility.UsingRimedieval && !ModCompatibility.AllowedResearchDefs.Contains(Research))
			{
				TooltipHandler.TipRegion(_rect, "ResearchPal.RimedievalDoesNotAllow".Translate());
			}

			TooltipHandler.TipRegion(_rect, GetResearchTooltipString, Research.shortHash);

			if (Settings.progressTooltip && ProgressWorthDisplaying() && !Research.IsFinished)
			{
				TooltipHandler.TipRegion(_rect, string.Format("Progress: {0}", ProgressString()));
			}
		}
		string ShortcutManualTooltip()
		{
			if (Event.current.shift)
			{
				StringBuilder builder = new StringBuilder();
				if (PainterIs(Painter.Queue)) builder.AppendLine(ResourceBank.String.LClickRemoveFromQueue);
				else
				{
					if (_available)
					{
						builder.AppendLine(ResourceBank.String.LClickReplaceQueue);
						builder.AppendLine(ResourceBank.String.SLClickAddToQueue);
						builder.AppendLine(ResourceBank.String.ALClickAddToQueue);
					}
					if (DebugSettings.godMode) builder.AppendLine(ResourceBank.String.CLClickDebugInstant);
				}
				if (_available) builder.AppendLine(ResourceBank.String.Drag);

				builder.AppendLine(ResourceBank.String.RClickHighlight);
				builder.AppendLine(ResourceBank.String.RClickIcon);
				return builder.ToString();
			}
			return ResourceBank.String.ShiftForShortcutManual;
		}
		string TechLevelTooLowTooltip()
		{
			var techlevel = Faction.OfPlayer.def.techLevel;
			return ResourceBank.String.TechLevelTooLow(techlevel, Research.CostFactor(techlevel), (int) Research.baseCost);
		}
		IEnumerable<ResearchProjectDef> OtherLockedPrerequisites(List<ResearchProjectDef> ps)
		{
			if (ps == null) yield break;
			foreach (var item in ps)
			{
				if (!item.IsFinished && item != Research) yield return item;
			}
		}
		string OtherPrereqTooltip(List<ResearchProjectDef> ps)
		{
			if (ps.NullOrEmpty()) return "";
			return ResourceBank.String.OtherPrerequisites(String.Join(", ", ps.Distinct().Select(p => p.LabelCap)));
		}
		string UnlockItemTooltip(Def def)
		{
			string unlockTooltip = "";
			string otherPrereqTooltip = "";
			if (def is TerrainDef terrainDef)
			{
				unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
				otherPrereqTooltip += OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(terrainDef.researchPrerequisites)));
			}
			else if (def is RecipeDef recipeDef)
			{
				unlockTooltip += ResourceBank.String.AllowsCraftingX(def.LabelCap);
				otherPrereqTooltip += OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(recipeDef.researchPrerequisites)));
			}
			else if (def is ThingDef thingDef)
			{
				List<ResearchProjectDef> plantPrerequisites = thingDef.plant?.sowResearchPrerequisites ?? new List<ResearchProjectDef>();
				if (plantPrerequisites.Contains(Research))
				{
					unlockTooltip += ResourceBank.String.AllowsPlantingX(def.LabelCap);
					otherPrereqTooltip += OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(plantPrerequisites)));
				}
				else
				{
					unlockTooltip += ResourceBank.String.AllowsBuildingX(def.LabelCap);
					OtherPrereqTooltip(new List<ResearchProjectDef>(OtherLockedPrerequisites(((BuildableDef)def).researchPrerequisites)));
				}
			}
			else unlockTooltip += ResourceBank.String.AllowGeneralX(def.LabelCap);
			
			return otherPrereqTooltip == ""
				? unlockTooltip
				: unlockTooltip + "\n\n" + otherPrereqTooltip;
		}
		FloatMenu MakeInfoMenuFromDefs(List<Def> defs, int skip = 0)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();

			var length = defs.Count;
			for (int i = skip; i < length; i++)
			{
				var def = defs[i];
			
				Texture2D icon = def.IconTexture();
				Dialog_InfoCard.Hyperlink hyperlink = new Dialog_InfoCard.Hyperlink(def);
			 
				options.Add(new FloatMenuOption(
					def.label, () => hyperlink.ActivateHyperlink(), icon, def.IconColor(),
					MenuOptionPriority.Default,
					rect => TooltipHandler.TipRegion(rect, () => UnlockItemTooltip(def), def.shortHash + Research.shortHash)));
			}
			return new FloatMenu(options);
		}
		void IconActions(bool draw)
		{
			// handle only right click
			if (!draw && !(Event.current.type == EventType.MouseDown && Event.current.button == 1)) return;

			var unlocks = Unlocks();
			var length = unlocks.Count;
			for (var i = 0; i < length; ++i)
			{
				var thisIconRect = IconsRect;
				var iconRect = new Rect(
					thisIconRect.xMax - ( i + 1 ) * ( IconSize.x + 4f ),
					thisIconRect.yMin + ( thisIconRect.height - IconSize.y ) / 2f,
					IconSize.x,
					IconSize.y );

				if (iconRect.xMin - IconSize.x < thisIconRect.xMin && i + 1 < unlocks.Count)
				{
					// stop the loop if we're about to overflow and have 2 or more unlocks yet to print.
					iconRect.x = thisIconRect.x + 4f;

					if (draw)
					{
						FastGUI.DrawTextureFast(iconRect, Assets.MoreIcon, this.GetFadedColor(Assets.colorWhite));

						if (!PainterIs(Painter.Drag))
						{
							var tip = string.Join("\n", unlocks.GetRange(i, unlocks.Count - i).Select(p => p.LabelCap).ToArray());
							TooltipHandler.TipRegion( iconRect, tip );
						}
					}
					else if (!draw && Mouse.IsOver(iconRect) && Find.WindowStack.FloatMenu == null)
					{
						var floatMenu = MakeInfoMenuFromDefs(unlocks, i);
						Find.WindowStack.Add(floatMenu);
						Event.current.Use();
					}
					break;
				}
				var def = unlocks[i];

				if (draw)
				{
					DrawColouredIcon(def, iconRect);
					if (!PainterIs(Painter.Drag))
					{
						TooltipHandler.TipRegion(iconRect, () => UnlockItemTooltip(def), def.shortHash + Research.shortHash);
					}
				}
				else if (Mouse.IsOver(iconRect))
				{
					Dialog_InfoCard.Hyperlink link = new Dialog_InfoCard.Hyperlink(def);
					link.ActivateHyperlink();
					Event.current.Use();
					break;
				}
			}

			void DrawColouredIcon(Def def, Rect canvas)
			{
				FastGUI.DrawTextureFast(canvas, def.IconTexture(), GetFadedColor(Assets.colorWhite));
				GUI.color = Assets.colorWhite;
			}
		}
		void DrawNodeDetailMode(bool mouseOver, Color savedColor)
		{
			Text.anchorInt = TextAnchor.UpperLeft;
			Text.wordWrapInt = true;
			Text.Font = _largeLabel ? GameFont.Tiny : GameFont.Small;
			

			FastGUI.DrawTextureFast(CostIconRect, !Research.IsFinished && !_available ? Assets.Lock : Assets.ResearchIcon, GetFadedColor(Assets.colorWhite));

			Color numberColor;
			float numberToDraw;
			if (Settings.alwaysDisplayProgress && ProgressWorthDisplaying() || SwitchToProgress())
			{
				if (Research.IsFinished)
				{
					numberColor = Assets.colorCyan;
					numberToDraw = 0;
				}
				else
				{
					numberToDraw = Research.CostApparent - Research.ProgressApparent;
					numberColor = Assets.colorGreen;
				}
			}
			else
			{
				numberToDraw = Research.CostApparent;
				numberColor = savedColor;
			}
			if (IsUnmatchedInSearch() && (!Highlighted())) numberColor = Assets.colorGrey;

			numberColor = GetFadedColor(numberColor, true);
			GUI.color = numberColor;
			Widgets.Label(_labelRect, Research.LabelCap);
			Text.anchorInt = TextAnchor.UpperRight;

			Text.Font = NumericalFont(numberToDraw);
			Widgets.Label(CostLabelRect, numberToDraw.ToStringByStyle(ToStringStyle.Integer));

			IconActions(true);
		}
		string ProgressString()
		{
			return string.Format("{0} / {1}",
				Research.ProgressApparent.ToStringByStyle(ToStringStyle.Integer),
				Research.CostApparent.ToStringByStyle(ToStringStyle.Integer));
		}
		bool ProgressWorthDisplaying()
		{
			return Research.ProgressApparent > 0;
		}
		void DrawNodeZoomedOut(bool mouseOver, Color color)
		{
			string textToDraw = (SwitchToProgress() && ! Research.IsFinished) ? ProgressString() : Research.LabelCap;

			Text.Anchor   = TextAnchor.MiddleCenter;
			Text.Font     = GameFont.Medium;
			Text.WordWrap = true;
			GUI.color = color;
			Widgets.Label(Rect, textToDraw);
		}
		bool ShouldGreyOutText()
		{
			return !( (Research.IsFinished || _available) && (Highlighted() || !IsUnmatchedInSearch()));
		}
		void DrawNode(bool detailedMode, bool mouseOver)
		{
			HandleTooltips();

			//was DrawBackground(mouseOver);
			if (mouseOver) FastGUI.DrawTextureFast(Rect, Assets.ButtonActive, Color);
			else FastGUI.DrawTextureFast(Rect, Assets.Button, Color);

			DrawProgressBar();

			// draw the research label
			Color color;
			if (ShouldGreyOutText()) color = Assets.colorGrey;
			else color = Assets.colorWhite;

			if (detailedMode) DrawNodeDetailMode(mouseOver, GetFadedColor(color));
			else DrawNodeZoomedOut(mouseOver, GetFadedColor(color, true));

			Text.WordWrap = true;
		}
		public static GameFont NumericalFont(float number)
		{
			return number >= 1000000 ? GameFont.Tiny : GameFont.Small;
		}
		public bool SwitchToProgress()
		{
			return Tree.DisplayProgressState && ProgressWorthDisplaying();
		}
		public bool MouseOver(Vector2 mousePos)
		{
			return Rect.Contains(mousePos);
		}
		void HandleDragging(bool mouseOver)
		{
			var evt = Event.current;
			if (! mouseOver || Event.current.shift || Event.current.alt) return;
			if (evt.type == EventType.MouseDown && evt.button == 0 && _available)
			{
				MainTabWindow_ResearchTree.Instance.StartDragging(this, _currentPainter);
				if (PainterIs(Painter.Queue)) Queue.NotifyNodeDraggedS();
				Highlight(Highlighting.Reason.Focused);
				Event.current.Use();
			}
			else if (evt.type == EventType.MouseUp && evt.button == 0 && PainterIs(Painter.Tree))
			{
				var tab = MainTabWindow_ResearchTree.Instance;
				if (tab.draggedNode == this && tab.DraggingTime() < Constants.DraggingClickDelay)
				{
					LeftClick();
					tab.StopDragging();
					Event.current.Use();
				}
			}
		}
		bool DetailMode()
		{
			return PainterIs(Painter.Queue) || PainterIs(Painter.Drag) || MainTabWindow_ResearchTree.Instance._zoomLevel < DetailedModeZoomLevelCutoff;
		}
		static ResearchNode mouseWasOver;
		public static bool mouseOverDirty;
		/// <summary>
		///     Draw the node, including interactions.
		/// </summary>
		public override void Draw(Rect visibleRect, Painter painter)
		{
			_currentPainter = painter;
			var mouseOver = Mouse.IsOver(Rect);
			if (mouseOver && mouseWasOver != this)
			{
				mouseOverDirty = true;
				mouseWasOver = this;
			}
			else mouseOverDirty = false;

			if (availableDirty) _available = GetAvailable();
			if (Event.current.type == EventType.Repaint)
			{
				DrawNode(DetailMode(), mouseOver);
			}

			if (PainterIs(Painter.Drag)) return;
			if (DetailMode()) IconActions(false);
			HandleDragging(mouseOver);

			// if clicked and not yet finished, queue up this research and all prereqs.
			if (!MainTabWindow_ResearchTree.Instance.IsDraggingNode())
			{
				MouseoverSounds.DoRegion(_rect, SoundDefOf.Mouseover_Standard);
				if (GUI.Button(_rect, GUIContent.Temp(""), Widgets.EmptyStyle))
				{
					if (Event.current.button == 0) LeftClick();
					else if (Event.current.button == 1) RightClick();
				}
			}
		}
		public bool LeftClick() {
			if (Research.IsFinished || !_available) return false;

			if (DebugSettings.godMode && Event.current.control)
			{
				Queue._instance.Finish(this);
				Messages.Message(ResourceBank.String.FinishedResearch(Research.LabelCap), MessageTypeDefOf.SilentInput, false);
				Queue.Notify_InstantFinished();
			}
			else if (!Queue._instance._queue.Contains(this))
			{
				if (Event.current.shift)
				{
					Queue._instance.Append(this);
					Queue.NewUndoState();
				}
				else if (Event.current.alt)
				{
					Queue._instance.Prepend(this);
            		Queue.NewUndoState();
				}
				else Queue.ReplaceS(this);
			}
			else Queue.RemoveS(this);

			return true;
		}
		bool RightClick()
		{
			SoundDefOf.Click.PlayOneShotOnCamera();
			Tree.HandleFixedHighlight(this);
			if (_currentPainter == Painter.Queue) MainTabWindow_ResearchTree.Instance.CenterOn(this);

			return true;
		}
		// inc means "inclusive"
		public List<ResearchNode> MissingPrerequisitesInc()
		{
			List<ResearchNode> result = new List<ResearchNode>();
			MissingPrerequitesRec(result);
			return result;
		}
		public List<ResearchNode> MissingPrerequisites()
		{
			List<ResearchNode> result = new List<ResearchNode>();
			if (!Research.PrerequisitesCompleted)
			{
				foreach (var n in DirectPrerequisites()) if (!n.Research.IsFinished) n.MissingPrerequitesRec(result);
			}
			return result;
		}
		public IEnumerable<ResearchNode> DirectPrerequisites()
		{
			var length = _inEdges.Count;
			for (int i = 0; i < length; i++) yield return _inEdges[i].InResearch();
		}
		void MissingPrerequitesRec(List<ResearchNode> acc)
		{
			if (acc.Contains(this)) return;
			if (!Research.PrerequisitesCompleted)
			{
				foreach (var n in DirectPrerequisites()) if (!n.Research.IsFinished) n.MissingPrerequitesRec(acc);
			}
			acc.Add(this);
		}
		public bool GetAvailable()
		{
			if (ModCompatibility.UsingVanillaVehiclesExpanded)
			{
				var valueArray = new object[] { Research, null };
				if ((bool)ModCompatibility.IsDisabledMethod.Invoke(null, valueArray))
				{
					var wreck = (ThingDef)valueArray[1];
					if (wreck != null)
						return false; // wreck not studied
				}
			}

			if (ModCompatibility.UsingVanillaExpanded)
			{
				var boolResult = (bool)ModCompatibility.TechLevelAllowedMethod.Invoke(null, new object[] { Research.techLevel });
				if (!boolResult)
					return false; // Storyteller does not allow
			}

			if (ModCompatibility.UsingRimedieval && !ModCompatibility.AllowedResearchDefs.Contains(Research))
			{
				return false; // Rimedieval does not allow
			}

			var prerec = MissingPrerequisites();

			foreach (var n in prerec) if (!n.Research.IsFinished && !n.GetAvailable()) return false;

			return !Research.IsFinished && (DebugSettings.godMode || (
				!Settings.readOnlyMode &&
				(Research.requiredResearchBuilding == null || Research.PlayerHasAnyAppropriateResearchBench) && 
				Research.TechprintRequirementMet && 
				Research.PlayerMechanitorRequirementMet && 
				Research.AnalyzedThingsRequirementsMet && 
				AllowedTechlevel(Research.techLevel) && 
				CompatibilityHooks.PassCustomUnlockRequirements(Research)
			));

			// special rules for tech-level availability
			bool AllowedTechlevel(TechLevel level)
			{
				if ((int)level > Settings.maxAllowedTechLvl) return false;
				//Hard-coded mod hooks. TODO: Get rid of this
				if (Current.gameInt?.storyteller.def.defName == "VFEM_Maynard") return level >= TechLevel.Animal && level <= TechLevel.Medieval;
				return true;
			}
		}
		
		/// <summary>
		///     Creates text version of research description and additional unlocks/prereqs/etc sections.
		/// </summary>
		/// <returns>string description</returns>
		string GetResearchTooltipString()
		{
			// start with the descripton
			var text = new StringBuilder();
			text.AppendLine(Research.description);
			text.AppendLine();
			text.Append(ResourceBank.String.TechLevelOfResearch + Research.techLevel.ToStringHuman().CapitalizeFirst());

			return text.ToString();
		}
		public void DrawAt(Vector2 pos, Rect visibleRect, Painter painter, bool deferRectReset = false)
		{
			SetRects(pos);
			if (_rect.Overlaps(visibleRect)) Draw(visibleRect, painter);
			if (!deferRectReset) SetRects();
		}
	}
}