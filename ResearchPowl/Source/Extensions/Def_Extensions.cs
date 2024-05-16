// Def_Extensions.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ResearchPowl
{
    public static class Def_Extensions
    {
        static readonly Dictionary<int, Texture2D> _cachedDefIcons = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, Color> _cachedIconColors = new Dictionary<int, Color>();

        public static Color IconColor( this Def def )
        {
            // garbage in, garbage out
            if ( def == null ) return Color.cyan;
            var index = def.index;

            // check cache
            if (_cachedIconColors.TryGetValue(index, out Color color)) return color;

            // get product color for recipes
            else if (def is RecipeDef rdef && !rdef.products.NullOrEmpty())
            {    
                color = rdef.products[0].thingDef.IconColor();
                _cachedIconColors.Add(index, color);
            }

            // get color from final lifestage for pawns
            else if (def is PawnKindDef pdef)
            {
                color = pdef.lifeStages[pdef.lifeStages.Count - 1].bodyGraphicData.color;
                _cachedIconColors.Add(index, color);
            }

            else if (def is not BuildableDef)
            {
                // if we reach this point, def.IconTexture() would return null. Just store and return white to make sure we don't get weird errors down the line.
                color = Assets.colorWhite;
                _cachedIconColors.Add(index, color);
            }

            // built def != listed def
            else if (def is ThingDef tdef && tdef.entityDefToBuild != null)
            {
                color = tdef.entityDefToBuild.IconColor();
                _cachedIconColors.Add(index, color);
            }

            // graphic.color set?
            else if (def is BuildableDef bdef)
            {
                color = bdef.graphic.color;
                _cachedIconColors.Add(index, color);
            }

            // stuff used?
            else if (def is ThingDef tDefStuff && tDefStuff.MadeFromStuff)
            {
                color = GenStuff.DefaultStuffFor(tDefStuff).stuffProps.color;
                _cachedIconColors.Add(index, color);
            }

            // all else failed.
            else
            {
                color = Assets.colorWhite;
                _cachedIconColors.Add(index, color);
            }

            return color;
        }

        public static Texture2D IconTexture(this Def def)
        {
            // garbage in, garbage out
            if (def == null ) return null;

            var index = def.index;

            // check cache
            if (_cachedDefIcons.TryGetValue(index, out Texture2D texture2D)) return texture2D;

            // recipes will be passed icon of first product, if defined.
            else if (def is RecipeDef recipeDef && !recipeDef.products.NullOrEmpty())
            {
                try
                {
                    texture2D = recipeDef.products[0].thingDef.IconTexture();
                }
                catch { texture2D = null; }
                _cachedDefIcons.Add(index, texture2D);
            }
            else if (def is PawnKindDef pawnKindDef)
                try
                {
                    texture2D = pawnKindDef.lifeStages[pawnKindDef.lifeStages.Count - 1].bodyGraphicData.Graphic.MatSouth.mainTexture as Texture2D;
                    _cachedDefIcons.Add(index, texture2D);
                }
                catch { texture2D = null; }

            else if (def is BuildableDef buildableDef)
            {
                // if def built != def listed.
                if (def is ThingDef thingDef && thingDef.entityDefToBuild != null )
                {
                    texture2D = thingDef.entityDefToBuild.IconTexture();
                }
                else texture2D = buildableDef.uiIcon;

                _cachedDefIcons.Add(index, texture2D);
            }
            else 
            {
                texture2D = null;
                _cachedDefIcons.Add(index, null);
            }
            
            return texture2D;
        }
    }
}