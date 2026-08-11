using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;

namespace Forge.SimWasm;

public sealed class HouseholdPreviewDto
{
    public string Id { get; init; } = "";
    public int TileX { get; init; }
    public int TileZ { get; init; }
    /// <summary>0–1 satisfaction.</summary>
    public float Happiness { get; init; }
    /// <summary>Commute time in game minutes.</summary>
    public float CommuteMin { get; init; }
    public int HomeBuildingId { get; init; }
    /// <summary>Workplace building id; 0 = unemployed / no job.</summary>
    public int WorkBuildingId { get; init; }
    /// <summary>Rent / income ratio (0–1+). Cathedral P4.4.</summary>
    public float RentBurden { get; init; }
}

/// <summary>Aggregated home→work tile pair for traffic debug export.</summary>
public sealed class CommuteOdSampleDto
{
    public int HomeTileX { get; init; }
    public int HomeTileZ { get; init; }
    public int WorkTileX { get; init; }
    public int WorkTileZ { get; init; }
    public int TripCount { get; init; }
}

/// <summary>Population L2 snapshot — top 50–100 households for drill-down + map pick (P4.4).</summary>
public sealed class PopulationL2Dto
{
    public HouseholdPreviewDto[] Households { get; init; } = [];

    public static PopulationL2Dto From(WorldState state, PopulationSystem? population)
    {
        if (population is null || state.Households.Count == 0)
            return new PopulationL2Dto();

        var rows = population.CollectHouseholdSample(
            state,
            limit: PopulationSystem.DefaultL2SampleLimit);
        if (rows.Length == 0)
            return new PopulationL2Dto();

        var households = new HouseholdPreviewDto[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            households[i] = new HouseholdPreviewDto
            {
                Id = row.Id,
                TileX = row.TileX,
                TileZ = row.TileZ,
                Happiness = row.Happiness,
                CommuteMin = row.CommuteMin,
                HomeBuildingId = row.HomeBuildingId,
                WorkBuildingId = row.WorkBuildingId,
                RentBurden = row.RentBurden,
            };
        }

        return new PopulationL2Dto { Households = households };
    }
}
public sealed class SimSnapshotDto
{
    public long Tick { get; init; }
    public int Population { get; init; }
    public int HouseholdCount { get; init; }
    public long CityFunds { get; init; }
    public int Era { get; init; }
    public float ResidentialDemand { get; init; }
    public float CommercialDemand { get; init; }
    public float IndustrialDemand { get; init; }
    /// <summary>Mayor approval percent (0–100).</summary>
    public float Approval { get; init; }
    public float Happiness { get; init; }
    public long MonthlyIncome { get; init; }
    public long MonthlyExpenses { get; init; }
    /// <summary>Net trade balance this month (exports − imports). Cathedral P3.</summary>
    public float TradeBalance { get; init; }
    /// <summary>Total export value this month.</summary>
    public float MonthlyExportValue { get; init; }
    /// <summary>Total import cost this month.</summary>
    public float MonthlyImportCost { get; init; }
    public BuildingDto[] Buildings { get; init; } = [];
    public ZoneDto[] Zones { get; init; } = [];
    public RoadDto[] Roads { get; init; } = [];
    /// <summary>Segment-graph nodes — parallel arrays indexed by node id (Cathedral P1.6).</summary>
    public RoadGraphSnapshotDto RoadGraph { get; init; } = new();
    /// <summary>Sparse road tiles with congestion density (0–1).</summary>
    public TrafficDto[] Traffic { get; init; } = [];
    /// <summary>Sparse zoned tiles with per-service coverage (0–1).</summary>
    public ServiceCoverageDto[] ServiceCoverage { get; init; } = [];
    public ActiveEventDto[] ActiveEvents { get; init; } = [];
    /// <summary>Count of live EventSystem instances — Herald HUD badge (Cathedral P6).</summary>
    public int ActiveEventCount { get; init; }
    /// <summary>
    /// Cathedral P6.1 — slug ids of player-enabled ordinances (laws.json).
    /// Deeper save/load than Law*Mult scalars alone — restore via <see cref="LawSystem.SetActive"/>.
    /// </summary>
    public string[] ActiveLawIds { get; init; } = [];
    /// <summary>
    /// WorldState ordinance bitfield (legacy / politics path). Orthogonal to <see cref="ActiveLawIds"/>.
    /// </summary>
    public ulong ActiveOrdinances { get; init; }
    /// <summary>Next mayoral election calendar year (PoliticsSystem).</summary>
    public int NextElectionYear { get; init; }
    /// <summary>Cathedral P6.1 — road capacity multiplier from active ordinances.</summary>
    public float LawTrafficCapacityMult { get; init; } = 1f;
    /// <summary>Cathedral P6.1 — construction duration multiplier from active ordinances.</summary>
    public float LawConstructionSpeedMult { get; init; } = 1f;
    /// <summary>Cathedral P6.1 — baseline zone spawn demand multiplier from ordinances.</summary>
    public float LawSpawnDemandMult { get; init; } = 1f;
    /// <summary>Cathedral P6.1 — residential zone spawn multiplier from ordinances.</summary>
    public float LawResidentialSpawnMult { get; init; } = 1f;
    /// <summary>Cathedral P6.1 — industrial zone spawn multiplier from ordinances.</summary>
    public float LawIndustrialSpawnMult { get; init; } = 1f;
    /// <summary>Cathedral P6.1 — commercial / office spawn multiplier from ordinances.</summary>
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
    /// <summary>Leontief goods shortages/surpluses for economy HUD.</summary>
    public EconomySnapshotDto Economy { get; init; } = new();
    /// <summary>Top households sample for CitizenPanel L2 drill-down.</summary>
    public PopulationL2Dto PopulationL2 { get; init; } = new();
    public float ResearchPoints { get; init; }
    public int CurrentResearchId { get; init; } = -1;
    public float CurrentResearchProgress { get; init; }
    public int[] UnlockedTechIds { get; init; } = [];
    public int[] ResearchQueue { get; init; } = [];
    public float[] QueueProgress { get; init; } = [];
    public Dictionary<int, float> EurekaBonuses { get; init; } = new();
    public Dictionary<int, int> BranchingChoices { get; init; } = new();
    public int ConstructingBuildingCount { get; init; }
    /// <summary>Buildings currently abandoned (Cathedral P2.6).</summary>
    public int AbandonedBuildingCount { get; init; }
    /// <summary>Share of working-age households with a workplace (0–1).</summary>
    public float EmploymentRate { get; init; }
    public float MeanTrafficDensity { get; init; }
    public float PowerCoverageFraction { get; init; } = 1f;
    public float WaterCoverageFraction { get; init; } = 1f;
    /// <summary>Rolling fraction of utility partitions in power deficit (0–1).</summary>
    public float BlackoutFraction { get; init; }
    /// <summary>Rolling fraction of utility partitions in water deficit (0–1).</summary>
    public float WaterShortageFraction { get; init; }
    public float UtilityStressIndex { get; init; }
    public float GoodsShortageIndex { get; init; }
    public float GoodsSurplusIndex { get; init; }
    public float InterZoneTradeVolume { get; init; }
    public float MeanInterZoneFriction { get; init; } = 1f;
    /// <summary>Composite 0–1 goods transport cost (friction + congestion).</summary>
    public float GoodsTransportCostIndex { get; init; }
    /// <summary>Cathedral P3.5 — mean goods delivery delay (0 free-flow … 1 congested).</summary>
    public float MeanGoodsDeliveryDelay { get; init; }
    public float MeanRentBurden { get; init; }
    public float ResidentialVacancy { get; init; } = 1f;
    /// <summary>Mean fire/EMS response minutes over sampled zoned tiles (P5.2).</summary>
    public float MeanEmergencyResponseMinutes { get; init; } = 30f;
    /// <summary>Fraction of sampled zoned buildings with hydrant coverage (P5.3).</summary>
    public float HydrantCoverageFraction { get; init; } = 1f;
    /// <summary>Buildings currently burning (P5.3).</summary>
    public int ActiveFireCount { get; init; }
    /// <summary>Mean EMS survival rate 0–1 from response-minute curve (P5.4).</summary>
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
    /// <summary>High-crime building-fire cluster / arson ring (Tier-2).</summary>
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
    /// <summary>Mean household HealthSatisfaction 0–1 (Tier-2 park amenity).</summary>
    public float MeanHealthSatisfaction { get; init; }
    /// <summary>Active Leontief market partitions (1–16).</summary>
    public int MarketZoneCount { get; init; } = 1;
    /// <summary>Sparse market-zone boundary friction samples (0–1 heat).</summary>
    public FrictionCorridorDto[] FrictionCorridors { get; init; } = [];
    /// <summary>Share of working commuters with valid home + work building IDs (0–1).</summary>
    public float CommuterCoverage { get; init; }
    /// <summary>Top home→work tile pairs aggregated from household assignments.</summary>
    public CommuteOdSampleDto[] CommuteOdSample { get; init; } = [];
    /// <summary>Faction id per council seat (length 9).</summary>
    public int[] CouncilSeats { get; init; } = [];
    /// <summary>
    /// 8-dimensional cultural identity vector (−1…+1), politics flavor.
    /// Matches <see cref="WorldState.CulturalDna"/>; clone on export / copy on restore.
    /// </summary>
    public float[] CulturalDna { get; init; } = [];
    /// <summary>City-wide car mode share from traffic assignment (0–1).</summary>
    public float CarModeShare { get; init; }
    /// <summary>City-wide transit mode share from traffic assignment (0–1).</summary>
    public float TransitModeShare { get; init; }
    /// <summary>City-wide walk mode share from traffic assignment (0–1).</summary>
    public float WalkModeShare { get; init; }
    /// <summary>Transit route count (0 until a transit graph exists).</summary>
    public int TransitLineCount { get; init; }
    /// <summary>Bus/transit coverage proxy (0–1), derived from line count.</summary>
    public float BusCoverage { get; init; }

