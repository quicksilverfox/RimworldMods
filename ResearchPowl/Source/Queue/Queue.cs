// Queue.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using System;
using static ResearchPowl.Assets;
using static ResearchPowl.Constants;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
    public class Queue : WorldComponent
    {
        public static Queue _instance;
        public readonly List<ResearchNode> _queue = new List<ResearchNode>();
        List<ResearchProjectDef> _saveableQueue;
        // State of the "random research" wildcard (see WildcardNode): the project it currently stands for, and its
        // queue position for saving (the wildcard has no def, so it cannot go into _saveableQueue).
        ResearchProjectDef _wildcardTarget;
        int _wildcardIndex = -1;
        // Content pack the wildcard is restricted to (captured from the pack filter when it was queued), or null for all.
        string _wildcardPack;
        public ResearchProjectDef WildcardTarget => _wildcardTarget;
        public string WildcardPack => _wildcardPack;
        void Notify_WildcardQueued()
        {
            _wildcardPack = MainTabWindow_ResearchTree.FilterPack;
            _wildcardTarget = null;
        }
        static UndoStateHandler<ResearchNode[]> undoState = new UndoStateHandler<ResearchNode[]>();

        public Queue(World world) : base(world) {
            _instance = this;
        }

        // Called whenever the tree recreates its nodes (layout reset, settings change, content pack filter).
        // Re-point queued entries at the new node objects; research that is no longer part of the tree stays
        // queued but is flagged detached so no labels get drawn at stale positions.
        public static void Notify_TreeRebuilt(List<ResearchNode> nodes)
        {
            if (_instance == null) return;
            var byDef = new Dictionary<ResearchProjectDef, ResearchNode>(nodes.Count);
            foreach (var n in nodes) if (n.Research != null) byDef[n.Research] = n;
            var wildcard = nodes.OfType<WildcardNode>().FirstOrDefault();
            ResearchNode Remap(ResearchNode old)
            {
                if (old == null) return null;
                if (old is WildcardNode) return wildcard ?? old;
                if (byDef.TryGetValue(old.Research, out var fresh)) return fresh;
                old.detached = true;
                return old;
            }
            var queue = _instance._queue;
            for (int i = 0; i < queue.Count; i++) queue[i] = Remap(queue[i]);
            undoState.Remap(state => { for (int i = 0; i < state.Length; i++) state[i] = Remap(state[i]); return state; });
        }
        public static void DrawLabels( Rect visibleRect )
        {
            var list = _instance._queue;
            for (int j = list.Count, i = 1; j-- > 0; i++)
            {
                var node = list[j];
                if (!node.detached && node.IsVisible(visibleRect))
                {
                    ColorCompleted.TryGetValue(node.TechLevel, out Color main);
                    var background = i > 1 ? ColorUnavailable.TryGetValue(node.TechLevel) : main;
                    DrawLabel(node.QueueRect, main, background, i);
                }
            }
        }
        public static void DrawLabel( Rect canvas, Color main, Color background, int label )
        {
            // draw coloured tag
            FastGUI.DrawTextureFast(canvas, CircleFill, main);

            // if this is not first in line, grey out centre of tag
            if (background != main) FastGUI.DrawTextureFast(canvas.ContractedBy(2f), CircleFill, background);

            // draw queue number
            Text.anchorInt = TextAnchor.MiddleCenter;
            Widgets.Label(canvas, label.ToString());
            Text.anchorInt = TextAnchor.UpperLeft;
        }
        // Require the input to be topologically ordered
        void UnsafeConcat(IEnumerable<ResearchNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (_queue.Contains(n)) continue;
                _queue.Add(n);
                if (n is WildcardNode) Notify_WildcardQueued();
            }
        }
        // Require the input to be topologically ordered
        void UnsafeConcatFront(IEnumerable<ResearchNode> nodes)
        {
            UnsafeInsert(nodes, 0);
        }
        void UnsafeInsert(IEnumerable<ResearchNode> nodes, int pos)
        {
            int i = pos;
            foreach (var n in nodes)
            {
                if (_queue.IndexOf(n, 0, pos) != -1) continue;
                bool wasQueued = _queue.Remove(n);
                _queue.Insert(i++, n);
                if (!wasQueued && n is WildcardNode) Notify_WildcardQueued();
            }
        }
        static public bool CantResearch(ResearchNode node)
        {
            return !node.GetAvailable();
        }
        void UnsafeAppend(ResearchNode node)
        {
            UnsafeConcat(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
        }
        public bool Append(ResearchNode node)
        {
            if (_queue.Contains(node) || CantResearch(node)) return false;
            UnsafeAppend(node);
            return true;
        }
        public bool Prepend(ResearchNode node)
        {
            if (CantResearch(node)) return false;
            UnsafeConcatFront(node.MissingPrerequisitesInc());
            UpdateCurrentResearch();
            return true;
        }            
        void MarkShouldRemove(int index, List<ResearchNode> shouldRemove)
        {
            var node = _queue[index];
            shouldRemove.Add(node);
            var length = _queue.Count;
            for (int i = index + 1; i < length; ++i) {
                if (shouldRemove.Contains(_queue[i])) continue;

                if (_queue[i].MissingPrerequisites().Contains(node)) MarkShouldRemove(i, shouldRemove);
            }
        }
        ResearchProjectDef CurrentResearch()
        {
            // Target resolves the wildcard to a concrete project (picking one if needed).
            return _queue.FirstOrDefault()?.Target;
        }
        // Project the wildcard currently stands for; picks a new one when there is none or it got finished.
        public ResearchProjectDef ResolveWildcardTarget()
        {
            if (_wildcardTarget != null && !_wildcardTarget.IsFinished) return _wildcardTarget;
            _wildcardTarget = PickRandomResearch();
            Log.Debug("Wildcard picked: {0}", _wildcardTarget?.label ?? "NONE");
            return _wildcardTarget;
        }
        // Candidates: projects the game would let you start right now (prerequisites, bench, techprints...), that the
        // mod's settings and compatibility hooks allow, and that belong to the wildcard's content pack if it has one.
        // Works on defs rather than tree nodes so the current pack filter of the view does not matter.
        // Preference order: already started > player's tech level (highest without cost penalty) > prerequisite of an
        // unfinished project at that level > lower tech level > higher (penalized) tech level. Random within the best group.
        ResearchProjectDef PickRandomResearch()
        {
            var available = new List<ResearchProjectDef>();
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.knowledgeCategory != null || !def.CanStartNow || !ResearchNode.DefAllowed(def)) continue;
                if (_wildcardPack != null && def.modContentPack?.Name != _wildcardPack) continue;
                available.Add(def);
            }
            if (available.Count == 0) return null;

            // Prefer projects the player has not planned already; fall back to queued ones if that is all there is.
            var queued = new HashSet<ResearchProjectDef>();
            foreach (var n in _queue) if (n.Research != null) queued.Add(n.Research);
            var pool = available.Where(r => !queued.Contains(r)).ToList();
            if (pool.Count == 0) pool = available;

            var optimal = Faction.OfPlayer.def.techLevel;
            var unlockers = new HashSet<ResearchProjectDef>();
            foreach (var def in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                if (def.techLevel == optimal && def.knowledgeCategory == null && !def.IsFinished) unlockers.AddRange(def.Ancestors());
            }
            int Rank(ResearchProjectDef r)
            {
                int tier = r.techLevel == optimal ? 0 : unlockers.Contains(r) ? 1 : r.techLevel < optimal ? 2 : 3;
                return (r.ProgressReal > 0 ? 0 : 10) + tier;
            }
            int best = pool.Min(Rank);
            return pool.Where(r => Rank(r) == best).RandomElement();
}
        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            // A wildcard at the head may have found nothing to research earlier (e.g. no bench yet); retry about hourly.
            if (Find.TickManager.TicksGame % 2500 != 0 || _queue.Count == 0 || !(_queue[0] is WildcardNode)) return;
            if (_wildcardTarget == null || _wildcardTarget.IsFinished) UpdateCurrentResearch();
        }
        void UpdateCurrentResearch()
        {
            Find.ResearchManager.currentProj = CurrentResearch();
        }
        public void SanityCheck()
        {
            List<ResearchNode> finished = new List<ResearchNode>();
            List<ResearchNode> unavailable = new List<ResearchNode>();

            foreach (var n in _queue)
            {
                if (n.IsFinished) finished.Add(n);
                // Only drop research that can never be done in this game, not research merely blocked right now
                // (unpowered bench, missing techprint...): the player queued it on purpose.
                else if (!n.GetAvailable(ignoreColonyState: true)) unavailable.Add(n);
            }
            foreach (var n in finished) _queue.Remove(n);
            foreach (var n in unavailable) Remove(n);
            
            var cur = Find.ResearchManager.currentProj;
            if (cur != null && cur != CurrentResearch())
            {
                // The current project may not be part of the (filtered) tree; never queue a null node.
                var curNode = cur.ResearchNode();
                if (curNode != null) Replace(curNode);
            }
            UpdateCurrentResearch();
        }
        bool Remove(ResearchNode node)
        {
            if (node is WildcardNode) _wildcardTarget = null;
            if (node.IsFinished) return _queue.Remove(node);

            List<ResearchNode> shouldRemove = new List<ResearchNode>();
            var idx = _queue.IndexOf(node);
            if (idx == -1) return false;

            MarkShouldRemove(idx, shouldRemove);
            foreach (var n in shouldRemove) _queue.Remove(n);

            if (idx == 0) UpdateCurrentResearch();
            return true;
        }
        void Replace(ResearchNode node)
        {
            _queue.Clear();
            Append(node);
        }
        void ReplaceMore(IEnumerable<ResearchNode> nodes)
        {
            _queue.Clear();
            foreach (var node in nodes) Append(node);
        }
        static public void ReplaceS(ResearchNode node) {
            _instance.Replace(node);
            NewUndoState();
        }
        static public bool RemoveS(ResearchNode node)
        {
            var b = _instance.Remove(node);
            NewUndoState();
            return b;
        }
        public void Finish(ResearchNode node)
        {
            foreach (var n in node.MissingPrerequisitesInc())
            {
                _queue.Remove(n);
                Find.ResearchManager.FinishProject(n.Research);
            }
        }
        public static String DebugQueueSerialize(IEnumerable<ResearchNode> nodes)
        {
            if (Settings.verboseDebug) return string.Join(", ", nodes.Select(n => n.Label));
            return "";
        }
        static public void NewUndoState()
        {
            var q = Queue._instance;
            var s = q._queue.ToArray();
            Log.Debug("Undo state recorded: {0}", DebugQueueSerialize(s));
            undoState.NewState(s);
        }
        ResearchNode Current()
        {
            return _queue.FirstOrDefault();
        }
        public static ResearchNode CurrentS() {
            return _instance.Current();
        }
        public static void TryStartNext( ResearchProjectDef finished )
        {
            var q = _instance;
            var queue = q._queue;
            var current = q.Current();
            bool wildcardHit = q._wildcardTarget == finished;

            var finishedNode = queue.Find(n => n.Research == finished);
            if (finishedNode == null && !wildcardHit) return;
            bool wasCurrent = (finishedNode != null && finishedNode == current) || (wildcardHit && current is WildcardNode);
            if (finishedNode != null) RemoveS(finishedNode);
            if (wildcardHit)
            {
                // The wildcard's pick is done: let anything queued behind it have its turn, then it picks again.
                q._wildcardTarget = null;
                var wildcard = queue.OfType<WildcardNode>().FirstOrDefault();
                if (wildcard != null && queue.Count > 1 && queue[0] == wildcard)
                {
                    queue.RemoveAt(0);
                    queue.Add(wildcard);
                }
            }
            if (!wasCurrent) return;
            var next = q.CurrentResearch();
            Find.ResearchManager.currentProj = next;
            if (! Settings.useVanillaResearchFinishedMessage)
            {
                Log.Debug("Send completion letter for {0}, next is {1}", finished.label, next?.label ?? "NONE");
                //was DoCompletionLetter()
                string label = "ResearchFinished".Translate( finished.LabelCap );
                string text  = finished.LabelCap + "\n\n" + finished.description;

                if ( next != null )
                {
                    text += "\n\n" + ResourceBank.String.NextInQueue(next.LabelCap);
                    Find.LetterStack.ReceiveLetter( label, text, LetterDefOf.PositiveEvent );
                }
                else
                {
                    text += "\n\n" + ResourceBank.String.NextInQueue("none");
                    Find.LetterStack.ReceiveLetter( label, text, LetterDefOf.NeutralEvent );
                }
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();

            // store research defs as these are the defining elements
            if ( Scribe.mode == LoadSaveMode.Saving )
            {
                _saveableQueue = new List<ResearchProjectDef>(_queue.Where(node => node.Research != null).Select(node => node.Research));
                _wildcardIndex = _queue.FindIndex(node => node is WildcardNode);
            }

            Scribe_Collections.Look( ref _saveableQueue, "Queue", LookMode.Def );
            Scribe_Values.Look( ref _wildcardIndex, "wildcardIndex", -1 );
            Scribe_Defs.Look( ref _wildcardTarget, "wildcardTarget" );
            Scribe_Values.Look( ref _wildcardPack, "wildcardPack" );

            if ( Scribe.mode == LoadSaveMode.PostLoadInit )
            {
                foreach (var research in _saveableQueue ?? new List<ResearchProjectDef>())
                {
                    // find a node that matches the research - or null if none found
                    var node = research?.ResearchNode();
                    if (node != null) UnsafeAppend(node);
                }
                if (_wildcardIndex >= 0 && Tree.Wildcard != null && !_queue.Contains(Tree.Wildcard))
                {
                    _queue.Insert(Math.Min(_wildcardIndex, _queue.Count), Tree.Wildcard);
                }
                undoState.Clear();
                NewUndoState();
                UpdateCurrentResearch();
            }
        }
        void DoMove(ResearchNode node, int from, int to)
        {
            List<ResearchNode> movingNodes = new List<ResearchNode>();
            to = Math.Max(0, Math.Min(_queue.Count, to));
            if (to > from)
            {
                movingNodes.Add(node);
                int dest = --to;
                for (int i = from + 1; i <= to; ++i)
                {
                    if (_queue[i].MissingPrerequisites().Contains(node))
                    {
                        movingNodes.Add(_queue[i]);
                        --dest;
                    }
                }
                foreach (var n in movingNodes) _queue.Remove(n);
                _queue.InsertRange(dest, movingNodes);
            }
            else if (to < from)
            {
                var prerequisites = node.MissingPrerequisites().ToList();
                for (int i = to; i < from; ++i)
                {
                    if (prerequisites.Contains(_queue[i])) movingNodes.Add(_queue[i]);
                }
                movingNodes.Add(node);
                UnsafeInsert(movingNodes, to);
            }
        }
        void Insert(ResearchNode node, int pos)
        {
            if (CantResearch(node)) return;

            pos = Math.Max(0, Math.Min(_queue.Count, pos));
            var idx = _queue.IndexOf(node);
            if (idx == pos) return;

            if (idx != -1) DoMove(node, idx, pos);
            else UnsafeInsert(node.MissingPrerequisitesInc(), pos);

            UpdateCurrentResearch();
        }
        static Vector2 _scroll_pos = new Vector2(0, 0);
        static void ReleaseNodeAt(ResearchNode node, int dropIdx)
        {
            if (dropIdx == -1) RemoveS(node);

            var tab = MainTabWindow_ResearchTree.Instance;
            if (_instance._queue.IndexOf(node) == dropIdx)
            {
                if (tab.DraggingTime() < 0.2f) node.LeftClick();
            }
            else
            {
                if (DraggingFromQueue() && dropIdx > _instance._queue.IndexOf(node)) ++dropIdx;

                _instance.Insert(node, dropIdx);
                NewUndoState();
            }
        } 
        static bool ReleaseEvent()
        {
            return DraggingNode() && Event.current.type == EventType.MouseUp && Event.current.button == 0;
        }
        static void StopDragging()
        {
            MainTabWindow_ResearchTree.Instance.StopDragging();
        }
        static int DropIndex(Rect visibleRect, Vector2 dropPos)
        {
            Rect relaxedRect = visibleRect;
            relaxedRect.yMin -= NodeSize.y * 0.3f;
            relaxedRect.height += NodeSize.y;

            return (!visibleRect.Contains(dropPos)) ? -1 : (int)(dropPos.x / (Margin + NodeSize.x));
        }
        static List<int> NormalPositions(int n)
        {
            List<int> poss = new List<int>();
            for (int i = 0; i < n; ++i) poss.Add(i);
            return poss;
        }
        static void ResetNodePositions()
        {
            currentPositions = NormalPositions(_instance._queue.Count);
        }
        static List<int> currentPositions = new List<int>();
        static bool nodeDragged = false;
        static public void NotifyNodeDraggedS()
        {
            nodeDragged = true;
        }
        static bool DraggingNode()
        {
            return MainTabWindow_ResearchTree.Instance.IsDraggingNode();
        }
        static bool DraggingFromQueue()
        {
            return MainTabWindow_ResearchTree.Instance.draggingSource == Painter.Queue;
        }
        static ResearchNode DraggedNode()
        {
            return MainTabWindow_ResearchTree.Instance.draggedNode;
        }
        //static List<ResearchNode> workingList = new List<ResearchNode>();
        static public void Draw(Rect baseCanvas, bool interactible) {

            //Draw background
            //GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            FastGUI.DrawTextureFast(baseCanvas, BaseContent.GreyTex, Assets.darkGrey);

            HandleUndo();
            var canvas = CanvasFromBaseCanvas(baseCanvas);

            if (_instance._queue.Count == 0)
            {
                Text.anchorInt = TextAnchor.MiddleCenter;
                GUI.color   = Assets.colorWhite;
                Widgets.Label( canvas, ResourceBank.String.NothingQueued );
                Text.anchorInt = TextAnchor.UpperLeft;
            }

            HandleReleaseOutside(canvas);
            HandleScroll(canvas);

            _scroll_pos = GUI.BeginScrollView(canvas, _scroll_pos, new Rect(0, 0, Mathf.Max(Width(), canvas.width), canvas.height), GUIStyle.none, GUIStyle.none);
            
            var visibleRect = new Rect(_scroll_pos, canvas.size); //was VisibleRect()
            HandleDragReleaseInside(visibleRect);
            UpdateCurrentPosition(visibleRect);

            DrawNodes(visibleRect);

            GUI.EndScrollView(false);

            //Embedded methods
            void DrawNodes(Rect visibleRect)
            {
                // when handling event in nodes, the queue itself may change so using a temporary queue to avoid the unmatching DrawAt and SetRect
                var workingList = _instance._queue.ToArray();

                for (int i = 0; i < workingList.Length; ++i)
                {
                    var node = workingList[i];
                    var pos = currentPositions[i];
                    if (pos != -1) node.DrawAt(new Vector2(pos * (Margin + NodeSize.x), 0), visibleRect, Painter.Queue, true);
                    node.SetRects();
                }

                if (Settings.showIndexOnQueue) DrawLabels(visibleRect);

                if (workingList.Length != _instance._queue.Count) ResetNodePositions();
            }
            void HandleDragReleaseInside(Rect visibleRect)
            {
                if (ReleaseEvent())
                {
                    ReleaseNodeAt(DraggedNode(), DropIndex(visibleRect, Event.current.mousePosition));
                    StopDragging();
                    ResetNodePositions();
                    Event.current.Use();
                }
            }
            Rect CanvasFromBaseCanvas(Rect baseCanvas)
            {
                var r = baseCanvas.ContractedBy(Constants.Margin);
                r.xMin += Margin;
                r.xMax -= Margin;
                return r;
            }
            void HandleScroll(Rect canvas)
            {
                if (Event.current.isScrollWheel && Mouse.IsOver(canvas))
                {
                    _scroll_pos.x += Event.current.delta.y * 20;
                    Event.current.Use();
                }
                else if (DraggingNode())
                {
                    var tab = MainTabWindow_ResearchTree.Instance;
                    var nodePos = tab.draggedPosition;
                    if (  nodePos.y <= canvas.yMin - NodeSize.y || nodePos.y >= canvas.yMax || (  nodePos.x >= canvas.xMin && nodePos.x <= canvas.xMax - NodeSize.x))
                    {
                        return;
                    }
                    float baseScroll = 20;
                    if (nodePos.x < canvas.xMin) _scroll_pos.x -= baseScroll * (canvas.xMin - nodePos.x) / NodeSize.x;
                    else if (nodePos.x > canvas.xMax - NodeSize.x) _scroll_pos.x += baseScroll * (nodePos.x + NodeSize.x - canvas.xMax) / NodeSize.x;
                }
            }
            void UpdateCurrentPosition(Rect visibleRect)
            {
                if (!DraggingNode())
                {
                    if (nodeDragged) ResetNodePositions();
                    else if (currentPositions.Count != _instance._queue.Count) ResetNodePositions();
                    return;
                }
                else if (nodeDragged)
                {
                    List<int> poss = new List<int>();
                    if (!DraggingNode()) currentPositions = NormalPositions(_instance._queue.Count);
                    int draggedIdx = DropIndex(visibleRect, Event.current.mousePosition);
                    var length = _instance._queue.Count;
                    for (int p = 0, i = 0; i < length;)
                    {
                        var node = _instance._queue[i];
                        // The dragged node should disappear
                        if (DraggingFromQueue() && node == DraggedNode())
                        {
                            poss.Add(-1);
                            ++i;
                        // The space of the queue is occupied
                        }
                        else if (draggedIdx == p)
                        {
                            ++p;
                            continue;
                        // usual situation
                        }
                        else
                        {
                            poss.Add(p);
                            ++p;
                            ++i;
                        }
                    }
                    currentPositions = poss;
                }
                nodeDragged = false;
            }
            void HandleReleaseOutside(Rect canvas)
            {
                var mouse = Event.current.mousePosition;
                if (ReleaseEvent() && !canvas.Contains(mouse))
                {
                    var vrange = new Pair<float, float>(canvas.yMin - NodeSize.x * 0.3f, canvas.yMax + NodeSize.y * 0.7f); //was TolerantVerticalRange()
                    if (mouse.y >= vrange.First && mouse.y <= vrange.Second)
                    {
                        if (mouse.x <= canvas.xMin) ReleaseNodeAt(DraggedNode(), 0);
                        else if (mouse.x >= canvas.xMax) ReleaseNodeAt(DraggedNode(), _instance._queue.Count);
                        ResetNodePositions();
                        StopDragging();
                        Event.current.Use();
                    }
                    else if (DraggingFromQueue())
                    {
                        RemoveS(DraggedNode());
                        ResetNodePositions();
                        StopDragging();
                        Event.current.Use();
                    }
                }
            }
            float Width()
            {
                //Get queue length and multiple by margins
                var original = (DraggingNode() && !DraggingFromQueue()) ? _instance._queue.Count + 1 : _instance._queue.Count * (NodeSize.x + Margin) - Margin;
                return Settings.showIndexOnQueue ? original + Constants.QueueLabelSize * 0.5f : original;
            }
            void HandleUndo()
            {
                if (Event.current.type == EventType.KeyDown && Event.current.control)
                {
                    if (Event.current.keyCode == KeyCode.Z)
                    {
                        //was Undo()
                        var oldState = undoState.Undo();
                        if (oldState != null)
                        {
                            Log.Debug("Undo to {0}", DebugQueueSerialize(oldState));
                            _instance.ReplaceMore(oldState); // avoid recording new undo state
                        }
                        Event.current.Use();
                    }
                    else if (Event.current.keyCode == KeyCode.R)
                    {
                        //was Redo()
                        var s = undoState.Redo();
                        if (s != null)
                        {
                            Log.Debug("Redo to {0}", DebugQueueSerialize(s));
                            _instance.ReplaceMore(s); // avoid recording new undo state
                        }
                        Event.current.Use();
                    }
                }
            }
        }
        public static void Notify_InstantFinished()
        {
            foreach (var node in new List<ResearchNode>(_instance._queue))
            {
                if (node.IsFinished) _instance._queue.Remove(node);
            }

            _instance.UpdateCurrentResearch();
        }
    }
}