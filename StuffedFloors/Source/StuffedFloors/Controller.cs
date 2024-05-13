#if DEBUG
#define DEBUG_IMPLIED_DEFS
//#define DEBUG_COSTLIST
//#define DEBUG_STUFFING
#endif

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StuffedFloors {
    public class Controller: Mod {
        // use static constructoronstartup for a late init point (after all defs of all mods are loaded).
        [StaticConstructorOnStartup]
        public static class Init {
            static Init() {
                Initialize();
            }
        }

        public static Controller Instance { get; protected set; }

        // constructor as an early init point (before any defs are loaded).
        public Controller(ModContentPack content) : base(content) {
            Harmony harmony = new Harmony( "Fluffy.StuffedFloors" );
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Instance = this;

            // Patches;
            // HarmonyPatch_GenerateImpliedDefs_PreResolve Prefix to add implied terrains
        }

        public static void Initialize() {
            // get all the floortypedefs from the game's library
            List<FloorTypeDef> floorTypes = DefDatabase<FloorTypeDef>.AllDefsListForReading;

            // hide obsoleted vanilla and modded floors
            IEnumerable<TerrainDef> obsoleteDesignators = floorTypes
                .SelectMany( f => f.obsoletes)
                .Select( n => DefDatabase<TerrainDef>.GetNamedSilentFail( n ) )
                .Where( t => t != null )
                .Distinct();
            DesignatorUtility.RemoveDesignators(obsoleteDesignators);

            // remove category added by More Floors
            RemoveObsoleteCategory("MoreFloors");
        }

        private static void RemoveObsoleteCategory(string categoryName) {
            DesignationCategoryDef category = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail( categoryName );
            if (category != null) {
                DesignationCategoryDef floorsCategory = DefDatabase<DesignationCategoryDef>.GetNamed( "Floors" );
                DesignatorUtility.MergeDesignationCategories(floorsCategory, category);
            }
        }

        private static readonly Dictionary<StuffCategoryDef, List<ThingDef>> _stuffCache = new Dictionary<StuffCategoryDef, List<ThingDef>>();

        public static List<ThingDef> GetStuffDefsFor(List<StuffCategoryDef> stuffCategoryDefs) {
            return stuffCategoryDefs.SelectMany(GetStuffDefsFor).ToList();
        }

        public static List<ThingDef> GetStuffDefsFor(StuffCategoryDef stuffCategory) {
            // there doesn't seem to be a good way to get a list of things by stuffType, so we'll have to match the list manually.
            // I really don't want to loop this list for every single floorTypeDef, so let's cache this

            // try get from cache
            if (_stuffCache.TryGetValue(stuffCategory, out List<ThingDef> stuffs)) {
                return stuffs;
            }

            // manually fetch list.
            stuffs = DefDatabase<ThingDef>.AllDefsListForReading
                                          .Where(td => td.IsStuff && td.stuffProps.categories.Contains(stuffCategory))
                                          .ToList();

            // cache
            _stuffCache.Add(stuffCategory, stuffs);
            return stuffs;
        }
    }
}
