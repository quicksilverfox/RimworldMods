using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using SubcoreAutomation.Core;
using SubcoreAutomation.Patches;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for defense automation - Turrets and Proximity Detector.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class DefenseHandler
	{
		#region Proximity Detector State

		// Per-instance state for proximity detector tracking
		private static readonly Dictionary<CompDefenseAutomation, ProximityState> _proximityStates =
			new Dictionary<CompDefenseAutomation, ProximityState>();

		private class ProximityState
		{
			public List<(Pawn pawn, IntVec3 lastKnownPos)> TrackedInvisibles = new List<(Pawn, IntVec3)>();
			public int LastScanTick;
		}

		// Constants
		private const int InvisibleScanInterval = 60; // Scan every 60 ticks (~1 second)
		private const float DetectorRadius = 19.9f; // Match vanilla proximity detector range

		// Static material for rendering (initialized on first use)
		private static Material _targetMarkerMat;
		private static Material TargetMarkerMat
		{
			get
			{
				if (_targetMarkerMat == null)
				{
					Texture2D tex = ContentFinder<Texture2D>.Get("UI/Overlays/TargetHighlight_Square", false)
						?? ContentFinder<Texture2D>.Get("UI/Overlays/SelectionBracket", false)
						?? BaseContent.BadTex;
					_targetMarkerMat = MaterialPool.MatFrom(tex, ShaderDatabase.MetaOverlay, new Color(1f, 0.3f, 0.3f, 0.7f));
				}
				return _targetMarkerMat;
			}
		}

		#endregion

		#region Proximity Detector Methods

		/// <summary>
		/// Performs one tick of invisible creature scanning.
		/// Should be called from CompTick when appropriate.
		/// </summary>
		public static void TryScanForInvisibles(CompDefenseAutomation comp)
		{
			// Get or create per-instance state
			if (!_proximityStates.TryGetValue(comp, out var state))
			{
				state = new ProximityState();
				_proximityStates[comp] = state;
			}

			int currentTick = Find.TickManager.TicksGame;
			if (currentTick - state.LastScanTick < InvisibleScanInterval)
				return;

			state.LastScanTick = currentTick;
			ScanForInvisibleCreatures(comp, state);
		}

		private static void ScanForInvisibleCreatures(CompDefenseAutomation comp, ProximityState state)
		{
			state.TrackedInvisibles.Clear();

			if (comp.parent.Map == null)
				return;

			foreach (Pawn pawn in comp.parent.Map.mapPawns.AllPawnsSpawned)
			{
				if (pawn.Position.DistanceTo(comp.parent.Position) > DetectorRadius)
					continue;

				if (!IsInvisibleCreature(pawn))
					continue;

				state.TrackedInvisibles.Add((pawn, pawn.Position));
			}
		}

		private static bool IsInvisibleCreature(Pawn pawn)
		{
			if (pawn == null || pawn.Dead)
				return false;

			// Read the hediff comp directly so our own IsPsychologicallyInvisible postfix
			// (which hides tracked pawns) does not break the scan.
			HediffComp_Invisibility comp;
			try
			{
				comp = pawn.GetInvisibilityComp();
			}
			catch
			{
				return false; // Anomaly DLC not present
			}
			return comp != null && !comp.PsychologicallyVisible;
		}

		/// <summary>
		/// Gets the tracked invisible creatures for a proximity detector.
		/// </summary>
		public static IReadOnlyList<(Pawn pawn, IntVec3 position)> GetTrackedInvisibles(CompDefenseAutomation comp)
		{
			if (_proximityStates.TryGetValue(comp, out var state))
				return state.TrackedInvisibles;
			return Array.Empty<(Pawn, IntVec3)>();
		}

		/// <summary>
		/// Draws invisible creature markers during PostDraw.
		/// </summary>
		public static void DrawInvisibleMarkers(CompDefenseAutomation comp)
		{
			if (!_proximityStates.TryGetValue(comp, out var state))
				return;

			if (state.TrackedInvisibles.Count == 0)
				return;

			// Draw pulsing target markers at tracked positions
			float pulse = 0.7f + (Mathf.Sin(Time.realtimeSinceStartup * 4f) * 0.3f);

			foreach (var tracked in state.TrackedInvisibles)
			{
				Vector3 drawPos = tracked.lastKnownPos.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);

				// Draw targeting reticle with pulsing effect
				Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(pulse, 1f, pulse));
				Graphics.DrawMesh(MeshPool.plane10, matrix, TargetMarkerMat, 0);
			}
		}

		/// <summary>
		/// Performs emergency reveal of all tracked invisible creatures.
		/// </summary>
		public static void DoEmergencyReveal(CompDefenseAutomation comp)
		{
			if (!_proximityStates.TryGetValue(comp, out var state))
				return;

			if (state.TrackedInvisibles.Count == 0)
				return;

			int revealedCount = 0;
			foreach (var tracked in state.TrackedInvisibles)
			{
				try
				{
					if (tracked.pawn == null || !tracked.pawn.Spawned)
						continue;

					HediffComp_Invisibility invisComp = tracked.pawn.GetInvisibilityComp();
					if (invisComp != null)
					{
						invisComp.DisruptInvisibility();
						revealedCount++;
					}
				}
				catch (Exception ex)
				{
					Log.Warning($"SubcoreAutomation: Error revealing creature: {ex.Message}");
				}
			}

			// Clear tracked list after reveal
			state.TrackedInvisibles.Clear();

			// Cause breakdown
			comp.CachedBreakdownable?.DoBreakdown();

			// Notify player
			if (revealedCount > 0)
			{
				Messages.Message(
					"SubcoreAutomation_DetectorOverloaded".Translate(revealedCount),
					comp.parent,
					MessageTypeDefOf.NeutralEvent);
			}
		}

		/// <summary>
		/// Cleans up proximity detector state when comp is destroyed.
		/// </summary>
		public static void CleanupProximityState(CompDefenseAutomation comp)
		{
			_proximityStates.Remove(comp);
		}

		#endregion

		#region Turret Methods

		/// <summary>
		/// Returns the inspect string for an automated turret.
		/// </summary>
		public static string GetTurretInspectString(CompDefenseAutomation comp)
		{
			if (SubcoreAutomationMod.CombatExtendedLoaded)
				return "SubcoreAutomation_CEDetected".Translate();

			string defName = comp.parent.def.defName;
			float accuracyBonus = TurretPatches.GetAccuracyBonusForSubcore(comp, defName);
			float warmupReduction = TurretPatches.GetWarmupReductionForSubcore(comp, defName);
			bool ffPrevention = TurretPatches.IsFriendlyFirePreventionEnabled(defName);

			StringBuilder sb = new StringBuilder();
			sb.Append("SubcoreAutomation_TurretBonuses".Translate(accuracyBonus.ToStringPercent(), warmupReduction.ToStringPercent()));
			if (ffPrevention)
			{
				sb.AppendLine();
				sb.Append("SubcoreAutomation_FriendlyFirePreventionShort".Translate());
			}
			return sb.ToString();
		}

		/// <summary>
		/// Returns the inspect string for an automated proximity detector.
		/// </summary>
		public static string GetProximityDetectorInspectString(CompDefenseAutomation comp)
		{
			var trackedInvisibles = GetTrackedInvisibles(comp);
			string status = trackedInvisibles.Count > 0
				? "SubcoreAutomation_TrackingCreatures".Translate(trackedInvisibles.Count)
				: "SubcoreAutomation_NoCreaturesDetected".Translate();

			return new InspectStringBuilder("SubcoreAutomation_AutomatedSimple".Translate())
				.AppendFeature(status)
				.ToString();
		}

		/// <summary>
		/// Returns the gizmos for an automated turret (Rocketswarm only).
		/// Regular turrets use vanilla targeting via CanSetForcedTarget patch.
		/// </summary>
		public static IEnumerable<Gizmo> GetTurretGizmos(CompDefenseAutomation comp)
		{
			if (!(comp.parent is Building_TurretGun turret))
				yield break;

			bool isRocketswarm = comp.parent.def.defName == MachineDefNames.TurretRocketswarmLauncher;

			if (isRocketswarm)
			{
				// Rocketswarm: Add activation gizmo using turret's verb for proper targeting
				var attackVerb = turret.AttackVerb;
				if (attackVerb != null)
				{
					yield return new Command_Action
					{
						defaultLabel = "SubcoreAutomation_ActivateTurret".Translate(),
						defaultDesc = "SubcoreAutomation_ActivateTurretDesc".Translate(),
						icon = TexCommand.Attack,
						hotKey = KeyBindingDefOf.Misc4,
						action = delegate
						{
							// Use the verb's targeting (supports locations, pawns, buildings)
							Find.Targeter.BeginTargeting(attackVerb, null, allowNonSelectedTargetingSource: true, null, delegate
							{
								// After targeting completes, activate the burst so the rocketswarm fires
								turret.TryActivateBurst();
							});
						}
					};
				}
			}
		}

		#endregion

		#region Proximity Detector Gizmos

		/// <summary>
		/// Returns the gizmos for an automated proximity detector.
		/// </summary>
		public static IEnumerable<Gizmo> GetProximityDetectorGizmos(CompDefenseAutomation comp)
		{
			// Emergency reveal gizmo
			Command_Action revealCmd = new Command_Action
			{
				defaultLabel = "SubcoreAutomation_EmergencyReveal".Translate(),
				defaultDesc = "SubcoreAutomation_EmergencyRevealDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/FireAtWill", false) ?? TexCommand.Attack,
				action = delegate
				{
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"SubcoreAutomation_EmergencyRevealConfirm".Translate(),
						() => DoEmergencyReveal(comp),
						destructive: true));
				}
			};

			var trackedInvisibles = GetTrackedInvisibles(comp);
			if (trackedInvisibles.Count == 0)
			{
				revealCmd.Disable("SubcoreAutomation_NoCreaturesTracked".Translate());
			}

			yield return revealCmd;
		}

		#endregion

		/// <summary>
		/// Returns the benefits description for proximity detector automation tooltip.
		/// </summary>
		public static string GetProximityDetectorBenefitsDescription()
		{
			return "\n\n" + "SubcoreAutomation_ProximityDetectorBenefits".Translate();
		}
	}
}
