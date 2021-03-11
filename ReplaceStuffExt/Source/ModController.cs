using HarmonyLib;
using RimWorld;
using System;
using System.Collections;
using System.Reflection;
using Verse;

namespace ReplaceStuffExt
{
    [StaticConstructorOnStartup]
    public class ReplaceStuffExt : Mod
    {
        public static Harmony harmony;
        public static IList ReplaceList;
        public static ConstructorInfo ReplacementConstructor;

        public ReplaceStuffExt(ModContentPack content) : base(content)
        {
            harmony = new Harmony("Oblitus.ReplaceStuffExt"); // only actually used for AccessTools

            if (AccessTools.TypeByName("Replace_Stuff.NewThing.NewThingReplacement") == null) // Replace Stuff not found... should never happen since it is a dependancy
                return;

            ReplaceList = ((IList)AccessTools.Field(AccessTools.TypeByName("Replace_Stuff.NewThing.NewThingReplacement"), "replacements").GetValue(null));
            ReplacementConstructor = AccessTools.TypeByName("Replace_Stuff.NewThing.NewThingReplacement").GetNestedType("Replacement").GetConstructors()[0];

            ReplaceStuffPatch();
        }

        public void ReplaceStuffPatch()
        {
            Action<Thing, Thing> transferBills = (n, o) =>
            {
                Building_WorkTable newTable = n as Building_WorkTable;
                Building_WorkTable oldTable = o as Building_WorkTable;

                foreach (Bill bill in oldTable.BillStack)
                {
                    newTable.BillStack.AddBill(bill);
                }
            };


            AddReplacement(d => typeof(Building_Battery).IsAssignableFrom(d.thingClass));
            AddReplacement(d => d.defName.Contains("lamp") && d.HasComp(typeof(CompGlower)));


            Type Building_SunLamp = AccessTools.TypeByName("RimWorld.Building_SunLamp");
            AddReplacement(d => Building_SunLamp.IsAssignableFrom(d.thingClass));

            Type Building_DoorExpanded = AccessTools.TypeByName("DoorsExpanded.Building_DoorExpanded");
            if (Building_DoorExpanded != null) AddReplacement(d => IsWall(d) || typeof(Building_Door).IsAssignableFrom(d.thingClass) || Building_DoorExpanded.IsAssignableFrom(d.thingClass));

            AddReplacement(d => d.Equals(ThingDefOf.NutrientPasteDispenser), d => IsWall(d));

            AddReplacement(d => d.PlaceWorkers?.Any(p => p.GetType() == typeof(PlaceWorker_OnSteamGeyser)) ?? true);
            AddReplacement(d => d.PlaceWorkers?.Any(p => p.GetType() == typeof(PlaceWorker_WatermillGenerator)) ?? true);
            AddReplacement(d => d.PlaceWorkers?.Any(p => p.GetType() == typeof(PlaceWorker_WindTurbine)) ?? true);

            // VFE benches
            AddReplacement(d => d.defName.Equals("VFE_TableButcherElectric"), d => d.defName.Equals("TableButcher"), transferBills);
            AddReplacement(d => d.defName.Equals("FabricationBench"), d => d.defName.Equals("VFE_ComponentFabricationBench"), transferBills);
            AddReplacement(d => d.defName.Equals("VFE_TableDrugLabElectric"), d => d.defName.Equals("DrugLab"), transferBills);
            AddReplacement(d => d.defName.Equals("ElectricSmelter"), d => d.defName.Equals("VFE_FueledSmelter"), transferBills);
            AddReplacement(d => d.defName.Equals("VFE_TableStonecutterElectric"), d => d.defName.Equals("TableStonecutter"), transferBills);

            AddReplacement(d => d.defName.Equals("VFE_TableMachiningLarge"), d => d.defName.Equals("TableMachining"), transferBills);
            AddReplacement(d => d.defName.Equals("VFE_TableSmithyLarge"), d => d.defName.Equals("ElectricSmithy") || d.defName.Equals("FueledSmithy"), transferBills);
            AddReplacement(d => d.defName.Equals("VFE_TableStoveLarge"), d => d.defName.Equals("ElectricStove") || d.defName.Equals("FueledStove"), transferBills);
            AddReplacement(d => d.defName.Equals("VFE_TableTailorLarge"), d => d.defName.Equals("ElectricTailoringBench") || d.defName.Equals("HandTailoringBench"), transferBills);

            AddReplacement(d => d.defName.Equals("VBE_TypewritersTable"), d => d.defName.Equals("VBE_WritersTable"), transferBills);
        }

        public void AddReplacement(String oldDefName, String newDefName)
        {
            AddReplacement(d => d.defName.Equals(oldDefName), d => d.defName.Equals(newDefName));
        }

        public void AddReplacement(Predicate<ThingDef> n, Predicate<ThingDef> o = null, Action<Thing, Thing> preAction = null, Action<Thing, Thing> postAction = null)
        {
            ReplaceList.Add(
                ReplacementConstructor.Invoke(new object[] { n, o, preAction, postAction })
            );
        }

        public static bool IsWall(BuildableDef bdef)
        {
            return bdef is ThingDef def && def.coversFloor && def.holdsRoof && def.passability == Traversability.Impassable &&
                (!def.building?.isNaturalRock ?? true);
        }
    }
}
