using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WorldGenRules
{
    /// <summary>
    /// Scales world camera rotation to the size of the planet's tiles.
    /// A smaller planet keeps the same 3D sphere but its hex tiles get bigger, so vanilla rotation speeds feel very
    /// slow: the camera crosses far fewer tiles per second and a mouse drag no longer keeps the surface under the
    /// cursor.
    /// PlanetLayer.Subdivide inserts a centroid into every triangle and re-triangulates, which triples the triangle
    /// (and tile) count per pass, so each subdivision step shrinks the tile edge by sqrt(3), not by 2.
    /// </summary>
    [StaticConstructorOnStartup]
    static class CameraSpeed
    {
        // Vanilla subdivision count of the surface layer, captured before the world-gen slider can change the def.
        static readonly int defaultSurfaceSubdivisions;

        static readonly AccessTools.FieldRef<PlanetLayer, int> subdivisionsRef =
            AccessTools.FieldRefAccess<PlanetLayer, int>("subdivisions");

        static CameraSpeed()
        {
            defaultSurfaceSubdivisions = PlanetLayerSettingsDefOf.Surface?.settings?.subdivisions ?? WorldGrid.SubdivisionsCount;
        }

        /// <summary>
        /// Ratio between the current tile size and the vanilla tile size for the layer the camera is looking at.
        /// 1 for vanilla-sized surfaces and for every non-surface layer (e.g. Odyssey orbit), which the mod doesn't touch.
        /// </summary>
        public static float TileSizeRatio()
        {
            PlanetLayer layer = Find.WorldSelector?.SelectedLayer ?? Find.WorldGrid?.Surface;
            if (layer == null || !layer.IsRootSurface)
            {
                return 1f;
            }
            int subdivisions = subdivisionsRef(layer);
            if (subdivisions == defaultSurfaceSubdivisions)
            {
                return 1f;
            }
            // Tile count scales with 3^subdivisions, tile edge with sqrt(3)^subdivisions.
            return Mathf.Pow(3f, (defaultSurfaceSubdivisions - subdivisions) / 2f);
        }

        /// <summary>
        /// Mouse drag: vanilla converts dragged pixels into degrees assuming vanilla-sized tiles.
        /// Shrinking the reported UI tile size by the ratio makes the surface follow the cursor again.
        /// </summary>
        public static float DragUITileSize(float uiTileSize)
        {
            return uiTileSize / TileSizeRatio();
        }

        /// <summary>
        /// Keyboard and screen-edge dolly: keep "tiles per second" at close zoom, where the player navigates tile by
        /// tile, and fade back to vanilla degrees per second when zoomed all the way out, where the whole planet is
        /// visible and faster spinning would only be a blur.
        /// </summary>
        public static float DollySpeedFactor(float altitudePercent)
        {
            float ratio = TileSizeRatio();
            if (ratio == 1f)
            {
                return 1f;
            }
            return Mathf.Pow(ratio, 1f - Mathf.Clamp01(altitudePercent));
        }

        [HarmonyPatch(typeof(WorldCameraDriver), "CalculateCurInputDollyVect")]
        static class WorldCameraDriver_CalculateCurInputDollyVect_Patch
        {
            static void Postfix(WorldCameraDriver __instance, ref Vector2 __result)
            {
                if (__result != Vector2.zero)
                {
                    __result *= DollySpeedFactor(__instance.AltitudePercent);
                }
            }
        }

        [HarmonyPatch(typeof(WorldCameraDriver), nameof(WorldCameraDriver.WorldCameraDriverOnGUI))]
        static class WorldCameraDriver_WorldCameraDriverOnGUI_Patch
        {
            // desiredRotationRaw += delta / GenWorldUI.CurUITileSize() * 0.273f * Prefs.MapDragSensitivity;
            // Wrap the CurUITileSize() call so that the division uses a tile size scaled back to vanilla.
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var curUITileSize = AccessTools.Method(typeof(GenWorldUI), nameof(GenWorldUI.CurUITileSize));
                bool patched = false;
                foreach (CodeInstruction code in instructions)
                {
                    yield return code;
                    if (code.Calls(curUITileSize))
                    {
                        yield return CodeInstruction.Call(typeof(CameraSpeed), nameof(DragUITileSize));
                        patched = true;
                    }
                }
                if (!patched)
                {
                    Log.Error("[My Little Planet] Could not find the GenWorldUI.CurUITileSize call in WorldCameraDriver.WorldCameraDriverOnGUI; mouse drag speed will not be scaled to planet size.");
                }
            }
        }
    }
}
