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
        int tickEra = EraFromTick(state.TickCount);
        int researchEra = EraFromResearch(state, research);
        state.Era = Math.Max(tickEra, researchEra);
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
}
