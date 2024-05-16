using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Text.RegularExpressions;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
    public class NodeLayers
    {
        public NodeLayer[] _layers;

        public NodeLayers(List<List<Node>> layers)
        {
            _layers = layers.Select((layer, idx) => new NodeLayer(idx, layer, this)).ToArray();
        }
        //public int LayerCount() => _layers.Count;
        public int NodeCount() => _layers.Select(l => l._nodes.Count).Sum();
        public NodeLayer Layer(int n)
        {
            return _layers[n];
        }
        void NLevelBCPhase1(int maxIter)
        {
            var layerCount = _layers.Length;

            for (int n = 0; n < maxIter; ++n)
            {
                for (int i = 1; i < layerCount; ++i) _layers[i].SortByUpperBarycenter();
                for (int i = layerCount - 2; i >= 0; --i) _layers[i].SortByLowerBarycenter();
            }
        }
        public void NLevelBCMethod(int maxIter1, int maxIter2)
        {
            NLevelBCPhase1(maxIter1);
            var layerCount = _layers.Length;
            for (int k = 0; k < maxIter2; ++k)
            {
                for (int i = layerCount - 2; i >= 0; --i)
                {
                    if ( _layers[i].ReverseLowerBarycenterTies() && ! MathUtil.Ascending(_layers[i + 1]._nodes.Select(n => n.UpperBarycenter())))
                    {
                        NLevelBCPhase1(maxIter1);
                    }
                }
                for (int i = 1; i < layerCount; ++i)
                {
                    if ( _layers[i].ReverseUpperBarycenterTies() && ! MathUtil.Ascending(_layers[i - 1]._nodes.Select(n => n.LowerBarycenter())))
                    {
                        NLevelBCPhase1(maxIter1);
                    }
                }
            }
        }
        void BruteforceSwapping(int maxIter)
        {
            for (int k = 0; k < maxIter; ++k)
            {
                for (int i = 1; i < _layers.Length; ++i) _layers[i].UnsafeBruteforceSwapping();
                for (int i = _layers.Length - 2; i >= 0; --i) _layers[i].UnsafeBruteforceSwapping();
            }

            foreach (var layer in _layers) layer.RearrangeOrder();
        }
        public void ApplyGridCoordinates() {
            foreach (var layer in _layers) {
                layer.ApplyGridCoordinates();
            }
        }
        public void ImproveNodePositionsInLayers()
        {
            for (int i = 0; i < _layers.Length; i++) _layers[i].AssignPositionPriorities();
            
            for (int i = 1; i < _layers.Length; ++i) _layers[i].ImprovePositionAccordingToUpper();
            
            for (int i = _layers.Length - 2; i >= 0; --i) _layers[i].ImprovePositionAccordingToLower();
            
            for (int i = 1; i < _layers.Length; ++i) _layers[i].ImprovePositionAccordingToUpper();
            
            if (!Settings.alignToAncestors)
            {
                for (int i = _layers.Length - 2; i >= 0; --i) _layers[i].ImprovePositionAccordingToLower();
            }
            AlignSegments(3);
        }
        public void MoveVertically(float f)
        {
            foreach (var l in _layers) l.MoveVertically(f);
        }
        public float TopPosition(int l) {
            return _layers[l].TopPosition();
        }
        public IEnumerable<Node> AllNodes()
        {
            foreach (var item in _layers)
            {
                foreach (var item2 in item) yield return item2;
            }
        }
        static List<List<Node>> EmptyNewLayers(int n)
        {
            var result = new List<List<Node>>();
            for (int i = 0; i < n; ++i)
            {
                result.Add(new List<Node>());
            }
            return result;
        }
        static void MergeDataFromTo(Node[] ns, List<List<Node>> data)
        {
            for (int i = 0; i < ns.Length; i++)
            {
                var n = ns[i];
                data[n.lx].Add(n);
            }
        }
        void DFSConnectiveComponents(Node cur, List<List<Node>> data, HashSet<Node> visited)
        {
            if (cur == null) return;
            visited.Add(cur);
            data[cur.lx].Add(cur);
            var list = cur.LocalInNodes();
            for (int i = 0; i < list.Length; i++)
            {
                var n = list[i];
                if (! visited.Contains(n)) DFSConnectiveComponents(n, data, visited);
            }
            list = cur.LocalOutNodes();
            for (int i = 0; i < list.Length; i++)
            {
                var n = list[i];
                if (! visited.Contains(n)) DFSConnectiveComponents(n, data, visited);
            }
        }
        public IEnumerable<NodeLayers> SplitConnectiveComponents()
        {
            HashSet<Node> visited = new HashSet<Node>();
            foreach (var node in AllNodes())
            {
                if (!visited.Contains(node))
                {
                    var data = EmptyNewLayers(_layers.Length);
                    DFSConnectiveComponents(node, data, visited);
                    yield return new NodeLayers(data);
                }
            }
        }
        static void AlignNode(Node node)
        {
            bool aligned = false;
            do
            {
                var segment = node.LocalSegment();
                aligned = segment.Align();
            }
            while (aligned);
        }
        void AlignSegments(int maxIter)
        {
            for (int n = 0; n < maxIter; ++n)
            {
                if (Settings.alignToAncestors)
                {
                    for (int i = 0; i < _layers.Length; ++i)
                    {
                        var list = _layers[i]._nodes;
                        var length2 = list.Count;
                        for (int j = 0; j < length2; j++) AlignNode(list[j]);
                    }
                }
                else
                {
                    for (int i = _layers.Length - 1; i >= 0; --i)
                    {
                        var list = _layers[i]._nodes;
                        var length2 = list.Count;
                        for (int j = 0; j < length2; j++) AlignNode(list[j]);
                    }
                }
            }
        }
        static Regex regex = new Regex("^Vanilla (.*)Expanded( - .*)?$", RegexOptions.Compiled); //This should probably be moved somewhere
        static string GroupingByMods(Node node)
        {
            if (node is ResearchNode n)
            {
                string name;
                if (n.Research.modContentPack == null) 
                {
                    Log.Debug("Research {0} does not belong to any mod?", n.Label);
                    name = "__Vanilla";
                }
                else name = n.Research.modContentPack.Name; 
                //Is an official mod?
                if (name == ModContentPack.LudeonPackageIdAuthor) return "__Vanilla";
                //Is a VE mod?
                else if (regex.IsMatch(name) || name.Contains("VFE")) return "__VanillaExpanded";
                return name;
            }
            else if (node is DummyNode) return GroupingByMods(node.OutNodes()[0]);
            return "";
        }
        static string GroupingByTabs(Node node)
        {
            if (node is ResearchNode n) return n.Research.tab?.defName ?? "__Vanilla";
            else if (node is DummyNode) return GroupingByTabs(node.OutNodes()[0]);
            return "__Vanilla";
        }      
        public IEnumerable<NodeLayers> SplitMods()
        {
            if (!Settings.placeModTechSeparately && !Settings.placeTabsSeparately)
            {
                yield return this;
            }
            else if (Settings.placeModTechSeparately)
            {
                foreach (var item in SplitLargeMods()) yield return item;
            }
            else
            {
                foreach (var item in SplitByTabs()) yield return item;
            }
        }
        public IEnumerable<NodeLayers> SplitByTabs()
        {
            var vanilla = EmptyNewLayers(_layers.Length);
            var result = new List<List<List<Node>>>() {vanilla};
            
            foreach (var group in AllNodes().GroupBy(n => GroupingByTabs(n)))
            {
                if (group.Key == "__Vanilla")
                {
                    MergeDataFromTo(group.ToArray(), vanilla);
                }
                else
                {
                    var newTab = EmptyNewLayers(_layers.Length);
                    MergeDataFromTo(group.ToArray(), newTab);
                    result.Add(newTab);
                }
            }
            //Return results
            var length = result.Count;
            for (int i = 0; i < length; i++)
            {
                yield return new NodeLayers(result[i]);
            }
        }
        public IEnumerable<NodeLayers> SplitLargeMods()
        {
            var vanilla = EmptyNewLayers(_layers.Length);
            var result = new List<List<List<Node>>>() {vanilla};
            
            foreach (var group in AllNodes().GroupBy(n => GroupingByMods(n)))
            {
                var ns = group.ToArray();
                var techCount = 0;
                for (int j = 0; j < ns.Length; j++)
                {
                    var n1 = ns[j];
                    if (n1 is ResearchNode) ++techCount;
                }

                if (techCount < Settings.largeModTechCount || group.Key == "__Vanilla")
                {
                    MergeDataFromTo(ns, vanilla);
                }
                else
                {
                    var newMod = EmptyNewLayers(_layers.Length);
                    MergeDataFromTo(ns, newMod);
                    result.Add(newMod);
                }
            }
            //Return results
            var length = result.Count;
            for (int i = 0; i < length; i++)
            {
                yield return new NodeLayers(result[i]);
            }
        }
    }

    public class NodeLayer
    {
        public List<Node> _nodes;
        public int _layer;
        public NodeLayers _layers;

        public NodeLayer(int layer, List<Node> nodes, NodeLayers layers)
        {
            _layer = layer;
            _layers = layers;
            _nodes = nodes;
            var length = _nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n = _nodes[i];
                n.layer = this;
                n.lx = layer;
            }
            AdjustY();
        }

        public Node this[int i] {
            get { return _nodes[i]; }
        }

        public bool AdjustY()
        {
            bool changed = false;
            var length = _nodes.Count;
            for (int i = 0; i < length; ++i)
            {
                var n = _nodes[i];
                changed = changed || n.ly != i;
                n.ly = i;
            }
            return changed;
        }

        public void SortBy(Func<Node, Node, int> comparator)
        {
            //_nodes.SortStable(comparator);

            var length = _nodes.Count;
            if (length > 0)
			{                
                List<Pair<Node, int>> list2 = new List<Pair<Node, int>>(length);
                
                for (int i = 0; i < length; i++) list2.Add( new Pair<Node, int>(_nodes[i], i) );

                list2.Sort(delegate(Pair<Node, int> lhs, Pair<Node, int> rhs)
                {
                    int num = comparator(lhs.first, rhs.first);
                    if (num != 0) return num;
                    return lhs.second.CompareTo(rhs.second);
                });
                _nodes.Clear();
                for (int j = 0; j < length; j++) _nodes.Add(list2[j].first);
            }
            
            AdjustY();
        }

        void SwapNodeY(int i, int j)
        {
            int temp = _nodes[i].ly;
            _nodes[i].ly = _nodes[j].ly;
            _nodes[j].ly = temp;
        }

        public void UnsafeBruteforceSwapping()
        {
            int nodes = _nodes.Count;
            for (int i = 0; i < nodes - 1; ++i)
            {
                for (int j = i + 1; j < nodes; ++j)
                {
                    Node ni = _nodes[i], nj = _nodes[j];
                    int c1 = ni.Crossings() + nj.Crossings();
                    int l1 = ni.EdgeLengthSquare() + nj.EdgeLengthSquare();
                    SwapNodeY(i, j);
                    int c2 = ni.Crossings() + nj.Crossings();
                    int l2 = ni.EdgeLengthSquare() + nj.EdgeLengthSquare();
                    if (c2 < c1 || c2 == c1 && l2 < l1) continue;
                    SwapNodeY(i, j);
                }
            }
        }

        public void RearrangeOrder()
        {
            _nodes.SortBy(n => n.ly);
            AdjustY();
        }

        public IEnumerator<Node> GetEnumerator()
        {
            return _nodes.GetEnumerator();
        }

        public void SortByUpperBarycenter()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n = _nodes[i];
                n.doubleCache = n.UpperBarycenter();
            }
            SortBy((n1, n2) => n1.doubleCache.CompareTo(n2.doubleCache));
        }
        public void SortByLowerBarycenter()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n = _nodes[i];
                n.doubleCache = n.LowerBarycenter();
            }
            SortBy((n1, n2) => n1.doubleCache.CompareTo(n2.doubleCache));
        }
        void ReverseSegment(int i, int j) {
            for (--j; i < j; ++i, --j)
            {
                Node temp = _nodes[i];
                _nodes[i] = _nodes[j];
                _nodes[j] = temp;
            }
        }
        public bool ReverseLowerBarycenterTies()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length - 1; )
            {
                var bi = _nodes[i].LowerBarycenter();
                int j = i + 1;
                ReverseSegment(i, j);
                i = j;
            }
            return AdjustY();
        }
        public bool ReverseUpperBarycenterTies()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length - 1; )
            {
                var bi = _nodes[i].UpperBarycenter();
                int j = i + 1;
                ReverseSegment(i, j);
                i = j;
            }
            return AdjustY();
        }
        public void ApplyGridCoordinates()
        {
            var length = _nodes.Count;
            for (int i = 0; i < length; ++i)
            {
                var n = _nodes[i];
                n.X = _layer + 1;
                n.Y = i + 1;
            }
        }
        public void AssignPositionPriorities()
        {
            var ordering = _nodes.OrderBy(n => n.DefaultPriority()).ToArray();
            for (int i = 0; i < ordering.Length; ++i) ordering[i].assignedPriority = i;
        }
        public void ImprovePositionAccordingToLower()
        {
            var list = _nodes.OrderByDescending(n => n.LayoutPriority()).ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var n = list[i];
                float c = (float) Math.Round(n.LowerPositionBarycenter());
                if (UnityEngine.Mathf.Approximately(c, n._pos.y)) continue;
                
                if (c < n._pos.y) n.PushUpTo(c);
                else n.PushDownTo(c);
            }
        }
        public void ImprovePositionAccordingToUpper()
        {
            var list = _nodes.OrderByDescending(n => n.LayoutPriority()).ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var n = list[i];
                float c = (float) Math.Round(n.UpperPositionBarycenter());
                if (UnityEngine.Mathf.Approximately(c, n._pos.y)) continue;
                if (c < n._pos.y) n.PushUpTo(c);
                else n.PushDownTo(c);
            }
        }
        public float TopPosition()
        {
            if (_nodes.Count == 0) return 99999;
            return _nodes[0]._pos.y;
        }

        public void MoveVertically(float f)
        {
            var length = _nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n = _nodes[i];
                n.Yf = n._pos.y + f;
            }
        }

        public bool IsBottomLayer() => _layer >= _layers._layers.Length;
        public NodeLayer LowerLayer() => IsBottomLayer() ? null : _layers.Layer(_layer + 1);

    }

    static class MathUtil {
        public static bool SignDiff(int x, int y)
        {
            return x < 0 && y > 0 || x > 0 && y < 0;
        }
        public static bool SignDiff(float x, float y)
        {
            return x < 0 && y > 0 || x > 0 && y < 0;
        }

        public static bool Ascending(IEnumerable<double> xs)
        {
            return xs.Zip(xs.Skip(1), (a, b) => new {a, b}).All(p => p.a <= p.b);
        }
    }

    static class NodeUtil {
        static float MinimumVerticalDistance = 1;
        
        public static int LowerCrossings(this Node n1)
        {
            int sum = 0;

            var list2 = n1.LocalOutNodes();
            //Make list1
            var list1 = new Node[n1.layer._nodes.Count];
            var length = n1.layer._nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n2 = n1.layer._nodes[i];
                if (n2 != n1)
                {
                    var list3 = n2.LocalOutNodes();

                    for (int j = 0; j < list2.Length; j++)
                    {
                        var m1 = list2[j];
                        for (int k = 0; k < list3.Length; k++)
                        {
                            var m2 = list3[k];
                            var x = n1.ly - n2.ly;
                            var y = m1.ly - m2.ly;
                            if (x < 0 && y > 0 || x > 0 && y < 0) ++sum;
                        }
                    }
                }
            }
            return sum;
        }

        public static int UpperCrossings(this Node n1)
        {
            int sum = 0;
           
            var list2 = n1.LocalInNodes();
            var length = n1.layer._nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n2 = n1.layer._nodes[i];
                if (n2 != n1)
                {
                    var list3 = n2.LocalInNodes();

                    for (int j = 0; j < list2.Length; j++)
                    {
                        var m1 = list2[j];
                    
                        for (int k = 0; k < list3.Length; k++)
                        {
                            var m2 = list3[k];
                            var x = n1.ly - n2.ly;
                            var y = m1.ly - m2.ly;
                            if (x < 0 && y > 0 || x > 0 && y < 0) ++sum;
                        }
                    }
                }
            }
            return sum;
        }

        public static int UpperEdgeLengthSquare(this Node node) {
            var tmp = node.LocalInNodes();
            int sum = 0;
            for (int i = 0; i < tmp.Length; ++i)
            {
                var tmp2 = tmp[i];
                sum += (node.ly - tmp2.ly) * (node.ly - tmp2.ly);
            }
            return sum;
        }
        public static int LowerEdgeLengthSquare(this Node node)
        {
            var tmp = node.LocalOutNodes();
            int sum = 0;
            for (int i = 0; i < tmp.Length; ++i)
            {
                var tmp2 = tmp[i];
                sum += (node.ly - tmp2.ly) * (node.ly - tmp2.ly);
            }
            return sum;
        }

        public static int EdgeLengthSquare(this Node node)
        {
            return node.UpperEdgeLengthSquare() + node.LowerEdgeLengthSquare();
        }

        public static int Crossings(this Node n)
        {
            return LowerCrossings(n) + UpperCrossings(n);
        }

        public static double LowerBarycenter(this Node node)
        {
            var outs = node.LocalOutNodes();
            if (outs.Length == 0) return node.ly;
            
            double sum = 0;
            for (int i = 0; i < outs.Length; i++)
            {
                sum += outs[i].ly;
            }
            return sum / (double)outs.Length;
        }
        public static double UpperBarycenter(this Node node)
        {
            var ins = node.LocalInNodes();
            if (ins.Length == 0) return node.ly;

            double sum = 0;
            for (int i = 0; i < ins.Length; i++)
            {
                sum += ins[i].ly;
            }
            return sum / (double)ins.Length;
        }

        public static double LowerPositionBarycenter(this Node node)
        {
            var outs = node.LocalOutNodes();
            if (outs.Length == 0) return node._pos.y;

            double sum = 0;
            for (int i = 0; i < outs.Length; i++)
            {
                sum += outs[i]._pos.y;
            }
            return sum / (double)outs.Length;
        }

        public static double UpperPositionBarycenter(this Node node)
        {
            var ins = node.LocalInNodes();
            if (ins.Length == 0) return node.Yf;

            double sum = 0;
            for (int i = 0; i < ins.Length; i++)
            {
                sum += ins[i]._pos.y;
            }
            return sum / (double)ins.Length;
        }

        public static Node MovingUpperbound(this Node node)
        {
            for (int i = node.ly - 1; i >= 0; --i)
            {
                if (node.layer[i].LayoutPriority() > node.LayoutPriority()) return node.layer[i];
            }
            return null;
        }
        public static Node MovingLowerbound(this Node node)
        {
            for (int i = node.ly + 1; i < node.layer._nodes.Count; ++i)
            {
                if (node.layer[i].LayoutPriority() > node.LayoutPriority()) return node.layer[i];
            }
            return null;
        }
        public static void PushUpTo(this Node node, float target) {
            Node blocker = node.MovingUpperbound();
            var layer = node.layer;
            if (blocker == null) node.Yf = target;
            else node.Yf = Math.Max(blocker.Yf + (node.ly - blocker.ly) * MinimumVerticalDistance, target);
            
            for (int i = node.ly - 1; i > (blocker?.ly ?? -1) && layer[i].Yf > layer[i + 1].Yf - MinimumVerticalDistance; --i)
            {
                layer[i].Yf = layer[i + 1].Yf - MinimumVerticalDistance;
            }
        }

        public static void PushDownTo(this Node node, float target) {
            Node blocker = node.MovingLowerbound();
            var layer = node.layer;
            if (blocker == null) node.Yf = target;
            else node.Yf = Math.Min(blocker.Yf - (blocker.ly - node.ly) * MinimumVerticalDistance, target);
            for ( int i = node.ly + 1; i < (blocker?.ly ?? layer._nodes.Count) && layer[i].Yf < layer[i - 1].Yf + MinimumVerticalDistance; ++i)
            {
                layer[i].Yf = layer[i - 1].Yf + MinimumVerticalDistance;
            }
        }

        public static Node[] LocalOutNodes(this Node node) {
            
            var list = node.OutNodes();
            var workingList = new Node[list.Length];
            int index = 0;
            
            for (int i = 0; i < list.Length; i++)
            {
                var tmp = list[i];
                if (tmp.layer == (node.layer._layer >= node.layer._layers._layers.Length ? null : node.layer._layers._layers[node.layer._layer + 1]))
                {
                    workingList[index++] = tmp;
                }

            }
            Array.Resize<Node>(ref workingList, index);
            return workingList;
        }
        
        public static Node[] LocalInNodes(this Node node)
        {
            var list = node.InNodes();
            var workingList = new Node[list.Length];
            var index = 0;
            for (int i = 0; i < list.Length; i++)
            {
                var tmp = list[i];
                if (tmp.layer == (node.layer._layer == 0 ? null : node.layer._layers._layers[node.layer._layer - 1])) //was UpperLayer()
                {
                    workingList[index++] = tmp;
                }
            }
            Array.Resize<Node>(ref workingList, index);
            return workingList;
        }
        public static NodeSegment LocalSegment(this Node node) {
            List<Node> segment = new List<Node>() {node};
            for (var outs = node.LocalOutNodes(); outs.Length == 1;)
            {
                var n = outs[0];
                if (!UnityEngine.Mathf.Approximately(n._pos.y, node._pos.y)) break;
                segment.Add(n);
                outs = n.LocalOutNodes();
            }
            for (var ins = node.LocalInNodes(); ins.Length == 1;)
            {
                var n = ins[0];
                if (!UnityEngine.Mathf.Approximately(n._pos.y, node._pos.y)) break;
                segment.Insert(0, n);
                ins = n.LocalOutNodes();
            }
            return new NodeSegment(segment);
        }
    }

    public class NodeSegment
    {
        List<Node> _nodes;

        public NodeSegment(List<Node> nodes)
        {
            _nodes = nodes;
        }
        float UpperMaximumEmptySpace()
        {
            float result = 99999;
            foreach (var n in _nodes)
            {
                if (n.ly <= 0) continue;
                var layer = n.layer;
                result = Math.Min(result, n.Yf - layer[n.ly - 1].Yf - 1);
            }
            return result;
        }
        float LowerMaximumEmptySpace()
        {
            float result = 99999;
            foreach (var n in _nodes)
            {
                var layer = n.layer;
                if (n.ly >= layer._nodes.Count - 1) continue;
                result = Math.Min(result, layer[n.ly + 1].Yf - n.Yf - 1);
            }
            return result;
        }
        float SelectAppropriateMovement(IEnumerable<Node> alignTo, float pos)
        {
            var dys = alignTo.Select(n => n.Yf - pos).ToArray();
            float dymax = dys.Max(), dymin = dys.Min();
            if (MathUtil.SignDiff(dymax, dymin)) return (float) Math.Round(dys.Average());
            return dymin;
        }
        float? ForwardAlignTarget()
        {
            var outs = _nodes[_nodes.Count - 1].LocalOutNodes();
            if (outs.Length == 0) return null;
            return SelectAppropriateMovement(outs, _nodes[0].Yf);
        }
        float? BackwardAlignTarget()
        {
            var ins = _nodes[0].LocalInNodes();
            if (ins.Length == 0) return null;
            return SelectAppropriateMovement(ins, _nodes[0].Yf);
        }
        float DetermineMovement(float? attempt, out bool aligned)
        {
            aligned = false;
            if (attempt == null) return 0;
            if (attempt > 0)
            {
                var lm = LowerMaximumEmptySpace();
                if (lm >= attempt.Value)
                {
                    aligned = true;
                    return attempt.Value;
                }
                return lm;
            }
            if (attempt < 0)
            {
                var um = -UpperMaximumEmptySpace();
                if (um <= attempt.Value)
                {
                    aligned = true;
                    return attempt.Value;
                }
            }
            return 0;
        }
        float DetermineMovement(float? left, float? right, out bool aligned)
        {
            if (left == null) return DetermineMovement(right, out aligned);
            if (right == null) return DetermineMovement(left, out aligned);
            if (MathUtil.SignDiff(left.Value, right.Value))
            {
                var res = DetermineMovement((float) Math.Round((left.Value - right.Value) / 2), out aligned);
                aligned = false;
                return res;
            }
            if (Math.Abs(left.Value) < Math.Abs(right.Value)) return DetermineMovement(left.Value, out aligned);
            return DetermineMovement(right.Value, out aligned);
        }
        public bool Align()
        {
            bool aligned;
            float? left = BackwardAlignTarget(), right = ForwardAlignTarget();
            float movement = DetermineMovement(left, right, out aligned);
            var length = _nodes.Count;
            for (int i = 0; i < length; i++)
            {
                var n = _nodes[i];
                n.Yf = n.Yf + movement; //was MoveVertically(movement);
            }
            return aligned;
        }
    }
}
