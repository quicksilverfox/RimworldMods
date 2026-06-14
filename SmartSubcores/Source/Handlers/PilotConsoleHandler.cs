using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SubcoreAutomation.Core;
using SubcoreAutomation.Patches;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for Piloting Console autopilot feature.
	/// Adds gizmo to launch gravship directly without the ritual.
	/// Uses the same approach as the dev mode "Launch instantly" gizmo.
	/// </summary>
	public static class PilotConsoleHandler
	{
		// Cached reflection for Odyssey types
		private static Type _compPilotConsoleType;
		private static Type _buildingGravEngineType;
		private static Type _launchInfoType;
		private static FieldInfo _engineField;  // engine is a field in base class CompGravshipFacility
		private static FieldInfo _launchInfoField;
		private static MethodInfo _startChoosingDestinationMethod;
		private static FieldInfo _cooldownCompleteTickField;
		private static PropertyInfo _totalFuelProperty;
		private static bool _reflectionInitialized = false;

		private static void InitializeReflection()
		{
			if (_reflectionInitialized)
				return;

			try
			{
				_compPilotConsoleType = AccessTools.TypeByName("RimWorld.CompPilotConsole");
				_buildingGravEngineType = AccessTools.TypeByName("RimWorld.Building_GravEngine");
				_launchInfoType = AccessTools.TypeByName("RimWorld.LaunchInfo");

				if (_compPilotConsoleType != null)
				{
					_engineField = AccessTools.Field(_compPilotConsoleType, "engine");
					_startChoosingDestinationMethod = AccessTools.Method(_compPilotConsoleType, 
						"StartChoosingDestination_NewTemp", new[] { typeof(bool) });
				}

				if (_buildingGravEngineType != null)
				{
					_launchInfoField = AccessTools.Field(_buildingGravEngineType, "launchInfo");
					_cooldownCompleteTickField = AccessTools.Field(_buildingGravEngineType, "cooldownCompleteTick");
					_totalFuelProperty = AccessTools.Property(_buildingGravEngineType, "TotalFuel");
				}
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubcoreAutomation] Could not cache Odyssey types: {ex.Message}");
			}

			_reflectionInitialized = true;
		}

		/// <summary>
		/// Gets the CompPilotConsole from a pilot console building.
		/// </summary>
		private static object GetCompPilotConsole(Thing pilotConsole)
		{
			if (_compPilotConsoleType == null)
				return null;

			var twc = pilotConsole as ThingWithComps;
			if (twc == null)
				return null;

			foreach (var comp in twc.AllComps)
			{
				if (_compPilotConsoleType.IsInstanceOfType(comp))
				{
					return comp;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the engine from a CompPilotConsole.
		/// </summary>
		private static Thing GetEngineFromComp(object compPilotConsole)
		{
			if (_engineField == null || compPilotConsole == null)
				return null;

			return _engineField.GetValue(compPilotConsole) as Thing;
		}

		/// <summary>
		/// Gets autopilot gizmos for the piloting console.
		/// Called from CompSubcoreAutomation.CompGetGizmosExtra.
		/// </summary>
		public static IEnumerable<Gizmo> GetAutopilotGizmos(
			Thing pilotConsole,
			CompSubcoreAutomationBase comp)
		{
			if (!PilotConsolePatches.ArePatchesEnabled())
				yield break;

			if (!comp.SubcoreInstalled || !comp.IsAutomationEnabled)
				yield break;

			InitializeReflection();

			// Get the CompPilotConsole
			object compPilotConsole = GetCompPilotConsole(pilotConsole);
			if (compPilotConsole == null)
				yield break;

			// Get the engine from CompPilotConsole
			Thing engine = GetEngineFromComp(compPilotConsole);

			// Autopilot launch gizmo
			var launchCmd = new Command_Action
			{
				defaultLabel = "SubcoreAutomation_AutopilotLaunch".Translate(),
				defaultDesc = "SubcoreAutomation_AutopilotLaunchDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip", false)
					?? ContentFinder<Texture2D>.Get("UI/Commands/LoadTransporter", false)
					?? TexCommand.Attack,
				action = delegate
				{
					// Show confirmation dialog before launching
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"SubcoreAutomation_AutopilotConfirm".Translate(),
						delegate
						{
							ExecuteAutopilotLaunch(compPilotConsole, engine, pilotConsole);
						},
						destructive: false,
						title: "SubcoreAutomation_AutopilotLaunch".Translate()));
				}
			};

			// Check if launch is possible
			string cannotLaunchReason = GetCannotLaunchReason(engine);
			if (cannotLaunchReason != null)
			{
				launchCmd.Disable(cannotLaunchReason);
			}

			yield return launchCmd;
		}

		/// <summary>
		/// Gets the reason why the gravship cannot launch, or null if it can.
		/// </summary>
		private static string GetCannotLaunchReason(Thing engine)
		{
			if (engine == null)
			{
				return "SubcoreAutomation_AutopilotNoEngine".Translate();
			}

			try
			{
				// Check fuel
				if (_totalFuelProperty != null)
				{
					float fuel = (float)_totalFuelProperty.GetValue(engine);
					if (fuel < 10f)
					{
						return "CannotLaunchNotEnoughFuel".Translate().CapitalizeFirst();
					}
				}

				// Check cooldown
				if (_cooldownCompleteTickField != null)
				{
					int cooldownTick = (int)_cooldownCompleteTickField.GetValue(engine);
					if (GenTicks.TicksGame < cooldownTick)
					{
						int ticksLeft = cooldownTick - GenTicks.TicksGame;
						return "CannotLaunchOnCooldown".Translate(ticksLeft.ToStringTicksToPeriod()).CapitalizeFirst();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubcoreAutomation] Error checking launch status: {ex.Message}");
				return "SubcoreAutomation_AutopilotNotReady".Translate();
			}

			return null;
		}

		/// <summary>
		/// Executes the autopilot launch using the same approach as dev mode.
		/// Sets launchInfo with 150% quality, then calls StartChoosingDestination_NewTemp.
		/// </summary>
		private static void ExecuteAutopilotLaunch(
			object compPilotConsole,
			Thing engine,
			Thing pilotConsole)
		{
			try
			{
				if (engine == null || _launchInfoField == null || _launchInfoType == null)
				{
					Messages.Message(
						"SubcoreAutomation_AutopilotNoEngine".Translate(),
						pilotConsole,
						MessageTypeDefOf.RejectInput);
					return;
				}

				// Create LaunchInfo with 150% quality (like dev mode does with 100%)
				// LaunchInfo is a struct with fields: quality, doNegativeOutcome
				object launchInfo = Activator.CreateInstance(_launchInfoType);
				
				var qualityField = AccessTools.Field(_launchInfoType, "quality");
				var negativeOutcomeField = AccessTools.Field(_launchInfoType, "doNegativeOutcome");

				if (qualityField != null)
					qualityField.SetValue(launchInfo, 1.5f);
				if (negativeOutcomeField != null)
					negativeOutcomeField.SetValue(launchInfo, false);

				// Set the launchInfo on the engine
				_launchInfoField.SetValue(engine, launchInfo);

				// Call StartChoosingDestination_NewTemp(true) to open destination picker
				if (_startChoosingDestinationMethod != null)
				{
					_startChoosingDestinationMethod.Invoke(compPilotConsole, new object[] { true });
				}
				else
				{
					// Fallback: try the older method name
					var fallbackMethod = AccessTools.Method(_compPilotConsoleType, "StartChoosingDestination");
					if (fallbackMethod != null)
					{
						fallbackMethod.Invoke(compPilotConsole, null);
					}
					else
					{
						Messages.Message(
							"SubcoreAutomation_AutopilotFailed".Translate(),
							pilotConsole,
							MessageTypeDefOf.NegativeEvent);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Autopilot launch failed: {ex.Message}");
				Messages.Message(
					"SubcoreAutomation_AutopilotFailed".Translate(),
					pilotConsole,
					MessageTypeDefOf.NegativeEvent);
			}
		}
	}
}
