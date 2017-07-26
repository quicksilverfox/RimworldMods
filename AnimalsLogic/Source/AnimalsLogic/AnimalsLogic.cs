using Harmony;
using System.Reflection;
using Verse;
using UnityEngine;

namespace AnimalsLogic
{
    [StaticConstructorOnStartup]
    class AnimalsLogic : Mod
    {
        public static Settings Settings;

        public AnimalsLogic(ModContentPack content) : base(content)
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.animalslogic");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.GetSettings<Settings>();
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
