using Forge.Engine.Simulation;
using Forge.Game.Simulation;

namespace Forge.SimWasm;

/// <summary>
/// Derives HUD/visual era from tick count and research proxies when tech_tree.json
/// is not bundled in the WASM spike (SB-3692 partial).
/// </summary>
public static class WasmEraDeriver
{
    public static readonly string[] EraNames =
    [
        "Frontier",
        "Industrial",
        "Postwar",
        "Modern",
        "Future",
    ];

    /// <summary>
    /// Recompute <see cref="WorldState.Era"/> from tick + research signals.
    /// Uses the higher of tick-based and research-based tier so either path can advance era.
    /// </summary>
    public static void UpdateEra(WorldState state, ResearchSystem research)
    {
        // Research gates only — tick thresholds are shown in eraProgress HUD but
        // must not bypass RP/pop/industry requirements (SB-3718 / SB-3692).
        state.Era = EraFromResearch(state, research);
    }

    public static string EraName(int era)
    {
        if (era < 0) return "Unknown";
        if (era >= EraNames.Length) return EraNames[^1];
        return EraNames[era];
    }

    private static int EraFromTick(long tickCount)
    {
        if (tickCount >= WasmConfig.EraFutureTickThreshold) return 4;
        if (tickCount >= WasmConfig.EraModernTickThreshold) return 3;
        if (tickCount >= WasmConfig.EraPostwarTickThreshold) return 2;
        if (tickCount >= WasmConfig.EraIndustrialTickThreshold) return 1;
        return 0;
    }

    private static int EraFromResearch(WorldState state, ResearchSystem research)
    {
        float rp = state.ResearchPoints;
        int pop = state.Population;
        int heavyIndustry = research.HeavyIndustryCount;
        int educated = research.EducatedPopulation;

        if (rp >= WasmConfig.EraFutureResearchPoints
            && pop >= WasmConfig.EraFutureMinPopulation)
            return 4;

        if (rp >= WasmConfig.EraModernResearchPoints
            && pop >= WasmConfig.EraModernMinPopulation)
            return 3;

        if (rp >= WasmConfig.EraPostwarResearchPoints
            && pop >= WasmConfig.EraPostwarMinPopulation)
            return 2;

        // Frontier → Industrial: primary threshold for WASM partial
        if (rp >= WasmConfig.EraIndustrialResearchPoints
            || heavyIndustry >= WasmConfig.EraIndustrialHeavyIndustry
            || (pop >= WasmConfig.EraIndustrialMinPopulation
                && educated >= WasmConfig.EraIndustrialEducatedPop))
            return 1;

        return 0;
    }

    /// <summary>
    /// Player-facing progress toward the next era — gates mirror <see cref="WasmConfig"/> thresholds.
    /// </summary>
    public static EraProgressSnapshot GetEraProgress(WorldState state, ResearchSystem research)
    {
        int currentEra = state.Era;
        if (currentEra >= EraNames.Length - 1)
        {
            return new EraProgressSnapshot
            {
                NextEra = currentEra,
                NextEraName = EraNames[currentEra],
                Percent = 100,
                Gates = [],
            };
        }

        int nextEra = currentEra + 1;
        var gates = BuildGatesForNextEra(currentEra, state, research);
        float percent = ComputePercent(currentEra, state, research, gates);

        return new EraProgressSnapshot
        {
            NextEra = nextEra,
            NextEraName = EraNames[nextEra],
            Percent = percent,
            Gates = gates,
        };
    }

