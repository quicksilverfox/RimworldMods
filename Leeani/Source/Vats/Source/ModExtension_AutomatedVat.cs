using System;
using System.Collections.Generic;
using Verse;

namespace AutomatedVat
{
	// Token: 0x02000003 RID: 3
	public class ModExtension_AutomatedVat : DefModExtension
	{
		// Token: 0x04000003 RID: 3
		public int workAmount = -1;

		// Token: 0x04000004 RID: 4
		public List<ThingDefCountClass> ingredients = new List<ThingDefCountClass>();

		// Token: 0x04000005 RID: 5
		public List<ThingDefCountClass> products = new List<ThingDefCountClass>();

		// Token: 0x04000006 RID: 6
		public int tickRateDivisor = 35;

		// Token: 0x04000007 RID: 7
		public float workSpeedMultiplier = 1f;

		// Token: 0x04000008 RID: 8
		public TemperatureManagementProperties temperatureManagement = new TemperatureManagementProperties();

		// Token: 0x04000009 RID: 9
		public List<DefTranslationOverride> overrides = new List<DefTranslationOverride>();
	}
}
