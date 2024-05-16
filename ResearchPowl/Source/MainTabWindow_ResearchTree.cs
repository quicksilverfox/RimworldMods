// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static ResearchPowl.Constants;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
	public class MainTabWindow_ResearchTree : MainTabWindow
	{
		internal static Vector2 _scrollPosition = Vector2.zero, _mousePosition = -Vector2.zero, absoluteMousePos;
		public Vector2 draggedPosition;
		bool _dragging, _viewRect_InnerDirty = true, _viewRectDirty = true;
		public bool _searchActive;
		float startDragging, lastSearchChangeTime = 0;
		public float _zoomLevel = 1f;
		public static float SearchResponseDelay = 0.3f;
		public static int SearchMaxFound = 100;
		string _prevQuery = "", _curQuery = "";
		Rect _treeRect, _baseViewRect, _baseViewRect_Inner, _viewRect, _viewRect_Inner, searchRect;
		List<ResearchNode> _searchResults;
		IntVec2 currentTreeSize = new IntVec2(0, 0);
		Matrix4x4 originalMatrix;
		public ResearchNode draggedNode = null;
		public Painter draggingSource;

		public Rect VisibleRect => new Rect(_scrollPosition.x, _scrollPosition.y, ViewRect_Inner().width, ViewRect_Inner().height );
		public MainTabWindow_ResearchTree()
		{
			closeOnClickedOutside = false;
			Instance              = this;
			preventCameraMotion = true;
		}
		public static MainTabWindow_ResearchTree Instance { get; private set; }
		public void SetZoomLevel(float value)
		{
			_zoomLevel           = Mathf.Clamp( value, 1f, MaxZoomLevel() );
			_viewRectDirty       = true;
			_viewRect_InnerDirty = true;
		}
		public Rect ViewRect()
		{
			if ( _viewRectDirty )
			{
				_viewRect = new Rect(
					_baseViewRect.xMin   * _zoomLevel,
					_baseViewRect.yMin   * _zoomLevel,
					_baseViewRect.width  * _zoomLevel,
					_baseViewRect.height * _zoomLevel
				);
				_viewRectDirty = false;
			}

			return _viewRect;
		}
		public Rect ViewRect_Inner()
		{
			if ( _viewRect_InnerDirty )
			{
				_viewRect_Inner      = _viewRect.ContractedBy( Margin * _zoomLevel );
				_viewRect_InnerDirty = false;
			}

			return _viewRect_Inner;
		}
		public Rect TreeRect()
		{
			if (currentTreeSize != Tree.Size)
			{
				ResetTreeRect();
				currentTreeSize = Tree.Size;
			}
			return _treeRect;
		}
		void ResetTreeRect()
		{
			var width  = Tree.Size.x * ( NodeSize.x + NodeMargins.x );
			var height = Tree.Size.z * ( NodeSize.y + NodeMargins.y );
			_treeRect = new Rect( 0f, 0f, width, height );
		}
		internal float MaxZoomLevel()
		{
			// get the minimum zoom level at which the entire tree fits onto the screen, or a static maximum zoom level.
			var fitZoomLevel = Mathf.Max( TreeRect().width  / _baseViewRect_Inner.width, TreeRect().height / _baseViewRect_Inner.height );
			return Mathf.Clamp(fitZoomLevel, 1f, AbsoluteMaxZoomLevel);
		}
		public override void PreOpen()
		{
			base.PreOpen();
			Tree.WaitForInitialization();
			
			//Set Rects 
			var ymin = TopBarHeight + StandardMargin + SideMargin;
			// tree view rects, have to deal with UIScale and ZoomLevel manually.
			_baseViewRect = new Rect(
				StandardMargin / Prefs.UIScale, ymin,
				(Screen.width - StandardMargin) / Prefs.UIScale,
				(Screen.height - MainButtonDef.ButtonHeight - StandardMargin - Constants.Margin) /
				Prefs.UIScale - ymin);
			_baseViewRect_Inner = _baseViewRect.ContractedBy( Constants.Margin / Prefs.UIScale );

			//Windowrect, set to topleft (for some reason vanilla alignment overlaps bottom buttons).
			windowRect.x = 0f;
			windowRect.y = 0f;
			windowRect.width = UI.screenWidth;
			windowRect.height = UI.screenHeight - MainButtonDef.ButtonHeight;

			forcePause = Settings.shouldPause;
			if (Settings.shouldReset)
			{
				ResetSearch();
				_scrollPosition = Vector2.zero;
				SetZoomLevel(1f);
			}

			//Clear node availability caches
			ResearchNode.ClearCaches();
			Queue._instance.SanityCheck();

			closeOnClickedOutside = _dragging = false;
		}
		public override void DoWindowContents( Rect canvas )
		{
			GUIClip.Internal_Pop();
			GUIClip.Internal_Pop();

			var cEvent = Event.current;
			var cEventType = cEvent.type;
			absoluteMousePos = cEvent.mousePosition;
			var topRect = new Rect(canvas.xMin + SideMargin, canvas.yMin + StandardMargin, canvas.width - StandardMargin, TopBarHeight );
			DrawTopBar(topRect);

			ApplyZoomLevel();

			// draw background
			FastGUI.DrawTextureFast(ViewRect(), Assets.SlightlyDarkBackground, Assets.colorWhite);

			// draw the actual tree
			var treeRect = TreeRect();
			_scrollPosition = GUI.BeginScrollView( ViewRect(), _scrollPosition, treeRect );
			var scaledMargin = Constants.Margin * _zoomLevel / Prefs.UIScale;
			GUI.BeginGroup(new Rect(scaledMargin, scaledMargin, treeRect.width  - scaledMargin * 2f, treeRect.height - scaledMargin * 2f));

			Tree.Draw( VisibleRect );
			Queue.DrawLabels( VisibleRect );
			
			//Handle LeftoverNode Release
			if (cEventType == EventType.MouseUp && cEvent.button == 0 && IsDraggingNode())
			{
				StopDragging();
				cEvent.Use();
			}
			
			//Handle StopFixed Highlights
			if (cEventType == EventType.MouseDown && (cEvent.button == 0 || cEvent.button == 1 && !cEvent.shift) && Tree.StopFixedHighlights())
			{
				SoundDefOf.Click.PlayOneShotOnCamera();
			}

			//Handle unfocus
			if (cEventType == EventType.MouseDown && !searchRect.Contains(absoluteMousePos))
			{
				UI.UnfocusCurrentControl();
			}
			
			//Handle Zoom, handle zoom only with shift
			if (cEventType == EventType.ScrollWheel && ((Settings.swapZoomMode && cEvent.shift) || (!Settings.swapZoomMode && !cEvent.shift && !cEvent.alt)) && !topRect.Contains(UI.MousePositionOnUIInverted))
			{
				// absolute position of mouse on research tree
				var absPos = Event.current.mousePosition;

				// relative normalized position of mouse on visible tree
				var relPos = ( Event.current.mousePosition - _scrollPosition ) / _zoomLevel;

				// update zoom level
				SetZoomLevel(_zoomLevel + Event.current.delta.y * ZoomStep * _zoomLevel * Settings.zoomingSpeedMultiplier);

				// we want to keep the _normalized_ relative position the same as before zooming
				_scrollPosition = absPos - relPos * _zoomLevel;
				Event.current.Use();
			}

			GUI.EndGroup();
			GUI.EndScrollView(false);

			//ResetZoomLevel
			GUI.matrix = originalMatrix;
			HandleNodeDragging();
			ApplyZoomLevel();

			//Handle dragging, middle mouse or holding down shift for panning
			if (cEvent.button == 2 || cEvent.shift && cEvent.button == 0)
			{
				if (cEventType == EventType.MouseDown)
				{
					_dragging = true;
					_mousePosition = cEvent.mousePosition;
					cEvent.Use();
				}
				if (cEventType == EventType.MouseUp)
				{
					_dragging = false;
					_mousePosition = Vector2.zero;
				}
				if (cEventType == EventType.MouseDrag)
				{
					var _currentMousePosition = cEvent.mousePosition;
					_scrollPosition += _mousePosition - _currentMousePosition;
					_mousePosition = _currentMousePosition;
					cEvent.Use();
				}
			}
			// scroll wheel vertical, switch to horizontal with alt
			if (cEventType == EventType.ScrollWheel && ((Settings.swapZoomMode && !cEvent.shift) || (!Settings.swapZoomMode && (cEvent.shift || cEvent.alt))) && !topRect.Contains(UI.MousePositionOnUIInverted))
			{
				float delta = Event.current.delta.y * 15 * Settings.scrollingSpeedMultiplier;
				if (Event.current.alt) _scrollPosition.x += delta;
				else _scrollPosition.y += delta;
				
				Event.current.Use();
			}
			
			//HandleDolly
			var dollySpeed = 10f;
			if (KeyBindingDefOf.MapDolly_Left.IsDown) _scrollPosition.x -= dollySpeed;
			else if (KeyBindingDefOf.MapDolly_Right.IsDown) _scrollPosition.x += dollySpeed;
			if (KeyBindingDefOf.MapDolly_Up.IsDown) _scrollPosition.y -= dollySpeed;
			else if (KeyBindingDefOf.MapDolly_Down.IsDown) _scrollPosition.y += dollySpeed;

			//Reset zoom level
			UI.ApplyUIScale();
			GUIClip.Internal_Push(windowRect, Vector2.zero, Vector2.zero, false);
			//GUI.BeginClip( windowRect );
			GUIClip.Internal_Push(new Rect( 0f, 0f, UI.screenWidth, UI.screenHeight ), Vector2.zero, Vector2.zero, false);
			//GUI.BeginClip( new Rect( 0f, 0f, UI.screenWidth, UI.screenHeight ) );

			//Cleanup
			GUI.color   = Assets.colorWhite;
			Text.anchorInt = TextAnchor.UpperLeft;
		}
		void ApplyZoomLevel()
		{
			originalMatrix = GUI.matrix;
			GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Prefs.UIScale / _zoomLevel, Prefs.UIScale / _zoomLevel, 1f));
		}
		public bool IsDraggingNode()
		{
			return draggedNode != null;
		}
		public void StartDragging(ResearchNode node, Painter painter)
		{
			Log.Debug("Start dragging node: {0}", node.Research.label);
			draggedNode = node;
			draggingSource = painter;
			draggedPosition = UI.GUIToScreenPoint(node.Rect.position);
			startDragging = Time.time;
		}
		public void StopDragging()
		{
			draggedNode?.Unhighlight(Highlighting.Reason.Focused);
			draggedNode = null;
		}
		public void HandleNodeDragging()
		{
			if (!IsDraggingNode()) return;
			var evt = Event.current;
			if (evt.type == EventType.MouseDrag && evt.button == 0)
			{
				draggedPosition += evt.delta;
				Queue.NotifyNodeDraggedS();
				evt.Use();
			}
			if (draggingSource == Painter.Tree && DraggingTime() > Settings.draggingDisplayDelay)
			{
				var pos = absoluteMousePos;
				pos.x -= NodeSize.x * 0.5f;
				pos.y -= NodeSize.y * 0.5f;
				draggedNode.DrawAt(pos, windowRect, Painter.Drag);
			}
			else draggedNode.DrawAt(draggedPosition, windowRect, Painter.Drag);
		}
		public float DraggingTime()
		{
			return !IsDraggingNode() ? 0 : Time.time - startDragging;
		}
		void DrawTopBar(Rect canvas)
		{
			Rect searchRect2 = new Rect(canvas) { width = 200f, y = canvas.yMin - 20f };
			Rect queueRect  = new Rect(canvas) { xMin = canvas.xMin + 200f, xMax = canvas.xMax - 130f };

			FastGUI.DrawTextureFast(searchRect2, Assets.SlightlyDarkBackground, Assets.colorWhite);

			//Search bar
			var searchCanvas = searchRect2.ContractedBy(Constants.Margin);
			searchRect = new Rect(searchCanvas.xMin, 0f, searchCanvas.width, 30f).CenteredOnYIn(searchCanvas);
			if (Widgets.ButtonImage(new Rect(searchCanvas.xMax - Constants.Margin - 12f, 0f, 12f, 12f ).CenteredOnYIn(searchCanvas), Assets.closeXSmall, false)) ResetSearch();

			DrawModFilter(new Rect(searchRect.x, searchRect.y + 35f, searchRect.width, searchRect.height));
			UpdateTextField();
			OnSearchFieldChanged();

			Queue.Draw(queueRect, !_dragging);
		}
		public static string filteredMod = ResourceBank.String.AllPacks;
		void DrawModFilter(Rect rect)
		{
			var beforeFont = Text.Font;
			Text.Font = GameFont.Tiny;
			if (Widgets.ButtonText(rect, filteredMod))
			{
				try
				{
					List<FloatMenuOption> buttonMenu = new List<FloatMenuOption>(MenuOfPacks());
					if (buttonMenu.Count != 0)
					{
						Find.WindowStack.Add(new FloatMenu(buttonMenu));
					}
				}
				catch (System.Exception ex) { Log.Message("[Research Powl] Error creating content pack drop-down menu.\n" + ex); }
			}
			Text.Font = beforeFont;
		}
		static List<FloatMenuOption> cachedModMenu;
		List<FloatMenuOption> MenuOfPacks()
		{
			if (cachedModMenu != null) return cachedModMenu;
			cachedModMenu = new List<FloatMenuOption>();

			var projects = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
			HashSet<string> modsWithResearch = new HashSet<string>();

			for (int i = projects.Count; i-- > 0;)
			{
				if(projects[i] == null)
                {
					Log.Debug("AllDefsListForReading has a null entry.");
					continue;
				}
				if (projects[i].modContentPack == null)
				{
					Log.Debug("ResearchProjectDef " + projects[i].defName + " has null modContentPack.");
					continue;
				}

				modsWithResearch.Add(projects[i].modContentPack.Name);
			}

			foreach (var mod in LoadedModManager.RunningModsListForReading)
			{
				if (mod == null)
				{
					Log.Debug("RunningModsListForReading has a null entry.");
					continue;
				}

				if (ModContentPack.AnomalyModPackageId.Equals(mod.PackageId)) // Anomaly has custom mechanics so it instead redirects to vanilla research menu
					continue;

				string label = mod.Name;
				if (!modsWithResearch.Contains(label)) continue;
				cachedModMenu.Add(new FloatMenuOption(label, delegate()
					{
						ApplyModFilter(label);
					}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
			}
			cachedModMenu.SortBy(x => x.labelInt);
			cachedModMenu.Insert(0, new FloatMenuOption(ResourceBank.String.AllPacks, delegate()
				{
					ApplyModFilter(ResourceBank.String.AllPacks);
				}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
			
			if (ModLister.AnomalyInstalled)
			{
				cachedModMenu.Insert(1, new FloatMenuOption("Anomaly", delegate ()
				{
					Find.MainTabsRoot.ToggleTab(Assets.MainButtonDefOf.ResearchOriginal);
					((MainTabWindow_Research)Assets.MainButtonDefOf.ResearchOriginal.TabWindow).CurTab =
						ResearchTabDefOf.Anomaly;
				}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
			}
			
			return cachedModMenu;
		}

		void ApplyModFilter(string modName)
		{
			filteredMod = modName;
			Tree.ResetLayout();
		}

		void UpdateTextField()
		{
			var curQuery = Widgets.TextField(searchRect, _curQuery);
			if (curQuery != _curQuery)
			{
				lastSearchChangeTime = Time.realtimeSinceStartup;
				_curQuery = curQuery;
			}
		}
		void OnSearchFieldChanged()
		{
			if ( _curQuery == _prevQuery || Time.realtimeSinceStartup - lastSearchChangeTime < SearchResponseDelay) return;

			_prevQuery = _curQuery;
			ClearPreviousSearch();

			if (_curQuery.Length <= 1) return;

			_searchActive = true;
			// open float menu with search results, if any.
			var options = new List<FloatMenuOption>();

			var list = Tree.ResearchNodes();
			List<(int, ResearchNode)> workingList = new List<(int, ResearchNode)>();
			int length = list.Count;
			int searchCurrent = 0;
			for (int i = 0; i < length; i++)
			{
				var node = list[i];
				var search = node.Matches(_curQuery);
				if (search > 0)
				{
					workingList.Add((search, node));
					if (++searchCurrent > SearchMaxFound) break;
				}
			}
			workingList.SortBy(x => x.Item1);
			List<ResearchNode> _searchResults = new List<ResearchNode>();
			foreach (var item in workingList) _searchResults.Add(item.Item2);

			Log.Debug("Search activate: {0}", _curQuery);
			Log.Debug("Search result: {0}", Queue.DebugQueueSerialize(_searchResults));

			foreach (var result in _searchResults)
			{
				result.isMatched = true;
				options.Add(new FloatMenuOption(result.Label, action: () => ClickAndCenterOn(result), mouseoverGuiAction: rect => CenterOn(result)));
			}

			if (!options.Any()) options.Add(new FloatMenuOption(ResourceBank.String.NoResearchFound, null));

			Find.WindowStack.Add(new FloatMenu_Fixed(options, UI.GUIToScreenPoint(new Vector2(searchRect.xMin, searchRect.yMax))));
		}
		void ResetSearch()
		{
			_curQuery = "";
			_prevQuery = "";
			ClearPreviousSearch();
		}
		void ClearPreviousSearch()
		{
			Find.WindowStack.FloatMenu?.Close(false);
			_searchActive = false;
			if (_searchResults != null)
			{
				foreach (var result in _searchResults) result.isMatched = false;                
				_searchResults = null;
			}
		}
		public void ClickAndCenterOn(ResearchNode node)
		{
			CenterOn(node);
			UI.UnfocusCurrentControl();
		}
		public void CenterOn(ResearchNode node)
		{
			var position = new Vector2((NodeSize.x + NodeMargins.x) * (node.X - .5f), (NodeSize.y + NodeMargins.y) * (node.Y - .5f) );

			position -= new Vector2(UI.screenWidth, UI.screenHeight) / 2f;

			position.x = Mathf.Clamp( position.x, 0f, TreeRect().width  - ViewRect().width );
			position.y = Mathf.Clamp( position.y, 0f, TreeRect().height - ViewRect().height );
			_scrollPosition = position;
		}
	}
}