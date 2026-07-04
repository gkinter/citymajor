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
}
