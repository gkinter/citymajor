using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;

namespace Forge.SimWasm;

/// <summary>
/// Single-threaded simulation host for browser WASM. Mirrors IronAndOakGame tick
/// wiring without Forge.Engine/SDL rendering or background SimulationLoop thread.
/// </summary>
public sealed class WasmSimHost
{
    private Config _config = null!;
    private WorldState _state = null!;
    private EventBus _eventBus = null!;

    private EconomySystem _economy = null!;
    private PopulationSystem _population = null!;
    private WasmTrafficLite _traffic = null!;
    private ServiceSystem _services = null!;
    private ZoneGrowthSystem _zoneGrowth = null!;
    private BudgetSystem _budget = null!;
    private PoliticsSystem _politics = null!;
    private ResearchSystem _research = null!;
    private EventSystem _events = null!;
    private LawSystem _laws = null!;
    private CulturalDNASystem _culturalDna = null!;
    private TradeSystem _trade = null!;

    private double _trafficLiteAccumulator;
    private double _trafficEdgeBatchAccumulator;
    private double _dayAccumulator;
    private double _monthAccumulator;
    private int _populationStaggerBucket;
    private int _lastCulturalDnaYear;

    public bool IsInitialized { get; private set; }
    public long TickCount => _state?.TickCount ?? 0;
    public int Population => _state?.Population ?? 0;
    public int HouseholdCount => _state?.Households.Count ?? 0;
    /// <summary>Net population change from the last game month (PopulationSystem.MonthlyTick).</summary>
    public int PopulationGrowthRate => _population?.LastMonthlyPopulationGrowth ?? 0;
    public long CityFunds => _state?.CityFunds ?? 0;
    public int Era => _state?.Era ?? 0;
    public float ResearchPoints => _state?.ResearchPoints ?? 0f;
    public float ResearchRate => _state?.ResearchRate ?? 0f;
    public int EventDefinitionCount => _events?.Definitions.Count ?? 0;
    public int LawDefinitionCount => _laws?.DefinitionCount ?? 0;
    public int ActiveLawCount => _laws?.ActiveLawCount ?? 0;
    /// <summary>Count of unlocked technologies (not catalog size).</summary>
    public int UnlockedTechCount =>
        _state is null ? 0 : ResearchSystem.CountUnlockedTechs(_state);
    public int CurrentResearchId => _state?.CurrentResearchId ?? -1;
    public float CurrentResearchProgress => _state?.CurrentResearchProgress ?? 0f;
    /// <summary>Estimated game-months until the active queue item completes.</summary>
    public float CurrentResearchMonthsRemaining
    {
        get
        {
            if (_research is null || _state is null) return 0f;
            if (_research.ResearchQueue[0] == -1) return 0f;
            float rate = _state.ResearchRate;
            if (rate <= 0f) return -1f;
            int techId = _research.ResearchQueue[0];
            float cost = _research.GetEffectiveCost(techId);
            float remaining = Math.Max(0f, cost - _research.QueueProgress[0]);
            return remaining / rate;
        }
    }
    public WasmTrafficMode TrafficMode => WasmTrafficMode.Lite;
    public ActiveEventDto[] ActiveEvents => CollectActiveEvents();
    public float ResidentialDemand => _economy?.ResidentialDemand ?? 0f;
    public float CommercialDemand => _economy?.CommercialDemand ?? 0f;
    public float IndustrialDemand => _economy?.IndustrialDemand ?? 0f;
    public float ApprovalRating => _state?.ApprovalRating ?? 0f;
    public float Happiness => _state?.Happiness ?? 0f;
    public long MonthlyIncome => _state?.Income.Total ?? 0;
    public long MonthlyExpenses => _state?.Expenses.Total ?? 0;
    public int BuildingCount => _state?.Buildings.Count ?? 0;
    public int WorldSize => _config?.WorldSize ?? WasmConfig.DefaultWorldSize;
    public EraProgressSnapshot EraProgress =>
        _state is null || _research is null
            ? new EraProgressSnapshot()
            : WasmEraDeriver.GetEraProgress(_state, _research);

    /// <summary>Top goods shortages/surpluses from Leontief market zones.</summary>
    public EconomySnapshotDto EconomySnapshot =>
        EconomySnapshotDto.From(_economy);

    /// <summary>Global-market export revenue from the last trade month.</summary>
    public float MonthlyExportValue => _trade?.MonthlyExportValue ?? 0f;

    /// <summary>Global-market import cost from the last trade month.</summary>
    public float MonthlyImportCost => _trade?.MonthlyImportCost ?? 0f;

