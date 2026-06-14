using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for production machines.
	/// Handles: DeepDrill, Hydroponics, MoisturePump, ToxifierGenerator.
	/// </summary>
	public class CompProductionAutomation : CompSubcoreAutomationBase, IThingGlower, IProductionAutomation
	{
		#region Production-Specific State

		// Deep drill
		private float _drillProgress;
		private CompDeepDrill _cachedDrill;

		// Hydroponics
		private Building_PlantGrower _cachedPlantGrower;
		private bool _sunLampEnabled = true;

		#endregion

		#region Properties

		/// <summary>
		/// Gets the deep drill component if present (cached).
		/// </summary>
		public CompDeepDrill DeepDrill => _cachedDrill;

		/// <summary>
		/// Gets or sets the drill progress (0-1). Used by DrillHandler for tracking.
		/// </summary>
		public float DrillProgress
		{
			get => _drillProgress;
			set => _drillProgress = value;
		}

		/// <summary>
		/// Gets or sets whether the built-in sun lamp is enabled for hydroponics.
		/// </summary>
		public bool SunLampEnabled
		{
			get => _sunLampEnabled;
			set => _sunLampEnabled = value;
		}

		/// <summary>
		/// The parent thing (for IProductionAutomation interface).
		/// </summary>
		public ThingWithComps Parent => parent;

		#endregion

		#region IThingGlower Implementation

		/// <summary>
		/// IThingGlower implementation - controls the built-in sun lamp for hydroponics basins.
		/// </summary>
		public bool ShouldBeLitNow()
		{
			// Only control glow for hydroponics basins
			if (_cachedPlantGrower == null)
				return true; // Don't interfere with other glowers

			// Sun lamp only active if subcore installed and enabled
			if (!_subcoreInstalled || !_sunLampEnabled)
				return false;

			// Check if feature is enabled in settings
			if (!SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
				return false;

			// Check power
			if (_cachedPower != null && !_cachedPower.PowerOn)
				return false;

			// Check growing hours (6 AM to midnight)
			return Patches.HydroponicsPatches.IsGrowingHours(parent.Map);
		}

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			// Cache deep drill
			_cachedDrill = parent.GetComp<CompDeepDrill>();

			// Cache plant grower
			_cachedPlantGrower = parent as Building_PlantGrower;

			// Initialize hydroponics sun lamp setting
			if (!respawningAfterLoad && _cachedPlantGrower != null)
			{
				_sunLampEnabled = SubcoreAutomationMod.Settings.hydroponicsDefaultSunLamp;
			}

			// Initialize drill progress
			if (!respawningAfterLoad && _cachedDrill != null)
			{
				_drillProgress = 0f;
			}
		}

		protected override void DoMachineSpecificTick()
		{
			// Deep drill automation
			if (_cachedDrill != null)
			{
				DrillHandler.TryAutomateDrillTick(this);
			}

			// Hydroponics automation (sow/harvest and sun lamp updates)
			if (_cachedPlantGrower != null)
			{
				if (parent.IsHashIntervalTick(WorkIntervalTicks) && SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
				{
					Patches.HydroponicsPatches.ProcessHydroponics(_cachedPlantGrower, this);
				}
				// Re-assert power draw EVERY tick. Re-Powered overwrites PowerOutput
				// based on its own active/idle logic, which doesn't know about the sun
				// lamp's extra draw. By rewriting after the map-component pass we make
				// sure the power net reads our combined total.
				UpdatePowerConsumption();
				UpdateHydroponicsGlower();
			}
		}

		protected override void DoMachineSpecificTickRare()
		{
			// Hydroponics rare tick processing
			if (_cachedPlantGrower != null)
			{
				Patches.HydroponicsPatches.ProcessHydroponics(_cachedPlantGrower, this);
				UpdateHydroponicsGlower();
			}
		}

		protected override void OnSubcoreInstalledRegistrations(bool respawningAfterLoad = false)
		{
			// Register hydroponics cells for automated processing
			if (_cachedPlantGrower != null)
			{
				Patches.HydroponicsPatches.RegisterAutomatedCells(_cachedPlantGrower);
			}
		}

		protected override void OnSubcoreRemovedRegistrations()
		{
			// Unregister hydroponics cells
			if (_cachedPlantGrower != null)
			{
				Patches.HydroponicsPatches.UnregisterAutomatedCells(_cachedPlantGrower);
			}
		}

		protected override void OnDestroyMachineSpecific(DestroyMode mode, Map previousMap)
		{
			// Cleanup drill state
			if (_cachedDrill != null)
			{
				DrillHandler.Cleanup(this);
			}

			// Cleanup hydroponics
			if (_subcoreInstalled && _cachedPlantGrower != null)
			{
				Patches.HydroponicsPatches.UnregisterAutomatedCells(_cachedPlantGrower);
			}
		}

		protected override int GetAdditionalPowerConsumption()
		{
			// Hydroponics sun lamp power
			if (_cachedPlantGrower != null)
			{
				return Patches.HydroponicsPatches.GetSunLampPower(this);
			}
			return 0;
		}

		protected override void ExposeDataMachineSpecific()
		{
			// Save drill progress
			Scribe_Values.Look(ref _drillProgress, "drillProgress", 0f);

			// Hydroponics settings
			Scribe_Values.Look(ref _sunLampEnabled, "sunLampEnabled", true);
		}

		protected override string GetMachineSpecificInspectString()
		{
			string defName = parent.def.defName;

			// Deep drill inspect string
			if (_cachedDrill != null)
			{
				string result = "SubcoreAutomation_AutomatedDrill".Translate();
				if (_drillProgress > 0)
				{
					result += "\n" + "SubcoreAutomation_DrillProgress".Translate(_drillProgress.ToStringPercent());
				}
				return result;
			}

			// Moisture pump inspect string
			if (defName == MachineDefNames.MoisturePump)
			{
				return ProductionHandler.GetMoisturePumpInspectString(this);
			}

			// Toxifier generator inspect string
			if (defName == MachineDefNames.ToxifierGenerator)
			{
				return ProductionHandler.GetToxifierInspectString(this);
			}

			// Hydroponics inspect string
			if (_cachedPlantGrower != null)
			{
				return ProductionHandler.GetHydroponicsInspectString(this);
			}

			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			// Hydroponics sun lamp gizmo
			if (_cachedPlantGrower != null && SubcoreAutomationMod.Settings.hydroponicsFeaturesEnabled)
			{
				foreach (var gizmo in ProductionHandler.GetHydroponicsGizmos(this))
				{
					yield return gizmo;
				}
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			string defName = parent.def.defName;

			// Deep drill benefits
			if (_cachedDrill != null)
			{
				return ProductionHandler.GetDrillBenefitsDescription(this);
			}

			// Moisture pump benefits
			if (defName == MachineDefNames.MoisturePump)
			{
				return ProductionHandler.GetMoisturePumpBenefitsDescription();
			}

			// Toxifier generator benefits
			if (defName == MachineDefNames.ToxifierGenerator)
			{
				return ProductionHandler.GetToxifierBenefitsDescription();
			}

			// Hydroponics benefits
			if (_cachedPlantGrower != null)
			{
				return ProductionHandler.GetHydroponicsBenefitsDescription();
			}

			return "";
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Attempts to turn off the drill via its flickable component.
		/// Called by DrillHandler when the deposit is exhausted.
		/// </summary>
		public void TryTurnOffDrill()
		{
			if (_cachedFlickable != null && _cachedFlickable.SwitchIsOn)
			{
				_cachedFlickable.SwitchIsOn = false;
				// Also set wantSwitchOn to match
				if (SubcoreAutomationUtils.FlickableWantSwitchOnField != null)
					SubcoreAutomationUtils.FlickableWantSwitchOnField.SetValue(_cachedFlickable, false);

				// Clean up any stale flick designation (our patch handles removal)
				FlickUtility.UpdateFlickDesignation(parent);

				Messages.Message("SubcoreAutomation_DrillDepositExhausted".Translate(parent.LabelShort),
					parent, MessageTypeDefOf.NeutralEvent, false);
			}
		}

		/// <summary>
		/// Updates the glower state for hydroponics sun lamp.
		/// </summary>
		public void UpdateHydroponicsGlower()
		{
			if (_cachedPlantGrower == null || parent.Map == null)
				return;

			var glower = parent.GetComp<CompGlower>();
			if (glower != null)
			{
				glower.UpdateLit(parent.Map);
			}
		}

		/// <summary>
		/// Counts remaining wet cells for moisture pump.
		/// </summary>
		public int CountRemainingWetCells()
		{
			if (parent?.Map == null)
				return 0;

			CompTerrainPump pump = parent.TryGetComp<CompTerrainPump>();
			if (pump == null)
				return 0;

			CompProperties_TerrainPump props = (CompProperties_TerrainPump)pump.props;
			float radius = props.radius;
			Map map = parent.Map;
			IntVec3 position = parent.Position;

			int count = 0;
			int numCells = GenRadial.NumCellsInRadius(radius);
			for (int i = 0; i < numCells; i++)
			{
				IntVec3 cell = position + GenRadial.RadialPattern[i];
				if (cell.InBounds(map))
				{
					TerrainDef terrain = map.terrainGrid.TopTerrainAt(cell);
					if (terrain?.driesTo != null)
						count++;

					TerrainDef underTerrain = map.terrainGrid.UnderTerrainAt(cell);
					if (underTerrain?.driesTo != null)
						count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Gets the effective efficiency based on settings.
		/// </summary>
		public float GetEffectiveEfficiency(MachineSettings settings)
		{
			if (settings == null) return 0.5f;
			
			var machineDef = AutomatableMachineDef.GetByTargetDefName(parent.def.defName);
			float efficiency = settings.efficiency >= 0 ? settings.efficiency : (machineDef?.defaultEfficiency ?? 0.5f);
			return UnityEngine.Mathf.Clamp01(efficiency);
		}

		#endregion
	}
}
