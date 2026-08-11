# WASM Sim Bridge — Design Contract

**Date:** 2026-07-04  
**Status:** Current (web v1 integration spine)  
**Closes:** [GAP_AUDIT_DESIGN_DOCS](./GAP_AUDIT_DESIGN_DOCS.md) fix #7  
**Implementation:** `web/lib/sim-bridge.ts`, `web/workers/sim-worker.ts`, `src/Forge.SimWasm/`

---

## 1. Purpose

The WASM sim bridge is the boundary between the **Next.js / R3F client** (main thread) and the **Forge.SimWasm** C# simulation (Web Worker + dotnet browser bundle). It defines:

1. **Worker message protocol** — how the client boots, commands, and receives state from the sim.
2. **Tick cadence** — sim advances at 8 Hz; render snapshots publish at up to 4 Hz.
3. **`SimCommand` / `SimSnapshot` schemas** — the typed contract shared by TypeScript and C# JSON exports.
4. **COOP / COEP requirements** — headers needed for future `SharedArrayBuffer` / pthread WASM.
5. **Procedural fallback contract** — behavior when WASM assets or worker init fail.

Deploy and build implications (Docker `BUILD_WASM`, preview verification) live in [`docs/DEPLOY_WEB.md`](../DEPLOY_WEB.md). Standalone spike notes and WASM system inclusion matrix live in [`web/wasm/README.md`](../../web/wasm/README.md).

---

## 2. Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Main thread (CityCanvas, HUD, R3F)                              │
│  createSimBridge() → postMessage commands                       │
│  onSnapshot() → cityDataFromSnapshot() → BuildingInstances      │
└───────────────────────────┬─────────────────────────────────────┘
                            │ WorkerInbound / WorkerOutbound
                            │ (structured clone — no SAB in v1)
┌───────────────────────────▼─────────────────────────────────────┐
│ web/workers/sim-worker.ts                                       │
│  RAF loop sends { type: "tick", deltaMs } at display rate       │
│  Accumulates sim time → sim.Tick(0.125) at 8 Hz                 │
│  Publishes SimSnapshot at ≤4 Hz (full) or 10 Hz (resources)     │
│  Optimistic zone/road grids merged into WASM snapshots          │
└───────────────────────────┬─────────────────────────────────────┘
                            │ JS interop (JSON strings)
