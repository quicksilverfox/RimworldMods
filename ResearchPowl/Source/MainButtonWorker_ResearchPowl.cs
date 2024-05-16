// Copyright Karel Kroeze, 2018-2020

using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPowl
{
    public class MainButtonWorker_ResearchTree : MainButtonWorker_ToggleResearchTab
    {
        public override void DoButton( Rect rect )
        {
            base.DoButton( rect );

            var numQueued = Queue._instance._queue.Count - 1;
            if ( numQueued > 0 )
            {
                var queueRect = new Rect(rect.xMax - Constants.SmallQueueLabelSize - Constants.Margin, rect.m_YMin + (rect.m_Height - Constants.SmallQueueLabelSize) / 2f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize);
                Queue.DrawLabel( queueRect, Assets.colorWhite, Assets.colorGrey, numQueued);
            }
        }

        public override void Activate()
        {
            if (Event.current.shift) Verse.Find.MainTabsRoot.ToggleTab(Assets.MainButtonDefOf.ResearchOriginal, true);
            else Verse.Find.MainTabsRoot.ToggleTab(this.def, true);
        }
    }
}