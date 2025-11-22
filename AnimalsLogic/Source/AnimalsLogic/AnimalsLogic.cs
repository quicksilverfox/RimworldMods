using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using AnimalsLogic.Patches;
using System;

namespace AnimalsLogic
{
    [StaticConstructorOnStartup]
    class AnimalsLogic : Mod
    {
#pragma warning disable 0649
        public static Settings settings;
#pragma warning restore 0649
        public static Harmony harmony;

        public AnimalsLogic(ModContentPack content) : base(content)
        {
            //Log.Message("Animal Logic is trying to apply patches.");
            harmony = new Harmony("net.quicksilverfox.rimworld.mod.animalslogic");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            AnimalsUseDispenser.Patch();
            NoBoomSlaughter.Patch();

            ShowAnimalRelations.Patch();

            NoToxicRot.Patch();

            HostilePredators.Patch();
            GetThemYoung.Patch();
            ForgetMeNot.Patch();

            settings = base.GetSettings<Settings>();
            //Log.Message("Animal Logic is loaded.");
        }

        public void Save()
        {
            LoadedModManager.GetMod<AnimalsLogic>().GetSettings<Settings>().Write();
        }

        public override string SettingsCategory()
        {
            return "AnimalsLogic";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }
}
