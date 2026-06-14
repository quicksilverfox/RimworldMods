using System.Collections.Generic;
using RimWorld;
using SubcoreAutomation.Core;
using Verse;

namespace SubcoreAutomation.Handlers
{
	/// <summary>
	/// Handler for automated Comms Console - spawns bonus orbital traders.
	/// </summary>
	public static class CommsConsoleHandler
	{
		// Cooldown: 24 hours = 60000 ticks
		private const int CooldownTicks = 60000;

		// Track last bonus trader tick per map (map-wide, not per-console)
		private static Dictionary<int, int> _lastBonusTraderTick = new Dictionary<int, int>();

		// Track which maps have rolled this tick interval (prevents multiple consoles from rolling multiple times)
		private static Dictionary<int, int> _lastRollTick = new Dictionary<int, int>();

		/// <summary>
		/// Handles automation for the Comms Console.
		/// Periodically spawns bonus orbital traders when subcore is installed.
		/// Multiple consoles on the same map share the same roll and cooldown.
		/// </summary>
		public static bool HandleAutomation(Thing building, CompSubcoreAutomationBase comp, float speedFactor)
		{
			if (!SubcoreAutomationMod.Settings.commsConsoleFeaturesEnabled)
				return false;

			if (!comp.SubcoreInstalled)
				return false;

			// Check power
			var powerComp = building.TryGetComp<CompPowerTrader>();
			if (powerComp != null && !powerComp.PowerOn)
				return false;

			// MTB check - tick every 250 ticks (like rare tick)
			if (!building.IsHashIntervalTick(250))
				return true;

			Map map = building.Map;
			if (map == null)
				return true;

			int mapId = map.uniqueID;
			int currentTick = GenTicks.TicksGame;

			// Check if we already rolled for this map this tick interval
			if (_lastRollTick.TryGetValue(mapId, out int lastRoll) && lastRoll == currentTick)
				return true; // Already rolled this tick, skip

			// Mark that we're rolling for this map this tick
			_lastRollTick[mapId] = currentTick;

			// Check 24h cooldown (map-wide)
			if (_lastBonusTraderTick.TryGetValue(mapId, out int lastTraderTick))
			{
				if (currentTick - lastTraderTick < CooldownTicks)
					return true; // Still on cooldown
			}

			// Roll for bonus trader using configured MTB
			float mtbDays = SubcoreAutomationMod.Settings.commsConsoleBonusTraderDays;
			if (Rand.MTBEventOccurs(mtbDays, 60000f, 250f))
			{
				if (TrySpawnBonusTrader(map))
				{
					// Record when we spawned a trader for cooldown
					_lastBonusTraderTick[mapId] = currentTick;
				}
			}

			return true;
		}

		/// <summary>
		/// Attempts to spawn a bonus orbital trader.
		/// Returns true if successful.
		/// </summary>
		private static bool TrySpawnBonusTrader(Map map)
		{
			// Check if we can spawn (not at max ships)
			if (map.passingShipManager.passingShips.Count >= 5)
				return false;

			var parms = StorytellerUtility.DefaultParmsNow(
				IncidentDefOf.OrbitalTraderArrival.category, map);

			if (IncidentDefOf.OrbitalTraderArrival.Worker.CanFireNow(parms))
			{
				return IncidentDefOf.OrbitalTraderArrival.Worker.TryExecute(parms);
			}

			return false;
		}

	}
}
