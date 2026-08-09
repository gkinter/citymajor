using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Engine.Data;
using Forge.Engine.Simulation;
using Forge.Game.Simulation;
using Forge.SimCore;

namespace Forge.SimWasm;

/// <summary>
/// Browser WASM facade: JSON export and save/load over <see cref="SimHost"/>.
/// </summary>
public sealed class WasmSimHost
{
    private readonly SimHost _host = new();

    public bool IsInitialized => _host.IsInitialized;
    public long TickCount => _host.TickCount;
    public int Population => _host.State?.Population ?? 0;
    public int HouseholdCount => _host.State?.Households.Count ?? 0;
    public int PopulationGrowthRate => _host.Population?.LastMonthlyPopulationGrowth ?? 0;
    public long CityFunds => _host.State?.CityFunds ?? 0;
    public int Era => _host.State?.Era ?? 0;
    public float ResearchPoints => _host.State?.ResearchPoints ?? 0f;
    public float ResearchRate => _host.State?.ResearchRate ?? 0f;
    public int EventDefinitionCount => _host.Events?.Definitions.Count ?? 0;
    public int LawDefinitionCount => _host.Laws?.DefinitionCount ?? 0;
    public int ActiveLawCount => _host.Laws?.ActiveLawCount ?? 0;
    public int UnlockedTechCount =>
        _host.State is null ? 0 : ResearchSystem.CountUnlockedTechs(_host.State);
    public int CurrentResearchId => _host.State?.CurrentResearchId ?? -1;
    public float CurrentResearchProgress => _host.State?.CurrentResearchProgress ?? 0f;
    public float CurrentResearchMonthsRemaining
    {
        get
        {
            var research = _host.Research;
            var state = _host.State;
            if (research is null || state is null) return 0f;
            if (research.ResearchQueue[0] == -1) return 0f;
            float rate = state.ResearchRate;
            if (rate <= 0f) return -1f;
            int techId = research.ResearchQueue[0];
            float cost = research.GetEffectiveCost(techId);
            float remaining = Math.Max(0f, cost - research.QueueProgress[0]);
            return remaining / rate;
        }
    }
    public WasmTrafficMode TrafficMode => WasmTrafficMode.Lite;
    public ActiveEventDto[] ActiveEvents => CollectActiveEvents();
    public float ResidentialDemand => _host.Economy?.ResidentialDemand ?? 0f;
    public float CommercialDemand => _host.Economy?.CommercialDemand ?? 0f;
    public float IndustrialDemand => _host.Economy?.IndustrialDemand ?? 0f;
    public float ApprovalRating => _host.State?.ApprovalRating ?? 0f;
    public float Happiness => _host.State?.Happiness ?? 0f;
    public long MonthlyIncome => _host.State?.Income.Total ?? 0;
    public long MonthlyExpenses => _host.State?.Expenses.Total ?? 0;
    public int BuildingCount => _host.State?.Buildings.Count ?? 0;
    public int WorldSize => _host.WorldSize;
    public EraProgressSnapshot EraProgress =>
        _host.State is null || _host.Research is null
            ? new EraProgressSnapshot()
            : WasmEraDeriver.GetEraProgress(_host.State, _host.Research);
    public EconomySnapshotDto EconomySnapshot =>
        EconomySnapshotDto.From(_host.Economy);
    public float MonthlyExportValue => _host.Trade?.MonthlyExportValue ?? 0f;
    public float MonthlyImportCost => _host.Trade?.MonthlyImportCost ?? 0f;
    public float TradeBalance => _host.Trade?.TradeBalance ?? 0f;
    public float HealthcareCoverage =>
        _host.Services is null || _host.State is null
            ? 0f
            : _host.ComputeAverageCoverage(_host.Services.HealthCoverage);
    public float PoliceCoverage =>
        _host.Services is null || _host.State is null
            ? 0f
            : _host.ComputeAverageCoverage(_host.Services.PoliceCoverage);
    public float FireCoverage =>
        _host.Services is null || _host.State is null
            ? 0f
            : _host.ComputeAverageCoverage(_host.Services.FireCoverage);
    public float EducationCoverage =>
        _host.Services is null || _host.State is null
            ? 0f
            : _host.ComputeAverageCoverage(_host.Services.EducationCoverage);
    public float EmploymentRate => _host.State?.EmploymentRate ?? 0f;
    public float MeanTrafficDensity => _host.State?.MeanTrafficDensity ?? 0f;
    public int ConstructingBuildingCount => _host.State?.ConstructingBuildingCount ?? 0;
    public float PowerCoverageFraction => _host.State?.PowerCoverageFraction ?? 1f;
    public float WaterCoverageFraction => _host.State?.WaterCoverageFraction ?? 1f;
    public float UtilityStressIndex => _host.State?.UtilityStressIndex ?? 0f;
    public float GoodsShortageIndex => _host.State?.GoodsShortageIndex ?? 0f;
    public float GoodsSurplusIndex => _host.State?.GoodsSurplusIndex ?? 0f;
    public float InterZoneTradeVolume => _host.State?.InterZoneTradeVolume ?? 0f;
    public float MeanInterZoneFriction => _host.State?.MeanInterZoneFriction ?? 1f;

    internal SimHost InnerHost => _host;

    public int[] CollectUnlockedTechIds()
    {
        if (_host.State is null) return [];
        var ids = new List<int>();
        for (int i = 0; i < ResearchSystem.MaxTechnologies; i++)
        {
            if (_host.State.IsTechUnlocked(i)) ids.Add(i);
        }
        return ids.ToArray();
    }

