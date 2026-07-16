using Forge.Engine.Data;

namespace Forge.Engine.Simulation;

/// <summary>
/// Master state container for the entire simulation. Owns all game data pools
/// and provides the single source of truth for the simulation thread.
///
/// All mutable game state lives here -- nothing is scattered across static fields
/// or singletons. This makes save/load a matter of serializing one object tree,
/// and makes the simulation thread boundary explicit.
/// </summary>
public sealed class WorldState
{
    // =========================================================================
    // Entity pools (SoA)
    // =========================================================================

    public TileData Tiles { get; }
    public HouseholdData Households { get; }
    public BuildingData Buildings { get; }
    public RoadGraph Roads { get; }
    public VehicleData Vehicles { get; }

    // =========================================================================
    // Game clock
    // =========================================================================

    /// <summary>Total simulation ticks elapsed since game start.</summary>
    public long TickCount { get; set; }

    /// <summary>Current day of the month (1-30).</summary>
    public int Day { get; set; } = 1;

    /// <summary>Current month (1-12).</summary>
    public int Month { get; set; } = 1;

    /// <summary>Current year.</summary>
    public int Year { get; set; } = 2024;

    /// <summary>
    /// Historical era that affects available buildings, tech, and aesthetics.
    /// 0=Ancient, 1=Medieval, 2=Colonial, 3=Industrial, 4=Modern, 5=Future.
    /// Advances automatically based on research progress, but can be locked by the player.
    /// </summary>
    public int Era { get; set; } = 4; // Start in Modern era

    /// <summary>Calendar display string, cached and updated on AdvanceDay.</summary>
    public string DateString { get; private set; } = "2024-01-01";

    // =========================================================================
    // Demographics
    // =========================================================================

    /// <summary>Total city population (sum of all household members).</summary>
    public int Population { get; set; }

    /// <summary>Average city happiness (0.0 = riot, 1.0 = utopia).</summary>
    public float Happiness { get; set; } = 0.5f;

    /// <summary>Share of working-age households with a workplace (0–1).</summary>
    public float EmploymentRate { get; set; } = 0.5f;

    /// <summary>1 − <see cref="EmploymentRate"/>.</summary>
    public float UnemploymentRate
    {
        get
        {
            float u = 1f - EmploymentRate;
            if (u < 0f) return 0f;
            if (u > 1f) return 1f;
            return u;
        }
    }

    /// <summary>Net trade balance last month (exports − imports).</summary>
    public float TradeBalance { get; set; }

    /// <summary>Export value last month.</summary>
    public float MonthlyExportValue { get; set; }

    /// <summary>Import cost last month.</summary>
    public float MonthlyImportCost { get; set; }

    /// <summary>Mean road-tile traffic density (0–1+), for tests / overlays.</summary>
    public float MeanTrafficDensity { get; set; }

    /// <summary>
    /// Multiplier on road edge capacity from active traffic ordinances (1.0 = neutral).
    /// Written by SimHost when laws toggle; read by WasmTrafficLite.
    /// </summary>
    public float LawTrafficCapacityMult { get; set; } = 1f;

    /// <summary>Count of player-enabled ordinances (mirrors LawSystem.ActiveLawCount).</summary>
    public int ActiveLawCount { get; set; }

    /// <summary>Construction duration multiplier from active laws (1.0 = neutral).</summary>
    public float LawConstructionSpeedMult { get; set; } = 1f;

    /// <summary>Baseline zone spawn demand multiplier from construction_cost ordinances.</summary>
    public float LawSpawnDemandMult { get; set; } = 1f;

    /// <summary>Residential zone spawn multiplier (housing_supply / housing_density).</summary>
    public float LawResidentialSpawnMult { get; set; } = 1f;

    /// <summary>Industrial zone spawn multiplier (industrial_output).</summary>
    public float LawIndustrialSpawnMult { get; set; } = 1f;

    /// <summary>Commercial / office zone spawn multiplier.</summary>
    public float LawCommercialSpawnMult { get; set; } = 1f;

    /// <summary>Buildings currently in constructing state (updated each zone growth tick).</summary>
    public int ConstructingBuildingCount { get; set; }

    // =========================================================================
    // Goods economy imbalance (persisted after daily economy tick)
    // =========================================================================

    public const int MaxTopGoodImbalances = 5;

    /// <summary>Top shortage good IDs (Good enum byte), dense prefix of length <see cref="TopShortageCount"/>.</summary>
    public byte[] TopShortageGoodIds { get; } = new byte[MaxTopGoodImbalances];

