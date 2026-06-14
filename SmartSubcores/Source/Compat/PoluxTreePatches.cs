using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using SubcoreAutomation.Core;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Compat
{
	/// <summary>
	/// CompProperties for Polux tree wastepack consumption.
	/// </summary>
	public class CompProperties_PoluxWastepackConsumer : CompProperties
	{
		/// <summary>
		/// Radius around the tree to scan for wastepacks.
		/// </summary>
		public float consumeRadius = 7.9f;

		/// <summary>
		/// Ticks between consumption attempts.
		/// 2.5 days = 150000 ticks (slower than atomizer's 12 hours).
		/// </summary>
		public int consumeIntervalTicks = 150000;

		/// <summary>
		/// Maximum stack size for wastepacks bound to the tree.
		/// </summary>
		public int maxWastepackStack = 5;

		public CompProperties_PoluxWastepackConsumer()
		{
			compClass = typeof(CompPoluxWastepackConsumer);
		}
	}

	/// <summary>
	/// Component that allows Polux trees to consume nearby wastepacks.
	/// Inspired by Harbinger tree mechanics:
	/// - Wastepacks entering the radius are bound with root visual effects
	/// - Bound stacks are split to max size and distributed within radius
	/// - Excess spills outside the radius
	/// - Smallest stacks consumed first to free up space
	/// - Bound wastepacks cannot be merged (blocks hauler refilling)
	/// </summary>
	public class CompPoluxWastepackConsumer : ThingComp
	{
		private int ticksUntilConsume;

		// Track bound wastepacks and their root motes (like Harbinger tree)
		private Dictionary<Thing, Mote> boundWastepacks = new Dictionary<Thing, Mote>();
		private Dictionary<Thing, IntVec3> boundPositions = new Dictionary<Thing, IntVec3>(); // Track positions for move detection
		private List<Thing> pendingUnbind = new List<Thing>();
		private List<Thing> tempWastepackList = new List<Thing>(); // Reusable list to avoid allocations

		// Cached values
		private float cachedRadiusSq;
		private bool hasPollutedCellsCached;
		private int lastPollutionCheckTick = -1;

		// Cache for wastepack stockpile zone creation
		private static readonly CachedTexture CreateWastepackStockpileIcon =
			new CachedTexture("UI/Designators/ZoneCreate_Stockpile");

		public CompProperties_PoluxWastepackConsumer Props =>
			(CompProperties_PoluxWastepackConsumer)props;

		public bool HasBoundWastepacks => boundWastepacks.Count > 0;

		/// <summary>
		/// Pre-calculated radius squared for distance checks.
		/// </summary>
		public float RadiusSq => cachedRadiusSq;

		/// <summary>
		/// Check if a wastepack is bound to this tree.
		/// </summary>
		public bool IsBound(Thing wastepack) => boundWastepacks.ContainsKey(wastepack);

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (!respawningAfterLoad)
			{
				ticksUntilConsume = Props.consumeIntervalTicks;
			}
			// Cache radius squared for performance
			cachedRadiusSq = Props.consumeRadius * Props.consumeRadius;
			// Register with global tracker
			PoluxTreeTracker.Register(this);
			// Initial binding pass
			LongEventHandler.ExecuteWhenFinished(UpdateBindings);
		}

		public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
		{
			base.PostDeSpawn(map, mode);
			// Unregister and release all bound wastepacks (pass map since parent.Map is null)
			PoluxTreeTracker.Unregister(this, map);
			foreach (var mote in boundWastepacks.Values)
			{
				mote?.Destroy();
			}
			boundWastepacks.Clear();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref ticksUntilConsume, "ticksUntilConsume", Props.consumeIntervalTicks);

			// Save/load bound wastepacks (motes are recreated on load)
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var boundList = boundWastepacks.Keys.ToList();
				Scribe_Collections.Look(ref boundList, "boundWastepacks", LookMode.Reference);
			}
			else
			{
				List<Thing> boundList = null;
				Scribe_Collections.Look(ref boundList, "boundWastepacks", LookMode.Reference);
				if (boundList != null && Scribe.mode == LoadSaveMode.PostLoadInit)
				{
					boundWastepacks.Clear();
					foreach (var thing in boundList)
					{
						if (thing != null && !thing.Destroyed)
							boundWastepacks[thing] = null; // Mote recreated in UpdateBindings
					}
				}
			}
		}

		public override void CompTickLong()
		{
			if (!parent.Spawned)
				return;

			// Update bindings (runs every 2000 ticks / ~33 seconds - plant tick rate)
			UpdateBindings();

			// Check for wastepack consumption (2000 ticks per long tick)
			ticksUntilConsume -= GenTicks.TickLongInterval;
			if (ticksUntilConsume <= 0)
			{
				ticksUntilConsume = Props.consumeIntervalTicks;
				TryConsumeWastepack();
			}
		}

		/// <summary>
		/// Updates bindings: binds new wastepacks, unbinds removed ones, maintains motes.
		/// </summary>
		private void UpdateBindings()
		{
			if (!parent.Spawned)
				return;

			Map map = parent.Map;

			// Find wastepacks that should be unbound (destroyed, left radius, or moved)
			pendingUnbind.Clear();
			foreach (var kvp in boundWastepacks)
			{
				Thing wastepack = kvp.Key;
				if (wastepack == null || wastepack.Destroyed || !wastepack.Spawned)
				{
					pendingUnbind.Add(wastepack);
					continue;
				}

				float distSq = (wastepack.Position - parent.Position).LengthHorizontalSquared;
				if (distSq > cachedRadiusSq)
				{
					pendingUnbind.Add(wastepack);
					continue;
				}

				// Check if wastepack moved (mote would be at wrong position)
				if (boundPositions.TryGetValue(wastepack, out IntVec3 boundPos) && boundPos != wastepack.Position)
				{
					pendingUnbind.Add(wastepack);
					// Will be re-bound at new position below
				}
			}

			// Unbind
			foreach (var wastepack in pendingUnbind)
			{
				Unbind(wastepack);
			}

			// Find new wastepacks in radius (copy to reusable list since BindNewWastepack may spawn new stacks)
			tempWastepackList.Clear();
			tempWastepackList.AddRange(map.listerThings.ThingsOfDef(ThingDefOf.Wastepack));
			foreach (Thing thing in tempWastepackList)
			{
				if (!thing.Spawned || thing.Destroyed)
					continue;

				if (boundWastepacks.ContainsKey(thing))
					continue; // Already bound

				float distSq = (thing.Position - parent.Position).LengthHorizontalSquared;
				if (distSq > cachedRadiusSq)
					continue;

				// Skip wastepacks in powered storage buildings (like atomizers)
				var slotGroup = thing.GetSlotGroup();
				if (slotGroup?.parent is Building building)
				{
					var powerComp = building.GetComp<CompPowerTrader>();
					if (powerComp != null && powerComp.PowerOn)
						continue;
				}

				// Bind and process this new wastepack
				BindNewWastepack(thing);
			}
			// Note: Motes are created in Bind() and destroyed in Unbind()
			// Following vanilla HarbingerTree pattern - don't iterate through all bindings each tick
		}

		/// <summary>
		/// Binds a new wastepack: splits it, distributes within radius, spills excess.
		/// </summary>
		private void BindNewWastepack(Thing wastepack)
		{
			if (wastepack == null || wastepack.Destroyed || !wastepack.Spawned)
				return;

			Map map = parent.Map;
			int totalCount = wastepack.stackCount;

			// If stack is within limit, just bind it
			if (totalCount <= Props.maxWastepackStack)
			{
				Bind(wastepack);
				return;
			}

			// Split into multiple stacks
			List<Thing> newStacks = new List<Thing>();
			IntVec3 originalPos = wastepack.Position;

			// Reduce original stack to max size
			int excess = totalCount - Props.maxWastepackStack;
			wastepack.stackCount = Props.maxWastepackStack;
			Bind(wastepack);

			// Create new stacks from excess
			while (excess > 0)
			{
				int stackSize = System.Math.Min(excess, Props.maxWastepackStack);
				excess -= stackSize;

				Thing newStack = ThingMaker.MakeThing(ThingDefOf.Wastepack);
				newStack.stackCount = stackSize;
				newStacks.Add(newStack);
			}

			// Try to place new stacks within radius, preferring stockpile cells
			int numCells = GenRadial.NumCellsInRadius(Props.consumeRadius);

			foreach (Thing stack in newStacks)
			{
				bool placed = false;

				// First pass: try to find empty cell in a wastepack stockpile within radius
				for (int i = 0; i < numCells && !placed; i++)
				{
					IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
					if (!cell.InBounds(map) || !cell.Standable(map))
						continue;
					if (cell.GetFirstItem(map) != null)
						continue;

					// Check if cell is in a stockpile that accepts wastepacks
					var zone = map.zoneManager.ZoneAt(cell);
					if (zone is Zone_Stockpile stockpile && 
					    stockpile.settings.filter.Allows(ThingDefOf.Wastepack))
					{
						if (GenPlace.TryPlaceThing(stack, cell, map, ThingPlaceMode.Direct))
						{
							Bind(stack);
							placed = true;
						}
					}
				}

				// Second pass: any empty cell within radius (even outside stockpile)
				for (int i = 0; i < numCells && !placed; i++)
				{
					IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
					if (!cell.InBounds(map) || !cell.Standable(map))
						continue;
					if (cell.GetFirstItem(map) != null)
						continue;

					if (GenPlace.TryPlaceThing(stack, cell, map, ThingPlaceMode.Direct))
					{
						Bind(stack);
						placed = true;
					}
				}

				// Last resort: place near original position (should stay in stockpile)
				if (!placed)
				{
					if (GenPlace.TryPlaceThing(stack, originalPos, map, ThingPlaceMode.Near))
					{
						// Check if it ended up within radius
						float distSq = (stack.Position - parent.Position).LengthHorizontalSquared;
						if (distSq <= cachedRadiusSq)
						{
							Bind(stack);
						}
					}
				}
			}
		}

		/// <summary>
		/// Binds a wastepack with a root mote, following vanilla HarbingerTree.TryMakeRoot pattern.
		/// Mote is created only once when binding - not updated each tick.
		/// </summary>
		private void Bind(Thing wastepack)
		{
			if (wastepack == null || boundWastepacks.ContainsKey(wastepack))
				return;

			// Create root mote immediately (like vanilla HarbingerTree.TryMakeRoot)
			Mote mote = null;
			ThingDef moteDef = ThingDefOf.Mote_HarbingerTreeRoots;
			if (moteDef != null && parent.Spawned)
			{
				mote = MoteMaker.MakeStaticMote(
					wastepack.Position.ToVector3Shifted(),
					parent.Map,
					moteDef,
					1f);
			}
			boundWastepacks[wastepack] = mote;
			boundPositions[wastepack] = wastepack.Position; // Track position for move detection
		}

		private void Unbind(Thing wastepack)
		{
			if (wastepack == null)
			{
				// Clean up any null entries and their motes - use pendingUnbind to avoid allocation
				pendingUnbind.Clear();
				foreach (var kvp in boundWastepacks)
				{
					if (kvp.Key == null)
					{
						kvp.Value?.Destroy();
						pendingUnbind.Add(kvp.Key);
					}
				}
				foreach (var key in pendingUnbind)
				{
					boundWastepacks.Remove(key);
					boundPositions.Remove(key);
				}
				return;
			}

			if (!boundWastepacks.TryGetValue(wastepack, out Mote mote))
				return;

			mote?.Destroy();
			boundWastepacks.Remove(wastepack);
			boundPositions.Remove(wastepack);
		}

		private void TryConsumeWastepack()
		{
			// Prioritize ground pollution cleanup (vanilla behavior) before consuming wastepacks
			if (HasPollutedCellsInRadius())
				return;

			// Find smallest bound stack to consume (frees up space faster)
			Thing smallest = null;
			int smallestCount = int.MaxValue;

			foreach (var wastepack in boundWastepacks.Keys)
			{
				if (wastepack == null || wastepack.Destroyed || !wastepack.Spawned)
					continue;

				if (wastepack.stackCount < smallestCount)
				{
					smallest = wastepack;
					smallestCount = wastepack.stackCount;
				}
			}

			if (smallest == null)
				return;

			Map map = parent.Map;
			IntVec3 pos = smallest.Position;

			// Consume one wastepack from the smallest stack
			smallest.SplitOff(1);

			// If stack is now empty, it will be unbound in next UpdateBindings

			// Visual effect
			EffecterDef effecter = DefDatabase<EffecterDef>.GetNamedSilentFail("PollutionExtractedPoluxTree");
			effecter?.Spawn(pos, map)?.Cleanup();
		}

		private bool HasPollutedCellsInRadius()
		{
			if (!parent.Spawned)
				return false;

			// Cache result for 60 ticks to avoid repeated expensive checks
			int currentTick = Find.TickManager.TicksGame;
			if (currentTick - lastPollutionCheckTick < 60)
				return hasPollutedCellsCached;

			lastPollutionCheckTick = currentTick;

			Map map = parent.Map;
			int numCells = GenRadial.NumCellsInRadius(Props.consumeRadius);

			for (int i = 0; i < numCells; i++)
			{
				IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
				if (cell.InBounds(map) && cell.IsPolluted(map))
				{
					hasPollutedCellsCached = true;
					return true;
				}
			}

			hasPollutedCellsCached = false;
			return false;
		}

		public override string CompInspectStringExtra()
		{
			if (!HasBoundWastepacks)
				return null;

			// Count total bound wastepacks without LINQ allocation
			int totalBound = 0;
			foreach (var wastepack in boundWastepacks.Keys)
			{
				if (wastepack != null && !wastepack.Destroyed)
					totalBound += wastepack.stackCount;
			}
			string result = "SubcoreAutomation_PoluxBoundWastepacks".Translate(totalBound);

			// Add consumption progress if not waiting for ground pollution
			if (!HasPollutedCellsInRadius())
			{
				float progress = 1f - ((float)ticksUntilConsume / Props.consumeIntervalTicks);
				progress = Mathf.Clamp01(progress);
				result += "\n" + "SubcoreAutomation_PoluxConsumptionProgress".Translate(progress.ToStringPercent());
			}
			else
			{
				result += "\n" + "SubcoreAutomation_PoluxCleaningGround".Translate();
			}

			return result;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			yield return new Command_Action
			{
				defaultLabel = "SubcoreAutomation_CreateWastepackStockpile".Translate(),
				defaultDesc = "SubcoreAutomation_CreateWastepackStockpileDesc".Translate(),
				icon = CreateWastepackStockpileIcon.Texture,
				action = CreateWastepackStockpile
			};

			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Consume wastepack now",
					action = TryConsumeWastepack
				};
				yield return new Command_Action
				{
					defaultLabel = "DEV: Update bindings",
					action = UpdateBindings
				};
			}
		}

		private void CreateWastepackStockpile()
		{
			Map map = parent.Map;

			Zone existingZone = map.zoneManager.ZoneAt(parent.Position);
			if (existingZone is Zone_Stockpile existingStockpile)
			{
				ExpandZoneAroundTree(existingStockpile);
				return;
			}

			Zone_Stockpile stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
			stockpile.settings.filter.SetDisallowAll();
			stockpile.settings.filter.SetAllow(ThingDefOf.Wastepack, true);
			stockpile.settings.Priority = StoragePriority.Important;

			map.zoneManager.RegisterZone(stockpile);

			int numCells = GenRadial.NumCellsInRadius(Props.consumeRadius);
			for (int i = 0; i < numCells; i++)
			{
				IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
				if (cell.InBounds(map) && Designator_ZoneAdd.IsZoneableCell(cell, map) &&
				    map.zoneManager.ZoneAt(cell) == null &&
				    !cell.GetTerrain(map).IsWater)
				{
					stockpile.AddCell(cell);
				}
			}

			if (stockpile.Cells.Count == 0)
			{
				stockpile.Delete();
				Messages.Message("SubcoreAutomation_NoSpaceForStockpile".Translate(),
					MessageTypeDefOf.RejectInput, historical: false);
			}
			else
			{
				Messages.Message("SubcoreAutomation_WastepackStockpileCreated".Translate(),
					new TargetInfo(parent.Position, map),
					MessageTypeDefOf.PositiveEvent, historical: false);
			}
		}

		private void ExpandZoneAroundTree(Zone_Stockpile stockpile)
		{
			Map map = parent.Map;
			int addedCells = 0;

			int numCells = GenRadial.NumCellsInRadius(Props.consumeRadius);
			for (int i = 0; i < numCells; i++)
			{
				IntVec3 cell = parent.Position + GenRadial.RadialPattern[i];
				if (cell.InBounds(map) && Designator_ZoneAdd.IsZoneableCell(cell, map) &&
				    !cell.GetTerrain(map).IsWater)
				{
					Zone zoneAt = map.zoneManager.ZoneAt(cell);
					if (zoneAt == null)
					{
						stockpile.AddCell(cell);
						addedCells++;
					}
				}
			}

			if (addedCells > 0)
			{
				Messages.Message("SubcoreAutomation_StockpileExpanded".Translate(addedCells),
					new TargetInfo(parent.Position, map),
					MessageTypeDefOf.PositiveEvent, historical: false);
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (Find.Selector.IsSelected(parent))
			{
				GenDraw.DrawRadiusRing(parent.Position, Props.consumeRadius);
			}
		}
	}

	/// <summary>
	/// Global tracker for all Polux tree consumer comps.
	/// Uses map-based dictionary for efficient lookups.
	/// </summary>
	public static class PoluxTreeTracker
	{
		private static Dictionary<int, HashSet<CompPoluxWastepackConsumer>> compsByMap = 
			new Dictionary<int, HashSet<CompPoluxWastepackConsumer>>();

		public static void Register(CompPoluxWastepackConsumer comp)
		{
			if (comp?.parent?.Map == null)
				return;
			int mapId = comp.parent.Map.uniqueID;
			if (!compsByMap.TryGetValue(mapId, out var set))
			{
				set = new HashSet<CompPoluxWastepackConsumer>();
				compsByMap[mapId] = set;
			}
			set.Add(comp);
		}

		public static void Unregister(CompPoluxWastepackConsumer comp, Map map = null)
		{
			if (comp == null)
				return;
			
			// Use provided map or try to get from parent
			map = map ?? comp.parent?.Map;
			if (map == null)
			{
				// Fallback: search all maps
				foreach (var kvp in compsByMap.ToList())
				{
					if (kvp.Value.Remove(comp) && kvp.Value.Count == 0)
						compsByMap.Remove(kvp.Key);
				}
				return;
			}
			
			int mapId = map.uniqueID;
			if (compsByMap.TryGetValue(mapId, out var set))
			{
				set.Remove(comp);
				if (set.Count == 0)
					compsByMap.Remove(mapId);
			}
		}

		/// <summary>
		/// Checks if a wastepack is bound to any Polux tree on the same map.
		/// </summary>
		public static bool IsBoundToAnyTree(Thing wastepack)
		{
			if (wastepack?.Map == null)
				return false;

			if (!compsByMap.TryGetValue(wastepack.Map.uniqueID, out var comps))
				return false;

			foreach (var comp in comps)
			{
				if (comp.IsBound(wastepack))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Checks if a wastepack is within range of any Polux tree on the same map.
		/// </summary>
		public static bool IsNearAnyTree(Thing wastepack)
		{
			if (wastepack?.Map == null || !wastepack.Spawned)
				return false;

			if (!compsByMap.TryGetValue(wastepack.Map.uniqueID, out var comps))
				return false;

			foreach (var comp in comps)
			{
				if (comp.parent == null || !comp.parent.Spawned)
					continue;

				// Use cached value if available, otherwise calculate
				float radiusSq = comp.RadiusSq > 0 ? comp.RadiusSq : comp.Props.consumeRadius * comp.Props.consumeRadius;
				float distSq = (wastepack.Position - comp.parent.Position).LengthHorizontalSquared;

				if (distSq <= radiusSq)
					return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Harmony patches for Polux tree wastepack handling.
	/// </summary>
	[HarmonyPatch]
	public static class PoluxTreeHarmonyPatches
	{
		/// <summary>
		/// Prevent dissolution for wastepacks near Polux trees.
		/// </summary>
		[HarmonyPatch(typeof(CompDissolution), nameof(CompDissolution.CanDissolveNow), MethodType.Getter)]
		[HarmonyPostfix]
		public static void CanDissolveNow_Postfix(CompDissolution __instance, ref bool __result)
		{
			if (!__result || !SubcoreAutomationMod.Settings.poluxTreeFeaturesEnabled)
				return;

			// Early exit for non-wastepacks (critical for performance)
			if (__instance.parent.def != ThingDefOf.Wastepack)
				return;

			if (PoluxTreeTracker.IsNearAnyTree(__instance.parent))
			{
				__result = false;
			}
		}

		/// <summary>
		/// Skip deterioration for wastepacks near Polux trees.
		/// </summary>
		[HarmonyPatch(typeof(SteadyEnvironmentEffects), "TryDoDeteriorate")]
		[HarmonyPrefix]
		public static bool TryDoDeteriorate_Prefix(Thing t)
		{
			if (!SubcoreAutomationMod.Settings.poluxTreeFeaturesEnabled)
				return true;

			if (t.def != ThingDefOf.Wastepack)
				return true;

			if (PoluxTreeTracker.IsNearAnyTree(t))
				return false;

			return true;
		}

		/// <summary>
		/// Prevent bound wastepacks from being merged with other stacks.
		/// This stops pawns from hauling more wastepacks to existing bound stacks.
		/// </summary>
		[HarmonyPatch(typeof(Thing), nameof(Thing.CanStackWith))]
		[HarmonyPostfix]
		public static void CanStackWith_Postfix(Thing __instance, Thing other, ref bool __result)
		{
			if (!__result || !SubcoreAutomationMod.Settings.poluxTreeFeaturesEnabled)
				return;

			// If either stack is bound, prevent merging
			if (__instance.def == ThingDefOf.Wastepack)
			{
				if (PoluxTreeTracker.IsBoundToAnyTree(__instance) ||
				    PoluxTreeTracker.IsBoundToAnyTree(other))
				{
					__result = false;
				}
			}
		}

		/// <summary>
		/// Add wastepack consumer comp to existing Polux trees when map loads.
		/// Handles mid-game mod installation.
		/// </summary>
		[HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
		[HarmonyPostfix]
		public static void Map_FinalizeInit_Postfix(Map __instance)
		{
			if (!ModsConfig.BiotechActive || !SubcoreAutomationMod.Settings.poluxTreeFeaturesEnabled)
				return;

			ThingDef poluxDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreePolux");
			if (poluxDef == null)
				return;

			foreach (Thing thing in __instance.listerThings.ThingsOfDef(poluxDef))
			{
				if (thing is ThingWithComps twc && twc.GetComp<CompPoluxWastepackConsumer>() == null)
				{
					// Add comp retroactively
					var compProps = new CompProperties_PoluxWastepackConsumer();
					var comp = (CompPoluxWastepackConsumer)Activator.CreateInstance(compProps.compClass);
					comp.parent = twc;
					twc.AllComps.Add(comp);
					comp.Initialize(compProps);
					comp.PostSpawnSetup(true); // respawningAfterLoad = true
				}
			}
		}
	}
}
