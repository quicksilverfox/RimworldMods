using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Moisture Pump automation.
	/// When automated, the pump works on cells that actually need pumping (like pollution pump)
	/// and auto-deconstructs when finished.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class MoisturePumpPatches
	{
		private static FieldInfo _progressTicksField;

		// Interval between pump actions (in ticks)
		// 145 tiles in 60 days = 60 * 60000 / 145 = 24828 ticks per tile
		private const int PumpIntervalTicks = 24828;

		/// <summary>
		/// Checks if a flickable is effectively on (current state or desired state is on).
		/// </summary>
		private static bool IsFlickableEffectivelyOn(CompFlickable flickable)
		{
			if (flickable == null)
				return true;

			if (!flickable.WantsFlick())
				return flickable.SwitchIsOn;

			if (SubcoreAutomationUtils.FlickableWantSwitchOnField != null)
			{
				object wantOn = SubcoreAutomationUtils.FlickableWantSwitchOnField.GetValue(flickable);
				if (wantOn != null)
					return (bool)wantOn;
			}

			return !flickable.SwitchIsOn;
		}

		static MoisturePumpPatches()
		{
			try
			{
				var harmony = new Harmony("SubcoreAutomation.MoisturePumpPatches");

				// Cache reflection for progressTicks field in CompTerrainPump
				_progressTicksField = AccessTools.Field(typeof(CompTerrainPump), "progressTicks");

				// Patch CompTerrainPumpDry.CompTickRare (this is what moisture pump uses)
				var compTickRare = AccessTools.Method(typeof(CompTerrainPumpDry), "CompTickRare");
				if (compTickRare != null)
					harmony.Patch(compTickRare, prefix: new HarmonyMethod(typeof(MoisturePumpPatches), nameof(CompTickRare_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Moisture Pump patches BROKEN: CompTerrainPumpDry.CompTickRare not found!");

				// Patch CompTerrainPump.CompInspectStringExtra to suppress vanilla text for automated pumps
				var inspectStringExtra = AccessTools.Method(typeof(CompTerrainPump), "CompInspectStringExtra");
				if (inspectStringExtra != null)
					harmony.Patch(inspectStringExtra, prefix: new HarmonyMethod(typeof(MoisturePumpPatches), nameof(CompInspectStringExtra_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Moisture Pump patches BROKEN: CompTerrainPump.CompInspectStringExtra not found!");

				// Patch CompTerrainPump.PostDrawExtraSelectionOverlays to show full radius instead of expanding
				var postDrawOverlays = AccessTools.Method(typeof(CompTerrainPump), "PostDrawExtraSelectionOverlays");
				if (postDrawOverlays != null)
					harmony.Patch(postDrawOverlays, prefix: new HarmonyMethod(typeof(MoisturePumpPatches), nameof(PostDrawExtraSelectionOverlays_Prefix)));
				else
					Log.Error("[SubcoreAutomation] Moisture Pump patches BROKEN: CompTerrainPump.PostDrawExtraSelectionOverlays not found!");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply moisture pump patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if a cell's terrain can be dried (has driesTo property).
		/// </summary>
		private static bool CanDryCell(Map map, IntVec3 c)
		{
			if (!c.InBounds(map))
				return false;

			TerrainDef terrain = map.terrainGrid.TopTerrainAt(c);
			if (terrain?.driesTo != null)
				return true;

			// Also check under terrain
			TerrainDef underTerrain = map.terrainGrid.UnderTerrainAt(c);
			if (underTerrain?.driesTo != null)
				return true;

			return false;
		}

		/// <summary>
		/// Get the cell to dry (nearest one that needs drying).
		/// </summary>
		private static IntVec3 GetCellToDry(CompTerrainPumpDry pumper)
		{
			Map map = pumper.parent.Map;
			if (map == null)
				return IntVec3.Invalid;

			IntVec3 position = pumper.parent.Position;
			CompProperties_TerrainPump props = (CompProperties_TerrainPump)pumper.props;
			float radius = props.radius;

			int numCells = GenRadial.NumCellsInRadius(radius);
			for (int i = 0; i < numCells; i++)
			{
				IntVec3 cell = position + GenRadial.RadialPattern[i];
				if (CanDryCell(map, cell))
				{
					return cell;
				}
			}

			return IntVec3.Invalid;
		}

		/// <summary>
		/// Prefix for CompTerrainPumpDry.CompTickRare - smart pumping for automated pumps.
		/// </summary>
		public static bool CompTickRare_Prefix(CompTerrainPumpDry __instance)
		{
			try
			{
				// Check if this pump is automated
				var automationComp = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
					return true; // Let vanilla handle it

				// Check power and flickable state
				CompPowerTrader power = __instance.parent.TryGetComp<CompPowerTrader>();
				if (power != null && !power.PowerOn)
					return true; // Let vanilla handle power-off state

				// Also check flickable explicitly - if user wants it off, let vanilla handle
				CompFlickable flickable = __instance.parent.TryGetComp<CompFlickable>();
				if (flickable != null && !IsFlickableEffectivelyOn(flickable))
					return true;

				// Smart pumping logic
				DoSmartPumping(__instance, automationComp);

				return false; // Skip vanilla
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in moisture pump CompTickRare_Prefix: {ex.Message}", 93827480);
				return true;
			}
		}

		/// <summary>
		/// Prefix for CompTerrainPump.CompInspectStringExtra - suppress vanilla inspect text for automated pumps.
		/// Our CompSubcoreAutomation provides its own inspect string.
		/// </summary>
		public static bool CompInspectStringExtra_Prefix(CompTerrainPump __instance, ref string __result)
		{
			try
			{
				var automationComp = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp != null && automationComp.SubcoreInstalled && automationComp.IsAutomationEnabled)
				{
					// Return empty string - our CompSubcoreAutomation handles the inspect string
					__result = null;
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in CompInspectStringExtra_Prefix: {ex.Message}", 93827481);
			}
			return true;
		}

		/// <summary>
		/// Prefix for CompTerrainPump.PostDrawExtraSelectionOverlays - show full working radius for automated pumps.
		/// </summary>
		public static bool PostDrawExtraSelectionOverlays_Prefix(CompTerrainPump __instance)
		{
			try
			{
				var automationComp = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
				if (automationComp != null && automationComp.SubcoreInstalled && automationComp.IsAutomationEnabled)
				{
					// Draw full radius ring instead of expanding radius
					CompProperties_TerrainPump props = (CompProperties_TerrainPump)__instance.props;
					GenDraw.DrawRadiusRing(__instance.parent.Position, props.radius);
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in PostDrawExtraSelectionOverlays_Prefix: {ex.Message}", 93827482);
			}
			return true;
		}

		/// <summary>
		/// Smart pumping - find and pump cells that actually need it.
		/// </summary>
		private static void DoSmartPumping(CompTerrainPumpDry pumper, CompSubcoreAutomationBase automationComp)
		{
			Map map = pumper.parent.Map;
			if (map == null)
				return;

			// Find a cell that needs drying
			IntVec3 cellToDry = GetCellToDry(pumper);

			// If no cells need drying, auto-deconstruct/uninstall
			if (!cellToDry.IsValid)
			{
				AutoDeconstructOrUninstall(pumper.parent);
				return;
			}

			// Progress the pump using the stored ticks
			if (_progressTicksField == null)
				return;

			int progressTicks = _progressTicksField.GetValue(pumper) is int pt ? pt : 0;
			
			// Apply speed multiplier from settings
			float speedMultiplier = Core.SubcoreAutomationMod.Settings.moisturePumpSpeedMultiplier;
			progressTicks += (int)(250 * speedMultiplier); // CompTickRare runs every 250 ticks

			// Check if we should pump a cell
			if (progressTicks >= PumpIntervalTicks)
			{
				progressTicks = 0;

				// Dry the cell using vanilla logic
				CompTerrainPumpDry.AffectCell(map, cellToDry);

				// Visual effect
				FleckMaker.ThrowDustPuffThick(cellToDry.ToVector3Shifted(), map, 1.5f, new Color(0.6f, 0.55f, 0.45f));
			}

			_progressTicksField.SetValue(pumper, progressTicks);
		}

		/// <summary>
		/// Auto-deconstruct or uninstall the pump when work is done.
		/// </summary>
		private static void AutoDeconstructOrUninstall(ThingWithComps pump)
		{
			if (pump?.Map == null || pump.Destroyed)
				return;

			// Check if already designated
			if (pump.Map.designationManager.DesignationOn(pump, DesignationDefOf.Deconstruct) != null)
				return;
			if (pump.Map.designationManager.DesignationOn(pump, DesignationDefOf.Uninstall) != null)
				return;

			// Prefer uninstall if minifiable
			if (pump.def.Minifiable)
			{
				pump.Map.designationManager.AddDesignation(new Designation(pump, DesignationDefOf.Uninstall));
				Messages.Message(
					"SubcoreAutomation_PumpFinishedUninstall".Translate(pump.LabelCapNoCount),
					pump,
					MessageTypeDefOf.TaskCompletion);
			}
			else
			{
				pump.Map.designationManager.AddDesignation(new Designation(pump, DesignationDefOf.Deconstruct));
				Messages.Message(
					"SubcoreAutomation_PumpFinishedDeconstruct".Translate(pump.LabelCapNoCount),
					pump,
					MessageTypeDefOf.TaskCompletion);
			}
		}
	}
}