using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutomatedVat
{
	// Token: 0x02000002 RID: 2
	public class Building_AutomatedVat : Building
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public bool PowerOn
		{
			get
			{
				CompPowerTrader comp = base.GetComp<CompPowerTrader>();
				return comp == null || comp.PowerOn;
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000002 RID: 2 RVA: 0x00002074 File Offset: 0x00000274
		public bool Ruined
		{
			get
			{
				CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
				return comp != null && comp.Ruined;
			}
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000003 RID: 3 RVA: 0x00002098 File Offset: 0x00000298
		public ModExtension_AutomatedVat Extension
		{
			get
			{
				return this.def.GetModExtension<ModExtension_AutomatedVat>();
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000004 RID: 4 RVA: 0x000020B5 File Offset: 0x000002B5
		public IEnumerable<IntVec3> AdjacentCells
		{
			get
			{
				return GenAdj.CellsAdjacent8Way(this);
			}
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000020C0 File Offset: 0x000002C0
		public override void PostMake()
		{
			base.PostMake();
			bool flag = this.Extension == null;
			if (flag)
			{
				Log.Error("Automated vat building needs <modExtensions> <li Class=\"FFModExtension_AutomatedVat\"> to define input and output, and will cause errors without. Destroying.");
				this.Destroy(DestroyMode.KillFinalize);
			}
			else
			{
				this.InitializeNewRecipe();
			}
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000020FF File Offset: 0x000002FF
		public virtual void InitializeNewRecipe()
		{
			this.workLeft = this.Extension.workAmount;
			this.localRecord = Building_AutomatedVat._ThingCountClass.ListToSaveable(this.Extension.ingredients);
		}

		// Token: 0x06000007 RID: 7 RVA: 0x0000212C File Offset: 0x0000032C
		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(base.GetInspectString());
			CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
			bool flag = comp != null;
			if (flag)
			{
				float num = (float)typeof(CompTemperatureRuinable).GetField("ruinedPercent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(comp);
				bool flag2 = num > 0f;
				if (flag2)
				{
					stringBuilder.AppendLine("(" + GenText.ToStringPercent(num) + ") " + (this.Extension.temperatureManagement.hasTemperatureManagement ? "AVInspect_BadTempManaged".AdvancedTranslate(this.Extension) : "AVInspect_BadTempUnmanaged".AdvancedTranslate(this.Extension)));
				}
			}
			stringBuilder.AppendLine(this.GetStringIngredients());
			bool flag3 = !GenCollection.Any<Building_AutomatedVat._ThingCountClass>(this.localRecord, (Building_AutomatedVat._ThingCountClass t) => t.count > 0);
			if (flag3)
			{
				stringBuilder.AppendLine(this.GetStringWorking());
			}
			return GenText.TrimEndNewlines(stringBuilder.ToString());
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002250 File Offset: 0x00000450
		public virtual string GetStringIngredients()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("AVInspect_IngredientsLeft".AdvancedTranslate(this.Extension));
			foreach (Building_AutomatedVat._ThingCountClass thingCountClass in this.localRecord)
			{
				stringBuilder.Append(thingCountClass.ToString());
			}
			return stringBuilder.ToString();
		}

		// Token: 0x06000009 RID: 9 RVA: 0x000022DC File Offset: 0x000004DC
		public virtual string GetStringWorking()
		{
			return "AVInspect_WorkLeft".AdvancedTranslate(this.Extension, GenText.ToStringWorkAmount((float)this.workLeft));
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002313 File Offset: 0x00000513
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look<Building_AutomatedVat._ThingCountClass>(ref this.localRecord, "localRecord", LookMode.Deep, new object[0]);
			Scribe_Values.Look<int>(ref this.workLeft, "workLeft", 0, false);
		}

		// Token: 0x0600000B RID: 11 RVA: 0x00002348 File Offset: 0x00000548
		public override void DrawExtraSelectionOverlays()
		{
			base.DrawExtraSelectionOverlays();
			GenDraw.DrawFieldEdges(this.AdjacentCells.ToList<IntVec3>());
		}

		// Token: 0x0600000C RID: 12 RVA: 0x00002364 File Offset: 0x00000564
		public override void Tick()
		{
			base.Tick();
			bool ruined = this.Ruined;
			if (ruined)
			{
				this.InitializeNewRecipe();
				bool flag = base.AmbientTemperature > base.GetComp<CompTemperatureRuinable>().Props.maxSafeTemperature || base.AmbientTemperature < base.GetComp<CompTemperatureRuinable>().Props.minSafeTemperature;
				if (flag)
				{
					this.ManageTemperature();
				}
			}
			else
			{
				bool flag2 = this.PowerOn && Find.TickManager.TicksGame % this.Extension.tickRateDivisor == 0;
				if (flag2)
				{
					bool hasTemperatureManagement = this.Extension.temperatureManagement.hasTemperatureManagement;
					if (hasTemperatureManagement)
					{
						this.ManageTemperature();
					}
					else
					{
						bool flag3 = base.GetComp<CompPowerTrader>() != null;
						if (flag3)
						{
							base.GetComp<CompPowerTrader>().powerOutputInt = -this.def.GetCompProperties<CompProperties_Power>().basePowerConsumption;
						}
					}
					bool flag4 = GenCollection.Any<Building_AutomatedVat._ThingCountClass>(this.localRecord, (Building_AutomatedVat._ThingCountClass x) => x.count > 0);
					if (flag4)
					{
						this.AcceptIngredientsForNextRecipe();
					}
					else
					{
						bool flag5 = this.workLeft > 0;
						if (flag5)
						{
							this.DoWork(Mathf.RoundToInt(this.Extension.workSpeedMultiplier * (float)this.Extension.tickRateDivisor));
						}
						else
						{
							this.MakeProductsAndStartNewRecipe();
						}
					}
				}
			}
		}

		// Token: 0x0600000D RID: 13 RVA: 0x000024C4 File Offset: 0x000006C4
		public void ManageTemperature()
		{
			CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
			CompPowerTrader comp2 = base.GetComp<CompPowerTrader>();
			FieldInfo field = typeof(CompTemperatureRuinable).GetField("ruinedPercent", BindingFlags.Instance | BindingFlags.NonPublic);
			bool flag = comp != null && comp2 != null && (float)field.GetValue(comp) > 0f;
			if (flag)
			{
				comp2.powerOutputInt = -this.def.GetCompProperties<CompProperties_Power>().basePowerConsumption - this.Extension.temperatureManagement.powerConsumptionExtra;
				field.SetValue(comp, (float)field.GetValue(comp) - comp.Props.progressPerDegreePerTick * this.Extension.temperatureManagement.temperatureManagementStrength * (float)this.Extension.tickRateDivisor);
			}
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00002584 File Offset: 0x00000784
		public virtual void MakeProductsAndStartNewRecipe()
		{
			bool flag = GenCollection.Any<ThingDefCountClass>(this.Extension.products);
			if (flag)
			{
				this.MakeProducts();
			}
			this.InitializeNewRecipe();
		}

		// Token: 0x0600000F RID: 15 RVA: 0x000025B4 File Offset: 0x000007B4
		public virtual void DoWork(int amount)
		{
			this.workLeft -= amount;
			bool flag = this.workLeft <= 0;
			if (flag)
			{
				this.workLeft = 0;
			}
		}

		// Token: 0x06000010 RID: 16 RVA: 0x000025EC File Offset: 0x000007EC
		public virtual void AcceptIngredientsForNextRecipe()
		{
			List<ThingDefCountClass> ingredients = this.Extension.ingredients;
			List<IntVec3> source = this.AdjacentCells.ToList<IntVec3>();
			IEnumerable<Thing> enumerable = source.SelectMany((IntVec3 c) => GridsUtility.GetThingList(c, base.Map));
			foreach (Thing thing in enumerable)
			{
				foreach (Building_AutomatedVat._ThingCountClass thingCountClass in from x in this.localRecord
				where x.count > 0
				select x)
				{
					bool flag = thing.def == thingCountClass.thingDef;
					if (flag)
					{
						bool flag2 = thingCountClass.count >= thing.stackCount;
						if (flag2)
						{
							thingCountClass.count -= thing.stackCount;
							thing.Destroy(0);
							return;
						}
						thing.stackCount -= thingCountClass.count;
						thingCountClass.count = 0;
						return;
					}
				}
			}
		}

		// Token: 0x06000011 RID: 17 RVA: 0x00002740 File Offset: 0x00000940
		public virtual void MakeProducts()
		{
			foreach (ThingDefCountClass thingCountClass in this.Extension.products)
			{
				Thing thing = ThingMaker.MakeThing(thingCountClass.thingDef, null);
				thing.stackCount = thingCountClass.count;
				CompIngredients compIngredients = ThingCompUtility.TryGetComp<CompIngredients>(thing);
				bool flag = compIngredients != null;
				if (flag)
				{
					compIngredients.ingredients.AddRange(from ThingDefCountClass i in this.Extension.ingredients
					select i.thingDef);
				}
				GenPlace.TryPlaceThing(thing, this.InteractionCell, base.Map, ThingPlaceMode.Near);
			}
		}

		// Token: 0x04000001 RID: 1
		public List<Building_AutomatedVat._ThingCountClass> localRecord;

		// Token: 0x04000002 RID: 2
		public int workLeft;

		// Token: 0x02000007 RID: 7
		public class _ThingCountClass : IExposable
		{
			// Token: 0x06000018 RID: 24 RVA: 0x00002917 File Offset: 0x00000B17
			public _ThingCountClass()
			{
			}

			// Token: 0x06000019 RID: 25 RVA: 0x00002921 File Offset: 0x00000B21
			public _ThingCountClass(ThingDef thingDef, int count)
			{
				this.thingDef = thingDef;
				this.count = count;
			}

			// Token: 0x0600001A RID: 26 RVA: 0x0000293C File Offset: 0x00000B3C
			public override string ToString()
			{
				return string.Concat(new object[]
				{
					"(",
					this.count,
					"x ",
					(this.thingDef == null) ? "null" : this.thingDef.defName,
					")"
				});
			}

			// Token: 0x0600001B RID: 27 RVA: 0x0000299C File Offset: 0x00000B9C
			public override int GetHashCode()
			{
				return (int)this.thingDef.shortHash + this.count << 16;
			}

			// Token: 0x0600001C RID: 28 RVA: 0x000029C3 File Offset: 0x00000BC3
			public void ExposeData()
			{
				Scribe_Defs.Look<ThingDef>(ref this.thingDef, "thingDef");
				Scribe_Values.Look<int>(ref this.count, "count", 0, false);
			}

			// Token: 0x0600001D RID: 29 RVA: 0x000029EC File Offset: 0x00000BEC
			public static implicit operator Building_AutomatedVat._ThingCountClass(ThingDefCountClass original)
			{
				return new Building_AutomatedVat._ThingCountClass(original.thingDef, original.count);
			}

			// Token: 0x0600001E RID: 30 RVA: 0x00002A10 File Offset: 0x00000C10
			public static List<Building_AutomatedVat._ThingCountClass> ListToSaveable(List<ThingDefCountClass> original)
			{
				List<Building_AutomatedVat._ThingCountClass> result = new List<Building_AutomatedVat._ThingCountClass>();
				original.ForEach(delegate(ThingDefCountClass t)
				{
					result.Add(t);
				});
				return result;
			}

			// Token: 0x0400000F RID: 15
			public ThingDef thingDef;

			// Token: 0x04000010 RID: 16
			public int count;
		}
	}
}
