using System.Collections.Generic;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Category of automatable machine, determines which settings are shown in UI.
	/// </summary>
	public enum MachineCategory
	{
		/// <summary>Work buildings use efficiency settings.</summary>
		WorkBuilding,
		/// <summary>Turrets use accuracy/warmup settings.</summary>
		Turret
	}

	/// <summary>
	/// Defines an automatable machine for the settings UI and patch system.
	/// Loaded from XML, allowing pure-XML mod compatibility patches.
	/// </summary>
	public class AutomatableMachineDef : Def
	{
		/// <summary>
		/// The defName of the ThingDef this applies to.
		/// </summary>
		public string targetDefName;

		/// <summary>
		/// The category of machine (determines which settings are shown).
		/// </summary>
		public MachineCategory category = MachineCategory.WorkBuilding;

		/// <summary>
		/// Whether this is a turret (uses accuracy/warmup bonuses instead of efficiency).
		/// LEGACY: Use 'category' instead. Kept for XML backwards compatibility.
		/// </summary>
		public bool isTurret
		{
			get => category == MachineCategory.Turret;
			set { if (value) category = MachineCategory.Turret; }
		}

		/// <summary>
		/// Default efficiency for work buildings (0.0 to 1.0).
		/// </summary>
		public float defaultEfficiency = 0.5f;

		/// <summary>
		/// Default accuracy bonus for turrets (0.0 to 1.0).
		/// </summary>
		public float defaultAccuracyBonus = 0.15f;

		/// <summary>
		/// Default warmup/cooldown reduction for turrets (0.0 to 1.0).
		/// </summary>
		public float defaultWarmupReduction = 0.10f;

		/// <summary>
		/// The subcore type required for this machine (defName).
		/// Used for settings UI display.
		/// </summary>
		public string subcoreDef = "SubcoreRegular";

		/// <summary>
		/// Checks if the target ThingDef exists in the database.
		/// This is used to filter out machines from mods that aren't loaded.
		/// </summary>
		public bool IsTargetDefLoaded()
		{
			if (string.IsNullOrEmpty(targetDefName))
				return false;

			return DefDatabase<ThingDef>.GetNamed(targetDefName, errorOnFail: false) != null;
		}

		/// <summary>
		/// Gets all loaded automatable machine defs.
		/// </summary>
		public static IEnumerable<AutomatableMachineDef> AllLoaded
		{
			get
			{
				foreach (var def in DefDatabase<AutomatableMachineDef>.AllDefs)
				{
					if (def.IsTargetDefLoaded())
						yield return def;
				}
			}
		}

		#region Cached Lookup

		private static Dictionary<string, AutomatableMachineDef> _byTargetDefName;

		/// <summary>
		/// Gets the AutomatableMachineDef for a given target defName, or null if not found.
		/// Uses cached lookup for performance.
		/// </summary>
		public static AutomatableMachineDef GetByTargetDefName(string targetDefName)
		{
			if (string.IsNullOrEmpty(targetDefName))
				return null;

			EnsureCacheBuilt();
			return _byTargetDefName.TryGetValue(targetDefName, out var def) ? def : null;
		}

		/// <summary>
		/// Builds the lookup cache on first access.
		/// </summary>
		private static void EnsureCacheBuilt()
		{
			if (_byTargetDefName != null)
				return;

			_byTargetDefName = new Dictionary<string, AutomatableMachineDef>();
			foreach (var def in DefDatabase<AutomatableMachineDef>.AllDefs)
			{
				if (!string.IsNullOrEmpty(def.targetDefName) && !_byTargetDefName.ContainsKey(def.targetDefName))
				{
					_byTargetDefName[def.targetDefName] = def;
				}
			}
		}

		#endregion
	}
}
