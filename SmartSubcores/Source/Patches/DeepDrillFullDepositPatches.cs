using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Postfix on CompDeepDrill.GetNextResource that flood-fills the connected deep
	/// resource deposit (same ThingDef) starting from the in-range cell vanilla picked,
	/// then returns the cell farthest from the drill. Mining outside-in keeps the
	/// remaining deposit connected for typical convex/blob deposits.
	///
	/// Gated by SubcoreAutomationSettings.deepDrillFullDepositEnabled.
	/// If deepDrillFullDepositAutomatedOnly is true, only drills with a subcore-equipped
	/// CompSubcoreAutomationBase get the behavior; otherwise it applies to every drill.
	/// </summary>
	[HarmonyPatch(typeof(CompDeepDrill), "GetNextResource")]
	public static class Patch_CompDeepDrill_GetNextResource
	{
		public static void Postfix(CompDeepDrill __instance, ref ThingDef resDef, ref int countPresent, ref IntVec3 cell, ref bool __result)
		{
			if (!__result || resDef == null)
				return;

			var settings = SubcoreAutomationMod.Settings;
			if (settings == null || !settings.deepDrillFullDepositEnabled)
				return;

			Thing parent = __instance?.parent;
			if (parent == null || !parent.Spawned)
				return;

			if (settings.deepDrillFullDepositAutomatedOnly)
			{
				var auto = parent.TryGetComp<CompSubcoreAutomationBase>();
				if (auto == null || !auto.HasSubcoreInstalled)
					return;
			}

			Map map = parent.Map;
			if (map?.deepResourceGrid == null)
				return;

			IntVec3 drillPos = parent.Position;
			IntVec3 farthestCell = cell;
			int farthestDistSq = (cell - drillPos).LengthHorizontalSquared;
			int farthestCount = countPresent;

			var visited = new HashSet<IntVec3> { cell };
			var queue = new Queue<IntVec3>();
			queue.Enqueue(cell);

			while (queue.Count > 0)
			{
				IntVec3 current = queue.Dequeue();

				for (int i = 0; i < 4; i++)
				{
					IntVec3 neighbor = current + GenAdj.CardinalDirections[i];
					if (visited.Contains(neighbor))
						continue;
					if (!neighbor.InBounds(map))
						continue;
					if (map.deepResourceGrid.ThingDefAt(neighbor) != resDef)
						continue;

					visited.Add(neighbor);
					queue.Enqueue(neighbor);

					int distSq = (neighbor - drillPos).LengthHorizontalSquared;
					if (distSq > farthestDistSq)
					{
						farthestDistSq = distSq;
						farthestCell = neighbor;
						farthestCount = map.deepResourceGrid.CountAt(neighbor);
					}
				}
			}

			cell = farthestCell;
			countPresent = farthestCount;
		}
	}
}
