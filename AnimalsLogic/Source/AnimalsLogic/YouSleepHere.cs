using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Reflection.Emit;
using System.Reflection;

namespace AnimalsLogic
{
    /*
     * Animals needing medical attention would stay in bed even if it is outside their allowed area.
     * 
     * Also, allows to assign beds to specific animals. Idea (and even one line of code) shamelessly stolen from The Salad Spinner of Woe!!
     */

    class YouSleepHere
    {
        // Patches area seeking job so animal in a medical bed would stay.
        [HarmonyPatch(typeof(JobGiver_SeekAllowedArea), "TryGiveJob", new Type[] { typeof(Pawn) })]
        static class JobGiver_SeekAllowedArea_TryGiveJob_Patch
        {
            static bool Prefix(ref Job __result, Pawn pawn)
            {
                if (pawn.RaceProps.Animal && pawn.InBed() && (pawn.CurrentBed().Medical || HealthAIUtility.ShouldSeekMedicalRestUrgent(pawn) || pawn.health.hediffSet.HasTendedAndHealingInjury()))
                {
                    __result = null;
                    return false;
                }
                return true;
            }
        }

        // Patches GetGizmos to display medical and set owner buttons
        [HarmonyPatch(typeof(Building_Bed), "GetGizmos", new Type[0])]
        static class Building_Bed_GetGizmos_Patch
        {
            static void Postfix(ref IEnumerable<Gizmo> __result, ref Building_Bed __instance)
            {
                if (__instance.Faction != Faction.OfPlayer || __instance.def.building.bed_humanlike)
                    return;

                if (__instance.def.building.bed_maxBodySize <= 0.01)
                    return; // bodysize check is for compatibility with Dubs Hygiene - he made his bathtubs as animal beds.

                Building_Bed bed = __instance;

                if (bed.GetType().Name == "Building_MechanoidPlatform" || bed.GetType().Name == "Building_PortableChargingPlatform" || bed.def.designationCategory.defName == "WTH_Hacking")
                    return; // for some other mods

                var gizmos = new List<Gizmo>(__result)
                {
                    new Command_Toggle
                    {
                        defaultLabel = "CommandBedSetAsMedicalLabel".Translate(),
                        defaultDesc = "CommandBedSetAsMedicalDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/AsMedical", true),
                        isActive = (() => bed.Medical),
                        toggleAction = delegate
                        {
                            bed.Medical = !bed.Medical;
                        },
                        hotKey = KeyBindingDefOf.Misc2
                    }
                };

                if (!bed.Medical)
                {
                    gizmos.Add(
                        new Command_Action
                        {
                            defaultLabel = "CommandThingSetOwnerLabel".Translate(),
                            icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner", true),
                            defaultDesc = "CommandBedSetOwnerDesc".Translate(),
                            action = delegate
                            {
                                Find.WindowStack.Add(new Dialog_AssignBuildingOwner(bed.CompAssignableToPawn));
                            },
                            hotKey = KeyBindingDefOf.Misc3
                        }
                    );
                }

                __result = gizmos.AsEnumerable();
            }
        }

        // patches get_AssigningCandidates to display list of animals
        [HarmonyPatch(typeof(CompAssignableToPawn), "get_AssigningCandidates")]
        static class CompAssignableToPawn_get_AssigningCandidates_Patch
        {
            static void Postfix(ref IEnumerable<Pawn> __result, ref CompAssignableToPawn __instance)
            {
                if (__instance.parent == null || !(__instance.parent is Building_Bed))
                    return;

                Building_Bed bed = (Building_Bed)__instance.parent;

                if (bed.Faction != Faction.OfPlayer || bed.def.building.bed_humanlike)
                    return;

                if (bed.def.building.bed_maxBodySize <= 0.01)
                    return; // bodysize check is for compatibility with Dubs Hygiene - he made his bathtubs as animal beds.

                if (bed.GetType().Name == "Building_MechanoidPlatform" || bed.GetType().Name == "Building_PortableChargingPlatform" || bed.def.designationCategory.defName == "WTH_Hacking")
                    return; // for some other mods

                if (!bed.Spawned)
                {
                    __result = Enumerable.Empty<Pawn>();
                    return;
                }

                __result = from p in Find.CurrentMap.mapPawns.AllPawns
                           where p.RaceProps.Animal && p.Faction == Faction.OfPlayer
                           select p;
            }
        }