    public static SimSnapshotDto From(
        SimSnapshot snap,
        WorldState state,
        EventSystem? events = null,
        EconomySystem? economy = null,
        ServiceSystem? services = null,
        PopulationSystem? population = null,
        ResearchSystem? research = null,
        LawSystem? laws = null,
        float[]? edgeVolumes = null,
        float[]? edgeTravelTimes = null,
        float carModeShare = 0f,
        float transitModeShare = 0f,
        float walkModeShare = 0f)
    {
        var buildings = CollectBuildings(state);
        var zones = CollectZones(state);
        var roads = CollectRoads(state);
        var roadGraph = RoadGraphSnapshotDto.From(state.Roads, edgeVolumes, edgeTravelTimes);
        var traffic = CollectTraffic(state);
        var serviceCoverage = services is null ? [] : CollectServiceCoverage(state, services);
        var frictionCorridors = CollectFrictionCorridors(state, economy);
        var commuterAudit = population?.AuditCommuters(state) ?? default;
        var commuteOdSample = population?.CollectCommuteOdSample(state, limit: 16) ?? [];

        return new SimSnapshotDto
        {
            Tick = snap.TickCount,
            Population = state.Population,
            HouseholdCount = state.Households.Count,
            CityFunds = state.CityFunds,
            Era = state.Era,
            ResidentialDemand = economy?.ResidentialDemand ?? 0f,
            CommercialDemand = economy?.CommercialDemand ?? 0f,
            IndustrialDemand = economy?.IndustrialDemand ?? 0f,
            Approval = state.ApprovalRating * 100f,
            Happiness = state.Happiness,
            MonthlyIncome = state.Income.Total,
            MonthlyExpenses = state.Expenses.Total,
            TradeBalance = state.TradeBalance,
            MonthlyExportValue = state.MonthlyExportValue,
            MonthlyImportCost = state.MonthlyImportCost,
            Buildings = buildings,
            Zones = zones,
            Roads = roads,
            RoadGraph = roadGraph,
            Traffic = traffic,
            ServiceCoverage = serviceCoverage,
            FrictionCorridors = frictionCorridors,
            ActiveEvents = events is null ? [] : CollectActiveEvents(events),
            ActiveEventCount = events?.ActiveEventCount ?? 0,
            ActiveLawIds = laws?.CollectActiveIds() ?? [],
            ActiveOrdinances = state.ActiveOrdinances,
            NextElectionYear = state.NextElectionYear,
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
            Economy = EconomySnapshotDto.From(economy),
            PopulationL2 = PopulationL2Dto.From(state, population),
            ResearchPoints = state.ResearchPoints,
            CurrentResearchId = state.CurrentResearchId,
            CurrentResearchProgress = state.CurrentResearchProgress,
            UnlockedTechIds = CollectUnlockedTechIds(state),
            ResearchQueue = research is null ? [] : (int[])research.ResearchQueue.Clone(),
            QueueProgress = research is null ? [] : (float[])research.QueueProgress.Clone(),
            EurekaBonuses = research is null ? new() : new Dictionary<int, float>(research.EurekaBonuses),
            BranchingChoices = research is null ? new() : new Dictionary<int, int>(research.BranchingChoices),
            ConstructingBuildingCount = state.ConstructingBuildingCount,
            AbandonedBuildingCount = state.AbandonedBuildingCount,
            EmploymentRate = state.EmploymentRate,
            MeanTrafficDensity = state.MeanTrafficDensity,
            PowerCoverageFraction = state.PowerCoverageFraction,
            WaterCoverageFraction = state.WaterCoverageFraction,
            BlackoutFraction = state.BlackoutFraction,
            WaterShortageFraction = state.WaterShortageFraction,
            UtilityStressIndex = state.UtilityStressIndex,
            GoodsShortageIndex = state.GoodsShortageIndex,
            GoodsSurplusIndex = state.GoodsSurplusIndex,
            InterZoneTradeVolume = state.InterZoneTradeVolume,
            MeanInterZoneFriction = state.MeanInterZoneFriction,
            GoodsTransportCostIndex = state.GoodsTransportCostIndex,
            MeanGoodsDeliveryDelay = state.MeanGoodsDeliveryDelay,
            MeanRentBurden = state.MeanRentBurden,
            ResidentialVacancy = state.ResidentialVacancy,
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
            MeanHealthSatisfaction = state.MeanHealthSatisfaction,
            MarketZoneCount = state.MarketZoneCount,
            CommuterCoverage = commuterAudit.Coverage,
            CommuteOdSample = ToCommuteOdSampleDtos(commuteOdSample),
            CouncilSeats = CollectCouncilSeats(state),
            CulturalDna = CollectCulturalDna(state),
            CarModeShare = carModeShare,
            TransitModeShare = transitModeShare,
            WalkModeShare = walkModeShare,
            TransitLineCount = state.TransitLineCount,
            BusCoverage = state.BusCoverage,
        };
    }

