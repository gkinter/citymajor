namespace Forge.SimWasm;

/// <summary>
/// WASM spike world sizing (SB-3683). Browser target is 256×256 v1.
/// </summary>
public static class WasmConfig
{
    public const int DefaultWorldSize = 256;
    public const int MinWorldSize = 32;
    public const int MaxWorldSize = 256;
    public const int ChunkSize = 64;

    /// <summary>Starter buildings at init — scaled from world area, capped for pool limits.</summary>
    public const int TargetStarterBuildings = 220;

    /// <summary>Traffic tick interval in sim seconds (stub runs at most this often).</summary>
    public const double TrafficStubInterval = 2.0;

    /// <summary>
    /// L1 simulation interval in sim seconds — one game day. EconomySystem.DailyTick runs
    /// when this accumulator elapses (SB-3690).
    /// </summary>
    public const double GameDayInterval = 1.0;

    // Era derivation thresholds (SB-3692 partial — visual eras, no tech_tree.json)

    /// <summary>Sim ticks before tick-only path can reach Industrial era.</summary>
    public const long EraIndustrialTickThreshold = 1_200;

    public const long EraPostwarTickThreshold = 4_800;
    public const long EraModernTickThreshold = 12_000;
    public const long EraFutureTickThreshold = 24_000;

    /// <summary>Accumulated RP before research path can reach Industrial era.</summary>
    public const float EraIndustrialResearchPoints = 25f;

    public const int EraIndustrialMinPopulation = 400;
    public const int EraIndustrialEducatedPop = 80;
    public const int EraIndustrialHeavyIndustry = 3;

    public const float EraPostwarResearchPoints = 120f;
    public const int EraPostwarMinPopulation = 2_000;

    public const float EraModernResearchPoints = 400f;
    public const int EraModernMinPopulation = 8_000;

    public const float EraFutureResearchPoints = 1_000f;
    public const int EraFutureMinPopulation = 20_000;
}
