using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

// DEBUG: Set to true to log door access checks for non-colony pawns
// #define DOOR_DEBUG

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Harmony patches for door lockdown functionality.
	/// </summary>
	/// <remarks>
	/// PERFORMANCE CONSIDERATIONS:
	/// 
	/// PawnCanOpen is called frequently during pathfinding - potentially 5,000-50,000 times
	/// per second in large colonies with many pawns and doors. This is a hot path.
	/// 
	/// Current optimizations:
	/// 1. Early exit if pawn is colony member (covers ~80% of calls)
	/// 2. Early exit if result is already false
	/// 
	/// If performance issues arise with many automated doors, consider:
	/// 1. Static Dictionary cache mapping door thingIDNumber -> CompSubcoreAutomation
	///    - Dictionary lookup is O(1) ~10ns vs GetComp O(n) ~100ns
	///    - Would need invalidation on door spawn/despawn (use MapComponent or comp callbacks)
	///    - Example:
	///      private static Dictionary&lt;int, CompSubcoreAutomation&gt; _doorCompCache = new();
	///      if (!_doorCompCache.TryGetValue(__instance.thingIDNumber, out var comp))
	///      {
	///          comp = __instance.GetComp&lt;CompSubcoreAutomation&gt;();
	///          _doorCompCache[__instance.thingIDNumber] = comp;
	///      }
	/// 
	/// 2. ConditionalWeakTable for automatic cleanup when doors are destroyed
	///    - No manual invalidation needed, but slightly slower than Dictionary
	/// </remarks>
	[HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
	public static class Patch_Building_Door_PawnCanOpen
	{
		/// <summary>
		/// Restricts door access when lockdown mode is enabled.
		/// Only blocks guests and prisoners when lockdown is explicitly enabled.
		/// When lockdown is OFF, this patch does nothing and vanilla behavior applies.
		/// </summary>
		public static void Postfix(Building_Door __instance, Pawn p, ref bool __result)
		{
			// Only restrict if vanilla already allowed access
			if (!__result)
				return;

			// OPTIMIZATION: Colony pawns that aren't prisoners always pass - skip GetComp
			if (p.Faction == Faction.OfPlayer && !p.IsPrisoner)
				return;

			// Check if door has automation with lockdown enabled
			CompUtilityAutomation comp = __instance.GetComp<CompUtilityAutomation>();
			
			// No comp or no subcore = no lockdown possible, allow vanilla behavior
			if (comp == null || !comp.HasSubcoreInstalled)
				return;
			
			// CRITICAL: Lockdown OFF means we do NOTHING - vanilla behavior applies
			// This must return before any blocking checks
			if (!comp.LockForNonColony)
				return;

			// === LOCKDOWN IS ON - Block specific pawn types ===
			
			// Block prisoners
			if (p.IsPrisoner)
			{
				__result = false;
				return;
			}

			// Block quest lodgers (temporary pawns from quests staying at colony)
			if (p.IsQuestLodger())
			{
				__result = false;
				return;
			}

			// Block guests that aren't released (arrested guests, etc)
			if (p.guest != null && !p.guest.Released)
			{
				__result = false;
				return;
			}
			
			// NOTE: Neutral faction pawns (caravans, visitors) are NOT blocked here
			// They don't have p.guest set and aren't prisoners/lodgers
			// If they're being blocked, it's vanilla behavior, not this patch
		}
	}
}