    private static EraProgressGate[] BuildGatesForNextEra(
        int currentEra,
        WorldState state,
        ResearchSystem research)
    {
        float rp = state.ResearchPoints;
        int pop = state.Population;
        int heavyIndustry = research.HeavyIndustryCount;
        int educated = research.EducatedPopulation;

        return currentEra switch
        {
            0 =>
            [
                Gate("researchPoints", "Research", rp, WasmConfig.EraIndustrialResearchPoints,
                    rp >= WasmConfig.EraIndustrialResearchPoints),
                Gate("population", "Population", pop, WasmConfig.EraIndustrialMinPopulation,
                    pop >= WasmConfig.EraIndustrialMinPopulation
                        && educated >= WasmConfig.EraIndustrialEducatedPop),
                Gate("heavyIndustry", "Industry", heavyIndustry, WasmConfig.EraIndustrialHeavyIndustry,
                    heavyIndustry >= WasmConfig.EraIndustrialHeavyIndustry),
            ],
            1 =>
            [
                Gate("researchPoints", "Research", rp, WasmConfig.EraPostwarResearchPoints,
                    rp >= WasmConfig.EraPostwarResearchPoints),
                Gate("population", "Population", pop, WasmConfig.EraPostwarMinPopulation,
                    pop >= WasmConfig.EraPostwarMinPopulation),
            ],
            2 =>
            [
                Gate("researchPoints", "Research", rp, WasmConfig.EraModernResearchPoints,
                    rp >= WasmConfig.EraModernResearchPoints),
                Gate("population", "Population", pop, WasmConfig.EraModernMinPopulation,
                    pop >= WasmConfig.EraModernMinPopulation),
            ],
            3 =>
            [
                Gate("researchPoints", "Research", rp, WasmConfig.EraFutureResearchPoints,
                    rp >= WasmConfig.EraFutureResearchPoints),
                Gate("population", "Population", pop, WasmConfig.EraFutureMinPopulation,
                    pop >= WasmConfig.EraFutureMinPopulation),
            ],
            _ => [],
        };
    }

    private static float ComputePercent(
        int currentEra,
        WorldState state,
        ResearchSystem research,
        EraProgressGate[] gates)
    {
        if (gates.Length == 0) return 100f;

        float rp = state.ResearchPoints;
        int pop = state.Population;
        int heavyIndustry = research.HeavyIndustryCount;
        int educated = research.EducatedPopulation;

        float pathProgress = currentEra switch
        {
            // Frontier → Industrial: OR across three paths (matches EraFromResearch).
            0 => Math.Max(
                Ratio(rp, WasmConfig.EraIndustrialResearchPoints),
                Math.Max(
                    Ratio(heavyIndustry, WasmConfig.EraIndustrialHeavyIndustry),
                    Math.Min(
                        Ratio(pop, WasmConfig.EraIndustrialMinPopulation),
                        Ratio(educated, WasmConfig.EraIndustrialEducatedPop)))),
            // Later eras: AND across RP + population.
            1 => Math.Min(
                Ratio(rp, WasmConfig.EraPostwarResearchPoints),
                Ratio(pop, WasmConfig.EraPostwarMinPopulation)),
            2 => Math.Min(
                Ratio(rp, WasmConfig.EraModernResearchPoints),
                Ratio(pop, WasmConfig.EraModernMinPopulation)),
            3 => Math.Min(
                Ratio(rp, WasmConfig.EraFutureResearchPoints),
                Ratio(pop, WasmConfig.EraFutureMinPopulation)),
            _ => 1f,
        };

        return Math.Clamp(pathProgress * 100f, 0f, 100f);
    }

    private static float Ratio(float current, float required) =>
        required <= 0 ? 1f : Math.Clamp(current / required, 0f, 1f);

    private static EraProgressGate Gate(
        string id,
        string label,
        float current,
        float required,
        bool met) =>
        new()
        {
            Id = id,
            Label = label,
            Current = current,
            Required = required,
            Met = met,
        };
}

public sealed class EraProgressGate
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public float Current { get; init; }
    public float Required { get; init; }
    public bool Met { get; init; }
}

public sealed class EraProgressSnapshot
{
    public int NextEra { get; init; }
    public string NextEraName { get; init; } = "";
    public float Percent { get; init; }
    public EraProgressGate[] Gates { get; init; } = [];
}
