using Forge.Engine;
using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimWasm;

namespace Forge.SimCore;

/// <summary>
/// Engine-agnostic simulation host shared by Unity and browser WASM.
/// JSON export stays in <c>Forge.SimWasm.WasmSimHost</c>.
/// </summary>
public sealed partial class SimHost
{
    private Config _config = null!;
    private WorldState _state = null!;
    private EventBus _eventBus = null!;

    private EconomySystem _economy = null!;
    private PopulationSystem _population = null!;
    private WasmTrafficLite _traffic = null!;
    private TrafficSystem? _fullTraffic;
    private bool _useFullTraffic;
    private ServiceSystem _services = null!;
    private ZoneGrowthSystem _zoneGrowth = null!;
    private BudgetSystem _budget = null!;
    private PoliticsSystem _politics = null!;
    private ResearchSystem _research = null!;
    private EventSystem _events = null!;
    private LawSystem _laws = null!;
    private CulturalDNASystem _culturalDna = null!;
    private TradeSystem _trade = null!;
    private HousingHeraldSystem _housingHerald = null!;
    private ApprovalHeraldSystem _approvalHerald = null!;

    private double _trafficLiteAccumulator;
    private double _trafficEdgeBatchAccumulator;
    private double _dayAccumulator;
    private double _monthAccumulator;
    private int _populationStaggerBucket;
    private int _lastCulturalDnaYear;
    private bool _roadsNeedRebuild;

    private SimHostInitOptions _initOptions = new();

    public bool IsInitialized { get; private set; }

    public WorldState State => _state;
    public Config Config => _config;
    public EconomySystem Economy => _economy;
    public PopulationSystem Population => _population;
    public WasmTrafficLite Traffic => _traffic;
    public TrafficSystem? FullTraffic => _fullTraffic;
    public ServiceSystem Services => _services;
    public EventSystem Events => _events;
    public LawSystem Laws => _laws;
    public ResearchSystem Research => _research;
    public TradeSystem Trade => _trade;
    public PoliticsSystem Politics => _politics;

    public long TickCount => _state?.TickCount ?? 0;
    public int WorldSize => _config?.WorldSize ?? WasmConfig.DefaultWorldSize;

    public void Init(int worldSize = WasmConfig.DefaultWorldSize, SimHostInitOptions? options = null)
    {
        _initOptions = options ?? new SimHostInitOptions();
        worldSize = NextPowerOfTwo(Math.Clamp(worldSize, WasmConfig.MinWorldSize, WasmConfig.MaxWorldSize));

        _config = new Config { WorldSize = worldSize, ChunkSize = WasmConfig.ChunkSize };
        _state = new WorldState(worldSize, maxHouseholds: 10_240, maxBuildings: 5_120,
            maxRoadNodes: 16384, maxVehicles: 256);
        _eventBus = new EventBus();

        _economy = new EconomySystem();
        _population = new PopulationSystem();
        _useFullTraffic = _initOptions.UseFullTraffic;
        if (_useFullTraffic)
        {
            _fullTraffic = new TrafficSystem();
            _traffic = new WasmTrafficLite();
        }
        else
        {
            _traffic = new WasmTrafficLite();
            _traffic.Configure(ResolveTrafficLiteZoneCount());
        }
        _services = new ServiceSystem(worldSize);
        _zoneGrowth = new ZoneGrowthSystem(worldSize);
        _budget = new BudgetSystem();
        _politics = new PoliticsSystem();
        _research = new ResearchSystem();
        _events = new EventSystem(seed: 12345);
        _laws = new LawSystem();
        _culturalDna = new CulturalDNASystem();
        _trade = new TradeSystem();
        _housingHerald = new HousingHeraldSystem();
        _approvalHerald = new ApprovalHeraldSystem();

        _budget.SetEventBus(_eventBus);
        _economy.SetEventBus(_eventBus);

        LoadGameData();

        CulturalDNASystem.ApplyPreset(_state, "western_european");
        _lastCulturalDnaYear = _state.Year;

        if (_initOptions.UnityModernProfile)
        {
            _state.Era = UnityModernConfig.DefaultEraIndex;
            _state.Year = UnityModernConfig.StartingYear;
        }
        else
        {
            _state.Era = 0; // Frontier — advanced by ResearchSystem.CheckEraTransition on monthly tick
        }

        GenerateMap();
        if (!_initOptions.SkipStarterCity)
        {
            SeedStarterCity();
            SeedStartingPopulation();
            _population.BootstrapCommuterAssignments(_state);
            BootstrapServiceCoverage();
        }

        RecomputeLawEffects();
        // PoliticsSystem owns seat assignment; mirror onto WorldState for snapshot/WASM export (P5.6).
        Array.Copy(_politics.CouncilSeats, _state.CouncilSeats, PoliticsSystem.CouncilSeatCount);
        IsInitialized = true;
    }

