using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace AnimalsLogic
{
    public class Alert_AnimalNeedsRescuing : Alert_Critical
    {

        private IEnumerable<Pawn> AnimalsNeedingRescue
        {
            get
            {
                foreach (Pawn p in PawnsFinder.AllMaps_Spawned.Where(p => p.PlayerColonyAnimal()))
                    if (Alert_ColonistNeedsRescuing.NeedsRescue(p))
                        yield return p;
            }
        }

        public override string GetLabel()
        {
            if (AnimalsNeedingRescue.Count() <= 1)
                return "AnimalNeedsRescue".Translate();
            return "AnimalsNeedRescue".Translate();
        }

        public override string GetExplanation()
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (Pawn pawn in AnimalAlertsUtility.SortedAnimalList(AnimalsNeedingRescue))
            {
                stringBuilder.AppendLine($"    {pawn.LabelShort} {((pawn.Name != null && !pawn.Name.Numerical) ? "(" + pawn.KindLabel + ")" : "")} {(pawn.HasBondRelation() ? "BondBrackets".Translate() : "")}");
            }
            return string.Format("AnimalsNeedRescueDesc".Translate(), stringBuilder.ToString());
        }

        public override AlertReport GetReport()
        {
            return (Settings.medical_alerts) ? AlertReport.CulpritsAre(AnimalsNeedingRescue) : false;
        }

    }
}
