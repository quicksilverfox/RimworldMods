using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for Deep Drill automation.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class DrillHandler
	{
		// Per-instance state for drill caching (keyed by comp instance)
		private static readonly Dictionary<IProductionAutomation, DrillState> _drillStates = new Dictionary<IProductionAutomation, DrillState>();

		private class DrillState
		{
			public int LastCanDrillCheckTick;
			public bool CachedCanDrill;
			public Effecter DrillEffecter;
			/// <summary>Whether we've ever seen valuable resources at this drill location.</summary>
			public bool HasSeenValuableResources;
			/// <summary>Whether we've already shut down once for resource exhaustion.</summary>
			public bool HasShutDownForExhaustion;
		}

		// Cached reflection for CompDeepDrill
		private static readonly FieldInfo DrillPortionProgress;
		private static readonly FieldInfo DrillPortionYieldPct;
		private static readonly FieldInfo DrillLastUsedTick;
		private static readonly MethodInfo DrillTryProducePortion;
		private static readonly EffecterDef DrillEffecterDef;



		// Constants
		private const int DrillCanDrillCheckInterval = 60; // Check CanDrillNow every 60 ticks (1 second)
		private const int DrillSyncInterval = 30; // Sync to vanilla fields every 30 ticks
		private const float ProgressPerTick = 0.000225f; // ~74 minutes per resource at 100% speed (1.5x vanilla speed)
		private const int WorkIntervalTicks = 60;

		static DrillHandler()
		{
			// CompDeepDrill fields/methods
			DrillPortionProgress = AccessTools.Field(typeof(CompDeepDrill), "portionProgress");
			DrillPortionYieldPct = AccessTools.Field(typeof(CompDeepDrill), "portionYieldPct");
			DrillLastUsedTick = AccessTools.Field(typeof(CompDeepDrill), "lastUsedTick");
			DrillTryProducePortion = AccessTools.Method(typeof(CompDeepDrill), "TryProducePortion");

			// Try to get the Drill effecter def (used by vanilla job driver)
			DrillEffecterDef = DefDatabase<EffecterDef>.GetNamed("Drill", errorOnFail: false);


		}

		/// <summary>
		/// Process one tick of automated drilling.
		/// </summary>
		public static void TryAutomateDrillTick(IProductionAutomation comp)
		{
			CompDeepDrill drill = comp.DeepDrill;
			if (drill == null)
				return;

			if (DrillPortionProgress == null || DrillPortionYieldPct == null || DrillLastUsedTick == null)
				return;

			// Get or create per-instance state
			if (!_drillStates.TryGetValue(comp, out var state))
			{
				state = new DrillState();
				_drillStates[comp] = state;
			}

			// OPTIMIZATION: Only check resources every DrillCanDrillCheckInterval ticks
			// ValuableResourcesPresent scans up to 21 cells in the deep resource grid - expensive!
			int currentTick = Find.TickManager.TicksGame;
			if (currentTick - state.LastCanDrillCheckTick >= DrillCanDrillCheckInterval)
			{
				state.LastCanDrillCheckTick = currentTick;
				state.CachedCanDrill = drill.ValuableResourcesPresent();
				
				// Track if we've ever seen valuable resources at this location
				if (state.CachedCanDrill)
				{
					state.HasSeenValuableResources = true;
					// Reset exhaustion flag when valuable resources are present again
					// (e.g., drill was moved or new deposit discovered)
					state.HasShutDownForExhaustion = false;
				}
			}

			// Handle resource exhaustion: only shut down ONCE when valuable resources run out
			// If there were never any valuable resources, or if we've already shut down once,
			// just continue drilling for chunks
			if (!state.CachedCanDrill && state.HasSeenValuableResources && !state.HasShutDownForExhaustion)
			{
				// Valuable deposits just exhausted - turn off the drill once and mark it
				state.HasShutDownForExhaustion = true;
				comp.TryTurnOffDrill();
				return;
			}

			// Also check power - if power is off, don't drill
			if (!drill.CanDrillNow())
				return;

			try
			{
				// Progress per tick based on level 10 miner baseline
				float progressPerTick = ProgressPerTick * comp.EffectiveSpeedFactor;
				comp.DrillProgress += progressPerTick;

				// IMPORTANT: Set lastUsedTick EVERY tick for Re-Powered compatibility
				// Re-Powered checks UsedLastTick() to determine if drill is operating
				DrillLastUsedTick.SetValue(drill, currentTick);

				// Only sync progress/yield to vanilla fields periodically (for inspect string display)
				if ((currentTick % DrillSyncInterval) == 0)
				{
					DrillPortionProgress.SetValue(drill, comp.DrillProgress);
					DrillPortionYieldPct.SetValue(drill, 1f);
				}

				// Spawn drilling effects periodically (not every tick)
				if (comp.Parent.IsHashIntervalTick(WorkIntervalTicks))
				{
					SpawnDrillingMotes(comp, state);
				}

				if (comp.DrillProgress >= 1f)
				{
					// Yield resources
					if (DrillTryProducePortion != null)
					{
						var parameters = DrillTryProducePortion.GetParameters();
						if (parameters.Length == 0)
						{
							DrillTryProducePortion.Invoke(drill, null);
						}
						else if (parameters.Length == 1)
						{
							DrillTryProducePortion.Invoke(drill, new object[] { 1f });
						}
						else
						{
							// (float yieldPct, Pawn driller) - pass null for driller
							DrillTryProducePortion.Invoke(drill, new object[] { 1f, null });
						}
					}
					comp.DrillProgress = 0f;
					DrillPortionProgress.SetValue(drill, 0f);
					DrillPortionYieldPct.SetValue(drill, 0f);
					
					// Re-check resources immediately after producing (deposit may be depleted)
					state.LastCanDrillCheckTick = 0;
					state.CachedCanDrill = drill.ValuableResourcesPresent();

					// If valuable deposits just exhausted, turn off the drill (only once)
					if (!state.CachedCanDrill && state.HasSeenValuableResources && !state.HasShutDownForExhaustion)
					{
						state.HasShutDownForExhaustion = true;
						comp.TryTurnOffDrill();
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"SubcoreAutomation: Error during automated drill operation: {ex.Message}", 48392717);
			}
		}

		private static void SpawnDrillingMotes(IProductionAutomation comp, DrillState state)
		{
			if (comp.Parent.Map == null)
				return;

			// Use the vanilla Drill effecter if available
			if (DrillEffecterDef != null)
			{
				if (state.DrillEffecter == null)
				{
					state.DrillEffecter = DrillEffecterDef.Spawn();
				}
				state.DrillEffecter.EffectTick(comp.Parent, comp.Parent);
			}
			else
			{
				// Fallback: simple dust effect
				FleckMaker.ThrowDustPuffThick(comp.Parent.Position.ToVector3Shifted(), comp.Parent.Map, 1.5f, new Color(0.55f, 0.45f, 0.35f));
			}
		}

		/// <summary>
		/// Clean up drill state when comp is destroyed.
		/// </summary>
		public static void Cleanup(IProductionAutomation comp)
		{
			if (_drillStates.TryGetValue(comp, out var state))
			{
				if (state.DrillEffecter != null)
				{
					state.DrillEffecter.Cleanup();
					state.DrillEffecter = null;
				}
				_drillStates.Remove(comp);
			}
		}

	}
}
