# CityMajor WASM simulation spike (SB-3661 / SB-3683 partial)

Standalone proof that C# simulation code runs in the browser and emits render snapshots.

## Prerequisites

- .NET 8 SDK (or .NET 10 SDK with `wasm-tools-net8` workload)
- WASM workloads: `dotnet workload restore src/Forge.SimWasm/Forge.SimWasm.csproj`
  - On .NET 10 hosts this installs both `wasm-tools` and `wasm-tools-net8`
- Node.js 20+ (for Vite demo page)

## Build WASM

From repo root (clear `NODE_OPTIONS` if emcc fails with `--max-semi-space-size`):

```bash
unset NODE_OPTIONS
dotnet workload restore src/Forge.SimWasm/Forge.SimWasm.csproj

# `dotnet publish` can hit MSB1008 on .NET 10 SDK — use msbuild Publish target:
dotnet msbuild src/Forge.SimWasm/Forge.SimWasm.csproj \
  -t:Publish \
  -p:Configuration=Release \
  -p:RuntimeIdentifier=browser-wasm \
  -p:SelfContained=true \
# Or use the helper script:
./web/wasm/build-wasm.sh
```

The publish output must include `_framework/blazor.boot.json` (copy from `AppBundle/`, not a flat `-o` publish).

## Run demo

```bash
cd web/wasm
npm install
npm run dev
```

Open http://localhost:5174 — click **Run 1000 ticks**. The worker loads `Forge.SimWasm` and ticks at ~12 Hz, logging snapshot summaries every 50 ticks.

## Export API (JS interop)

| Export | Description |
|--------|-------------|
| `Init(worldSize)` | Creates 64×64 (default) world, seeds map + starter city |
| `Tick(dtSeconds)` | Advances simulation; returns `tickCount` |
| `GetRenderSnapshot()` | JSON string — see schema below |
| `GetStatus()` | JSON metadata: included vs stubbed systems |

## Render snapshot schema

Matches `web/lib/sim-bridge.ts` `SimSnapshot` in citymajor-web-r3f-spike:

```json
{
  "tick": 1000,
  "buildings": [
    {
      "id": 3,
      "typeId": 12,
      "tileX": 32,
      "tileZ": 31,
      "level": 2,
      "state": 1,
      "condition": 100
    }
  ]
}
```

- `tileZ` is the grid Y axis (3D world Z)
- `state`: 0=constructing, 1=operational, 2=abandoned, 3=demolishing

## Simulation systems

**Included (linked via `Forge.SimCore`):**

- `EconomySystem`, `PopulationSystem`, `TrafficSystem`, `ServiceSystem`
- `ZoneGrowthSystem`, `BudgetSystem`, `PoliticsSystem`, `CulturalDNASystem`
- Engine data: `WorldState`, `SimSnapshot`, `MapGenerator`, tile/building pools

**Stubbed / degraded in spike:**

- `EventSystem` — no `events.json` bundled in WASM
- `ResearchSystem` — no `tech_tree.json` bundled
- `TradeSystem`, `ProductionChain` — not wired in `WasmSimHost`
- `SimulationLoop` background thread — replaced by single-threaded `WasmSimHost`
- Full 512×512 world — spike uses 64×64 for browser performance

## COOP / COEP and SharedArrayBuffer

This spike does **not** use `SharedArrayBuffer` or .NET pthreads. No special headers are required for local dev.

If you add **SharedArrayBuffer** (e.g. for pthread-based `SimulationLoop` or shared snapshot buffers), the page must be cross-origin isolated:

| Header | Value |
|--------|--------|
| `Cross-Origin-Opener-Policy` | `same-origin` |
| `Cross-Origin-Embedder-Policy` | `require-corp` |

Uncomment the headers in `vite.config.ts` `server.headers`, or configure your CDN/origin similarly. All subresources (WASM, workers, scripts) must be same-origin or send `Cross-Origin-Resource-Policy: cross-origin`.

Without COOP+COEP, `SharedArrayBuffer` is undefined and pthread WASM builds will fail at runtime.

## Project layout

```
src/Forge.SimCore/     # Engine-agnostic sim library (no SDL/ImGui)
src/Forge.SimWasm/     # browser-wasm entry + JSExport API
web/wasm/              # Vite demo + worker (sibling to future R3F app)
```
