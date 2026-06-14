using UnityEngine;
using Verse;

namespace SubcoreAutomation.UI
{
	/// <summary>
	/// A Command_Action that can display an overlay icon on top of the main icon.
	/// </summary>
	public class Command_ActionOverlay : Command_Action
	{
		public Texture2D overlayIcon;
		public float overlayScale = 0.5f;
		public Vector2 overlayOffset = new Vector2(0.25f, -0.25f); // Bottom-right by default

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);

			if (overlayIcon != null)
			{
				Rect gizmoRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
				Rect iconRect = gizmoRect.ContractedBy(10f);

				// Calculate overlay position (offset from center)
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
