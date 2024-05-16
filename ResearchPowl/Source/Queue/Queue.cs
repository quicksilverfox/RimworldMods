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
        static UndoStateHandler<ResearchNode[]> undoState = new UndoStateHandler<ResearchNode[]>();

        public Queue(World world) : base(world) {
            _instance = this;
        }

        public static void DrawLabels( Rect visibleRect )
        {
            var list = _instance._queue;
            for (int j = list.Count, i = 1; j-- > 0; i++)
            {
                var node = list[j];
                if (node.IsVisible(visibleRect))
                {
                    ColorCompleted.TryGetValue(node.Research.techLevel, out Color main);
                    var background = i > 1 ? ColorUnavailable.TryGetValue(node.Research.techLevel) : main;
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
            foreach (var n in nodes) if (!_queue.Contains(n)) _queue.Add(n);
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
                _queue.Remove(n);
                _queue.Insert(i++, n);
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
            return _queue.FirstOrDefault()?.Research;
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
                if (n.Research.IsFinished) finished.Add(n);
                else if (!n.GetAvailable()) unavailable.Add(n);
            }
            foreach (var n in finished) _queue.Remove(n);
            foreach (var n in unavailable) Remove(n);
            
            var cur = Find.ResearchManager.currentProj;
            if (cur != null && cur != CurrentResearch()) Replace(Find.ResearchManager.currentProj.ResearchNode());
            UpdateCurrentResearch();
        }
        bool Remove(ResearchNode node)
        {
            if (node.Research.IsFinished) return _queue.Remove(node);

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
            if (Settings.verboseDebug) return string.Join(", ", nodes.Select(n => n.Research.label));
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
            var current = CurrentS();

            var finishedNode = _instance._queue.Find(n => n.Research == finished);
            if (finishedNode == null) return;
            RemoveS(finishedNode);
            if (finishedNode != current) return;
            var next = CurrentS()?.Research;
            Find.ResearchManager.currentProj = next;
            if (! Settings.useVanillaResearchFinishedMessage)
            {
                Log.Debug("Send completion letter for {0}, next is {1}", current.Research.label, next?.label ?? "NONE");
                //was DoCompletionLetter()
                string label = "ResearchFinished".Translate( current.Research.LabelCap );
                string text  = current.Research.LabelCap + "\n\n" + current.Research.description;

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
            if ( Scribe.mode == LoadSaveMode.Saving ) _saveableQueue = new List<ResearchProjectDef>(_queue.Select( node => node.Research));

            Scribe_Collections.Look( ref _saveableQueue, "Queue", LookMode.Def );

            if ( Scribe.mode == LoadSaveMode.PostLoadInit )
            {
                foreach (var research in _saveableQueue)
                {
                    // find a node that matches the research - or null if none found
                    var node = research.ResearchNode();
                    if (node != null) UnsafeAppend(node);
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
                if (node.Research.IsFinished) _instance._queue.Remove(node);
            }

            Find.ResearchManager.currentProj = _instance._queue.FirstOrDefault()?.Research;
        }
    }
}