using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace AnimalsLogic.Patches
{
    /// <summary>
    /// Vanilla's AnimalProductionUtility only knows CompMilkable and CompShearable, so modded animals with a custom
    /// CompHasGatherableBodyResource (venom, silk, stone chunks, ...) show no production on the info card at all.
    /// This appends the same kind of rows for them in vanilla's "Animal productivity" category, right after the wool rows.
    ///
    /// Hooked at RaceProperties.SpecialDisplayStats rather than AnimalProductionUtility because the StatRequest carries the
    /// pawn: several mods compute amount or interval from the parent pawn, so numbers come from the live comp when there is
    /// one. The extra rows are concatenated lazily onto vanilla's own enumerable; nothing is copied or re-ordered.
    ///
    /// Vanilla Expanded Framework's CompAnimalProduct (used by Alpha Animals and others) gets a dedicated reader, because
    /// its ResourceDef getter returns a different random item on every call for random producers, and its amount and
    /// interval getters throw without a parent. Everything else goes through the generic getter path.
    /// </summary>
    [HarmonyPatch(typeof(RaceProperties), nameof(RaceProperties.SpecialDisplayStats))]
    static class ModdedAnimalProduction
    {
        // The getters are protected abstract on the base class; invoking the base PropertyInfo dispatches virtually.
        static readonly PropertyInfo ResourceDefProp = AccessTools.Property(typeof(CompHasGatherableBodyResource), "ResourceDef");
        static readonly PropertyInfo ResourceAmountProp = AccessTools.Property(typeof(CompHasGatherableBodyResource), "ResourceAmount");
        static readonly PropertyInfo IntervalDaysProp = AccessTools.Property(typeof(CompHasGatherableBodyResource), "GatherResourcesIntervalDays");

        const int FirstDisplayPriority = 9760; // vanilla's last wool row is 9770

        /// <summary>Everything the rows need, independent of which comp it came from.</summary>
        class ProductInfo
        {
            public string typeValue;                     // value of the "Product type" row
            public string typeDesc;                      // its tooltip
            public List<ThingDef> typeLinks = new List<ThingDef>();
            public string rowLabel;                      // "{0}" in "{0} amount" etc.; the resource name, or a generic word
            public int amount;
            public int intervalDays;
            public float marketValue;                    // per unit; averaged for random producers
            public string extraValue;                    // optional "Extra items" row
            public string extraDesc;
            public List<ThingDef> extraLinks;
        }

        static void Postfix(ThingDef parentDef, StatRequest req, ref IEnumerable<StatDrawEntry> __result)
        {
            if (!Settings.extra_display_stats || parentDef?.comps == null || parentDef.race == null || !parentDef.race.Animal)
                return;

            if (!parentDef.comps.Any(IsModdedBodyResource))
                return;

            __result = __result.Concat(ModdedProductionStats(parentDef, req.Thing as Pawn));
        }

        static IEnumerable<StatDrawEntry> ModdedProductionStats(ThingDef d, Pawn pawn)
        {
            int priority = FirstDisplayPriority;
            StatCategoryDef category = StatCategoryDefOf.AnimalProductivity;

            foreach (CompProperties props in d.comps)
            {
                if (!IsModdedBodyResource(props))
                    continue;

                ProductInfo info = Read(props, pawn);
                if (info == null)
                    continue;

                yield return new StatDrawEntry(category, "AL_ResourceType".Translate(), info.typeValue, info.typeDesc, priority--, null,
                    info.typeLinks.Select(t => new Dialog_InfoCard.Hyperlink(t)));
                yield return new StatDrawEntry(category, "AL_ResourceAmount".Translate(info.rowLabel), info.amount.ToString(),
                    "AL_ResourceAmountDesc".Translate(), priority--);
                yield return new StatDrawEntry(category, "AL_ResourceInterval".Translate(info.rowLabel), "PeriodDays".Translate(info.intervalDays),
                    "AL_ResourceIntervalDesc".Translate(), priority--);

                if (info.intervalDays > 0)
                {
                    float perYear = 60f / info.intervalDays * info.amount;
                    yield return new StatDrawEntry(category, "AL_ResourcePerYear".Translate(info.rowLabel), perYear.ToString("F0"),
                        "AL_ResourcePerYearDesc".Translate(), priority--);
                    yield return new StatDrawEntry(category, "AL_ResourceValue".Translate(info.rowLabel), info.marketValue.ToStringMoney(),
                        "AL_ResourceValueDesc".Translate(), priority--);
                    yield return new StatDrawEntry(category, "AL_ResourceValuePerYear".Translate(info.rowLabel), (perYear * info.marketValue).ToStringMoney(),
                        "AL_ResourceValuePerYearDesc".Translate(), priority--);
                }

                if (info.extraValue != null)
                {
                    yield return new StatDrawEntry(category, "AL_ResourceExtra".Translate(), info.extraValue, info.extraDesc, priority--, null,
                        info.extraLinks.Select(t => new Dialog_InfoCard.Hyperlink(t)));
                }
            }
        }

        static bool IsModdedBodyResource(CompProperties props)
        {
            Type compClass = props?.compClass;
            if (compClass == null || !typeof(CompHasGatherableBodyResource).IsAssignableFrom(compClass))
                return false;

            // vanilla already lists these two
            return !typeof(CompMilkable).IsAssignableFrom(compClass) && !typeof(CompShearable).IsAssignableFrom(compClass);
        }

        static ProductInfo Read(CompProperties props, Pawn pawn)
        {
            CompHasGatherableBodyResource liveComp = null;
            if (pawn != null)
            {
                foreach (ThingComp c in pawn.AllComps)
                {
                    if (c.props == props && c is CompHasGatherableBodyResource gatherable)
                    {
                        liveComp = gatherable;
                        break;
                    }
                }
            }

            try
            {
                if (Vef.Matches(props))
                    return ReadVef(props, liveComp, pawn);
                return ReadGeneric(props, liveComp);
            }
            catch (Exception)
            {
                // most likely a getter that needs a spawned parent, on a def-only card: show nothing rather than break the card
                return null;
            }
        }

        /// <summary>Unknown comps: ask the three abstract getters, on the live comp if possible, else on a detached one.</summary>
        static ProductInfo ReadGeneric(CompProperties props, CompHasGatherableBodyResource comp)
        {
            if (comp == null)
            {
                comp = (CompHasGatherableBodyResource)Activator.CreateInstance(props.compClass);
                comp.props = props;
            }

            ThingDef resourceDef = ResourceDefProp.GetValue(comp) as ThingDef;
            if (resourceDef == null)
                return null;

            var info = new ProductInfo
            {
                typeValue = resourceDef.LabelCap,
                typeDesc = "AL_ResourceTypeDesc".Translate(),
                rowLabel = resourceDef.LabelCap,
                amount = (int)ResourceAmountProp.GetValue(comp),
                intervalDays = (int)IntervalDaysProp.GetValue(comp),
                marketValue = resourceDef.BaseMarketValue,
            };
            info.typeLinks.Add(resourceDef);
            return info;
        }

        /// <summary>
        /// Vanilla Expanded Framework's CompProperties_AnimalProduct. Its resourceDef is only a placeholder when the product
        /// is random or variable, so the real candidates are listed instead and the value is averaged over them.
        /// </summary>
        static ProductInfo ReadVef(CompProperties props, CompHasGatherableBodyResource liveComp, Pawn pawn)
        {
            // Mod authors use this to keep the product a secret until the animal is tamed.
            if (Vef.HideOnWild(props) && (pawn == null || pawn.Faction == null || !pawn.Faction.IsPlayer))
                return null;

            var info = new ProductInfo();

            // amount and interval: stat-scaled on a live pawn, base values from the def otherwise
            if (liveComp != null)
            {
                info.amount = (int)ResourceAmountProp.GetValue(liveComp);
                info.intervalDays = (int)IntervalDaysProp.GetValue(liveComp);
            }
            else
            {
                info.amount = Vef.ResourceAmount(props);
                info.intervalDays = Vef.IntervalDays(props);
            }

            // VEF calls these "seasonalItems", but the list is only a set of variants selected by an index that other comps
            // set from the animal's state (the Chameleon Yak's wool follows the terrain it stands on), so the wording stays neutral.
            List<ThingDef> variants = ResolveDefs(Vef.VariantItems(props));
            List<ThingDef> random = Vef.IsRandom(props) ? ResolveDefs(Vef.RandomItems(props)) : null;

            if (!variants.NullOrEmpty())
            {
                ThingDef current = null;
                if (liveComp != null)
                {
                    int index = Vef.VariantIndex(liveComp);
                    if (index >= 0 && index < variants.Count)
                        current = variants[index];
                }

                info.typeValue = current != null
                    ? "AL_VariableProductCurrent".Translate(current.LabelCap)
                    : "AL_VariableProduct".Translate();
                info.typeDesc = "AL_VariableProductDesc".Translate(LabelList(variants));
                info.typeLinks.AddRange(variants);
                info.rowLabel = "AL_GenericProduct".Translate();
                info.marketValue = current != null ? current.BaseMarketValue : variants.Average(t => t.BaseMarketValue);
            }
            else if (!random.NullOrEmpty())
            {
                string custom = Vef.CustomResourceString(props);
                info.typeValue = !custom.NullOrEmpty() && custom.CanTranslate()
                    ? "AL_RandomProductNamed".Translate(custom.Translate())
                    : "AL_RandomProduct".Translate();
                info.typeDesc = "AL_RandomProductDesc".Translate(LabelList(random));
                info.typeLinks.AddRange(random);
                info.rowLabel = "AL_GenericProduct".Translate();
                info.marketValue = random.Average(t => t.BaseMarketValue);
            }
            else
            {
                ThingDef resourceDef = Vef.ResourceDef(props);
                if (resourceDef == null)
                    return null;

                info.typeValue = resourceDef.LabelCap;
                info.typeDesc = "AL_ResourceTypeDesc".Translate();
                info.typeLinks.Add(resourceDef);
                info.rowLabel = resourceDef.LabelCap;
                info.marketValue = resourceDef.BaseMarketValue;
            }

            if (Vef.HasAdditional(props))
            {
                List<ThingDef> extras = ResolveDefs(Vef.AdditionalItems(props));
                if (!extras.NullOrEmpty())
                {
                    info.extraValue = "AL_ResourceExtraValue".Translate(Vef.AdditionalProb(props), Vef.AdditionalNumber(props));
                    info.extraDesc = "AL_ResourceExtraDesc".Translate(LabelList(extras));
                    info.extraLinks = extras;
                }
            }

            return info;
        }

        static List<ThingDef> ResolveDefs(List<string> defNames)
        {
            if (defNames.NullOrEmpty())
                return null;

            var defs = new List<ThingDef>(defNames.Count);
            foreach (string name in defNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null)
                    defs.Add(def);
            }
            return defs;
        }

        static string LabelList(List<ThingDef> defs)
        {
            return defs.Select(t => t.label).ToCommaList(useAnd: true);
        }

        /// <summary>
        /// Reflection access to VEF.AnimalBehaviours.CompProperties_AnimalProduct, resolved once. All members are null when
        /// Vanilla Expanded Framework is not loaded, and Matches() is then always false.
        /// </summary>
        static class Vef
        {
            static readonly Type PropsType = AccessTools.TypeByName("VEF.AnimalBehaviours.CompProperties_AnimalProduct");
            static readonly Type CompType = AccessTools.TypeByName("VEF.AnimalBehaviours.CompAnimalProduct");

            static readonly FieldInfo resourceDef = Field("resourceDef");
            static readonly FieldInfo resourceAmount = Field("resourceAmount");
            static readonly FieldInfo gatheringIntervalDays = Field("gatheringIntervalDays");
            static readonly FieldInfo customResourceString = Field("customResourceString");
            static readonly FieldInfo isRandom = Field("isRandom");
            static readonly FieldInfo randomItems = Field("randomItems");
            static readonly FieldInfo seasonalItems = Field("seasonalItems");
            static readonly FieldInfo hasAditional = Field("hasAditional");
            static readonly FieldInfo additionalItemsProb = Field("additionalItemsProb");
            static readonly FieldInfo additionalItemsNumber = Field("additionalItemsNumber");
            static readonly FieldInfo additionalItems = Field("additionalItems");
            static readonly FieldInfo hideDisplayOnWildAnimals = Field("hideDisplayOnWildAnimals");
            static readonly FieldInfo seasonalItemIndex = CompType == null ? null : AccessTools.Field(CompType, "seasonalItemIndex");

            static FieldInfo Field(string name) => PropsType == null ? null : AccessTools.Field(PropsType, name);

            public static bool Matches(CompProperties props) => PropsType != null && PropsType.IsInstanceOfType(props)
                && resourceAmount != null && gatheringIntervalDays != null;

            public static ThingDef ResourceDef(CompProperties p) => resourceDef?.GetValue(p) as ThingDef;
            public static int ResourceAmount(CompProperties p) => (int)resourceAmount.GetValue(p);
            public static int IntervalDays(CompProperties p) => (int)gatheringIntervalDays.GetValue(p);
            public static string CustomResourceString(CompProperties p) => customResourceString?.GetValue(p) as string;
            public static bool IsRandom(CompProperties p) => isRandom != null && (bool)isRandom.GetValue(p);
            public static List<string> RandomItems(CompProperties p) => randomItems?.GetValue(p) as List<string>;
            public static List<string> VariantItems(CompProperties p) => seasonalItems?.GetValue(p) as List<string>;
            public static bool HasAdditional(CompProperties p) => hasAditional != null && (bool)hasAditional.GetValue(p);
            public static int AdditionalProb(CompProperties p) => additionalItemsProb == null ? 0 : (int)additionalItemsProb.GetValue(p);
            public static int AdditionalNumber(CompProperties p) => additionalItemsNumber == null ? 0 : (int)additionalItemsNumber.GetValue(p);
            public static List<string> AdditionalItems(CompProperties p) => additionalItems?.GetValue(p) as List<string>;
            public static bool HideOnWild(CompProperties p) => hideDisplayOnWildAnimals != null && (bool)hideDisplayOnWildAnimals.GetValue(p);
            public static int VariantIndex(ThingComp comp) => seasonalItemIndex == null ? -1 : (int)seasonalItemIndex.GetValue(comp);
        }
    }
}
