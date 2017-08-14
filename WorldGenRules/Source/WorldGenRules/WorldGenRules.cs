using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace WorldGenRules
{
    [StaticConstructorOnStartup]
    class WorldGenRules : Mod
    {
#pragma warning disable 0649
        //public static Settings Settings;
#pragma warning restore 0649

        public WorldGenRules(ModContentPack content) : base(content)
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.worldgenrules");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            //base.GetSettings<Settings>();
        }

        public void Save()
        {
            //LoadedModManager.GetMod<WorldGenRules>().GetSettings<Settings>().Write();
        }

        //public override string SettingsCategory()
        //{
        //    return "WorldGenRules";
        //}

        //public override void DoSettingsWindowContents(Rect inRect)
        //{
        //    Settings.DoSettingsWindowContents(inRect);
        //}
    }
}