    public void Tick(double dt)
    {
        if (!IsInitialized) return;

        if (_roadsNeedRebuild)
        {
            RebuildRoadGraphFromTiles();
            _roadsNeedRebuild = false;
        }

        _state.TickCount++;

        _services.L0Tick(_state, (float)dt);

        _population.Tick(_state, _populationStaggerBucket);
        _populationStaggerBucket = (_populationStaggerBucket + 1) % 30;

        _trafficLiteAccumulator += dt;
        while (_trafficLiteAccumulator >= WasmConfig.TrafficLiteInterval)
        {
            _trafficLiteAccumulator -= WasmConfig.TrafficLiteInterval;
            if (_useFullTraffic && _fullTraffic is not null)
                _fullTraffic.Tick(_state, WasmConfig.TrafficLiteInterval);
            else
                _traffic.Tick(_state, WasmConfig.TrafficLiteInterval);
        }

        if (!_useFullTraffic)
        {
            _trafficEdgeBatchAccumulator += dt;
            while (_trafficEdgeBatchAccumulator >= WasmConfig.TrafficLiteEdgeBatchInterval)
            {
                _trafficEdgeBatchAccumulator -= WasmConfig.TrafficLiteEdgeBatchInterval;
                _traffic.TickEdgeBatch(_state, WasmConfig.TrafficLiteEdgeBatchInterval);
            }
        }

        _dayAccumulator += dt;
        while (_dayAccumulator >= WasmConfig.GameDayInterval)
        {
            _dayAccumulator -= WasmConfig.GameDayInterval;
            RunDayTick();
        }

        _monthAccumulator += dt;
        while (_monthAccumulator >= WasmConfig.GameMonthInterval)
        {
            _monthAccumulator -= WasmConfig.GameMonthInterval;
            RunMonthTick();
        }
    }

    public SimSnapshot GetSnapshot()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("SimHost is not initialized.");

