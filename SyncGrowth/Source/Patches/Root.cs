using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace SyncGrowth.Patches
{
	/**
	 * Creates group outline when a plant is selected.
	 * 
	 * Honestly, patching Root feels like a bad idea.
	 */
	[HarmonyPatch(typeof(Root), "Update")]
	class Root_Patch
	{
		static void Postfix()
		{
			if (!Settings.mod_enabled || Settings.zone_mode || !Settings.draw_overlay || Find.CurrentMap == null)
				return;

			try
			{
				var t = Find.Selector.SingleSelectedThing;

				if (t == null)
				{
					return;
				}
				if (t is Plant plant)
				{
					if (Settings.draw_overlay && KeyBindingDefOf.Misc1.JustPressed)
					{
						GroupMaker.TryCreateGroup(Find.CurrentMap.GetComponent<MapCompGrowthSync>(), plant, true);
					}

					var group = GroupsUtils.GroupOf(plant);

					if (group != null)
					{
						group.Draw(0);
					}
				}
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}
	}
}
