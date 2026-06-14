using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.UI
{
	/// <summary>
	/// A Command_Action that can either display a small badge over the main icon
	/// (overlayIcon, e.g. a Cancel/Drop indicator), or tile several co-equal icons
	/// across the icon area at the same size (tiledIcons, e.g. the component types a
	/// fallback tier costs). The two modes are mutually exclusive.
	/// </summary>
	public class Command_ActionOverlay : Command_Action
	{
		// Badge mode: a small icon drawn over the bottom-right of the main icon.
		public Texture2D overlayIcon;
		public float overlayScale = 0.5f;
		public Vector2 overlayOffset = new Vector2(0.25f, -0.25f); // Bottom-right by default

		// Tiled mode: co-equal icons drawn overlapping (fanned down-right), all the same
		// size. When set, these replace the single main icon.
		public List<Texture2D> tiledIcons;
		public float tiledOverlap = 0.5f; // fraction of icon size each successive icon is shifted

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			bool tiling = tiledIcons != null && tiledIcons.Count > 0;

			// Suppress the base single icon so it doesn't draw under the tiled row.
			if (tiling)
				icon = BaseContent.ClearTex;

			GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);

			Rect gizmoRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
			Rect iconRect = gizmoRect.ContractedBy(10f);

			if (tiling)
			{
				int n = tiledIcons.Count;
				// Each icon stays large; successive icons are shifted by a fraction so they
				// partially overlap and fan toward the bottom-right.
				float size = Mathf.Min(iconRect.width, iconRect.height) * (n > 1 ? 0.74f : 1f);
				float stepX = size * tiledOverlap;
				float stepY = size * (tiledOverlap * 0.28f);
				float groupW = size + stepX * (n - 1);
				float groupH = size + stepY * (n - 1);
				float startX = iconRect.x + (iconRect.width - groupW) * 0.5f;
				float startY = iconRect.y + (iconRect.height - groupH) * 0.5f;
				for (int i = 0; i < n; i++)
				{
					if (tiledIcons[i] == null)
						continue;
					GUI.DrawTexture(new Rect(startX + stepX * i, startY + stepY * i, size, size), tiledIcons[i]);
				}
			}
			else if (overlayIcon != null)
			{
				float overlaySize = iconRect.width * overlayScale;
				Rect overlayRect = new Rect(
					iconRect.x + iconRect.width * (0.5f + overlayOffset.x) - overlaySize * 0.5f,
					iconRect.y + iconRect.height * (0.5f - overlayOffset.y) - overlaySize * 0.5f,
					overlaySize,
					overlaySize
				);

				GUI.DrawTexture(overlayRect, overlayIcon);
			}

			return result;
		}
	}
}
