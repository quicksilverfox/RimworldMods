using System;
using System.Reflection;
using HarmonyLib;

namespace SyncGrowth.Patches
{
	/// <summary>
	/// Growth itself needs no per-mod patches: Plant.GrowthPerTick is non-virtual, so the growth patch
	/// already applies to every Plant subclass. Only the inspect string is virtual, and a subclass that
	/// overrides it without calling the base method would not show the sync adjustment, so those
	/// overrides are patched here. Types that are not loaded are skipped.
	/// </summary>
	class Compatibility
	{
		static readonly string[] PlantSubclasses =
		{
			// Orassans - plants that grow at negative temperatures
			"Orassans.FrostPlant",

			// Vanilla Plants Expanded
			"VanillaPlantsExpanded.Plant_ChecksRiver",
			"VanillaPlantsExpanded.Plant_HighTempAffected",
			"VanillaPlantsExpanded.Plant_RainAffected",
			"VanillaPlantsExpanded.Plant_StopsGrowingInToxins",
			"VanillaPlantsExpanded.Plant_WinterBlooming",
			"VanillaPlantsExpanded.Plant_WinterResistant",

			// Caveworld Flora Unleashed mushrooms
			"Caveworld_Flora_Unleashed.FruitingBody",

			// Biomes! Core and submods
			"BMT.BiomesPlant",

			// Pawnmorpher - Glowvine
			"Pawnmorph.Things.ExpandedPlant"
		};

		public static void Patch()
		{
			var postfix = new HarmonyMethod(typeof(Plant_GetInspectString_Patch), nameof(Plant_GetInspectString_Patch.Postfix));

			foreach (string typeName in PlantSubclasses)
			{
				Type type = AccessTools.TypeByName(typeName);
				if (type == null)
					continue;

				MethodInfo method = AccessTools.DeclaredMethod(type, "GetInspectString");
				if (method != null)
					SyncGrowth.harmony.Patch(method, postfix: postfix);
			}
		}
	}
}
