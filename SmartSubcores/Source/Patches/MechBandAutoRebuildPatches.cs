using HarmonyLib;
using RimWorld;
using Verse;

namespace SubcoreAutomation.Patches
{
	/// <summary>
	/// Auto-rebuild mechband antenna / dish after they self-destruct on use.
	/// Vanilla CompUseEffect_DestroySelf calls parent.Destroy() with DestroyMode.Vanish,
	/// which skips ThingUtility.CheckAutoRebuildOnDestroyed (it requires KillFinalize).
	/// We replicate the rebuild check around the destroy call so the structures behave
	/// like spike traps under the global auto-rebuild toggle.
	/// </summary>
	[HarmonyPatch(typeof(CompUseEffect_DestroySelf), "DoDestroy")]
	public static class Patch_MechBand_AutoRebuild
	{
		public static bool Prepare() => ModsConfig.BiotechActive;

		public struct CapturedState
		{
			public bool valid;
			public ThingDef def;
			public IntVec3 position;
			public Rot4 rotation;
			public Map map;
			public Faction faction;
			public ThingDef stuff;
			public Precept_ThingStyle styleSourcePrecept;
			public ThingStyleDef styleDef;
		}

		public static void Prefix(CompUseEffect_DestroySelf __instance, out CapturedState __state)
		{
			__state = default;

			Thing parent = __instance?.parent;
			if (parent == null || !parent.Spawned)
				return;

			if (!IsTargetDef(parent.def))
				return;

			__state = new CapturedState
			{
				valid = true,
				def = parent.def,
				position = parent.Position,
				rotation = parent.Rotation,
				map = parent.Map,
				faction = parent.Faction,
				stuff = parent.Stuff,
				styleSourcePrecept = parent.StyleSourcePrecept as Precept_ThingStyle,
				styleDef = parent.StyleDef
			};
		}

		public static void Postfix(CapturedState __state)
		{
			if (!__state.valid)
				return;

			if (Find.PlaySettings == null || !Find.PlaySettings.autoRebuild)
				return;
			if (__state.faction != Faction.OfPlayer)
				return;
			if (__state.def?.blueprintDef == null)
				return;
			if (!__state.def.IsResearchFinished)
				return;
			if (__state.map?.areaManager?.Home == null || !__state.map.areaManager.Home[__state.position])
				return;
			if (!GenConstruct.CanPlaceBlueprintAt(__state.def, __state.position, __state.rotation, __state.map, godMode: false, null, null, __state.stuff).Accepted)
				return;

			GenConstruct.PlaceBlueprintForBuild(
				__state.def,
				__state.position,
				__state.map,
				__state.rotation,
				Faction.OfPlayer,
				__state.stuff,
				__state.styleSourcePrecept,
				__state.styleDef);
		}

		private static bool IsTargetDef(ThingDef def)
		{
			if (def == null) return false;
			// Vanilla mechband antenna defName is BurnoutMechlinkBooster (the building, despite the comp name).
			return def.defName == "BurnoutMechlinkBooster" || def.defName == "MechbandDish";
		}
	}
}