┌───────────────────────────▼─────────────────────────────────────┐
│ Forge.SimWasm (browser-wasm dotnet bundle at /dotnet/)          │
│  Init · Tick · GetRenderSnapshot · GetStatus                      │
│  PaintZone · Bulldoze · PlaceRoad (optional exports)            │
└─────────────────────────────────────────────────────────────────┘
```

**Key files**

| Layer | Path |
|-------|------|
| Client API | `web/lib/sim-bridge.ts` |
| Worker | `web/workers/sim-worker.ts` |
| Snapshot → R3F | `web/lib/city-data.ts` (`cityDataFromSnapshot`) |
| Play integration | `web/components/city/CityCanvas.tsx` |
| C# DTO | `src/Forge.SimWasm/SimSnapshotDto.cs` |
| TS DTO contract | `web/packages/sim-types/src/simSnapshot.ts` (`@citymajor/sim-types`) |
| WASM build / demo | `web/wasm/README.md`, `web/public/dotnet/` |

---

## 3. Worker protocol

Communication uses `postMessage` with structured-clone payloads. **v1 does not transfer `SharedArrayBuffer`** — all state crosses the worker boundary as JSON-parsed `SimSnapshot` objects.

### 3.1 Main thread → worker (`WorkerInbound`)

| `type` | Payload | Behavior |
|--------|---------|----------|
| `init` | `{ wasmBaseUrl: string; worldSize: number }` | Loads dotnet bundle from `wasmBaseUrl` (default `/dotnet`), calls `Init(worldSize)`, posts first snapshot, then `{ type: "ready" }`. On failure posts `{ type: "error", message }`. |
| `command` | `{ command: SimCommand }` | Dispatches a sim command (see §5). |
| `dispose` | — | Clears sim reference, zone/road grids, and cached snapshot. Main thread also calls `worker.terminate()`. |

### 3.2 Worker → main thread (`WorkerOutbound`)

| `type` | Payload | When |
|--------|---------|------|
| `ready` | — | WASM `Init` succeeded; client may start RAF tick loop. |
| `snapshot` | `{ snapshot: SimSnapshot }` | After init, after sim ticks (rate-limited), or immediately after zone/road/bulldoze commands. |
| `error` | `{ message: string }` | WASM load or `Init` failure; client should fall back to procedural (§8). |

### 3.3 Client lifecycle (`createSimBridge`)

1. `init(wasmUrl?, worldSize?)` — spawns worker, awaits `ready` or rejects on `error`.
2. Registers persistent `message` listener for `snapshot` events.
3. Main thread RAF loop sends `{ type: "tick", deltaMs }` every frame (typically 60 Hz).
4. Worker accumulates `deltaMs × speedLevel` and runs WASM ticks at 8 Hz (§4).
5. `send(command)` — forwards `SimCommand` to worker.
6. `getSnapshot()` / `onSnapshot(cb)` — latest cached snapshot on main thread.
7. `dispose()` — posts `dispose`, terminates worker.

`CityCanvas` starts the RAF tick loop only after `bridge.init()` resolves. Game speed is controlled via `set_speed` / `pause` / `resume` commands.

---

## 4. Tick rates

Sim time is **decoupled from display refresh**. The worker receives RAF `deltaMs` but only calls `sim.Tick(dt)` when accumulated time crosses fixed sim-step boundaries.

| Layer | Rate | Constant | Notes |
|-------|------|----------|-------|
| **Display RAF** | ~60 Hz | — | Main thread; only sends `tick` commands with `deltaMs`. |
| **Sim tick** | **8 Hz** | `SIM_TICK_HZ = 8`, `SIM_TICK_MS = 125` | Each tick: `sim.Tick(0.125)` seconds of sim time. |
| **Full snapshot** | **≤ 4 Hz** | `SNAPSHOT_MIN_MS = 250` | Calls `GetRenderSnapshot()` + merge zone/road grids. |
| **Resource-only update** | **≤ 10 Hz** | `RESOURCE_MIN_MS = 100` | Between full snapshots, calls `GetStatus()` and merges counters into `lastPublished` snapshot (buildings/zones/roads unchanged). |

### 4.1 Game speed

`speedLevel`: `0` = paused, `1` = 1×, `2` = 2×, `3` = 3×.

When `speedLevel > 0`, incoming `deltaMs` is multiplied by `speedLevel` before accumulation. Multiple 8 Hz sim steps may run in one RAF frame if the tab was backgrounded or speed is >1.

### 4.2 Why these rates

| Concern | Choice |
|---------|--------|
| 256×256 WASM budget | 8 Hz keeps `WasmSimHost.Tick` under ~10 ms/frame on target hardware. |
| React + R3F churn | Full JSON snapshot parse + `cityDataFromSnapshot` at 60 Hz would dominate main thread. |
| HUD responsiveness | `GetStatus()` is lighter than `GetRenderSnapshot()`; 10 Hz keeps population/funds/tick fresh between 4 Hz geometry updates. |

See [`web/wasm/README.md`](../../web/wasm/README.md) §Performance budget for C#-side traffic lite scheduling (5 sim s Frank-Wolfe, not every 8 Hz tick).

---

## 5. `SimCommand` schema

Defined in `web/lib/sim-bridge.ts`. All commands are sent as `{ type: "command", command }`.

```typescript
export type GameSpeedLevel = 0 | 1 | 2 | 3;

