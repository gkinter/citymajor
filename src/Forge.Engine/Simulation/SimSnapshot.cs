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

    /// <summary>Working-age employment rate (0–1).</summary>
    public float EmploymentRate { get; init; }

    /// <summary>Net trade balance last month (exports − imports).</summary>
    public float TradeBalance { get; init; }

    /// <summary>Export value last month.</summary>
    public float MonthlyExportValue { get; init; }

    /// <summary>Import cost last month.</summary>
    public float MonthlyImportCost { get; init; }

    /// <summary>Mean tile traffic density snapshot.</summary>
    public float MeanTrafficDensity { get; init; }

    /// <summary>Top shortage goods (good id + demand−supply score), up to 5.</summary>
    public GoodImbalanceSnapshot[] ShortageGoods { get; init; } = [];

    /// <summary>Top surplus goods (good id + supply−demand score), up to 5.</summary>
    public GoodImbalanceSnapshot[] SurplusGoods { get; init; } = [];

    /// <summary>City-wide shortage pressure (0–1).</summary>
    public float GoodsShortageIndex { get; init; }

    /// <summary>City-wide surplus pressure (0–1).</summary>
    public float GoodsSurplusIndex { get; init; }

    /// <summary>Daily inter-zone goods volume (units moved across market zones).</summary>
    public float InterZoneTradeVolume { get; init; }

    /// <summary>Weighted mean trade friction for executed inter-zone transfers (≥1.0).</summary>
    public float MeanInterZoneFriction { get; init; }

    /// <summary>Composite 0–1 goods transport cost pressure (friction + congestion).</summary>
    public float GoodsTransportCostIndex { get; init; }

    /// <summary>Cathedral PROGRAM P3.6 — mean goods delivery delay (0 free-flow … 1 congested).</summary>
    public float MeanGoodsDeliveryDelay { get; init; }

    /// <summary>Cathedral P3.5 — active bilateral trade routes (PartnerCityId ≥ 0).</summary>
    public int BilateralRouteCount { get; init; }

    /// <summary>Cathedral P3.5 — monthly bilateral contract notional (Σ |qty| × price).</summary>
    public float BilateralTradeValue { get; init; }

    /// <summary>Cathedral P3.5 — mean freight lag in months across bilateral routes.</summary>
    public float MeanFreightMonths { get; init; }

    /// <summary>SB-3728 — NPC towns on the regional map catalog.</summary>
    public int NpcPartnerCount { get; init; }

    /// <summary>Active Leontief market partitions (1–16).</summary>
    public int MarketZoneCount { get; init; } = 1;

    /// <summary>Mean household rent burden (rent / income).</summary>
    public float MeanRentBurden { get; init; }

    /// <summary>City-wide residential vacancy proxy (0–1).</summary>
    public float ResidentialVacancy { get; init; } = 1f;

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

    /// <summary>Player-enabled ordinances (mirrors LawSystem.ActiveLawCount).</summary>
    public int ActiveLawCount { get; init; }

    /// <summary>Traffic edge capacity multiplier from active traffic ordinances.</summary>
    public float LawTrafficCapacityMult { get; init; } = 1f;

    /// <summary>Construction duration multiplier from active ordinances.</summary>
    public float LawConstructionSpeedMult { get; init; } = 1f;

    /// <summary>Baseline spawn demand multiplier from construction_cost ordinances.</summary>
    public float LawSpawnDemandMult { get; init; } = 1f;

    /// <summary>Residential zone spawn multiplier (housing_supply / housing_density).</summary>
    public float LawResidentialSpawnMult { get; init; } = 1f;

    /// <summary>Industrial zone spawn multiplier (industrial_output / operating cost).</summary>
    public float LawIndustrialSpawnMult { get; init; } = 1f;

    /// <summary>Commercial / office zone spawn multiplier (accessibility / diversity).</summary>
    public float LawCommercialSpawnMult { get; init; } = 1f;

    /// <summary>Cathedral P6.2 — tax revenue multiplier from active events.</summary>
    public float EventTaxRevenueMult { get; init; } = 1f;

    /// <summary>Cathedral P6.2 — immigration multiplier from active events.</summary>
    public float EventImmigrationMult { get; init; } = 1f;

    /// <summary>Cathedral P6.2 — commercial spawn multiplier from active events.</summary>
    public float EventCommercialSpawnMult { get; init; } = 1f;

    /// <summary>Cathedral P6.2 — productivity / industrial spawn multiplier from active events.</summary>
    public float EventProductivityMult { get; init; } = 1f;

    /// <summary>Cathedral P6.2 — research rate multiplier from active events.</summary>
    public float EventResearchMult { get; init; } = 1f;

    /// <summary>Cathedral P6.2 — baseline spawn demand multiplier from active events.</summary>
    public float EventSpawnDemandMult { get; init; } = 1f;

    /// <summary>Buildings currently under construction.</summary>
    public int ConstructingBuildingCount { get; init; }

    /// <summary>Buildings currently abandoned (Cathedral P2.6).</summary>
    public int AbandonedBuildingCount { get; init; }

    /// <summary>Fraction of partitions with power supply ≥ demand (0–1).</summary>
    public float PowerCoverageFraction { get; init; } = 1f;

    /// <summary>Fraction of partitions with water supply ≥ demand (0–1).</summary>
    public float WaterCoverageFraction { get; init; } = 1f;

    /// <summary>Rolling fraction of partitions in power deficit (0–1).</summary>
    public float BlackoutFraction { get; init; }

    /// <summary>Rolling fraction of partitions in water deficit (0–1).</summary>
    public float WaterShortageFraction { get; init; }

    /// <summary>Composite utility stress (0 = healthy, 1 = severe shortage).</summary>
    public float UtilityStressIndex { get; init; }

    /// <summary>
    /// Mean fire/EMS response minutes over sampled zoned tiles (road distance + traffic).
    /// Default matches <c>EmergencyResponseTime.NoStationResponseMinutes</c>.
    /// </summary>
    public float MeanEmergencyResponseMinutes { get; init; } = 30f;

    /// <summary>Fraction of sampled zoned buildings with hydrant coverage (Cathedral P5.3).</summary>
    public float HydrantCoverageFraction { get; init; } = 1f;

    /// <summary>Buildings currently burning (Cathedral P5.3).</summary>
    public int ActiveFireCount { get; init; }

    /// <summary>Mean EMS survival rate 0–1 from response-minute curve (Cathedral P5.4).</summary>
    public float MeanEmsSurvivalRate { get; init; } = 0.40f;

    /// <summary>Hospital bed occupancy 0–1 (Tier-2 / MISSING_SYSTEMS §1.2).</summary>
    public float HospitalBedOccupancyFraction { get; init; }

    /// <summary>Free hospital beds city-wide (Tier-2 EMS diversion).</summary>
    public int AvailableHospitalBeds { get; init; }

    /// <summary>Drought / wildfire risk 0–1 (Tier-2 / MISSING_SYSTEMS §1.1).</summary>
    public float WildfireRiskIndex { get; init; }

    /// <summary>Forest / park / ag tiles currently burning (Tier-2 wildfire).</summary>
    public int ActiveWildfireTileCount { get; init; }

    /// <summary>Mean arson risk from crime excess 0–1 (Tier-2).</summary>
    public float ArsonRiskIndex { get; init; }

    /// <summary>High-crime building-fire cluster / arson ring flag (Tier-2).</summary>
    public bool ArsonRingActive { get; init; }

    /// <summary>Active lookout towers (Tier-2 / MISSING_SYSTEMS §1.1).</summary>
    public int LookoutTowerCount { get; init; }

    /// <summary>Aerial firefighting / water-bomber base available (Tier-2).</summary>
    public bool AerialFirefightingAvailable { get; init; }

    /// <summary>City fire safety rating 1–10 (Tier-2 fire rating → insurance).</summary>
    public byte FireSafetyRating { get; init; } = 5;

    /// <summary>Insurance premium mult from fire safety rating (Tier-2).</summary>
    public float FireInsurancePremiumMult { get; init; } = 1f;

    /// <summary>Mean household education level 0–3 (Tier-2 education depth).</summary>
    public float MeanEducationLevel { get; init; }

    /// <summary>Fraction of households with school coverage (Tier-2 education depth).</summary>
    public float EducationCoverageFraction { get; init; }

    /// <summary>Mean household park / exercise access 0–1 (Tier-2 park amenity).</summary>
    public float MeanParkAccess { get; init; }

    /// <summary>Fraction of households with park access (Tier-2 park amenity).</summary>
    public float ParkAccessFraction { get; init; }

    /// <summary>Fraction of households with hospital coverage (Tier-2 health progression).</summary>
    public float HealthCoverageFraction { get; init; }

    /// <summary>Mean household HealthSatisfaction 0–1 (Tier-2 hospital + park health).</summary>
    public float MeanHealthSatisfaction { get; init; }

    /// <summary>Fraction of households with police coverage (Tier-2 police/crime).</summary>
    public float PoliceCoverageFraction { get; init; }

    /// <summary>Mean tile crime at household homes 0–1 (Tier-2 police/crime).</summary>
    public float MeanCrimeRate { get; init; }

    /// <summary>Mean household SafetySatisfaction 0–1 (Tier-2 police → crime → safety).</summary>
    public float MeanSafetySatisfaction { get; init; }

    /// <summary>Park attraction units for tourism (Tier-2).</summary>
    public int ParkAttractionCount { get; init; }

    /// <summary>Landmark / monument attraction units for tourism (Tier-2).</summary>
    public int LandmarkAttractionCount { get; init; }

    /// <summary>Park + landmark attraction units (Tier-2).</summary>
    public int TourismAttractionCount { get; init; }

    /// <summary>Monthly tourism income including attractions (Tier-2).</summary>
    public float TourismIncome { get; init; }

    public readonly record struct BuildingSnapshot(
        int GridX, int GridY, ushort TypeId, byte Level,
        byte State, ushort Occupants, ushort MaxOccupants, byte Condition);

    public readonly record struct VehicleSnapshot(
        float WorldX, float WorldY, ushort TypeId, float Heading,
        float Speed, float MaxSpeed, byte Flags);

    public readonly record struct GoodImbalanceSnapshot(byte GoodId, float Score);

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

        var shortageGoods = new GoodImbalanceSnapshot[state.TopShortageCount];
        for (int i = 0; i < state.TopShortageCount; i++)
        {
            shortageGoods[i] = new GoodImbalanceSnapshot(
                state.TopShortageGoodIds[i],
                state.TopShortageScores[i]);
        }

        var surplusGoods = new GoodImbalanceSnapshot[state.TopSurplusCount];
        for (int i = 0; i < state.TopSurplusCount; i++)
        {
            surplusGoods[i] = new GoodImbalanceSnapshot(
                state.TopSurplusGoodIds[i],
                state.TopSurplusScores[i]);
        }

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
            EmploymentRate = state.EmploymentRate,
            TradeBalance = state.TradeBalance,
            MonthlyExportValue = state.MonthlyExportValue,
            MonthlyImportCost = state.MonthlyImportCost,
            MeanTrafficDensity = state.MeanTrafficDensity,
            ShortageGoods = shortageGoods,
            SurplusGoods = surplusGoods,
            GoodsShortageIndex = state.GoodsShortageIndex,
            GoodsSurplusIndex = state.GoodsSurplusIndex,
            InterZoneTradeVolume = state.InterZoneTradeVolume,
            MeanInterZoneFriction = state.MeanInterZoneFriction,
            GoodsTransportCostIndex = state.GoodsTransportCostIndex,
            MeanGoodsDeliveryDelay = state.MeanGoodsDeliveryDelay,
            BilateralRouteCount = state.BilateralRouteCount,
            BilateralTradeValue = state.BilateralTradeValue,
            MeanFreightMonths = state.MeanFreightMonths,
            NpcPartnerCount = state.NpcPartnerCount,
            MarketZoneCount = state.MarketZoneCount,
            MeanRentBurden = state.MeanRentBurden,
            ResidentialVacancy = state.ResidentialVacancy,
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
            // Laws
            ActiveLawCount = state.ActiveLawCount,
            LawTrafficCapacityMult = state.LawTrafficCapacityMult,
            LawConstructionSpeedMult = state.LawConstructionSpeedMult,
            LawSpawnDemandMult = state.LawSpawnDemandMult,
            LawResidentialSpawnMult = state.LawResidentialSpawnMult,
            LawIndustrialSpawnMult = state.LawIndustrialSpawnMult,
            LawCommercialSpawnMult = state.LawCommercialSpawnMult,
            EventTaxRevenueMult = state.EventTaxRevenueMult,
            EventImmigrationMult = state.EventImmigrationMult,
            EventCommercialSpawnMult = state.EventCommercialSpawnMult,
            EventProductivityMult = state.EventProductivityMult,
            EventResearchMult = state.EventResearchMult,
            EventSpawnDemandMult = state.EventSpawnDemandMult,
            ConstructingBuildingCount = state.ConstructingBuildingCount,
            AbandonedBuildingCount = state.AbandonedBuildingCount,
            PowerCoverageFraction = state.PowerCoverageFraction,
            WaterCoverageFraction = state.WaterCoverageFraction,
            BlackoutFraction = state.BlackoutFraction,
            WaterShortageFraction = state.WaterShortageFraction,
            UtilityStressIndex = state.UtilityStressIndex,
            MeanEmergencyResponseMinutes = state.MeanEmergencyResponseMinutes,
            HydrantCoverageFraction = state.HydrantCoverageFraction,
            ActiveFireCount = state.ActiveFireCount,
            MeanEmsSurvivalRate = state.MeanEmsSurvivalRate,
            HospitalBedOccupancyFraction = state.HospitalBedOccupancyFraction,
            AvailableHospitalBeds = state.AvailableHospitalBeds,
            WildfireRiskIndex = state.WildfireRiskIndex,
            ActiveWildfireTileCount = state.ActiveWildfireTileCount,
            ArsonRiskIndex = state.ArsonRiskIndex,
            ArsonRingActive = state.ArsonRingActive,
            LookoutTowerCount = state.LookoutTowerCount,
            AerialFirefightingAvailable = state.AerialFirefightingAvailable,
            FireSafetyRating = state.FireSafetyRating,
            FireInsurancePremiumMult = state.FireInsurancePremiumMult,
            MeanEducationLevel = state.MeanEducationLevel,
            EducationCoverageFraction = state.EducationCoverageFraction,
            MeanParkAccess = state.MeanParkAccess,
            ParkAccessFraction = state.ParkAccessFraction,
            HealthCoverageFraction = state.HealthCoverageFraction,
            MeanHealthSatisfaction = state.MeanHealthSatisfaction,
            PoliceCoverageFraction = state.PoliceCoverageFraction,
            MeanCrimeRate = state.MeanCrimeRate,
            MeanSafetySatisfaction = state.MeanSafetySatisfaction,
            ParkAttractionCount = state.ParkAttractionCount,
            LandmarkAttractionCount = state.LandmarkAttractionCount,
            TourismAttractionCount = state.TourismAttractionCount,
            TourismIncome = state.TourismIncome,
        };
    }
}
