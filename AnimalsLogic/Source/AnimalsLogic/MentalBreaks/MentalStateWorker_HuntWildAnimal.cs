using System.Linq;
using Verse;
using Verse.AI;

namespace AnimalsLogic.MentalBreaks
{
    /// <summary>
    /// Keeps the hunting spree from being picked when it could not do anything: the pawn must be able to
    /// fight and there must be a huntable wild animal on the map. Without this the state would start,
    /// send its letter and recover on the next tick.
    /// </summary>
    public class MentalStateWorker_HuntWildAnimal : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn))
                return false;

            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                return false;

            Map map = pawn.MapHeld;
            if (map == null)
                return false;

            return map.mapPawns.AllPawnsSpawned.Any(animal => MentalState_HuntWildAnimal.IsValidHuntTarget(pawn, animal));
        }
    }
}
