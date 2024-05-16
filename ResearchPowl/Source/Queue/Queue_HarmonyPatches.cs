// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchPowl
{
    [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
    public class DoCompletionDialog
    {
        // suppress vanilla completion dialog, we never want to show it.
        static void Prefix(ref bool doCompletionDialog)
        {
            doCompletionDialog = doCompletionDialog && ModSettings_ResearchPowl.useVanillaResearchFinishedMessage;
        }

        static void Postfix(ResearchProjectDef proj)
        {
            if (proj.IsFinished)
            {
                Log.Debug("Patch of FinishProject: {0} finished", proj.label);
                Queue.TryStartNext(proj);
            }
        }
    }
}