// Constants.cs
// Copyright Karel Kroeze, 2018-2020

using UnityEngine;

namespace ResearchPowl
{
	public static class Constants
	{
		public const double Epsilon = 1e-4;
		public const float DetailedModeZoomLevelCutoff = 1.5f,
			Margin = 6f, SideMargin = 13f,
			QueueLabelSize = 25f,
			SmallQueueLabelSize = 20f,
			AbsoluteMaxZoomLevel = 3f,
            ZoomStep = .05f,
			DraggingClickDelay = 0.25f,
			Faded = 0.025f,
			LessFaded = 0.17f;
		
        public static readonly Vector2 IconSize = new Vector2(18f, 18f), 
			NodeMargins = new Vector2(50f, 10f),
			NodeSize = new Vector2(200f, 58f),
			TechLevelLabelSize = new Vector2( 200f, 30f );
		public static readonly float TopBarHeight = NodeSize.y + 2 * Margin;
	}
}