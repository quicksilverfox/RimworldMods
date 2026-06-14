using HarmonyLib;
using RimWorld;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Fixes a vanilla bug where mech boosters (CompCauseHediff_AoE with
	/// onlyTargetMechs=true) apply their boost hediff to ALL mechs in range,
	/// including hostile ones — turning a player's own booster into a buff for
	/// raiding mechanoids passing through its radius.
	///
	/// Scoped to onlyTargetMechs to avoid affecting other CompCauseHediff_AoE
	/// users (sleep accelerator, biosculpter, etc.) which legitimately target
	/// non-faction pawns.
	/// </summary>
	[HarmonyPatch(typeof(CompCauseHediff_AoE), "IsPawnAffected")]
	public static class Patch_CompCauseHediff_AoE_IsPawnAffected
	{
		public static void Postfix(CompCauseHediff_AoE __instance, Pawn target, ref bool __result)
		{
			if (!__result)
				return;

			if (__instance.Props == null || !__instance.Props.onlyTargetMechs)
				return;

			if (__instance.parent.Faction != target.Faction)
				__result = false;
		}
	}
}
