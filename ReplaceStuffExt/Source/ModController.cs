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
            AddReplacement(d => d.GetCompProperties<CompProperties_Glower>()?.glowRadius >= 10); // loose way to replace lamps
            AddReplacement(d => d.GetCompProperties<CompProperties_Glower>()?.overlightRadius > 0); // any sun lamp on any sun lamp

            Type Building_DoorExpanded = AccessTools.TypeByName("DoorsExpanded.Building_DoorExpanded"); // DoorsExpanded patch
            if (Building_DoorExpanded != null) AddReplacement(d => IsWall(d) || typeof(Building_Door).IsAssignableFrom(d.thingClass) || Building_DoorExpanded.IsAssignableFrom(d.thingClass));

            AddReplacement(d => d.Equals(ThingDefOf.NutrientPasteDispenser), d => IsWall(d)); // nutrient paste dispenser can replace walls

            // power production
            AddReplacement(d => d.PlaceWorkers?.Any(p => p.GetType() == typeof(PlaceWorker_OnSteamGeyser)) ?? false); // any steam on any steam
            AddReplacement(d => d.PlaceWorkers?.Any(p => p.GetType() == typeof(PlaceWorker_WatermillGenerator)) ?? false); // any watermill on any watermill
            AddReplacement(d => d.PlaceWorkers?.Any(p => p.GetType() == typeof(PlaceWorker_WindTurbine)) ?? false); // any wind on any wind
            AddReplacement(d => d.HasComp(typeof(CompPowerPlantSolar))); // any  solar on any solar
            AddReplacement(d => d.HasComp(typeof(CompProperties_Battery))); // any batteries on any batteries

            // VFE: Vanilla Furniture Extended 1880253632
            // VPM: Vanilla Furniture Extended - Power Module 2062943477
            // VBE: Vanilla Books Extended
            GenReplacements("FueledStove", "ElectricStove", "VPE_GasStove", "VFE_TableStoveLarge");
            GenReplacements("ElectricSmelter", "VFE_FueledSmelter", "VPE_GasSmelter");
            GenReplacements("FueledSmithy", "ElectricSmithy", "VPE_GasSmithy", "VFE_TableSmithyLarge");
            GenReplacements("BiofuelRefinery", "VPE_GasBiofuelRefinery");
            GenReplacements("HandTailoringBench", "ElectricTailoringBench", "VFE_TableTailorLarge");
            GenReplacements("TableMachining", "VFE_TableMachiningLarge");
            GenReplacements("TableStonecutter", "VFE_TableStonecutterElectric");
            GenReplacements("DrugLab", "VFE_TableDrugLabElectric");
            GenReplacements("FabricationBench", "VFE_ComponentFabricationBench");
            GenReplacements("TableButcher", "VFE_TableButcherElectric");

            GenReplacements("PodLauncher", "VPE_GasPodLauncher");
            GenReplacements("VBE_TypewritersTable", "VBE_WritersTable");

            // SoS2 structurals (technically vanilla too but who cares about vanilla ship)
            AddReplacement(d => (d.building?.shipPart ?? false) && (d.holdsRoof || d.passability == Traversability.Impassable));
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

        void GenReplacements(params String[] variants)
        {
            for (int i = 0; i < variants.Length; i++)
            {
                String from = variants[i];
                for (int x = 0; x < variants.Length; x++)
                {
                    if (x == i)
                        continue;
                    String to = variants[x];
                    AddReplacement(d => d.defName.Equals(from), d => d.defName.Equals(to), transferBills);
                }
            }
        }

        Action<Thing, Thing> transferBills = (n, o) =>
        {
            Building_WorkTable newTable = n as Building_WorkTable;
            Building_WorkTable oldTable = o as Building_WorkTable;

            foreach (Bill bill in oldTable.BillStack)
            {
                newTable.BillStack.AddBill(bill);
            }
        };

        public static bool IsWall(BuildableDef bdef)
        {
            return bdef is ThingDef def && def.coversFloor && def.holdsRoof && def.passability == Traversability.Impassable &&
                (!def.building?.isNaturalRock ?? true);
        }
    }
}
