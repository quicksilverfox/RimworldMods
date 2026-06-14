using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Handlers;
using SubcoreAutomation.UI;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Abstract base class for subcore automation components.
	/// Provides core installation/removal logic, power management, and flick automation.
	/// Machine-specific behavior is implemented in subclasses.
	/// </summary>
	public abstract class CompSubcoreAutomationBase : ThingComp
	{
		#region Constants

		protected const int WorkIntervalTicks = 250;

		#endregion

		#region Core State

		protected bool _subcoreInstalled;
		protected bool _installationRequested;
		protected bool _removalRequested;
		protected int _basePowerConsumption = -1;

		#endregion

		#region Cached Components (Common)

		protected CompPowerTrader _cachedPower;
		protected CompFlickable _cachedFlickable;
		protected CompBreakdownable _cachedBreakdownable;
		protected CompRefuelable _cachedRefuelable;

		#endregion

		#region Properties

		/// <summary>
		/// The CompProperties for this component.
		/// Subclasses should shadow this with their specific type.
		/// </summary>
		public CompProperties_SubcoreAutomation Props => (CompProperties_SubcoreAutomation)props;

		public bool SubcoreInstalled => _subcoreInstalled;
		public bool InstallationRequested => _installationRequested;
		public bool RemovalRequested => _removalRequested;
		public bool HasSubcoreInstalled => _subcoreInstalled;

		/// <summary>
		/// Whether automation is enabled in mod settings for this machine type.
		/// </summary>
		public bool IsAutomationEnabled
		{
			get
			{
				if (SubcoreAutomationMod.Settings == null)
					return true;
				return SubcoreAutomationMod.Settings.IsAutomationEnabled(parent.def.defName);
			}
		}

		/// <summary>
		/// Gets the effective speed factor, considering mod settings override.
		/// </summary>
		public virtual float EffectiveSpeedFactor
		{
			get
			{
				if (SubcoreAutomationMod.Settings == null)
					return Props.automatedSpeedFactor;

				float settingsEfficiency = SubcoreAutomationMod.Settings.GetEfficiencyOverride(parent.def.defName);
				if (settingsEfficiency >= 0f)
					return settingsEfficiency;

				return Props.automatedSpeedFactor;
			}
		}

		/// <summary>
		/// Gets the power trader component (cached).
		/// </summary>
		public CompPowerTrader PowerTrader => _cachedPower;

		/// <summary>
		/// Gets the power comp for generator functionality.
		/// </summary>
		public CompPowerTrader PowerComp => _cachedPower;

		/// <summary>
		/// Gets the power net this building is connected to.
		/// </summary>
		public PowerNet PowerNet => _cachedPower?.PowerNet;

		/// <summary>
		/// Gets the cached breakdownable comp, if any.
		/// </summary>
		public CompBreakdownable CachedBreakdownable => _cachedBreakdownable;

		/// <summary>
		/// Gets the cached refuelable comp, if any.
		/// </summary>
		public CompRefuelable CachedRefuelable => _cachedRefuelable;

		/// <summary>
		/// Gets the flickable component.
		/// </summary>
		public CompFlickable CachedFlickable => _cachedFlickable;

		#endregion

		#region ThingComp Overrides

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);

			// Cache common comps
			_cachedPower = parent.GetComp<CompPowerTrader>();
			_cachedFlickable = parent.GetComp<CompFlickable>();
			_cachedBreakdownable = parent.GetComp<CompBreakdownable>();
			_cachedRefuelable = parent.GetComp<CompRefuelable>();

			// Store base power consumption
			if (_cachedPower != null && _basePowerConsumption < 0)
			{
				_basePowerConsumption = (int)_cachedPower.Props.PowerConsumption;
			}

			// Machine-specific setup
			PostSpawnSetupMachineSpecific(respawningAfterLoad);

			UpdatePowerConsumption();

			// Machine-specific registration
			if (_subcoreInstalled)
			{
				OnSubcoreInstalledRegistrations(respawningAfterLoad);
			}
		}

		public override void CompTick()
		{
			base.CompTick();

			if (!_subcoreInstalled)
				return;

			// If automation is disabled in settings, don't do any work
			if (!IsAutomationEnabled)
				return;

			// Auto-complete any pending flick commands immediately
			TryAutoFlick();

			if (!CanOperate())
				return;

			// Machine-specific tick logic
			DoMachineSpecificTick();
		}

		public override void CompTickRare()
		{
			base.CompTickRare();

			if (!_subcoreInstalled)
				return;

			if (!IsAutomationEnabled)
				return;

			// Auto-complete any pending flick commands (for buildings that only use TickRare)
			TryAutoFlick();

			if (!CanOperate())
				return;

			DoMachineSpecificTickRare();
		}

		public override string CompInspectStringExtra()
		{
			// Don't show inspect string if not spawned (e.g., minified)
			if (!parent.Spawned)
				return null;

			// No subcore installed
			if (!_subcoreInstalled)
			{
				if (_installationRequested)
					return "SubcoreAutomation_AwaitingSubcore".Translate();
				if (_removalRequested)
					return "SubcoreAutomation_AwaitingRemoval".Translate();
				return null;
			}

			// Check if automation is disabled in settings
			if (!IsAutomationEnabled)
			{
				return "SubcoreAutomation_AutomationDisabled".Translate();
			}

			// Subcore installed - get machine-specific inspect string
			return GetMachineSpecificInspectString();
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (var gizmo in base.CompGetGizmosExtra())
				yield return gizmo;

			// Only show gizmos for player faction
			if (parent.Faction != Faction.OfPlayer)
				yield break;

			// Base gizmos (install/remove)
			foreach (var gizmo in GetBaseGizmos())
				yield return gizmo;

			// Machine-specific gizmos (only when subcore installed)
			if (_subcoreInstalled && IsAutomationEnabled)
			{
				foreach (var gizmo in GetMachineSpecificGizmos())
					yield return gizmo;
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();

			// Core state
			Scribe_Values.Look(ref _subcoreInstalled, "subcoreInstalled", false);
			Scribe_Values.Look(ref _installationRequested, "installationRequested", false);
			Scribe_Values.Look(ref _removalRequested, "removalRequested", false);
			Scribe_Values.Look(ref _basePowerConsumption, "basePowerConsumption", -1);

			// Machine-specific state
			ExposeDataMachineSpecific();
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			base.PostDestroy(mode, previousMap);

			// Return the installed subcore when the player deconstructs the building,
			// matching the behavior of the removal gizmo. Damage-destroyed builds lose it.
			if (_subcoreInstalled
				&& mode == DestroyMode.Deconstruct
				&& previousMap != null
				&& (SubcoreAutomationMod.Settings == null || !SubcoreAutomationMod.Settings.permanentSubcoreInstallation))
			{
				MakeRemovalYield(previousMap, parent.Position, placeOnGround: true);
			}

			// Clean up machine-specific state
			OnDestroyMachineSpecific(mode, previousMap);
		}

		public override void PostDraw()
		{
			base.PostDraw();

			if (_subcoreInstalled)
			{
				PostDrawMachineSpecific();
			}
		}

		#endregion

		#region Abstract/Virtual Methods for Subclasses

		/// <summary>
		/// Called during PostSpawnSetup for machine-specific initialization.
		/// </summary>
		protected virtual void PostSpawnSetupMachineSpecific(bool respawningAfterLoad) { }

		/// <summary>
		/// Called every tick when subcore is installed and automation is enabled.
		/// </summary>
		protected virtual void DoMachineSpecificTick() { }

		/// <summary>
		/// Called every rare tick when subcore is installed and automation is enabled.
		/// </summary>
		protected virtual void DoMachineSpecificTickRare() { }

		/// <summary>
		/// Returns the inspect string for this machine type.
		/// </summary>
		protected abstract string GetMachineSpecificInspectString();

		/// <summary>
		/// Returns gizmos specific to this machine type.
		/// </summary>
		protected abstract IEnumerable<Gizmo> GetMachineSpecificGizmos();

		/// <summary>
		/// Save/load machine-specific state.
		/// </summary>
		protected virtual void ExposeDataMachineSpecific() { }

		/// <summary>
		/// Called when the component is destroyed.
		/// </summary>
		protected virtual void OnDestroyMachineSpecific(DestroyMode mode, Map previousMap) { }

		/// <summary>
		/// Called after drawing for machine-specific overlays.
		/// </summary>
		protected virtual void PostDrawMachineSpecific() { }

		/// <summary>
		/// Called when subcore is installed to register machine-specific state.
		/// <paramref name="respawningAfterLoad"/> is true when invoked from PostSpawnSetup
		/// after a save load or gravship transit — subclasses should skip any
		/// recompute/notify in that case because the persisted state already reflects it.
		/// </summary>
		protected virtual void OnSubcoreInstalledRegistrations(bool respawningAfterLoad = false) { }

		/// <summary>
		/// Called when subcore is removed to unregister machine-specific state.
		/// </summary>
		protected virtual void OnSubcoreRemovedRegistrations() { }

		/// <summary>
		/// Returns the benefits description for the automation tooltip.
		/// </summary>
		protected abstract string GetMachineSpecificBenefitsDescription();

		#endregion

		#region Core Methods

		/// <summary>
		/// Checks if the building can currently operate.
		/// </summary>
		public bool CanOperate()
		{
			if (parent.Map == null)
				return false;

			// Check flickable state - if building is toggled off, can't operate
			if (_cachedFlickable != null && !IsFlickableEffectivelyOn())
				return false;

			// Check power
			if (_cachedPower != null && !_cachedPower.PowerOn)
				return false;

			// Check breakdown
			if (_cachedBreakdownable?.BrokenDown ?? false)
				return false;

			return true;
		}

		/// <summary>
		/// Auto-complete any pending flick commands immediately.
		/// </summary>
		protected void TryAutoFlick()
		{
			if (_cachedFlickable == null || !_cachedFlickable.WantsFlick())
				return;

			_cachedFlickable.DoFlick();
		}

		/// <summary>
		/// Checks if the flickable is effectively on (current state or pending state is on).
		/// Returns true if: switch is on AND (no pending flick OR flicking to stay on)
		/// Returns false if: switch is off OR user wants to turn it off
		/// </summary>
		protected bool IsFlickableEffectivelyOn()
		{
			if (_cachedFlickable == null)
				return true; // No flickable = always on

			// If no pending flick, just check current state
			if (!_cachedFlickable.WantsFlick())
				return _cachedFlickable.SwitchIsOn;

			// There's a pending flick - check what user wants via wantSwitchOn field
			if (SubcoreAutomationUtils.FlickableWantSwitchOnField != null)
			{
				object wantOn = SubcoreAutomationUtils.FlickableWantSwitchOnField.GetValue(_cachedFlickable);
				if (wantOn != null)
					return (bool)wantOn;
			}

			// Fallback: if we can't read wantSwitchOn, assume toggle means opposite of current
			return !_cachedFlickable.SwitchIsOn;
		}

		/// <summary>
		/// Update power consumption based on automation state.
		/// Base implementation sets power output directly.
		/// Subclasses can override for additional power needs (e.g., sun lamp).
		/// </summary>
		public virtual void UpdatePowerConsumption()
		{
			if (_cachedPower == null || _basePowerConsumption < 0)
				return;

			// Check if building is effectively off via flickable
			if (_cachedFlickable != null && !IsFlickableEffectivelyOn())
			{
				// Building is off - set power consumption to 0
				// We handle the off state ourselves rather than relying on vanilla
				// because vanilla's flickable-power integration doesn't work well
				// with our custom power management
				if (!SubcoreAutomationMod.TurnItOnAndOffLoaded)
				{
					_cachedPower.PowerOutput = 0;
				}
				return;
			}

			if (_subcoreInstalled)
			{
				// When subcore is installed and building is ON, set our automated power
				int totalPower = _basePowerConsumption + Props.automatedPowerConsumption;
				totalPower += GetAdditionalPowerConsumption();
				_cachedPower.PowerOutput = -totalPower;
			}
			else
			{
				// When no subcore, let Re-Powered manage idle/active states if loaded
				if (SubcoreAutomationMod.TurnItOnAndOffLoaded)
					return;
				_cachedPower.PowerOutput = -_basePowerConsumption;
			}
		}

		/// <summary>
		/// Returns additional power consumption for this machine type.
		/// Override in subclasses for machine-specific power needs.
		/// </summary>
		protected virtual int GetAdditionalPowerConsumption()
		{
			return 0;
		}

		/// <summary>
		/// Reapply power consumption (called when settings change).
		/// </summary>
		public void ReapplyPowerConsumption()
		{
			UpdatePowerConsumption();
		}

		#endregion

		#region Installation/Removal

		/// <summary>
		/// Request subcore installation.
		/// </summary>
		public void RequestInstallation()
		{
			if (_subcoreInstalled || _installationRequested)
				return;

			_installationRequested = true;
		}

		/// <summary>
		/// Cancel installation request.
		/// </summary>
		public void CancelInstallation()
		{
			_installationRequested = false;
		}

		/// <summary>
		/// Request subcore removal.
		/// </summary>
		public void RequestRemoval()
		{
			if (!_subcoreInstalled || _removalRequested)
				return;

			_removalRequested = true;
		}

		/// <summary>
		/// Cancel removal request.
		/// </summary>
		public void CancelRemoval()
		{
			_removalRequested = false;
		}

		/// <summary>
		/// Complete the installation (called by job driver).
		/// </summary>
		public void CompleteInstallation()
		{
			if (_subcoreInstalled)
				return;

			_subcoreInstalled = true;
			_installationRequested = false;
			UpdatePowerConsumption();

			// Remove any pending flick designation - subcore now controls power state
			parent.Map?.designationManager?.TryRemoveDesignationOn(parent, DesignationDefOf.Flick);

			// Machine-specific registrations
			OnSubcoreInstalledRegistrations();

			Messages.Message(
				"SubcoreAutomation_SubcoreInstalledMessage".Translate(parent.LabelCapNoCount),
				parent,
				MessageTypeDefOf.PositiveEvent);
		}

		/// <summary>
		/// Remove the installed subcore.
		/// </summary>
		public void RemoveSubcore(bool spawnSubcore = true, bool silent = false)
		{
			if (!_subcoreInstalled)
				return;

			_subcoreInstalled = false;
			_removalRequested = false;
			UpdatePowerConsumption();

			// Machine-specific cleanup
			OnSubcoreRemovedRegistrations();

			// Spawn the subcore (or refund fallback materials) if requested
			if (spawnSubcore)
			{
				MakeRemovalYield(parent.Map, parent.Position, placeOnGround: true);
			}

			if (!silent)
			{
				Messages.Message(
					"SubcoreAutomation_SubcoreRemovedMessage".Translate(parent.LabelCapNoCount),
					parent,
					MessageTypeDefOf.NeutralEvent);
			}
		}

		/// <summary>
		/// Complete the subcore removal (called by job driver).
		/// Returns the subcore thing for the pawn to carry.
		/// </summary>
		public Thing CompleteRemoval()
		{
			if (!_subcoreInstalled)
				return null;

			_subcoreInstalled = false;
			_removalRequested = false;
			UpdatePowerConsumption();

			// Machine-specific cleanup
			OnSubcoreRemovedRegistrations();

			// Create and return the subcore for the pawn to carry.
			// In fallback mode this refunds materials on the ground and returns null.
			Thing subcore = MakeRemovalYield(parent.Map, parent.Position, placeOnGround: false);

			Messages.Message(
				"SubcoreAutomation_SubcoreRemovedMessage".Translate(parent.LabelCapNoCount),
				parent,
				MessageTypeDefOf.NeutralEvent);

			return subcore;
		}

		/// <summary>
		/// Produces what subcore removal yields.
		/// In fallback mode (no Biotech), refunds the crafted-component materials onto the
		/// ground and returns null. Otherwise creates the subcore item; if placeOnGround is
		/// true it is placed near the building and null is returned, otherwise it is returned
		/// for the pawn to carry.
		/// </summary>
		private Thing MakeRemovalYield(Map map, IntVec3 pos, bool placeOnGround)
		{
			if (map == null)
				return null;

			// Fallback mode: refund crafted-component materials instead of a subcore item.
			if (SubcoreFallback.IsActive)
			{
				foreach (var mat in SubcoreFallback.GetFallbackMaterials(Props.tier))
				{
					if (mat?.thingDef == null || mat.count <= 0)
						continue;
					Thing stack = ThingMaker.MakeThing(mat.thingDef);
					stack.stackCount = mat.count;
					GenPlace.TryPlaceThing(stack, pos, map, ThingPlaceMode.Near);
				}
				return null;
			}

			ThingDef subcoreDef = Props.SubcoreDef;
			if (subcoreDef == null)
				return null;

			Thing subcore = ThingMaker.MakeThing(subcoreDef);
			if (placeOnGround)
			{
				GenPlace.TryPlaceThing(subcore, pos, map, ThingPlaceMode.Near);
				return null;
			}
			return subcore;
		}

		/// <summary>
		/// Install subcore immediately (god mode).
		/// </summary>
		public void InstallSubcoreGodMode()
		{
			if (_subcoreInstalled)
				return;

			_subcoreInstalled = true;
			_installationRequested = false;
			UpdatePowerConsumption();

			OnSubcoreInstalledRegistrations();
		}

		#endregion

		#region Base Gizmos

		/// <summary>
		/// Returns the base installation/removal gizmos.
		/// </summary>
		protected virtual IEnumerable<Gizmo> GetBaseGizmos()
		{
			if (!CanShowAutomationGizmos())
				yield break;

			var gizmos = _subcoreInstalled ? GetRemovalGizmos() : GetInstallationGizmos();
			foreach (var gizmo in gizmos)
				yield return gizmo;
		}

		/// <summary>
		/// Returns gizmos for installing a subcore.
		/// </summary>
		private IEnumerable<Gizmo> GetInstallationGizmos()
		{
			if (!_installationRequested)
			{
				string costDescription = SubcoreFallback.IsActive
					? SubcoreFallback.GetMaterialsDescription(Props.tier)
					: (Props.SubcoreDef?.label ?? "subcore");

				Texture2D primaryIcon = GetSubcoreIcon();
				Texture2D secondaryIcon = GetSubcoreSecondaryIcon();
				yield return new Command_ActionOverlay
				{
					defaultLabel = "SubcoreAutomation_RequestInstallation".Translate(),
					defaultDesc = "SubcoreAutomation_RequestInstallationDesc".Translate(costDescription) + GetMachineSpecificBenefitsDescription(),
					icon = primaryIcon,
					// When the tier costs two component types, show both at equal size.
					tiledIcons = secondaryIcon != null
						? new List<Texture2D> { primaryIcon, secondaryIcon }
						: null,
					action = TryRequestInstallation
				};
			}
			else
			{
				yield return new Command_ActionOverlay
				{
					defaultLabel = "SubcoreAutomation_CancelInstallation".Translate(),
					defaultDesc = "SubcoreAutomation_CancelInstallationDesc".Translate(),
					icon = GetSubcoreIcon(),
					overlayIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
					action = CancelInstallation
				};
			}

			if (Prefs.DevMode && DebugSettings.godMode)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Install Subcore",
					action = InstallSubcoreGodMode
				};
			}
		}

		/// <summary>
		/// Returns gizmos for removing an installed subcore.
		/// </summary>
		private IEnumerable<Gizmo> GetRemovalGizmos()
		{
			if (!_removalRequested)
			{
				yield return new Command_ActionOverlay
				{
					defaultLabel = "SubcoreAutomation_RemoveSubcore".Translate(),
					defaultDesc = "SubcoreAutomation_RemoveSubcoreDesc".Translate(),
					icon = GetSubcoreIcon(),
					overlayIcon = ContentFinder<Texture2D>.Get("UI/Buttons/Drop", true),
					action = RequestRemoval
				};
			}
			else
			{
				yield return new Command_ActionOverlay
				{
					defaultLabel = "SubcoreAutomation_CancelRemoval".Translate(),
					defaultDesc = "SubcoreAutomation_CancelRemovalDesc".Translate(),
					icon = GetSubcoreIcon(),
					overlayIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
					action = CancelRemoval
				};
			}

			if (Prefs.DevMode && DebugSettings.godMode)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Remove Subcore",
					action = () => RemoveSubcore(spawnSubcore: true, silent: false)
				};
			}
		}

		/// <summary>
		/// Attempts to request installation, showing error if no subcore available.
		/// </summary>
		private void TryRequestInstallation()
		{
			// In fallback mode, check for materials instead of subcore. The concrete subcore
			// def is null when Biotech is absent; the tier still selects the right materials.
			if (SubcoreFallback.IsActive)
			{
				if (!SubcoreFallback.HasEnoughMaterials(parent.Map, Props.tier))
				{
					Messages.Message(
						"SubcoreAutomation_NoFallbackMaterials".Translate(SubcoreFallback.GetMaterialsDescription(Props.tier)),
						parent,
						MessageTypeDefOf.RejectInput,
						false);
					SoundDefOf.ClickReject.PlayOneShotOnCamera();
					return;
				}
			}
			else
			{
				ThingDef subcoreDef = Props.SubcoreDef;
				if (subcoreDef == null)
					return;

				Thing subcore = FindSubcoreOnMap(subcoreDef);
				if (subcore == null)
				{
					Messages.Message(
						"SubcoreAutomation_NoSubcoreAvailable".Translate(subcoreDef.label),
						parent,
						MessageTypeDefOf.RejectInput,
						false);
					SoundDefOf.ClickReject.PlayOneShotOnCamera();
					return;
				}
			}

			RequestInstallation();
		}

		/// <summary>
		/// Check if mechtech research is complete or dev mode is enabled.
		/// </summary>
		protected bool CanShowAutomationGizmos()
		{
			if (Prefs.DevMode)
				return true;

			// In fallback mode, use Microelectronics research
			if (SubcoreFallback.IsActive)
			{
				return SubcoreFallback.IsFallbackResearchComplete();
			}

			// Check for BasicMechtech research
			ResearchProjectDef mechtech = DefDatabase<ResearchProjectDef>.GetNamed("BasicMechtech", errorOnFail: false);
			if (mechtech != null && mechtech.IsFinished)
				return true;

			return false;
		}

		/// <summary>
		/// Get the icon for the subcore type.
		/// </summary>
		protected Texture2D GetSubcoreIcon()
		{
			// In fallback mode, show the primary component icon for the tier.
			if (SubcoreFallback.IsActive)
			{
				var components = SubcoreFallback.GetFallbackComponents(Props.tier);
				return (components.Count > 0 ? components[0] : ThingDefOf.ComponentIndustrial).uiIcon;
			}

			if (Props.SubcoreDef != null)
				return Props.SubcoreDef.uiIcon;
			return ThingDefOf.ComponentIndustrial.uiIcon;
		}

		/// <summary>
		/// In fallback mode, returns the secondary component icon when a tier uses two
		/// component types (e.g. Regular = industrial + spacer), otherwise null.
		/// </summary>
		protected Texture2D GetSubcoreSecondaryIcon()
		{
			if (!SubcoreFallback.IsActive)
				return null;

			var components = SubcoreFallback.GetFallbackComponents(Props.tier);
			return components.Count > 1 ? components[1].uiIcon : null;
		}

		/// <summary>
		/// Find an available subcore on the map.
		/// </summary>
		public Thing FindSubcoreOnMap(ThingDef subcoreDef)
		{
			if (parent.Map == null)
				return null;

			return parent.Map.listerThings.ThingsOfDef(subcoreDef)
				.Find(t => t.Spawned
					&& !t.IsForbidden(Faction.OfPlayer)
					&& !t.Map.reservationManager.IsReservedByAnyoneOf(t, Faction.OfPlayer));
		}

		#endregion

	}
}
