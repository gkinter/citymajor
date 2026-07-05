using Forge.Engine.Simulation;
using Forge.Game.Simulation;

namespace Forge.SimWasm;

/// <summary>
/// HUD/visual era helpers for the WASM spike. Era transitions follow
/// <see cref="ResearchSystem.CheckEraTransition"/> — population + unlocked tech
/// count gates from <see cref="ResearchSystem.EraRequirements"/> (SB-3718).
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
    /// Apply <see cref="ResearchSystem.CheckEraTransition"/> — no building-count proxies.
    /// Prefer letting <see cref="ResearchSystem.MonthlyTick"/> set era; this is a sync helper.
    /// </summary>
    public static void UpdateEra(WorldState state, ResearchSystem research)
    {
        int newEra = research.CheckEraTransition(state);
        if (newEra >= 0)
            state.Era = newEra;
    }

    public static string EraName(int era)
    {
        if (era < 0) return "Unknown";
        if (era >= EraNames.Length) return EraNames[^1];
        return EraNames[era];
    }

    /// <summary>
    /// Player-facing progress toward the next era — gates mirror
    /// <see cref="ResearchSystem.EraRequirements"/> (population AND tech count).
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
        float percent = ComputePercent(gates);

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
        int nextEra = currentEra + 1;
        if (nextEra >= ResearchSystem.EraRequirements.Length)
            return [];

        var req = ResearchSystem.EraRequirements[nextEra];
        int pop = state.Population;
        int techCount = ResearchSystem.CountUnlockedTechs(state);

        return
        [
            Gate("population", "Population", pop, req.MinPopulation,
                pop >= req.MinPopulation),
            Gate("techCount", "Technologies researched", techCount, req.RequiredTechCount,
                techCount >= req.RequiredTechCount),
        ];
    }

    private static float ComputePercent(EraProgressGate[] gates)
    {
        if (gates.Length == 0) return 100f;

        float minRatio = 1f;
        foreach (var gate in gates)
        {
            float ratio = gate.Required <= 0 ? 1f : Math.Clamp(gate.Current / gate.Required, 0f, 1f);
            minRatio = Math.Min(minRatio, ratio);
        }

        return Math.Clamp(minRatio * 100f, 0f, 100f);
    }

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