    public void Init(int worldSize = WasmConfig.DefaultWorldSize, bool skipStarterCity = false)
    {
        _host.Init(worldSize, new SimHostInitOptions
        {
            SkipStarterCity = skipStarterCity,
            EmbeddedDataReader = WasmEmbeddedData.Read,
        });
    }

    public void Tick(double dt) => _host.Tick(dt);

    public string GetRenderSnapshotJson() => _host.GetSnapshotJson();

    public string GetStatusJson()
    {
        if (!IsInitialized)
            return JsonSerializer.Serialize(new WasmStatusDto(), JsonContext.Default.WasmStatusDto);

        return JsonSerializer.Serialize(WasmStatusDto.From(this), JsonContext.Default.WasmStatusDto);
    }

    public PopulationL2Dto GetPopulationL2Export()
    {
        if (!IsInitialized || _host.Population is null || _host.State is null)
            return new PopulationL2Dto();
        return PopulationL2Dto.From(_host.State, _host.Population);
    }

    public void PaintZone(int x, int y, byte zoneType) => _host.PaintZone(x, y, zoneType);

    public void Bulldoze(int x, int y) => _host.Bulldoze(x, y);

    public void PlaceRoad(int x, int y) => _host.PlaceRoad(x, y);

    public bool PlaceBuilding(int x, int y, int typeId) => _host.PlaceBuilding(x, y, typeId);

    public bool EnqueueResearch(int techId) => _host.EnqueueResearch(techId);

    public bool SetLawActive(string lawId, bool active) => _host.SetLawActive(lawId, active);

    public LawPreviewDto? GetSampleLawPreview()
    {
        var laws = _host.Laws;
        if (laws is null || laws.DefinitionCount == 0) return null;

        var definition = laws.Definitions[0];
        return new LawPreviewDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Active = laws.IsActive(0),
        };
    }

    public void AdjustBudget(long deltaFunds) => _host.AdjustBudget(deltaFunds);

    public void ApplyApprovalDelta(float deltaPercent) => _host.ApplyApprovalDelta(deltaPercent);

    public void BoostResearch(float points) => _host.BoostResearch(points);

    public bool ResolveHeraldEvent(int eventId) => _host.ResolveHeraldEvent(eventId);

    public bool LoadSnapshotFromJson(string json) => _host.LoadSnapshotFromJson(json);

    public byte[] ExportCmjrBytes(string cityName = "City") =>
        _host.ExportCmjrBytes(cityName);

    public bool LoadFromCmjrBytes(ReadOnlySpan<byte> data) => _host.LoadFromCmjrBytes(data);

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

    private ActiveEventDto[] CollectActiveEvents()
    {
        var events = _host.Events;
        if (events is null) return [];

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
    public float HealthcareCoverage { get; init; }
    public float PoliceCoverage { get; init; }
    public float FireCoverage { get; init; }
    public float EducationCoverage { get; init; }
    public EconomySnapshotDto Economy { get; init; } = new();
    public float MonthlyExportValue { get; init; }
    public float MonthlyImportCost { get; init; }
    public float TradeBalance { get; init; }
    public PopulationL2Dto PopulationL2 { get; init; } = new();
    /// <summary>Share of working-age households with a workplace (0–1).</summary>
    public float EmploymentRate { get; init; }
    public float MeanTrafficDensity { get; init; }
    public int ConstructingBuildingCount { get; init; }
    public float PowerCoverageFraction { get; init; } = 1f;
    public float WaterCoverageFraction { get; init; } = 1f;
    public float UtilityStressIndex { get; init; }
    public float GoodsShortageIndex { get; init; }
    public float GoodsSurplusIndex { get; init; }
    public float InterZoneTradeVolume { get; init; }
    public float MeanInterZoneFriction { get; init; } = 1f;

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
        EmploymentRate = host.EmploymentRate,
        MeanTrafficDensity = host.MeanTrafficDensity,
        ConstructingBuildingCount = host.ConstructingBuildingCount,
        PowerCoverageFraction = host.PowerCoverageFraction,
        WaterCoverageFraction = host.WaterCoverageFraction,
        UtilityStressIndex = host.UtilityStressIndex,
        GoodsShortageIndex = host.GoodsShortageIndex,
        GoodsSurplusIndex = host.GoodsSurplusIndex,
        InterZoneTradeVolume = host.InterZoneTradeVolume,
        MeanInterZoneFriction = host.MeanInterZoneFriction,
    };
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ActiveEventDto))]
[JsonSerializable(typeof(ActiveEventDto[]))]
[JsonSerializable(typeof(LawPreviewDto))]
[JsonSerializable(typeof(EconomySnapshotDto))]
[JsonSerializable(typeof(GoodImbalanceDto))]
[JsonSerializable(typeof(GoodImbalanceDto[]))]
[JsonSerializable(typeof(PopulationL2Dto))]
[JsonSerializable(typeof(HouseholdPreviewDto))]
[JsonSerializable(typeof(HouseholdPreviewDto[]))]
[JsonSerializable(typeof(WasmStatusDto))]
[JsonSerializable(typeof(TickIntervalsDto))]
[JsonSerializable(typeof(TrafficLiteInfoDto))]
[JsonSerializable(typeof(EraProgressSnapshot))]
[JsonSerializable(typeof(EraProgressGate))]
[JsonSerializable(typeof(EraProgressGate[]))]
[JsonSerializable(typeof(string[]))]
internal partial class JsonContext : JsonSerializerContext;
