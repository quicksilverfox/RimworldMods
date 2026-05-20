using RimWorld;
using Verse;

namespace AnimalsLogic
{
    /// <summary>
    /// DefOf class for AnimalsLogic custom JobDefs.
    /// </summary>
    [DefOf]
    public static class ALJobDefOf
    {
        public static JobDef AL_PlayWithAnimal;
        
        static ALJobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ALJobDefOf));
        }
    }

    /// <summary>
    /// DefOf class for AnimalsLogic custom JoyKindDefs.
    /// </summary>
    [DefOf]
    public static class ALJoyKindDefOf
    {
        public static JoyKindDef AL_AnimalInteraction;
        
        static ALJoyKindDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ALJoyKindDefOf));
        }
    }

    /// <summary>
    /// DefOf class for AnimalsLogic custom MentalStateDefs.
    /// </summary>
    [DefOf]
    public static class ALMentalStateDefOf
    {
        public static MentalStateDef AL_HuntingWildAnimal;
        
        static ALMentalStateDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ALMentalStateDefOf));
        }
    }
}
