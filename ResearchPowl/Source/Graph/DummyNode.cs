// DummyNode.cs
// Copyright Karel Kroeze, 2018-2020

using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace ResearchPowl
{
    public class DummyNode : Node
    {
        public override string Label => "DUMMY";

        #region Overrides of Node

        #if DEBUG_DUMMIES
        public override void Draw()
        {
            // cop out if off-screen
            var screen = new Rect( MainTabWindow_ResearchTree._scrollPosition.x,
                                   MainTabWindow_ResearchTree._scrollPosition.y, Screen.width, Screen.height - 35 );
            if ( Rect.xMin > screen.xMax ||
                 Rect.xMax < screen.xMin ||
                 Rect.yMin > screen.yMax ||
                 Rect.yMax < screen.yMin )
            {
                return;
            }

            Widgets.DrawBox( Rect );
            Widgets.Label( Rect, Label );
        }
        #endif

        #endregion

        public List<ResearchNode> Parent()
        {
            List<ResearchNode> workingList = new List<ResearchNode>();
            var list = InNodes();
            for (int i = 0; i < list.Length; i++)
            {
                var node = list[i];
                if (node is DummyNode dNode) workingList.AddRange(dNode.Parent());
            }
            return workingList;
        }
        public List<ResearchNode> Child()
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
        public override bool Highlighted()
        {
            return OutResearch().HighlightInEdge(InResearch());
        }
        public ResearchNode OutResearch()
        {
            return _outEdges[0].OutResearch();
        }
        public ResearchNode InResearch()
        {
            return _inEdges[0].InResearch();
        }
        public override Color Color {
            get {
                return OutResearch().InEdgeColor(InResearch());
            }
        }
    }
}