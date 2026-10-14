using HarmonyLib;
using RimWorld;
using System.Text;
using Verse;

namespace HousekeeperCat
{
    /*
     * Social effects of the awakened bond (HKCat_Bonded, created by SentienceConversion from the
     * cat's animal Bond). The relation def itself gives the permanent +50 opinion; the patches
     * here cover the two systems a relation def cannot reach:
     *
     *  - Compatibility. Vanilla compatibility is an age term plus a Gaussian seeded from the two
     *    pawns' thing IDs. The awakened pawn is a brand-new pawn, so its pair value with the
     *    former bond partner is a fresh random roll. Bonded pairs get a flat bonus on top, which
     *    roughly halves insults/slights and nearly doubles deep talks between them.
     *
     *  - Romance acceptance. A romance proposal between bonded pawns is near-certain to be
     *    accepted. Initiation (RandomSelectionWeight) is untouched, so whether a romance ever
     *    starts is still up to the usual opinion / attraction / orientation rolls, and a hard
     *    zero from vanilla (orientation mismatch, asexual, underage, inhumanized, existing
     *    partner blocking) is respected.
     */
    public static class AwakenedBondUtility
    {
        public const float CompatibilityBonus = 1f;
        public const float RomanceAcceptChance = 0.95f;

        private static PawnRelationDef bondedDef;
        private static bool bondedDefLookedUp;

        public static PawnRelationDef BondedDef
        {
            get
            {
                if (!bondedDefLookedUp)
                {
                    bondedDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail("HKCat_Bonded");
                    bondedDefLookedUp = true;
                }
                return bondedDef;
            }
        }

        public static bool AreBonded(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b)
                return false;
            PawnRelationDef def = BondedDef;
            if (def == null || a.relations == null)
                return false;
            return a.relations.DirectRelationExists(def, b);
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.CompatibilityWith))]
    static class Patch_Pawn_RelationsTracker_CompatibilityWith_AwakenedBond
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Harmony")]
        static void Postfix(Pawn otherPawn, Pawn ___pawn, ref float __result)
        {
            // Same-def check is already enforced by the original (0 for differing defs), and a
            // bonded pair is only ever two humanlikes after awakening, so no extra guard needed.
            if (AwakenedBondUtility.AreBonded(___pawn, otherPawn))
                __result += AwakenedBondUtility.CompatibilityBonus;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.SuccessChance))]
    static class Patch_InteractionWorker_RomanceAttempt_SuccessChance_AwakenedBond
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Harmony")]
        static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            // Only lift a chance that is already possible; vanilla hard zeros stay zero.
            if (__result > 0f && __result < AwakenedBondUtility.RomanceAcceptChance
                && AwakenedBondUtility.AreBonded(initiator, recipient))
            {
                __result = AwakenedBondUtility.RomanceAcceptChance;
            }
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RomanceFactors))]
    static class Patch_InteractionWorker_RomanceAttempt_RomanceFactors_AwakenedBond
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Harmony")]
        static void Postfix(Pawn romancer, Pawn romanceTarget, ref string __result)
        {
            // Explain the override on the social card's romance tooltip.
            if (AwakenedBondUtility.AreBonded(romancer, romanceTarget))
            {
                StringBuilder sb = new StringBuilder(__result);
                sb.AppendLine(" - " + "HKCat_RomanceBondFactor".Translate() + ": " + AwakenedBondUtility.RomanceAcceptChance.ToStringPercent());
                __result = sb.ToString();
            }
        }
    }
}
