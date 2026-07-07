# CityMajor — Simulation v1 Gap Matrix

> **Branch:** `feat/wasm-r3f-integration-2026-07-04` (`citymajor-web-r3f-spike`)  
> **Compared:** Desktop spec (`SIMULATION_ARCHITECTURE.md`) + `IronAndOakGame` wiring vs browser `WasmSimHost` + web `/play` UI  
> **v1 locked scope:** 256×256 map, ~5K buildings, ~10K households, Frontier→Industrial era arc, ≥30 FPS integrated GPU

---

## 1. System gap matrix

| System | Desktop depth (spec + `IronAndOakGame`) | WASM status (`WasmSimHost` + `GetStatus()`) | Web UI exposure | v1 priority |
|--------|----------------------------------------|---------------------------------------------|-----------------|-------------|
| **Economy** | 45 goods, 8–16 market zones, daily order collection → elasticity pricing → transactions → RCI demand; `ProductionChainRegistry` drives building I/O in `DailyTick` | **Live** — `EconomySystem.DailyTick` + `MonthlyTick` (no-op; budget owns taxes) | HUD: **Funds** only. No RCI bars, goods prices, unemployment | **P0** — core loop; extend HUD with RCI + monthly budget summary |
| **Population** | 100K-cap households; staggered `Tick` (1/30 per sim step) + `MonthlyTick` (births/deaths/migration/employment/satisfaction) | **Live** — same systems; pools capped at **8,192 HH** (below v1 10K target); seeds ~50–200 HH | HUD: **Pop** only. `householdCount` in `GetStatus()` but not shown | **P0** — raise pool to 10,240+; expose HH count + happiness |
| **Traffic** | `TrafficSystem`: ~500 zones, MNL mode choice, 5 Frank-Wolfe iterations @ 2 Hz, vehicle spawn data | **Degraded** — `WasmTrafficLite`: 64 zones, 4 FW iter every **5 sim s**, edge-batch BPR every 0.5 s. `GetStatus().trafficMode = "lite"`. Full `TrafficSystem` listed **stubbed** | No traffic overlay or flow viz. `Tiles.Traffic` written in WASM but **not** in `SimSnapshotDto` | **P1** — traffic overlay + lite mode acceptable for v1; full desktop model post-v1 |
| **Trade** | `TradeSystem`: inter-zone + global import/export, 16×16 friction matrix (`SIMULATION_ARCHITECTURE` §5) | **Not wired** — class exists + tests; **also not wired in desktop `IronAndOakGame`** | None | **P2** — defer; wire inter-zone trade before global market |
| **ProductionChain** | 20 chains, building productivity, input/output goods (`EconomySystem.CollectOrders`) | **Partially live** via `EconomySystem` (registry default). `GetStatus()` incorrectly lists as stubbed — only standalone `TradeSystem` is unwired | None (invisible to player) | **P1** — fix `GetStatus()` label; v1 needs visible growth/abandonment feedback, not chain UI |
| **Politics** | `PoliticsSystem` daily (protests/decay) + monthly (approval, elections, corruption) | **Live** — same ticks; scores fed via `FeedCrossSystemData` (service/safety partly hardcoded 0.5–0.6) | None. Desktop has `PoliticsPanel` | **P2** — approval in HUD for Herald narrative buckets; full council UI post-v1 |
| **Events** | `EventSystem` daily triggers + `UpdateEvents`; desktop also calls `ApplyEventEffectsToState` | **Partial** — daily tick + `state.TickEvents()` but **missing** `ApplyEventEffectsToState` bridge | Herald LLM uses **heuristic** `healthcareCoverage` from building categories, not sim events | **P1** — wire event effects; connect Herald buckets to real sim state |
| **Research** | `ResearchSystem` monthly RP, tech queue, era gates from `technologies.json` | **Live** — embedded JSON + monthly tick; era via `WasmEraDeriver` (tick/RP proxies, not full tech tree) | `GetStatus()` exports RP/rate; HUD shows **Era** badge only | **P1** — Frontier→Industrial arc is v1; minimal research strip in HUD |
| **ZoneGrowth** | Daily spawn/decline from RCI + monthly land value + upgrades | **Live** — `Tick`, `RecalculateLandValue`, `CheckUpgrades` | Zone paint overlay (sparse tiles). Building growth visible only when snapshot updates | **P0** — must drive city toward 5K buildings organically |
| **CulturalDNA** | 8-dimension drift yearly; 12 presets, 20 archetypes (spec L2/L3) | **Live** — preset at init + `YearlyTick`; no archetype classifier | None | **P2** — v1 needs preset only; archetype UI is post-v1 |

### `GetStatus()` — live vs stubbed (authoritative)

