using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace ResearchPowl
{	
	public static class Highlighting
    {
		public enum Reason {
			Unknown = 0,
			FixedSecondary = 1,
			HoverSecondary = 3,
			HoverPrimary = 4,
			FixedPrimary = 6,
			Focused = 7
		}
		
		public static int Priority(Reason reason)
        {
			return (int) reason;
		}
		public static bool Similar(Reason r1, Reason r2)
        {
			if (r1 > r2)
            {
				Reason temp = r1;
				r1 = r2;
				r2 = temp;
			}
			return r1 == Reason.FixedSecondary && r2 == Reason.FixedPrimary
            || r1 == Reason.HoverSecondary && r2 == Reason.HoverPrimary
            || r1 == r2;
		}
		public static Color Color(Reason reason, TechLevel techLevel)
        {
			if (reason == Reason.FixedSecondary) return Assets.NormalHighlightColor;
			if (reason == Reason.FixedPrimary) return Assets.FixedPrimaryColor;
			if (reason == Reason.HoverSecondary) return Assets.NormalHighlightColor;
			if (reason == Reason.HoverPrimary) return Assets.HoverPrimaryColor;
			if (reason == Reason.Focused) return Assets.FixedPrimaryColor;
			return Assets.NormalHighlightColor;
		}
		public static bool Stackable(Reason r)
        {
			Reason[] stackable = {Reason.FixedSecondary};
			return stackable.Contains(r);
		}
	}
	
	public class HighlightReasonSet
    {
		List<Pair<Highlighting.Reason, int>> _reasons = new List<Pair<Highlighting.Reason, int>>();
		
		public bool Highlighted()
        {
			return _reasons.Any();
		}
		public Highlighting.Reason Current()
        {
			return _reasons.Select(p => p.First).MaxBy(Highlighting.Priority);
		}
		public IEnumerable<Highlighting.Reason> Reasons()
        {
            foreach (var item in _reasons) yield return item.First;
		}
		public bool Highlight(Highlighting.Reason r)
        {
			var length = _reasons.Count;
			for (int i = 0; i < length; ++i)
            {
				var p = _reasons[i];
				if (p.First == r)
                {
					if (!Highlighting.Stackable(r)) return false;
					_reasons[i] = new Pair<Highlighting.Reason, int>(r, p.Second + 1);
					return true;
				}
			}
			_reasons.Add(new Pair<Highlighting.Reason, int>(r, 1));
			return true;
		} 
		public bool Unhighlight(Highlighting.Reason r)
        {
			var length =  _reasons.Count;
			for (int i = 0; i < length; ++i)
            {
				var p = _reasons[i];
				if (p.First == r)
                {
					if (p.Second == 1) _reasons.RemoveAt(i);
					else _reasons[i] = new Pair<Highlighting.Reason, int>(r, p.Second - 1);
					return true;
				}
			}
			return false;
		}
	}
	public class RelatedNodeHighlightSet
    {
		public ResearchNode _causer;
		List<ResearchNode> _relatedNodes;
		Highlighting.Reason _causerReason;
		Highlighting.Reason _relatedReason;
		
		public bool _activated = false;
		
		RelatedNodeHighlightSet(ResearchNode causer, Highlighting.Reason causerR, Highlighting.Reason relatedR)
        {
			_causer = causer;
			_causerReason = causerR;
			_relatedReason = relatedR;
		}
		public static List<ResearchNode> RelatedNodes(ResearchNode node)
        {
            var workingList = RelatedPrerequisites(node);
			foreach (var n in node._outEdges) workingList.Add(n.OutResearch());
            return workingList;
		}
		static List<ResearchNode> RelatedPrerequisites(ResearchNode node)
        {
			
            var list = new List<ResearchNode>(node.DirectPrerequisites());
			var concatList = list.ToList();
            foreach (var item in concatList)
            {
                if (!item.Research.IsFinished) foreach (var item2 in RelatedPrerequisites(item)) list.Add(item2);
            }
			return list;
		}
		public static RelatedNodeHighlightSet HoverOn(ResearchNode node)
        {
			var instance = new RelatedNodeHighlightSet(node, Highlighting.Reason.HoverPrimary, Highlighting.Reason.HoverSecondary);
			instance._relatedNodes = RelatedNodes(node);
			return instance;
		}
		public static RelatedNodeHighlightSet FixHighlight(ResearchNode node)
        {
			var instance = new RelatedNodeHighlightSet(node, Highlighting.Reason.FixedPrimary,Highlighting.Reason.FixedSecondary);
			instance._relatedNodes = RelatedNodes(node);
			return instance;
		}
		public bool ShouldContinue(Vector2 mouse)
		{
			return _causer.MouseOver(mouse);
		}
		public bool Stop()
		{
			if (!_activated) return false;
			_causer.Unhighlight(_causerReason);
			foreach (var n in _relatedNodes) n.Unhighlight(_relatedReason);
			return true;
		}
		public bool Start()
		{
			if (_activated) return false;
			_activated = true;
			_causer.Highlight(_causerReason);
			foreach (var n in _relatedNodes) n.Highlight(_relatedReason);
			return true;
		}
		public bool TryStop(Vector2 mouse)
        {
			if (ShouldContinue(mouse)) return false;
			Stop();
			return true;
		}
	}	
}