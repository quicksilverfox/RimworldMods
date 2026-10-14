using UnityEngine;

namespace ResearchPowl
{
	// RimWorld 1.7 replaced Unity IMGUI with its own stub layer. There, Graphics.DrawTexture with a null
	// material goes straight to the GUI backend (Graphics.Internal_DrawTexture is just a wrapper around it),
	// so the old publicized Internal_DrawTextureArguments trick is no longer needed or faster.
	internal static class FastGUI
	{
		static readonly Rect fullTexture = new Rect(0f, 0f, 1f, 1f);

		public static void DrawTextureFast(Rect position, Texture image, Color color)
		{
			Graphics.DrawTexture(position, image, fullTexture, 0, 0, 0, 0, color);
		}

		public static void DrawTextureFastWithCoords(Rect position, Texture image, Color color, Rect rect)
		{
			Graphics.DrawTexture(position, image, rect, 0, 0, 0, 0, color);
		}
	}
}
