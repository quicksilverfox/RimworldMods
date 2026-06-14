using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HousekeeperCat
{
    /*
     * Sentience catalyst conversion.
     *
     * The Odyssey "sentience catalyst" item installs the SentienceCatalyst brain hediff on a
     * target animal (normal effect: +1 trainability tier, reduced wildness). A housekeeper cat
     * is already maxed on both - trainability Advanced, wildness 0 - so the vanilla effect is a
     * near no-op on them. We repurpose it: a catalyst used on a housekeeper cat awakens it into
     * a full humanlike colonist.
     *
     * Standalone "disguised replacement": RimWorld has no clean animal->humanlike conversion,
     * so rather than mutate the cat in place we generate a fresh humanlike pawn, shape it to a
     * fixed housekeeper-cat appearance, spawn it exactly where the cat stood, and vanish the
     * cat. No corpse, no death notification - the swap reads as a transformation.
     *
     * Known v1 limitations:
     *  - Relations other than the animal bond are dropped; the bond carries over as the
     *    HKCat_Bonded relation.
     *  - Only a spawned cat converts (not one in a caravan / transport pod / being carried).
     *  - The awakened pawn is always an adult.
     */
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    static class Patch_Pawn_HealthTracker_AddHediff_SentienceCatalyst
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Harmony")]
        static void Postfix(Hediff hediff, Pawn ___pawn)
        {
            // Cheapest test first - nearly every AddHediff call in the game is on some other pawn.
            if (!(___pawn is Pawn_HousekeeperCat cat))
                return;
            if (hediff?.def == null || hediff.def.defName != "SentienceCatalyst")
                return;
            if (cat.IsFormerHuman()) // Pawnmorpher former humans have their own reversion logic
                return;

            // Never despawn/spawn pawns mid-AddHediff; defer to a safe point.
            LongEventHandler.ExecuteWhenFinished(() => SentienceConversion.Convert(cat));
        }
    }

    public static class SentienceConversion
    {
        // Charcoal-black skin, matching the housekeeper cat's dark coat. No-Biotech fallback only.
        private static readonly Color CharcoalBlack = new Color(0.13f, 0.13f, 0.13f);

        // Hair colours for the no-Biotech fallback; match the Hair_InkBlack / Hair_SnowWhite genes.
        private static readonly Color InkBlackHair = new Color(25f / 255f, 25f / 255f, 25f / 255f);
        private static readonly Color SnowWhiteHair = new Color(250f / 255f, 250f / 255f, 250f / 255f);

        // Skills the awakened pawn may roll passions in. "Medical" -> Medicine skill.
        private static readonly Func<SkillDef[]> PassionPool = () => new[]
        {
            SkillDefOf.Cooking, SkillDefOf.Plants, SkillDefOf.Animals, SkillDefOf.Medicine, SkillDefOf.Social
        };

        public static void Convert(Pawn_HousekeeperCat cat)
        {
            // State may have changed between the catalyst being applied and this running.
            if (cat == null || cat.Destroyed || !cat.Spawned)
                return;

            // The conversion is ordered so the cat is vanished only AFTER the awakened pawn is
            // safely on the map - if any earlier step fails, the cat is untouched and nothing
            // is lost. Any exception is caught, logged, and (if the cat survived) the catalyst
            // hediff is cleared so the player can retry.
            Pawn human = null;
            try
            {
                Map map = cat.Map;
                IntVec3 pos = cat.Position;
                Rot4 rot = cat.Rotation;
                Faction faction = cat.Faction;
                bool wild = faction == null; // a wild, unfactioned cat awakens as a wild man
                bool keepName = HasRealName(cat);
                Name catName = cat.Name;

                // Pawns the cat is bonded to; the animal Bond carries over as a humanlike
                // relation. Collected before the cat's relations are cleared below.
                PawnRelationDef bondDef = DefDatabase<PawnRelationDef>.GetNamedSilentFail("Bond");
                List<Pawn> bondedPawns = (cat.relations != null && bondDef != null)
                    ? cat.relations.DirectRelations
                        .Where(r => r.def == bondDef && r.otherPawn != null)
                        .Select(r => r.otherPawn).Distinct().ToList()
                    : null;

                float age = HumanAgeFor(cat);

                // With Biotech, generate the pawn as the nekomata xenotype - it carries the full
                // genome, including both hair-colour genes. Without Biotech this stays null and
                // appearance falls back to the direct story fields (see ApplyAppearance).
                XenotypeDef xenotype = ModsConfig.BiotechActive
                    ? DefDatabase<XenotypeDef>.GetNamedSilentFail("HKCat_Nekomata")
                    : null;

                PawnGenerationRequest request = new PawnGenerationRequest(
                    wild ? PawnKindDefOf.WildMan : PawnKindDefOf.Colonist,
                    faction: faction,
                    context: PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f,
                    allowAddictions: false,
                    fixedGender: cat.gender,
                    fixedBiologicalAge: age,
                    fixedChronologicalAge: age,
                    dontGiveWeapon: true,
                    forceNoGear: true, // no apparel, no weapon, no inventory
                    forcedXenotype: xenotype,
                    developmentalStages: DevelopmentalStage.Adult);

                human = PawnGenerator.GeneratePawn(request);
                if (human == null)
                {
                    Log.Error("[HousekeeperCat] Sentience conversion aborted: pawn generation "
                        + "returned null. " + cat.ToStringSafe() + " is left unchanged.");
                    return; // cat untouched - nothing lost
                }

                // A real given name carries over; a default numbered name is replaced by the
                // random name PawnGenerator already produced.
                if (keepName && catName != null)
                    human.Name = catName;

                ApplyAppearance(human);
                ApplyTraits(human);
                ApplyBackstory(human);
                SetSkills(human);
                ForceHairColour(human);
                TransferHealth(cat, human);

                // Drop anything the cat is carrying (pack-animal load, a hauled item) so it is
                // not destroyed along with the pawn.
                cat.inventory?.innerContainer?.TryDropAll(pos, map, ThingPlaceMode.Near);
                cat.carryTracker?.innerContainer?.TryDropAll(pos, map, ThingPlaceMode.Near);

                // Spawn the human BEFORE vanishing the cat: every step that can fail has now
                // run, so nothing past this point can leave the colony short a pawn.
                GenSpawn.Spawn(human, pos, map, rot);

                // Vanish the cat (no corpse, no "colonist lost" letter).
                cat.relations?.ClearAllRelations(); // removes reciprocals too - no dangling bonds
                cat.Destroy(DestroyMode.Vanish);

                human.Drawer?.renderer?.SetAllGraphicsDirty();
                TransferBonds(human, bondedPawns);

                // The transformation is not instant - drop the awakened pawn into a recovery
                // coma, the hediff a xenogerm implant uses. Biotech-only; without it, no coma.
                HediffDef coma = DefDatabase<HediffDef>.GetNamedSilentFail("XenogerminationComa");
                if (coma != null)
                    human.health.AddHediff(coma);

                string nameLabel = human.LabelShortCap;
                string letterText = nameLabel + " has been remade by a sentience catalyst. The "
                    + "mechanites rewrote far more than neural pathways: " + nameLabel + " has "
                    + "awakened into a fully sapient person"
                    + (wild
                        ? ", and - with no ties to any colony - now wanders free as a wild person."
                        : ", and now joins the colony as a colonist.");
                Find.LetterStack.ReceiveLetter(
                    "Sapience awakened",
                    letterText,
                    wild ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent,
                    new LookTargets(human));
            }
            catch (Exception ex)
            {
                Log.Error("[HousekeeperCat] Sentience conversion failed for " + cat.ToStringSafe()
                    + ": " + ex);
                // The cat survived (failure happened before it was vanished): leave it as a cat
                // and clear the catalyst hediff so another catalyst can be tried later.
                if (!cat.Destroyed)
                {
                    HediffDef catalyst = DefDatabase<HediffDef>.GetNamedSilentFail("SentienceCatalyst");
                    Hediff onCat = catalyst != null
                        ? cat.health?.hediffSet?.GetFirstHediffOfDef(catalyst)
                        : null;
                    if (onCat != null)
                        cat.health.RemoveHediff(onCat);
                }
                // Discard a half-built human that never reached the map.
                if (human != null && !human.Spawned && !human.Discarded)
                    human.Discard();
            }
        }

        /*
         * Fixed housekeeper-cat look. With Biotech, skin / body / ears / tail / hair colour all
         * come from the nekomata xenotype and the forced hair-colour gene at generation - only
         * the hair cut is set here. Without Biotech there is no gene system, so skin colour,
         * body type and hair colour fall back to the direct story fields.
         */
        private static void ApplyAppearance(Pawn human)
        {
            Pawn_StoryTracker story = human.story;
            if (story == null)
                return;

            // "Long mess" hairstyle (Anomaly DLC); fall back to a random long hair without it.
            HairDef hair = DefDatabase<HairDef>.GetNamedSilentFail("LongMess")
                ?? RandomLongHair(human.gender);
            if (hair != null)
                story.hairDef = hair;

            if (!ModsConfig.BiotechActive || human.genes == null)
            {
                // No gene system - fall back to the direct appearance fields.
                story.bodyType = BodyTypeDefOf.Thin;
                story.skinColorOverride = CharcoalBlack;
                story.HairColor = Rand.Bool ? InkBlackHair : SnowWhiteHair;
            }
        }

        private static HairDef RandomLongHair(Gender gender)
        {
            StyleGender opposite = gender == Gender.Male ? StyleGender.Female : StyleGender.Male;
            List<HairDef> all = DefDatabase<HairDef>.AllDefsListForReading;
            List<HairDef> longHair = all.Where(h => h.styleTags != null
                && h.styleTags.Contains("HairLong") && h.styleGender != opposite).ToList();
            if (longHair.Count == 0) // no gendered match - fall back to any long hair
                longHair = all.Where(h => h.styleTags != null && h.styleTags.Contains("HairLong")).ToList();
            return longHair.Count > 0 ? longHair.RandomElement() : null;
        }

        /*
         * The nekomata xenotype carries both hair-colour genes (ink-black and snow-white) as a
         * random-chosen group. RimWorld would activate one at random; we make that pick here
         * instead, so awakening is deterministic. The choice persists - Gene.overriddenByGene
         * is saved - and descendants still re-roll it via the normal random-chosen mechanic.
         * No-op without Biotech (no genes) - hair colour is handled in ApplyAppearance there.
         */
        private static void ForceHairColour(Pawn human)
        {
            if (human.genes == null)
                return;
            List<Gene> hairColourGenes = human.genes.GenesListForReading
                .Where(g => g.def.endogeneCategory == EndogeneCategory.HairColor)
                .ToList();
            if (hairColourGenes.Count == 0)
                return;

            Gene chosen = hairColourGenes.RandomElement();
            foreach (Gene g in hairColourGenes)
                g.OverrideBy(g == chosen ? null : chosen); // null = active, otherwise overridden

            if (chosen.def.hairColorOverride.HasValue && human.story != null)
                human.story.HairColor = chosen.def.hairColorOverride.Value;
        }

        /*
         * Strip generated traits, then grant the fixed set: nudist, fast learner, and kind.
         * Gene-sourced traits are preserved - the nekomata xenotype's KindInstinct gene grants
         * kindness itself, so the Kind trait is only added when that gene is absent (no Biotech).
         */
        private static void ApplyTraits(Pawn human)
        {
            TraitSet traits = human.story?.traits;
            if (traits == null)
                return;

            foreach (Trait existing in traits.allTraits.ToList())
                if (existing.sourceGene == null) // keep traits granted by genes
                    traits.RemoveTrait(existing);

            List<string> wanted = new List<string> { "Nudist", "FastLearner" };
            GeneDef kindInstinct = DefDatabase<GeneDef>.GetNamedSilentFail("KindInstinct");
            bool hasKindGene = human.genes != null && kindInstinct != null
                && human.genes.HasActiveGene(kindInstinct);
            if (!hasKindGene)
                wanted.Add("Kind");

            foreach (string defName in wanted)
            {
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
                if (def != null && !traits.HasTrait(def))
                    traits.GainTrait(new Trait(def, 0, forced: true));
            }
        }

        /*
         * Give the awakened pawn its backstory pair: the exclusive HKCat_AwakenedChildhood
         * childhood (its spawnCategory is on no PawnKindDef, so it is never rolled for ordinary
         * colonists), and the vanilla "colonist" adulthood (Colonist97 - "became an adult in
         * our colony, story still being written"), which fits a freshly awakened pawn and
         * disables no work types. Colonist97 is a Core def, so it needs no DLC gate.
         */
        private static void ApplyBackstory(Pawn human)
        {
            if (human.story == null)
                return;
            BackstoryDef childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("HKCat_AwakenedChildhood");
            if (childhood != null)
                human.story.Childhood = childhood;
            human.story.Adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("Colonist97");
            human.Notify_DisabledWorkTypesChanged();
        }

        /*
         * Re-create the cat's animal bond(s) on the awakened pawn as the HKCat_Bonded humanlike
         * relation - permanent mutual +50 opinion with whoever it was bonded to. The bonded
         * pawns were collected from the cat before its relations were cleared (see Convert).
         */
        private static void TransferBonds(Pawn human, List<Pawn> bondedPawns)
        {
            if (bondedPawns.NullOrEmpty() || human.relations == null)
                return;
            PawnRelationDef bonded = DefDatabase<PawnRelationDef>.GetNamedSilentFail("HKCat_Bonded");
            if (bonded == null)
                return;
            foreach (Pawn other in bondedPawns)
                if (other != null && !other.Destroyed && other != human)
                    human.relations.AddDirectRelation(bonded, other);
        }

        /*
         * Copy the cat's health conditions onto the awakened pawn. The housekeeper cat uses the
         * Monkey body, close enough to the Human body to match parts directly by BodyPartDef
         * (and by index among parts sharing a def, so left/right is kept). A condition on a part
         * with no human equivalent is skipped; the SentienceCatalyst trigger is not carried.
         */
        private static void TransferHealth(Pawn cat, Pawn human)
        {
            if (cat.health?.hediffSet == null || human.health?.hediffSet == null)
                return;
            foreach (Hediff h in cat.health.hediffSet.hediffs.ToList())
            {
                if (h?.def == null || h.def.defName == "SentienceCatalyst")
                    continue;
                BodyPartRecord part = null;
                if (h.Part != null)
                {
                    part = MatchBodyPart(cat, human, h.Part);
                    if (part == null)
                        continue; // no equivalent part on the human body
                }
                Hediff copy = HediffMaker.MakeHediff(h.def, human, part);
                copy.Severity = h.Severity;
                human.health.AddHediff(copy, part);
            }
        }

        /*
         * Maps a cat body part to the equivalent human one: same BodyPartDef, same index among
         * the parts sharing that def (so the left arm maps to the left arm, etc.).
         */
        private static BodyPartRecord MatchBodyPart(Pawn cat, Pawn human, BodyPartRecord catPart)
        {
            List<BodyPartRecord> catParts = cat.RaceProps.body.AllParts.FindAll(p => p.def == catPart.def);
            int idx = catParts.IndexOf(catPart);
            if (idx < 0)
                return null;
            List<BodyPartRecord> humanParts = human.RaceProps.body.AllParts.FindAll(p => p.def == catPart.def);
            return idx < humanParts.Count ? humanParts[idx] : null;
        }

        /*
         * Housekeeper-cat skills are placeholders that only tune work speed, so the awakened
         * pawn starts every skill at 0. Three passion levels are then spread randomly across
         * cooking / plants / animals / medical / social (at most Major per skill).
         */
        private static void SetSkills(Pawn human)
        {
            Pawn_SkillTracker skills = human.skills;
            if (skills == null)
                return;

            foreach (SkillRecord rec in skills.skills)
            {
                if (!rec.TotallyDisabled)
                {
                    rec.Level = 0;
                    rec.xpSinceLastLevel = 0f;
                }
                rec.passion = Passion.None;
            }

            SkillDef[] pool = PassionPool();
            for (int i = 0; i < 3; i++)
            {
                List<SkillRecord> eligible = pool
                    .Select(d => skills.GetSkill(d))
                    .Where(r => r != null && !r.TotallyDisabled && r.passion < Passion.Major)
                    .ToList();
                if (eligible.Count == 0)
                    break;
                SkillRecord chosen = eligible.RandomElement();
                chosen.passion = chosen.passion == Passion.None ? Passion.Minor : Passion.Major;
            }
        }

        // A real given name; false for no name or a default numbered one ("housekeeper cat 3").
        private static bool HasRealName(Pawn cat)
        {
            if (cat.Name == null)
                return false;
            if (cat.Name is NameSingle ns && ns.Numerical)
                return false;
            return true;
        }

        /*
         * Housekeeper cats reach adulthood at 6 and have an 80-year life expectancy. Humans
         * reach the Adult developmental stage at 13. Map the cat adult span 6..80 onto a human
         * 13..79 so an adult cat awakens as an adult human of similar relative age. Sub-adult
         * cats clamp to the young-adult end.
         */
        private static float HumanAgeFor(Pawn cat)
        {
            float catAge = cat.ageTracker?.AgeBiologicalYearsFloat ?? 6f;
            float t = Mathf.InverseLerp(6f, 80f, catAge);
            return Mathf.Clamp(Mathf.Lerp(13f, 79f, t), 13f, 79f);
        }
    }
}
