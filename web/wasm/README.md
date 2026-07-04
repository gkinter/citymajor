# CityMajor WASM simulation spike (SB-3661 / SB-3683 / SB-3690 partial)

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
| `Init(worldSize)` | Creates 256×256 (default) world, seeds map + starter city (~220 buildings) |
| `Tick(dtSeconds)` | Advances simulation; returns `tickCount` |
| `GetRenderSnapshot()` | JSON string — see schema below |
| `GetStatus()` | JSON metadata: tick count, population, city funds, era, tick intervals, included vs stubbed systems |

## Render snapshot schema

Matches `web/lib/sim-bridge.ts` `SimSnapshot` in citymajor-web-r3f-spike:

```json
{
  "tick": 1000,
  "population": 842,
  "cityFunds": 51200,
  "era": 0,
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
  ],
  "zones": [
    { "tileX": 120, "tileZ": 128, "zoneType": 1 }
  ]
}
```

`GetStatus()` returns live counters without a full snapshot:

```json
{
  "initialized": true,
  "tickCount": 1000,
  "population": 842,
  "cityFunds": 51200,
  "era": 0,
  "eraName": "Frontier",
  "researchPoints": 12.5,
  "researchRate": 3.2,
  "tickIntervals": {
    "gameDaySeconds": 1.0,
    "trafficStubSeconds": 2.0
  },
  "systems": ["EconomySystem", "PopulationSystem", "..."],
  "stubbed": ["TrafficSystem (WasmTrafficStub — no BPR assignment in browser)", "..."]
}
```

- `tileZ` is the grid Y axis (3D world Z)
- `state`: 0=constructing, 1=operational, 2=abandoned, 3=demolishing

## Simulation systems

**Tick schedule (SB-3690 partial):**

| Layer | Interval | Systems |
|-------|----------|---------|
| L0 (sub-day) | `TrafficStubInterval` (2.0 sim s) | `WasmTrafficStub` — road occupancy only, no BPR |
| L1 (game day) | `GameDayInterval` (1.0 sim s) | `EconomySystem.DailyTick`, `ServiceSystem`, `ZoneGrowthSystem`, `PoliticsSystem`, events |
| L2 (month) | every 30 game days | `PopulationSystem`, `BudgetSystem`, `ResearchSystem`, land-value recalc, **era derivation** |

`WasmSimHost` accumulates sim time and calls `EconomySystem.DailyTick` once per game day. Population and city treasury are exposed via `GetStatus()` and `GetRenderSnapshot()`.

**Era derivation (SB-3692 partial):** Without `tech_tree.json`, `WasmEraDeriver` sets `era` from tick count and research proxies (accumulated RP, heavy-industry tiles, educated population). The primary threshold is **Frontier → Industrial** (`EraIndustrialTickThreshold` = 1200 ticks, or RP ≥ 25 / heavy industry ≥ 3). HUD uses visual era names (Frontier … Future) with per-era badge colors.

**Included (linked via `Forge.SimCore`):**

- `EconomySystem`, `PopulationSystem`, `ServiceSystem`
- `ZoneGrowthSystem`, `BudgetSystem`, `PoliticsSystem`, `CulturalDNASystem`
- Engine data: `WorldState`, `SimSnapshot`, `MapGenerator`, tile/building pools

**Stubbed / degraded in spike:**

- `TrafficSystem` — replaced by `WasmTrafficStub` (no BPR / Frank-Wolfe in browser)
- `EventSystem` — no `events.json` bundled in WASM
- `ResearchSystem` — no `tech_tree.json` bundled
- `TradeSystem`, `ProductionChain` — not wired in `WasmSimHost`
- `SimulationLoop` background thread — replaced by single-threaded `WasmSimHost`
- Full 512×512 world — WASM v1 uses 256×256 (`WasmConfig.DefaultWorldSize`)

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
