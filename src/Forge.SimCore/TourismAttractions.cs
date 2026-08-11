using Forge.Engine.Data;
using Forge.Engine.Simulation;

namespace Forge.SimCore;

/// <summary>
/// Tourism attractions stub — parks and landmarks drive visitor demand into
/// <c>BudgetSystem.TourismIncome</c> (not only population × happiness).
/// Cathedral Tier-2 / <c>MISSING_SYSTEMS.md</c> §13 tourism mechanics (thin).
/// </summary>
public static class TourismAttractions
{
    /// <summary>ServiceFlags bit — park / recreation ploppable (shared with <see cref="ParkAmenity"/>).</summary>
    public const uint ServicePark = ParkAmenity.ServicePark;

    /// <summary>ServiceFlags bit — landmark / monument / civic attraction.</summary>
    public const uint ServiceLandmark = 1u << 12;

    /// <summary>Painted park / recreation zone (TileData.ZoneType byte 8).</summary>
    public const byte ZonePark = ParkAmenity.ZonePark;

    /// <summary>Painted park tiles per one attraction unit (ceil).</summary>
    public const int ParkTilesPerAttraction = 9;

    /// <summary>Extra monthly tourists per park attraction.</summary>
    public const float VisitorsPerParkAttraction = 20f;

    /// <summary>Extra monthly tourists per landmark (monuments draw harder).</summary>
    public const float VisitorsPerLandmark = 80f;

    /// <summary>Average spend per tourist (matches legacy BudgetSystem tourism line).</summary>
    public const float SpendPerTourist = 50f;

    /// <summary>Active buildings with <see cref="ServicePark"/>.</summary>
    public static int CountParkBuildings(WorldState state)
    {
        var buildings = state.Buildings;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServicePark) != 0)
                count++;
        }
        return count;
    }

    /// <summary>Painted <see cref="ZonePark"/> tile count.</summary>
    public static int CountParkZoneTiles(WorldState state)
    {
        var tiles = state.Tiles;
        int count = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles.ZoneType[i] == ZonePark)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Park attraction units: each park building + ceil(park zone tiles / 9).
    /// Empty city → 0.
    /// </summary>
    public static int CountParkAttractions(WorldState state)
    {
        int buildings = CountParkBuildings(state);
        int tiles = CountParkZoneTiles(state);
        int zoneUnits = tiles <= 0
            ? 0
            : (tiles + ParkTilesPerAttraction - 1) / ParkTilesPerAttraction;
        return buildings + zoneUnits;
    }

    /// <summary>Active buildings with <see cref="ServiceLandmark"/>.</summary>
    public static int CountLandmarkAttractions(WorldState state)
    {
        var buildings = state.Buildings;
        int count = 0;
        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i)) continue;
            if ((buildings.ServiceFlags[i] & ServiceLandmark) != 0)
                count++;
        }
        return count;
    }

    /// <summary>Park + landmark attraction units.</summary>
    public static int CountTourismAttractions(WorldState state)
        => CountParkAttractions(state) + CountLandmarkAttractions(state);

    /// <summary>Visitor demand attributable to attractions (monthly tourist count).</summary>
    public static float AttractionTourists(int parkAttractions, int landmarkAttractions)
    {
        parkAttractions = Math.Max(0, parkAttractions);
        landmarkAttractions = Math.Max(0, landmarkAttractions);
        return parkAttractions * VisitorsPerParkAttraction
             + landmarkAttractions * VisitorsPerLandmark;
    }

    /// <summary>
    /// Monthly tourism income = (base tourists + attraction tourists) × spend.
    /// Base tourists remain the legacy pop × happiness × cosmopolitan term.
    /// </summary>
    public static float ComputeTourismIncome(float baseTourists, int parkAttractions, int landmarkAttractions)
    {
        float tourists = Math.Max(0f, baseTourists)
            + AttractionTourists(parkAttractions, landmarkAttractions);
        return tourists * SpendPerTourist;
    }

    /// <summary>
    /// Writes attraction counts onto <see cref="WorldState"/> (does not recompute income —
    /// <c>BudgetSystem</c> owns the monthly tourism line).
    /// </summary>
    public static void PublishCounts(WorldState state)
    {
        int parks = CountParkAttractions(state);
        int landmarks = CountLandmarkAttractions(state);
        state.ParkAttractionCount = parks;
        state.LandmarkAttractionCount = landmarks;
        state.TourismAttractionCount = parks + landmarks;
    }
}