export type SimCommand =
  | { type: "tick"; deltaMs: number }
  | { type: "place_building"; tileX: number; tileZ: number; typeId: number }
  | { type: "place_road"; tileX: number; tileZ: number }
  | { type: "zone_paint"; tileX: number; tileZ: number; zoneType: number }
  | { type: "bulldoze"; tileX: number; tileZ: number }
  | { type: "set_speed"; level: GameSpeedLevel }
  | { type: "pause" }
  | { type: "resume" };
```

| Command | Worker behavior | WASM export |
|---------|-----------------|-------------|
| `tick` | Accumulate time; run 8 Hz ticks; rate-limit snapshots | `Tick(dtSeconds)` |
| `set_speed` | Clamp level 0–3 | — |
| `pause` | `speedLevel = 0` | — |
| `resume` | If paused, `speedLevel = 1` | — |
| `zone_paint` | Update optimistic `zoneGrid`; call `PaintZone`; publish snapshot immediately | `PaintZone(x, y, zoneType)` |
| `bulldoze` | Clear zone in `zoneGrid`; call `Bulldoze`; publish snapshot | `Bulldoze(x, y)` |
| `place_road` | Set `roadGrid` flag; call `PlaceRoad`; publish snapshot | `PlaceRoad(x, y)` |
| `place_building` | **No-op in v1** — growth is tick-driven via `ZoneGrowthSystem` | *(planned)* |

### 5.1 Zone type IDs

Engine zone types match `TileData.cs` / `web/lib/zoning.ts`:

| `zoneType` | Meaning |
|------------|---------|
| `0` | None / bulldozed |
| `1` | Residential low |
| `2` | Residential high |
| `3` | Commercial |
| `4` | Industrial |
| `5` | Office |
| `6` | Mixed |

Toolbar `ENGINE_ZONE_TYPE` maps UI tools to `1`, `3`, `4` for residential, commercial, industrial.

---

## 6. `SimSnapshot` schema

**Cathedral field matrix (P7.1):** [`SIM_SNAPSHOT_V2.md`](./SIM_SNAPSHOT_V2.md) — engine `SimSnapshot`, WASM `SimSnapshotDto`, and Unity `CitySimState` ownership/cadence.

**Canonical TypeScript contract:** `@citymajor/sim-types` → `web/packages/sim-types/src/simSnapshot.ts` (mirrors tip `SimSnapshotDto.cs`). Archival harness types in `web/lib/sim-bridge.ts` extend the same camelCase JSON shape (optional fields for resource-merge / GetStatus extras). C# source of truth: `src/Forge.SimWasm/SimSnapshotDto.cs`. WASM emits **camelCase JSON** from `GetRenderSnapshot()`.

### 6.1 `SimResources` (counters)

Resource-only merges and HUD consumers use a scalar subset of the DTO (plus optional GetStatus extras such as `eraProgress`, `researchRate`, `sampleLaw`). Core counters:

```typescript
export type SimResources = {
  tick: number;
  population: number;
  householdCount?: number;
  cityFunds: number;
  era: number; // 0=Frontier … 4=Future
  residentialDemand?: number;
  commercialDemand?: number;
  industrialDemand?: number;
  approval?: number; // percent 0–100
  happiness?: number;
  monthlyIncome?: number;
  monthlyExpenses?: number;
  tradeBalance?: number;
  monthlyExportValue?: number;
  monthlyImportCost?: number;
  // … Event*Mult, Law*Mult, utilities, fire/EMS — see §6.2 / simSnapshot.ts
};
```

### 6.2 `SimSnapshotDto` (full render state — tip contract)

```typescript
/** Mirrors Forge.SimWasm.SimSnapshotDto — @citymajor/sim-types */
export type SimSnapshotDto = {
  tick: number;
  population: number;
  householdCount: number;
  cityFunds: number;
  era: number;
  residentialDemand: number;
  commercialDemand: number;
  industrialDemand: number;
  approval: number; // percent 0–100
  happiness: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  tradeBalance: number;
  monthlyExportValue: number;
  monthlyImportCost: number;

  buildings: BuildingDto[];
  zones: ZoneDto[];
  roads: RoadDto[];
  roadGraph: RoadGraphSnapshotDto;
  traffic: TrafficDto[];
  serviceCoverage: ServiceCoverageDto[];
  frictionCorridors: FrictionCorridorDto[];
  activeEvents: ActiveEventDto[];
  activeEventCount: number;

  activeLawIds: string[];
  activeOrdinances: number; // ulong bitfield
  nextElectionYear: number;
  lawTrafficCapacityMult: number;
  lawConstructionSpeedMult: number;
  lawSpawnDemandMult: number;
  lawResidentialSpawnMult: number;
  lawIndustrialSpawnMult: number;
  lawCommercialSpawnMult: number;

  eventTaxRevenueMult: number;
  eventImmigrationMult: number;
  eventCommercialSpawnMult: number;
  eventProductivityMult: number;
  eventResearchMult: number;
  eventSpawnDemandMult: number;

  economy: EconomySnapshotDto;
  populationL2: PopulationL2Dto;

  researchPoints: number;
  currentResearchId: number;
  currentResearchProgress: number;
  unlockedTechIds: number[];
  researchQueue: number[];
  queueProgress: number[];
  eurekaBonuses: Record<string, number>;
  branchingChoices: Record<string, number>;

  constructingBuildingCount: number;
  abandonedBuildingCount: number;
  employmentRate: number;
  meanTrafficDensity: number;

  powerCoverageFraction: number;
  waterCoverageFraction: number;
  blackoutFraction: number;
  waterShortageFraction: number;
  utilityStressIndex: number;

  goodsShortageIndex: number;
  goodsSurplusIndex: number;
  interZoneTradeVolume: number;
  meanInterZoneFriction: number;
  goodsTransportCostIndex: number;
  meanGoodsDeliveryDelay: number;

  meanRentBurden: number;
  residentialVacancy: number;

  meanEmergencyResponseMinutes: number;
  hydrantCoverageFraction: number;
  activeFireCount: number;
  meanEmsSurvivalRate: number;
  hospitalBedOccupancyFraction: number;
  availableHospitalBeds: number;
  wildfireRiskIndex: number;
  activeWildfireTileCount: number;
  arsonRiskIndex: number;
  arsonRingActive: boolean;
  lookoutTowerCount: number;
  aerialFirefightingAvailable: boolean;
  fireSafetyRating: number; // 1–10
  fireInsurancePremiumMult: number;
  meanEducationLevel: number; // 0–3
  educationCoverageFraction: number;
  meanParkAccess: number; // 0–1
  parkAccessFraction: number;
  healthCoverageFraction: number; // 0–1
  meanHealthSatisfaction: number; // 0–1
  policeCoverageFraction: number; // 0–1
  meanCrimeRate: number; // 0–1
  meanSafetySatisfaction: number; // 0–1
  wasteCoverageFraction: number; // 0–1
  meanPollution: number; // 0–1
  meanEnvironmentScore: number; // 0–1
  landfillUtilizationFraction: number; // 0–1+
  recyclingDiversionRate: number; // 0–1
  landfillOverflowRate: number; // 0–1
  sewageCoverageFraction: number; // 0–1
  meanWaterContamination: number; // 0–1
  meanWaterQuality: number; // 0–1
  meanPipeUtilization: number; // 0–1+
  csoOverflowRate: number; // 0–1
  stormRunoffLoad: number; // 0–1+
  internetCoverageFraction: number; // 0–1
  meanInternetTier: number; // 0–3
  meanTelecomAccess: number; // 0–1

  marketZoneCount: number;
  commuterCoverage: number;
  commuteOdSample: CommuteOdSampleDto[];
  councilSeats: number[]; // length 9
  culturalDna: number[]; // length 8, −1…+1

  carModeShare: number;
  transitModeShare: number;
  walkModeShare: number;
  transitLineCount: number;
  busCoverage: number;
};
```

Harness `SimSnapshot` in `sim-bridge.ts` is `SimResources & { buildings; zones?; roads?; roadGraph?; traffic?; serviceCoverage?; frictionCorridors? }` — same wire JSON, optional geometry for resource-only merges.

### 6.3 Nested types

```typescript
export type BuildingDto = {
  id: number; // building pool slot
  typeId: number; // ushort — see BUILDING_ARCHETYPE_3D.md
  tileX: number;
  tileZ: number; // grid Y → world Z
  level: number;
  state: number; // 0=constructing, 1=operational, 2=abandoned, 3=demolishing
  condition: number; // 0–255
  fireRisk?: number; // P5.3 burning intensity
  serviceFlags?: number; // P5 service bitmask
};

