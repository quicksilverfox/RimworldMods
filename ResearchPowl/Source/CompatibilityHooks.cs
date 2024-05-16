using System.Collections.Generic;
using Verse;

namespace ResearchPowl
{
    /* Currently Does not work */

    // This static class contains hooks that supported altered behavior of
    // researchpal for the convenience of other awesome modders that try to
    // make their mods compatible. These methods are assumed to be patched
    // with harmony. The patch should perform incrementally, e.g. the postfix
    // patch of `IsHidden` should never returns `true`, but `true || __result`
    // (well I guess in this case you just returns `__result` XD)
    //
    // I'm not a professional C# user, but I assume harmony is a powerful enough
    // tool to patch the method to serve appropriate purposes. So if you have
    // suggestions on how to accomplish certain goal more properly, feel free to
    // lecture me on the steam workshop page.
    // 
    // If you don't find the method you need here, and you believe it would be
    // way simpler for your compatibility patch if I provide one, also feel free
    // put your suggestion in the "Mod Compatibility" discussion.
    public static class CompatibilityHooks {
        // Returns true if a research node shouldn't be drawn on the research
        // tab.
        // if `IsHidden(research) == true`, then all the children node of
        // `research` will be hidden.
        public static bool IsHidden(ResearchProjectDef r)
        {
            return false;
        }

        // If there are additional unlock requirements for your modded research
        // other than the vanilla special requirements (research bench,
        // research facility and techprints).
        // The research node will be grayed out (just like lacking techprints)
        // if this function returns false on the according research.
        public static bool PassCustomUnlockRequirements(ResearchProjectDef p)
        {
            return true;
        }

        // Returns a list of prompts that tell the player what should be done if
        // the tech is locked behind a modded requirement.
        // A tooltip will be displayed when hovering the locked techs
        // (and only the locked techs)
        public static List<string> CustomUnlockRequirementPrompts(ResearchProjectDef p)
        {
            return new List<string>();
        }
    }
}
