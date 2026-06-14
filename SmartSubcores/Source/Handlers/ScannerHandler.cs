using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for Ground-Penetrating Scanner automation.
	/// </summary>
	public static class ScannerHandler
	{
		#region Mountain Scanning State

		// Per-instance state for mountain scanning (keyed by comp instance)
		private static readonly Dictionary<CompScannerAutomation, MountainScanState> _mountainStates = 
			new Dictionary<CompScannerAutomation, MountainScanState>();

		private class MountainScanState
		{
			public List<IntVec3> CellsToScan;
			public int ScanIndex;
			public IntVec3 CachePosition;
		}

		// Constants
		private const int MountainTilesPerInterval = 5;

		#endregion

		#region Mountain Scanning Methods

		/// <summary>
		/// Performs one tick of mountain scanning automation.
		/// </summary>
		public static bool TryMountainScan(CompScannerAutomation comp, float speedFactor)
		{
			Map map = comp.parent.Map;
			if (map == null) return false;

			// Require power
			CompPowerTrader power = comp.PowerTrader;
			if (power != null && !power.PowerOn) return false;

			// Get or create per-instance state
			if (!_mountainStates.TryGetValue(comp, out var state))
			{
				state = new MountainScanState();
				_mountainStates[comp] = state;
			}

			try
			{
				// Build cache if needed, or rebuild if scanner moved (mods can minify buildings)
				if (state.CellsToScan == null || comp.parent.Position != state.CachePosition)
					BuildMountainCellCache(comp, map, state);

				// Already done
				if (state.ScanIndex >= state.CellsToScan.Count)
					return true;

				// Process tiles based on speed factor
				int tilesToProcess = Mathf.Max(1, (int)(MountainTilesPerInterval * speedFactor));
				int tilesRevealed = 0;

				while (tilesRevealed < tilesToProcess && state.ScanIndex < state.CellsToScan.Count)
				{
					IntVec3 cell = state.CellsToScan[state.ScanIndex];
					state.ScanIndex++;

					// Cell might have been revealed by something else (mining, combat, etc.)
					// Skip if no longer fogged
					if (!map.fogGrid.IsFogged(cell))
						continue;

					map.fogGrid.Unfog(cell);
					comp.MountainTilesProcessed++;
					tilesRevealed++;
				}

				// Check if mountain scan just completed
				if (state.ScanIndex >= state.CellsToScan.Count)
				{
					// Send completion message
					Messages.Message(
						"SubcoreAutomation_MountainScanComplete".Translate(comp.MountainTilesProcessed),
						comp.parent,
						MessageTypeDefOf.PositiveEvent);

					// Reset and switch back to deep scan mode
					comp.CompleteMountainScan();
					state.CellsToScan = null;
					state.ScanIndex = 0;
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"SubcoreAutomation: Error during mountain scan: {ex.Message}", 48392718);
				return false;
			}
		}

		/// <summary>
		/// Builds a cached list of all fogged mountain cells, sorted by distance from scanner.
		/// </summary>
		private static void BuildMountainCellCache(CompScannerAutomation comp, Map map, MountainScanState state)
		{
			IntVec3 center = comp.parent.Position;
			state.CachePosition = center;
			state.CellsToScan = new List<IntVec3>();

			foreach (IntVec3 cell in map.AllCells)
			{
				if (IsFoggedMountainCell(map, cell))
					state.CellsToScan.Add(cell);
			}

			// Sort by distance from scanner (closest first)
			state.CellsToScan.SortBy(c => c.DistanceToSquared(center));
			state.ScanIndex = 0;
		}

		/// <summary>
		/// Checks if a cell is a fogged mountain cell (overhead mountain or impassable rock).
		/// </summary>
		private static bool IsFoggedMountainCell(Map map, IntVec3 cell)
		{
			// Must be fogged
			if (!map.fogGrid.IsFogged(cell))
				return false;

			// Check for overhead mountain roof (primary indicator)
			if (map.roofGrid.RoofAt(cell) == RoofDefOf.RoofRockThick)
				return true;

			// Check for rock/ore edifice (mineable hill)
			Building edifice = cell.GetEdifice(map);
			if (edifice?.def?.building?.isNaturalRock == true)
				return true;

			// Or impassable terrain without edifice (mountain floor under fog)
			if (edifice == null)
			{
				var terrain = map.terrainGrid.TerrainAt(cell);
				return terrain?.passability == Traversability.Impassable;
			}
			return false;
		}

		/// <summary>
		/// Gets the count of remaining fogged mountain cells.
		/// </summary>
		public static int GetRemainingMountainCells(CompScannerAutomation comp)
		{
			if (!_mountainStates.TryGetValue(comp, out var state) || state.CellsToScan == null)
				return -1;
			return state.CellsToScan.Count - state.ScanIndex;
		}

		/// <summary>
		/// Checks if mountain scanning is complete.
		/// </summary>
		public static bool IsMountainScanComplete(CompScannerAutomation comp)
		{
			if (!_mountainStates.TryGetValue(comp, out var state) || state.CellsToScan == null)
				return false;
			return state.ScanIndex >= state.CellsToScan.Count;
		}

		/// <summary>
		/// Clean up mountain scan state when comp is destroyed or mode changes.
		/// </summary>
		public static void CleanupMountainState(CompScannerAutomation comp)
		{
			_mountainStates.Remove(comp);
		}

		/// <summary>
		/// Toggle between deep resource scanning and mountain scanning modes.
		/// </summary>
		public static void ToggleMode(CompScannerAutomation comp)
		{
			var newMode = comp.ScannerMode == ScannerMode.DeepResources
				? ScannerMode.MountainOres
				: ScannerMode.DeepResources;
			
			comp.SetScannerMode(newMode);
			comp.MountainTilesProcessed = 0;
			CleanupMountainState(comp);
		}

		/// <summary>
		/// Called when mountain scan completes. Resets state and switches to deep scan mode.
		/// </summary>
		public static void CompleteMountainScan(CompScannerAutomation comp)
		{
			comp.SetScannerMode(ScannerMode.DeepResources);
			comp.MountainTilesProcessed = 0;
		}

		#endregion

		#region Deep Resource Scanning

		/// <summary>
		/// Performs automated deep resource scanning tick.
		/// Called every 250 ticks when scanner is automated.
		/// </summary>
		public static bool TryDeepResourceScan(CompScannerAutomation comp, float speedFactor)
		{
			// Get the vanilla scanner component
			CompScanner vanillaScanner = comp.CachedDeepScanner ?? (CompScanner)comp.CachedMineralScanner;
			if (vanillaScanner == null)
			{
				Log.ErrorOnce("[SubcoreAutomation] TryDeepResourceScan: vanillaScanner is null", 8472911);
				return false;
			}

			// Check power
			CompPowerTrader power = comp.PowerTrader;
			if (power != null && !power.PowerOn)
				return false;

			// Verify reflection is available
			if (ReflectionManifest.Scanner_daysWorking == null)
			{
				Log.ErrorOnce("[SubcoreAutomation] TryDeepResourceScan: Scanner_daysWorking reflection is null", 8472912);
				return false;
			}
			if (ReflectionManifest.Scanner_TickDoesFind == null)
			{
				Log.ErrorOnce("[SubcoreAutomation] TryDeepResourceScan: Scanner_TickDoesFind reflection is null", 8472913);
				return false;
			}
			if (ReflectionManifest.Scanner_DoFind == null)
			{
				Log.ErrorOnce("[SubcoreAutomation] TryDeepResourceScan: Scanner_DoFind reflection is null", 8472914);
				return false;
			}

			try
			{
				// Get current progress
				float daysWorking = (float)ReflectionManifest.Scanner_daysWorking.GetValue(vanillaScanner);

				// Add progress (59 ticks per interval to match vanilla's TickDoesFind check)
				float progressPerTick = (speedFactor * 59f) / 60000f;
				float newDaysWorking = daysWorking + progressPerTick;
				
				// Update progress BEFORE checking for find (vanilla updates daysWorking first)
				ReflectionManifest.Scanner_daysWorking.SetValue(vanillaScanner, newDaysWorking);

				// Check if we found something
				bool didFind = (bool)ReflectionManifest.Scanner_TickDoesFind.Invoke(vanillaScanner, new object[] { speedFactor });

				if (didFind)
				{
					// Call DoFind with null worker (automated)
					ReflectionManifest.Scanner_DoFind.Invoke(vanillaScanner, new object[] { null });
					// Reset progress after finding
					ReflectionManifest.Scanner_daysWorking.SetValue(vanillaScanner, 0f);
				}

				// Update last scan tick for UI
				if (ReflectionManifest.Scanner_lastScanTick != null)
				{
					ReflectionManifest.Scanner_lastScanTick.SetValue(vanillaScanner, Find.TickManager.TicksGame);
				}

				return didFind;
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Scanner automation error: {ex.Message}\n{ex.StackTrace}", 8472910);
				return false;
			}
		}

		#endregion

		#region Gizmos

		public static IEnumerable<Gizmo> GetScannerGizmos(CompScannerAutomation comp)
		{
			// Scanner mode toggle gizmo (Deep scan vs Mountain scan)
			yield return new Command_Action
			{
				defaultLabel = comp.ScannerMode == ScannerMode.DeepResources 
					? "SubcoreAutomation_ScannerModeDeep".Translate() 
					: "SubcoreAutomation_ScannerModeMountain".Translate(),
				defaultDesc = comp.ScannerMode == ScannerMode.DeepResources
					? "SubcoreAutomation_ScannerModeDeepDesc".Translate()
					: "SubcoreAutomation_ScannerModeMountainDesc".Translate(),
				icon = comp.ScannerMode == ScannerMode.DeepResources
					? (ContentFinder<Texture2D>.Get("Things/Building/Production/DeepDrill", false) ?? TexCommand.Attack)
					: (ContentFinder<Texture2D>.Get("UI/Designators/Mine", false) ?? TexCommand.Attack),
				action = () => comp.ToggleScannerMode()
			};

			// Target mineral selection gizmo - only show in deep scan mode
			if (comp.ScannerMode == ScannerMode.DeepResources)
			{
				if (comp.TargetMineral != null)
				{
					yield return new Command_Action
					{
						defaultLabel = comp.TargetMineral.LabelCap,
						defaultDesc = "SubcoreAutomation_ScannerTargetDesc".Translate(comp.TargetMineral.LabelCap),
						icon = comp.TargetMineral.uiIcon ?? BaseContent.BadTex,
						action = () => ShowMineralSelectionFloatMenu(comp)
					};
				}
				else
				{
					// "Any" mode - use question mark to indicate random selection
					yield return new Command_Action
					{
						defaultLabel = "SubcoreAutomation_ScannerTargetAny".Translate(),
						defaultDesc = "SubcoreAutomation_ScannerTargetAnyDesc".Translate(),
						icon = ContentFinder<Texture2D>.Get("UI/Overlays/QuestionMark", false) 
							?? ContentFinder<Texture2D>.Get("UI/Icons/Medical/FeedPatient", false) 
							?? TexCommand.Attack,
						action = () => ShowMineralSelectionFloatMenu(comp)
					};
				}
			}
		}

		public static void ShowMineralSelectionFloatMenu(CompScannerAutomation comp)
		{
			var options = new List<FloatMenuOption>();

			// "Any" option - random weighted selection
			options.Add(new FloatMenuOption("Any mineral (random)", () => comp.SetTargetMineral(null)));

			// Get targetable minerals - exclude extremely rare ones (commonality < 0.1)
			// Resources with very low commonality are intentionally rare and shouldn't be directly targetable
			const float minTargetableCommonality = 0.1f;
			var minerals = DefDatabase<ThingDef>.AllDefs
				.Where(d => d.deepCommonality >= minTargetableCommonality)
				.OrderByDescending(d => d.deepCommonality)
				.ToList();

			foreach (var mineral in minerals)
			{
				var def = mineral; // Capture for lambda
				options.Add(new FloatMenuOption(def.LabelCap, () => comp.SetTargetMineral(def), def));
			}

			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static string GetScannerBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_ScannerBenefits".Translate();
		}

		/// <summary>
		/// Gets the detailed inspect string for scanner automation.
		/// </summary>
		public static string GetInspectString(CompScannerAutomation comp)
		{
			var sb = new System.Text.StringBuilder();

			// Ground-penetrating scanner
			if (comp.IsGroundPenetratingScanner)
			{
				if (comp.ScannerMode == ScannerMode.MountainOres)
				{
					// Mountain scanning mode - condensed to single line
					int remaining = GetRemainingMountainCells(comp);
					if (IsMountainScanComplete(comp))
					{
						sb.Append("SubcoreAutomation_MountainScanDone".Translate(comp.MountainTilesProcessed));
					}
					else if (remaining > 0)
					{
						sb.Append("SubcoreAutomation_MountainScanStatus".Translate(comp.MountainTilesProcessed, remaining));
					}
					else
					{
						sb.Append("SubcoreAutomation_MountainScanProgress".Translate(comp.MountainTilesProcessed));
					}
				}
				else
				{
					// Deep resource scanning mode - condensed with target and progress on one line
					string target = comp.TargetMineral != null 
						? comp.TargetMineral.LabelCap.ToString()
						: "SubcoreAutomation_ScannerTargetAny".Translate().ToString();
					
					// Try to get progress info
					string timeRemaining = null;
					float progress = 0f;
					bool hasProgress = false;
					
					if (comp.CachedDeepScanner != null && Core.ReflectionManifest.Scanner_daysWorking != null)
					{
						try
						{
							float daysWorking = (float)Core.ReflectionManifest.Scanner_daysWorking.GetValue(comp.CachedDeepScanner);
							var props = comp.CachedDeepScanner.Props;
							float guaranteedDays = props.scanFindGuaranteedDays;

							if (guaranteedDays > 0f)
							{
								progress = Mathf.Clamp01(daysWorking / guaranteedDays);
								hasProgress = true;

								float speedFactor = comp.EffectiveScanSpeedFactor;
								float daysRemaining = (guaranteedDays - daysWorking) / speedFactor;
								if (daysRemaining > 0f)
								{
									int ticksRemaining = (int)(daysRemaining * GenDate.TicksPerDay);
									timeRemaining = ticksRemaining.ToStringTicksToPeriod();
								}
							}
						}
						catch { /* Ignore reflection errors */ }
					}
					
					// Condensed: "Scanning [Target]: [X%] (~[time])"
					if (hasProgress && timeRemaining != null)
					{
						sb.Append("SubcoreAutomation_ScannerStatusFull".Translate(target, progress.ToStringPercent(), timeRemaining));
					}
					else if (hasProgress)
					{
						sb.Append("SubcoreAutomation_ScannerStatusProgress".Translate(target, progress.ToStringPercent()));
					}
					else
					{
						sb.Append("SubcoreAutomation_ScannerStatusBasic".Translate(target));
					}
				}
			}
			// Long-range mineral scanner
			else if (comp.IsLongRangeMineralScanner)
			{
				string target = comp.TargetMineral != null 
					? comp.TargetMineral.LabelCap.ToString()
					: "SubcoreAutomation_ScannerTargetAny".Translate().ToString();
				
				// Try to get progress info
				string timeRemaining = null;
				float progress = 0f;
				bool hasProgress = false;
				
				if (comp.CachedMineralScanner != null && Core.ReflectionManifest.Scanner_daysWorking != null)
				{
					try
					{
						float daysWorking = (float)Core.ReflectionManifest.Scanner_daysWorking.GetValue(comp.CachedMineralScanner);
						var props = comp.CachedMineralScanner.Props;
						float guaranteedDays = props.scanFindGuaranteedDays;

						if (guaranteedDays > 0f)
						{
							progress = Mathf.Clamp01(daysWorking / guaranteedDays);
							hasProgress = true;

							float speedFactor = comp.EffectiveScanSpeedFactor;
							float daysRemaining = (guaranteedDays - daysWorking) / speedFactor;
							if (daysRemaining > 0f)
							{
								int ticksRemaining = (int)(daysRemaining * GenDate.TicksPerDay);
								timeRemaining = ticksRemaining.ToStringTicksToPeriod();
							}
						}
					}
					catch { /* Ignore reflection errors */ }
				}
				
				// Condensed: "Scanning [Target]: [X%] (~[time])"
				if (hasProgress && timeRemaining != null)
				{
					sb.Append("SubcoreAutomation_ScannerStatusFull".Translate(target, progress.ToStringPercent(), timeRemaining));
				}
				else if (hasProgress)
				{
					sb.Append("SubcoreAutomation_ScannerStatusProgress".Translate(target, progress.ToStringPercent()));
				}
				else
				{
					sb.Append("SubcoreAutomation_ScannerStatusBasic".Translate(target));
				}
			}
			else
			{
				sb.Append("SubcoreAutomation_AutomatedSimple".Translate());
			}

			return sb.ToString();
		}

		#endregion
	}
}