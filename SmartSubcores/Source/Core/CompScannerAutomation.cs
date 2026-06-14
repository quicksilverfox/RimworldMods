using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Scanning mode for Ground Penetrating Scanner with subcore.
	/// </summary>
	public enum ScannerMode
	{
		DeepResources,  // Standard GPS - find deep deposits
		MountainOres    // Reveal fogged mountain cells
	}

	/// <summary>
	/// Subcore automation component for ground/mineral scanner machines.
	/// Handles: GroundPenetratingScanner, LongRangeMineralScanner.
	/// </summary>
	public class CompScannerAutomation : CompSubcoreAutomationBase
	{
		#region Scanner-Specific State

		private CompDeepScanner _cachedDeepScanner;
		private CompLongRangeMineralScanner _cachedMineralScanner;
		private int _lastScanTick;
		
		// Ground-penetrating scanner state
		private ThingDef _targetMineral;
		private ScannerMode _scannerMode = ScannerMode.DeepResources;
		private int _mountainTilesProcessed;

		#endregion

		#region Properties

		public CompDeepScanner CachedDeepScanner => _cachedDeepScanner;
		public CompLongRangeMineralScanner CachedMineralScanner => _cachedMineralScanner;
		public bool IsGroundPenetratingScanner => _cachedDeepScanner != null;
		public bool IsLongRangeMineralScanner => _cachedMineralScanner != null;

		public int LastScanTick
		{
			get => _lastScanTick;
			set => _lastScanTick = value;
		}

		public ThingDef TargetMineral => _targetMineral;
		
		public ScannerMode ScannerMode => _scannerMode;
		
		public int MountainTilesProcessed
		{
			get => _mountainTilesProcessed;
			set => _mountainTilesProcessed = value;
		}

		// Speed penalty applied when targeting a specific mineral (vs. "any").
		public const float TargetedScanSpeedMultiplier = 0.6f;

		public void SetTargetMineral(ThingDef mineral)
		{
			if (_targetMineral == mineral)
				return;
			_targetMineral = mineral;
			ResetScanProgress();
		}

		private void ResetScanProgress()
		{
			if (ReflectionManifest.Scanner_daysWorking == null)
				return;
			if (_cachedDeepScanner != null)
				ReflectionManifest.Scanner_daysWorking.SetValue(_cachedDeepScanner, 0f);
			if (_cachedMineralScanner != null)
				ReflectionManifest.Scanner_daysWorking.SetValue(_cachedMineralScanner, 0f);
		}

		public float EffectiveScanSpeedFactor
		{
			get
			{
				float baseFactor = Props?.automatedSpeedFactor ?? 0.5f;
				return _targetMineral != null ? baseFactor * TargetedScanSpeedMultiplier : baseFactor;
			}
		}
		
		public void SetScannerMode(ScannerMode mode) => _scannerMode = mode;
		
		public void ToggleScannerMode() => Handlers.ScannerHandler.ToggleMode(this);
		
		public void CompleteMountainScan() => Handlers.ScannerHandler.CompleteMountainScan(this);

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			_cachedDeepScanner = parent.TryGetComp<CompDeepScanner>();
			_cachedMineralScanner = parent.TryGetComp<CompLongRangeMineralScanner>();
		}

		protected override void DoMachineSpecificTick()
		{
			// IMPORTANT: Update lastScanTick EVERY tick for Re-Powered compatibility
			// Re-Powered checks if scanner was used recently to determine power consumption
			int currentTick = Find.TickManager.TicksGame;
			if (ReflectionManifest.Scanner_lastScanTick != null)
			{
				if (_cachedDeepScanner != null)
					ReflectionManifest.Scanner_lastScanTick.SetValue(_cachedDeepScanner, currentTick);
				else if (_cachedMineralScanner != null)
					ReflectionManifest.Scanner_lastScanTick.SetValue(_cachedMineralScanner, currentTick);
			}

			// Mountain scanning (every 250 ticks - matches original implementation speed)
			if (IsGroundPenetratingScanner && _scannerMode == ScannerMode.MountainOres)
			{
				if (parent.IsHashIntervalTick(250))
				{
					ScannerHandler.TryMountainScan(this, Props?.automatedSpeedFactor ?? 0.5f);
				}
			}
			// Deep resource scanning (every 59 ticks - matches vanilla's internal TickDoesFind check)
			else if (parent.IsHashIntervalTick(59))
			{
				// Ground-penetrating scanner in deep mode OR long-range mineral scanner
				if (_scannerMode == ScannerMode.DeepResources || IsLongRangeMineralScanner)
				{
					ScannerHandler.TryDeepResourceScan(this, EffectiveScanSpeedFactor);
				}
			}
		}

		// DoMachineSpecificTickRare removed - deep scanning handled in DoMachineSpecificTick with IsHashIntervalTick(250)

		protected override void ExposeDataMachineSpecific()
		{
			Scribe_Values.Look(ref _lastScanTick, "lastScanTick", 0);
			Scribe_Defs.Look(ref _targetMineral, "targetMineral");
			Scribe_Values.Look(ref _scannerMode, "scannerMode", ScannerMode.DeepResources);
			Scribe_Values.Look(ref _mountainTilesProcessed, "mountainTilesProcessed", 0);
		}

		protected override string GetMachineSpecificInspectString()
		{
			return ScannerHandler.GetInspectString(this);
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			foreach (var gizmo in ScannerHandler.GetScannerGizmos(this))
			{
				yield return gizmo;
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			return ScannerHandler.GetScannerBenefitsDescription();
		}

		#endregion
	}
}
