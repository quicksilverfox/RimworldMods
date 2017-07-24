using Harmony;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace BuyablePackAnimals
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.buyablepackanimals");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(TraderCaravanUtility), "GetTraderCaravanRole", new Type[] { typeof(Pawn) })]
    static class Patch_TraderCaravanUtility_GetTraderCaravanRole
    {
        static bool Prefix(ref Pawn p, ref Pawn __state)
        {
            __state = p;
            return true;
        }

        static void Postfix(ref TraderCaravanRole __result, ref Pawn __state)
        {
            if (__state != null && __state.kindDef.RaceProps.packAnimal && !__state.inventory.innerContainer.Any)
            {
                __result = TraderCaravanRole.Chattel;
            }
        }
    }
}
