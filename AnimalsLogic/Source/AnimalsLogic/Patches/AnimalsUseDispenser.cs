using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;

namespace AnimalsLogic
{
    class AnimalsUseDispenser
    {
        public static void Patch()
        {
            AnimalsLogic.harmony.Patch(
                typeof(FoodUtility).GetMethod("BestFoodSourceOnMap"),
                transpiler: new HarmonyMethod(typeof(AnimalsUseDispenser).GetMethod(nameof(BestFoodSourcePatch)))
                );

            //AnimalsLogic.harmony.Patch(
            //    typeof(FoodUtility).GetMethod("TryFindBestFoodSourceFor_NewTemp"),
            //    transpiler: new HarmonyMethod(typeof(AnimalsUseDispenser).GetMethod(nameof(BestFoodSourcePatch)))
            //    );

            AnimalsLogic.harmony.Patch(
                System.Array.Find(
                    typeof(JobDriver_Ingest).GetMethods(AccessTools.all),
                    p => p.Name.Equals("PrepareToIngestToils_Dispenser") // private method
                    ),
                prefix: new HarmonyMethod(typeof(AnimalsUseDispenser).GetMethod(nameof(PrepareToIngestToils_DispenserPrefix)))
                );
        }

        /**
         * Allows non-intelligent pawns to use dispenser. Seriously, even pidgeons are smart enough to understand "press button for food" technology. Well, ok, coalas aren't smart enough, but they are just too dumb to live.
         */
        [HarmonyTranspiler]
        public static List<CodeInstruction> BestFoodSourcePatch(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            var target = AccessTools.PropertyGetter(typeof(RaceProperties), "ToolUser");
            var replacement = AccessTools.Method(typeof(AnimalsUseDispenser), nameof(ToolUser));

            bool patched = false;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand != null && codes[i].operand.Equals(target))
                {
                    codes[i].operand = replacement;
                    patched = true;
                    break;
                }
            }

            if (!patched)
            {
                Log.Error("[AnimalsLogic] Unable to patch FoodUtility: could not find call to RaceProperties.ToolUser.");
            }

            return codes;
        }

        public static bool ToolUser(RaceProperties prop)
        {
            return prop.ToolUser || Settings.use_dispenser;
        }

        /**
         * A dirty, detouring patch, but too much work to transpile.
         */
        [HarmonyPrefix]
        public static bool PrepareToIngestToils_DispenserPrefix(ref JobDriver_Ingest __instance, ref IEnumerable<Toil> __result)
        {
            if (WildManUtility.AnimalOrWildMan(__instance.pawn)) // AnimalOrWildMan used to support Pawnmorpher sentient former humans
            {
                __result = PrepareToIngestToils_DispenserOverride(__instance.pawn);
                return false;
            }
            return true;
        }

        /**
         * Damn enumerators
         */
        static private IEnumerable<Toil> PrepareToIngestToils_DispenserOverride(Pawn pawn)
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, pawn);
            yield return /*Toils_Ingest.*/CarryIngestibleToChewSpot(pawn, TargetIndex.A).FailOnDestroyedNullOrForbidden(TargetIndex.A);
            //yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        }

        /**
         * Move few steps srom dispenser to avoid clustering in a single cell
         */
        public static Toil CarryIngestibleToChewSpot(Pawn pawn, TargetIndex ingestibleInd)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                IntVec3 intVec = IntVec3.Invalid;
                Thing thing = null;
                Thing thing2 = actor.CurJob.GetTarget(ingestibleInd).Thing;

                intVec = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(ingestibleInd).Thing);
                Danger chewSpotDanger = intVec.GetDangerFor(pawn, actor.Map);
                if (chewSpotDanger != Danger.None)
                {
                    thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor), thing2.def.ingestible.chairSearchRadius, (Thing t) => (int)t.Position.GetDangerFor(pawn, t.Map) <= (int)chewSpotDanger);
                }
                if (thing != null)
                {
                    intVec = thing.Position;
                    actor.Reserve(thing, actor.CurJob);
                }

                actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, intVec);
                actor.pather.StartPath(intVec, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }
    }
}
