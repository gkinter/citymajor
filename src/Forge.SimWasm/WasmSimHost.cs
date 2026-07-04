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
    private const double TrafficInterval = 0.5;
    private const double DayInterval = 1.0;

    private Config _config = null!;
    private WorldState _state = null!;
    private EventBus _eventBus = null!;

    private EconomySystem _economy = null!;
    private PopulationSystem _population = null!;
    private TrafficSystem _traffic = null!;
    private ServiceSystem _services = null!;
    private ZoneGrowthSystem _zoneGrowth = null!;
    private BudgetSystem _budget = null!;
    private PoliticsSystem _politics = null!;
    private ResearchSystem _research = null!;
    private EventSystem _events = null!;
    private CulturalDNASystem _culturalDna = null!;

    private double _trafficAccumulator;
    private double _dayAccumulator;
    private int _lastCulturalDnaYear;

    public bool IsInitialized { get; private set; }
    public long TickCount => _state?.TickCount ?? 0;

    public void Init(int worldSize = 64)
    {
        worldSize = NextPowerOfTwo(Math.Clamp(worldSize, 32, 256));

        _config = new Config { WorldSize = worldSize, ChunkSize = Math.Min(64, worldSize) };
        _state = new WorldState(worldSize, maxHouseholds: 4096, maxBuildings: 2048,
            maxRoadNodes: 8192, maxVehicles: 512);
        _eventBus = new EventBus();

        _economy = new EconomySystem();
        _population = new PopulationSystem();
        _traffic = new TrafficSystem();
        _services = new ServiceSystem(worldSize);
        _zoneGrowth = new ZoneGrowthSystem(worldSize);
        _budget = new BudgetSystem();
        _politics = new PoliticsSystem();
        _research = new ResearchSystem();
        _events = new EventSystem(seed: 12345);
        _culturalDna = new CulturalDNASystem();

        _budget.SetEventBus(_eventBus);
        _economy.SetEventBus(_eventBus);

        CulturalDNASystem.ApplyPreset(_state, "western_european");
        _lastCulturalDnaYear = _state.Year;

        GenerateMap();
        SeedStarterCity();
        SeedStartingPopulation();

        IsInitialized = true;
    }

    public void Tick(double dt)
    {
        if (!IsInitialized) return;

        _state.TickCount++;

        _trafficAccumulator += dt;
        while (_trafficAccumulator >= TrafficInterval)
        {
            _trafficAccumulator -= TrafficInterval;
            _traffic.Tick(_state, TrafficInterval);
        }

        _dayAccumulator += dt;
        while (_dayAccumulator >= DayInterval)
        {
            _dayAccumulator -= DayInterval;
            RunDayTick();
        }
    }

    public string GetRenderSnapshotJson()
    {
        if (!IsInitialized) return "{}";

        var snap = SimSnapshot.CaptureFrom(_state);
        var dto = SimSnapshotDto.From(snap, _state);
        return JsonSerializer.Serialize(dto, JsonContext.Default.SimSnapshotDto);
    }

    private void RunDayTick()
    {
        _economy.DailyTick(_state, DayInterval);
        _services.DailyTick(_state, DayInterval);
        _zoneGrowth.Tick(_state, _economy);
        _events.DailyTick(_state);
        _events.UpdateEvents(_state, 1f);
        _state.TickEvents();
        _politics.DailyTick(_state, DayInterval);

        if (_state.AdvanceDay())
            RunMonthTick();
    }

    private void RunMonthTick()
    {
        _services.MonthlyTick(_state);
        FeedCrossSystemData();
        _population.MonthlyTick(_state);
        _economy.MonthlyTick(_state, DayInterval);
        _zoneGrowth.RecalculateLandValue(_state);
        _zoneGrowth.CheckUpgrades(_state);
        _budget.CalculateMonthlyBudget(_state, _economy);
        _politics.MonthlyTick(_state, DayInterval);
        _research.MonthlyTick(_state, DayInterval);

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

        for (int x = cx - 8; x <= cx + 8; x++)
        {
            _state.Tiles.RoadFlags[_state.Tiles.Index(x, cy)] = 1;
            _state.Roads.AddNode(x, cy);
        }

        for (int y = cy - 6; y <= cy + 6; y++)
        for (int x = cx - 6; x <= cx + 6; x++)
        {
            if (!_state.Tiles.InBounds(x, y)) continue;
            byte terrain = _state.Tiles.TerrainType[_state.Tiles.Index(x, y)];
            if (terrain == (byte)TerrainId.Water) continue;

            int dist = Math.Abs(x - cx) + Math.Abs(y - cy);
            byte zone = dist < 3 ? (byte)3 : (byte)1; // commercial core, residential ring
            _state.Tiles.ZoneType[_state.Tiles.Index(x, y)] = zone;
        }

        var rng = new Random(7);
        for (int i = 0; i < 12; i++)
        {
            int x = cx + rng.Next(-5, 6);
            int y = cy + rng.Next(-5, 6);
            if (!_state.Tiles.InBounds(x, y)) continue;

            int slot = _state.Buildings.Allocate();
            if (slot < 0) break;

            _state.Buildings.GridX[slot] = x;
            _state.Buildings.GridY[slot] = y;
            _state.Buildings.Width[slot] = 1;
            _state.Buildings.Height[slot] = 1;
            _state.Buildings.TypeId[slot] = (ushort)(10 + rng.Next(0, 5));
            _state.Buildings.Level[slot] = (byte)rng.Next(1, 4);
            _state.Buildings.State[slot] = 1;
            _state.Buildings.Occupants[slot] = (ushort)rng.Next(1, 20);
            _state.Buildings.MaxOccupants[slot] = 40;
        }
    }

    private void SeedStartingPopulation()
    {
        const int initialHouseholds = 50;
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
    public BuildingDto[] Buildings { get; init; } = [];

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

        return new SimSnapshotDto
        {
            Tick = snap.TickCount,
            Buildings = buildings,
        };
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

[JsonSerializable(typeof(SimSnapshotDto))]
[JsonSerializable(typeof(BuildingDto))]
internal partial class JsonContext : JsonSerializerContext;