    private static int[] CollectCouncilSeats(WorldState state)
    {
        var src = state.CouncilSeats;
        var seats = new int[src.Length];
        for (int i = 0; i < src.Length; i++)
            seats[i] = src[i];
        return seats;
    }

    private static float[] CollectCulturalDna(WorldState state)
    {
        var src = state.CulturalDna;
        if (src is not { Length: > 0 })
            return [];
        return (float[])src.Clone();
    }

    private static CommuteOdSampleDto[] ToCommuteOdSampleDtos(
        PopulationSystem.CommuteOdSampleRow[] rows)
    {
        if (rows.Length == 0) return [];
        var result = new CommuteOdSampleDto[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            result[i] = new CommuteOdSampleDto
            {
                HomeTileX = row.HomeTileX,
                HomeTileZ = row.HomeTileZ,
                WorkTileX = row.WorkTileX,
                WorkTileZ = row.WorkTileZ,
                TripCount = row.TripCount,
            };
        }

        return result;
    }

    private static int[] CollectUnlockedTechIds(WorldState state)
    {
        var ids = new List<int>();
        for (int i = 0; i < ResearchSystem.MaxTechnologies; i++)
        {
            if (state.IsTechUnlocked(i)) ids.Add(i);
        }

        return ids.ToArray();
    }

