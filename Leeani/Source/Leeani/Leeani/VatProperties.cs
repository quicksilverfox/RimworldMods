using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Leeani
{
    public class VatProperties : Editable
    {
        public ThingDef inputThingDef;
        public ThingDef outputThingDef;
        public int maxCapacity = 25;
        public float fermentationModifier = 1.0f;
        public int inputToOutputRatio = 1;

        //Translations
        public string containsInputTranslation = "ContainsBerries";
        public string containsOutputTranslation = "ContainsCider";
        public string fermentedTranslation = "Fermented";
        public string fermentationProgressTranslation = "FermentationProgress";
        public string fermentationNonIdealTranslation = "FermentationBarrelOutOfIdealTemperature";
    }
}
