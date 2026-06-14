using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using SubcoreAutomation.Core;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for Re-Powered mod compatibility.
	/// Makes Re-Powered recognize our automated buildings as "in use" so they draw full power.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class RePoweredCompatPatches
	{
		static RePoweredCompatPatches()
		{
			// Only apply these patches if Re-Powered is loaded
			if (!SubcoreAutomationMod.TurnItOnAndOffLoaded)
				return;

			var harmony = new Harmony("SubcoreAutomation.RePoweredCompat");

			// Patch ReservationManager.IsReservedByAnyoneOf to return true for our automated buildings
			var original = AccessTools.Method(typeof(ReservationManager), "IsReservedByAnyoneOf",
				new[] { typeof(LocalTargetInfo), typeof(Faction) });

			if (original != null)
			{
				harmony.Patch(original,
					postfix: new HarmonyMethod(typeof(RePoweredCompatPatches), nameof(IsReservedByAnyoneOf_Postfix)));
				Log.Message("[SubcoreAutomation] Re-Powered compatibility patches applied.");
			}
			else
				Log.Error("[SubcoreAutomation] Re-Powered compatibility patches BROKEN: ReservationManager.IsReservedByAnyoneOf not found!");
		}

		/// <summary>
		/// Postfix for IsReservedByAnyoneOf - return true for automated buildings.
		/// This makes Re-Powered think a pawn is using the building, so it draws full power.
		/// </summary>
		public static void IsReservedByAnyoneOf_Postfix(LocalTargetInfo target, Faction faction, ref bool __result)
		{
			// Already reserved by someone, no need to check further
			if (__result)
				return;

			// Only care about player faction buildings
			if (faction != Faction.OfPlayer)
				return;

			// Check if target is a Thing with our automation comp
			if (!target.HasThing)
				return;

			var building = target.Thing;
			if (building == null || !building.Spawned)
				return;

			// Don't fake reservation if building is designated for deconstruction or uninstallation
			// This ensures players can still minify/deconstruct automated buildings without issues
			if (building.Map?.designationManager != null)
			{
				var designations = building.Map.designationManager;
				if (designations.DesignationOn(building, DesignationDefOf.Deconstruct) != null ||
				    designations.DesignationOn(building, DesignationDefOf.Uninstall) != null)
				{
					return;
				}
			}

			// Check if this building has our automation component and is actively automating
			var automationComp = building.TryGetComp<CompSubcoreAutomationBase>();
			if (automationComp == null || !automationComp.SubcoreInstalled || !automationComp.IsAutomationEnabled)
				return;

			// Don't fake reservation if building has no power (disabled, outage, etc.)
			var powerComp = building.TryGetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return;

			// Don't fake reservation if building is toggled off
			var flickable = building.TryGetComp<CompFlickable>();
			if (flickable != null && !IsFlickableEffectivelyOn(flickable))
				return;

			// Make Re-Powered think this building is reserved (in use)
			__result = true;
		}

		/// <summary>
		/// Checks if a flickable is effectively on (current state or desired state is on).
		/// </summary>
		private static bool IsFlickableEffectivelyOn(CompFlickable flickable)
		{
			if (flickable == null)
				return true;

			if (!flickable.WantsFlick())
				return flickable.SwitchIsOn;

			if (SubcoreAutomationUtils.FlickableWantSwitchOnField != null)
			{
				object wantOn = SubcoreAutomationUtils.FlickableWantSwitchOnField.GetValue(flickable);
				if (wantOn != null)
					return (bool)wantOn;
			}

			return !flickable.SwitchIsOn;
		}
	}
}