    private static ActiveEventDto[] CollectActiveEvents(EventSystem events)
    {
        var active = events.ActiveEvents;
        var result = new ActiveEventDto[active.Count];
        for (int i = 0; i < active.Count; i++)
        {
            var e = active[i];
            result[i] = new ActiveEventDto
            {
                EventId = e.EventId,
                TypeId = e.TypeId,
                Phase = e.Phase.ToString().ToLowerInvariant(),
                Severity = e.Severity,
                TileX = e.TileX,
                TileY = e.TileY,
            };
        }

        return result;
    }

    /// <summary>
    /// Walk allocated building pool slots — indices are sparse, not 0..Count-1.
    /// SimSnapshot.CaptureFrom still packs by Count; WASM JSON uses pool slots directly.
    /// </summary>
    private static BuildingDto[] CollectBuildings(WorldState state)
    {
        var pool = state.Buildings;
        var list = new List<BuildingDto>(pool.Count);
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (!pool.IsActive(i)) continue;
            list.Add(new BuildingDto
            {
                Id = i,
                TypeId = pool.TypeId[i],
                TileX = pool.GridX[i],
                TileZ = pool.GridY[i],
                Level = pool.Level[i],
                State = pool.State[i],
                Condition = pool.Condition[i],
                FireRisk = pool.FireRisk[i],
                ServiceFlags = pool.ServiceFlags[i],
            });
        }
        return list.ToArray();
    }

    private static ZoneDto[] CollectZones(WorldState state)
    {
        var tiles = state.Tiles;
        var list = new List<ZoneDto>(256);
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            byte zoneType = tiles.ZoneType[idx];
            if (zoneType == 0) continue;
            list.Add(new ZoneDto { TileX = x, TileZ = y, ZoneType = zoneType });
        }
        return list.ToArray();
    }

    private static RoadDto[] CollectRoads(WorldState state)
    {
        var tiles = state.Tiles;
        var list = new List<RoadDto>(512);
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            byte roadFlags = tiles.RoadFlags[idx];
            if (roadFlags == 0) continue;
            list.Add(new RoadDto { TileX = x, TileZ = y, RoadFlags = roadFlags });
        }
        return list.ToArray();
    }

    private static TrafficDto[] CollectTraffic(WorldState state)
    {
        const float MinDensity = 0.01f;
        var tiles = state.Tiles;
        var list = new List<TrafficDto>(256);
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            float density = tiles.Traffic[idx];
            if (density < MinDensity) continue;
            list.Add(new TrafficDto { TileX = x, TileZ = y, Density = density });
        }
        return list.ToArray();
    }

    private static ServiceCoverageDto[] CollectServiceCoverage(WorldState state, ServiceSystem services)
    {
        var tiles = state.Tiles;
        var health = services.HealthCoverage;
        var police = services.PoliceCoverage;
        var fire = services.FireCoverage;
        var education = services.EducationCoverage;
        var list = new List<ServiceCoverageDto>(512);
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            if (tiles.ZoneType[idx] == 0) continue;

            float h = Math.Clamp(health.GetValue(x, y), 0f, 1f);
            float p = Math.Clamp(police.GetValue(x, y), 0f, 1f);
            float f = Math.Clamp(fire.GetValue(x, y), 0f, 1f);
            float e = Math.Clamp(education.GetValue(x, y), 0f, 1f);
            list.Add(new ServiceCoverageDto
            {
                TileX = x,
                TileZ = y,
                Health = h,
                Police = p,
                Fire = f,
                Education = e,
            });
        }

        return list.ToArray();
    }

    private static FrictionCorridorDto[] CollectFrictionCorridors(
        WorldState state, EconomySystem? economy)
    {
        if (economy is null || economy.ActiveZoneCount <= 1) return [];

        var samples = economy.CollectFrictionCorridors(
            state.Tiles.Size,
            state.MeanInterZoneFriction,
            state.Tiles.Traffic,
            stride: 2);

        if (samples.Length == 0) return [];

        var list = new FrictionCorridorDto[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            list[i] = new FrictionCorridorDto
            {
                TileX = samples[i].TileX,
                TileZ = samples[i].TileZ,
                Friction = samples[i].Friction,
            };
        }

        return list;
    }

}

