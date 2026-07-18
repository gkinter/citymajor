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

    /// <summary>WP-E / v1-scale tick budget: households to seed (pool cap 10,240).</summary>
    public const int V1ScaleTargetHouseholds = 8_200;

    /// <summary>WP-E / v1-scale tick budget: buildings to seed (pool cap 5,120).</summary>
    public const int V1ScaleTargetBuildings = 3_200;

    /// <summary>WP-E / v1-scale tick budget: total population for RestoreHouseholds (~3 members/HH).</summary>
    public const int V1ScaleTargetPopulation = 24_600;

    /// <summary>Legacy stub interval — superseded by <see cref="TrafficLiteInterval"/>.</summary>
    public const double TrafficStubInterval = 2.0;

    /// <summary>
    /// Full BPR-lite Frank-Wolfe interval in sim seconds (SB-3685).
    /// Runs at most this often — never every 8 Hz sim tick.
    /// </summary>
    public const double TrafficLiteInterval = 5.0;

    /// <summary>Zone count for lite O-D matrix (8×8 on 256×256 browser WASM world).</summary>
    public const int TrafficLiteZoneCount = 64;

    /// <summary>Zone count for Unity / desktop lite traffic (11×11 on 128×128, 16×16 on 256×256).</summary>
    public const int TrafficLiteZoneCountUnity = 128;

    /// <summary>
    /// When true, Unity uses full <c>TrafficSystem</c> (500-zone FW + MNL) instead of lite.
    /// Override at runtime with env <c>CITYMAJOR_FULL_TRAFFIC=1</c>.
    /// </summary>
    public const bool UseFullTrafficUnityDefault = false;

    /// <summary>Max Frank-Wolfe iterations per lite traffic tick.</summary>
    public const int TrafficLiteFrankWolfeIterations = 4;

    /// <summary>Edge batches for partial BPR refresh between full lite ticks.</summary>
    public const int TrafficLiteEdgeBatchCount = 4;

    /// <summary>Partial edge BPR refresh interval in sim seconds (cheap subset update).</summary>
    public const double TrafficLiteEdgeBatchInterval = 0.5;

    /// <summary>
    /// L1 simulation interval in sim seconds — one game day. EconomySystem.DailyTick runs
    /// when this accumulator elapses (SB-3690).
    /// </summary>
    public const double GameDayInterval = 1.0;

    /// <summary>
    /// L2 simulation interval in sim seconds — one game month (30 game days).
    /// PopulationSystem.MonthlyTick and other month-tier systems run when this elapses (SB-3689).
    /// </summary>
    public const double GameMonthInterval = GameDayInterval * 30.0;

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
