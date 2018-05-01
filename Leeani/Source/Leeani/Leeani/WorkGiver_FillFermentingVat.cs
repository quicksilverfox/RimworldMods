using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace Leeani
{
    class WorkGiver_FillFermentingVat : WorkGiver_Scanner
    {
        private string TemperatureTrans;

        private string NoInputTrans;

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                WorkGiverDynamicDef dyn_def = def as WorkGiverDynamicDef;
                if(dyn_def != null && dyn_def.inputThingDef != null)
                {
                    return ThingRequest.ForDef(dyn_def.inputThingDef);
                }

                return ThingRequest.ForDef(ThingDefOf.FermentingBarrel);
            }
        }

        public JobDef JobDefToUse
        {
            get
            {
                WorkGiverDynamicDef dyn_def = def as WorkGiverDynamicDef;
                if (dyn_def != null && dyn_def.jobDef != null)
                {
                    return dyn_def.jobDef;
                }

                return DefDatabase<JobDef>.GetNamed("FillFermentingVat");
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public void Reset()
        {
            if(TemperatureTrans == null || TemperatureTrans == "" ||
               NoInputTrans == null || NoInputTrans == "")
            {
                /*WorkGiverDynamicDef dyn_def = def as WorkGiverDynamicDef;
                if (dyn_def != null && dyn_def.translations != null)
                {
                    if (dyn_def.translations.ContainsKey("BadTemperature"))
                        TemperatureTrans = dyn_def.translations["BadTemperature"].Translate().ToLower();

                    if (dyn_def.translations.ContainsKey("NoInput"))
                        NoInputTrans = dyn_def.translations["NoInput"].Translate();
                }
                else*/
                {
                    TemperatureTrans = "BadTemperature".Translate().ToLower();
                    NoInputTrans = "NoBerries".Translate();
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced)
        {
            Building_FermentingVat building_FermentingVat = t as Building_FermentingVat;
            if (building_FermentingVat == null || building_FermentingVat.Fermented || building_FermentingVat.SpaceLeftForInput <= 0)
            {
                return false;
            }
            float temperature = building_FermentingVat.Position.GetTemperature(building_FermentingVat.Map);
            CompProperties_TemperatureRuinable compProperties = building_FermentingVat.def.GetCompProperties<CompProperties_TemperatureRuinable>();
            if (temperature < compProperties.minSafeTemperature + 2f || temperature > compProperties.maxSafeTemperature - 2f)
            {
                Reset();
                JobFailReason.Is(TemperatureTrans);
                return false;
            }
            if (t.IsForbidden(pawn) || !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1))
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }
            if (this.FindInputThing(pawn, building_FermentingVat) == null)
            {
                Reset();
                JobFailReason.Is(NoInputTrans);
                return false;
            }
            WorkGiverDynamicDef dyn_def = def as WorkGiverDynamicDef;
            if (dyn_def != null && dyn_def.inputThingDef != null)
            {
                if (building_FermentingVat.def != dyn_def.inputThingDef)
                    return false;
            }

            return !t.IsBurning();
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced)
        {
            Building_FermentingVat building_FermentingVat = (Building_FermentingVat)t;
            Thing t2 = this.FindInputThing(pawn, building_FermentingVat);

            return new Job(JobDefToUse, t, t2)
            {
                count = building_FermentingVat.SpaceLeftForInput
            };
        }

        private Thing FindInputThing(Pawn pawn, Building_FermentingVat barrel)
        {
            Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1);
            Predicate<Thing> validator = predicate;

            ThingDef input_def = null;

            ExtraThingDef extra_def = barrel.def as ExtraThingDef;
            if (extra_def != null && extra_def.vatProperties != null)
            {
                input_def = extra_def.vatProperties.inputThingDef;
            }

            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(input_def), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
        }
    }
}
