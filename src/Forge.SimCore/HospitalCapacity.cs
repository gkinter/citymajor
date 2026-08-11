using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Hospital bed capacity + EMS transport to the nearest hospital with free beds
/// (Cathedral Tier-2 / <c>MISSING_SYSTEMS.md</c> §1.2).
/// Beds use <see cref="BuildingData.MaxOccupants"/> / <see cref="BuildingData.Occupants"/>
/// on buildings with <see cref="ServiceHealth"/>.
/// </summary>
public static class HospitalCapacity
{
    /// <summary>Same bit as <see cref="EmergencyResponseTime.ServiceHealth"/>.</summary>
    public const uint ServiceHealth = EmergencyResponseTime.ServiceHealth;

    /// <summary>Beds when a health building has <c>MaxOccupants == 0</c>.</summary>
    public const ushort DefaultBeds = 50;

    /// <summary>
    /// Transport / diversion minutes when every hospital is full (or none exist
    /// after the player has placed at least one health building that filled up).
    /// Matches no-station EMS default so survival falls to the ≥15 min bucket.
    /// </summary>
    public const float NoCapacityTransportMinutes = EmergencyResponseTime.NoStationResponseMinutes;

    /// <summary>Bed capacity for a health building (MaxOccupants, else default × level).</summary>
    public static int GetBedCapacity(BuildingData buildings, int buildingId)
    {
        if (buildingId < 0 || buildingId >= buildings.Capacity) return 0;
        if (!buildings.IsActive(buildingId)) return 0;
        if ((buildings.ServiceFlags[buildingId] & ServiceHealth) == 0) return 0;

        ushort max = buildings.MaxOccupants[buildingId];
        if (max > 0) return max;

        int level = Math.Max(1, (int)buildings.Level[buildingId]);
        return DefaultBeds * level;
    }

    /// <summary>Occupied beds clamped to capacity.</summary>
    public static int GetOccupiedBeds(BuildingData buildings, int buildingId)
    {
        int capacity = GetBedCapacity(buildings, buildingId);
        if (capacity <= 0) return 0;
        return Math.Min(capacity, buildings.Occupants[buildingId]);
    }

    /// <summary>Free beds at a health building (0 if inactive / non-health).</summary>
    public static int GetAvailableBeds(BuildingData buildings, int buildingId)
    {
        int capacity = GetBedCapacity(buildings, buildingId);
        if (capacity <= 0) return 0;
        return Math.Max(0, capacity - GetOccupiedBeds(buildings, buildingId));
    }

    /// <summary>True when the building is an active hospital with at least one free bed.</summary>
    public static bool HasAvailableCapacity(BuildingData buildings, int buildingId)
        => GetAvailableBeds(buildings, buildingId) > 0;

    /// <summary>City-wide free beds across active health buildings.</summary>
    public static int CountAvailableBeds(WorldState state)
    {
        var buildings = state.Buildings;
        int sum = 0;
        for (int i = 0; i < buildings.Capacity; i++)
            sum += GetAvailableBeds(buildings, i);
        return sum;
    }

    /// <summary>City-wide bed capacity across active health buildings.</summary>
    public static int CountTotalBeds(WorldState state)
    {
        var buildings = state.Buildings;
        int sum = 0;
        for (int i = 0; i < buildings.Capacity; i++)
            sum += GetBedCapacity(buildings, i);
        return sum;
    }

    /// <summary>
    /// Occupied / total beds (0–1). Empty city (no hospitals) reports 0 — no unmet demand yet.
    /// </summary>
    public static float CalculateOccupancyFraction(WorldState state)
    {
        int total = CountTotalBeds(state);
        if (total <= 0) return 0f;

        var buildings = state.Buildings;
        int occupied = 0;
        for (int i = 0; i < buildings.Capacity; i++)
            occupied += GetOccupiedBeds(buildings, i);

        return Math.Clamp(occupied / (float)total, 0f, 1f);
    }

    /// <summary>
    /// Nearest active health building with free beds (Euclidean). Skips full hospitals
    /// so EMS diverts to the next with capacity (<c>MISSING_SYSTEMS</c> §1.2).
    /// </summary>
    public static bool TryFindNearestWithCapacity(
        WorldState state,
        int tileX,
        int tileY,
        out int hospitalId,
        out int hospitalX,
        out int hospitalY,
        out float euclideanDistance)
    {
        hospitalId = -1;
        hospitalX = 0;
        hospitalY = 0;
        euclideanDistance = float.MaxValue;

        var buildings = state.Buildings;
        float bestDistSq = float.MaxValue;
        bool found = false;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!HasAvailableCapacity(buildings, i)) continue;

            float dx = buildings.GridX[i] - tileX;
            float dy = buildings.GridY[i] - tileY;
            float distSq = dx * dx + dy * dy;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                hospitalId = i;
                hospitalX = buildings.GridX[i];
                hospitalY = buildings.GridY[i];
                found = true;
            }
        }

        if (!found) return false;

        euclideanDistance = MathF.Sqrt(bestDistSq);
        return true;
    }

    /// <summary>
    /// Minutes to transport from the incident tile to the nearest hospital with capacity.
    /// <list type="bullet">
    /// <item>Hospital with free beds → road-graph / BPR path ÷ vehicle speed (no crew delay).</item>
    /// <item>Health buildings exist but all full → <see cref="NoCapacityTransportMinutes"/>.</item>
    /// <item>No health buildings → 0 (legacy P5.4: survival from station response only).</item>
    /// </list>
    /// </summary>
    public static float CalculateTransportMinutes(
        WorldState state,
        int incidentX,
        int incidentY,
        float[]? edgeTravelTimes = null,
        float vehicleSpeedTilesPerMinute = EmergencyResponseTime.DefaultVehicleSpeedTilesPerMinute)
    {
        if (!state.Tiles.InBounds(incidentX, incidentY))
            return NoCapacityTransportMinutes;

        if (TryFindNearestWithCapacity(
                state, incidentX, incidentY,
                out _, out int hx, out int hy, out float euclidean))
        {
            float pathCost = EmergencyResponseTime.CalculatePathCost(
                state, incidentX, incidentY, hx, hy, edgeTravelTimes, euclidean);
            float speed = Math.Max(0.1f, vehicleSpeedTilesPerMinute);
            return pathCost / speed;
        }

        // Full hospitals → divert / overflow penalty. No hospitals → no transport leg.
        return CountTotalBeds(state) > 0 ? NoCapacityTransportMinutes : 0f;
    }

    /// <summary>
    /// Full EMS chain minutes: station→incident response + incident→hospital transport.
    /// Feeds <see cref="EmsSurvival.CalculateRate"/>.
    /// </summary>
    public static float CalculateEmsChainMinutes(
        float stationResponseMinutes,
        float hospitalTransportMinutes)
    {
        float station = float.IsNaN(stationResponseMinutes) || float.IsInfinity(stationResponseMinutes)
            ? EmergencyResponseTime.NoStationResponseMinutes
            : Math.Max(0f, stationResponseMinutes);
        float transport = float.IsNaN(hospitalTransportMinutes) || float.IsInfinity(hospitalTransportMinutes)
            ? NoCapacityTransportMinutes
            : Math.Max(0f, hospitalTransportMinutes);
        return station + transport;
    }
}
