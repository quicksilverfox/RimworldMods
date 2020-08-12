using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Leeani
{
    [StaticConstructorOnStartup]
    public class Building_FermentingVat : Building
    {
        public int MaxCapacity = 25;

        public float FermentationModifier = 1.0f;

        private const float MinIdealTemperature = 7f;

        private int thingCount;

        private float progressInt;

        private Material barFilledCachedMat;

        private static readonly Vector2 Barsize = new Vector2(0.55f, 0.1f);

        private static readonly Color BarZeroProgressColor = new Color(0.8f, 0.8f, 0.8f);

        private static readonly Color BarFermentedColor = new Color(0.2f, 0.90f, 0.2f);

        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));

        public override void PostMake()
        {
            base.PostMake();

            SetupValues();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            SetupValues();
        }

        public void SetupValues()
        {
            ExtraThingDef extra_def = def as ExtraThingDef;
            if (extra_def != null && extra_def.vatProperties != null)
            {
                MaxCapacity = extra_def.vatProperties.maxCapacity;
                FermentationModifier = extra_def.vatProperties.fermentationModifier;
            }
        }

        public float Progress
        {
            get
            {
                return this.progressInt;
            }
            set
            {
                if (value == this.progressInt)
                {
                    return;
                }
                this.progressInt = value;
                this.barFilledCachedMat = null;
            }
        }

        private Material BarFilledMat
        {
            get
            {
                if (this.barFilledCachedMat == null)
                {
                    this.barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(Building_FermentingVat.BarZeroProgressColor, Building_FermentingVat.BarFermentedColor, this.Progress));
                }
                return this.barFilledCachedMat;
            }
        }

        private float Temperature
        {
            get
            {
                if (base.MapHeld == null)
                {
                    Log.ErrorOnce("Tried to get a fermenting barrel temperature but MapHeld is null.", 847163513);
                    return 7f;
                }
                return base.PositionHeld.GetTemperature(base.MapHeld);
            }
        }

        public int SpaceLeftForInput
        {
            get
            {
                if (this.Fermented)
                {
                    return 0;
                }
                return MaxCapacity - this.thingCount;
            }
        }

        private bool Empty
        {
            get
            {
                return this.thingCount <= 0;
            }
        }

        public bool Fermented
        {
            get
            {
                return !this.Empty && this.Progress >= 1f;
            }
        }

        private float CurrentTempProgressSpeedFactor
        {
            get
            {
                CompProperties_TemperatureRuinable compProperties = this.def.GetCompProperties<CompProperties_TemperatureRuinable>();
                float temperature = this.Temperature;
                if (temperature < compProperties.minSafeTemperature)
                {
                    return 0.1f;
                }
                if (temperature < 7f)
                {
                    return GenMath.LerpDouble(compProperties.minSafeTemperature, 7f, 0.1f, 1f, temperature);
                }
                return 1f;
            }
        }

        private float ProgressPerTickAtCurrentTemp
        {
            get
            {
                return (1.66666666E-06f * this.CurrentTempProgressSpeedFactor) * FermentationModifier;
            }
        }

        private int EstimatedTicksLeft
        {
            get
            {
                return Mathf.Max(Mathf.RoundToInt((1f - this.Progress) / this.ProgressPerTickAtCurrentTemp), 0);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.thingCount, "thingCount", 0, false);
            Scribe_Values.Look<float>(ref this.progressInt, "progress", 0f, false);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!this.Empty)
            {
                this.Progress = Mathf.Min(this.Progress + 250f * this.ProgressPerTickAtCurrentTemp, 1f);
            }
        }

        public void AddInput(int count)
        {
            if (this.Fermented)
            {
                Log.Warning("Tried to add <input> to a barrel full of <output>. Colonists should take the <output> first.");
                return;
            }
            int num = Mathf.Min(count, MaxCapacity - this.thingCount);
            if (num <= 0)
            {
                return;
            }
            this.Progress = GenMath.WeightedAverage(0f, (float)num, this.Progress, (float)this.thingCount);
            this.thingCount += num;
            base.GetComp<CompTemperatureRuinable>().Reset();
        }

        public void AddInput(Thing input)
        {
            CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
            if (comp.Ruined)
            {
                comp.Reset();
            }
            this.AddInput(input.stackCount);
            input.Destroy(DestroyMode.Vanish);
        }

        protected override void ReceiveCompSignal(string signal)
        {
            if (signal == "RuinedByTemperature")
            {
                this.Reset();
            }
        }

        private void Reset()
        {
            this.thingCount = 0;
            this.Progress = 0f;
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length != 0)
            {
                stringBuilder.AppendLine();
            }
            CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
            if (!this.Empty && !comp.Ruined)
            {
                if (this.Fermented)
                {
                    ExtraThingDef extra_def = def as ExtraThingDef;
                    if (extra_def != null && extra_def.vatProperties != null)
                        stringBuilder.AppendLine(extra_def.vatProperties.containsOutputTranslation.Translate(this.thingCount, MaxCapacity));
                    else
                        stringBuilder.AppendLine("ContainsBerries".Translate(this.thingCount, MaxCapacity));
                }
                else
                {
                    ExtraThingDef extra_def = def as ExtraThingDef;
                    if (extra_def != null && extra_def.vatProperties != null)
                        stringBuilder.AppendLine(extra_def.vatProperties.containsInputTranslation.Translate(this.thingCount, MaxCapacity));
                    else
                        stringBuilder.AppendLine("ContainsCider".Translate(this.thingCount, MaxCapacity));
                }
            }
            if (!this.Empty)
            {
                if (this.Fermented)
                {
                    ExtraThingDef extra_def = def as ExtraThingDef;
                    if (extra_def != null && extra_def.vatProperties != null)
                        stringBuilder.AppendLine(extra_def.vatProperties.fermentedTranslation.Translate());
                    else
                        stringBuilder.AppendLine("Fermented".Translate());
                }
                else
                {
                    ExtraThingDef extra_def = def as ExtraThingDef;
                    if (extra_def != null && extra_def.vatProperties != null)
                        stringBuilder.AppendLine(extra_def.vatProperties.fermentationProgressTranslation.Translate(this.Progress.ToStringPercent(), this.EstimatedTicksLeft.ToStringTicksToPeriod()));
                    else
                        stringBuilder.AppendLine("FermentationProgress".Translate(this.Progress.ToStringPercent(), this.EstimatedTicksLeft.ToStringTicksToPeriod()));
                    if (this.CurrentTempProgressSpeedFactor != 1f)
                    {
                        if (extra_def != null && extra_def.vatProperties != null)
                            stringBuilder.AppendLine(extra_def.vatProperties.fermentationNonIdealTranslation.Translate(this.CurrentTempProgressSpeedFactor.ToStringPercent()));
                        else
                            stringBuilder.AppendLine("FermentationBarrelOutOfIdealTemperature".Translate(this.CurrentTempProgressSpeedFactor.ToStringPercent()));
                    }
                }
            }
            stringBuilder.AppendLine("Temperature".Translate() + ": " + base.AmbientTemperature.ToStringTemperature("F0"));
            stringBuilder.AppendLine(string.Concat(new string[]
            {
                "IdealFermentingTemperature".Translate(),
                ": ",
                7f.ToStringTemperature("F0"),
                " ~ ",
                comp.Props.maxSafeTemperature.ToStringTemperature("F0")
            }));
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public Thing TakeOutThing()
        {
            if (!this.Fermented)
            {
                Log.Warning("Tried to get Defs but it's not yet fermented.");
                return null;
            }

            Thing thing = null;

            int stack_count_modifier = 1;

            ExtraThingDef extra_def = def as ExtraThingDef;
            if (extra_def != null && extra_def.vatProperties != null)
            {
                thing = ThingMaker.MakeThing(extra_def.vatProperties.outputThingDef, null);
                stack_count_modifier = extra_def.vatProperties.inputToOutputRatio;
            }
            thing.stackCount = this.thingCount / stack_count_modifier;
            this.Reset();
            return thing;
        }

        public override void Draw()
        {
            base.Draw();
            if (!this.Empty)
            {
                Vector3 drawPos = this.DrawPos;
                drawPos.y += 0.05f;
                drawPos.z += 0.25f;
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = drawPos,
                    size = Building_FermentingVat.Barsize,
                    fillPercent = (float)this.thingCount / (float)MaxCapacity,
                    filledMat = this.BarFilledMat,
                    unfilledMat = Building_FermentingVat.BarUnfilledMat,
                    margin = 0.1f,
                    rotation = Rot4.North
                });
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            List<Gizmo> gizmos = new List<Gizmo>(base.GetGizmos());

            if (DebugSettings.godMode)
            {
                {
                    Command_Action debug_action = new Command_Action();
                    debug_action.defaultLabel = "Set progress to 1";
                    debug_action.defaultDesc = "Finish fermenting.";
                    debug_action.action = delegate ()
                    {
                        progressInt = 1.0f;
                    };

                    gizmos.Add(debug_action);
                }
            }

            return gizmos;
        }
    }
}
