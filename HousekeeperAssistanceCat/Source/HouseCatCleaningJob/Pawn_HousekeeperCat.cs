using Verse;
using RimWorld;
using System.Collections.Generic;

namespace HousekeeperCat
{
    /*
     * Housekeeper cats are some kind of intermediate between animal and humanlike. They have full-fledged cleaning and hauling work givers instead of stubs other animals have. They would be able to do all relevant jobs, like refueling!
     * 
     * But they are still not fully sapient, so you need to teach them what they have to do and keep them updated on it, hence necessary training.
     * 
     * TODO: maybe handle disabled work with a backstory?
     */
    public class Pawn_HousekeeperCat : Pawn
    {
        private static WorkTypeDef Cleaning, Hauling, BasicWorker;
        private static TrainableDef Obedience, Release, Haul;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void TickRare() // not the best place for it tbh, but makes thigns much easier
        {
            base.TickRare();
            UpdateWork();
        }

        /**
         * Checks that only allowed works are enabled
         */
        public void UpdateWork()
        {
            if (IsFormerHuman())
                return;

            // just to be sure
            InitWork();

            // wild or other factions
            if (training == null || Faction == null || !Faction.IsPlayer)
                return;

            // stuff like being a patient is handled by basic animal AI, no need to enable it as a work - they only work when animal AI gives no other job
            workSettings.SetPriority(BasicWorker, training.HasLearned(Obedience) ? 3 : 0);

            // both genders can do both cleaning and hauling, but males prefer hauling and females prefer cleaning
            workSettings.SetPriority(Cleaning, training.HasLearned(Obedience) ? (gender == Gender.Male ? 4 : 3) : 0);
            workSettings.SetPriority(Hauling, training.HasLearned(Haul) ? (gender == Gender.Male ? 3 : 4) : 0);
        }

        /**
         * Upon changing faction work settings are reset and need reassigning
         */
        public override void SetFaction(Faction newFaction, Pawn recruiter = null)
        {
            base.SetFaction(newFaction, recruiter);

            if (IsFormerHuman())
                return;

            InitWork();
            workSettings.DisableAll();
            UpdateWork();
        }

        private void InitWork()
        {
            // Caching stuff
            if (Obedience == null)
                Obedience = DefDatabase<TrainableDef>.GetNamed("Obedience");
            if (Release == null)
                Release = DefDatabase<TrainableDef>.GetNamed("Release");
            if (Haul == null)
                Haul = DefDatabase<TrainableDef>.GetNamed("Haul");

            if (BasicWorker == null)
                BasicWorker = DefDatabase<WorkTypeDef>.GetNamed("BasicWorker");
            if (Cleaning == null)
                Cleaning = DefDatabase<WorkTypeDef>.GetNamed("Cleaning");
            if (Hauling == null)
                Hauling = DefDatabase<WorkTypeDef>.GetNamed("Hauling");

            if (IsFormerHuman())
                return;

            if (skills == null)
            {
                skills = new Pawn_SkillTracker(this);
                foreach (SkillRecord skill in skills.skills) // to make skills neutral for price factor
                {
                    skill.Level = 6;
                }
            }

            if (story == null)
            {
                story = new Pawn_StoryTracker(this) // necessary for job giver to work properly, but adds a bunch of problems since only humanlikes are supposed to have it
                {
                    bodyType = BodyTypeDefOf.Thin,
                    crownType = CrownType.Average,
                    //childhood = xxx,
                    //adulthood = xxx
                };
            }

            if (workSettings == null)
            {
                workSettings = new Pawn_WorkSettings(this);
                workSettings.EnableAndInitialize();
                workSettings.DisableAll();
            }
        }

        /**
         * For Pawnmorpher compatibility
         */
        public bool IsFormerHuman()
        {
            return story != null && (story.childhood != null || story.adulthood != null);
        }
    }
}
