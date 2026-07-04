using Forge.Engine.Simulation;

namespace Forge.SimWasm;

/// <summary>
/// Lightweight traffic placeholder for browser WASM — skips BPR / Frank-Wolfe assignment.
/// Full <see cref="Forge.Game.Simulation.TrafficSystem"/> remains in desktop builds only.
/// </summary>
public sealed class WasmTrafficStub
{
    public void Tick(WorldState state, double dt)
    {
        // No O-D matrix, no pathfinding — keeps 256×256 WASM ticks within budget.
        _ = state;
        _ = dt;
    }
}
