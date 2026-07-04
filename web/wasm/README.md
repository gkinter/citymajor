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
  "householdCount": 128,
  "cityFunds": 51200,
  "era": 0,
  "residentialDemand": 0.42,
  "commercialDemand": -0.15,
  "industrialDemand": 0.08,
  "approval": 62.5,
  "happiness": 0.68,
  "monthlyIncome": 12400,
  "monthlyExpenses": 9800,
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
  "householdCount": 128,
  "cityFunds": 51200,
  "era": 0,
  "eraName": "Frontier",
  "residentialDemand": 0.42,
  "commercialDemand": -0.15,
  "industrialDemand": 0.08,
  "approval": 62.5,
  "happiness": 0.68,
  "monthlyIncome": 12400,
  "monthlyExpenses": 9800,
  "researchPoints": 12.5,
  "researchRate": 3.2,
  "trafficMode": "lite",
  "tickIntervals": {
    "gameDaySeconds": 1.0,
    "gameMonthSeconds": 30.0,
    "trafficLiteSeconds": 5.0,
    "trafficLiteEdgeBatchSeconds": 0.5
  },
  "trafficLite": {
    "zoneCount": 64,
    "frankWolfeIterations": 4,
    "edgeBatchCount": 4
  },
  "systems": ["EconomySystem", "WasmTrafficLite", "..."],
  "stubbed": ["TrafficSystem full (500 zones — desktop only)", "..."]
}
```

- `tileZ` is the grid Y axis (3D world Z)
- `state`: 0=constructing, 1=operational, 2=abandoned, 3=demolishing

## Simulation systems

**Tick schedule (SB-3690 partial):**

| Layer | Interval | Systems |
|-------|----------|---------|
| L0 (sub-day) | `TrafficLiteInterval` (5.0 sim s) | `WasmTrafficLite` — 64-zone Frank-Wolfe BPR (4 iter max) |
| L0b (edge refresh) | `TrafficLiteEdgeBatchInterval` (0.5 sim s) | Partial BPR on ¼ of edges — no full O-D / FW |
| L1 (game day) | `GameDayInterval` (1.0 sim s) | `EconomySystem.DailyTick`, `ServiceSystem`, `ZoneGrowthSystem`, `PoliticsSystem`, events |
| L2 (month) | `GameMonthInterval` (30.0 sim s) | `PopulationSystem.MonthlyTick` (migration, births/deaths), `BudgetSystem`, `ResearchSystem`, land-value recalc, **era derivation** |

`WasmSimHost` accumulates sim time: `GameDayInterval` drives L1 daily ticks; `GameMonthInterval` (30 game days) drives L2 month ticks including `PopulationSystem.MonthlyTick` and staggered per-tick satisfaction updates (1/30 of households per sim tick). Population, household count, city treasury, **RCI demand** (`EconomySystem`), **approval / happiness** (`PoliticsSystem` / `WorldState`), and **monthly budget ledgers** (`BudgetSystem`) are exposed via `GetStatus()` and `GetRenderSnapshot()`.

**Era derivation (SB-3692 partial):** Without `tech_tree.json`, `WasmEraDeriver` sets `era` from tick count and research proxies (accumulated RP, heavy-industry tiles, educated population). The primary threshold is **Frontier → Industrial** (`EraIndustrialTickThreshold` = 1200 ticks, or RP ≥ 25 / heavy industry ≥ 3). HUD uses visual era names (Frontier … Future) with per-era badge colors.

**Included (linked via `Forge.SimCore`):**

- `EconomySystem`, `PopulationSystem`, `ServiceSystem`
- `WasmTrafficLite` — BPR-lite traffic (SB-3685 partial; see performance budget below)
- `ZoneGrowthSystem`, `BudgetSystem`, `PoliticsSystem`, `EventSystem`, `ResearchSystem`, `CulturalDNASystem`
- Engine data: `WorldState`, `SimSnapshot`, `MapGenerator`, tile/building pools

**Traffic modes (`GetStatus().trafficMode`):**

| Mode | Where | Description |
|------|-------|-------------|
| `stub` | Legacy spike | No-op placeholder (removed in SB-3685) |
| `lite` | Browser WASM | 64 zones, 4 Frank-Wolfe iterations, gravity O-D from zone buildings |
| `full` | Desktop | Full `TrafficSystem` (~500 zones, MNL mode choice, 5 FW iterations @ 2 Hz) |

**Stubbed / degraded in spike:**

- `TrafficSystem` full — desktop only; WASM uses `lite` mode
- `TradeSystem` — not wired in `WasmSimHost` (`ProductionChain` runs inside `EconomySystem.DailyTick`)
- `SimulationLoop` background thread — replaced by single-threaded `WasmSimHost`
- Full 512×512 world — WASM v1 uses 256×256 (`WasmConfig.DefaultWorldSize`)

## Performance budget (SB-3685)

The R3F play page runs the sim worker at **8 Hz** (125 ms per tick). Traffic must not block that loop.

| Work | Schedule | Target cost |
|------|----------|-------------|
| `WasmSimHost.Tick` (economy, population, …) | Every 8 Hz tick | ≤ 10 ms total |
| `WasmTrafficLite.Tick` (full FW) | Every 5 sim s (`TrafficLiteInterval`) | ≤ 2 ms when it fires |
| `WasmTrafficLite.TickEdgeBatch` | Every 0.5 sim s | ≤ 0.2 ms |

Full Frank-Wolfe runs **at most once per 5 sim seconds**, not every 8 Hz tick. Between full runs, only a rotating ¼ of road edges get a cheap BPR congestion refresh. Desktop `TrafficSystem` (~500 zones, per-household O-D, 5 FW iterations at 2 Hz) remains native-only.

Benchmark locally: open browser devtools → Performance tab → record 30 s of 8 Hz ticking on a 256×256 city with starter roads. Lite traffic spikes should appear as isolated ≤ 2 ms frames every ~40 sim ticks (5 s × 8 Hz), not sustained blocking.

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
