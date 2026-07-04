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
    private CulturalDNASystem _culturalDna = null!;

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
    public long CityFunds => _state?.CityFunds ?? 0;
    public int Era => _state?.Era ?? 0;
    public float ResearchPoints => _state?.ResearchPoints ?? 0f;
    public float ResearchRate => _state?.ResearchRate ?? 0f;
    public int EventDefinitionCount => _events?.Definitions.Count ?? 0;
    public int TechCount => _research?.TechCount ?? 0;
    public WasmTrafficMode TrafficMode => WasmTrafficMode.Lite;

    public void Init(int worldSize = WasmConfig.DefaultWorldSize)
    {
        worldSize = NextPowerOfTwo(Math.Clamp(worldSize, WasmConfig.MinWorldSize, WasmConfig.MaxWorldSize));

        _config = new Config { WorldSize = worldSize, ChunkSize = WasmConfig.ChunkSize };
        _state = new WorldState(worldSize, maxHouseholds: 8192, maxBuildings: 4096,
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
        _culturalDna = new CulturalDNASystem();

        _budget.SetEventBus(_eventBus);
        _economy.SetEventBus(_eventBus);

        LoadGameData();

        CulturalDNASystem.ApplyPreset(_state, "western_european");
        _lastCulturalDnaYear = _state.Year;
        _state.Era = 0; // Frontier — derived each month via WasmEraDeriver (SB-3692)

        GenerateMap();
        SeedStarterCity();
        SeedStartingPopulation();

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
        var dto = SimSnapshotDto.From(snap, _state);
        return JsonSerializer.Serialize(dto, JsonContext.Default.SimSnapshotDto);
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

    private void RunDayTick()
    {
        // L1 economy tick — order collection, zone pricing, RCI demand (SIMULATION_ARCHITECTURE L1)
        _economy.DailyTick(_state, WasmConfig.GameDayInterval);
        _services.DailyTick(_state, WasmConfig.GameDayInterval);
        _zoneGrowth.Tick(_state, _economy);
        _events.DailyTick(_state);
        _events.UpdateEvents(_state, 1f);
        _state.TickEvents();
        _politics.DailyTick(_state, WasmConfig.GameDayInterval);

        _state.AdvanceDay();
    }

    private void RunMonthTick()
    {
        _services.MonthlyTick(_state);
        FeedCrossSystemData();
        _population.MonthlyTick(_state);
        _economy.MonthlyTick(_state, WasmConfig.GameDayInterval);
        _zoneGrowth.RecalculateLandValue(_state);
        _zoneGrowth.CheckUpgrades(_state);
        _budget.CalculateMonthlyBudget(_state, _economy);
        _politics.MonthlyTick(_state, WasmConfig.GameDayInterval);
        _research.MonthlyTick(_state, WasmConfig.GameDayInterval);
        WasmEraDeriver.UpdateEra(_state, _research);

        if (_state.Year > _lastCulturalDnaYear)
        {
            _culturalDna.YearlyTick(_state);
            _lastCulturalDnaYear = _state.Year;
        }
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
                3 or 2 => (ushort)40,
                4 => (ushort)80,
                _ => (ushort)10,
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
    public BuildingDto[] Buildings { get; init; } = [];
    public ZoneDto[] Zones { get; init; } = [];
    public RoadDto[] Roads { get; init; } = [];

    public static SimSnapshotDto From(SimSnapshot snap, WorldState state)
    {
        var buildings = new BuildingDto[snap.BuildingCount];
        for (int i = 0; i < snap.BuildingCount; i++)
        {
            var b = snap.Buildings[i];
            int slot = FindBuildingSlot(state, b.GridX, b.GridY, b.TypeId);
            buildings[i] = new BuildingDto
            {
                Id = slot >= 0 ? slot : i,
                TypeId = b.TypeId,
                TileX = b.GridX,
                TileZ = b.GridY,
                Level = b.Level,
                State = b.State,
                Condition = b.Condition,
            };
        }

        var zones = CollectZones(state);
        var roads = CollectRoads(state);

        return new SimSnapshotDto
        {
            Tick = snap.TickCount,
            Population = state.Population,
            HouseholdCount = state.Households.Count,
            CityFunds = state.CityFunds,
            Era = state.Era,
            Buildings = buildings,
            Zones = zones,
            Roads = roads,
        };
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

    private static int FindBuildingSlot(WorldState state, int x, int y, ushort typeId)
    {
        var pool = state.Buildings;
        for (int i = 0; i < pool.Capacity; i++)
        {
            if (!pool.IsActive(i)) continue;
            if (pool.GridX[i] == x && pool.GridY[i] == y && pool.TypeId[i] == typeId)
                return i;
        }
        return -1;
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

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SimSnapshotDto))]
[JsonSerializable(typeof(BuildingDto))]
[JsonSerializable(typeof(ZoneDto))]
[JsonSerializable(typeof(RoadDto))]
internal partial class JsonContext : JsonSerializerContext;
