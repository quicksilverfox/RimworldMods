using Harmony;
using RimWorld;
using System;
using System.Linq;
using System.Reflection;
using Verse;

namespace DoNotForbidSlaughtered
{
    [StaticConstructorOnStartup]
    class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("net.quicksilverfox.rimworld.mod.donotforbidslaughtered");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(ExecutionUtility), "DoExecutionByCut", new Type[] { typeof(Pawn), typeof(Pawn) })]
    static class ExecutionUtility_DoExecutionByCut_Patch
    {
        static bool Prefix(ref Pawn __state, Pawn executioner, Pawn victim)
        {
            __state = victim;
            return true;
        }

        static void Postfix(ref Pawn __state)
        {
            Pawn victim = __state;
            if (victim == null || !victim.Dead || victim.Faction == null || !victim.Faction.IsPlayer || victim.Corpse == null)
            {
                return;
            }
            
            victim.Corpse.SetForbidden(false);
        }
    }
}
