using System.Text.Json;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
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
            snap, _state, _events, _economy, _services, _population, _research, _laws,
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

    /// <summary>
    /// Cathedral U3.5 — mean commute minutes, O-D assignment coverage, mean commute satisfaction (0–1).
    /// Prefer full TrafficSystem average when enabled; otherwise population graph cost rollup.
    /// </summary>
    public (float MeanCommuteMinutes, float CommuterCoverage, float MeanCommuteSatisfaction) CollectCommuteHudMetrics()
    {
        if (!IsInitialized || _state is null || _population is null)
            return (0f, 0f, 0f);

        var audit = _population.AuditCommuters(_state);
        float meanSat = _population.AuditMeanCommuteSatisfaction(_state) / 100f;
        float meanMin = _useFullTraffic && _fullTraffic is not null && _fullTraffic.AverageCommuteMinutes > 0f
            ? _fullTraffic.AverageCommuteMinutes
            : _population.AuditMeanCommuteMinutes(_state);

        return (meanMin, audit.Coverage, meanSat);
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

        // Prefer saved pool ids so Household L2 home/work ids stay valid after load.
        var buildingIdMap = new Dictionary<int, int>(dto.Buildings.Length);
        foreach (var building in dto.Buildings)
        {
            if (!_state.Tiles.InBounds(building.TileX, building.TileZ)) continue;

            int slot = _state.Buildings.AllocateAt(building.Id);
            if (slot < 0) break;

            buildingIdMap[building.Id] = slot;
            _state.Buildings.GridX[slot] = building.TileX;
            _state.Buildings.GridY[slot] = building.TileZ;
            _state.Buildings.Width[slot] = 1;
            _state.Buildings.Height[slot] = 1;
            _state.Buildings.TypeId[slot] = building.TypeId;
            _state.Buildings.Level[slot] = building.Level == 0 ? (byte)1 : building.Level;
            _state.Buildings.State[slot] = building.State == 0 ? (byte)1 : building.State;
            _state.Buildings.Condition[slot] = building.Condition == 0 ? (byte)255 : building.Condition;
            _state.Buildings.FireRisk[slot] = building.FireRisk;
            _state.Buildings.ServiceFlags[slot] = building.ServiceFlags;
            _state.Buildings.Occupants[slot] = 0;
            _state.Buildings.MaxOccupants[slot] = 48;

            int tileIdx = _state.Tiles.Index(building.TileX, building.TileZ);
            _state.Tiles.BuildingId[tileIdx] = (ushort)slot;
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

        // Cathedral wave restores — active law ids (+ Mults), Event*Mult, delivery delay, vacancy, abandoned, L2 sample.
        RestoreActiveLaws(dto);
        RestoreLawMultipliers(dto);
        RestoreEventMultipliers(dto);
        RestoreActiveEvents(dto);
        _economy.RestoreMeanGoodsDeliveryDelay(dto.MeanGoodsDeliveryDelay, _state);
        _state.ResidentialVacancy = float.IsFinite(dto.ResidentialVacancy)
            ? Math.Clamp(dto.ResidentialVacancy, 0f, 1f)
            : 1f;
        _state.AbandonedBuildingCount = ZoneGrowthSystem.CountAbandonedBuildings(_state);
        _state.ConstructingBuildingCount = dto.ConstructingBuildingCount;
        _state.MeanInterZoneFriction = dto.MeanInterZoneFriction > 0f
            ? dto.MeanInterZoneFriction
            : 1f;
        _state.GoodsTransportCostIndex = Math.Clamp(dto.GoodsTransportCostIndex, 0f, 1f);
        _state.MarketZoneCount = Math.Clamp(dto.MarketZoneCount, 1, 16);

        if (dto.PopulationL2?.Households is { Length: > 0 })
        {
            var rows = new PopulationSystem.HouseholdSampleRow[dto.PopulationL2.Households.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var preview = dto.PopulationL2.Households[i];
                rows[i] = new PopulationSystem.HouseholdSampleRow
                {
                    Id = preview.Id,
                    TileX = preview.TileX,
                    TileZ = preview.TileZ,
                    Happiness = preview.Happiness,
                    CommuteMin = preview.CommuteMin,
                    HomeBuildingId = RemapBuildingId(buildingIdMap, preview.HomeBuildingId),
                    WorkBuildingId = RemapBuildingId(buildingIdMap, preview.WorkBuildingId),
                    RentBurden = preview.RentBurden,
                };
            }

            _population.RestoreHouseholdSampleRows(_state, rows);
        }

        ResetTickAccumulators();
        RebuildServicesAfterLoad();
        // FireRisk + ServiceFlags on restored buildings feed ActiveFireCount / hydrant / EMS
        // via ServiceSystem.DailyTick — no DTO override needed when geometry is present.
    }

    private static int RemapBuildingId(Dictionary<int, int> map, int savedId)
    {
        if (savedId <= 0) return 0;
        return map.TryGetValue(savedId, out int mapped) ? mapped : savedId;
    }

    private void RestoreActiveLaws(SimSnapshotDto dto)
    {
        if (_laws is null || _state is null) return;

        _laws.ClearActive();
        if (dto.ActiveLawIds is { Length: > 0 })
        {
            foreach (var lawId in dto.ActiveLawIds)
            {
                if (string.IsNullOrWhiteSpace(lawId)) continue;
                _laws.SetActive(lawId, true);
            }
        }

        // Re-rollup Mults from restored toggles; RestoreLawMultipliers may override with saved scalars.
        RecomputeLawEffects();
    }

    private void RestoreLawMultipliers(SimSnapshotDto dto)
    {
        // Match RecomputeLawEffects clamps — traffic allows a deeper floor (0.1).
        _state.LawTrafficCapacityMult = ClampLawMult(dto.LawTrafficCapacityMult, min: 0.1f);
        _state.LawConstructionSpeedMult = ClampLawMult(dto.LawConstructionSpeedMult);
        _state.LawSpawnDemandMult = ClampLawMult(dto.LawSpawnDemandMult);
        _state.LawResidentialSpawnMult = ClampLawMult(dto.LawResidentialSpawnMult);
        _state.LawIndustrialSpawnMult = ClampLawMult(dto.LawIndustrialSpawnMult);
        _state.LawCommercialSpawnMult = ClampLawMult(dto.LawCommercialSpawnMult);
    }

    private void RestoreEventMultipliers(SimSnapshotDto dto)
    {
        _state.EventTaxRevenueMult = ClampEventMult(dto.EventTaxRevenueMult);
        _state.EventImmigrationMult = ClampEventMult(dto.EventImmigrationMult);
        _state.EventCommercialSpawnMult = ClampEventMult(dto.EventCommercialSpawnMult);
        _state.EventProductivityMult = ClampEventMult(dto.EventProductivityMult);
        _state.EventResearchMult = ClampEventMult(dto.EventResearchMult);
        _state.EventSpawnDemandMult = ClampEventMult(dto.EventSpawnDemandMult);
    }

    private static float ClampLawMult(float value, float min = 0.25f) =>
        float.IsFinite(value) ? Math.Clamp(value, min, 3f) : 1f;

    private static float ClampEventMult(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0.25f, 3f) : 1f;

    private void RestoreActiveEvents(SimSnapshotDto dto)
    {
        if (dto.ActiveEvents is not { Length: > 0 })
            return;

        _events.ClearAllEvents(_state);
        foreach (var evt in dto.ActiveEvents)
        {
            if (string.IsNullOrWhiteSpace(evt.TypeId)) continue;
            _events.TriggerEvent(
                evt.TypeId,
                _state,
                severityOverride: evt.Severity > 0f ? evt.Severity : null,
                tileX: evt.TileX,
                tileY: evt.TileY);
        }

        // Re-apply saved Mults after spawn — Brewing phase would otherwise soften aggregates.
        RestoreEventMultipliers(dto);
    }
}
