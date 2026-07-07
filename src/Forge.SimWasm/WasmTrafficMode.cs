namespace Forge.SimWasm;

/// <summary>Traffic fidelity tier exposed via <see cref="WasmExports.GetStatus"/>.</summary>
public enum WasmTrafficMode
{
    /// <summary>No BPR — placeholder only (legacy spike).</summary>
    Stub,

    /// <summary>64-zone Frank-Wolfe with capped iterations — browser WASM budget (SB-3685).</summary>
    Lite,

    /// <summary>Full <see cref="Forge.Game.Simulation.TrafficSystem"/> — desktop builds only.</summary>
    Full,
}
