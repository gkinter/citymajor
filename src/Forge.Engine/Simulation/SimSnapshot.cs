namespace Forge.Engine.Simulation;

/// <summary>
/// Immutable snapshot of simulation state for the render thread.
/// Copied from WorldState at the end of each simulation tick via double-buffering.
/// Contains only the data needed for rendering -- no mutable references.
/// </summary>
public sealed class SimSnapshot
{
    /// <summary>Flat array of tile terrain types for rendering (worldSize * worldSize).</summary>
    public byte[] TileTerrainTypes { get; init; } = [];

    /// <summary>Flat array of tile zone types for overlay rendering.</summary>
    public byte[] TileZoneTypes { get; init; } = [];

    /// <summary>Building positions and types for sprite rendering.</summary>
    public BuildingSnapshot[] Buildings { get; init; } = [];
    public int BuildingCount { get; init; }

    /// <summary>Vehicle positions for sprite rendering.</summary>
    public VehicleSnapshot[] Vehicles { get; init; } = [];
    public int VehicleCount { get; init; }

    // Scalar state for UI
    public long TickCount { get; init; }
    public string DateString { get; init; } = "";
    public long CityFunds { get; init; }
    public int Population { get; init; }
    public float Happiness { get; init; }

    // --- Extended data for visual life systems ---

    /// <summary>Flat array of tile road flags for rendering (worldSize * worldSize).</summary>
    public byte[] TileRoadFlags { get; init; } = [];

    /// <summary>Flat array of tile traffic density for rendering (worldSize * worldSize). 0.0-1.0.</summary>
    public float[] TileTraffic { get; init; } = [];

    /// <summary>World size in tiles (one axis of the square grid).</summary>
    public int WorldSize { get; init; }

    /// <summary>Current hour of day as a float (0.0-24.0). Derived from TickCount.</summary>
    public float TimeOfDay { get; init; }

    /// <summary>Current weather condition. 0=clear, 1=cloudy, 2=rain, 3=storm, 4=snow, 5=fog, 6=heatwave, 7=blizzard.</summary>
    public int WeatherCondition { get; init; }

    /// <summary>Current season: 0=spring, 1=summer, 2=autumn, 3=winter.</summary>
    public int Season { get; init; }

    /// <summary>Current era: 0=Ancient, 1=Medieval, 2=Colonial, 3=Industrial, 4=Modern, 5=Future.</summary>
    public int Era { get; init; }

    /// <summary>Wind speed in m/s.</summary>
    public float WindSpeed { get; init; }

    /// <summary>Wind direction in radians.</summary>
    public float WindDirection { get; init; }

    // --- Budget / Economy data for UI panels ---

    /// <summary>Monthly income total from last completed month.</summary>
    public long MonthlyIncome { get; init; }

    /// <summary>Monthly expenses total from last completed month.</summary>
    public long MonthlyExpenses { get; init; }

    /// <summary>Property tax rate (0.0-1.0).</summary>
    public float PropertyTaxRate { get; init; }

    /// <summary>Commercial tax rate (0.0-1.0).</summary>
    public float CommercialTaxRate { get; init; }

    /// <summary>Industrial tax rate (0.0-1.0).</summary>
    public float IndustrialTaxRate { get; init; }

    /// <summary>Outstanding loan balance.</summary>
    public long LoanBalance { get; init; }

    // --- Research / Technology data ---

    /// <summary>Accumulated research points.</summary>
    public float ResearchPoints { get; init; }

    /// <summary>Monthly research point generation rate.</summary>
    public float ResearchRate { get; init; }

    /// <summary>Currently researching technology ID. -1 = idle.</summary>
    public int CurrentResearchId { get; init; }

    /// <summary>Progress toward current research (0.0-1.0).</summary>
    public float CurrentResearchProgress { get; init; }

    // --- Politics data ---

    /// <summary>Mayor approval rating (0.0-1.0).</summary>
    public float ApprovalRating { get; init; }

    /// <summary>City council seat faction assignments (9 seats).</summary>
    public byte[] CouncilSeats { get; init; } = [];

    /// <summary>Active ordinances bitfield.</summary>
    public ulong ActiveOrdinances { get; init; }

    /// <summary>Next election year.</summary>
    public int NextElectionYear { get; init; }

    // --- Cultural DNA (8 dimensions) ---

    /// <summary>8-dimensional cultural identity snapshot.</summary>
    public float[] CulturalDna { get; init; } = [];

    // --- Active events ---

    /// <summary>Number of currently active events.</summary>
    public int ActiveEventCount { get; init; }

    public readonly record struct BuildingSnapshot(
        int GridX, int GridY, ushort TypeId, byte Level,
        byte State, ushort Occupants, ushort MaxOccupants, byte Condition);

