using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Leeani
{
    // RimWorld.TradeDeal
    public Tradeable ShiniTradeable
    {
        get
        {
            for (int i = 0; i < this.tradeables.Count; i++)
            {
                if (this.tradeables[i].ThingDef == ThingDefOf.GoldShini)
                {
                    return this.tradeables[i];
                }
            }
            return null;
        }
    }
}