public sealed class BuildingDto
{
    public int Id { get; init; }
    public ushort TypeId { get; init; }
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public byte Level { get; init; }
    public byte State { get; init; }
    public byte Condition { get; init; }
    /// <summary>Burning intensity (0 = not on fire). Cathedral P5.3 save/load.</summary>
    public byte FireRisk { get; init; }
    /// <summary>Service bitmask (hydrants, stations, utilities). Cathedral P5 save/load.</summary>
    public uint ServiceFlags { get; init; }
}

public sealed class ZoneDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public byte ZoneType { get; init; }
}

public sealed class RoadDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public byte RoadFlags { get; init; }
}

/// <summary>
/// Compact segment-graph export — <see cref="NodeTypes"/>[i] is <see cref="RoadNodeType"/> for node i.
/// <see cref="EdgeVolumes"/>, <see cref="TravelTimes"/>, <see cref="EdgeFrom"/>, and <see cref="EdgeTo"/>
/// are parallel arrays indexed by graph edge id (P1.6 / P4.2) in CSR neighbor order.
/// </summary>
public sealed class RoadGraphSnapshotDto
{
    public int NodeCount { get; init; }
    public byte[] NodeTypes { get; init; } = [];
    public int[] NodeTileX { get; init; } = [];
    public int[] NodeTileZ { get; init; } = [];
    public int EdgeCount { get; init; }
    /// <summary>Source node id per edge (CSR order — matches traffic lite edge tables).</summary>
    public int[] EdgeFrom { get; init; } = [];
    /// <summary>Target node id per edge.</summary>
    public int[] EdgeTo { get; init; } = [];
    public float[] EdgeVolumes { get; init; } = [];
    public float[] TravelTimes { get; init; } = [];

