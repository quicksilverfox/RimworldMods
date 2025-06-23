using HarmonyLib;
using System.Reflection;
using Verse;

namespace WorldGenRules
{
    [StaticConstructorOnStartup]
    class MyLittlePlanet : Mod
    {
        public MyLittlePlanet(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("net.quicksilverfox.rimworld.mod.worldgenrules");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }
    }
}