    /// <summary>Demand − supply magnitude per top shortage good.</summary>
    public float[] TopShortageScores { get; } = new float[MaxTopGoodImbalances];

    /// <summary>Top surplus good IDs (Good enum byte), dense prefix of length <see cref="TopSurplusCount"/>.</summary>
    public byte[] TopSurplusGoodIds { get; } = new byte[MaxTopGoodImbalances];

    /// <summary>Supply − demand magnitude per top surplus good.</summary>
    public float[] TopSurplusScores { get; } = new float[MaxTopGoodImbalances];

    public int TopShortageCount { get; set; }

    public int TopSurplusCount { get; set; }

    /// <summary>City-wide shortage pressure (0–1) for Herald / HUD buckets.</summary>
    public float GoodsShortageIndex { get; set; }

    /// <summary>City-wide surplus pressure (0–1) for Herald / HUD buckets.</summary>
    public float GoodsSurplusIndex { get; set; }

    // =========================================================================
    // Budget state
    // =========================================================================

    /// <summary>Current city treasury balance in currency units.</summary>
    public long CityFunds { get; set; } = 50_000;

    /// <summary>Monthly income breakdown by category.</summary>
    public BudgetLedger Income { get; } = new();

    /// <summary>Monthly expense breakdown by category.</summary>
    public BudgetLedger Expenses { get; } = new();

    /// <summary>Outstanding loan balance. Positive = debt.</summary>
    public long LoanBalance { get; set; }

    /// <summary>Annual interest rate on loans (0.0-1.0).</summary>
    public float LoanInterestRate { get; set; } = 0.05f;

    /// <summary>Property tax rate (0.0-1.0). Applied monthly to land values.</summary>
    public float PropertyTaxRate { get; set; } = 0.09f;

    /// <summary>Commercial tax rate (0.0-1.0).</summary>
    public float CommercialTaxRate { get; set; } = 0.10f;

    /// <summary>Industrial tax rate (0.0-1.0).</summary>
    public float IndustrialTaxRate { get; set; } = 0.12f;

    // =========================================================================
    // Research / technology state
    // =========================================================================

    /// <summary>Accumulated research points.</summary>
    public float ResearchPoints { get; set; }

    /// <summary>Monthly research point generation rate.</summary>
    public float ResearchRate { get; set; }

    /// <summary>
    /// Unlocked technology IDs. Packed bitfield -- each bit represents one tech.
    /// Supports up to 256 technologies (4 * 64 bits).
    /// </summary>
    public ulong[] UnlockedTech { get; } = new ulong[4];

    /// <summary>Currently researching technology ID. -1 = idle.</summary>
    public int CurrentResearchId { get; set; } = -1;

    /// <summary>Progress toward current research (0.0-1.0).</summary>
    public float CurrentResearchProgress { get; set; }

    // =========================================================================
    // Political state
    // =========================================================================

    /// <summary>Mayor approval rating (0.0 = impeachment, 1.0 = beloved).</summary>
    public float ApprovalRating { get; set; } = 0.6f;

    /// <summary>
    /// City council seats. Each seat has a faction alignment.
    /// Index = seat number, value = faction ID (0=neutral, 1=green, 2=business, 3=labor, 4=conservative).
    /// </summary>
    public byte[] CouncilSeats { get; } = new byte[9];

    /// <summary>Next election year. Elections occur every 4 years.</summary>
    public int NextElectionYear { get; set; } = 2028;

    /// <summary>Active ordinances as a bitfield. Each bit = one ordinance type.</summary>
    public ulong ActiveOrdinances { get; set; }

    // =========================================================================
    // Cultural DNA (8 dimensions, each -1.0 to +1.0)
    // These drift over time based on city composition, events, and policies.
    // =========================================================================

    /// <summary>
    /// 8-dimensional cultural identity of the city.
    /// [0] Tradition vs Innovation (-1 = deeply traditional, +1 = cutting-edge)
    /// [1] Collectivism vs Individualism (-1 = communal, +1 = libertarian)
    /// [2] Industry vs Environment (-1 = heavy industry, +1 = green paradise)
    /// [3] Isolation vs Cosmopolitan (-1 = insular, +1 = melting pot)
    /// [4] Austerity vs Luxury (-1 = frugal, +1 = opulent)
    /// [5] Order vs Freedom (-1 = authoritarian, +1 = anarchic)
    /// [6] Sacred vs Secular (-1 = devout, +1 = rationalist)
    /// [7] Martial vs Peaceful (-1 = militaristic, +1 = pacifist)
    /// </summary>
    public float[] CulturalDna { get; } = new float[8];