    /// <summary>Net trade balance (exports − imports) from the last trade month.</summary>
    public float TradeBalance => _trade?.TradeBalance ?? 0f;

    /// <summary>City-wide average health coverage over zoned tiles (0–1).</summary>
    public float HealthcareCoverage =>
        _services is null || _state is null ? 0f : ComputeAverageCoverage(_services.HealthCoverage);

    /// <summary>City-wide average police coverage over zoned tiles (0–1).</summary>
    public float PoliceCoverage =>
        _services is null || _state is null ? 0f : ComputeAverageCoverage(_services.PoliceCoverage);

    /// <summary>City-wide average fire coverage over zoned tiles (0–1).</summary>
    public float FireCoverage =>
        _services is null || _state is null ? 0f : ComputeAverageCoverage(_services.FireCoverage);

    /// <summary>City-wide average education coverage over zoned tiles (0–1).</summary>
    public float EducationCoverage =>
        _services is null || _state is null ? 0f : ComputeAverageCoverage(_services.EducationCoverage);

    public int[] CollectUnlockedTechIds()
    {
        if (_state is null) return [];
        var ids = new List<int>();
        for (int i = 0; i < ResearchSystem.MaxTechnologies; i++)
        {
            if (_state.IsTechUnlocked(i)) ids.Add(i);
        }
        return ids.ToArray();
    }

    public void Init(int worldSize = WasmConfig.DefaultWorldSize)
    {
        worldSize = NextPowerOfTwo(Math.Clamp(worldSize, WasmConfig.MinWorldSize, WasmConfig.MaxWorldSize));

        _config = new Config { WorldSize = worldSize, ChunkSize = WasmConfig.ChunkSize };
        _state = new WorldState(worldSize, maxHouseholds: 10_240, maxBuildings: 5_120,
            maxRoadNodes: 16384, maxVehicles: 256);
        _eventBus = new EventBus();

        _economy = new EconomySystem();
        _population = new PopulationSystem();
        _traffic = new WasmTrafficLite();
        _services = new ServiceSystem(worldSize);
        _zoneGrowth = new ZoneGrowthSystem(worldSize);
        _budget = new BudgetSystem();
        _politics = new PoliticsSystem();
        _research = new ResearchSystem();
        _events = new EventSystem(seed: 12345);
        _laws = new LawSystem();
        _culturalDna = new CulturalDNASystem();
        _trade = new TradeSystem();

        _budget.SetEventBus(_eventBus);
        _economy.SetEventBus(_eventBus);

        LoadGameData();

        CulturalDNASystem.ApplyPreset(_state, "western_european");
        _lastCulturalDnaYear = _state.Year;
        _state.Era = 0; // Frontier — advanced by ResearchSystem.CheckEraTransition on monthly tick

        GenerateMap();
        SeedStarterCity();
        SeedStartingPopulation();
        BootstrapServiceCoverage();

        IsInitialized = true;
    }

