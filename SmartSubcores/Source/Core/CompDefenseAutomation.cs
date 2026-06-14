using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Handlers;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Subcore automation component for defense machines.
	/// Handles: All Turrets, ProximityDetector.
	/// </summary>
	public class CompDefenseAutomation : CompSubcoreAutomationBase
	{
		#region Defense-Specific State

		private Building_TurretGun _cachedTurret;
		private bool _isProximityDetector;

		#endregion

		#region Properties

		public Building_TurretGun CachedTurret => _cachedTurret;
		public bool IsProximityDetector => _isProximityDetector;

		/// <summary>
		/// Gets the list of tracked invisible creatures from the defense handler.
		/// </summary>
		public IReadOnlyList<(Pawn pawn, IntVec3 position)> TrackedInvisibles => DefenseHandler.GetTrackedInvisibles(this);

		#endregion

		#region Overrides

		protected override void PostSpawnSetupMachineSpecific(bool respawningAfterLoad)
		{
			_cachedTurret = parent as Building_TurretGun;
			_isProximityDetector = parent.def.defName == MachineDefNames.ProximityDetector;
		}

		protected override void DoMachineSpecificTick()
		{
			// Turrets get accuracy/warmup bonuses via Harmony patches
			
			// Proximity detector: scan for invisible creatures
			if (_isProximityDetector)
			{
				DefenseHandler.TryScanForInvisibles(this);
			}
		}

		protected override string GetMachineSpecificInspectString()
		{
			if (_cachedTurret != null)
			{
				return DefenseHandler.GetTurretInspectString(this);
			}
			if (_isProximityDetector)
			{
				return DefenseHandler.GetProximityDetectorInspectString(this);
			}
			return "SubcoreAutomation_AutomatedSimple".Translate();
		}

		protected override IEnumerable<Gizmo> GetMachineSpecificGizmos()
		{
			if (_cachedTurret != null)
			{
				foreach (var gizmo in DefenseHandler.GetTurretGizmos(this))
				{
					yield return gizmo;
				}
			}
			if (_isProximityDetector)
			{
				foreach (var gizmo in DefenseHandler.GetProximityDetectorGizmos(this))
				{
					yield return gizmo;
				}
			}
		}

		protected override string GetMachineSpecificBenefitsDescription()
		{
			if (_cachedTurret != null)
			{
				return "\n\n" + "SubcoreAutomation_TurretBenefitsGeneric".Translate();
			}
			if (_isProximityDetector)
			{
				return DefenseHandler.GetProximityDetectorBenefitsDescription();
			}
			return "";
		}

		protected override void OnDestroyMachineSpecific(DestroyMode mode, Map previousMap)
		{
			if (_isProximityDetector)
			{
				DefenseHandler.CleanupProximityState(this);
			}
		}

		protected override void PostDrawMachineSpecific()
		{
			if (_isProximityDetector)
			{
				DefenseHandler.DrawInvisibleMarkers(this);
			}
		}

		#endregion
	}
}