    // =========================================================================
    // Weather state
    // =========================================================================

    /// <summary>Current weather condition. 0=clear, 1=cloudy, 2=rain, 3=storm, 4=snow, 5=fog, 6=heatwave, 7=blizzard.</summary>
    public int WeatherCondition { get; set; }

    /// <summary>Current ambient temperature in Celsius.</summary>
    public float AmbientTemperature { get; set; } = 20f;

    /// <summary>Wind speed in m/s (affects pollution spread, fire spread, turbine output).</summary>
    public float WindSpeed { get; set; } = 3f;

    /// <summary>Wind direction in radians (0 = north, clockwise).</summary>
    public float WindDirection { get; set; }

    /// <summary>Current precipitation intensity (0.0 = dry, 1.0 = torrential).</summary>
    public float Precipitation { get; set; }

    /// <summary>Hours remaining for the current weather condition. Weather changes when this hits 0.</summary>
    public float WeatherDurationHours { get; set; } = 24f;

    /// <summary>Current season: 0=spring, 1=summer, 2=autumn, 3=winter. Derived from month.</summary>
    public int Season => Month switch
    {
        >= 3 and <= 5 => 0,  // Spring
        >= 6 and <= 8 => 1,  // Summer
        >= 9 and <= 11 => 2, // Autumn
        _ => 3               // Winter
    };

    // =========================================================================
    // Active events
    // =========================================================================

    /// <summary>
    /// Currently active random/scripted events (disasters, festivals, elections, etc.).
    /// Events are added by the event system and removed when their duration expires.
    /// Max 16 concurrent events to prevent runaway stacking.
    /// </summary>
    public ActiveEvent[] Events { get; } = new ActiveEvent[16];

    /// <summary>Number of currently active events.</summary>
    public int ActiveEventCount { get; set; }

    // =========================================================================
    // Constructor
    // =========================================================================

    public WorldState(int worldSize, int maxHouseholds = 65536, int maxBuildings = 16384,
                      int maxRoadNodes = 32768, int maxVehicles = 8192)
    {
        Tiles = new TileData(worldSize);
        Households = new HouseholdData(maxHouseholds);
        Buildings = new BuildingData(maxBuildings);
        Roads = new RoadGraph(maxRoadNodes);
        Vehicles = new VehicleData(maxVehicles);

        UpdateDateString();
    }

    // =========================================================================
    // Clock operations
    // =========================================================================

    /// <summary>
    /// Advance the calendar by one day. Returns true if a new month started.
    /// </summary>
    public bool AdvanceDay()
    {
        Day++;
        bool newMonth = false;
        if (Day > 30)
        {
            Day = 1;
            Month++;
            newMonth = true;
            if (Month > 12)
            {
                Month = 1;
                Year++;
            }
        }
        UpdateDateString();
        return newMonth;
    }

    private void UpdateDateString()
    {
        DateString = $"{Year}-{Month:D2}-{Day:D2}";
    }

    // =========================================================================
    // Technology helpers
    // =========================================================================

    /// <summary>Check if a technology is unlocked. Tech IDs 0-255.</summary>
    public bool IsTechUnlocked(int techId)
    {
        int arrayIndex = techId >> 6;     // / 64
        int bitIndex = techId & 0x3F;     // % 64
        return (UnlockedTech[arrayIndex] & (1UL << bitIndex)) != 0;
    }

    /// <summary>Unlock a technology.</summary>
    public void UnlockTech(int techId)
    {
        int arrayIndex = techId >> 6;
        int bitIndex = techId & 0x3F;
        UnlockedTech[arrayIndex] |= (1UL << bitIndex);
    }

    // =========================================================================
    // Ordinance helpers
    // =========================================================================

    /// <summary>Check if an ordinance is active. Ordinance IDs 0-63.</summary>
    public bool IsOrdinanceActive(int ordinanceId) =>
        (ActiveOrdinances & (1UL << ordinanceId)) != 0;

    /// <summary>Toggle an ordinance on or off.</summary>
    public void ToggleOrdinance(int ordinanceId)
    {
        ActiveOrdinances ^= (1UL << ordinanceId);
    }

    // =========================================================================
    // Event management
    // =========================================================================

    /// <summary>
    /// Add an active event. Returns false if the event array is full.
    /// </summary>
    public bool AddEvent(int eventId, float durationDays, float severity)
    {
        if (ActiveEventCount >= Events.Length) return false;

        Events[ActiveEventCount] = new ActiveEvent
        {
            EventId = eventId,
            RemainingDays = durationDays,
            Severity = severity,
            StartDay = Day,
            StartMonth = Month,
            StartYear = Year,
        };
        ActiveEventCount++;
        return true;
    }