    public void Tick(double dt)
    {
        if (!IsInitialized) return;

        _state.TickCount++;

        // L1 staggered satisfaction — 1/30 of households per sim tick (spread load for 8 Hz budget)
        _population.Tick(_state, _populationStaggerBucket);
        _populationStaggerBucket = (_populationStaggerBucket + 1) % 30;

        _trafficLiteAccumulator += dt;
        while (_trafficLiteAccumulator >= WasmConfig.TrafficLiteInterval)
        {
            _trafficLiteAccumulator -= WasmConfig.TrafficLiteInterval;
            _traffic.Tick(_state, WasmConfig.TrafficLiteInterval);
        }

        _trafficEdgeBatchAccumulator += dt;
        while (_trafficEdgeBatchAccumulator >= WasmConfig.TrafficLiteEdgeBatchInterval)
        {
            _trafficEdgeBatchAccumulator -= WasmConfig.TrafficLiteEdgeBatchInterval;
            _traffic.TickEdgeBatch(_state, WasmConfig.TrafficLiteEdgeBatchInterval);
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

    public string GetRenderSnapshotJson()
    {
        if (!IsInitialized) return "{}";

        var snap = SimSnapshot.CaptureFrom(_state);
        var dto = SimSnapshotDto.From(snap, _state, _events, _economy, _services, _population, _research);
        return JsonSerializer.Serialize(dto, JsonContext.Default.SimSnapshotDto);
    }

    public string GetStatusJson()
    {
        if (!IsInitialized)
            return JsonSerializer.Serialize(new WasmStatusDto(), JsonContext.Default.WasmStatusDto);

        return JsonSerializer.Serialize(WasmStatusDto.From(this), JsonContext.Default.WasmStatusDto);
    }

    /// <summary>Top households sample for CitizenPanel L2 drill-down.</summary>
    public PopulationL2Dto GetPopulationL2Export()
    {
        if (!IsInitialized || _population is null || _state is null)
            return new PopulationL2Dto();
        return PopulationL2Dto.From(_state, _population);
    }

    public void PaintZone(int x, int y, byte zoneType)
    {
        if (!IsInitialized || !_state.Tiles.InBounds(x, y)) return;

        int idx = _state.Tiles.Index(x, y);
        if (_state.Tiles.TerrainType[idx] == (byte)TerrainId.Water) return;
        if (_state.Tiles.TerrainType[idx] == (byte)TerrainId.Rock) return;

        _state.Tiles.ZoneType[idx] = zoneType;
        _state.Tiles.ZoneDensity[idx] = zoneType == 0 ? (byte)0 : (byte)1;
    }

    public void Bulldoze(int x, int y)
    {
        if (!IsInitialized || !_state.Tiles.InBounds(x, y)) return;

        int idx = _state.Tiles.Index(x, y);
        _state.Tiles.ZoneType[idx] = 0;
        _state.Tiles.ZoneDensity[idx] = 0;
        // Building demolition lands in a follow-up command.
    }

    /// <summary>Place a single dirt-road tile and refresh neighbor connection flags.</summary>
    public void PlaceRoad(int x, int y)
    {
        if (!IsInitialized || !_state.Tiles.InBounds(x, y)) return;

        int idx = _state.Tiles.Index(x, y);
        byte terrain = _state.Tiles.TerrainType[idx];
        if (terrain == (byte)TerrainId.Water || terrain == (byte)TerrainId.Rock) return;

        _state.Tiles.RoadFlags[idx] = ComputeRoadFlags(x, y);
        _state.Roads.AddNode(x, y);

        RefreshRoadFlagsAt(x - 1, y);
        RefreshRoadFlagsAt(x + 1, y);
        RefreshRoadFlagsAt(x, y - 1);
        RefreshRoadFlagsAt(x, y + 1);
    }

    /// <summary>
    /// Place one building on a zoned, buildable tile. Returns false when placement is rejected.
    /// </summary>
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
        _state.Buildings.State[slot] = 1; // operational
        _state.Buildings.Condition[slot] = 255;
        _state.Buildings.Occupants[slot] = 0;
        _state.Buildings.MaxOccupants[slot] = 48;

        _state.Tiles.BuildingId[idx] = (ushort)slot;
        return true;
    }

    /// <summary>Enqueue a technology for research. Returns true if added to the queue.</summary>
    public bool EnqueueResearch(int techId)
    {
        if (!IsInitialized || _research is null) return false;
        return _research.EnqueueResearch(techId, _state);
    }

    /// <summary>Enable or disable an ordinance by slug id. Effect application deferred.</summary>
    public bool SetLawActive(string lawId, bool active)
    {
        if (!IsInitialized || _laws is null || string.IsNullOrWhiteSpace(lawId)) return false;
        return _laws.SetActive(lawId, active);
    }

    /// <summary>First catalog entry for HUD sample toggle (v1.5 stub).</summary>
    public LawPreviewDto? GetSampleLawPreview()
    {
        if (_laws is null || _laws.DefinitionCount == 0) return null;

        var definition = _laws.Definitions[0];
        return new LawPreviewDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Active = _laws.IsActive(0),
        };
    }

    /// <summary>Apply a one-time treasury change from a Herald council budget option.</summary>
    public void AdjustBudget(long deltaFunds)
    {
        if (!IsInitialized || _state is null || deltaFunds == 0) return;
        _state.CityFunds += deltaFunds;
    }

    /// <summary>Apply a mayor approval swing from a Herald council option (percentage points).</summary>
    public void ApplyApprovalDelta(float deltaPercent)
    {
        if (!IsInitialized || _state is null || deltaPercent == 0f) return;
        _state.ApprovalRating = Math.Clamp(
            _state.ApprovalRating + deltaPercent / 100f,
            0f,
            1f);
    }

    /// <summary>Stub RP grant from Herald council options until policy research hooks land.</summary>
    public void BoostResearch(float points)
    {
        if (!IsInitialized || _state is null || points <= 0f) return;
        _state.ResearchPoints += points;
    }

    /// <summary>Resolve an active sim event after the player picks a Herald council option.</summary>
    public bool ResolveHeraldEvent(int eventId)
    {
        if (!IsInitialized || _events is null || _state is null) return false;
        return _events.ResolvePlayerResponse(eventId, _state);
    }

