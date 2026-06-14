using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Piloting Console automation (Odyssey DLC).
	/// Features:
	/// - Autopilot launch with 150% piloting ability (handled in PilotConsoleHandler)
	/// - Mishap intervention during assisted (ritual) launches
	/// </summary>
	[StaticConstructorOnStartup]
	public static class PilotConsolePatches
	{
		private static bool _odysseyLoaded = false;
		private static bool _patchesApplied = false;

		// Cached reflection for Odyssey types
		private static Type _compPilotConsoleType;
		private static Type _buildingGravEngineType;
		private static Type _launchInfoType;
		private static Type _gravshipControllerType;
		private static Type _gravshipType;
		private static FieldInfo _engineField;  // engine is a field in base class CompGravshipFacility
		private static FieldInfo _launchInfoField;
		private static FieldInfo _qualityField;
		private static FieldInfo _doNegativeOutcomeField;
		private static FieldInfo _gravshipField;  // gravship field in WorldComponent_GravshipController
		private static PropertyInfo _engineProperty;  // Engine property in Gravship

		static PilotConsolePatches()
		{
			// Check if Odyssey DLC is loaded (use package ID, not display name)
			_odysseyLoaded = ModsConfig.IsActive("Ludeon.RimWorld.Odyssey");

			if (!_odysseyLoaded)
				return;

			// Check settings
			if (SubcoreAutomationMod.Settings != null &&
				!SubcoreAutomationMod.Settings.pilotConsoleFeaturesEnabled)
				return;

			try
			{
				var harmony = new Harmony("SubcoreAutomation.PilotConsolePatches");

				// Cache Odyssey types via reflection
				_compPilotConsoleType = AccessTools.TypeByName("RimWorld.CompPilotConsole");
				_buildingGravEngineType = AccessTools.TypeByName("RimWorld.Building_GravEngine");
				_launchInfoType = AccessTools.TypeByName("RimWorld.LaunchInfo");

				if (_compPilotConsoleType == null)
					Log.Error("[SubcoreAutomation] Pilot Console patches BROKEN: CompPilotConsole type not found!");
				else
					_engineField = AccessTools.Field(_compPilotConsoleType, "engine");

				if (_buildingGravEngineType == null)
					Log.Error("[SubcoreAutomation] Pilot Console patches BROKEN: Building_GravEngine type not found!");
				else
					_launchInfoField = AccessTools.Field(_buildingGravEngineType, "launchInfo");

				if (_launchInfoType == null)
					Log.Error("[SubcoreAutomation] Pilot Console patches BROKEN: LaunchInfo type not found!");
				else
				{
					_qualityField = AccessTools.Field(_launchInfoType, "quality");
					_doNegativeOutcomeField = AccessTools.Field(_launchInfoType, "doNegativeOutcome");
				}

				// Cache WorldComponent_GravshipController and Gravship types
				_gravshipControllerType = AccessTools.TypeByName("Verse.WorldComponent_GravshipController");
				_gravshipType = AccessTools.TypeByName("RimWorld.Planet.Gravship");

				if (_gravshipControllerType == null)
					Log.Error("[SubcoreAutomation] Pilot Console patches BROKEN: WorldComponent_GravshipController type not found!");
				else
					_gravshipField = AccessTools.Field(_gravshipControllerType, "gravship");

				if (_gravshipType == null)
					Log.Error("[SubcoreAutomation] Pilot Console patches BROKEN: Gravship type not found!");
				else
					_engineProperty = AccessTools.Property(_gravshipType, "Engine");

				// Patch LandingEnded to potentially prevent mishaps with subcore intervention
				if (_gravshipControllerType != null)
				{
					var landingEndedMethod = AccessTools.Method(_gravshipControllerType, "LandingEnded");
					if (landingEndedMethod != null)
					{
						harmony.Patch(landingEndedMethod,
							prefix: new HarmonyMethod(typeof(PilotConsolePatches),
								nameof(LandingEnded_Prefix)));
						_patchesApplied = true;
					}
					else
						Log.Error("[SubcoreAutomation] Pilot Console patches BROKEN: LandingEnded method not found!");
				}

				if (_patchesApplied)
				{
					Log.Message("[SubcoreAutomation] Piloting Console patches applied (Odyssey DLC)");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply Piloting Console patches: {ex.Message}");
			}
		}

		/// <summary>
		/// Checks if patches are enabled and Odyssey is loaded.
		/// </summary>
		public static bool ArePatchesEnabled()
		{
			if (!_odysseyLoaded)
				return false;
			return SubcoreAutomationMod.Settings?.pilotConsoleFeaturesEnabled ?? true;
		}

		/// <summary>
		/// Gets the CompPilotConsole type (cached).
		/// </summary>
		public static Type CompPilotConsoleType => _compPilotConsoleType;

		/// <summary>
		/// Gets the engine field info (cached).
		/// </summary>
		public static FieldInfo EngineField => _engineField;

		/// <summary>
		/// Checks if a pilot console has an enhanced subcore installed.
		/// </summary>
		public static bool HasEnhancedSubcore(Thing pilotConsole)
		{
			if (!ArePatchesEnabled())
				return false;

			if (pilotConsole == null)
				return false;

			var comp = pilotConsole.TryGetComp<CompSubcoreAutomationBase>();
			return comp != null && comp.SubcoreInstalled && comp.IsAutomationEnabled;
		}

		/// <summary>
		/// Gets the engine from a pilot console using reflection.
		/// </summary>
		public static Thing GetEngineFromConsole(Thing pilotConsole)
		{
			if (_compPilotConsoleType == null || _engineField == null)
				return null;

			var twc = pilotConsole as ThingWithComps;
			if (twc == null)
				return null;

			foreach (var thingComp in twc.AllComps)
			{
				if (_compPilotConsoleType.IsInstanceOfType(thingComp))
				{
					return _engineField.GetValue(thingComp) as Thing;
				}
			}

			return null;
		}

		/// <summary>
		/// Checks if any pilot console connected to this engine has an enhanced subcore.
		/// </summary>
		private static bool HasEnhancedConsoleForEngine(Thing engine)
		{
			if (engine?.Map == null)
				return false;

			var pilotConsoleDef = DefDatabase<ThingDef>.GetNamed("PilotConsole", false);
			if (pilotConsoleDef == null)
				return false;

			foreach (var building in engine.Map.listerBuildings.AllBuildingsColonistOfDef(pilotConsoleDef))
			{
				// Check if this console is connected to the engine
				Thing consoleEngine = GetEngineFromConsole(building);
				if (consoleEngine == engine && HasEnhancedSubcore(building))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Prefix for LandingEnded - potentially prevent mishaps with subcore intervention.
		/// When a landing mishap is about to happen and a subcore is installed,
		/// there's a configurable chance for the AI to intervene and prevent it.
		/// </summary>
		public static void LandingEnded_Prefix(object __instance)
		{
			try
			{
				if (!ArePatchesEnabled())
					return;

				// Get the gravship from the controller
				if (_gravshipField == null || _engineProperty == null)
					return;

				object gravship = _gravshipField.GetValue(__instance);
				if (gravship == null)
					return;

				// Get the engine from the gravship
				Thing engine = _engineProperty.GetValue(gravship) as Thing;
				if (engine == null)
					return;

				// Check if any pilot console connected to this engine has enhanced subcore
				if (!HasEnhancedConsoleForEngine(engine))
					return;

				// Get launchInfo from engine
				if (_launchInfoField == null || _doNegativeOutcomeField == null)
					return;

				object launchInfo = _launchInfoField.GetValue(engine);
				if (launchInfo == null)
					return;

				// Check if a mishap is scheduled
				bool doNegativeOutcome = (bool)_doNegativeOutcomeField.GetValue(launchInfo);
				if (!doNegativeOutcome)
					return; // No mishap scheduled, nothing to do

				// Get intervention chance from settings (default 50%)
				float interventionChance = SubcoreAutomationMod.Settings?.pilotConsoleInterventionChance ?? 0.5f;

				// Roll for intervention
				if (Rand.Chance(interventionChance))
				{
					// Subcore AI intervenes and prevents the mishap
					_doNegativeOutcomeField.SetValue(launchInfo, false);
					_launchInfoField.SetValue(engine, launchInfo);

					// Get quality for the notification
					float quality = _qualityField != null ? (float)_qualityField.GetValue(launchInfo) : 0f;

					// Send notification
					Messages.Message(
						"SubcoreAutomation_AutopilotIntervention".Translate(),
						engine,
						MessageTypeDefOf.PositiveEvent);

					Log.Message($"[SubcoreAutomation] Subcore AI prevented landing mishap (quality was {quality:F2}, intervention chance {interventionChance:P0})");
				}
				else
				{
					Log.Message($"[SubcoreAutomation] Subcore AI failed to prevent landing mishap (intervention chance {interventionChance:P0})");
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in LandingEnded prefix: {ex.Message}", 98372654);
			}
		}

		/// <summary>
		/// Checks if an engine has an enhanced pilot console for intervention.
		/// This version searches globally since the engine may have been moved to a Gravship.
		/// </summary>
		public static bool HasEnhancedConsoleForGravship(Thing engine)
		{
			// The engine on a gravship won't have a Map, but we can check
			// if the engine has subcore automation data saved
			if (engine == null)
				return false;

			// Try to get subcore automation from any connected console
			// Since we're in a gravship, we need to iterate through all things
			var twc = engine as ThingWithComps;
			if (twc == null)
				return false;

			// Check engine's linked facilities or iterate
			// For now, use the Map-based check if available
			if (engine.Map != null)
			{
				return HasEnhancedConsoleForEngine(engine);
			}

			// If engine is in a gravship (no map), we need a different approach
			// The gravship contains all the buildings including pilot consoles
			// For simplicity, just return true if we got here - the patch already verified
			return true;
		}

		/// <summary>
		/// Sets the quality on a launch info object.
		/// Called from PilotConsoleHandler for autopilot launches.
		/// </summary>
		public static void SetLaunchQuality(Thing engine, float quality)
		{
			if (_launchInfoField == null || _qualityField == null)
				return;

			object launchInfo = _launchInfoField.GetValue(engine);
			if (launchInfo != null)
			{
				_qualityField.SetValue(launchInfo, quality);
				_launchInfoField.SetValue(engine, launchInfo);
			}
		}
	}
}