    public static RoadGraphSnapshotDto From(
        RoadGraph graph,
        float[]? edgeVolumes = null,
        float[]? travelTimes = null)
    {
        int n = graph.NodeCount;
        int edgeCount = graph.EdgeCount;
        if (n == 0 && edgeCount == 0) return new RoadGraphSnapshotDto();

        var types = new byte[n];
        var xs = new int[n];
        var zs = new int[n];
        for (int i = 0; i < n; i++)
        {
            var (x, z) = graph.GetNodePosition(i);
            xs[i] = x;
            zs[i] = z;
            types[i] = (byte)graph.GetNodeType(i);
        }

        var edgeFrom = new int[edgeCount];
        var edgeTo = new int[edgeCount];
        int edgeIdx = 0;
        for (int node = 0; node < n; node++)
        {
            foreach (var (target, _, _) in graph.GetNeighbors(node))
            {
                if (edgeIdx >= edgeCount) break;
                edgeFrom[edgeIdx] = node;
                edgeTo[edgeIdx] = target;
                edgeIdx++;
            }
        }

        return new RoadGraphSnapshotDto
        {
            NodeCount = n,
            NodeTypes = types,
            NodeTileX = xs,
            NodeTileZ = zs,
            EdgeCount = edgeCount,
            EdgeFrom = edgeFrom,
            EdgeTo = edgeTo,
            EdgeVolumes = NormalizeEdgeArray(edgeVolumes, edgeCount),
            TravelTimes = NormalizeEdgeArray(travelTimes, edgeCount),
        };
    }

