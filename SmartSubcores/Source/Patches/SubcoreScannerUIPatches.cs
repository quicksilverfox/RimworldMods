using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Patches the FloatMenu to sort and color-code pawn selection options for subcore scanners.
	/// Uses the same color convention as beds/colonist bar.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class SubcoreScannerUIPatches
	{
		private static readonly FieldInfo LabelField = AccessTools.Field(typeof(FloatMenuOption), "labelInt");

		static SubcoreScannerUIPatches()
		{
			if (!ModsConfig.BiotechActive)
			{
				Log.Message("[SubcoreAutomation] Scanner UI patches skipped - Biotech not active.");
				return;
			}

			try
			{
				var harmony = new Harmony("SubcoreAutomation.SubcoreScannerUI");
				int patchedCount = 0;

				// Patch all FloatMenu constructors that take a List<FloatMenuOption>
				foreach (var ctor in typeof(FloatMenu).GetConstructors())
				{
					var parameters = ctor.GetParameters();
					if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(List<FloatMenuOption>))
					{
						harmony.Patch(ctor,
							prefix: new HarmonyMethod(typeof(SubcoreScannerUIPatches), nameof(FloatMenu_Prefix)));
						patchedCount++;
					}
				}

				Log.Message($"[SubcoreAutomation] Scanner UI sorting patches applied ({patchedCount} constructors).");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreScannerUI] Failed to patch: {ex}");
			}
		}

		public static void FloatMenu_Prefix(List<FloatMenuOption> options)
		{
			try
			{
				if (options == null || options.Count == 0)
					return;

				// Check if feature is enabled in settings
				if (SubcoreAutomationMod.Settings == null)
					return;

				if (!SubcoreAutomationMod.Settings.scannerUISortingEnabled)
					return;

				bool scannerSelected = IsSubcoreScannerSelected();
				if (!scannerSelected)
					return;

				// Count pawn options - must have at least some pawn icons
				int pawnIconCount = 0;
				foreach (var opt in options)
				{
					if (opt?.iconThing is Pawn)
						pawnIconCount++;
				}

				if (pawnIconCount == 0)
					return;

				// Build categorized lists and apply colors
				var colonists = new List<FloatMenuOption>();
				var slaves = new List<FloatMenuOption>();
				var prisoners = new List<FloatMenuOption>();
				var others = new List<FloatMenuOption>();

				foreach (var opt in options)
				{
					if (opt?.iconThing is Pawn pawn)
					{
						// Apply vanilla pawn name color
						ColorizeOption(opt, PawnNameColorUtility.PawnNameColorOf(pawn));

						if (pawn.IsSlave)
							slaves.Add(opt);
						else if (pawn.IsPrisonerOfColony)
							prisoners.Add(opt);
						else if (pawn.IsColonist || pawn.IsColonistPlayerControlled)
							colonists.Add(opt);
						else
							others.Add(opt);
					}
					else
					{
						others.Add(opt);
					}
				}

				// Sort each category alphabetically
				colonists.SortBy(o => o.Label);
				slaves.SortBy(o => o.Label);
				prisoners.SortBy(o => o.Label);
				others.SortBy(o => o.Label);

				// Rebuild list - colonists first, then slaves, then prisoners
				options.Clear();
				int order = 10000;

				foreach (var opt in colonists)
				{
					opt.orderInPriority = order--;
					options.Add(opt);
				}

				foreach (var opt in slaves)
				{
					opt.orderInPriority = order--;
					options.Add(opt);
				}

				foreach (var opt in prisoners)
				{
					opt.orderInPriority = order--;
					options.Add(opt);
				}

				foreach (var opt in others)
				{
					opt.orderInPriority = order--;
					options.Add(opt);
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreScannerUI] Exception: {ex}");
			}
		}

		private static void ColorizeOption(FloatMenuOption opt, Color color)
		{
			string hexColor = ColorUtility.ToHtmlStringRGB(color);
			string currentLabel = opt.Label;
			
			if (LabelField != null)
			{
				LabelField.SetValue(opt, $"<color=#{hexColor}>{currentLabel}</color>");
			}
		}

		private static bool IsSubcoreScannerSelected()
		{
			// Find.Selector -> Find.MapUI casts UIRoot to UIRoot_Play, which throws
			// InvalidCastException when the FloatMenu is opened from the main menu or
			// the Mods config page (UIRoot is UIRoot_Entry). Bail out early there.
			if (Current.ProgramState != ProgramState.Playing)
				return false;

			if (Find.Selector?.SelectedObjects == null)
				return false;

			foreach (var obj in Find.Selector.SelectedObjects)
			{
				if (obj is Building_SubcoreScanner)
					return true;
			}
			return false;
		}
	}
}