**Live counters:** `tick`, `population`, `householdCount`, `cityFunds`, `era`, `eraName`, `researchPoints`, `researchRate`, `eventDefinitionCount`, `techCount`, `trafficMode`, tick intervals, traffic lite config.

**Listed in `systems[]`:** Economy, Population, WasmTrafficLite, Service, ZoneGrowth, Budget, Politics, Event, Research, CulturalDNA.

**Listed in `stubbed[]`:**
- `TrafficSystem` full (500 zones) — accurate; WASM uses lite
- `TradeSystem` — accurate; unwired everywhere
- `ProductionChain` — **misleading**; chains run inside `EconomySystem.DailyTick`

---

## 2. Tick schedule L0–L2 — spec vs WASM

### Level 0 (every tick / sub-day)

| Spec system | Desktop | WASM today | Gap |
|-------------|---------|------------|-----|
| Traffic BPR on all edges | Every 0.5 s traffic tick | Full FW every **5 sim s**; ¼-edge BPR every **0.5 s** | No per-tick BPR; no MNL mode choice; no rush-hour multipliers |
| Utility grid balance | Per-partition power/water | `ServiceSystem.DailyTick` only (not L0) | Utilities not balanced sub-day |
| Active disaster progression | L0 event tick | Events on **L1 day** only | Disasters don't progress intra-day |
| Vehicle interpolation (10K cosmetic) | L0 lerp along paths | `maxVehicles: 256`, no tick in `WasmSimHost` | No cosmetic traffic on web |

### Level 1 (game day)

| Spec system | Desktop | WASM | Gap |
|-------------|---------|------|-----|
| Economy Leontief + zone pricing | `EconomySystem.DailyTick` | Same | Market zone count scales with pop but not full 16-zone partition map |
| Construction progress | Spec + desktop tools | Zone growth spawns buildings; **no** construction timer UX | Building `state=constructing` not surfaced in web DTO |
| Zone growth / RCI | `ZoneGrowthSystem.Tick` | Same | — |
| Traffic assignment (1 FW iter/day) | Part of `TrafficSystem` @ 2 Hz | Absorbed into lite FW schedule | Coarser O-D (gravity model, not home-work pairs) |
| Services (crime, fire risk) | `ServiceSystem.DailyTick` | Same | Overlay data not exported to web |

### Level 2 (game month)

| Spec system | Desktop | WASM | Gap |
|-------------|---------|------|-----|
| Population lifecycle + migration | `PopulationSystem.MonthlyTick` | Same | Pool cap blocks 10K target |
| Employment matching | In monthly tick | Same | Not shown in UI |
| Satisfaction (partition aggregates) | L2 monthly + L0 stagger | Stagger via `PopulationSystem.Tick` each 8 Hz step | Overlay maps not rendered |
| Cultural DNA drift | Yearly (on year rollover) | Same | — |
| Research | `ResearchSystem.MonthlyTick` | Same + `WasmEraDeriver` | Era thresholds are WASM-specific proxies |
| Politics approval | `PoliticsSystem.MonthlyTick` | Same | Hardcoded partial input scores |
| Budget | `BudgetSystem.CalculateMonthlyBudget` | Same | No budget panel |

### Level 3 (game year) — **entirely missing in WASM v1**

Archetype detection, full era transitions from tech tree, infrastructure aging, sports leagues, city rating — spec §1 L3. Acceptable to defer for v1 (Frontier→Industrial only).

---

## 3. 10K households / 5K buildings — what blocks it

| Blocker | Current state | v1 target | Fix direction |
|---------|---------------|-----------|---------------|
| **WASM pool caps** | `maxHouseholds: 8192`, `maxBuildings: 4096` in `WasmSimHost.Init` | 10K / 5K | Raise to ≥10,240 / 5,120 in `WorldState` ctor |
| **Starter seed scale** | ~220 buildings, 50–200 HH | Grow to 5K / 10K via sim | Rely on `ZoneGrowthSystem` over playtime; optional denser seed for demos |
| **Render vs sim split** | Procedural fallback = **5K** mock buildings; WASM path = **~220** in snapshot | Single source of truth | Default `/play` to WASM; drop procedural except dev fallback |
| **Snapshot payload** | `GetRenderSnapshot()` JSON-serializes every building + sparse zones/roads | 5K buildings @ 4 Hz = large worker→main traffic | Delta snapshots, chunk-scoped building lists, or binary `SharedArrayBuffer` |
| **DTO truncation** | `SimSnapshotDto` drops traffic tiles, vehicles, approval, research progress, full tile arrays | Renderer needs traffic/LOD fields | Extend DTO or add `GetChunkSnapshot(cx,cy)` |
| **Tick budget** | 8 Hz single-threaded; 10K HH → ~333 HH staggered/tick | ≤10 ms/tick (SB-3685) | Profile at 10K; consider 6 Hz or wasm SIMD; optional pthread + COOP/COEP |
| **Instancing perf** | R3F handles 5K instanced (PERF.md) | ≥30 FPS integrated | Chunk GPU culling before 50K+; already on roadmap |
| **Save/load** | `save-store` stub; no WASM serialize | Persist 5K city | `WorldState` binary serialize (spec §17) |
| **Zone grid merge** | Worker keeps optimistic `zoneGrid` when WASM omits zones | Consistency at scale | Full zoned tile export doesn't scale — use chunk bitmasks |

