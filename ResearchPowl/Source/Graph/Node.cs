// Node.cs
// Copyright Karel Kroeze, 2019-2020

using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static ResearchPowl.Constants;

namespace ResearchPowl
{
    public class Node
    {
        public List<Edge<Node, Node>> _inEdges = new List<Edge<Node, Node>>();
        protected bool _largeLabel, _rectsSet;
        public List<Edge<Node, Node>> _outEdges = new List<Edge<Node, Node>>();
        public Vector2 _pos = Vector2.zero;
        protected Rect _queueRect, _labelRect, _costLabelRect, _costIconRect, _iconsRect, _lockRect;
        public Rect _rect;
        protected Vector2 _topLeft = Vector2.zero, _right = Vector2.zero, _left = Vector2.zero;

        public List<Node> Descendants()
        {
            List<Node> workingList = new List<Node>(OutNodes());
            foreach (var item in OutNodes()) workingList.AddRange(item.Descendants());
            return workingList;
        }

        public Node[] OutNodes()
        {
            var workingList = new Node[_outEdges.Count];
            for (int i = 0; i < workingList.Length; i++)
            {
                workingList[i] = _outEdges[i]._out;
            }
            return workingList;
        }
        public Node[] InNodes()
        {
            var workingList = new Node[_inEdges.Count];
            for (int i = 0; i < workingList.Length; i++)
            {
                workingList[i] = _inEdges[i]._in;
            }
            return workingList;
        }

