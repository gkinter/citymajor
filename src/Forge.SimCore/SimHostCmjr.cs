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

        // Capture without RefreshHousingSnapshotMetrics: Layer-C restore seeds households
        // sparsely, so a re-rollup would wipe MeanRentBurden restored by ApplySnapshotDto.
        // Live HUD still refreshes via GetSnapshot(); month tick keeps the aggregate fresh.
        var snap = SimSnapshot.CaptureFrom(_state);
        var (edgeVolumes, edgeTravelTimes) = CollectTrafficEdgeExport();
        var (carShare, transitShare, walkShare) = CollectModeShares();
        var dto = SimSnapshotDto.From(
            snap, _state, _events, _economy, _services, _population, _research,
            edgeVolumes, edgeTravelTimes,
            carShare, transitShare, walkShare);
        return JsonSerializer.Serialize(dto, SnapshotJsonContext.Default.SimSnapshotDto);
    }

    private (float[] EdgeVolumes, float[] EdgeTravelTimes) CollectTrafficEdgeExport()
    {
        if (_useFullTraffic && _fullTraffic is not null)
            return (_fullTraffic.EdgeVolumes, _fullTraffic.EdgeTravelTimes);
        return (_traffic.EdgeVolumes, _traffic.EdgeTravelTimes);
    }

    /// <summary>City-wide car/transit/walk shares from active traffic model (0–1).</summary>
    public (float Car, float Transit, float Walk) CollectModeShares()
    {
        if (_useFullTraffic && _fullTraffic is not null)
        {
            return (
                _fullTraffic.CarModeShare,
                _fullTraffic.TransitModeShare,
                _fullTraffic.WalkModeShare);
        }

        return (_traffic.CarModeShare, _traffic.TransitModeShare, _traffic.WalkModeShare);
    }

    /// <summary>Per-edge assignment export for snapshot/status (P1.6 / P4.2).</summary>
    public (float[] EdgeVolumes, float[] EdgeTravelTimes) GetTrafficEdgeExport() =>
        CollectTrafficEdgeExport();

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
        }

        RebuildRoadGraphFromTiles();

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

        _population.RestoreMeanRentBurden(dto.MeanRentBurden, _state);
        _politics.RestoreCouncilSeats(dto.CouncilSeats, _state);
        _traffic.RestoreModeShares(dto.CarModeShare, dto.TransitModeShare, dto.WalkModeShare);
        _fullTraffic?.RestoreModeShares(
            dto.CarModeShare, dto.TransitModeShare, dto.WalkModeShare);

        ResetTickAccumulators();
        RebuildServicesAfterLoad();
    }
}