        _population.RefreshHousingSnapshotMetrics(_state);
        return SimSnapshot.CaptureFrom(_state);
    }

    /// <summary>Keep WASM status housing fields aligned with the latest household assignments.</summary>
    public void RefreshHousingSnapshotMetrics()
    {
        if (!IsInitialized) return;
        _population.RefreshHousingSnapshotMetrics(_state);
    }

    /// <summary>Top household sample for citizen drill-down (Unity Citizen panel, WASM L2 export).</summary>
    public PopulationL2Dto GetPopulationL2()
    {
        if (!IsInitialized)
            return new PopulationL2Dto();

        return PopulationL2Dto.From(_state, _population);
    }

    /// <summary>Stepped service coverage for GL overlays (police / health / fire / education).</summary>
    public ServiceCoverageDto[] GetServiceCoverageSample(int step = 8)
    {
        if (!IsInitialized)
            return [];

        return ServiceCoverageExport.Sample(_state, _services, step);
    }

    /// <summary>Stepped power/water coverage for GL overlays (utility stress).</summary>
    public UtilityCoverageDto[] GetUtilityCoverageSample(int step = 8)
    {
        if (!IsInitialized)
            return [];

        return UtilityCoverageExport.Sample(_state, step);
    }

    /// <param name="density">0 = low (default); 1–3 = low / medium / high per TileData.ZoneDensity.</param>
    public void PaintZone(int x, int y, byte zoneType, byte density = 0)
    {
        if (!IsInitialized || !_state.Tiles.InBounds(x, y)) return;

        int idx = _state.Tiles.Index(x, y);
        if (_state.Tiles.TerrainType[idx] == (byte)TerrainId.Water) return;
        if (_state.Tiles.TerrainType[idx] == (byte)TerrainId.Rock) return;

        _state.Tiles.ZoneType[idx] = zoneType;
        _state.Tiles.ZoneDensity[idx] = zoneType == 0 ? (byte)0 : NormalizeZoneDensity(density);
    }

    /// <summary>Map brush density to TileData encoding: low=1, medium=2, high=3.</summary>
    internal static byte NormalizeZoneDensity(byte density) =>
        density switch
        {
            0 or 1 => 1,
            2 => 2,
            _ => 3,
        };

    public void Bulldoze(int x, int y)
    {
        if (!IsInitialized || !_state.Tiles.InBounds(x, y)) return;

        int idx = _state.Tiles.Index(x, y);
        bool hadRoad = _state.Tiles.RoadFlags[idx] != 0;

        ushort buildingId = _state.Tiles.BuildingId[idx];
        if (buildingId != 0 && buildingId < _state.Buildings.Capacity
            && _state.Buildings.IsActive(buildingId))
        {
            _state.Buildings.Free(buildingId);
        }

        _state.Tiles.BuildingId[idx] = 0;
        _state.Tiles.ZoneType[idx] = 0;
        _state.Tiles.ZoneDensity[idx] = 0;

        if (hadRoad)
        {
            _state.Tiles.RoadFlags[idx] = 0;
            _state.Tiles.Traffic[idx] = 0;
            ClearNeighborRoadConnections(x, y);
            _roadsNeedRebuild = true;
        }
    }

    public bool PlaceRoad(int x, int y, byte tier = 1, bool bridge = false, bool tunnel = false, bool ramp = false)
    {
        if (!IsInitialized || !_state.Tiles.InBounds(x, y)) return false;

        int idx = _state.Tiles.Index(x, y);
        byte terrain = _state.Tiles.TerrainType[idx];
        if (terrain == (byte)TerrainId.Water || terrain == (byte)TerrainId.Rock) return false;

        tier = (byte)Math.Clamp(tier, (byte)0, (byte)2);
        if (ramp)
        {
            // Ramp connectors are always surface (local/collector), never highway.
            if (RoadTier.IsHighwayTier(tier))
                tier = 1;
            bridge = false;
            tunnel = false;
            if (!RoadHighwayAccess.IsValidRampPlacement(_state.Tiles, x, y))
                return false;
        }

        if (RoadHighwayAccess.WouldCreateIllegalMerge(_state.Tiles, x, y, tier))
            return false;

        _state.Tiles.RoadFlags[idx] = ComputeRoadFlags(x, y, tier, bridge, tunnel, ramp);
        _roadsNeedRebuild = true;

        RefreshRoadFlagsAt(x - 1, y);
        RefreshRoadFlagsAt(x + 1, y);
        RefreshRoadFlagsAt(x, y - 1);
        RefreshRoadFlagsAt(x, y + 1);
        return true;
    }

    public bool PlaceBuilding(int x, int y, int typeId)
    {
        if (!IsInitialized || typeId <= 0 || !_state.Tiles.InBounds(x, y)) return false;

        int idx = _state.Tiles.Index(x, y);
        if (!_state.Tiles.IsBuildable(x, y)) return false;
        if (_state.Tiles.ZoneType[idx] == 0) return false;

        int slot = _state.Buildings.Allocate();
        if (slot < 0) return false;

        _state.Buildings.GridX[slot] = x;
        _state.Buildings.GridY[slot] = y;
        _state.Buildings.Width[slot] = 1;
        _state.Buildings.Height[slot] = 1;
        _state.Buildings.TypeId[slot] = (ushort)typeId;
        _state.Buildings.Level[slot] = 1;
        _state.Buildings.State[slot] = 1;
        _state.Buildings.Condition[slot] = 255;
        _state.Buildings.Occupants[slot] = 0;
        _state.Buildings.MaxOccupants[slot] = 48;

        _state.Tiles.BuildingId[idx] = (ushort)slot;
        return true;
    }

    public bool EnqueueResearch(int techId)
    {
        if (!IsInitialized || _research is null) return false;
        return _research.EnqueueResearch(techId, _state);
    }

    public bool SetLawActive(string lawId, bool active)
    {
        if (!IsInitialized || _laws is null || string.IsNullOrWhiteSpace(lawId)) return false;
        if (!_laws.SetActive(lawId, active)) return false;
        RecomputeLawEffects();
        return true;
    }

    public LawPreviewDto? GetSampleLawPreview()
    {
        if (!IsInitialized || _laws is null || _laws.DefinitionCount == 0)
            return null;

        var definition = _laws.Definitions[0];
        return new LawPreviewDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Active = _laws.IsActive(0),
        };
    }

    public void AdjustBudget(long deltaFunds)
    {
        if (!IsInitialized || _state is null || deltaFunds == 0) return;
        _state.CityFunds += deltaFunds;
    }

    public void ApplyApprovalDelta(float deltaPercent)
    {
        if (!IsInitialized || _state is null || deltaPercent == 0f) return;
        _state.ApprovalRating = Math.Clamp(
            _state.ApprovalRating + deltaPercent / 100f,
            0f,
            1f);
    }

    public void BoostResearch(float points)
    {
        if (!IsInitialized || _state is null || points <= 0f) return;
        _state.ResearchPoints += points;
    }

    public bool ResolveHeraldEvent(int eventId)
    {
        if (!IsInitialized || _events is null || _state is null) return false;
        return _events.ResolvePlayerResponse(eventId, _state);
    }

    public void ResetTickAccumulators()
    {
        _trafficLiteAccumulator = 0;
        _trafficEdgeBatchAccumulator = 0;
        _dayAccumulator = 0;
        _monthAccumulator = 0;
        _populationStaggerBucket = 0;
    }

    public void ClearBuildings()
    {
        var pool = _state.Buildings;
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (pool.IsActive(i))
                pool.Free(i);
        }
    }

    public void ClearZonesAndRoads()
    {
        var tiles = _state.Tiles;
        Array.Clear(tiles.ZoneType, 0, tiles.ZoneType.Length);
        Array.Clear(tiles.ZoneDensity, 0, tiles.ZoneDensity.Length);
        Array.Clear(tiles.RoadFlags, 0, tiles.RoadFlags.Length);
        Array.Clear(tiles.Traffic, 0, tiles.Traffic.Length);
        _state.Roads.Clear();
    }

    public void ClearHouseholds()
    {
        var pool = _state.Households;
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (pool.IsActive(i))
                pool.Free(i);
        }
    }

    /// <summary>
    /// Test / characterization helper: fills pools near v1 charter scale (≥8K HH, ≥3K buildings)
    /// without affecting default <see cref="Init"/> / starter seed. Requires 256×256 (or ≥128).
    /// </summary>
    public void SeedV1ScaleCity()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("SimHost is not initialized.");

        int size = _config.WorldSize;
        if (size < 128)
            throw new InvalidOperationException("V1 scale seed requires world size >= 128.");

        ClearBuildings();
        ClearHouseholds();
        ClearZonesAndRoads();

        int cx = size / 2;
        int cy = size / 2;
        int zoneHalf = size / 2 - 8;
        int buildingTarget = Math.Min(
            WasmConfig.V1ScaleTargetBuildings,
            _state.Buildings.Capacity - 64);

        SeedV1ScaleRoadGrid(cx, cy, zoneHalf);
        SeedV1ScaleZones(cx, cy, zoneHalf, size);
        SeedV1ScaleBuildings(cx, cy, zoneHalf, buildingTarget);

        RebuildRoadGraphFromTiles();
        RestoreHouseholds(WasmConfig.V1ScaleTargetPopulation, WasmConfig.V1ScaleTargetHouseholds);
        BootstrapServiceCoverage();
        _state.Happiness = 0.6f;
    }

    public void RestoreHouseholds(int targetPopulation, int householdCount)
    {
        ClearHouseholds();
        targetPopulation = Math.Max(0, targetPopulation);

        int households = householdCount > 0
            ? Math.Min(householdCount, _state.Households.Capacity)
            : Math.Max(1, Math.Min(200, targetPopulation / 3));
        if (targetPopulation == 0)
        {
            _state.Population = 0;
            return;
        }
        if (households <= 0)
            households = 1;

        var rng = new Random(99);
        int baseMembers = Math.Max(1, targetPopulation / households);
        int remainder = Math.Max(0, targetPopulation - baseMembers * households);

        for (int i = 0; i < households; i++)
        {
            int slot = _state.Households.Allocate();
            if (slot < 0) break;

            int members = baseMembers + (i < remainder ? 1 : 0);
            _state.Households.MemberCount[slot] = (byte)Math.Clamp(members, 1, 8);
            _state.Households.AgeGroup[slot] = 1;
            _state.Households.Education[slot] = (byte)rng.Next(0, 4);
            _state.Households.Income[slot] = 1500 + rng.Next(0, 3000);
            _state.Households.Savings[slot] = rng.Next(500, 10000);
            _state.Households.WealthLevel[slot] = (byte)rng.Next(1, 4);
            _state.Households.Happiness[slot] = 160;
            _state.Households.HealthSatisfaction[slot] = 150;
            _state.Households.SafetySatisfaction[slot] = 150;
            _state.Households.TransportSatisfaction[slot] = 128;
            _state.Households.LeisureSatisfaction[slot] = 128;
        }

        int totalPop = 0;
        for (int i = 0; i < _state.Households.Capacity; i++)
        {
            if (_state.Households.IsActive(i))
                totalPop += _state.Households.MemberCount[i];
        }

        _state.Population = totalPop > 0 ? totalPop : targetPopulation;
        _population.BootstrapCommuterAssignments(_state);
    }

    public void RestoreResearchState(
        float researchPoints,
        int currentResearchId,
        float currentResearchProgress,
        int[]? unlockedTechIds,
        int[]? researchQueue,
        float[]? queueProgress,
        Dictionary<int, float>? eurekaBonuses,
        Dictionary<int, int>? branchingChoices)
    {
        _state.ResearchPoints = researchPoints;
        _state.CurrentResearchId = currentResearchId;
        _state.CurrentResearchProgress = currentResearchProgress;

        if (unlockedTechIds is { Length: > 0 })
        {
            foreach (int techId in unlockedTechIds)
            {
                if (techId >= 0 && techId < ResearchSystem.MaxTechnologies)
                    _state.UnlockTech(techId);
            }
        }

        for (int i = 0; i < ResearchSystem.MaxResearchQueue; i++)
        {
            _research.ResearchQueue[i] = -1;
            _research.QueueProgress[i] = 0f;
        }

        if (researchQueue is { Length: > 0 })
        {
            int n = Math.Min(researchQueue.Length, ResearchSystem.MaxResearchQueue);
            for (int i = 0; i < n; i++)
            {
                _research.ResearchQueue[i] = researchQueue[i];
                _research.QueueProgress[i] =
                    queueProgress is not null && i < queueProgress.Length
                        ? queueProgress[i]
                        : 0f;
            }
        }

        _research.EurekaBonuses.Clear();
        if (eurekaBonuses is not null)
        {
            foreach (var kv in eurekaBonuses)
                _research.EurekaBonuses[kv.Key] = kv.Value;
        }

        _research.BranchingChoices.Clear();
        if (branchingChoices is not null)
        {
            foreach (var kv in branchingChoices)
                _research.BranchingChoices[kv.Key] = kv.Value;
        }
    }

    public void RebuildServicesAfterLoad()
    {
        _services.RebuildFromWorld(_state);
        _services.DailyTick(_state, WasmConfig.GameDayInterval);
    }

    public float ComputeAverageCoverage(InfluenceMap map)
    {
        var tiles = _state.Tiles;
        double sum = 0;
        int count = 0;
        for (int y = 0; y < tiles.Size; y++)
        for (int x = 0; x < tiles.Size; x++)
        {
            int idx = tiles.Index(x, y);
            if (tiles.ZoneType[idx] == 0) continue;
            sum += Math.Clamp(map.GetValue(x, y), 0f, 1f);
            count++;
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    private void RunDayTick()
    {
        _economy.DailyTick(_state, WasmConfig.GameDayInterval);
        _economy.PublishImbalancesTo(_state);
        _services.DailyTick(_state, WasmConfig.GameDayInterval);
        _zoneGrowth.Tick(_state, _economy);
        _events.DailyTick(_state);
        _events.UpdateEvents(_state, 1f);
        ApplyEventEffectsToState(_state);
        _state.TickEvents();
        _politics.DailyTick(_state, WasmConfig.GameDayInterval);

        _state.AdvanceDay();
    }

    private void ApplyEventEffectsToState(WorldState state)
    {
        if (_events.ActiveEventCount == 0) return;

        float happinessMod = _events.GetAggregateEffect(EventSystem.EffectIndex.Happiness);
        if (happinessMod != 0f)
        {
            state.Happiness = Math.Clamp(state.Happiness + happinessMod * 0.01f, 0f, 1f);
        }

        float approvalMod = _events.GetAggregateEffect(EventSystem.EffectIndex.ApprovalRatingChange);
        if (approvalMod != 0f)
        {
            state.ApprovalRating = Math.Clamp(state.ApprovalRating + approvalMod * 0.01f, 0f, 1f);
        }
    }

    private void RunMonthTick()
    {
        _services.MonthlyTick(_state);
        FeedCrossSystemData();
        _population.MonthlyTick(_state);
        _economy.MonthlyTick(_state, WasmConfig.GameDayInterval);
        ProcessGlobalMarketTrade();
        _zoneGrowth.RecalculateLandValue(_state);
        _zoneGrowth.CheckUpgrades(_state);
        _budget.CalculateMonthlyBudget(_state, _economy);
        _budget.ApplyLawModifiers(_state, _laws);
        _politics.MonthlyTick(_state, WasmConfig.GameDayInterval);
        _research.MonthlyTick(_state, WasmConfig.GameDayInterval);

        _housingHerald.MonthlyTick(
            _state,
            _events,
            _state.MeanRentBurden,
            _economy.ResidentialDemand);
        _approvalHerald.MonthlyTick(_state, _events);

        if (_state.Year > _lastCulturalDnaYear)
        {
            _culturalDna.YearlyTick(_state);
            _lastCulturalDnaYear = _state.Year;
        }
    }

    private void BootstrapServiceCoverage()
    {
        var pool = _state.Buildings;
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (!pool.IsActive(i)) continue;
            _services.OnBuildingPlaced(new BuildingPlacedEvent
            {
                BuildingId = i,
                TileX = pool.GridX[i],
                TileY = pool.GridY[i],
                TypeId = pool.TypeId[i],
            }, _state);
        }

        _services.DailyTick(_state, WasmConfig.GameDayInterval);
    }

    private void GenerateMap()
    {
        var (tiles, _) = MapGenerator.Generate(seed: 42, MapType.Plains, size: _config.WorldSize);
        Array.Copy(tiles.TerrainType, _state.Tiles.TerrainType, tiles.TerrainType.Length);
        Array.Copy(tiles.ZoneType, _state.Tiles.ZoneType, tiles.ZoneType.Length);
        Array.Copy(tiles.Elevation, _state.Tiles.Elevation, tiles.Elevation.Length);
        Array.Copy(tiles.LandValue, _state.Tiles.LandValue, tiles.LandValue.Length);
        Array.Copy(tiles.RoadFlags, _state.Tiles.RoadFlags, tiles.RoadFlags.Length);
        Array.Copy(tiles.Traffic, _state.Tiles.Traffic, tiles.Traffic.Length);
        Array.Copy(tiles.ResourceDeposit, _state.Tiles.ResourceDeposit, tiles.ResourceDeposit.Length);
    }

    private void SeedStarterCity()
    {
        int size = _config.WorldSize;
        int cx = size / 2;
        int cy = size / 2;
        int roadHalf = Math.Max(8, size / 8);
        int zoneHalf = Math.Max(12, size / 5);
        int buildingTarget = Math.Min(
            WasmConfig.TargetStarterBuildings,
            (size * size) / 300);

        for (int x = cx - roadHalf; x <= cx + roadHalf; x++)
        {
            if (!_state.Tiles.InBounds(x, cy)) continue;
            _state.Tiles.RoadFlags[_state.Tiles.Index(x, cy)] = 1;
        }

        for (int y = cy - roadHalf; y <= cy + roadHalf; y++)
        {
            if (!_state.Tiles.InBounds(cx, y)) continue;
            _state.Tiles.RoadFlags[_state.Tiles.Index(cx, y)] = 1;
        }

        int ringRadius = size / 3;
        for (int angle = 0; angle < 360; angle += 6)
        {
            double rad = angle * Math.PI / 180.0;
            int x = cx + (int)Math.Round(Math.Cos(rad) * ringRadius);
            int y = cy + (int)Math.Round(Math.Sin(rad) * ringRadius);
            if (!_state.Tiles.InBounds(x, y)) continue;
            byte terrain = _state.Tiles.TerrainType[_state.Tiles.Index(x, y)];
            if (terrain == (byte)TerrainId.Water) continue;
            _state.Tiles.RoadFlags[_state.Tiles.Index(x, y)] = 1;
        }

        int commercialCore = size / 32;
        int residentialRing = size / 16;
        int commercialMid = size / 10;

        for (int y = cy - zoneHalf; y <= cy + zoneHalf; y++)
        for (int x = cx - zoneHalf; x <= cx + zoneHalf; x++)
        {
            if (!_state.Tiles.InBounds(x, y)) continue;
            byte terrain = _state.Tiles.TerrainType[_state.Tiles.Index(x, y)];
            if (terrain == (byte)TerrainId.Water) continue;

            int dist = Math.Abs(x - cx) + Math.Abs(y - cy);
            byte zone;
            if (dist < commercialCore)
                zone = 3;
            else if (dist < residentialRing)
                zone = 1;
            else if (dist < commercialMid)
                zone = 2;
            else
                zone = 4;
            _state.Tiles.ZoneType[_state.Tiles.Index(x, y)] = zone;
        }

        var rng = new Random(7);
        int placed = 0;
        int attempts = 0;
        int maxAttempts = buildingTarget * 20;
        int spawnHalf = zoneHalf - 2;

        while (placed < buildingTarget && attempts < maxAttempts)
        {
            attempts++;
            int x = cx + rng.Next(-spawnHalf, spawnHalf + 1);
            int y = cy + rng.Next(-spawnHalf, spawnHalf + 1);
            if (!_state.Tiles.InBounds(x, y)) continue;

            byte terrain = _state.Tiles.TerrainType[_state.Tiles.Index(x, y)];
            if (terrain == (byte)TerrainId.Water) continue;

            int slot = _state.Buildings.Allocate();
            if (slot < 0) break;

            byte tileZone = _state.Tiles.ZoneType[_state.Tiles.Index(x, y)];
            ushort typeBase = tileZone switch
            {
                3 or 2 => (ushort)300,
                4 => (ushort)400,
                _ => (ushort)100,
            };

            _state.Buildings.GridX[slot] = x;
            _state.Buildings.GridY[slot] = y;
            _state.Buildings.Width[slot] = 1;
            _state.Buildings.Height[slot] = 1;
            _state.Buildings.TypeId[slot] = (ushort)(typeBase + rng.Next(0, 20));
            _state.Buildings.Level[slot] = (byte)rng.Next(1, 5);
            _state.Buildings.State[slot] = 1;
            _state.Buildings.Occupants[slot] = 0;
            _state.Buildings.MaxOccupants[slot] = 48;
            placed++;
        }

        RebuildRoadGraphFromTiles();
    }

    private void SeedV1ScaleRoadGrid(int cx, int cy, int zoneHalf)
    {
        const int roadStep = 16;
        for (int x = cx - zoneHalf; x <= cx + zoneHalf; x += roadStep)
        {
            for (int y = cy - zoneHalf; y <= cy + zoneHalf; y++)
            {
                if (!_state.Tiles.InBounds(x, y)) continue;
                byte terrain = _state.Tiles.TerrainType[_state.Tiles.Index(x, y)];
                if (terrain == (byte)TerrainId.Water || terrain == (byte)TerrainId.Rock) continue;
                _state.Tiles.RoadFlags[_state.Tiles.Index(x, y)] = 1;
            }
        }

        for (int y = cy - zoneHalf; y <= cy + zoneHalf; y += roadStep)
        {
            for (int x = cx - zoneHalf; x <= cx + zoneHalf; x++)
            {
                if (!_state.Tiles.InBounds(x, y)) continue;
                byte terrain = _state.Tiles.TerrainType[_state.Tiles.Index(x, y)];
                if (terrain == (byte)TerrainId.Water || terrain == (byte)TerrainId.Rock) continue;
                _state.Tiles.RoadFlags[_state.Tiles.Index(x, y)] = 1;
            }
        }

        int roadHalf = Math.Max(16, _config.WorldSize / 4);
        for (int x = cx - roadHalf; x <= cx + roadHalf; x++)
        {
            if (!_state.Tiles.InBounds(x, cy)) continue;
            _state.Tiles.RoadFlags[_state.Tiles.Index(x, cy)] = 1;
        }

        for (int y = cy - roadHalf; y <= cy + roadHalf; y++)
        {
            if (!_state.Tiles.InBounds(cx, y)) continue;
            _state.Tiles.RoadFlags[_state.Tiles.Index(cx, y)] = 1;
        }
    }

    private void SeedV1ScaleZones(int cx, int cy, int zoneHalf, int size)
    {
        int commercialCore = size / 16;
        int residentialRing = size / 8;
        int commercialMid = size / 5;

        for (int y = cy - zoneHalf; y <= cy + zoneHalf; y++)
        for (int x = cx - zoneHalf; x <= cx + zoneHalf; x++)
        {
            if (!_state.Tiles.InBounds(x, y)) continue;
            int idx = _state.Tiles.Index(x, y);
            byte terrain = _state.Tiles.TerrainType[idx];
            if (terrain == (byte)TerrainId.Water || terrain == (byte)TerrainId.Rock) continue;
            if (_state.Tiles.RoadFlags[idx] != 0) continue;

            int dist = Math.Abs(x - cx) + Math.Abs(y - cy);
            byte zone = dist switch
            {
                _ when dist < commercialCore => (byte)3,
                _ when dist < residentialRing => (byte)1,
                _ when dist < commercialMid => (byte)2,
                _ => (byte)4,
            };
            _state.Tiles.ZoneType[idx] = zone;
            _state.Tiles.ZoneDensity[idx] = 3;
        }
    }

    private void SeedV1ScaleBuildings(int cx, int cy, int zoneHalf, int buildingTarget)
    {
        var rng = new Random(17);
        int placed = 0;

        for (int y = cy - zoneHalf + 1; y < cy + zoneHalf && placed < buildingTarget; y += 2)
        for (int x = cx - zoneHalf + 1; x < cx + zoneHalf && placed < buildingTarget; x += 2)
        {
            if (TryPlaceSeedBuilding(x, y, rng))
                placed++;
        }

        int attempts = 0;
        int maxAttempts = (buildingTarget - placed) * 12;
        int spawnHalf = zoneHalf - 2;
        while (placed < buildingTarget && attempts < maxAttempts)
        {
            attempts++;
            int x = cx + rng.Next(-spawnHalf, spawnHalf + 1);
            int y = cy + rng.Next(-spawnHalf, spawnHalf + 1);
            if (TryPlaceSeedBuilding(x, y, rng))
                placed++;
        }
    }

    private bool TryPlaceSeedBuilding(int x, int y, Random rng)
    {
        if (!_state.Tiles.InBounds(x, y)) return false;

        int idx = _state.Tiles.Index(x, y);
        byte terrain = _state.Tiles.TerrainType[idx];
        if (terrain == (byte)TerrainId.Water || terrain == (byte)TerrainId.Rock) return false;
        if (_state.Tiles.ZoneType[idx] == 0) return false;
        if (_state.Tiles.BuildingId[idx] != 0) return false;

        int slot = _state.Buildings.Allocate();
        if (slot < 0) return false;

        byte tileZone = _state.Tiles.ZoneType[idx];
        ushort typeBase = tileZone switch
        {
            3 or 2 => (ushort)300,
            4 => (ushort)400,
            _ => (ushort)100,
        };

        _state.Buildings.GridX[slot] = x;
        _state.Buildings.GridY[slot] = y;
        _state.Buildings.Width[slot] = 1;
        _state.Buildings.Height[slot] = 1;
        _state.Buildings.TypeId[slot] = (ushort)(typeBase + rng.Next(0, 20));
        _state.Buildings.Level[slot] = (byte)rng.Next(1, 5);
        _state.Buildings.State[slot] = 1;
        _state.Buildings.Condition[slot] = 255;
        _state.Buildings.Occupants[slot] = (ushort)rng.Next(1, 30);
        _state.Buildings.MaxOccupants[slot] = 48;
        _state.Tiles.BuildingId[idx] = (ushort)slot;
        return true;
    }

    private void SeedStartingPopulation()
    {
        int initialHouseholds = Math.Min(200, Math.Max(50, _config.WorldSize / 2));
        var rng = new Random(42);

        for (int i = 0; i < initialHouseholds; i++)
        {
            int slot = _state.Households.Allocate();
            if (slot < 0) break;

            _state.Households.MemberCount[slot] = (byte)rng.Next(1, 5);
            _state.Households.AgeGroup[slot] = 1;
            _state.Households.Education[slot] = (byte)rng.Next(0, 4);
            _state.Households.Income[slot] = 1500 + rng.Next(0, 3000);
            _state.Households.Savings[slot] = rng.Next(500, 10000);
            _state.Households.WealthLevel[slot] = (byte)rng.Next(1, 4);
            _state.Households.Happiness[slot] = 160;
            _state.Households.HealthSatisfaction[slot] = 150;
            _state.Households.SafetySatisfaction[slot] = 150;
            _state.Households.TransportSatisfaction[slot] = 128;
            _state.Households.LeisureSatisfaction[slot] = 128;
        }

        int totalPop = 0;
        for (int i = 0; i < _state.Households.Capacity; i++)
        {
            if (_state.Households.IsActive(i))
                totalPop += _state.Households.MemberCount[i];
        }

        _state.Population = totalPop;
        _state.Happiness = 0.6f;
    }

    private void LoadGameData()
    {
        string? ReadEvents() => ReadPack(_initOptions.DataPaths?.EventsJsonPath, "events.json");
        string? ReadTech() => ReadPack(_initOptions.DataPaths?.TechnologiesJsonPath, "technologies.json");
        string? ReadLaws() => ReadPack(_initOptions.DataPaths?.LawsJsonPath, "laws.json");

        try
        {
            var eventsJson = ReadEvents();
            if (eventsJson is not null)
                _events.LoadDefinitionsFromJson(eventsJson);
        }
        catch
        {
            // Same degrade path as desktop when data pack is missing.
        }

        try
        {
            var techJson = ReadTech();
            if (techJson is not null)
            {
                if (_initOptions.UnityModernProfile)
                    techJson = UnityModernConfig.FilterTechnologiesJson(techJson);
                _research.LoadFromJson(techJson);
            }
        }
        catch
        {
        }

        try
        {
            var lawsJson = ReadLaws();
            if (lawsJson is not null)
                _laws.LoadFromJson(lawsJson);
        }
        catch
        {
        }
    }

    private string? ReadPack(string? filePath, string embeddedLogicalName)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            return File.ReadAllText(filePath);

        if (_initOptions.EmbeddedDataReader is not null)
        {
            try
            {
                return _initOptions.EmbeddedDataReader(embeddedLogicalName);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private void ProcessGlobalMarketTrade()
    {
        var routes = _trade.Routes;
        for (int i = routes.Count - 1; i >= 0; i--)
        {
            if (routes[i].PartnerCityId != -1)
                _trade.CancelTradeRoute(i);
        }

        _trade.ProcessTrade(_state, _economy);
        _state.TradeBalance = _trade.TradeBalance;
        _state.MonthlyExportValue = _trade.MonthlyExportValue;
        _state.MonthlyImportCost = _trade.MonthlyImportCost;
    }

    private void FeedCrossSystemData()
    {
        float fundsRatio = Math.Clamp(_state.CityFunds / 100_000f, 0f, 1f);
        float employmentRate = CalculateEmploymentRate();
        _state.EmploymentRate = employmentRate;
        _politics.EconomyScore = fundsRatio * 0.5f + employmentRate * 0.5f;
        _politics.ServiceScore = EstimateServiceScore();
        _politics.SafetyScore = EstimateSafetyScore();

        int libraryCount = 0, universityCount = 0, heavyIndustryCount = 0;
        var buildings = _state.Buildings;
        var tiles = _state.Tiles;

        for (int i = 0; i < buildings.Capacity; i++)
        {
            if (!buildings.IsActive(i) || buildings.State[i] != 1) continue;

            uint flags = buildings.ServiceFlags[i];
            if ((flags & (1u << 5)) != 0)
            {
                if (buildings.Level[i] >= 3) universityCount++;
                else libraryCount++;
            }

            int bx = buildings.GridX[i];
            int by = buildings.GridY[i];
            if (tiles.InBounds(bx, by))
            {
                byte zone = tiles.ZoneType[tiles.Index(bx, by)];
                if (zone == 4) heavyIndustryCount++;
            }
        }

        _research.LibraryCount = libraryCount;
        _research.UniversityCount = universityCount;
        _research.HeavyIndustryCount = heavyIndustryCount;

        int educatedPop = 0;
        for (int i = 0; i < _state.Households.Capacity; i++)
        {
            if (!_state.Households.IsActive(i)) continue;
            if (_state.Households.Education[i] >= 2)
                educatedPop += _state.Households.MemberCount[i];
        }
        _research.EducatedPopulation = educatedPop;

        _budget.PropertyTaxRate = _state.PropertyTaxRate;
        _budget.CommercialTaxRate = _state.CommercialTaxRate;
        _budget.IndustrialTaxRate = _state.IndustrialTaxRate;
    }

    private float CalculateEmploymentRate()
    {
        int employed = 0, workingAge = 0;
        for (int i = 0; i < _state.Households.Capacity; i++)
        {
            if (!_state.Households.IsActive(i)) continue;
            if (_state.Households.AgeGroup[i] != 1) continue;
            workingAge++;
            if (_state.Households.WorkBuildingId[i] != 0) employed++;
        }

        return workingAge > 0 ? employed / (float)workingAge : 0.5f;
    }

    private float EstimateServiceScore()
    {
        // Blend health + education coverage as a 0–1 politics input.
        float health = ComputeAverageCoverage(_services.HealthCoverage);
        float education = ComputeAverageCoverage(_services.EducationCoverage);
        return Math.Clamp((health + education) * 0.5f, 0f, 1f);
    }

    private float EstimateSafetyScore()
    {
        float police = ComputeAverageCoverage(_services.PoliceCoverage);
        float fire = ComputeAverageCoverage(_services.FireCoverage);
        return Math.Clamp((police + fire) * 0.5f, 0f, 1f);
    }

    private void RecomputeLawEffects()
    {
        if (_state is null || _laws is null) return;

        _state.ActiveLawCount = _laws.ActiveLawCount;

        float roadCapacity = _laws.GetAggregateEffect(LawEffectKeys.RoadCapacity);
        float trafficCapacity = _laws.GetAggregateEffect(LawEffectKeys.TrafficCapacity);
        _state.LawTrafficCapacityMult = Math.Clamp(1f + roadCapacity + trafficCapacity, 0.1f, 3f);

        float constructionCost = _laws.GetAggregateEffect(LawEffectKeys.ConstructionCost);
        float constructionSpeed = _laws.GetAggregateEffect(LawEffectKeys.ConstructionSpeed);
        float housingSupply = _laws.GetAggregateEffect(LawEffectKeys.HousingSupply);
        float housingDensity = _laws.GetAggregateEffect(LawEffectKeys.HousingDensity);
        float industrialOutput = _laws.GetAggregateEffect(LawEffectKeys.IndustrialOutput);

        _state.LawConstructionSpeedMult = Math.Clamp(1f + constructionSpeed, 0.25f, 3f);
        _state.LawSpawnDemandMult = Math.Clamp(1f - constructionCost * 0.5f, 0.25f, 3f);
        _state.LawResidentialSpawnMult = Math.Clamp(
            1f + housingSupply + housingDensity * 0.5f - constructionCost * 0.3f, 0.25f, 3f);
        _state.LawIndustrialSpawnMult = Math.Clamp(
            1f + industrialOutput - constructionCost * 0.3f, 0.25f, 3f);
        _state.LawCommercialSpawnMult = Math.Clamp(
            1f - constructionCost * 0.2f, 0.25f, 3f);
    }

    private byte ComputeRoadFlags(int x, int y, byte tier, bool bridge, bool tunnel, bool ramp = false)
    {
        byte connections = 0;
        if (HasRoadAt(x, y - 1)) connections |= RoadFlags.North;
        if (HasRoadAt(x + 1, y)) connections |= RoadFlags.East;
        if (HasRoadAt(x, y + 1)) connections |= RoadFlags.South;
        if (HasRoadAt(x - 1, y)) connections |= RoadFlags.West;
        if (connections == 0) connections = RoadFlags.North;
        byte flags = (byte)(connections | ((tier & 0x03) << 4));
        // Structure bits are mutually exclusive (none / bridge / tunnel / ramp).
        if (ramp) flags |= RoadFlags.Ramp;
        else if (bridge) flags |= RoadFlags.Bridge;
        else if (tunnel) flags |= RoadFlags.Tunnel;
        return flags;
    }

    private bool HasRoadAt(int x, int y)
    {
        if (!_state.Tiles.InBounds(x, y)) return false;
        return _state.Tiles.RoadFlags[_state.Tiles.Index(x, y)] != 0;
    }

    private void RefreshRoadFlagsAt(int x, int y)
    {
        if (!_state.Tiles.InBounds(x, y)) return;
        int idx = _state.Tiles.Index(x, y);
        if (_state.Tiles.RoadFlags[idx] == 0) return;
        byte existing = _state.Tiles.RoadFlags[idx];
        byte tier = RoadTier.ExtractLevel(existing);
        bool ramp = RoadFlags.IsRamp(existing);
        bool bridge = RoadFlags.IsBridge(existing);
        bool tunnel = RoadFlags.IsTunnel(existing);
        _state.Tiles.RoadFlags[idx] = ComputeRoadFlags(x, y, tier, bridge, tunnel, ramp);
    }

    private void ClearNeighborRoadConnections(int cx, int cy)
    {
        (int nx, int ny, byte clearBit)[] neighbors =
        [
            (cx, cy - 1, 0x04),
            (cx + 1, cy, 0x08),
            (cx, cy + 1, 0x01),
            (cx - 1, cy, 0x02),
        ];

        foreach (var (nx, ny, bit) in neighbors)
        {
            if (!_state.Tiles.InBounds(nx, ny)) continue;
            int nidx = _state.Tiles.Index(nx, ny);
            if ((_state.Tiles.RoadFlags[nidx] & 0x0F) != 0)
                _state.Tiles.RoadFlags[nidx] &= (byte)~bit;
        }
    }

    /// <summary>Rebuild CSR road graph from tile RoadFlags (after bulldoze / bulk load).</summary>
    private void RebuildRoadGraphFromTiles() =>
        RoadGraphBuilder.Build(_state.Tiles, _state.Roads);

    private static int NextPowerOfTwo(int value)
    {
        int p = 1;
        while (p < value) p <<= 1;
        return p;
    }

    private int ResolveTrafficLiteZoneCount()
    {
        if (_initOptions.TrafficLiteZoneCount is int overrideCount)
            return Math.Max(1, overrideCount);
        if (_initOptions.UnityModernProfile)
            return WasmConfig.TrafficLiteZoneCountUnity;
        return WasmConfig.TrafficLiteZoneCount;
    }
}
