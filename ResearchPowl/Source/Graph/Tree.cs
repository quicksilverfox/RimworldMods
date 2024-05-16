// Tree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static ResearchPowl.Constants;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
	public static class Tree
	{
		public static volatile bool Initialized, Initializing;
		public static IntVec2 Size = IntVec2.Zero;
		public static bool DisplayProgressState, OrderDirty;
		static List<Node> _nodes, _singletons;
		static List<Edge<Node, Node>> _edges;
		static List<TechLevel> _relevantTechLevels;
		static Dictionary<TechLevel, IntRange> _techLevelBounds;
		static bool prerequisitesFixed;
		static List<List<Node>> _layers;
		static List<ResearchNode> _researchNodes;
		public static HashSet<ushort> filteredOut = new HashSet<ushort>();
		static float mainGraphUpperbound = 1;
		static RelatedNodeHighlightSet hoverHighlightSet;
		static List<RelatedNodeHighlightSet> fixedHighlightSets = new List<RelatedNodeHighlightSet>();

		public static List<TechLevel> RelevantTechLevels()
		{
			if (_relevantTechLevels != null) return _relevantTechLevels;

			_relevantTechLevels = new List<TechLevel>();
			var sortedDefs = DefDatabase<ResearchProjectDef>.AllDefsListForReading.OrderBy(x => x.techLevel).ToArray();
			
			TechLevel lastTechlevel = 0;
			for (int i = 0; i < sortedDefs.Length; i++)
			{
				var def = sortedDefs[i];
				if (def.techLevel != lastTechlevel) _relevantTechLevels.Add(def.techLevel);
				lastTechlevel = def.techLevel;
			}

			return _relevantTechLevels;
		}
		static List<Node> Nodes()
		{
			if (_nodes == null) InitializeNodesStructures();
			return _nodes;
		}
		public static List<ResearchNode> ResearchNodes()
		{
			if (_researchNodes == null) InitializeNodesStructures();
			return _researchNodes;
		}
		public static List<ResearchNode> WaitForResearchNodes()
		{
			while (_researchNodes == null) continue;
			return _researchNodes;
		}
		public static void WaitForInitialization()
		{
			if (!Tree.Initialized)
			{
				Tree.InitializeLayout();
			}
		}
		public static bool ResetLayout() {
			if (Initializing) return false;
			Initializing = true;
			Initialized = false;
			InitializeNodesStructures();
			InitializeLayout();
			return true;
		}
		public static void InitializeLayout()
		{
			var timer = new System.Diagnostics.Stopwatch();
  			timer.Start();
			Initializing = true;
			mainGraphUpperbound = 1;

			// actually a lot of the initialization are done by the call of
			// `Nodes()` and `ResearchNodes()`

			LegacyPreprocessing();
			MainAlgorithm(_layers);
			RemoveEmptyRows();
			
			//Determine tree size
			var list = Nodes();
			var length = list.Count;
			float z = 0f, x = 0f;
			for (int i = 0; i < length; i++)
			{
				var n = list[i];
				if (n._pos.y > z) z = n._pos.y;
				if (n._pos.x > x) x = n._pos.x;
			}
			Tree.Size.z = (int)(z + 0.01f) + 1;
			Tree.Size.x = (int)x;

			//Log.Message("Research layout initialized", Tree.Size.x, Tree.Size.z);
			Log.Debug("Layout Size: x = {0}, y = {1}", Tree.Size.x, Tree.Size.z);
			Initialized = true;
			Initializing = false;
			timer.Stop();
			var timeTaken = timer.Elapsed;
			if (Prefs.DevMode) Log.Message("Processed in " + timeTaken.ToString(@"ss\.fffff"));

			//Embedded methods
			void RemoveEmptyRows()
			{
				//was var z = Nodes().Max(n => n.Yf);
				float z = 0;
				var list = Nodes();
				var length = list.Count;
				for (int i = 0; i < length; i++)
				{
					var n = list[i]._pos.y;
					if (n > z) z = n;
				}

				var y = 1;
				while (y < z)
				{
					if (RowIsEmpty(y))
					{
						var edits = 0;
						for (int i = 0; i < length; i++)
						{
							var node = list[i];
							if (node._pos.y > y)
							{
								++edits;
								node.Yf = node._pos.y - 1;
							}
						}
						if (edits == 0) break;
					}
					else ++y;
				}

				bool RowIsEmpty(int Y)
				{
					var list = Nodes();
					var length = list.Count;
					for (int i = 0; i < length; i++)
					{
						if (list[i]._pos.y == Y) return false;
					}
					return true;
				}
			}
			void LegacyPreprocessing()
			{
				var layers = Layering(Nodes());
				var singletons = ProcessSingletons(layers);
				_layers = layers;
				_singletons = singletons;

				List<Node> ProcessSingletons(List<List<Node>> layers)
				{
					if (Settings.shouldSeparateByTechLevels) return new List<Node>();
					List<ResearchNode> singletons = new List<ResearchNode>();

					var length = layers[0].Count;
					List<Node> workingList = new List<Node>();
					for (int i = 0; i < length; i++)
					{
						var node = layers[0][i];

						if (node._outEdges.Count > 0) workingList.Add(node);
						if (node is ResearchNode rNode && rNode._outEdges.Count == 0) singletons.Add(rNode);
					}
					singletons.OrderBy(x => x.Research.techLevel);

					layers[0] = workingList;
					foreach (var g in singletons.GroupBy(n => n.Research.techLevel)) PlaceSingletons(g, layers.Count - 1);

					return new List<Node>(singletons);

					void PlaceSingletons(IEnumerable<Node> singletons, int colNum)
					{
						int x = 0, y = (int) mainGraphUpperbound;
						foreach (var n in singletons)
						{
							n.X = x + 1; n.Y = y;
							y += (x + 1) / colNum;
							x = (x + 1) % colNum;
						}
						mainGraphUpperbound = x == 0 ? y : y + 1;
					}
				}

				List<List<Node>> Layering(List<Node> nodes)
				{
				var layers = new List<List<Node>>();
				foreach (var node in Nodes())
				{
					var nodeX = node.X;
					if (nodeX > layers.Count)
					{
						for (int i = layers.Count; i < nodeX; ++i)
						{
							layers.Add(new List<Node>());
						}
					}
					layers[nodeX - 1].Add(node);
				}
				return layers;
			}
			}
			void MainAlgorithm(List<List<Node>> data)
			{
				NodeLayers layers = new NodeLayers(data);

				var allLayers = layers.SplitMods().OrderBy(l => l.NodeCount()).SelectMany(ls => ls.SplitConnectiveComponents().OrderBy(l => l.NodeCount())).ToArray();

				//was OrganizeLayers()
				for (int i = 0; i < allLayers.Length; i++)
				{
					var layer = allLayers[i];
					layer.NLevelBCMethod(4, 3);
					layer.ApplyGridCoordinates();
					layer.ImproveNodePositionsInLayers();
				}
				
				Log.Debug("PositionAllLayers: starting upper bound {0}", mainGraphUpperbound);
				float[] topBounds = new float[_layers.Count];
				for (int i = 0; i < topBounds.Length; ++i) topBounds[i] = mainGraphUpperbound;
				
				for (int j = 0; j < allLayers.Length; j++)
				{
					var layer = allLayers[j];
					float dy = -99999;
					var length2 = layer._layers.Length;
					for (int i = 0; i < length2; ++i) dy = Math.Max(dy, topBounds[i] - layer.TopPosition(i));
					layer.MoveVertically(dy);

					length2 = layer._layers.Length;
					for (int i = 0; i < length2; ++i)
					{
						//was Math.Max(topBounds[i], layer.BottomPosition(i) + 1);
						var tmp = layer._layers[i];
						float a = 1;
            			if (tmp._nodes.Count == 0) a += -99999f;
            			else a += tmp._nodes[tmp._nodes.Count - 1]._pos.y;
						float b = topBounds[i];

						topBounds[i] = a > b ? a : b;
					}
				}
				mainGraphUpperbound = topBounds.Max();
			}
		}
		static void FilteredTopoSortRec(ResearchNode cur, Func<ResearchNode, bool> p, List<ResearchNode> result, HashSet<ResearchNode> visited)
		{
			if (visited.Contains(cur)) return;
			foreach (var next in cur.InNodes().OfType<ResearchNode>().Where(p))
			{
				FilteredTopoSortRec(next, p, result, visited);
			}
			result.Add(cur);
			visited.Add(cur);
		}
		static void InitializeNodesStructures()
		{
			PopulateNodes(out List<ResearchNode> nodes, out List<Node> allNodes);
			Log.Debug("{0} valid nodes found in def database", nodes.Count);
			CheckPrerequisites(nodes);
			var edges = CreateEdges(nodes);
			Log.Debug("{0} edges created", edges.Count);

			HorizontalPositions(nodes);
			NormalizeEdges(edges, allNodes);
			Log.Debug("{0} nodes after adding dummies", allNodes.Count);

			_nodes = allNodes;
			_researchNodes = nodes;
			_edges = edges;

			//Inlined methods
			void CheckPrerequisites(List<ResearchNode> nodes)
			{
				var nodesQueue = new Queue<ResearchNode>(nodes);
				// remove redundant prerequisites
				while (nodesQueue.Count > 0)
				{
					var node = nodesQueue.Dequeue();
					if (node.Research.prerequisites.NullOrEmpty()) continue;

					var ancestors = node.Research.prerequisites?.SelectMany(r => r.Ancestors()).ToList();
					foreach ( var redundantPrerequisite in ancestors.Intersect( node.Research.prerequisites )) node.Research.prerequisites.Remove(redundantPrerequisite);
				}

				// fix bad techlevels
				nodesQueue = new Queue<ResearchNode>(nodes);
				while ( nodesQueue.Count > 0 )
				{
					var node = nodesQueue.Dequeue();
					if ( !node.Research.prerequisites.NullOrEmpty() )
						// warn and fix badly configured techlevels
						if ( node.Research.prerequisites.Any( r => r.techLevel > node.Research.techLevel ) )
						{
							Log.Debug( "\t{0} has a lower techlevel than (one of) its prerequisites", node.Research.label );
							node.Research.techLevel = node.Research.prerequisites.Max( r => r.techLevel );

							// re-enqeue all descendants
							foreach (var descendant in node.Descendants())
							{
								if (descendant is ResearchNode rNode) nodesQueue.Enqueue(rNode);
							}
						}
				}
			}
			void PopulateNodes(out List<ResearchNode> nodes, out List<Node> allNodes)
			{
				List<ResearchProjectDef> projects = new();
				projects.AddRange(DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def => def.knowledgeCategory == null));

				if (Settings.dontIgnoreHiddenPrerequisites && !prerequisitesFixed)
				{
					for (int i = projects.Count; i-- > 0;) FixPrerequisites(projects[i]);
					prerequisitesFixed = true;
				}

				// Find hidden nodes (nodes that have themselves as a prerequisite)
				HashSet<ResearchProjectDef> hidden = new HashSet<ResearchProjectDef>();
				for (int i = projects.Count; i-- > 0;)
				{
					var project = projects[i];
					if ((project.prerequisites != null && project.prerequisites.Contains(project)) || 
						(Settings.dontShowUnallowedTech && (int)project.techLevel > Settings.maxAllowedTechLvl)) hidden.Add(project);
				}

				// Find locked nodes (nodes that have a hidden node as a prerequisite)
				var locked = projects.Where( p => p.Ancestors().Intersect( hidden ).Any() ).ToHashSet();

				// Populate all nodes
				filteredOut = new HashSet<ushort>();
				HashSet<ResearchProjectDef> workingList = new HashSet<ResearchProjectDef>();
				allNodes = new List<Node>();
				var filteredMod = MainTabWindow_ResearchTree.filteredMod;
				bool usingFilter = filteredMod != ResourceBank.String.AllPacks;

				for (int i = projects.Count; i-- > 0;)
				{
					var project = projects[i];
					if (hidden.Contains(project) || locked.Contains(project)) continue;

					if (usingFilter)
					{
						if (filteredMod != project.modContentPack.Name) continue;
						var tmp = AllAncestors(project).ToHashSet();
						filteredOut.AddRange(tmp.Where(x => x.modContentPack.Name != filteredMod).Select(x => x.index));
						workingList.AddRange(tmp);
					}

					workingList.Add(project);
				}

				nodes = new List<ResearchNode>();
				//Add prereqs
				foreach (var item in workingList)
				{
					var newNode = new ResearchNode(item);
					nodes.Add(newNode);
					allNodes.Add(newNode as Node);
				}

				IEnumerable<ResearchProjectDef> AllAncestors(ResearchProjectDef project)
				{
					foreach (var prerequisite in project.Ancestors())
					{
						foreach (var sub in AllAncestors(prerequisite))
						{
							yield return sub;
						}
						yield return prerequisite;
					}
				}
				
				void FixPrerequisites(ResearchProjectDef d)
				{
					if (d.prerequisites == null) d.prerequisites = d.hiddenPrerequisites;
					else if (d.hiddenPrerequisites != null)
					{
						d.prerequisites = new List<ResearchProjectDef>(d.prerequisites);
						d.prerequisites.AddRange(d.hiddenPrerequisites);
					}
				}
			}
			List<Edge<Node, Node>> CreateEdges(List<ResearchNode> nodes)
			{
				// create links between nodes
				var edges = new List<Edge<Node, Node>>();

				foreach (var node in nodes)
				{
					if (node.Research.prerequisites.NullOrEmpty()) continue;
					foreach ( var prerequisite in node.Research.prerequisites )
					{
						ResearchNode prerequisiteNode = nodes.Find(n => n.Research == prerequisite);
						if ( prerequisiteNode == null ) continue;
						var edge = new Edge<Node, Node>( prerequisiteNode, node );
						edges.Add( edge );
						node._inEdges.Add( edge );
						prerequisiteNode._outEdges.Add(edge);
					}
				}

				return edges;
			}
			void HorizontalPositions(List<ResearchNode> nodes)
			{
				if (Settings.shouldSeparateByTechLevels)
				{
					_techLevelBounds = new Dictionary<TechLevel, IntRange>();
					float leftBound = 1;
					foreach (var group in nodes.GroupBy(n => n.Research.techLevel).OrderBy(g => g.Key))
					{
						float newLeftBound = leftBound;
						foreach (var node in FilteredTopoSort(group, n => n.Research.techLevel == group.Key)) newLeftBound = Math.Max(newLeftBound, node.SetDepth((int)leftBound));

						_techLevelBounds[group.Key] = new IntRange((int)leftBound - 1, (int)newLeftBound);
						leftBound = newLeftBound + 1;
					}
				}
				else foreach (var node in FilteredTopoSort(nodes, n => true)) node.SetDepth(1);

				IEnumerable<ResearchNode> FilteredTopoSort(IEnumerable<ResearchNode> nodes, Func<ResearchNode, bool> p)
				{
					List<ResearchNode> result = new List<ResearchNode>();
					HashSet<ResearchNode> visited = new HashSet<ResearchNode>();
					foreach (var node in nodes)
					{
						var list = node.OutNodes();
						for (int i = 0; i < list.Length; i++)
						{
							if (list[i] is ResearchNode researchNode && p(researchNode)) goto skipNode;
						}
						FilteredTopoSortRec(node, p, result, visited);
						skipNode: continue;
					}
					return result;
				}
			}
			void NormalizeEdges(List<Edge<Node, Node>> edges, List<Node> nodes)
			{
				foreach (var edge in new List<Edge<Node, Node>>(edges.Where(e => e.Span > 1)))
				{
					// remove and decouple long edge
					edges.Remove( edge );
					edge.In._outEdges.Remove( edge );
					edge.Out._inEdges.Remove( edge );
					var cur = edge.In;
					var yOffset = ( edge.Out.Yf - edge.In.Yf ) / edge.Span;

					// create and hook up dummy chain
					var length = edge.Out.X;
					for (var x = edge.In.X + 1; x < length; x++)
					{
						var dummy = new DummyNode();
						dummy.X  = x;
						dummy.Yf = edge.In.Yf + yOffset * ( x - edge.In.X );
						var dummyEdge = new Edge<Node, Node>(cur, dummy);
						cur._outEdges.Add( dummyEdge );
						dummy._inEdges.Add( dummyEdge );
						nodes.Add( dummy );
						edges.Add( dummyEdge );
						cur = dummy;
					}

					// hook up final dummy to out node
					var finalEdge = new Edge<Node, Node>( cur, edge.Out );
					cur._outEdges.Add( finalEdge );
					edge.Out._inEdges.Add( finalEdge );
					edges.Add( finalEdge );
				}
			}
		}
		static public bool StopFixedHighlights()
		{
			bool success = fixedHighlightSets.Any();
			foreach (var n in fixedHighlightSets) n.Stop();
			fixedHighlightSets.Clear();
			return success;
		}
		public static void HandleFixedHighlight(ResearchNode node) {
			var i = fixedHighlightSets.FirstIndexOf(s => s._causer == node);
			Log.Debug("Fixed highlight index: {0}", i);
			if (i >= 0 && i < fixedHighlightSets.Count)
			{
				fixedHighlightSets[i].Stop();
				fixedHighlightSets.RemoveAt(i);
			}
			else
			{
				Log.Debug("Add fixed highlight caused by {0}", node.Research.label);
				var hl = RelatedNodeHighlightSet.FixHighlight(node);
				hl.Start();
				if (!Event.current.shift) StopFixedHighlights();
				fixedHighlightSets.Add(hl);
			}
		}
		static int ticker = 1;
		public static void Draw(Rect visibleRect)
		{
			if (--ticker == 0)
			{
				ResearchNode.availableDirty = true;
				ticker = 60;
			}
			else ResearchNode.availableDirty = false;
			if (Settings.shouldSeparateByTechLevels)
			{
				List<TechLevel> relevantTechLevels = new List<TechLevel>(RelevantTechLevels());
				for (int i = relevantTechLevels.Count; i-- > 0;)
				{
					DrawTechLevel(relevantTechLevels[i], visibleRect);
				}
			}

			foreach (var item in _edges?.OrderBy( e => e.DrawOrder())) item.DrawLines(visibleRect);

			//was TryModifySharedState()
			if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) DisplayProgressState = true;
			else if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) DisplayProgressState = false;

			var evt = new Event(Event.current);
			var mousePos = evt.mousePosition;

			//Compile list of drawn nodes
			List<ResearchNode> list = ResearchNodes();
			bool hoverHighlight = ContinueHoverHighlight(mousePos);
			for (int i = list.Count; i-- > 0;)
			{
				var node = list[i];
				if (node.Rect.Overlaps(visibleRect))
				{
					if (!hoverHighlight && node.MouseOver(mousePos))
					{
						hoverHighlightSet?.Stop();
						hoverHighlightSet = RelatedNodeHighlightSet.HoverOn(node);
						hoverHighlightSet.Start();
					}
					node.Draw(visibleRect, Painter.Tree);
				}
			}

			//Embedded methods
			bool ContinueHoverHighlight(Vector2 mouse)
			{
				if (hoverHighlightSet == null) return false;
				if (hoverHighlightSet.TryStop(mouse))
				{
					hoverHighlightSet = null;
					return false;
				}
				return true;
			}

			void DrawTechLevel(TechLevel techlevel, Rect visibleRect)
			{
				if (Settings.dontShowUnallowedTech && (int)techlevel > Settings.maxAllowedTechLvl) return;

				// determine positions
				if (_techLevelBounds == null || !_techLevelBounds.TryGetValue(techlevel, out IntRange bounds)) return;
				var xMin = ( NodeSize.x + NodeMargins.x ) * bounds.min - NodeMargins.x / 2f;
				var xMax = ( NodeSize.x + NodeMargins.x ) * bounds.max - NodeMargins.x / 2f;

				GUI.color   = Assets.TechLevelColor;
				Text.anchorInt = TextAnchor.MiddleCenter;

				// lower bound
				if ( bounds.min > 0 && xMin > visibleRect.xMin && xMin < visibleRect.xMax )
				{
					// line
					Widgets.DrawLine( new Vector2( xMin, visibleRect.yMin ), new Vector2( xMin, visibleRect.yMax ), Assets.TechLevelColor, 1f );

					// label
					var labelRect = new Rect(
						xMin + TechLevelLabelSize.y / 2f - TechLevelLabelSize.x / 2f,
						visibleRect.center.y             - TechLevelLabelSize.y / 2f,
						TechLevelLabelSize.x,
						TechLevelLabelSize.y );

					VerticalLabel( labelRect, techlevel.ToStringHuman() );
				}

				// upper bound
				if ( bounds.max < Size.x && xMax > visibleRect.xMin && xMax < visibleRect.xMax )
				{
					// label
					var labelRect = new Rect(
						xMax - TechLevelLabelSize.y / 2f - TechLevelLabelSize.x / 2f,
						visibleRect.center.y             - TechLevelLabelSize.y / 2f,
						TechLevelLabelSize.x,
						TechLevelLabelSize.y );

					VerticalLabel( labelRect, techlevel.ToStringHuman() );
				}

				GUI.color = Assets.colorWhite;
				Text.anchorInt = TextAnchor.UpperLeft;

				void VerticalLabel(Rect rect, string text)
				{
					// store the scaling matrix
					var matrix = GUI.matrix;

					// rotate and then apply the scaling
					GUI.matrix = Matrix4x4.identity;
					GUIUtility.RotateAroundPivot( -90f, rect.center );
					GUI.matrix = matrix * GUI.matrix;

					Widgets.Label(rect, text);

					// restore the original scaling matrix
					GUI.matrix = matrix;
				}
			}
		}
	}
}