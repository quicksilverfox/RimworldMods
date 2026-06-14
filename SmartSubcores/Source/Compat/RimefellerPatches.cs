using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Compat
{
	/// <summary>
	/// Conditional patches for Rimefeller mod compatibility.
	/// These only apply if Rimefeller is loaded.
	/// </summary>
	[StaticConstructorOnStartup]
	public static class RimefellerPatches
	{
		private static readonly Type ResourceConsoleType;

		static RimefellerPatches()
		{
			// Try to find Rimefeller's Building_ResourceConsole type
			ResourceConsoleType = AccessTools.TypeByName("Rimefeller.Building_ResourceConsole");

			if (ResourceConsoleType == null)
			{
				// Rimefeller not detected, skip patches
				return;
			}

			var harmony = new Harmony("Pawnmorpher.SubcoreAutomation.Rimefeller");

			// Patch the Manned property to return true when subcore is installed
			var mannedProp = AccessTools.Property(ResourceConsoleType, "Manned");
			if (mannedProp != null && mannedProp.GetGetMethod() != null)
			{
				harmony.Patch(mannedProp.GetGetMethod(),
					postfix: new HarmonyMethod(typeof(RimefellerPatches), nameof(Manned_Postfix)));
			}

			// Find and patch Rimefeller work givers that target ResourceConsole
			PatchRimefellerWorkGivers(harmony);


		}

		private static void PatchRimefellerWorkGivers(Harmony harmony)
		{
			// Find work givers from Rimefeller assembly
			var rimefellerAssembly = ResourceConsoleType.Assembly;
			var workGiverTypes = rimefellerAssembly.GetTypes()
				.Where(t => typeof(WorkGiver).IsAssignableFrom(t) && !t.IsAbstract);

			foreach (var wgType in workGiverTypes)
			{
				// Check if this work giver targets ResourceConsole (by name pattern)
				if (wgType.Name.Contains("Console") || wgType.Name.Contains("Resource"))
				{
					var hasJobMethod = AccessTools.Method(wgType, "HasJobOnThing");
					if (hasJobMethod != null)
					{
						try
						{
							harmony.Patch(hasJobMethod,
								prefix: new HarmonyMethod(typeof(RimefellerPatches), nameof(WorkGiver_HasJobOnThing_Prefix)));
						}
						catch (Exception ex)
						{
							Log.Warning($"[SubcoreAutomation] Failed to patch {wgType.Name}: {ex.Message}");
						}
					}
				}
			}
		}

		/// <summary>
		/// Postfix for Manned property - returns true if a subcore is installed and automation is enabled.
		/// </summary>
		public static void Manned_Postfix(object __instance, ref bool __result)
		{
			if (__result)
				return;

			if (__instance is Thing thing)
			{
				CompSubcoreAutomationBase automation = thing.TryGetComp<CompSubcoreAutomationBase>();
				if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled && automation.CanOperate())
				{
					__result = true;
				}
			}
		}

		/// <summary>
		/// Prefix for work giver HasJobOnThing - skip if automated and automation is enabled.
		/// </summary>
		public static bool WorkGiver_HasJobOnThing_Prefix(Thing t, ref bool __result)
		{
			if (t == null)
				return true;

			// Check if this is a ResourceConsole with subcore installed and automation enabled
			if (ResourceConsoleType != null && ResourceConsoleType.IsInstanceOfType(t))
			{
				CompSubcoreAutomationBase automation = t.TryGetComp<CompSubcoreAutomationBase>();
				if (automation != null && automation.SubcoreInstalled && automation.IsAutomationEnabled)
				{
					__result = false;
					return false; // Skip original method
				}
			}

			return true;
		}
	}
}