    /// <summary>
    /// Restore simulation state from a Layer-C JSON snapshot (save/load v1).
    /// Re-inits the world shell, clears starter content, then applies saved tiles/buildings/scalars.
    /// </summary>
    public bool LoadSnapshotFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        SimSnapshotDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, JsonContext.Default.SimSnapshotDto);
        }
        catch
        {
            return false;
        }

        if (dto is null) return false;

        int size = _config?.WorldSize ?? WasmConfig.DefaultWorldSize;
        Init(size);
        ApplySnapshotDto(dto);
        return true;
    }

    /// <summary>Export canonical CMJR bytes (Layer B) for cloud persistence.</summary>
    public byte[] ExportCmjrBytes(string cityName = "City") =>
        CmjrSave.Export(this, cityName);

    /// <summary>Restore simulation from CMJR bytes; accepts base64 from API transport.</summary>
    public bool LoadFromCmjrBytes(ReadOnlySpan<byte> data) => CmjrSave.Load(this, data);

    public bool LoadFromCmjrBase64(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return false;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return LoadFromCmjrBytes(bytes);
        }
        catch
        {
            return false;
        }
    }

    private void ApplySnapshotDto(SimSnapshotDto dto)
    {
        ClearBuildings();
        ClearZonesAndRoads();

        foreach (var zone in dto.Zones)
        {
            if (!_state.Tiles.InBounds(zone.TileX, zone.TileZ)) continue;
            PaintZone(zone.TileX, zone.TileZ, zone.ZoneType);
        }

        foreach (var road in dto.Roads)
        {
            if (!_state.Tiles.InBounds(road.TileX, road.TileZ)) continue;
            int idx = _state.Tiles.Index(road.TileX, road.TileZ);
            _state.Tiles.RoadFlags[idx] = road.RoadFlags != 0 ? road.RoadFlags : (byte)0x01;
            _state.Roads.AddNode(road.TileX, road.TileZ);
        }

        Array.Clear(_state.Tiles.Traffic, 0, _state.Tiles.Traffic.Length);
        foreach (var tile in dto.Traffic)
        {
            if (!_state.Tiles.InBounds(tile.TileX, tile.TileZ)) continue;
            int idx = _state.Tiles.Index(tile.TileX, tile.TileZ);
            _state.Tiles.Traffic[idx] = Math.Clamp(tile.Density, 0f, 1f);
        }

        foreach (var building in dto.Buildings)
        {
            if (!_state.Tiles.InBounds(building.TileX, building.TileZ)) continue;

            int slot = _state.Buildings.Allocate();
            if (slot < 0) break;

            _state.Buildings.GridX[slot] = building.TileX;
            _state.Buildings.GridY[slot] = building.TileZ;
            _state.Buildings.Width[slot] = 1;
            _state.Buildings.Height[slot] = 1;
            _state.Buildings.TypeId[slot] = building.TypeId;
            _state.Buildings.Level[slot] = building.Level == 0 ? (byte)1 : building.Level;
            _state.Buildings.State[slot] = building.State == 0 ? (byte)1 : building.State;
            _state.Buildings.Condition[slot] = building.Condition == 0 ? (byte)255 : building.Condition;
            _state.Buildings.Occupants[slot] = 0;
            _state.Buildings.MaxOccupants[slot] = 48;
        }

        _state.TickCount = dto.Tick;
        RestoreHouseholds(dto.Population, dto.HouseholdCount);
        _state.CityFunds = dto.CityFunds;
        _state.Era = dto.Era;
        _state.Happiness = dto.Happiness;
        _state.ApprovalRating = Math.Clamp(dto.Approval / 100f, 0f, 1f);
        _state.Income.Reset();
        _state.Expenses.Reset();
        if (dto.MonthlyIncome > 0)
            _state.Income.ResidentialTax = dto.MonthlyIncome;
        if (dto.MonthlyExpenses > 0)
            _state.Expenses.InfrastructureMaintenance = dto.MonthlyExpenses;

        RestoreResearchState(dto);

        _trafficLiteAccumulator = 0;
        _trafficEdgeBatchAccumulator = 0;
        _dayAccumulator = 0;
        _monthAccumulator = 0;
        _populationStaggerBucket = 0;

        _services.RebuildFromWorld(_state);
        _services.DailyTick(_state, WasmConfig.GameDayInterval);
    }

    private void RestoreResearchState(SimSnapshotDto dto)
    {
        _state.ResearchPoints = dto.ResearchPoints;
        _state.CurrentResearchId = dto.CurrentResearchId;
        _state.CurrentResearchProgress = dto.CurrentResearchProgress;

        if (dto.UnlockedTechIds is { Length: > 0 })
        {
            foreach (int techId in dto.UnlockedTechIds)
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

        if (dto.ResearchQueue is { Length: > 0 })
        {
            int n = Math.Min(dto.ResearchQueue.Length, ResearchSystem.MaxResearchQueue);
            for (int i = 0; i < n; i++)
            {
                _research.ResearchQueue[i] = dto.ResearchQueue[i];
                _research.QueueProgress[i] =
                    dto.QueueProgress is not null && i < dto.QueueProgress.Length
                        ? dto.QueueProgress[i]
                        : 0f;
            }
        }

        _research.EurekaBonuses.Clear();
        if (dto.EurekaBonuses is not null)
        {
            foreach (var kv in dto.EurekaBonuses)
                _research.EurekaBonuses[kv.Key] = kv.Value;
        }

        _research.BranchingChoices.Clear();
        if (dto.BranchingChoices is not null)
        {
            foreach (var kv in dto.BranchingChoices)
                _research.BranchingChoices[kv.Key] = kv.Value;
        }
    }

    private void ClearBuildings()
    {
        var pool = _state.Buildings;
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (pool.IsActive(i))
                pool.Free(i);
        }
    }

    private void ClearZonesAndRoads()
    {
        var tiles = _state.Tiles;
        Array.Clear(tiles.ZoneType, 0, tiles.ZoneType.Length);
        Array.Clear(tiles.ZoneDensity, 0, tiles.ZoneDensity.Length);
        Array.Clear(tiles.RoadFlags, 0, tiles.RoadFlags.Length);
        Array.Clear(tiles.Traffic, 0, tiles.Traffic.Length);
        _state.Roads.Clear();
    }

    private void ClearHouseholds()
    {
        var pool = _state.Households;
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (pool.IsActive(i))
                pool.Free(i);
        }
    }

    /// <summary>
    /// Rebuild household agents to match saved population scalars after load.
    /// </summary>
    private void RestoreHouseholds(int targetPopulation, int householdCount)
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
    }

    private byte ComputeRoadFlags(int x, int y)
    {
        byte connections = 0;
        if (HasRoadAt(x, y - 1)) connections |= 0x01; // N
        if (HasRoadAt(x + 1, y)) connections |= 0x02; // E
        if (HasRoadAt(x, y + 1)) connections |= 0x04; // S
        if (HasRoadAt(x - 1, y)) connections |= 0x08; // W
        // bits 4-5: road level 0=dirt — leave at 0
        return connections != 0 ? connections : (byte)0x01;
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
        _state.Tiles.RoadFlags[idx] = ComputeRoadFlags(x, y);
    }

    /// <summary>Mean influence-map value across all zoned tiles.</summary>
    private float ComputeAverageCoverage(InfluenceMap map)
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
        // L1 economy tick — order collection, zone pricing, RCI demand (SIMULATION_ARCHITECTURE L1)
        _economy.DailyTick(_state, WasmConfig.GameDayInterval);
        _services.DailyTick(_state, WasmConfig.GameDayInterval);
        _zoneGrowth.Tick(_state, _economy);
        _events.DailyTick(_state);
        _events.UpdateEvents(_state, 1f);
        ApplyEventEffectsToState(_state);
        _state.TickEvents();
        _politics.DailyTick(_state, WasmConfig.GameDayInterval);

        _state.AdvanceDay();
    }

    /// <summary>
    /// Apply aggregate happiness / approval modifiers from active game events.
    /// Mirrors <c>IronAndOakGame.ApplyEventEffectsToState</c> (partial parity — tile-local
    /// effects are still applied inside <see cref="EventSystem.UpdateEvents"/>).
    /// </summary>
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
        _politics.MonthlyTick(_state, WasmConfig.GameDayInterval);
        _research.MonthlyTick(_state, WasmConfig.GameDayInterval);
        // Era set by ResearchSystem.MonthlyTick via CheckEraTransition (pop + tech count).

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
            _state.Roads.AddNode(x, cy);
        }

        for (int y = cy - roadHalf; y <= cy + roadHalf; y++)
        {
            if (!_state.Tiles.InBounds(cx, y)) continue;
            _state.Tiles.RoadFlags[_state.Tiles.Index(cx, y)] = 1;
            _state.Roads.AddNode(cx, y);
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
            _state.Roads.AddNode(x, y);
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
            _state.Buildings.Occupants[slot] = (ushort)rng.Next(1, 30);
            _state.Buildings.MaxOccupants[slot] = 48;
            placed++;
        }
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
        try
        {
            _events.LoadDefinitionsFromJson(WasmEmbeddedData.Read("events.json"));
        }
        catch
        {
            // Same degrade path as desktop when data pack is missing.
        }

        try
        {
            _research.LoadFromJson(WasmEmbeddedData.Read("technologies.json"));
        }
        catch
        {
            // Same degrade path as desktop when data pack is missing.
        }

        try
        {
            _laws.LoadFromJson(WasmEmbeddedData.Read("laws.json"));
        }
        catch
        {
            // Same degrade path as desktop when data pack is missing.
        }
    }

    /// <summary>
    /// Auto import/export against the anonymous global market only (<c>PartnerCityId = -1</c>).
    /// Inter-city trade routes are deferred until regional map (v1.5).
    /// </summary>
    private void ProcessGlobalMarketTrade()
    {
        // WASM spike does not simulate partner cities — strip non-global routes before execution.
        var routes = _trade.Routes;
        for (int i = routes.Count - 1; i >= 0; i--)
        {
            if (routes[i].PartnerCityId != -1)
                _trade.CancelTradeRoute(i);
        }

        _trade.ProcessTrade(_state, _economy);
    }

    private void FeedCrossSystemData()
    {
        float fundsRatio = Math.Clamp(_state.CityFunds / 100_000f, 0f, 1f);
        float employmentRate = CalculateEmploymentRate();
        _politics.EconomyScore = fundsRatio * 0.5f + employmentRate * 0.5f;
        _politics.ServiceScore = 0.5f;
        _politics.SafetyScore = 0.6f;

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

    private static int NextPowerOfTwo(int value)
    {
        int p = 1;
        while (p < value) p <<= 1;
        return p;
    }

    private ActiveEventDto[] CollectActiveEvents()
    {
        if (_events is null) return [];

        var active = _events.ActiveEvents;
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
}

public sealed class TickIntervalsDto
{
    public double GameDaySeconds { get; init; }
    public double GameMonthSeconds { get; init; }
    public double TrafficLiteSeconds { get; init; }
    public double TrafficLiteEdgeBatchSeconds { get; init; }
    public double TrafficStubSeconds { get; init; }
}

public sealed class TrafficLiteInfoDto
{
    public int ZoneCount { get; init; }
    public int FrankWolfeIterations { get; init; }
    public int EdgeBatchCount { get; init; }
}

/// <summary>Named household row for web CitizenPanel L2 list.</summary>
public sealed class HouseholdPreviewDto
{
    public string Id { get; init; } = "";
    public int TileX { get; init; }
    public int TileZ { get; init; }
    /// <summary>0–1 satisfaction.</summary>
    public float Happiness { get; init; }
    /// <summary>Commute time in game minutes.</summary>
    public float CommuteMin { get; init; }
}

/// <summary>Population L2 snapshot — top households sample for drill-down.</summary>
public sealed class PopulationL2Dto
{
    public HouseholdPreviewDto[] Households { get; init; } = [];

    public static PopulationL2Dto From(WorldState state, PopulationSystem? population)
    {
        if (population is null || state.Households.Count == 0)
            return new PopulationL2Dto();

        var rows = population.CollectHouseholdSample(state, limit: 50);
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
            };
        }

        return new PopulationL2Dto { Households = households };
    }
}

