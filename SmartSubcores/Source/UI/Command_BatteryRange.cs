using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.UI
{
	/// <summary>
	/// Custom gizmo for setting backup power battery thresholds.
	/// Displays sliders for min/max battery levels.
	/// Based on Fluffy's BackupPower mod (MIT license).
	/// </summary>
	[StaticConstructorOnStartup]
	public class Command_BatteryRange : Command
	{
		private readonly CompPowerAutomation _comp;

		// Colors for UI elements
		private static readonly Color BlueColor = GenUI.MouseoverColor;
		private static readonly Color GreenColor = new Color(0.3725f, 0.8588f, 0.6549f);
		private static readonly Color OrangeColor = new Color(0.9f, 0.6f, 0.1f);
		private static readonly Color RedColor = new Color(0.6667f, 0.2157f, 0.2275f);
		private static readonly Color WhiteAlpha = new Color(1f, 1f, 1f, 0.3f);

		private static readonly Texture2D BatteryIcon;

		static Command_BatteryRange()
		{
			BatteryIcon = ContentFinder<Texture2D>.Get("UI/BackupPower/Battery", false);
			if (BatteryIcon == null)
			{
				// Fallback to vanilla battery icon
				BatteryIcon = DefDatabase<ThingDef>.GetNamed("Battery", false)?.uiIcon;
			}
		}

		public Command_BatteryRange(CompPowerAutomation comp)
		{
			_comp = comp;
		}

		public override string Label => "SubcoreAutomation_BackupPower_CommandLabel".Translate();

		public override string Desc => GetStatusString();

		private string GetStatusString()
		{
			string status = "SubcoreAutomation_BackupPower_Status".Translate(
				GetStatusLabel(_comp.GeneratorStatus).Colorize(GetStatusColor(_comp.GeneratorStatus)));
			
			float storageLevel = GetStorageLevel();
			string storage = "SubcoreAutomation_BackupPower_Storage".Translate(
				storageLevel.ToStringPercent().Colorize(Color.white));
			
			string turnOn = "SubcoreAutomation_BackupPower_TurnsOnBelow".Translate(
				_comp.BackupPowerBatteryMin.ToStringPercent().Colorize(GreenColor));
			
			string turnOff = "SubcoreAutomation_BackupPower_TurnsOffAbove".Translate(
				_comp.BackupPowerBatteryMax.ToStringPercent().Colorize(RedColor));

			return $"{status}\n{storage.Colorize(Color.grey)}\n{turnOn.Colorize(Color.grey)}\n{turnOff.Colorize(Color.grey)}";
		}

		private string GetStatusLabel(BackupPowerStatus status)
		{
			return ("SubcoreAutomation_Status" + status).Translate();
		}

		private Color GetStatusColor(BackupPowerStatus status)
		{
			return status switch
			{
				BackupPowerStatus.Standby => BlueColor,
				BackupPowerStatus.Running => GreenColor,
				BackupPowerStatus.NoFuel => OrangeColor,
				BackupPowerStatus.Error => RedColor,
				_ => Color.white
			};
		}

		private float GetStorageLevel()
		{
			var powerNet = _comp.PowerNet;
			if (powerNet?.batteryComps == null || !powerNet.batteryComps.Any())
				return 0f;

			float current = 0f;
			float max = 0f;

			foreach (var battery in powerNet.batteryComps)
			{
				current += battery.StoredEnergy;
				max += battery.Props.storedEnergyMax;
			}

			return max > 0 ? current / max : 0f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			float width = GetWidth(maxWidth);
			Rect canvas = new Rect(topLeft, new Vector2(width, Height + 10));
			bool mouseOver = Mouse.IsOver(canvas);

			Find.WindowStack.ImmediateWindow(246685 + _comp.parent.thingIDNumber, canvas, WindowLayer.GameUI, () =>
			{
				canvas = canvas.AtZero();
				Rect buttonRect = canvas.AtZero().TopPartPixels(Height);

				// Draw background
				GUI.color = mouseOver ? BlueColor : Color.white;
				Widgets.DrawAtlas(buttonRect, BGTexture);
				GUI.color = Color.white;

				// Tooltip
				TooltipHandler.TipRegion(buttonRect, () => Desc, 2338712 + _comp.parent.thingIDNumber);

				// Right-click menu for copying settings
				if (Mouse.IsOver(buttonRect) && Input.GetMouseButtonDown(1))
				{
					ShowCopyMenu();
				}

				Rect innerRect = buttonRect.ContractedBy(6);

				// Min slider (left side)
				Rect minSliderRect = innerRect.LeftPart(0.2f);
				float newMin = GUI.VerticalSlider(minSliderRect, _comp.BackupPowerBatteryMin, 1f, 0f);

				// Max slider (right side)
				Rect maxSliderRect = innerRect.RightPart(0.2f);
				float newMax = GUI.VerticalSlider(maxSliderRect, _comp.BackupPowerBatteryMax, 1f, 0f);

				// Enforce min < max
				if (Mathf.Abs(newMin - _comp.BackupPowerBatteryMin) > 0.001f)
				{
					_comp.BackupPowerBatteryMin = newMin;
					_comp.BackupPowerBatteryMax = Mathf.Max(_comp.BackupPowerBatteryMin, _comp.BackupPowerBatteryMax);
				}
				else if (Mathf.Abs(newMax - _comp.BackupPowerBatteryMax) > 0.001f)
				{
					_comp.BackupPowerBatteryMax = newMax;
					_comp.BackupPowerBatteryMin = Mathf.Min(_comp.BackupPowerBatteryMin, _comp.BackupPowerBatteryMax);
				}

				// Battery icon in center
				if (BatteryIcon != null)
				{
					GUI.color = WhiteAlpha;
					Rect batteryRect = new Rect(
						innerRect.x + innerRect.width * 0.35f,
						innerRect.y + innerRect.height * 0.1f,
						innerRect.width * 0.3f,
						innerRect.height * 0.8f);
					GUI.DrawTexture(batteryRect, BatteryIcon);

					// Draw battery fill level
					var powerNet = _comp.PowerNet;
					if (powerNet?.batteryComps?.Any() ?? false)
					{
						float pct = GetStorageLevel();
						GUI.color = BlueColor;
						Rect fillRect = new Rect(
							batteryRect.x,
							batteryRect.yMax - batteryRect.height * pct,
							batteryRect.width,
							batteryRect.height * pct);
						GUI.DrawTextureWithTexCoords(fillRect, BatteryIcon, new Rect(0, 0, 1, pct));
					}

					// Draw threshold lines
					float minY = batteryRect.yMin + batteryRect.height * (1 - _comp.BackupPowerBatteryMin);
					float maxY = batteryRect.yMin + batteryRect.height * (1 - _comp.BackupPowerBatteryMax);

					DrawDashedLine(new Vector2(batteryRect.xMin - 5, minY),
						new Vector2(batteryRect.xMin + batteryRect.width * 0.66f, minY),
						GreenColor, 2);
					DrawDashedLine(new Vector2(batteryRect.xMax + 5, maxY),
						new Vector2(batteryRect.xMin + batteryRect.width * 0.33f, maxY),
						RedColor, 2);
				}

				GUI.color = Color.white;

				// Draw label
				string label = LabelCap;
				if (!label.NullOrEmpty())
				{
					Text.Font = GameFont.Tiny;
					float height = Text.CalcHeight(label, canvas.width);
					Rect labelRect = new Rect(canvas.x, buttonRect.yMax - height + 12f, canvas.width, height);
					GUI.DrawTexture(labelRect, TexUI.GrayTextBG);
					Text.Anchor = TextAnchor.UpperCenter;
					Widgets.Label(labelRect, label);
					Text.Font = GameFont.Small;
					Text.Anchor = TextAnchor.UpperLeft;
				}
			}, false);

			return mouseOver
				? new GizmoResult(GizmoState.Mouseover)
				: new GizmoResult(GizmoState.Clear);
		}

		private void ShowCopyMenu()
		{
			var options = new List<FloatMenuOption>
			{
				new FloatMenuOption("SubcoreAutomation_BackupPower_CopyTo_Room".Translate(), CopyToRoom),
				new FloatMenuOption("SubcoreAutomation_BackupPower_CopyTo_Connected".Translate(), CopyToConnected),
				new FloatMenuOption("SubcoreAutomation_BackupPower_CopyTo_All".Translate(), CopyToAll)
			};

			Find.WindowStack.Add(new FloatMenu(options, "SubcoreAutomation_BackupPower_CopyTo".Translate()));
		}

		private void CopyTo(IEnumerable<CompPowerAutomation> targets)
		{
			foreach (var target in targets.Where(t => t != _comp && t.IsGenerator && t.HasSubcoreInstalled))
			{
				_comp.CopyBackupPowerSettingsTo(target);
			}
		}

		private void CopyToRoom()
		{
			var room = _comp.parent.GetRoom();
			if (room == null) return;

			var targets = room.Cells
				.SelectMany(c => c.GetThingList(_comp.parent.Map))
				.Where(t => t is Building && t.Faction == Faction.OfPlayer)
				.Select(t => t.TryGetComp<CompPowerAutomation>())
				.Where(c => c != null)
				.Distinct();

			CopyTo(targets);
		}

		private void CopyToConnected()
		{
			var powerNet = _comp.PowerNet;
			if (powerNet == null) return;

			var targets = powerNet.powerComps
				.Select(cp => cp.parent.TryGetComp<CompPowerAutomation>())
				.Where(c => c != null);

			CopyTo(targets);
		}

		private void CopyToAll()
		{
			var targets = _comp.parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
				.Where(t => t.Faction == Faction.OfPlayer)
				.Select(t => t.TryGetComp<CompPowerAutomation>())
				.Where(c => c != null);

			CopyTo(targets);
		}

		private void DrawDashedLine(Vector2 start, Vector2 end, Color color, float size = 1f, float stroke = 5f, float dash = 3f)
		{
			float partLength = dash + stroke;
			float totalLength = (end - start).magnitude;
			Vector2 direction = (end - start).normalized;
			float done = 0f;

			while (done < totalLength)
			{
				Vector2 lineStart = start + done * direction;
				Vector2 lineEnd = start + Mathf.Min(done + stroke, totalLength) * direction;
				Widgets.DrawLine(lineStart, lineEnd, color, size);
				done += partLength;
			}
		}
	}
}
