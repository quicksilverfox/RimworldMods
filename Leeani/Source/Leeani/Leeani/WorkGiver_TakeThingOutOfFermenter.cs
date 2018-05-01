using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace Leeani
{
    public class WorkGiver_TakeThingOutOfFermenter : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                WorkGiverDynamicDef dyn_def = def as WorkGiverDynamicDef;
                if (dyn_def != null && dyn_def.outputThingDef != null)
                {
                    return ThingRequest.ForDef(dyn_def.outputThingDef);
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

                return DefDatabase<JobDef>.GetNamed("TakeThinggOutOfFermentingVat");
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced)
        {
            Building_FermentingVat building_FermentingVat = t as Building_FermentingVat;

            WorkGiverDynamicDef dyn_def = def as WorkGiverDynamicDef;
            if (dyn_def != null && dyn_def.outputThingDef != null)
            {
                if (building_FermentingVat.def != dyn_def.outputThingDef)
                    return false;
            }

            return building_FermentingVat != null && building_FermentingVat.Fermented && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced)
        {
            return new Job(JobDefToUse, t);
        }
    }
}
