using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Compat
{
	/// <summary>
	/// Harmony patches for Pawnmorpher's MutationSequencer integration.
	/// These patches make the MutationSequencerComp work with CompSubcoreAutomation.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class PawnmorpherPatches
	{
		private static bool _pawnmorpherLoaded;
		private static Type _mutationSequencerCompType;
		private static Type _compScannerType;
		private static Type _chamberDatabaseType;
		private static Type _mutationDefType;
		private static FieldInfo _targetAnimalField;
		private static FieldInfo _animalSequencedField;
		private static FieldInfo _lastScanTickField;
		private static FieldInfo _lastUserSpeedField;
		private static FieldInfo _daysWorkingField;
		private static FieldInfo _propsField;
		private static FieldInfo _genebankField;
		private static MethodInfo _tickDoesFind;
		private static MethodInfo _doFindMethod;
		private static PropertyInfo _taggedAnimalsProperty;
		private static PropertyInfo _storedMutationsProperty;
		private static PropertyInfo _canTagProperty;
		private static MethodInfo _getAllMutationsFromMethod;

		static PawnmorpherPatches()
		{
			_pawnmorpherLoaded = ModsConfig.IsActive("tachyonite.pawnmorpherpublic");
			if (!_pawnmorpherLoaded)
				return;

			try
			{
				// Get the MutationSequencerComp type
				_mutationSequencerCompType = AccessTools.TypeByName("Pawnmorph.ThingComps.MutationSequencerComp");
				_compScannerType = AccessTools.TypeByName("RimWorld.CompScanner");
				_chamberDatabaseType = AccessTools.TypeByName("Pawnmorph.Chambers.ChamberDatabase");
				_mutationDefType = AccessTools.TypeByName("Pawnmorph.Hediffs.MutationDef");

				if (_mutationSequencerCompType == null)
				{
					Log.Warning("[SubcoreAutomation] Could not find MutationSequencerComp type.");
					return;
				}

				// Cache reflection info from CompScanner (base class)
				_lastScanTickField = AccessTools.Field(_compScannerType, "lastScanTick");
				_lastUserSpeedField = AccessTools.Field(_compScannerType, "lastUserSpeed");
				_daysWorkingField = AccessTools.Field(_compScannerType, "daysWorkingSinceLastFinding");
				_propsField = AccessTools.Field(_compScannerType, "props");
				_tickDoesFind = AccessTools.Method(_compScannerType, "TickDoesFind");

				// Cache reflection info from MutationSequencerComp
				_targetAnimalField = AccessTools.Field(_mutationSequencerCompType, "_targetAnimal");
				_animalSequencedField = AccessTools.Field(_mutationSequencerCompType, "_animalSequenced");
				_genebankField = AccessTools.Field(_mutationSequencerCompType, "_genebank");
				_doFindMethod = AccessTools.Method(_mutationSequencerCompType, "DoFind");

				// Cache reflection info from ChamberDatabase
				if (_chamberDatabaseType != null)
				{
					_taggedAnimalsProperty = AccessTools.Property(_chamberDatabaseType, "TaggedAnimals");
					_storedMutationsProperty = AccessTools.Property(_chamberDatabaseType, "StoredMutations");
					_canTagProperty = AccessTools.Property(_chamberDatabaseType, "CanTag");
				}

				// Cache GetAllMutationsFrom extension method from DatabaseUtilities
				var databaseUtilitiesType = AccessTools.TypeByName("Pawnmorph.Genebank.DatabaseUtilities");
				if (databaseUtilitiesType != null)
				{
					_getAllMutationsFromMethod = AccessTools.Method(databaseUtilitiesType, "GetAllMutationsFrom");
				}

				var harmony = new Harmony("Pawnmorpher.SubcoreAutomation.Compat");

				// Patch the CanUseNow property getter - use DeclaredOnly to avoid ambiguous match
				var canUseNowProperty = _mutationSequencerCompType.GetProperty("CanUseNow",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
				if (canUseNowProperty != null)
				{
					var canUseNowGetter = canUseNowProperty.GetGetMethod();
					if (canUseNowGetter != null)
					{
						harmony.Patch(canUseNowGetter, postfix: new HarmonyMethod(typeof(PawnmorpherPatches), nameof(CanUseNow_Postfix)));
					}
				}

				// Patch the Used method with a prefix to skip when subcore is installed
				var usedMethod = AccessTools.Method(_mutationSequencerCompType, "Used");
				if (usedMethod != null)
				{
					harmony.Patch(usedMethod, prefix: new HarmonyMethod(typeof(PawnmorpherPatches), nameof(Used_Prefix)));
				}

				// Patch CompInspectStringExtra to provide clean output for automated sequencers
				var inspectMethod = AccessTools.Method(_mutationSequencerCompType, "CompInspectStringExtra");
				if (inspectMethod != null)
				{
					harmony.Patch(inspectMethod, prefix: new HarmonyMethod(typeof(PawnmorpherPatches), nameof(CompInspectStringExtra_Prefix)));
				}

				// Patch ThingComp.CompTick - the MutationSequencer uses tickerType="Normal" so CompTick is called.
				// Our postfix filters for MutationSequencerComp instances and only processes every 250 ticks.
				var compTickMethod = AccessTools.Method(typeof(ThingComp), "CompTick");
				if (compTickMethod != null)
				{
					harmony.Patch(compTickMethod, postfix: new HarmonyMethod(typeof(PawnmorpherPatches), nameof(CompTick_Postfix)));
				}
				else
				{
					Log.Warning("[SubcoreAutomation] Could not find ThingComp.CompTick method.");
				}

				Log.Message("[SubcoreAutomation] Pawnmorpher integration patches applied.");
			}
			catch (Exception ex)
			{
				Log.Error($"[SubcoreAutomation] Failed to apply Pawnmorpher patches: {ex}");
			}
		}

		/// <summary>
		/// Postfix for CanUseNow - return false if subcore is installed (prevent manual operation).
		/// </summary>
		public static void CanUseNow_Postfix(ThingComp __instance, ref bool __result)
		{
			if (!__result)
				return;

			// Check if subcore is installed
			var automationComp = __instance.parent?.TryGetComp<CompSubcoreAutomationBase>();
			if (automationComp != null && automationComp.SubcoreInstalled)
			{
				__result = false;
			}
		}

		/// <summary>
		/// Prefix for Used - skip entirely if subcore is installed (prevent pawn interference).
		/// </summary>
		public static bool Used_Prefix(ThingComp __instance)
		{
			var automationComp = __instance.parent?.TryGetComp<CompSubcoreAutomationBase>();
			if (automationComp != null && automationComp.SubcoreInstalled)
			{
				return false; // Skip original method
			}
			return true; // Run original method
		}

		/// <summary>
		/// Prefix for CompInspectStringExtra - provide clean output for automated sequencers.
		/// </summary>
		public static bool CompInspectStringExtra_Prefix(ThingComp __instance, ref string __result)
		{
			var automationComp = __instance.parent?.TryGetComp<CompSubcoreAutomationBase>();
			if (automationComp == null || !automationComp.SubcoreInstalled)
				return true; // Run original method

			// Build our own inspect string for automated sequencers
			if (_targetAnimalField == null || _animalSequencedField == null || _daysWorkingField == null)
				return true; // Can't build string, run original

			var targetAnimal = _targetAnimalField.GetValue(__instance) as Def;
			if (targetAnimal == null)
			{
				__result = "PMSelectAnimalToSequence".Translate();
				return false;
			}

			bool animalSequenced = (bool)_animalSequencedField.GetValue(__instance);
			if (animalSequenced)
			{
				__result = "SequencingComplete".Translate(targetAnimal.label.CapitalizeFirst().Named("animal"));
				return false;
			}

			// Calculate progress
			float days = (float)_daysWorkingField.GetValue(__instance);
			float guaranteedDays = 1f;
			if (_propsField != null)
			{
				var props = _propsField.GetValue(__instance) as CompProperties_Scanner;
				if (props != null && props.scanFindGuaranteedDays > 0)
					guaranteedDays = props.scanFindGuaranteedDays;
			}
			float progress = days / guaranteedDays;

			__result = "SequencingProgress".Translate(targetAnimal.label.Named("animal")) + ": " + progress.ToStringPercent();
			return false; // Skip original method
		}

		/// <summary>
		/// Postfix for CompTick - add automated sequencing when subcore is installed.
		/// Only processes every 250 ticks to match vanilla scanner behavior.
		/// </summary>
		public static void CompTick_Postfix(ThingComp __instance)
		{
			// Only run for MutationSequencerComp instances
			if (_mutationSequencerCompType == null || !_mutationSequencerCompType.IsInstanceOfType(__instance))
				return;

			if (__instance.parent == null)
				return;

			var automationComp = __instance.parent.TryGetComp<CompSubcoreAutomationBase>();
			if (automationComp == null || !automationComp.SubcoreInstalled)
				return;

			// Only do actual work every 250 ticks (like CompTickRare)
			if (!__instance.parent.IsHashIntervalTick(250))
				return;

			// Check if we can do automated work (will also try auto-select if needed)
			if (!CanUseNowAutomated(__instance, automationComp))
				return;

			// Do automated tick
			DoAutomatedTick(__instance, automationComp);
		}

		private static bool CanUseNowAutomated(ThingComp comp, CompSubcoreAutomationBase automationComp)
		{
			if (!automationComp.SubcoreInstalled)
				return false;

			var parent = comp.parent;
			if (parent?.Spawned != true)
				return false;

			// Check power
			var powerComp = parent.TryGetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return false;

			// Check forbidden
			var forbiddable = parent.TryGetComp<CompForbiddable>();
			if (forbiddable != null && forbiddable.Forbidden)
				return false;

			// Check faction
			if (parent.Faction != Faction.OfPlayer)
				return false;

			// Check if animal is already sequenced - if so, try to pick a new one
			if (_animalSequencedField != null)
			{
				bool animalSequenced = (bool)_animalSequencedField.GetValue(comp);
				if (animalSequenced)
				{
					// Current animal is done, try to auto-select a new one
					if (!TryAutoSelectTargetAnimal(comp))
						return false;
				}
			}

			// Check if target animal is set - if not, try auto-select
			if (_targetAnimalField != null)
			{
				var targetAnimal = _targetAnimalField.GetValue(comp);
				if (targetAnimal == null)
				{
					if (!TryAutoSelectTargetAnimal(comp))
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Tries to auto-select a target animal from tagged animals that have unsequenced mutations.
		/// Returns true if a valid target was selected, false if none available.
		/// </summary>
		private static bool TryAutoSelectTargetAnimal(ThingComp comp)
		{
			try
			{
				// Get the genebank from the comp
				if (_genebankField == null)
					return false;

				var genebank = _genebankField.GetValue(comp);
				if (genebank == null)
					return false;

				// Check if genebank has capacity
				if (_canTagProperty != null)
				{
					bool canTag = (bool)_canTagProperty.GetValue(genebank);
					if (!canTag)
						return false; // No space in genebank
				}

				// Get tagged animals
				if (_taggedAnimalsProperty == null)
					return false;

				var taggedAnimals = _taggedAnimalsProperty.GetValue(genebank) as IList;
				if (taggedAnimals == null || taggedAnimals.Count == 0)
					return false;

				// Get stored mutations
				if (_storedMutationsProperty == null || _getAllMutationsFromMethod == null)
					return false;

				var storedMutations = _storedMutationsProperty.GetValue(genebank) as IList;
				if (storedMutations == null)
					return false;

				// Find first animal with unsequenced mutations
				foreach (var animal in taggedAnimals)
				{
					var animalKind = animal as PawnKindDef;
					if (animalKind == null)
						continue;

					// Get all mutations for this animal
					var allMutations = _getAllMutationsFromMethod.Invoke(null, new object[] { animalKind }) as IList;
					if (allMutations == null || allMutations.Count == 0)
						continue;

					// Check if any mutations are not yet stored
					bool hasUnsequenced = false;
					foreach (var mutation in allMutations)
					{
						if (!storedMutations.Contains(mutation))
						{
							hasUnsequenced = true;
							break;
						}
					}

					if (hasUnsequenced)
					{
						// Set this animal as target
						if (_targetAnimalField != null)
						{
							_targetAnimalField.SetValue(comp, animalKind);
						}
						if (_animalSequencedField != null)
						{
							_animalSequencedField.SetValue(comp, false);
						}
						// Reset progress for new target
						if (_daysWorkingField != null)
						{
							_daysWorkingField.SetValue(comp, 0f);
						}
						return true;
					}
				}

				return false; // No animals with unsequenced mutations found
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in auto-select target animal: {ex}", 894572);
				return false;
			}
		}

		private static void DoAutomatedTick(ThingComp comp, CompSubcoreAutomationBase automationComp)
		{
			try
			{
				// Get speed factor from automation comp
				float speedFactor = automationComp.Props?.automatedSpeedFactor ?? 0.5f;

				// Get sequencing multiplier from Pawnmorpher settings
				float sequencingMultiplier = GetSequencingMultiplier();

				// Get props for guaranteed days
				float guaranteedDays = 1f;
				if (_propsField != null)
				{
					var props = _propsField.GetValue(comp) as CompProperties_Scanner;
					if (props != null && props.scanFindGuaranteedDays > 0)
						guaranteedDays = props.scanFindGuaranteedDays;
				}

				// Get current days working
				float currentDays = 0f;
				if (_daysWorkingField != null)
				{
					currentDays = (float)_daysWorkingField.GetValue(comp);
				}

				// Cap progress at guaranteed days to prevent going over 100%
				// If already over, force a find
				if (currentDays >= guaranteedDays)
				{
					// Call DoFind with null worker (automated)
					if (_doFindMethod != null)
					{
						_doFindMethod.Invoke(comp, new object[] { null });
					}
					// Always reset after DoFind attempt
					if (_daysWorkingField != null)
						_daysWorkingField.SetValue(comp, 0f);
					return;
				}

				// Update days working - CompTickRare runs every 250 ticks
				currentDays += speedFactor * sequencingMultiplier * 250f / 60000f;
				
				// Cap at guaranteed days
				if (currentDays > guaranteedDays)
					currentDays = guaranteedDays;
					
				if (_daysWorkingField != null)
					_daysWorkingField.SetValue(comp, currentDays);

				// Check if MTB roll succeeds for early find
				if (_tickDoesFind != null)
				{
					bool doesFind = (bool)_tickDoesFind.Invoke(comp, new object[] { speedFactor });
					if (doesFind)
					{
						if (_doFindMethod != null)
						{
							_doFindMethod.Invoke(comp, new object[] { null });
						}
						if (_daysWorkingField != null)
							_daysWorkingField.SetValue(comp, 0f);
					}
				}
			}
			catch (Exception ex)
			{
				Log.ErrorOnce($"[SubcoreAutomation] Error in automated sequencer tick: {ex}", 894571);
			}
		}

		private static float GetSequencingMultiplier()
		{
			try
			{
				var settingsType = AccessTools.TypeByName("Pawnmorph.PawnmorpherSettings");
				if (settingsType == null)
					return 1f;

				var modType = AccessTools.TypeByName("Pawnmorph.PawnmorpherMod");
				if (modType == null)
					return 1f;

				var settingsField = AccessTools.Field(modType, "Settings");
				if (settingsField == null)
					return 1f;

				var settings = settingsField.GetValue(null);
				if (settings == null)
					return 1f;

				var multiplierField = AccessTools.Field(settingsType, "SequencingMultiplier");
				if (multiplierField == null)
					return 1f;

				return (float)multiplierField.GetValue(settings);
			}
			catch
			{
				return 1f;
			}
		}
	}
}