public sealed class WasmStatusDto
{
    public bool Initialized { get; init; }
    public long Tick { get; init; }
    public long TickCount { get; init; }
    public int Population { get; init; }
    public int HouseholdCount { get; init; }
    /// <summary>Net population change per game month from PopulationSystem.</summary>
    public int PopulationGrowthRate { get; init; }
    public long CityFunds { get; init; }
    public int Era { get; init; }
    public string EraName { get; init; } = "";
    public EraProgressSnapshot EraProgress { get; init; } = new();
    public float ResidentialDemand { get; init; }
    public float CommercialDemand { get; init; }
    public float IndustrialDemand { get; init; }
    public float Approval { get; init; }
    public float Happiness { get; init; }
    public long MonthlyIncome { get; init; }
    public long MonthlyExpenses { get; init; }
    public int BuildingCount { get; init; }
    public float ResearchPoints { get; init; }
    public float ResearchRate { get; init; }
    public int EventDefinitionCount { get; init; }
    public int LawDefinitionCount { get; init; }
    public int ActiveLawCount { get; init; }
    /// <summary>First ordinance for HUD sample toggle (v1.5 stub).</summary>
    public LawPreviewDto? SampleLaw { get; init; }
    public ActiveEventDto[] ActiveEvents { get; init; } = [];
    public int TechCount { get; init; }
    public int CurrentResearchId { get; init; } = -1;
    public float CurrentResearchProgress { get; init; }
    public float CurrentResearchMonthsRemaining { get; init; }
    public int[] UnlockedTechIds { get; init; } = [];
    public string TrafficMode { get; init; } = "";
    public TickIntervalsDto TickIntervals { get; init; } = new();
    public TrafficLiteInfoDto TrafficLite { get; init; } = new();
    public string[] Systems { get; init; } = [];
    public string[] Stubbed { get; init; } = [];
    /// <summary>Mean health coverage over zoned tiles (0–1) from ServiceSystem.</summary>
    public float HealthcareCoverage { get; init; }
    /// <summary>Mean police coverage over zoned tiles (0–1).</summary>
    public float PoliceCoverage { get; init; }
    /// <summary>Mean fire coverage over zoned tiles (0–1).</summary>
    public float FireCoverage { get; init; }
    /// <summary>Mean education coverage over zoned tiles (0–1).</summary>
    public float EducationCoverage { get; init; }
    /// <summary>Leontief goods shortages/surpluses for economy HUD.</summary>
    public EconomySnapshotDto Economy { get; init; } = new();
    /// <summary>Global-market export revenue from the last trade month.</summary>
    public float MonthlyExportValue { get; init; }
    /// <summary>Global-market import cost from the last trade month.</summary>
    public float MonthlyImportCost { get; init; }
    /// <summary>Net trade balance (exports − imports) from the last trade month.</summary>
    public float TradeBalance { get; init; }
    /// <summary>Top households sample for CitizenPanel L2 drill-down.</summary>
    public PopulationL2Dto PopulationL2 { get; init; } = new();

