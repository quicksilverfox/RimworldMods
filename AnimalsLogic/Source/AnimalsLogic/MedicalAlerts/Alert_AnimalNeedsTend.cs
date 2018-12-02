using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace AnimalsLogic
{
    public class Alert_AnimalNeedsTend : Alert
    {

        public Alert_AnimalNeedsTend()
        {
            defaultLabel = "AnimalNeedsTreatment".Translate();
            defaultPriority = AlertPriority.High;
        }

        private IEnumerable<Pawn> NeedingAnimals
        {
            get
            {
                foreach (Pawn p in PawnsFinder.AllMaps_Spawned.Where(p => p.PlayerColonyAnimal()))
                    if (p.health.HasHediffsNeedingTendByPlayer(true))
                    {
                        Building_Bed curBed = p.CurrentBed();
                        if (curBed == null || !curBed.Medical)
                            if (!Alert_ColonistNeedsRescuing.NeedsRescue(p))
                                yield return p;
                    }
            }
        }

        public override string GetLabel()
        {
            if (NeedingAnimals.Count() <= 1)
                return "AnimalNeedsTreatment".Translate();
            return "AnimalsNeedTreatment".Translate();
        }

        public override string GetExplanation()
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (Pawn pawn in AnimalAlertsUtility.SortedAnimalList(NeedingAnimals))
            {
                stringBuilder.AppendLine($"    {pawn.LabelShort} {((pawn.Name != null && !pawn.Name.Numerical) ? "(" + pawn.KindLabel + ")" : "")} {(pawn.HasBondRelation() ? "BondBrackets".Translate() : "")}");
            }
            return string.Format("AnimalNeedsTreatmentDesc".Translate(), stringBuilder.ToString());
        }

        public override AlertReport GetReport()
        {
            return (Settings.medical_alerts) ? AlertReport.CulpritsAre(NeedingAnimals) : false;
        }

    }
}
