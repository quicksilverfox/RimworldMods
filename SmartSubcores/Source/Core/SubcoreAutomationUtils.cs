using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Shared utility methods and reflection fields used across handlers.
	/// </summary>
	public static class SubcoreAutomationUtils
	{
		#region Reflection Fields

		/// <summary>
		/// Field info for CompFlickable.wantSwitchOn (private).
		/// Used to force flickable state without triggering flick designation.
		/// </summary>
		public static readonly FieldInfo FlickableWantSwitchOnField;

		static SubcoreAutomationUtils()
		{
			FlickableWantSwitchOnField = AccessTools.Field(typeof(CompFlickable), "wantSwitchOn");
			if (FlickableWantSwitchOnField == null)
			{
				Log.Error("[SubcoreAutomation] Could not find CompFlickable.wantSwitchOn field");
			}
		}

		#endregion

		#region Flickable Utilities

		/// <summary>
		/// Forces a flickable component to a specific state without creating a flick designation.
		/// Uses direct field access like the original BackupPower mod for reliable state changes.
		/// </summary>
		/// <param name="flickable">The flickable component to modify.</param>
		/// <param name="on">True to turn on, false to turn off.</param>
		public static void ForceFlickable(CompFlickable flickable, bool on)
		{
			if (flickable == null)
				return;

			// If already in desired state, nothing to do
			if (flickable.SwitchIsOn == on)
				return;

			// Directly set the switch state using public property (like original BackupPower mod)
			flickable.SwitchIsOn = on;

			// Also update wantSwitchOn if there's a pending flick designation
			if (flickable.WantsFlick() && FlickableWantSwitchOnField != null)
			{
				FlickableWantSwitchOnField.SetValue(flickable, on);
			}
		}

		#endregion
	}
}
