// Copyright Karel Kroeze, 2018-2020

using System;
using UnityEngine;
using static ResearchPowl.Constants;

namespace ResearchPowl
{
    public class Edge<T1, T2> where T1 : Node where T2 : Node
    {
        public T1 _in;
        public T2 _out;
        ResearchNode _inResearch, _outResearch;

        public Edge( T1 @in, T2 @out )
        {
            _in     = @in;
            _out    = @out;
            isDummy = _out is DummyNode;
        }
        public T1 In
        {
            get => _in;
            set
            {
                _in     = value;
                isDummy = _out is DummyNode;
            }
        }
        public T2 Out
        {
            get => _out;
            set
            {
                _out    = value;
                isDummy = _out is DummyNode;
            }
        }

        public int Span => _out.X - _in.X;
        public bool isDummy;

        public int DrawOrder()
        {
            if ( OutResearch().HighlightInEdge(InResearch()) ) return 3;
            if ( OutResearch().Research.IsFinished ) return 2;
            if ( OutResearch()._available ) return 1;
            return 0;
        }
        public ResearchNode InResearch()
        {
            if (_inResearch == null)
            {
                if (In is ResearchNode rn) _inResearch = rn;
                else if (In is DummyNode dn) _inResearch = dn.InResearch();
            }
            return _inResearch;
        }
        public ResearchNode OutResearch()
        {
            if (_outResearch == null)
            {
                if (Out is ResearchNode rn) _outResearch = rn;
                else if (Out is DummyNode dn) _outResearch = dn.OutResearch();
            }
            return _outResearch;
        }
       
        public void DrawEnd(Rect visibleRect, Vector2 left, Vector2 right, Color color)
        {
            if (isDummy)
            {
                // or draw a line piece through the dummy
                var through = new Rect(right.x, right.y - 2, NodeSize.x, 4f);
                if (through.Overlaps(visibleRect)) FastGUI.DrawTextureFast(through, Assets.LineEW, color);
                return;
            }
            // draw the end arrow (if not dummy)
            var end = new Rect(right.x - 16f, right.y - 8f, 16f, 16f);
            if (end.Overlaps(visibleRect)) FastGUI.DrawTextureFast(end, Assets.LineEnd, color);
        }
        public void DrawComplicatedSegments(Rect visibleRect, Vector2 left, Vector2 right, Color color)
        {
            // draw three line pieces and two curves.
            // determine top and bottom y positions
            var yMin = left.y < right.y ? left.y : right.y;
            var yMax = left.y > right.y ? left.y : right.y;
            var top = yMin + NodeMargins.x / 4f;
            var bottom = yMax - NodeMargins.x / 4f;

            // if too far off, just skip
            if (!(new Rect(left.x, yMin, right.x - left.x, yMax - yMin).Overlaps(visibleRect))) return;

            // straight bits
            // left to curve
            var leftToCurve = new Rect(left.x, left.y - 2f, NodeMargins.x / 4f, 4f );
            if (leftToCurve.Overlaps(visibleRect)) FastGUI.DrawTextureFast(leftToCurve, Assets.LineEW, color);

            // curve to curve
            var curveToCurve = new Rect( left.x + NodeMargins.x / 2f - 2f, top, 4f, bottom - top );
            if (curveToCurve.Overlaps(visibleRect)) FastGUI.DrawTextureFast(curveToCurve, Assets.LineNS, color);

            // curve to right
            var curveToRight = new Rect( left.x + NodeMargins.x / 4f * 3f + 1f, right.y - 2f, right.x - left.x - NodeMargins.x / 4f * 3f, 4f );
            if (curveToRight.Overlaps(visibleRect)) FastGUI.DrawTextureFast(curveToRight, Assets.LineEW, color);

            // curve positions
            var curveLeft = new Rect(left.x + NodeMargins.x / 4f, left.y - NodeMargins.x / 4f, NodeMargins.x / 2f, NodeMargins.x / 2f );
            var curveRight = new Rect(left.x + NodeMargins.x / 4f + 1f, right.y - NodeMargins.x / 4f, NodeMargins.x / 2f, NodeMargins.x / 2f );

            // going down
            if (left.y < right.y)
            {
                if (curveLeft.Overlaps(visibleRect)) FastGUI.DrawTextureFastWithCoords(curveLeft, Assets.LineCircle, color, new Rect(0.5f, 0.5f, 0.5f, 0.5f));
                if (curveRight.Overlaps(visibleRect)) FastGUI.DrawTextureFastWithCoords(curveRight, Assets.LineCircle, color, new Rect(0f, 0f, 0.5f, 0.5f));
                // bottom right quadrant
                // top left quadrant
            }
            else
            {
                // going up
                if (curveLeft.Overlaps(visibleRect)) FastGUI.DrawTextureFastWithCoords(curveLeft, Assets.LineCircle, color, new Rect(0.5f, 0f, 0.5f, 0.5f));
                // top right quadrant
                if (curveRight.Overlaps(visibleRect)) FastGUI.DrawTextureFastWithCoords(curveRight, Assets.LineCircle, color, new Rect(0f, 0.5f, 0.5f, 0.5f));
                // bottom left quadrant
            }
        }
        Color colorCache;
        public void DrawLines( Rect visibleRect )
        {
            var left  = _in.Right;
            var right = _out.Left;
            if (ResearchNode.mouseOverDirty || ResearchNode.availableDirty) colorCache = Out.InEdgeColor(InResearch());;
            var color = colorCache;

            if ((Tree.filteredOut.Contains(_inResearch?.Research.index ?? 0) || Tree.filteredOut.Contains(_outResearch?.Research.index ?? 0)) && (!_inResearch?.Highlighted() ?? false)) color.a = Faded;

            // if left and right are on the same level, just draw a straight line.
            if (left.y == right.y)
            {
                var line = new Rect( left.x, left.y - 2f, right.x - left.x, 4f );
                if (line.Overlaps(visibleRect)) FastGUI.DrawTextureFast(line, Assets.LineEW, color);
            }
            else DrawComplicatedSegments(visibleRect, left, right, color);
            DrawEnd(visibleRect, left, right, color);
        }
        public override string ToString()
        {
            return _in + " -> " + _out;
        }
    }
}