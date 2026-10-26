using System.Linq;
using Verse;
using Verse.AI;

namespace AnimalsLogic.MentalBreaks
{
    /// <summary>
    /// Keeps the hunting spree from being picked when it could not do anything: the pawn must be able to
    /// fight and there must be a wild animal on the map that is safe enough for the pawn's current weapon
    /// (see <see cref="MentalState_HuntWildAnimal.IsSafeEnoughTarget"/>). Without this the state would start,
    /// send its letter and recover on the next tick.
    /// </summary>
    public class MentalStateWorker_HuntWildAnimal : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            // also blocks the state when something other than the break def tries to start it directly
            if (!Settings.hunting_spree)
                return false;

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
