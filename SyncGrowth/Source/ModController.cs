using System.Reflection;
using HarmonyLib;
using SyncGrowth.Patches;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	/**
	 * Main class that initializes stuff.
	 */
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

			Compatibility.Patch();

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
	}
}