    public static WasmStatusDto From(WasmSimHost host) => new()
    {
        Initialized = host.IsInitialized,
        Tick = host.TickCount,
        TickCount = host.TickCount,
        Population = host.Population,
        HouseholdCount = host.HouseholdCount,
        PopulationGrowthRate = host.PopulationGrowthRate,
        CityFunds = host.CityFunds,
        Era = host.Era,
        EraName = WasmEraDeriver.EraName(host.Era),
        EraProgress = host.EraProgress,
        ResidentialDemand = host.ResidentialDemand,
        CommercialDemand = host.CommercialDemand,
        IndustrialDemand = host.IndustrialDemand,
        Approval = host.ApprovalRating * 100f,
        Happiness = host.Happiness,
        MonthlyIncome = host.MonthlyIncome,
        MonthlyExpenses = host.MonthlyExpenses,
        BuildingCount = host.BuildingCount,
        ResearchPoints = host.ResearchPoints,
        ResearchRate = host.ResearchRate,
        EventDefinitionCount = host.EventDefinitionCount,
        LawDefinitionCount = host.LawDefinitionCount,
        ActiveLawCount = host.ActiveLawCount,
        SampleLaw = host.GetSampleLawPreview(),
        ActiveEvents = host.ActiveEvents,
        TechCount = host.UnlockedTechCount,
        CurrentResearchId = host.CurrentResearchId,
        CurrentResearchProgress = host.CurrentResearchProgress,
        CurrentResearchMonthsRemaining = host.CurrentResearchMonthsRemaining,
        UnlockedTechIds = host.CollectUnlockedTechIds(),
        TrafficMode = host.TrafficMode.ToString().ToLowerInvariant(),
        TickIntervals = new TickIntervalsDto
        {
            GameDaySeconds = WasmConfig.GameDayInterval,
            GameMonthSeconds = WasmConfig.GameMonthInterval,
            TrafficLiteSeconds = WasmConfig.TrafficLiteInterval,
            TrafficLiteEdgeBatchSeconds = WasmConfig.TrafficLiteEdgeBatchInterval,
            TrafficStubSeconds = WasmConfig.TrafficStubInterval,
        },
        TrafficLite = new TrafficLiteInfoDto
        {
            ZoneCount = WasmConfig.TrafficLiteZoneCount,
            FrankWolfeIterations = WasmConfig.TrafficLiteFrankWolfeIterations,
            EdgeBatchCount = WasmConfig.TrafficLiteEdgeBatchCount,
        },
        Systems =
        [
            "EconomySystem",
            "PopulationSystem",
            "WasmTrafficLite (64-zone BPR Frank-Wolfe)",
            "ServiceSystem",
            "ZoneGrowthSystem",
            "BudgetSystem",
            "PoliticsSystem",
            "EventSystem",
            "LawSystem",
            "ResearchSystem",
            "CulturalDNASystem",
            "TradeSystem (global market, PartnerCityId=-1)",
        ],
        Stubbed =
        [
            "TrafficSystem full (500 zones — desktop only; WASM uses lite mode)",
        ],
        HealthcareCoverage = host.HealthcareCoverage,
        PoliceCoverage = host.PoliceCoverage,
        FireCoverage = host.FireCoverage,
        EducationCoverage = host.EducationCoverage,
        Economy = host.EconomySnapshot,
        MonthlyExportValue = host.MonthlyExportValue,
        MonthlyImportCost = host.MonthlyImportCost,
        TradeBalance = host.TradeBalance,
        PopulationL2 = host.GetPopulationL2Export(),
    };
}

