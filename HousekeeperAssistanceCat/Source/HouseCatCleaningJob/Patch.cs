using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace HousekeeperCat
{
    /*
     * This is necessary to hide "Bio" inspector tab. It shows for any pawn with story, and we need story to have work working.
     */
    [StaticConstructorOnStartup]
    class HousekeeperCat : Mod
    {
        public static Harmony harmony;

        public HousekeeperCat(ModContentPack content) : base(content)
        {
            harmony = new Harmony("net.quicksilverfox.rimworld.mod.housekeepercat");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(ITab_Pawn_Character), "get_IsVisible", new Type[0])]
    static class Patch_ITab_Pawn_Character_IsVisible
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Harmony")]
        static void Postfix(ref bool __result)
        {
            Thing SelThing = Find.Selector.SingleSelectedThing;

            if (__result && SelThing != null && SelThing is Pawn_HousekeeperCat cat &&
                    !cat.IsFormerHuman() // this is for Pawnmorpher former humans
                )
                __result = false;
        }
    }
}