export type ZoneDto = {
  tileX: number;
  tileZ: number;
  zoneType: number;
};

export type RoadDto = {
  tileX: number;
  tileZ: number;
  roadFlags: number;
};

export type RoadGraphSnapshotDto = {
  nodeCount: number;
  nodeTypes: number[];
  nodeTileX: number[];
  nodeTileZ: number[];
  edgeCount: number;
  edgeFrom: number[];
  edgeTo: number[];
  edgeVolumes: number[];
  travelTimes: number[];
};

export type TrafficDto = {
  tileX: number;
  tileZ: number;
  density: number; // ≥ 0.01 exported
};

export type ServiceCoverageDto = {
  tileX: number;
  tileZ: number;
  health: number;
  police: number;
  fire: number;
  education: number;
};

export type FrictionCorridorDto = {
  tileX: number;
  tileZ: number;
  friction: number; // 0–1
};

export type ActiveEventDto = {
  eventId: number;
  typeId: string;
  phase: string;
  severity: number;
  tileX: number;
  tileY: number;
};

export type EconomySnapshotDto = {
  shortages: GoodImbalanceDto[];
  surpluses: GoodImbalanceDto[];
  flows: GoodFlowDto[];
  marketZoneCount: number;
  marketZonePrices: MarketZonePriceDto[];
};

