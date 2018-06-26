using System;
using Verse;

namespace AutomatedVat
{
	// Token: 0x02000005 RID: 5
	public static class DefTranslationOverrideUtility
	{
		// Token: 0x06000016 RID: 22 RVA: 0x0000289C File Offset: 0x00000A9C
		public static string AdvancedTranslate(this string original, ModExtension_AutomatedVat instance, params object[] args)
		{
			DefTranslationOverride defTranslationOverride = instance.overrides.Find((DefTranslationOverride s) => s.original == original);
			bool flag = defTranslationOverride != null;
			string result;
			if (flag)
			{
				result = Translator.Translate(defTranslationOverride.modified, args);
			}
			else
			{
				result = Translator.Translate(original, args);
			}
			return result;
		}
	}
}
