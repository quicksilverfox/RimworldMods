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

        //public new bool IsColonyMech => base.Faction == Faction.OfPlayer && MentalStateDef == null;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (IsFormerHuman())
                return; // former humans have their own logic, ignore them

            if (skills == null)
            {
                // Can avoid this by making them using mech code, but it may require way more work so this hack would do
                skills = new Pawn_SkillTracker(this);
                foreach (SkillRecord skill in skills.skills) // to make skills neutral for price factor
                {
                    skill.Level = 6;
                }
            }

            if (story == null)
            {
                // FIXME: is this still necessary?
                story = new Pawn_StoryTracker(this) // necessary for job giver to work properly, but adds a bunch of problems since only humanlikes are supposed to have it
                {
                    bodyType = BodyTypeDefOf.Thin,
                    //crownType = CrownType.Average,
                    //childhood = xxx,
                    //adulthood = xxx
                };
            }

            if (workSettings == null) // only used for WorkGiversInOrderNormal / WorkGiversInOrderEmergency
            {
                workSettings = new Pawn_WorkSettings(this);
                workSettings.EnableAndInitialize();

                // both genders can do both cleaning and hauling, but males prefer hauling and females prefer cleaning so they divide jobs and don't neglect one or another too much
                if (gender == Gender.Female)
                    workSettings.SetPriority(WorkTypeDefOf.Hauling, 4);
            }

            GetDisabledWorkTypes(); // init stuff
            workSettings.Notify_DisabledWorkTypesChanged();
        }

        /**
         * Not actually changed
         */
        public new bool WorkTagIsDisabled(WorkTags w)
        {
            return (CombinedDisabledWorkTags & w) != 0;
        }

        /**
         * Not actually changed
         */
        public new bool WorkTypeIsDisabled(WorkTypeDef w)
        {
            return GetDisabledWorkTypes().Contains(w);
        }

        private List<WorkTypeDef> cachedDisabledWorkTypes;
        private List<WorkTypeDef> cachedDisabledWorkTypesPermanent;
        /**
         * Stripped to bare bones. Uses mechanoid tags for available job tags.
         */
        public new List<WorkTypeDef> GetDisabledWorkTypes(bool permanentOnly = false)
        {
            if (IsFormerHuman())
                    return base.GetDisabledWorkTypes(permanentOnly);

            if (permanentOnly)
            {
                if (cachedDisabledWorkTypesPermanent == null)
                {
                    cachedDisabledWorkTypesPermanent = new List<WorkTypeDef>();
                }

                FillList(cachedDisabledWorkTypesPermanent);
                return cachedDisabledWorkTypesPermanent;
            }

            if (cachedDisabledWorkTypes == null)
            {
                cachedDisabledWorkTypes = new List<WorkTypeDef>();
            }

            FillList(cachedDisabledWorkTypes);
            return cachedDisabledWorkTypes;
            void FillList(List<WorkTypeDef> list)
            {
                List<WorkTypeDef> allDefsListForReading = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                for (int j = 0; j < allDefsListForReading.Count; j++)
                {
                    if (!RaceProps.mechEnabledWorkTypes.Contains(allDefsListForReading[j]) && !list.Contains(allDefsListForReading[j]))
                    {
                        list.Add(allDefsListForReading[j]);
                    }
                }
            }
        }

        /**
         * For Pawnmorpher compatibility
         */
        public bool IsFormerHuman()
        {
            return story != null && (story.Childhood != null || story.Adulthood != null);
        }
    }
}
