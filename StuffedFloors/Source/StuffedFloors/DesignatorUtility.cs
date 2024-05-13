// DesignatorUtility.cs
// Copyright Karel Kroeze, 2018-2018

using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StuffedFloors {
    public static class DesignatorUtility {
        public static void RemoveDesignators(IEnumerable<TerrainDef> terrains) {
            HashSet<DesignationCategoryDef> affectedCategories = new HashSet<DesignationCategoryDef>();
            foreach (TerrainDef terrain in terrains) {
                affectedCategories.Add(terrain.designationCategory);

                terrain.designatorDropdown = null;
                terrain.designationCategory = null;
            };

            foreach (DesignationCategoryDef affectedCategory in affectedCategories) {
                RecacheDesignationCategory(affectedCategory);
            }
        }

        public static void MergeDesignationCategories(DesignationCategoryDef target, DesignationCategoryDef source) {
            // change designation category for all build designators in source
            foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefs) {
                if (terrain.designationCategory == source) {
                    terrain.designationCategory = target;
                }
            }

            // add specials that don't exist in target yet
            foreach (System.Type designator in source.specialDesignatorClasses) {
                if (!target.specialDesignatorClasses.Contains(designator)) {
                    target.specialDesignatorClasses.Add(designator);
                }
            }

            // recache target
            RecacheDesignationCategory(target);

            // remove source
            RemoveDesignationCategory(source);
        }

        private static void RemoveDesignationCategory(DesignationCategoryDef category) {
            (DefDatabase<DesignationCategoryDef>.AllDefs as List<DesignationCategoryDef>)?.Remove(category);
            RecacheDesignationCategories();
        }

        private static void RecacheDesignationCategory(DesignationCategoryDef category) {
            category.ResolveReferences(); // calls ResolveDesignators, recreating cache;
        }

        private static void RecacheDesignationCategories() {
            Traverse.Create(MainButtonDefOf.Architect.TabWindow).Method("CacheDesPanels").GetValue();
        }
    }
}
