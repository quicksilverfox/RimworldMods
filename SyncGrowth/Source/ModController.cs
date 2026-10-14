using System.Reflection;
using HarmonyLib;
using SyncGrowth.Patches;
using UnityEngine;
using Verse;

namespace SyncGrowth
{
	/// <summary>
	/// Mod entry point: loads settings and applies the Harmony patches.
	/// </summary>
	class SyncGrowth : Mod
	{
		public static Harmony harmony;

		public SyncGrowth(ModContentPack content) : base(content)
		{
			GetSettings<Settings>();

			harmony = new Harmony("Oblitus.SyncGrowth");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			Compatibility.Patch();
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