    /// <summary>
    /// Remove an event by swapping with the last element (O(1) removal).
    /// </summary>
    public void RemoveEvent(int index)
    {
        if (index < 0 || index >= ActiveEventCount) return;
        ActiveEventCount--;
        if (index < ActiveEventCount)
        {
            Events[index] = Events[ActiveEventCount];
        }
        Events[ActiveEventCount] = default;
    }

    /// <summary>
    /// Tick all active events, decrementing their remaining duration.
    /// Removes expired events. Call once per game day.
    /// </summary>
    public void TickEvents()
    {
        for (int i = ActiveEventCount - 1; i >= 0; i--)
        {
            Events[i].RemainingDays -= 1f;
            if (Events[i].RemainingDays <= 0f)
            {
                RemoveEvent(i);
            }
        }
    }

    // =========================================================================
    // Memory diagnostics
    // =========================================================================

    /// <summary>Total memory usage report for all pools.</summary>
    public string MemoryReport()
    {
        long tileBytes = Tiles.ComputeMemoryBytes();
        long householdBytes = (long)Households.Capacity * 30;  // Approximate per-slot
        long buildingBytes = (long)Buildings.Capacity * 40;
        long vehicleBytes = (long)Vehicles.Capacity * 44;
        long total = tileBytes + householdBytes + buildingBytes + vehicleBytes;
        double mb = total / (1024.0 * 1024.0);

        return $"WorldState memory: {mb:F2} MB total\n" +
               $"  {Tiles.MemoryReport()}\n" +
               $"  Households: {Households.Capacity:N0} slots\n" +
               $"  Buildings: {Buildings.Capacity:N0} slots\n" +
               $"  Vehicles: {Vehicles.Capacity:N0} slots";
    }
}

// =========================================================================
// Supporting types (co-located for discoverability)
// =========================================================================

/// <summary>
/// Monthly income or expense breakdown by category.
/// Each field is the amount accumulated for the current month.
/// Reset at the start of each month after recording to history.
/// </summary>
public sealed class BudgetLedger
{
    public long ResidentialTax { get; set; }
    public long CommercialTax { get; set; }
    public long IndustrialTax { get; set; }
    public long TransportFees { get; set; }
    public long ServiceFees { get; set; }
    public long UtilityFees { get; set; }
    public long LoanPayments { get; set; }
    public long PoliceExpense { get; set; }
    public long FireExpense { get; set; }
    public long HealthExpense { get; set; }
    public long EducationExpense { get; set; }
    public long TransportExpense { get; set; }
    public long UtilityExpense { get; set; }
    public long InfrastructureMaintenance { get; set; }
    public long Miscellaneous { get; set; }

    /// <summary>Sum of all entries.</summary>
    public long Total =>
        ResidentialTax + CommercialTax + IndustrialTax + TransportFees +
        ServiceFees + UtilityFees + LoanPayments +
        PoliceExpense + FireExpense + HealthExpense + EducationExpense +
        TransportExpense + UtilityExpense + InfrastructureMaintenance + Miscellaneous;

    /// <summary>Reset all entries to zero (called at month start).</summary>
    public void Reset()
    {
        ResidentialTax = 0;
        CommercialTax = 0;
        IndustrialTax = 0;
        TransportFees = 0;
        ServiceFees = 0;
        UtilityFees = 0;
        LoanPayments = 0;
        PoliceExpense = 0;
        FireExpense = 0;
        HealthExpense = 0;
        EducationExpense = 0;
        TransportExpense = 0;
        UtilityExpense = 0;
        InfrastructureMaintenance = 0;
        Miscellaneous = 0;
    }
}

/// <summary>
/// An active event affecting the city (disaster, festival, policy effect, etc.).
/// </summary>
public struct ActiveEvent
{
    /// <summary>Event definition ID, referencing event data tables.</summary>
    public int EventId;

    /// <summary>Remaining duration in game days. Removed when <= 0.</summary>
    public float RemainingDays;

    /// <summary>Severity/intensity multiplier (0.0-1.0). Affects magnitude of event impact.</summary>
    public float Severity;

    /// <summary>Day event started (for display/history).</summary>
    public int StartDay;

    /// <summary>Month event started.</summary>
    public int StartMonth;

    /// <summary>Year event started.</summary>
    public int StartYear;
}