    public readonly record struct VehicleSnapshot(
        float WorldX, float WorldY, ushort TypeId, float Heading,
        float Speed, float MaxSpeed, byte Flags);

    /// <summary>
    /// Create a snapshot from the current world state. Called on the simulation thread.
    /// </summary>
    public static SimSnapshot CaptureFrom(WorldState state)
    {
        // Copy tile data (only terrain and zone for rendering)
        int tileCount = state.Tiles.Size * state.Tiles.Size;
        var terrainCopy = new byte[tileCount];
        var zoneCopy = new byte[tileCount];
        var roadFlagsCopy = new byte[tileCount];
        var trafficCopy = new float[tileCount];
        Array.Copy(state.Tiles.TerrainType, terrainCopy, tileCount);
        Array.Copy(state.Tiles.ZoneType, zoneCopy, tileCount);
        Array.Copy(state.Tiles.RoadFlags, roadFlagsCopy, tileCount);
        Array.Copy(state.Tiles.Traffic, trafficCopy, tileCount);

        // Copy buildings — pool slots are sparse; Count is active total, not dense 0..Count-1
        var buildingPool = state.Buildings;
        int bCount = buildingPool.Count;
        var buildings = new BuildingSnapshot[bCount];
        int buildingOut = 0;
        for (int i = 0; i < buildingPool.Capacity && buildingOut < bCount; i++)
        {
            if (!buildingPool.IsActive(i)) continue;
            buildings[buildingOut++] = new BuildingSnapshot(
                buildingPool.GridX[i],
                buildingPool.GridY[i],
                buildingPool.TypeId[i],
                buildingPool.Level[i],
                buildingPool.State[i],
                buildingPool.Occupants[i],
                buildingPool.MaxOccupants[i],
                buildingPool.Condition[i]
            );
        }

        // Copy vehicles — same sparse slot layout as buildings
        var vehiclePool = state.Vehicles;
        int vCount = vehiclePool.Count;
        var vehicles = new VehicleSnapshot[vCount];
        int vehicleOut = 0;
        for (int i = 0; i < vehiclePool.Capacity && vehicleOut < vCount; i++)
        {
            if (!vehiclePool.IsActive(i)) continue;
            vehicles[vehicleOut++] = new VehicleSnapshot(
                vehiclePool.WorldX[i],
                vehiclePool.WorldY[i],
                vehiclePool.TypeId[i],
                vehiclePool.Heading[i],
                vehiclePool.Speed[i],
                vehiclePool.MaxSpeed[i],
                vehiclePool.Flags[i]
            );
        }

        // Derive time of day from tick count (each tick = 1 simulation step,
        // assume 1 tick = 1 game-minute; 1440 ticks/day)
        float timeOfDay = (state.TickCount % 1440) / 60f;

        return new SimSnapshot
        {
            TileTerrainTypes = terrainCopy,
            TileZoneTypes = zoneCopy,
            TileRoadFlags = roadFlagsCopy,
            TileTraffic = trafficCopy,
            WorldSize = state.Tiles.Size,
            Buildings = buildings,
            BuildingCount = bCount,
            Vehicles = vehicles,
            VehicleCount = vCount,
            TickCount = state.TickCount,
            DateString = state.DateString,
            CityFunds = state.CityFunds,
            Population = state.Population,
            Happiness = state.Happiness,
            TimeOfDay = timeOfDay,
            WeatherCondition = state.WeatherCondition,
            Season = state.Season,
            Era = state.Era,
            WindSpeed = state.WindSpeed,
            WindDirection = state.WindDirection,
            // Budget / Economy
            MonthlyIncome = state.Income.Total,
            MonthlyExpenses = state.Expenses.Total,
            PropertyTaxRate = state.PropertyTaxRate,
            CommercialTaxRate = state.CommercialTaxRate,
            IndustrialTaxRate = state.IndustrialTaxRate,
            LoanBalance = state.LoanBalance,
            // Research / Technology
            ResearchPoints = state.ResearchPoints,
            ResearchRate = state.ResearchRate,
            CurrentResearchId = state.CurrentResearchId,
            CurrentResearchProgress = state.CurrentResearchProgress,
            // Politics
            ApprovalRating = state.ApprovalRating,
            CouncilSeats = (byte[])state.CouncilSeats.Clone(),
            ActiveOrdinances = state.ActiveOrdinances,
            NextElectionYear = state.NextElectionYear,
            // Cultural DNA
            CulturalDna = (float[])state.CulturalDna.Clone(),
            // Events
            ActiveEventCount = state.ActiveEventCount,
        };
    }
}
