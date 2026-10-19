// WildcardNode.cs
// A pseudo research node pinned to the top-left corner of the tree ("Random research").
//
// It has no ResearchProjectDef of its own: a fake def would leak into the game's progress dictionary and save files.
// Instead, while it is queued the queue resolves it to a concrete project (Queue.ResolveWildcardTarget), picking a
// random available one. Once that project is finished the queue moves this node to the end and it picks again.

using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Settings = ResearchPowl.ModSettings_ResearchPowl;

namespace ResearchPowl
{
	public class WildcardNode : ResearchNode
	{
		const int TooltipId = 0x5EED;

		public WildcardNode() : base(null)
		{
			_pos = new Vector2(1, 1);
		}

		// Content pack this node stands for: the one it was queued with, otherwise the pack the tree is filtered to.
		string Scope => Queued ? Queue._instance.WildcardPack : MainTabWindow_ResearchTree.FilterPack;
		// Kept short on purpose; the content pack (which can have a long name) is shown on the status line instead.
		public override string Label => ResourceBank.String.RandomResearch;
		public override bool IsFinished => false;
		public override ResearchProjectDef Target => Queue._instance?.ResolveWildcardTarget();
		// Used for the queue tag colour; the wildcard has no tech level of its own, so borrow the player's.
		public override TechLevel TechLevel
		{
			get
			{
				var level = Faction.OfPlayer?.def.techLevel ?? TechLevel.Undefined;
				if (Assets.ColorCompleted.ContainsKey(level)) return level;
				var levels = Tree.RelevantTechLevels();
				return levels.Count > 0 ? levels[levels.Count - 1] : level;
			}
		}
		public override bool GetAvailable(bool ignoreColonyState = false) => !Settings.readOnlyMode;
		public override List<ResearchNode> MissingPrerequisitesInc() => new List<ResearchNode> { this };
		public override List<ResearchNode> MissingPrerequisites() => new List<ResearchNode>();
		public override int Matches(string query) => 0;
		public override int DefaultPriority() => 0;

		bool Queued => Queue._instance != null && Queue._instance._queue.Contains(this);
		bool HeadOfQueue => Queue._instance != null && Queue._instance._queue.Count > 0 && Queue._instance._queue[0] == this;
// The current pick without triggering a new one (drawing must not have side effects).
		ResearchProjectDef CurrentTarget => Queued ? Queue._instance.WildcardTarget : null;

		public override Color Color
		{
			get
			{
				Color color;
				if (Highlighted()) color = Assets.NormalHighlightColor;
				else if (IsUnmatchedInSearch()) Assets.ColorUnmatched.TryGetValue(TechLevel, out color);
				else if (_available) Assets.ColorCompleted.TryGetValue(TechLevel, out color);
				else Assets.ColorUnavailable.TryGetValue(TechLevel, out color);
				return color;
			}
		}

		protected override void HandleIconClicks() { }

		protected override bool RightClick()
		{
			if (PainterIs(Painter.Queue)) MainTabWindow_ResearchTree.Instance.CenterOn(this);
			return true;
		}

		public override bool LeftClick()
		{
			if (!_available) return false;
			var queue = Queue._instance;
			if (!queue._queue.Contains(this))
			{
				if (Event.current.shift)
				{
					queue.Append(this);
					Queue.NewUndoState();
				}
				else if (Event.current.alt)
				{
					queue.Prepend(this);
					Queue.NewUndoState();
				}
				else Queue.ReplaceS(this);
			}
			else Queue.RemoveS(this);
			return true;
		}

		protected override void DrawNode(bool detailedMode, bool mouseOver)
		{
			var target = CurrentTarget;
			if (target != null && target.IsFinished) target = null;

			if (!PainterIs(Painter.Drag))
			{
				Text.WordWrap = true;
				if (!Settings.disableShortcutManual) TooltipHandler.TipRegion(_rect, ShortcutManualTooltip, TooltipId + 1);
				TooltipHandler.TipRegion(_rect, () => Tooltip(target), TooltipId);
			}

			// background, with the progress of the current pick shown the same way regular nodes show theirs
			FastGUI.DrawTextureFast(Rect, mouseOver ? Assets.ButtonActive : Assets.Button, Color);
			if (_available)
			{
				var progressBarRect = Rect.ContractedBy(3f);
				progressBarRect.xMin += (target?.ProgressPercent ?? 0f) * progressBarRect.width;
				FastGUI.DrawTextureFast(progressBarRect, BaseContent.WhiteTex, Assets.ColorAvailable.TryGetValue(TechLevel));
			}

			var textColor = _available && (Highlighted() || !IsUnmatchedInSearch()) ? Assets.colorWhite : Assets.colorGrey;
			GUI.color = textColor;
			Text.wordWrapInt = true;
			if (detailedMode)
			{
				Text.anchorInt = TextAnchor.UpperLeft;
				Text.Font = _largeLabel ? GameFont.Tiny : GameFont.Small;
				Widgets.Label(_labelRect, Label);

				Text.anchorInt = TextAnchor.UpperRight;
				Widgets.Label(CostLabelRect, "?");
				FastGUI.DrawTextureFast(CostIconRect, Assets.ResearchIcon, textColor);

				// lower half: the content pack it is restricted to (if any) and what it currently stands for
				Text.anchorInt = TextAnchor.LowerLeft;
				Text.Font = GameFont.Tiny;
				var statusRect = IconsRect;
				statusRect.xMin += 6f;
				statusRect.xMax -= 6f;
				statusRect.yMin -= 4f;
				statusRect.yMax -= 3f;
				// one line each, truncated to the node: the lower half fits two Tiny lines at most
				var status = Status(target).Truncate(statusRect.width);
				var scope = Scope;
				if (scope != null) status = status.Length == 0 ? scope.Truncate(statusRect.width) : scope.Truncate(statusRect.width) + "\n" + status;
				if (status.Length > 0) Widgets.Label(statusRect, status);
			}
			else
			{
				Text.anchorInt = TextAnchor.MiddleCenter;
				Text.Font = GameFont.Medium;
				Widgets.Label(Rect, Label);
			}

			GUI.color = Assets.colorWhite;
			Text.anchorInt = TextAnchor.UpperLeft;
			Text.WordWrap = true;
		}

		string Status(ResearchProjectDef target)
		{
			if (target != null) return ResourceBank.String.RandomResearchTarget(target.LabelCap, target.ProgressPercent.ToStringPercent());
			if (!Queued) return "";
			// Only the head of the queue resolves to a project; further back it is simply waiting.
			return HeadOfQueue ? ResourceBank.String.RandomResearchNoTarget : ResourceBank.String.RandomResearchWaiting;
}

		string Tooltip(ResearchProjectDef target)
		{
			var text = new StringBuilder();
			text.AppendLine(ResourceBank.String.RandomResearchTip);
			text.AppendLine();
			var scope = Scope;
			if (scope != null) text.AppendLine(ResourceBank.String.RandomResearchScope(scope));
			var status = Status(target);
			if (status.Length > 0) text.Append(status);
			return text.ToString();
		}
	}
}
