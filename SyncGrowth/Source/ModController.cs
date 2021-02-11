using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	[StaticConstructorOnStartup]
	class SyncGrowth : Mod
	{
#pragma warning disable 0649
		public static Settings Settings;
#pragma warning restore 0649
		public static Harmony harmony;

		public SyncGrowth(ModContentPack content) : base(content)
		{
			harmony = new Harmony("Oblitus.SyncGrowth");
			harmony.PatchAll(Assembly.GetExecutingAssembly());

            base.GetSettings<Settings>();
		}

		public void Save()
		{
			LoadedModManager.GetMod<SyncGrowth>().GetSettings<Settings>().Write();
		}

		public override string SettingsCategory()
		{
			return "Sync Growth";
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Settings.DoSettingsWindowContents(inRect);
		}

		/**
		 * Honestly, looks like a bad idea
		 */
		[HarmonyPatch(typeof(Root), "Update")]
		static class Root_Patch
		{
			static void Postfix()
			{
				if (!Settings.mod_enabled || Settings.zone_mode || !Settings.draw_overlay)
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
					//screw that bug, I can't fix it and it seems harmless.
					//if (!(ex is InvalidCastException))
						throw ex;
				}
			}
		}
	}
}