/// <summary>
/// JSON snapshot matching web/lib/sim-bridge.ts SimSnapshot (SB-3683).
/// </summary>
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
    public BuildingDto[] Buildings { get; init; } = [];
    public ZoneDto[] Zones { get; init; } = [];
    public RoadDto[] Roads { get; init; } = [];
    /// <summary>Sparse road tiles with congestion density (0–1).</summary>
    public TrafficDto[] Traffic { get; init; } = [];
    /// <summary>Sparse zoned tiles with per-service coverage (0–1).</summary>
    public ServiceCoverageDto[] ServiceCoverage { get; init; } = [];
    public ActiveEventDto[] ActiveEvents { get; init; } = [];
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

    public static SimSnapshotDto From(
        SimSnapshot snap,
        WorldState state,
        EventSystem? events = null,
        EconomySystem? economy = null,
        ServiceSystem? services = null,
        PopulationSystem? population = null,
        ResearchSystem? research = null)
    {
        var buildings = CollectBuildings(state);
        var zones = CollectZones(state);
        var roads = CollectRoads(state);
        var traffic = CollectTraffic(state);
        var serviceCoverage = services is null ? [] : CollectServiceCoverage(state, services);

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
            Buildings = buildings,
            Zones = zones,
            Roads = roads,
            Traffic = traffic,
            ServiceCoverage = serviceCoverage,
            ActiveEvents = events is null ? [] : CollectActiveEvents(events),
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
        };
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