        public Rect CostIconRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _costIconRect;
            }
        }

        public Rect CostLabelRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _costLabelRect;
            }
        }

        public virtual Color Color  => Assets.colorWhite;
        public virtual Color InEdgeColor(ResearchNode from)
        {
            return Color;
        }

        public Rect IconsRect
        {
            get
            {
                if (!_rectsSet) SetRects();
                return _iconsRect;
            }
        }

        public Vector2 Left
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _left;
            }
        }

        public Rect QueueRect
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _queueRect;
            }
        }

        public Rect Rect
        {
            get
            {
                if (!_rectsSet) SetRects();
                return _rect;
            }
        }

        public Vector2 Right
        {
            get
            {
                if ( !_rectsSet )
                    SetRects();

                return _right;
            }
        }

        public virtual int X
        {
            get => (int) _pos.x;
            set
            {
                if ( value < 0 )
                    throw new ArgumentOutOfRangeException( nameof( value ) );
                if ( Math.Abs( _pos.x - value ) < Epsilon )
                    return;

                _pos.x = value;

                // update caches
                _rectsSet       = false;
                Tree.OrderDirty = true;
            }
        }

        public virtual int Y
        {
            get => (int) _pos.y;
            set
            {
                if ( value < 0 )
                    throw new ArgumentOutOfRangeException( nameof( value ) );
                if ( Math.Abs( _pos.y - value ) < Epsilon )
                    return;

                _pos.y = value;

                // update caches
                _rectsSet       = false;
                // Tree.Size.z     = Tree.Nodes().Max( n => n.Y );
                Tree.OrderDirty = true;
            }
        }

        public virtual Vector2 Pos => new Vector2( X, Y );

        public virtual float Yf
        {
            get => _pos.y;
            set
            {
                if (Math.Abs( _pos.y - value) < Epsilon ) return;

                _pos.y = value;
                Tree.OrderDirty = true;
            }
        }

        public virtual string Label { get; }
        public virtual bool Highlighted()
        {
            return false;
        }

        public List<Node> MissingPrerequisiteNodes()
        {
            List<Node> results = new List<Node>();
            var list = InNodes();
            for (int i = 0; i < list.Length; i++)
            {
                var n = list[i];
                if (n is ResearchNode rn)
                {
                    if (! rn.Research.IsFinished) 
                    {
                        results.Add(n);
                        results.AddRange(n.MissingPrerequisiteNodes());
                    }
                }
                else if (n is DummyNode dn)
                {
                    var temp = dn.MissingPrerequisiteNodes();
                    if (temp.Count != 0)
                    {
                        results.Add(dn);
                        results.AddRange(temp);
                    }
                }
            }
            return results;
        }

        protected internal virtual float SetDepth( int min = 1 )
        {
            // calculate desired position
            var isRoot  = InNodes().NullOrEmpty();
            int desired = 1;
            if (!isRoot)
            {
                var list = InNodes();
                for (int i = 0; i < list.Length; i++)
                {
                    var n = list[i];
                    if (n._pos.x > desired) desired = (int)n._pos.x;
                }
                ++desired;
            }
            var depth   = desired > min ? desired : min;

            // update
            X = depth;
            return depth;
        }

        public override string ToString()
        {
            return Label + _pos;
        }

        public void SetRects()
        {
            // origin
            _topLeft = new Vector2(
                ( X  - 1 ) * ( NodeSize.x + NodeMargins.x ),
                ( Yf - 1 ) * ( NodeSize.y + NodeMargins.y ) );

            SetRects( _topLeft );
        }

        public void SetRects( Vector2 topLeft )
        {
            // main rect
            _rect = new Rect( topLeft.x,
                              topLeft.y,
                              NodeSize.x,
                              NodeSize.y );

            // left and right edges
            _left  = new Vector2( _rect.xMin, _rect.yMin + _rect.height / 2f );
            _right = new Vector2( _rect.xMax, _left.y );

            // queue rect
            _queueRect = new Rect( _rect.xMax - QueueLabelSize * 0.6f,
                                   _rect.yMin + ( _rect.height - QueueLabelSize ) / 2f, QueueLabelSize,
                                   QueueLabelSize );

            // label rect
            _labelRect = new Rect( _rect.xMin             + 6f,
                                   _rect.yMin             + 3f,
                                   _rect.width * 2f / 3f  - 6f,
                                   _rect.height * 2f / 3f);

            // research cost rect
            _costLabelRect = new Rect( _rect.xMin                  + _rect.width * 2f / 3f,
                                       _rect.yMin                  + 3f,
                                       _rect.width * 1f / 3f - 16f - 3f,
                                       _rect.height * .5f          - 3f );

            // research icon rect
            _costIconRect = new Rect( _costLabelRect.xMax,
                                      _rect.yMin + ( _costLabelRect.height - 16f ) / 2,
                                      16f,
                                      16f );

            // icon container rect
            _iconsRect = new Rect( _rect.xMin,
                                   _rect.yMin + _rect.height * .5f,
                                   _rect.width,
                                   _rect.height * .5f );

            // lock icon rect
            _lockRect = new Rect( 0f, 0f, 32f, 32f );
            _lockRect = _lockRect.CenteredOnXIn( _rect );
            _lockRect = _lockRect.CenteredOnYIn( _rect );

            // see if the label is too big
            _largeLabel = Text.CalcHeight( Label, _labelRect.width ) > _labelRect.height;

            // done
            _rectsSet = true;
        }

        public virtual bool IsVisible( Rect visibleRect )
        {
            var nodeRect = Rect;
            return !(
            nodeRect.m_YMin > visibleRect.yMin + visibleRect.m_Height || 
            visibleRect.yMin + visibleRect.m_Height < visibleRect.m_YMin ||
            nodeRect.m_XMin > visibleRect.xMin + visibleRect.m_Width || 
            visibleRect.xMin + visibleRect.m_Width < visibleRect.m_XMin);
        }

        public virtual void Draw(Rect visibleRect, Painter painter)
        {
        }

        public int assignedPriority = int.MinValue;

        public virtual int DefaultPriority()
        {
            return int.MaxValue;
        }

        public int LayoutPriority()
        {
            if (assignedPriority != int.MinValue) return assignedPriority;
            return DefaultPriority();
        }

        public int lx;
        public int ly;

        public NodeLayer layer;

        public double doubleCache;
    }
}