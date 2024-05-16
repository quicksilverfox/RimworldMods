using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ResearchPowl
{
    [StaticConstructorOnStartup]
    public static class ModCompatibility
    {
        public static readonly bool UsingVanillaVehiclesExpanded;

        public static readonly bool UsingVanillaExpanded;

        public static readonly bool UsingRimedieval;


        public static readonly MethodInfo IsDisabledMethod;
        public static readonly MethodInfo TechLevelAllowedMethod;
        public static readonly MethodInfo GetAllowedProjectDefsMethod;
        public static readonly List<ResearchProjectDef> AllowedResearchDefs;

        static ModCompatibility()
        {
            UsingRimedieval = ModLister.GetActiveModWithIdentifier("Ogam.Rimedieval") != null;
            AllowedResearchDefs = new();

            if (UsingRimedieval)
            {
                var defCleanerType = AccessTools.TypeByName("Rimedieval.DefCleaner");
                if (defCleanerType == null)
                {
                    Log.Debug("[FluffyResearchTree]: Failed to find the DefCleaner-type in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                    UsingRimedieval = false;
                }
                else
                {
                    GetAllowedProjectDefsMethod = AccessTools.Method(defCleanerType, "GetAllowedProjectDefs", new[] { typeof(List<ResearchProjectDef>) });
                    if (GetAllowedProjectDefsMethod == null)
                    {
                        Log.Debug("[FluffyResearchTree]: Failed to find method GetAllowedProjectDefs in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                        UsingRimedieval = false;
                    }
                    else
                    {
                        AllowedResearchDefs = (List<ResearchProjectDef>)GetAllowedProjectDefsMethod.Invoke(null, new[] { DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def => def.knowledgeCategory == null) });
                    }
                }
            }

            UsingVanillaExpanded = ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaFactionsExpanded.Core") != null;
            if (UsingVanillaExpanded)
            {
                var storyTellerUtility = AccessTools.TypeByName("VanillaStorytellersExpanded.CustomStorytellerUtility");
                if (storyTellerUtility == null)
                {
                    Log.Debug("[FluffyResearchTree]: Failed to find the CustomStorytellerUtility-type in VanillaExpanded. Will not be able to show or block research based on storyteller limitations.");
                    UsingVanillaExpanded = false;
                }
                else
                {
                    TechLevelAllowedMethod = AccessTools.Method(storyTellerUtility, "TechLevelAllowed", new[] { typeof(TechLevel) });
                    if (TechLevelAllowedMethod == null)
                    {
                        Log.Debug("[FluffyResearchTree]: Failed to find method TechLevelAllowed in VanillaExpanded. Will not be able to show or block research based on storyteller limitations.");
                        UsingVanillaExpanded = false;
                    }
                }
            }

            UsingVanillaVehiclesExpanded = ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaVehiclesExpanded") != null;

            if (UsingVanillaVehiclesExpanded)
            {
                var utilsType = AccessTools.TypeByName("VanillaVehiclesExpanded.Utils");
                if (utilsType == null)
                {
                    Log.Debug("[FluffyResearchTree]: Failed to find the Utils-type in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                    UsingVanillaVehiclesExpanded = false;
                }
                else
                {
                    var utilsMethods = AccessTools.GetDeclaredMethods(utilsType);
                    if (utilsMethods == null || !utilsMethods.Any())
                    {
                        Log.Debug("[FluffyResearchTree]: Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                        UsingVanillaVehiclesExpanded = false;
                    }
                    else
                    {
                        IsDisabledMethod = utilsMethods.FirstOrDefault(methodInfo => methodInfo.GetParameters().Length == 2);
                        if (IsDisabledMethod == null)
                        {
                            Log.Debug("[FluffyResearchTree]: Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                            UsingVanillaVehiclesExpanded = false;
                        }
                    }
                }
            }
        }
    }
}