        // patches TryAssignPawn to accept animals
        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "TryAssignPawn", new Type[] { typeof(Pawn) })]
        static class CompAssignableToPawn_Bed_TryAssignPawn_Patch
        {
            static bool Prefix(Pawn pawn)
            {
                if (pawn.ownership == null)
                    pawn.ownership = new Pawn_Ownership(pawn);
                return true;
            }
        }

        // patches AssignedAnything to accept animals
        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "AssignedAnything", new Type[] { typeof(Pawn) })]
        static class CompAssignableToPawn_Bed_AssignedAnything_Patch
        {
            static bool Prefix(Pawn pawn)
            {
                if (pawn.ownership == null)
                    pawn.ownership = new Pawn_Ownership(pawn);
                return true;
            }
        }

        // patches set_Medical to allow medical assignment for non-humanlikes
        [HarmonyPatch(typeof(Building_Bed), "set_Medical", new Type[] { typeof(bool) })]
        static class Building_Bed_set_Medical_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo bed_humanlike = typeof(BuildingProperties).GetField("bed_humanlike");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    //IL_000d: ldfld class Verse.ThingDef Verse.Thing::def
                    //IL_0012: ldfld class RimWorld.BuildingProperties Verse.ThingDef::building
                    //IL_0017: ldfld bool RimWorld.BuildingProperties::bed_humanlike
                    if (codes[i].opcode == OpCodes.Ldfld)
                    {
                        FieldInfo operand = (FieldInfo)codes[i].operand;
                        if (operand == bed_humanlike)
                        {
                            codes.Insert(i + 1, new CodeInstruction(OpCodes.Pop));
                            codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4_1));
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }

        // patches GetInspectString to display all stats for non-humanlike beds
        [HarmonyPatch(typeof(Building_Bed), "GetInspectString", new Type[0])]
        static class Building_Bed_GetInspectString_Patch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo bed_humanlike = typeof(BuildingProperties).GetField("bed_humanlike");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    //IL_000d: ldfld class Verse.ThingDef Verse.Thing::def
                    //IL_0012: ldfld class RimWorld.BuildingProperties Verse.ThingDef::building
                    //IL_0017: ldfld bool RimWorld.BuildingProperties::bed_humanlike
                    if (codes[i].opcode == OpCodes.Ldfld)
                    {
                        FieldInfo operand = (FieldInfo)codes[i].operand;
                        if (operand == bed_humanlike)
                        {
                            codes.Insert(i + 1, new CodeInstruction(OpCodes.Pop));
                            codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4_1));
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }

        /*
         * Patches RestUtility to find correct animal bed for medical purposes - medical if possible, assigned otherwise. Otherwise they would just lay down on nearest one and claim it.
         */

        [HarmonyPatch]
        static class RestUtility_FindPatientBedFor_Patch
        {
            static MethodInfo TargetMethod()
            {
                MethodInfo method = typeof(RestUtility).GetNestedTypes(AccessTools.all).First(
                        inner_class => inner_class.GetField("medBedValidator", AccessTools.all) != null // medBedValidator field should be reliable enough signature
                    ).GetMethods(AccessTools.all).First(
                        m => m.Name.Contains("<FindPatientBedFor>")
                    );

                if (method == null)
                    Log.Error("Animal Logic is unable to detect FindPatientBedFor inner method.");

                return method;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // IL_0025: ldfld bool RimWorld.BuildingProperties::bed_humanlike
                FieldInfo bed_humanlike = typeof(BuildingProperties).GetField("bed_humanlike");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld)
                    {
                        FieldInfo operand = (FieldInfo)codes[i].operand;
                        if (operand == bed_humanlike)
                        {
                            //IL_001a: ldloc.0
                            //IL_001b: ldfld class Verse.ThingDef Verse.Thing::def
                            //IL_0020: ldfld class RimWorld.BuildingProperties Verse.ThingDef::building
                            //IL_0025: ldfld bool RimWorld.BuildingProperties::bed_humanlike
                            //IL_002a: brfalse IL_0031

                            codes.RemoveAt(i - 3);
                            codes.RemoveAt(i - 3);
                            codes.RemoveAt(i - 3);
                            codes.RemoveAt(i - 3);
                            codes.RemoveAt(i - 3);
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }

        /*
         * Patches WorkGiver_TakeToBedToOperate, so it won't stuck when animal is already moving to bed and therefore reserved it.
         */

        // public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        [HarmonyPatch(typeof(WorkGiver_TakeToBedToOperate), "HasJobOnThing", new Type[] { typeof(Pawn), typeof(Thing), typeof(bool) })]
        static class WorkGiver_TakeToBedToOperate_HasJobOnThing_Patch
        {
            static bool Prefix(ref bool __result, Pawn pawn, Thing t)
            {
                if (t is Pawn pawn2 && pawn2.CurJob != null && pawn2.CurJob.def == JobDefOf.LayDown)
                {
                    __result = false;
                    return false;
                }

                return true;
            }
        }

        /*
         * Patches JobGiver_RescueNearby, so it won't rescue animals designated for slaughter.
         */

        // protected override Job TryGiveJob(Pawn pawn)
        [HarmonyPatch]
        static class JobGiver_RescueNearby_TryGiveJob_Patch
        {
            static MethodInfo TargetMethod()
            {
                var toils_inner = typeof(JobGiver_RescueNearby).GetNestedTypes(AccessTools.all);
                foreach (var inner_class in toils_inner)
                {
                    var methods = inner_class.GetMethods(AccessTools.all);
                    foreach (var method in methods)
                    {
                        if (method.Name.Contains("<TryGiveJob>"))
                            return method;
                    }
                }
                Log.Error("Animal Logic is unable to detect JobGiver_RescueNearby.TryGiveJob inner method.");
                return null;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                object return_false = null;

                // Trying to find address for false return bracnch for later use
                for (int i = 0; i < codes.Count; i++)
                {
                    // IL_0064: call bool Verse.AI.GenAI::EnemyIsNear(class Verse.Pawn, float32)
#pragma warning disable CS0252 // Possible unintended reference comparison; left hand side needs cast
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand == typeof(GenAI).GetMethod(nameof(GenAI.EnemyIsNear)))
#pragma warning restore CS0252 // Possible unintended reference comparison; left hand side needs cast
                    {
                        // IL_0069: brfalse IL_0070
                        return_false = codes[i + 1].operand;
                        break;
                    }
                }

                if (return_false == null)
                    Log.Error("Animal Logic is unable to find injection point for JobGiver_RescueNearby.TryGiveJob.");

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ret)
                    {
                        codes.InsertRange(i + 1,
                            new List<CodeInstruction>() {
                                new CodeInstruction(OpCodes.Ldloc_0),
                                new CodeInstruction(OpCodes.Call, typeof(JobGiver_RescueNearby_TryGiveJob_Patch).GetMethod(nameof(IsDesignatedForSlaughter))),
                                new CodeInstruction(OpCodes.Brtrue, return_false)
                            });

                        break;
                    }
                }

                return codes.AsEnumerable();
            }

            public static bool IsDesignatedForSlaughter(Pawn p)
            {
                return p.Map.designationManager.DesignationOn(p, DesignationDefOf.Slaughter) != null;
            }
        }
    }
}