    private static float[] NormalizeEdgeArray(float[]? values, int edgeCount)
    {
        if (edgeCount <= 0) return [];
        if (values is null || values.Length == 0) return new float[edgeCount];
        if (values.Length == edgeCount) return values;
        var normalized = new float[edgeCount];
        Array.Copy(values, normalized, Math.Min(values.Length, edgeCount));
        return normalized;
    }
}

public sealed class TrafficDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public float Density { get; init; }
}

/// <summary>Sparse market-zone boundary friction heat for P3.4 overlay.</summary>
public sealed class FrictionCorridorDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    /// <summary>Normalized corridor friction (0–1).</summary>
    public float Friction { get; init; }
}

public sealed class ServiceCoverageDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public float Health { get; init; }
    public float Police { get; init; }
    public float Fire { get; init; }
    public float Education { get; init; }
}

public sealed class UtilityCoverageDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public float Power { get; init; }
    public float Water { get; init; }
}

public sealed class ActiveEventDto
{
    public int EventId { get; init; }
    public string TypeId { get; init; } = "";
    public string Phase { get; init; } = "";
    public float Severity { get; init; }
    public int TileX { get; init; }
    public int TileY { get; init; }
}

public sealed class LawPreviewDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Active { get; init; }
}

public sealed class GoodImbalanceDto
{
    /// <summary>Good enum byte (0–44).</summary>
    public byte GoodId { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Absolute demand−supply (shortage) or supply−demand (surplus).</summary>
    public float Magnitude { get; init; }
    /// <summary>City-wide mean price across active market zones.</summary>
    public float Price { get; init; }
}

/// <summary>Production vs demand for a top-activity good (P3.2 production-chain HUD).</summary>
public sealed class GoodFlowDto
{
    public byte GoodId { get; init; }
    public string Name { get; init; } = "";
    /// <summary>City-wide supply (production) units this day.</summary>
    public float Production { get; init; }
    /// <summary>City-wide demand units this day.</summary>
    public float Demand { get; init; }
    /// <summary>(production − demand) / activity, clamped −1…+1.</summary>
    public float InventoryRate { get; init; }
}

public sealed class MarketZonePriceDto
{
    public byte GoodId { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Lowest price among active market zones.</summary>
    public float MinPrice { get; init; }
    /// <summary>Highest price among active market zones.</summary>
    public float MaxPrice { get; init; }
    /// <summary>City-wide mean price for this good.</summary>
    public float CityAvgPrice { get; init; }
}

public sealed class EconomySnapshotDto
{
    public GoodImbalanceDto[] Shortages { get; init; } = [];
    public GoodImbalanceDto[] Surpluses { get; init; } = [];
    /// <summary>Top goods by activity with production vs demand (P3.2 stretch).</summary>
    public GoodFlowDto[] Flows { get; init; } = [];
    /// <summary>Active Leontief market partitions (1–16).</summary>
    public int MarketZoneCount { get; init; } = 1;
    /// <summary>Per-zone min/max for staple goods when <see cref="MarketZoneCount"/> &gt; 1.</summary>
    public MarketZonePriceDto[] MarketZonePrices { get; init; } = [];