export type PopulationL2Dto = {
  households: HouseholdPreviewDto[]; // ≤100
};

export type CommuteOdSampleDto = {
  homeTileX: number;
  homeTileZ: number;
  workTileX: number;
  workTileZ: number;
  tripCount: number;
};
```

### 6.4 Example JSON (`GetRenderSnapshot`)

```json
{
  "tick": 1000,
  "population": 842,
  "householdCount": 128,
  "cityFunds": 51200,
  "era": 0,
  "approval": 62.5,
  "tradeBalance": 4200,
  "monthlyExportValue": 8000,
  "monthlyImportCost": 3800,
  "activeLawIds": ["zoning_reform"],
  "lawSpawnDemandMult": 1.05,
  "eventTaxRevenueMult": 0.95,
  "blackoutFraction": 0,
  "waterShortageFraction": 0,
  "abandonedBuildingCount": 0,
  "activeFireCount": 0,
  "wildfireRiskIndex": 0.12,
  "fireSafetyRating": 5,
  "fireInsurancePremiumMult": 1,
  "meanEducationLevel": 1.2,
  "educationCoverageFraction": 0.55,
  "meanParkAccess": 0.42,
  "parkAccessFraction": 0.38,
  "healthCoverageFraction": 0.48,
  "meanHealthSatisfaction": 0.61,
  "policeCoverageFraction": 0.55,
  "meanCrimeRate": 0.18,
  "meanSafetySatisfaction": 0.72,
  "wasteCoverageFraction": 0.48,
  "meanPollution": 0.22,
  "meanEnvironmentScore": 0.78,
  "landfillUtilizationFraction": 0.42,
  "recyclingDiversionRate": 0.35,
  "landfillOverflowRate": 0.02,
  "sewageCoverageFraction": 0.52,
  "meanWaterContamination": 0.2,
  "meanWaterQuality": 0.8,
  "meanPipeUtilization": 0.62,
  "csoOverflowRate": 0.08,
  "stormRunoffLoad": 0.22,
  "internetCoverageFraction": 0.58,
  "meanInternetTier": 1.8,
  "meanTelecomAccess": 0.6,
  "culturalDna": [0, 0, 0, 0, 0, 0, 0, 0],
  "councilSeats": [0, 0, 0, 0, 0, 0, 0, 0, 0],
  "buildings": [
    {
      "id": 3,
      "typeId": 12,
      "tileX": 32,
      "tileZ": 31,
      "level": 2,
      "state": 1,
      "condition": 100,
      "fireRisk": 0,
      "serviceFlags": 0
    }
  ],
  "zones": [
    { "tileX": 120, "tileZ": 128, "zoneType": 1 }
  ],
  "roads": [
    { "tileX": 64, "tileZ": 64, "roadFlags": 1 }
  ]
}
```

### 6.5 `GetStatus()` (lightweight metadata)

Not part of `SimSnapshot` on the wire, but merged by the worker for resource-only updates:

```json
{
  "initialized": true,
  "tickCount": 1000,
  "population": 842,
  "householdCount": 128,
  "cityFunds": 51200,
  "era": 0,
  "eraName": "Frontier",
  "researchPoints": 12.5,
  "researchRate": 3.2,
  "trafficMode": "lite",
  "tickIntervals": { "gameDaySeconds": 1.0, "gameMonthSeconds": 30.0 },
  "systems": ["EconomySystem", "WasmTrafficLite"],
  "stubbed": ["TrafficSystem full (desktop only)"]
}
```

**Known gap (v1):** `researchPoints`, `researchRate`, and `techCount` are emitted by C# but not yet propagated through `readStatus()` → `SimResources` → HUD. See [TECH_TREE_GAP_ANALYSIS](./TECH_TREE_GAP_ANALYSIS.md).

### 6.6 Optimistic zone/road merge

When WASM zone/road arrays are empty or lag behind UI paint, the worker maintains **optimistic `Uint8Array` grids** (`worldSize²`) and merges them into every published snapshot:

- WASM zones/roads win on tile collision for WASM-provided tiles.
- Worker grid fills gaps for tiles the player painted before WASM reflects them.
- Zone/road/bulldoze commands trigger an **immediate** full snapshot (bypasses 250 ms throttle).

### 6.7 Snapshot → rendering

`cityDataFromSnapshot(snapshot)` in `web/lib/city-data.ts` maps each `BuildingSnapshot` to a `BuildingInstance`:

- `typeId` → `archetypeKey`, `category`, `era`, `stories` via `@citymajor/sim-types`
- Chunk indices for frustum LOD (64 chunks × 32×32 tiles)
- Same instancing path as procedural fallback — only the **data source** differs

### 6.8 Planned extensions

[GAMEPLAY_LOOP_IMPROVEMENTS](./GAMEPLAY_LOOP_IMPROVEMENTS.md) proposes adding further HUD-facing fields. **Do not add fields without updating `SimSnapshotDto.cs`, `@citymajor/sim-types` `simSnapshot.ts`, and [`SIM_SNAPSHOT_V2.md`](./SIM_SNAPSHOT_V2.md).** Web harness remains archival — prefer Unity `CitySimState` for product UI.

---

## 7. WASM export API

| Export | Signature | Description |
|--------|-----------|-------------|
| `Init` | `(worldSize: number) => void` | Power-of-two clamped 32–256; seeds map + ~220 starter buildings. |
| `Tick` | `(dtSeconds: number) => number` | Advances sim; returns tick count. |
| `GetRenderSnapshot` | `() => string` | JSON `SimSnapshot`. |
| `GetStatus` | `() => string` | JSON metadata (optional but expected in integration). |
| `PaintZone` | `(x, y, zoneType) => void` | Optional — worker degrades gracefully if missing. |
| `Bulldoze` | `(x, y) => void` | Optional. |
| `PlaceRoad` | `(x, y) => void` | Optional. |

Bundle layout: `web/public/dotnet/_framework/blazor.boot.json` + `Forge.SimWasm.dll` interop. Loaded via `dotnet.js` `create()` in the worker.

---

## 8. COOP / COEP and SharedArrayBuffer

### 8.1 Current v1 (no SAB)

The integration spine **does not use `SharedArrayBuffer`**. Snapshots are JSON strings parsed in the worker, then posted as structured-clone objects. The dotnet publish is **single-threaded** (no pthread WASM).

COOP/COEP headers are still set proactively so future work does not require a deploy surprise.

### 8.2 Required headers

Set on **all routes** in `web/next.config.ts`:

| Header | Value |
|--------|--------|
| `Cross-Origin-Opener-Policy` | `same-origin` |
| `Cross-Origin-Embedder-Policy` | `require-corp` |

**Production:** Coolify / Traefik must not strip these headers. Verify on `/play` in DevTools → Network. Smoke test: `web/scripts/smoke-play.mjs`.

**Local WASM demo** (`web/wasm/`): headers are commented out in `vite.config.ts` — not required until SAB/pthreads are enabled.

### 8.3 When adding SharedArrayBuffer

Prerequisites if migrating to SAB-backed snapshots or pthread `SimulationLoop`:

1. Page must be [cross-origin isolated](https://developer.mozilla.org/en-US/docs/Web/API/crossOriginIsolated) (`crossOriginIsolated === true`).
2. All subresources (WASM, workers, scripts, images) must be same-origin **or** send `Cross-Origin-Resource-Policy: cross-origin` (or `same-site` where applicable).
3. Third-party embeds without CORP (analytics, Stripe iframe) may break — audit before enabling.
4. Without COOP+COEP, `SharedArrayBuffer` is `undefined` and pthread WASM builds fail at runtime.

### 8.4 Failure mode without headers

Missing COOP/COEP does **not** block the current JSON worker path. It **will** block future SAB/pthread builds. If WASM init fails for any reason (missing bundle, interop error, header-related isolation failure), the client falls back to procedural (§9).

---

## 9. Procedural fallback contract

When WASM is unavailable, `/play` must remain **playable** with deterministic mock city data. This is a hard deploy requirement — see [`docs/DEPLOY_WEB.md`](../DEPLOY_WEB.md).

### 9.1 Trigger conditions

| Condition | Result |
|-----------|--------|
| `/dotnet/_framework/` absent (build skipped or failed) | `bridge.init()` rejects → procedural |
| `bridge.init()` throws (load error, missing exports, `Init` failure) | `catch` in `CityCanvas` → procedural |
| Docker `BUILD_WASM=0` or publish OOM | Image ships without dotnet assets → procedural |
| User runs `pnpm dev` without `pnpm build:wasm` | Procedural (unless assets committed) |

### 9.2 Client behavior (`CityCanvas`)

1. On mount: `createSimBridge()` + `bridge.init("/dotnet", 256)`.
2. **Success:** `simSource = "wasm"`, RAF tick loop starts, snapshots drive `cityDataFromSnapshot`.
3. **Failure:** `console.warn`, `simSource = "procedural"`, `setCity(getCityData())`, **no tick loop**, static mock city.

`FpsHud` displays `Data: WASM sim` or `Data: procedural` via `stats.simSource`.

### 9.3 Procedural data (`city-data.ts`)

| Property | WASM sim | Procedural fallback |
|----------|----------|---------------------|
| World grid | 256×256 | 256×256 (`GRID_SIZE`) |
| Building count | ~220 at init, grows via sim | ~5000 (`TARGET_BUILDING_COUNT`) |
| Data generator | `cityDataFromSnapshot()` | `generateCityData()` (Mulberry32 seed `0x63697479`) |
| TypeId layout | From sim pool | Random within era bands — matches `sim-types` / `BUILDING_ARCHETYPE_3D` |
| Zoning / roads | Live from snapshot | Static — toolbar paint **does not** mutate procedural city in v1 |
| Sim counters (HUD) | Live tick, population, funds, era | Not updated (no bridge) |
| Save/load API | `SimClientApi` when `bridgeReady` | `onSimApi(null)` — save UI disabled or stub |

### 9.4 Rendering parity

Both paths feed the **same** R3F pipeline:

- `BuildingInstances` — instanced meshes per `archetypeKey`
- Chunk LOD L0–L3 — `lib/chunks.ts`
- Missing GLTF → procedural box placeholders (`docs/MESHY_ASSET_PIPELINE.md`)

Procedural mode stress-tests GPU with ~5000 buildings; WASM mode stress-tests sim CPU with growth over time. Building count dominates GPU cost, not data source.

### 9.5 Build / deploy matrix

| Command / arg | Sim source |
|---------------|------------|
| `pnpm build:wasm && pnpm build` | WASM (if init succeeds) |
| `pnpm build` (no wasm step) | Procedural |
| Docker `BUILD_WASM=1` (default) | WASM when publish succeeds |
| Docker `BUILD_WASM=0` | Procedural-only (faster CI preview) |

**Verify:** `GET /dotnet/_framework/blazor.boot.json` → 200 and HUD shows `WASM sim`.

### 9.6 `createSimBridgeStub()`

No-op bridge for tests — `init` resolves immediately, no worker, `getSnapshot()` returns `null`. Does **not** generate procedural city; tests must seed their own fixtures.

---

## 10. Error handling

| Failure | Worker | Main thread |
|---------|--------|-------------|
| WASM load / `Init` | `postMessage({ type: "error", message })` | `init()` Promise rejects → procedural fallback |
| Command handler throw | `postMessage({ type: "error", message })` | Logged; bridge stays on WASM if already ready |
| `GetRenderSnapshot` parse error | Uncaught in worker — treat as bug | Snapshot stream stops — fix WASM/TS contract |
| `dispose` | Clears state, no outbound message | `terminate()` worker |

---

## 11. Related docs

| Doc | Relevance |
|-----|-----------|
| [SIM_SNAPSHOT_V2](./SIM_SNAPSHOT_V2.md) | **P7.1** Cathedral field / cadence / ownership matrix |
| [GAP_AUDIT_DESIGN_DOCS](./GAP_AUDIT_DESIGN_DOCS.md) | Identified missing bridge contract (fix #7) |
| [DEPLOY_WEB.md](../DEPLOY_WEB.md) | `BUILD_WASM`, COOP/COEP deploy, fallback verification |
| [BUILDING_ARCHETYPE_3D](./BUILDING_ARCHETYPE_3D.md) | `typeId` → mesh archetype keys |
| [TECH_TREE_GAP_ANALYSIS](./TECH_TREE_GAP_ANALYSIS.md) | Research fields not yet in bridge |
| [GAMEPLAY_LOOP_IMPROVEMENTS](./GAMEPLAY_LOOP_IMPROVEMENTS.md) | Proposed snapshot extensions |
| [SIMULATION_ARCHITECTURE](./SIMULATION_ARCHITECTURE.md) | Full desktop sim spec (scale footnotes differ for web) |
| `web/README.md` | Local dev quick start |
| `web/wasm/README.md` | WASM build, systems matrix, perf budget |

---

## 12. Changelog

| Date | Change |
|------|--------|
| 2026-07-04 | Initial contract doc (GAP_AUDIT #7) |
| 2026-08-11 | Link **P7.1** [`SIM_SNAPSHOT_V2.md`](./SIM_SNAPSHOT_V2.md); note TS sketch may lag Cathedral DTO fields |
| 2026-08-11 | Refresh §6 TypeScript sketch + `@citymajor/sim-types` `simSnapshot.ts` to tip `SimSnapshotDto` (Event*Mult, Law*Mult, ActiveLawIds, ordinances, utilities, CulturalDna, TradeBalance, fire/wildfire/hospital) — closes SIM_SNAPSHOT_V2 §6 #1 |
