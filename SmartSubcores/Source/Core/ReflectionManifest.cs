using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Central manifest of all reflected fields and methods used by the mod.
	/// This provides a single point of maintenance when RimWorld updates change internal APIs.
	/// All reflection is cached here on startup for performance.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class ReflectionManifest
	{
		/// <summary>
		/// List of failed reflection lookups for diagnostics.
		/// </summary>
		public static List<string> FailedLookups { get; } = new List<string>();

		// ============================================
		// BUILDING_COOLER (CoolerPatches.cs)
		// ============================================

		/// <summary>
		/// CompTempControl.operatingAtHighPower - Private field for visual feedback.
		/// </summary>
		public static FieldInfo CompTempControl_operatingAtHighPower { get; private set; }

		// ============================================
		// TURRETS (TurretPatches.cs)
		// ============================================

		/// <summary>
		/// ShotReport.factorFromShooterAndDist - Private field for accuracy factor.
		/// HIGH RISK: Internal combat calculations.
		/// </summary>
		public static FieldInfo ShotReport_factorFromShooterAndDist { get; private set; }

		// ============================================
		// COMPSCANNER (CompSubcoreAutomation)
		// ============================================

		/// <summary>
		/// CompScanner.lastScanTick - Private field for last scan tick.
		/// </summary>
		public static FieldInfo Scanner_lastScanTick { get; private set; }

		/// <summary>
		/// CompScanner.daysWorkingSinceLastFinding - Private field for days worked.
		/// </summary>
		public static FieldInfo Scanner_daysWorking { get; private set; }

		/// <summary>
		/// CompScanner.TickDoesFind - Private method to check if scan finds something.
		/// </summary>
		public static MethodInfo Scanner_TickDoesFind { get; private set; }

		/// <summary>
		/// CompScanner.DoFind - Private method to execute find action.
		/// </summary>
		public static MethodInfo Scanner_DoFind { get; private set; }

		// ============================================
		// INITIALIZATION
		// ============================================

		static ReflectionManifest()
		{
			try
			{
				CompTempControl_operatingAtHighPower = TryGetField(
					typeof(CompTempControl), "operatingAtHighPower", "CompTempControl.operatingAtHighPower");

				ShotReport_factorFromShooterAndDist = TryGetField(
					typeof(ShotReport), "factorFromShooterAndDist", "ShotReport.factorFromShooterAndDist");

				Scanner_lastScanTick = TryGetField(
					typeof(CompScanner), "lastScanTick", "CompScanner.lastScanTick");
				Scanner_daysWorking = TryGetField(
					typeof(CompScanner), "daysWorkingSinceLastFinding", "CompScanner.daysWorkingSinceLastFinding");
				Scanner_TickDoesFind = TryGetMethod(
					typeof(CompScanner), "TickDoesFind", "CompScanner.TickDoesFind");
				Scanner_DoFind = TryGetMethod(
					typeof(CompScanner), "DoFind", "CompScanner.DoFind");

				if (FailedLookups.Count > 0)
				{
					Log.Warning($"[SubcoreAutomation] ReflectionManifest: {FailedLookups.Count} lookups failed. " +
						$"Some features may not work correctly:\n  - {string.Join("\n  - ", FailedLookups)}");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] ReflectionManifest initialization failed: {ex}");
			}
		}

		// ============================================
		// HELPER METHODS
		// ============================================

		private static FieldInfo TryGetField(Type type, string fieldName, string displayName)
		{
			var field = AccessTools.Field(type, fieldName);
			if (field == null)
				FailedLookups.Add(displayName);
			return field;
		}

		private static MethodInfo TryGetMethod(Type type, string methodName, string displayName)
		{
			var method = AccessTools.Method(type, methodName);
			if (method == null)
				FailedLookups.Add(displayName);
			return method;
		}
	}
}
