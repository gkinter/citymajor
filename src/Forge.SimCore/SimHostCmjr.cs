using System.Text.Json;
using Forge.Engine.Simulation;
using Forge.SimWasm;

namespace Forge.SimCore;

public sealed partial class SimHost
{
    public byte[] ExportCmjrBytes(string cityName = "City") => CmjrSave.Export(this, cityName);

    public bool LoadFromCmjrBytes(ReadOnlySpan<byte> data) => CmjrSave.Load(this, data);

    public string GetSnapshotJson()
    {
        if (!IsInitialized) return "{}";

        var snap = GetSnapshot();
        var dto = SimSnapshotDto.From(
            snap, _state, _events, _economy, _services, _population, _research);
        return JsonSerializer.Serialize(dto, SnapshotJsonContext.Default.SimSnapshotDto);
    }

    public bool LoadSnapshotFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        SimSnapshotDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SimSnapshotDto);
        }
        catch
        {
            return false;
        }

        if (dto is null) return false;

        int size = WorldSize > 0 ? WorldSize : WasmConfig.DefaultWorldSize;
        Init(size, _initOptions);
        ApplySnapshotDto(dto);
        return true;
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

        RestoreResearchState(
            dto.ResearchPoints,
            dto.CurrentResearchId,
            dto.CurrentResearchProgress,
            dto.UnlockedTechIds,
            dto.ResearchQueue,
            dto.QueueProgress,
            dto.EurekaBonuses,
            dto.BranchingChoices);

        ResetTickAccumulators();
        RebuildServicesAfterLoad();
    }
}
