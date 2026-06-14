using RimWorld;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Interface for production automation components.
	/// Implemented by both CompSubcoreAutomation (compat) and CompProductionAutomation (new).
	/// </summary>
	public interface IProductionAutomation
	{
		/// <summary>
		/// The parent thing (building/thing with comps).
		/// </summary>
		ThingWithComps Parent { get; }

		/// <summary>
		/// Whether a subcore is installed.
		/// </summary>
		bool HasSubcoreInstalled { get; }

		/// <summary>
		/// Gets the deep drill component if present.
		/// </summary>
		CompDeepDrill DeepDrill { get; }

		/// <summary>
		/// Gets or sets the drill progress (0-1).
		/// </summary>
		float DrillProgress { get; set; }

		/// <summary>
		/// Whether the sun lamp is enabled for hydroponics.
		/// </summary>
		bool SunLampEnabled { get; set; }

		/// <summary>
		/// Gets the effective speed factor for automation.
		/// </summary>
		float EffectiveSpeedFactor { get; }

		/// <summary>
		/// Updates power consumption.
		/// </summary>
		void UpdatePowerConsumption();

		/// <summary>
		/// Updates the hydroponics glower state.
		/// </summary>
		void UpdateHydroponicsGlower();

		/// <summary>
		/// Attempts to turn off the drill.
		/// </summary>
		void TryTurnOffDrill();

		/// <summary>
		/// Counts remaining wet cells for moisture pump.
		/// </summary>
		int CountRemainingWetCells();

		/// <summary>
		/// Gets the effective efficiency based on settings.
		/// </summary>
		float GetEffectiveEfficiency(MachineSettings settings);
	}
}
