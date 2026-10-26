using Verse;
using Verse.AI;

namespace AnimalsLogic.MentalBreaks
{
    /// <summary>
    /// Ties the hunting spree break to the mod settings: the toggle removes it from the pool of possible breaks,
    /// the frequency slider scales its weight against the game's other major breaks.
    /// </summary>
    public class MentalBreakWorker_HuntingSpree : MentalBreakWorker
    {
        public override bool BreakCanOccur(Pawn pawn)
        {
            return Settings.hunting_spree && base.BreakCanOccur(pawn);
        }

        public override float CommonalityFor(Pawn pawn, bool moodCaused = false)
        {
            return base.CommonalityFor(pawn, moodCaused) * Settings.hunting_spree_frequency;
        }
    }
}
