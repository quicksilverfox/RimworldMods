using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace SubcoreAutomation.Core
{
	/// <summary>
	/// Manages backup power control for generators with installed subcores.
	/// Automatically toggles generators on/off based on power grid needs.
	/// Based on Fluffy's BackupPower mod (MIT license).
	/// </summary>
	public class MapComponent_PowerBroker : MapComponent
	{
		// Cached reflection for DesiredPowerOutput property
		private static readonly PropertyInfo DesiredPowerOutputProperty;
		private static readonly bool HasDesiredPowerOutputProperty;

		// Reusable collections to reduce GC allocations
		private readonly HashSet<CompPowerAutomation> _generators = new HashSet<CompPowerAutomation>();
		private readonly Dictionary<PowerNet, List<CompPowerAutomation>> _generatorsByNet = new Dictionary<PowerNet, List<CompPowerAutomation>>();
		
		// Pooled lists for candidate selection (avoids LINQ .ToList() allocations)
		private readonly List<CompPowerAutomation> _turnOffCandidates = new List<CompPowerAutomation>();
		private readonly List<CompPowerAutomation> _turnOnCandidates = new List<CompPowerAutomation>();
		
		// Pool of lists for _generatorsByNet to avoid allocations
		private readonly List<List<CompPowerAutomation>> _listPool = new List<List<CompPowerAutomation>>();
		private int _listPoolIndex;

		static MapComponent_PowerBroker()
		{
			try
			{
				DesiredPowerOutputProperty = typeof(CompPowerPlant).GetProperty("DesiredPowerOutput",
					BindingFlags.Instance | BindingFlags.NonPublic);
				HasDesiredPowerOutputProperty = DesiredPowerOutputProperty != null;
			}
			catch (Exception ex)
			{
				Log.Warning($"[SubcoreAutomation] Failed to cache DesiredPowerOutput property: {ex.Message}");
				HasDesiredPowerOutputProperty = false;
			}
		}

		public MapComponent_PowerBroker(Map map) : base(map)
		{
		}

		public static MapComponent_PowerBroker For(Map map)
		{
			return map?.GetComponent<MapComponent_PowerBroker>();
		}

		public static void RegisterGenerator(CompPowerAutomation comp)
		{
			if (comp?.parent?.Map == null) return;
			var broker = For(comp.parent.Map);
			if (broker != null && !broker._generators.Contains(comp))
			{
				broker._generators.Add(comp);
			}
		}

		public static void DeregisterGenerator(CompPowerAutomation comp)
		{
			// Handle case where map might be null during despawn
			if (comp?.parent == null) return;
			
			// Try to get map from parent, or from cached map reference
			var map = comp.parent.Map ?? comp.parent.MapHeld;
			if (map == null) return;
			
			var broker = For(map);
			broker?._generators.Remove(comp);
		}

		/// <summary>
		/// Gets a list from the pool, creating new ones as needed.
		/// </summary>
		private List<CompPowerAutomation> GetPooledList()
		{
			if (_listPoolIndex < _listPool.Count)
			{
				var list = _listPool[_listPoolIndex];
				list.Clear();
				_listPoolIndex++;
				return list;
			}
			
			var newList = new List<CompPowerAutomation>();
			_listPool.Add(newList);
			_listPoolIndex++;
			return newList;
		}

		public override void MapComponentTick()
		{
			base.MapComponentTick();

			if (!SubcoreAutomationMod.Settings.backupPowerEnabled)
				return;

			if (Find.TickManager.TicksGame % SubcoreAutomationMod.Settings.backupPowerUpdateInterval != 0)
				return;

			// Clean up destroyed/despawned generators
			_generators.RemoveWhere(g => g?.parent == null || !g.parent.Spawned || g.parent.Destroyed);

			// Reset list pool for this tick
			_listPoolIndex = 0;
			_generatorsByNet.Clear();
			
			// Group by power net - reuse pooled lists
			foreach (var gen in _generators)
			{
				if (!gen.HasSubcoreInstalled || !gen.IsGenerator || gen.PowerNet == null)
					continue;

				if (!_generatorsByNet.TryGetValue(gen.PowerNet, out var list))
				{
					list = GetPooledList();
					_generatorsByNet[gen.PowerNet] = list;
				}
				list.Add(gen);
			}

			foreach (var kvp in _generatorsByNet)
			{
				try
				{
					ProcessPowerNet(kvp.Key, kvp.Value);
				}
				catch (Exception ex)
				{
					Log.ErrorOnce($"[SubcoreAutomation] Error processing power net: {ex.Message}", 
						kvp.Key.GetHashCode() ^ 0x1337);
				}
			}
		}

		private void ProcessPowerNet(PowerNet net, List<CompPowerAutomation> generators)
		{
			// Calculate power needs
			float totalConsumption = 0f;
			float currentProduction = 0f;
			
			foreach (var comp in net.powerComps)
			{
				totalConsumption += GetConsumption(comp);
				currentProduction += GetCurrentProduction(comp);
			}

			bool hasStorage = net.batteryComps != null && net.batteryComps.Count > 0;
			float storageLevel = GetStorageLevel(net);

			// If we have excess power or batteries, try turning off backup generators
			if (currentProduction > totalConsumption || (hasStorage && storageLevel > 0))
			{
				TryTurnOffBackups(generators, currentProduction, totalConsumption, hasStorage, storageLevel);
			}

			// If we need more power or batteries are low, try turning on backup generators
			bool hasProductionShortfall = currentProduction < totalConsumption;
			if (hasProductionShortfall || (hasStorage && storageLevel < 1))
			{
				TryTurnOnBackups(net, generators, hasStorage, storageLevel, hasProductionShortfall);
			}
		}

		private void TryTurnOffBackups(List<CompPowerAutomation> generators, float production, float consumption, bool hasStorage, float storageLevel)
		{
			// Use pre-allocated list instead of LINQ .Where().ToList()
			_turnOffCandidates.Clear();
			
			for (int i = 0; i < generators.Count; i++)
			{
				var g = generators[i];
				float genProduction = GetCurrentProduction(g.PowerComp);
				
				if (genProduction > 0 
					&& g.CanTurnOffGenerator()
					&& (genProduction <= (production - consumption) || g.BackupPowerRunOnBatteriesOnly)
					&& ((!hasStorage && !g.BackupPowerRunOnBatteriesOnly) || storageLevel >= g.BackupPowerBatteryMax))
				{
					_turnOffCandidates.Add(g);
				}
			}

			if (_turnOffCandidates.TryRandomElementByWeight(c => 1f / Mathf.Max(GetCurrentProduction(c.PowerComp), 1f), out var chosen))
			{
				chosen.TurnOffGenerator();
			}
		}

		private void TryTurnOnBackups(PowerNet net, List<CompPowerAutomation> generators, bool hasStorage, float storageLevel, bool hasProductionShortfall)
		{
			// Use pre-allocated list instead of LINQ .Where().ToList()
			_turnOnCandidates.Clear();
			
			for (int i = 0; i < generators.Count; i++)
			{
				var g = generators[i];
				float currentProd = GetCurrentProduction(g.PowerComp);
				float potentialProd = GetPotentialProduction(g);
				
				// Generator must be off and able to produce power
				if (Mathf.Abs(currentProd) >= 0.01f || potentialProd <= 0)
					continue;
				
				// Decide if this generator should turn on based on its mode
				bool shouldTurnOn;
				if (g.BackupPowerRunOnBatteriesOnly)
				{
					// Only turn on based on battery level
					shouldTurnOn = hasStorage && storageLevel <= g.BackupPowerBatteryMin;
				}
				else
				{
					// Turn on for battery level OR production shortfall
					bool batteryLow = hasStorage && storageLevel <= g.BackupPowerBatteryMin;
					shouldTurnOn = batteryLow || hasProductionShortfall;
				}
				
				if (shouldTurnOn)
				{
					_turnOnCandidates.Add(g);
				}
			}

			if (_turnOnCandidates.TryRandomElementByWeight(c => GetPotentialProduction(c), out var chosen))
			{
				chosen.TurnOnGenerator();
			}
		}

		private float GetConsumption(CompPowerTrader comp)
		{
			if (!comp.PowerOn && !FlickUtility.WantsToBeOn(comp.parent))
				return 0f;

			return Mathf.Max(-comp.PowerOutput, 0f);
		}

		private float GetCurrentProduction(CompPowerTrader comp)
		{
			if (!(comp is CompPowerPlant plant))
				return 0f;

			if (!plant.PowerOn)
				return 0f;

			return Mathf.Max(plant.PowerOutput, 0f);
		}

		private float GetPotentialProduction(CompPowerAutomation gen)
		{
			var plant = gen.PowerComp as CompPowerPlant;
			if (plant == null)
				return 0f;

			// Use cached comps from CompSubcoreAutomation when available
			var refuelable = gen.CachedRefuelable;
			if (refuelable != null && !refuelable.HasFuel)
				return 0f;

			var breakdownable = gen.CachedBreakdownable;
			if (breakdownable != null && breakdownable.BrokenDown)
				return 0f;

			// Use cached reflection to get DesiredPowerOutput
			float desired = GetDesiredOutput(plant);
			return Mathf.Max(desired, plant.PowerOutput, 0f);
		}

		private float GetDesiredOutput(CompPowerPlant plant)
		{
			if (!HasDesiredPowerOutputProperty)
				return plant.Props.PowerConsumption;

			try
			{
				return (float)DesiredPowerOutputProperty.GetValue(plant);
			}
			catch
			{
				return plant.Props.PowerConsumption;
			}
		}

		private float GetStorageLevel(PowerNet net)
		{
			if (net?.batteryComps == null || net.batteryComps.Count == 0)
				return 0f;

			float current = 0f;
			float max = 0f;

			foreach (var battery in net.batteryComps)
			{
				current += battery.StoredEnergy;
				max += battery.Props.storedEnergyMax;
			}

			return max > 0 ? current / max : 0f;
		}
	}
}
