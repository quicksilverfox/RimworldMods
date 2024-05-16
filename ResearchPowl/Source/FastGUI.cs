using UnityEngine;

namespace ResearchPowl
{
	internal static class FastGUI
	{
		static Internal_DrawTextureArguments drawArguments = new Internal_DrawTextureArguments
		{
			leftBorder = 0,
			rightBorder = 0,
			topBorder = 0,
			bottomBorder = 0,
			leftBorderColor = Color.white,
			topBorderColor = Color.white,
			rightBorderColor = Color.white,
			bottomBorderColor = Color.white,
			cornerRadiuses = new Vector4(0f, 0f, 0f, 0f),
			smoothCorners = false,
			sourceRect = new Rect(0f, 0f, 1f, 1f),
			mat = GUI.roundedRectMaterial,
			borderWidths = Vector4.zero
		};

		public static void DrawTextureFast(Rect position, Texture image, Color color)
		{
			drawArguments.screenRect = position;
			drawArguments.texture = image;
			drawArguments.color = color;
			Graphics.Internal_DrawTexture(ref drawArguments);
		}

		static Internal_DrawTextureArguments drawArguments2 = new Internal_DrawTextureArguments
		{
			leftBorder = 0,
			rightBorder = 0,
			topBorder = 0,
			bottomBorder = 0,
			leftBorderColor = Color.white,
			topBorderColor = Color.white,
			rightBorderColor = Color.white,
			bottomBorderColor = Color.white,
			cornerRadiuses = new Vector4(0f, 0f, 0f, 0f),
			smoothCorners = false,
			sourceRect = new Rect(0f, 0f, 1f, 1f),
			mat = GUI.roundedRectMaterial,
			borderWidths = Vector4.zero
		};

		public static void DrawTextureFastWithCoords(Rect position, Texture image, Color color, Rect rect)
		{
			drawArguments2.screenRect = position;
			drawArguments2.texture = image;
			drawArguments2.color = color;
			drawArguments2.sourceRect = rect;
			Graphics.Internal_DrawTexture(ref drawArguments2);
		}
	}
}