public sealed class TrafficDto
{
    public int TileX { get; init; }
    public int TileZ { get; init; }
    public float Density { get; init; }
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
    public string Name { get; init; } = "";
    /// <summary>Absolute demand−supply (shortage) or supply−demand (surplus).</summary>
    public float Magnitude { get; init; }
}

public sealed class EconomySnapshotDto
{
    public GoodImbalanceDto[] Shortages { get; init; } = [];
    public GoodImbalanceDto[] Surpluses { get; init; } = [];

    public static EconomySnapshotDto From(EconomySystem? economy)
    {
        if (economy is null) return new EconomySnapshotDto();

        var (shortages, surpluses) = economy.GetTopImbalances(5);
        return new EconomySnapshotDto
        {
            Shortages = ToDto(shortages),
            Surpluses = ToDto(surpluses),
        };
    }

    private static GoodImbalanceDto[] ToDto(EconomySystem.GoodImbalanceEntry[] entries)
    {
        var result = new GoodImbalanceDto[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            result[i] = new GoodImbalanceDto
            {
                Name = entries[i].Name,
                Magnitude = entries[i].Magnitude,
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
[JsonSerializable(typeof(TrafficDto))]
[JsonSerializable(typeof(TrafficDto[]))]
[JsonSerializable(typeof(ServiceCoverageDto))]
[JsonSerializable(typeof(ServiceCoverageDto[]))]
[JsonSerializable(typeof(ActiveEventDto))]
[JsonSerializable(typeof(ActiveEventDto[]))]
[JsonSerializable(typeof(LawPreviewDto))]
[JsonSerializable(typeof(GoodImbalanceDto))]
[JsonSerializable(typeof(GoodImbalanceDto[]))]
[JsonSerializable(typeof(EconomySnapshotDto))]
[JsonSerializable(typeof(WasmStatusDto))]
[JsonSerializable(typeof(HouseholdPreviewDto))]
[JsonSerializable(typeof(HouseholdPreviewDto[]))]
[JsonSerializable(typeof(PopulationL2Dto))]
[JsonSerializable(typeof(TickIntervalsDto))]
[JsonSerializable(typeof(TrafficLiteInfoDto))]
[JsonSerializable(typeof(EraProgressSnapshot))]
[JsonSerializable(typeof(EraProgressGate))]
[JsonSerializable(typeof(EraProgressGate[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<int, float>))]
[JsonSerializable(typeof(Dictionary<int, int>))]
internal partial class JsonContext : JsonSerializerContext;