    public static EconomySnapshotDto From(EconomySystem? economy)
    {
        if (economy is null) return new EconomySnapshotDto();

        var (shortages, surpluses) = economy.GetTopImbalances(5);
        var flows = economy.GetTopGoodFlows(8);
        var zoneSpreads = economy.GetAnchorZonePriceSpreads();
        var zonePrices = new MarketZonePriceDto[zoneSpreads.Length];
        for (int i = 0; i < zoneSpreads.Length; i++)
        {
            var spread = zoneSpreads[i];
            zonePrices[i] = new MarketZonePriceDto
            {
                GoodId = spread.GoodId,
                Name = spread.Name,
                MinPrice = spread.MinPrice,
                MaxPrice = spread.MaxPrice,
                CityAvgPrice = spread.CityAvgPrice,
            };
        }

        var flowDtos = new GoodFlowDto[flows.Length];
        for (int i = 0; i < flows.Length; i++)
        {
            flowDtos[i] = new GoodFlowDto
            {
                GoodId = flows[i].GoodId,
                Name = flows[i].Name,
                Production = flows[i].Production,
                Demand = flows[i].Demand,
                InventoryRate = flows[i].InventoryRate,
            };
        }

        return new EconomySnapshotDto
        {
            Shortages = ToDto(economy, shortages),
            Surpluses = ToDto(economy, surpluses),
            Flows = flowDtos,
            MarketZoneCount = economy.ActiveZoneCount,
            MarketZonePrices = zonePrices,
        };
    }

    private static GoodImbalanceDto[] ToDto(
        EconomySystem economy,
        EconomySystem.GoodImbalanceEntry[] entries)
    {
        var result = new GoodImbalanceDto[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var good = (Good)entries[i].GoodId;
            result[i] = new GoodImbalanceDto
            {
                GoodId = entries[i].GoodId,
                Name = entries[i].Name,
                Magnitude = entries[i].Magnitude,
                Price = economy.GetAveragePrice(good),
            };
        }

        return result;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SimSnapshotDto))]
[JsonSerializable(typeof(BuildingDto))]
[JsonSerializable(typeof(BuildingDto[]))]
[JsonSerializable(typeof(ZoneDto))]
[JsonSerializable(typeof(ZoneDto[]))]
[JsonSerializable(typeof(RoadDto))]
[JsonSerializable(typeof(RoadDto[]))]
[JsonSerializable(typeof(RoadGraphSnapshotDto))]
[JsonSerializable(typeof(TrafficDto))]
[JsonSerializable(typeof(TrafficDto[]))]
[JsonSerializable(typeof(FrictionCorridorDto))]
[JsonSerializable(typeof(FrictionCorridorDto[]))]
[JsonSerializable(typeof(ServiceCoverageDto))]
[JsonSerializable(typeof(ServiceCoverageDto[]))]
[JsonSerializable(typeof(UtilityCoverageDto))]
[JsonSerializable(typeof(UtilityCoverageDto[]))]
[JsonSerializable(typeof(ActiveEventDto))]
[JsonSerializable(typeof(ActiveEventDto[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(GoodImbalanceDto))]
[JsonSerializable(typeof(GoodImbalanceDto[]))]
[JsonSerializable(typeof(GoodFlowDto))]
[JsonSerializable(typeof(GoodFlowDto[]))]
[JsonSerializable(typeof(MarketZonePriceDto))]
[JsonSerializable(typeof(MarketZonePriceDto[]))]
[JsonSerializable(typeof(EconomySnapshotDto))]
[JsonSerializable(typeof(HouseholdPreviewDto))]
[JsonSerializable(typeof(HouseholdPreviewDto[]))]
[JsonSerializable(typeof(CommuteOdSampleDto))]
[JsonSerializable(typeof(CommuteOdSampleDto[]))]
[JsonSerializable(typeof(PopulationL2Dto))]
[JsonSerializable(typeof(Dictionary<int, float>))]
[JsonSerializable(typeof(Dictionary<int, int>))]
internal partial class SnapshotJsonContext : JsonSerializerContext;
