#if DEBUG
#define DEBUG_IMPLIED_DEFS
#define DEBUG_COSTLIST
#define DEBUG_STUFFING
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace StuffedFloors {
    public class FloorTypeDef: TerrainDef {
        private const int FINE_BEAUTY_THRESHOLD = 4;
        // TerrainDef basically contains all the fields we need, so just derive from that.
        // note that we're hardwiring stuffed terrains to the Floors category, that way we
        // can leave designationCategory on null for the floorType, and we don't have to deal
        // with the game adding the category placeholder itself as a buildable floor. This is
        // not exactly a perfect solution, and should be refactored at some point in the (near)
        // future.

        // the stuffcost that will be used for all the generated terrains. Note that this is in addition
        // to the floorTypeDef's base cost.
        public int stuffCost;

        // a list of vanilla or modded terrains that are made obsolete by this category and should be removed.
        // Note that we're using strings instead of defs so that we can manually resolve references, and allow
        // for obsoleting of defs regardless of wether the mods they belong to are actually active.
        public List<string> obsoletes = new();

        // a list of terrains that have been generated for this floorTypeDef
        public List<TerrainDef> terrains = new();

        // list of relevant stats that should be affected by stuff
        public List<StatDef> statsAffectedByStuff = new();

        private DesignatorDropdownGroupDef _designatorGroup;
        public DesignatorDropdownGroupDef DesignatorGroup {
            get {
                if (_designatorGroup == null) {
                    // create and register group.
                    _designatorGroup = new DesignatorDropdownGroupDef {
                        defName = defName
                    };
                    DefDatabase<DesignatorDropdownGroupDef>.Add(_designatorGroup);
                }
                return _designatorGroup;
            }
        }

        public override IEnumerable<string> ConfigErrors() {
            List<string> errors = base.ConfigErrors()?.ToList() ?? new List<string>();
            if (designationCategory != null) {
                errors.Add("FloorTypeDef should not have designationCategory set (hardcoded to Floors)");
            }

            return errors;
        }

        public TerrainDef GetStuffedTerrainDef(ThingDef stuffThingDef) {
            if (!stuffThingDef.IsStuff) {
                throw new ArgumentException(stuffThingDef.defName + " is not a stuff!");
            }


            StuffProperties stuff = stuffThingDef.stuffProps;

            // create new terrain
            TerrainDef terrain = new() {

                // label it as ours
                modContentPack = Controller.Instance.Content,

                // set/change custom data
                designatorDropdown = DesignatorGroup,
                designationCategory = DefDatabase<DesignationCategoryDef>.GetNamed("Floors"),
                generated = true,
                useStuffTerrainAffordance = false,

                // apply stuff elements
                color = stuff.color,
                constructEffect = stuff.constructEffect,
                defName = stuffThingDef.defName + "_" + defName,
                label = "ThingMadeOfStuffLabel".Translate(stuffThingDef.LabelAsStuff, label),
                repairEffect = stuff.constructEffect,

                // copy properties. Everything that could potentially be relevant.
                affordances = affordances.DeepCopy(),
                autoRebuildable = autoRebuildable,
                avoidWander = avoidWander,
                altitudeLayer = altitudeLayer,
                artisticSkillPrerequisite = artisticSkillPrerequisite,
                blocksAltitudes = blocksAltitudes.NullOrEmpty() ? new() : new(blocksAltitudes),
                blueprintDef = blueprintDef,
                bridge = bridge,
                buildingPrerequisites = buildingPrerequisites.DeepCopy(),
                burnedDef = burnedDef,
                canGenerateDefaultDesignator = canGenerateDefaultDesignator,
                changeable = changeable,
                clearBuildingArea = clearBuildingArea,
                constructionSkillPrerequisite = constructionSkillPrerequisite,
                costList = costList.DeepCopy(),
                costListForDifficulty = costListForDifficulty.DeepCopy(),
                description = description,
                descriptionHyperlinks = descriptionHyperlinks,
                defaultPlacingRot = defaultPlacingRot,
                destroyBuildingsOnDestroyed = destroyBuildingsOnDestroyed,
                destroyEffect = destroyEffect,
                destroyEffectWater = destroyEffectWater,
                destroyOnBombDamageThreshold = destroyOnBombDamageThreshold,
                dominantStyleCategory = dominantStyleCategory,
                driesTo = driesTo,
                edgeType = edgeType,
                extinguishesFire = extinguishesFire,
                extraDeteriorationFactor = extraDeteriorationFactor,
                extraDraftedPerceivedPathCost = extraDraftedPerceivedPathCost,
                extraNonDraftedPerceivedPathCost = extraNonDraftedPerceivedPathCost,
                fertility = fertility,
                filthAcceptanceMask = filthAcceptanceMask,
                frameDef = frameDef,
                generatedFilth = generatedFilth,
                holdSnow = holdSnow,
                installBlueprintDef = installBlueprintDef,
                isAltar = isAltar,
                layerable = layerable,
                maxTechLevelToBuild = maxTechLevelToBuild,
                minTechLevelToBuild = minTechLevelToBuild,
                modExtensions = modExtensions.NullOrEmpty() ? new() : new(modExtensions),
                passability = passability,
                pathCost = pathCost,
                pathCostIgnoreRepeat = pathCostIgnoreRepeat,
                placeWorkers = placeWorkers.NullOrEmpty() ? new () : new (placeWorkers),
                placingDraggableDimensions = placingDraggableDimensions,
                renderPrecedence = renderPrecedence,
                researchPrerequisites = researchPrerequisites.DeepCopy(),
                resourcesFractionWhenDeconstructed = resourcesFractionWhenDeconstructed,
                scatterType = scatterType,
                smoothedTerrain = smoothedTerrain,
                specialDisplayRadius = specialDisplayRadius,
                statBases = statBases.DeepCopy(),
                tags = tags.NullOrEmpty() ? new() : new(tags),
                takeFootprints = takeFootprints,
                takeSplashes = takeSplashes,
                terrainAffordanceNeeded = terrainAffordanceNeeded,
                texturePath = texturePath,
                tools = tools.NullOrEmpty() ? new() : new(tools),
                traversedThought = traversedThought,
                waterDepthMaterial = waterDepthMaterial,
                waterDepthShader = waterDepthShader,
                waterDepthShaderParameters = waterDepthShaderParameters.NullOrEmpty() ? new () : new (waterDepthShaderParameters)
            };

            if (stuffCost > 0) {
                if (terrain.costList is not null) {
                    terrain.costList.Add(new(stuffThingDef, Mathf.CeilToInt(stuffCost / stuffThingDef.VolumePerUnit)));
                }
                // I think this is only used for turrets, but it could theoretically be used for terrain as well.
                if (terrain.costListForDifficulty?.costList is not null) {
                    terrain.costListForDifficulty.costList.Add(new(stuffThingDef, Mathf.CeilToInt(stuffCost / stuffThingDef.VolumePerUnit)));
                }
            }

            if (useStuffTerrainAffordance) {
                terrain.terrainAffordanceNeeded = stuffThingDef.terrainAffordanceNeeded;
            }

            // apply stuff offsets and factors, but apply them to a new list of statmodifiers, re-using the same list
            // keeps the actual statmodifier entries around as references, and leads to exponentially increasing stats
            // for terrains of the same base def and different stuffs
            if (!statsAffectedByStuff.NullOrEmpty()) {
                // prepare variables
                StringBuilder text = new();
                foreach (StatDef stat in statsAffectedByStuff) {
                    // get base/default value
                    float value = terrain.statBases.GetStatValueFromList( stat, stat.defaultBaseValue );
                    text.AppendLine($"Base {stat.label} for {terrain.label}: {value}");

                    // apply offset
                    float offset = stuff.statOffsets.GetStatOffsetFromList( stat );

                    // apply factor
                    float factor = ( value >= 0 || stat.applyFactorsIfNegative )
                        ? stuff.statFactors.GetStatFactorFromList( stat )
                        : 1f;

                    // lower impact of stuff beauty on floors
                    if (stat == StatDefOf.Beauty) {
                        offset *= 1 / 3f;
                        factor = Mathf.Sqrt(factor);
                    }

                    // calculate new value
                    float final = ( value + offset ) * factor;
                    text.AppendLine($"\tstuffed: ({value} + {offset}) x {factor} = {final}");

                    StatUtility.SetStatValueInList(ref terrain.statBases, stat, final);
                }

#if DEBUG_IMPLIED_DEFS
                Log.Message($"Created {terrain.defName} from {stuffThingDef.defName}");
#if DEBUG_COSTLIST
                foreach (ThingDefCountClass count in terrain.costList) {
                    Log.Message($"\t{count.thingDef.defName}: {count.count}");
                }
#endif
#if DEBUG_STUFFING
                Log.Message(text.ToString());
#endif
#endif

            }

            // apply FineFloor tag if beauty > threshold
            float beauty = terrain.GetStatValueAbstract(StatDefOf.Beauty);
            if (!terrain.tags.Any(t => t == "FineFloor") && beauty > FINE_BEAUTY_THRESHOLD) {
#if DEBUG
                Log.Message($"fine floor tag added to {terrain.defName} (beauty {beauty})");
#endif
                terrain.tags.Add("FineFloor");
            }

            return terrain;
        }
    }
}