---

## 4. Web UI exposure summary

| Surface | Sim data shown | Missing vs desktop |
|---------|----------------|-------------------|
| `ResourcesHud` | Pop, Funds, Tick, Era | RP, approval, tax rates, RCI |
| `FpsHud` | WASM vs procedural, building counts, LOD | Traffic mode, sim tick Hz |
| `ZoningToolbar` | Zone paint, bulldoze, **road** (wired to `PlaceRoad`) | Office/mixed zones, density tools |
| `HeraldPanel` | LLM/template narrative | Not driven by `EventSystem` or politics |
| `SaveLoadControls` | JSON stub | No real WASM state round-trip |
| Overlays | Zone + road tint | Traffic, land value, services, pollution |

Desktop panels (`BudgetPanel`, `ResearchPanel`, `PoliticsPanel`) have **no web equivalent**.

---

## 5. Milestone recommendations (M2–M4)

Assumes **M0** = R3F spike + procedural 5K (done), **M1** = WASM host + core tick wiring (this branch, ~partial).

### M2 — Scale alignment (sim pools = render path)

1. Raise `WorldState` pools to **10,240 HH / 5,120 buildings**; align `WasmConfig` starter caps.
2. Make WASM the default `/play` data source; procedural 5K = dev-only fallback.
3. Chunk-scoped or delta building snapshots; cap full JSON snapshot frequency at 5K.
4. Benchmark `WasmSimHost.Tick` at 10K HH / 5K buildings @ 8 Hz; document headroom.
5. Fix `GetStatus().stubbed` — remove `ProductionChain`; keep `TradeSystem` + full `TrafficSystem`.

**Exit:** HUD shows WASM sim with **≥1,000** buildings grown from zoning; perf doc shows 30 FPS at 5K instances.

### M3 — Playable loop depth (v1 mechanics)

1. Wire `ApplyEventEffectsToState` (or equivalent) in `WasmSimHost.RunDayTick`.
2. Export traffic density (chunk heatmap) + `ApprovalRating` + `ResearchPoints` to web HUD.
3. Traffic overlay toggle (lite congestion from `Tiles.Traffic`).
4. Budget summary modal (income/expense from `SimSnapshot` fields already captured server-side).
5. Herald buckets from real sim metrics (`approval`, `RCI`, traffic congestion), not `estimateHealthcareCoverage`.
6. Binary save/load v1 (LZ4-compressed `WorldState`).

**Exit:** 30-minute playable loop: zone → grow → tax → era tick → Herald event — all on WASM state.

### M4 — v1 ship hardening

1. Complete **Frontier → Industrial** era arc via research + population milestones (replace pure tick thresholds where possible).
2. Meshy GLTF catalog covers top 40 archetypes; hero landmarks ×8.
3. Stripe Founder Pass + narrative quota (already scaffolded) production-tested.
4. COOP/COEP + optional `SharedArrayBuffer` snapshot ring if delta JSON insufficient.
5. Coolify preview + docker-fleet path per deploy routing; smoke + perf gates in CI.

**Exit:** Public `/play` meets CLAUDE.md v1 table; no procedural sim on happy path.

---

## 6. Key code references

**WASM pool limits (blocker for v1 scale):**

```55:56:/Users/fredericbeeg/citymajor/citymajor-web-r3f-spike/src/Forge.SimWasm/WasmSimHost.cs
        _state = new WorldState(worldSize, maxHouseholds: 8192, maxBuildings: 4096,
            maxRoadNodes: 16384, maxVehicles: 256);
```

**GetStatus stub list:**

```97:102:/Users/fredericbeeg/citymajor/citymajor-web-r3f-spike/src/Forge.SimWasm/WasmExports.cs
            stubbed = new[]
            {
                "TrafficSystem full (500 zones — desktop only; WASM uses lite mode)",
                "TradeSystem (not wired in spike)",
                "ProductionChain (not wired in spike)",
            },
```

**Desktop event bridge missing in WASM:**

```143:144:/Users/fredericbeeg/citymajor/citymajor-web-r3f-spike/src/Forge.Game/IronAndOakGame.cs
            // Apply active event effects to WorldState each day
            ApplyEventEffectsToState(state);
```

---

*Generated from gap analysis on `citymajor-web-r3f-spike` @ `feat/wasm-r3f-integration-2026-07-04`.*
