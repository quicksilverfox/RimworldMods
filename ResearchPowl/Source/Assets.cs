// Assets.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPowl
{
	[StaticConstructorOnStartup]
	public static class Assets
	{
		public static Texture2D 
		Button = ContentFinder<Texture2D>.Get( "Buttons/button" ),
		ButtonActive = ContentFinder<Texture2D>.Get( "Buttons/button-active" ),
		ResearchIcon = ContentFinder<Texture2D>.Get( "Icons/Research" ),
		MoreIcon = ContentFinder<Texture2D>.Get( "Icons/more" ),
		Lock = ContentFinder<Texture2D>.Get( "Icons/padlock" ),
		CircleFill = ContentFinder<Texture2D>.Get( "Icons/circle-fill" ),
		SlightlyDarkBackground = SolidColorMaterials.NewSolidColorTexture( 0f, 0f, 0f, .1f ),
		closeXSmall = ContentFinder<Texture2D>.Get("UI/Widgets/CloseXSmall"),
		LineCircle = ContentFinder<Texture2D>.Get( "Lines/Outline/circle" ),
		LineEnd = ContentFinder<Texture2D>.Get( "Lines/Outline/end" ),
		LineEW = ContentFinder<Texture2D>.Get( "Lines/Outline/ew" ),
		LineNS = ContentFinder<Texture2D>.Get( "Lines/Outline/ns" );
		
		public static Color 
		NormalHighlightColor = GenUI.MouseoverColor,
		HoverPrimaryColor = new Color(0.6f, 0.55f, 0.9f),
		FixedPrimaryColor = new Color(0.55f, 0.9f, 0.95f),
		TechLevelColor = new Color( 1f, 1f, 1f, .2f ),
		colorWhite = Color.white,
		colorGrey = Color.grey,
		colorCyan = Color.cyan,
		colorGreen = Color.green,
		darkGrey = new Color(0.3f, 0.3f, 0.3f, 0.8f);

		public static Dictionary<TechLevel, Color>
		ColorCompleted = new Dictionary<TechLevel, Color>(),
		ColorEdgeCompleted = new Dictionary<TechLevel, Color>(),
		ColorAvailable = new Dictionary<TechLevel, Color>(),
		ColorUnavailable = new Dictionary<TechLevel, Color>(),
		ColorUnmatched = new Dictionary<TechLevel, Color>(),
		ColorMatched = new Dictionary<TechLevel, Color>();

		static Assets()
		{
			var techlevels = Tree.RelevantTechLevels();
			var n = techlevels.Count;
			for ( var i = 0; i < n; i++ )
			{
				ColorCompleted[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .75f, .75f );
				ColorEdgeCompleted[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .5f, .6f );
				ColorMatched[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .4f, .45f );
				ColorAvailable[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .33f, .33f );
				ColorUnavailable[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .125f, .33f);
				ColorUnmatched[techlevels[i]] = Color.HSVToRGB( 1f / n * i, .17f, .17f);
			}
		}

		[DefOf]
        public static class MainButtonDefOf
        {
            public static MainButtonDef ResearchOriginal;
        }
	}
}