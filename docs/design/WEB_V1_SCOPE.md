# CityMajor — Web v1 Scope Charter (Locked)

> **⚠ SUPERSEDED for platform decisions:** CityMajor's **primary v1 platform is Unity 6 desktop (Steam)**. See **[UNITY_V1_SCOPE.md](./UNITY_V1_SCOPE.md)** for the canonical scope charter. This document remains for **web maintenance-mode** reference only (R3F + WASM client at `web/`).

**Status:** Locked (historical — web maintenance only)  
**Date:** 2026-07-04  
**Linear:** [SB-3704](https://linear.app/softblaze/issue/SB-3704)  
**Supersedes:** Conflicting scale/platform rows in `MASTER_GAME_CONCEPT.md` §1–2, `CITY_BUILDER_GDD.md`, `SIMULATION_ARCHITECTURE.md`, and agent briefs that assume Godot/Steam/pixel art.

> **Canonical onboarding:** [`CLAUDE.md`](../../CLAUDE.md) — stack, directories, architecture notes.  
> **Sim depth (unchanged):** [`MASTER_GAME_CONCEPT.md`](MASTER_GAME_CONCEPT.md) — economy, politics, LLM narrative design.  
> **Gap audit:** [`GAP_AUDIT_DESIGN_DOCS.md`](GAP_AUDIT_DESIGN_DOCS.md) — doc inventory and contradictions this charter resolves.

---

## 1. Product intent

CityMajor web v1 is a **browser-only**, mesh-3D city builder (Cities: Skylines lite) with **deep simulation** at a **deliberately reduced scale**. One playable era arc — **Frontier → Industrial** — proves the WASM sim + R3F renderer + monetization loop before expanding content or platform.

**In scope:** Single-player mayor experience, zoning, services, economy slider, research through Industrial era, LLM-flavored narrative (quota-gated), cloud saves, Founder Pass + cosmetic shop.

**Out of scope for v1:** See §6 (explicit cuts).

---

## 2. Locked parameters

| Parameter | v1 value | Notes |
|-----------|----------|-------|
| **Map** | **256×256** tiles (65,536) | 64 chunks @ 32×32; matches sim spatial partitions |
| **Buildings** | **~5,000** max instanced | 40–60 archetypes per era band; not unique meshes per building |
| **Households** | **~10,000** | Full cultural DNA / satisfaction at reduced population |
| **Era arc** | **1 arc: Frontier → Industrial** | Postwar, Modern, Future content exists in JSON but **does not ship** in v1 |
| **Platform** | **Browser only** | WebGL2 + WASM; COOP/COEP for SharedArrayBuffer |
| **Multiplayer** | **None** | No co-op, no P2P, no shared cities |
| **Renderer** | R3F + Three.js | `InstancedMesh` per archetype; LOD mandatory |
| **Simulation** | C# → .NET 8 WASM | Web Workers + double-buffer snapshots |
| **Tech tree (playable)** | Subset gated to Frontier + Industrial eras | Full JSON has 156 techs across 5 eras — v1 unlocks only the first two era tags |

---

## 3. Era arc: Frontier → Industrial

v1 delivers roughly **60–90 minutes** of focused progression (not the full 150-minute / 5-era journey in [`ERA_ARC_DESIGN_V2.md`](ERA_ARC_DESIGN_V2.md)).

| Phase | Gameplay focus | Visual band (`TypeId` suffix) |
|-------|----------------|-------------------------------|
| **Frontier** | Survival, grid layout, wells, early health, dirt → cobblestone roads | `x00–x19` — wood, rust, peaked roofs, ≤3 stories |
| **Industrial** | Rapid growth, pollution, strikes, zoning at scale, coal power, trams/trains | `x20–x39` — brick, steel, chimneys, sawtooth factories |

**Transition triggers (v1):** Era flip when Industrial-era research milestones are met (e.g. structural steel, coal plant, asphalt roads — see `ERA_ARC_DESIGN_V2.md` §3). Post-Industrial tech **cannot** be researched in v1.

**Content pools (v1):** Buildings, events, and laws filtered to `era` / `era_min` ∈ {frontier, industrial}. Hero landmarks: 8–12 per active era band ([`MESHY_HERO_LANDMARKS.md`](MESHY_HERO_LANDMARKS.md)).

---

## 4. Performance targets

| Metric | Target | Enforcement |
|--------|--------|-------------|
| **FPS (integrated GPU)** | **≥30** at max city fill with LOD active | Quality tier auto-downgrade; heatmap/box LOD at distance |
| **FPS (discrete GPU)** | **≥60** with LOD active | Default quality tier |
| **Draw calls** | 20–40 per visible chunk | Instancing + 32×32 chunk frustum cull |
| **Sim tick** | WASM worker; render reads snapshot only | No React state per building |
| **LOD bands** | L0 full GLTF → L1 simplified → L2 boxes → L3 heatmap blocks | Mandatory at street/neighborhood/city zoom |

**FPS vs sim tick-ms:** The ≥30 / ≥60 FPS rows are **render wall-clock** (R3F/WebGL with LOD). They are **not** a `SimHost.Tick` millisecond budget. Sim runs at **8 Hz** (125 ms/frame) on a worker; characterization gates live in [`SIM_TICK_BUDGET.md`](./SIM_TICK_BUDGET.md) (keep-pace &lt;125 ms always; historical &lt;25 ms under `CI_STRICT=1`).

**Measurement (CI / `pnpm perf:gate`):** Enforce **stable** FPS — **median** sample after canvas warm-up (`PERF_WARMUP_MS`) plus discard of the first `PERF_DISCARD_MS` of the sample window (default 2s). Absolute min is diagnostic only (GC / shader / first-frame hitch). A healthy sustained window (median typically ≫ floor while a single sample dips) must clear ≥30. See [`web/PERF.md`](../../web/PERF.md).

**Non-goals:** 4K ultra, uncapped entity counts, or “full vision” 1024×1024 / 50k building stress tests.

---

## 5. Monetization (v1)

| Tier | Price | Includes |
|------|-------|----------|
| **Free core** | $0 | Full sim depth for Frontier → Industrial; 3 cloud save slots; **10 LLM narrative events/day** |
| **Founder Pass** | **$24.99** (Stripe) | Unlimited LLM events; 20 save slots; cosmetic bundle; early badge; **no pay-to-win** |
| **Cosmetic shop** | Variable (Stripe) | Facade skins, era-appropriate ornaments — **sim-neutral** only |
| **Solana perks** | Phase 2 | Not in v1 launch scope |

**Principles:** Simulation systems, zoning, research, and laws are never paywalled. Templates always available when LLM quota exhausted or API down.

---

## 6. Explicitly CUT from full GDD (post-v1)

The following appear in legacy docs (`MASTER_GAME_CONCEPT` §1–2, `CITY_BUILDER_GDD`, agent briefs) but are **not** v1 deliverables:

### Platform & distribution
- Steam / Epic native builds, Steam Next Fest demo, Steam Workshop
- Linux/macOS/Windows desktop ports; console (Switch, PS5, Xbox)
- Godot 4 frontend; Forge Engine OpenGL sprite renderer as shipped product
- Self-hosted Qwen on player GPU

### Visual & art
- Songs of Conquest–style **pixel art** (64×32 isometric sprites, 600–800 unique sprites)
- Aseprite pipeline, GPUParticles2D, palette shaders per [`AGENT_09_ART_PIPELINE.md`](AGENT_09_ART_PIPELINE.md)
- 2D vehicle wealth sprites ([`VEHICLE_WEALTH_SYSTEM.md`](VEHICLE_WEALTH_SYSTEM.md))

### Scale & content
- **1024×1024** map (1M tiles); **50,000+** buildings; **100,000** households; **500,000** citizens
- **5 eras** (Postwar, Modern, Future) and 200-year campaign arc
- **1024×1024** regional / open-world maps ([`OPEN_WORLD_SCALE_PROPOSAL.md`](OPEN_WORLD_SCALE_PROPOSAL.md))
- **10 global city archetypes** and global trade MP scaling ([`GLOBAL_CITY_ARCHETYPES.md`](GLOBAL_CITY_ARCHETYPES.md))
- Full 156-tech tree playable through Future era

### Multiplayer & live services
- P2P co-op, host-authoritative sync ([`MASTER_GAME_CONCEPT.md`](MASTER_GAME_CONCEPT.md) §10)
- Live multiplayer proposals ([`MULTIPLAYER_LIVE_PLAY_PROPOSAL.md`](MULTIPLAYER_LIVE_PLAY_PROPOSAL.md))
- Shared cities, async visit, leaderboard seasons

### Systems deferred
- Modding / Workshop / user CDN asset pipeline
- Sports leagues full sim ([`EXPANDED_ZONES_EVENTS_SPORTS.md`](EXPANDED_ZONES_EVENTS_SPORTS.md) — partial)
- Solana wallet integration at launch
- FlatBuffers + Zstd desktop save format as primary (web uses cloud API + WASM binary — spec TBD: `SAVE_FORMAT_WEB.md`)

### Team / business (original vision)
- $29.99 1.0 premium pricing; EA roadmap Q3 2027–Q4 2029
- Contracted pixel art specialists; zero API cost via local LLM

---

## 7. In scope (sim depth retained)

v1 **keeps** deep simulation where scale allows (~10k HH, ~5k buildings):

- Economic Control Spectrum (slider)
- RCI zoning + organic growth
- Leontief-style production chains (coefficients TBD)
- BPR traffic, transit modes (era-appropriate)
- Fire / police / health / education / utilities
- Factions, elections, 70+ laws (era-filtered)
- Cultural DNA (8 dimensions)
- LLM narrative via backend proxy + template fallback
- Events pool (era-filtered from `events.json`)

See agent specs [`AGENT_02`–`AGENT_07`](.) — valid with scale footnotes; Godot/UI sections superseded.

---

## 8. Doc hierarchy (this charter’s place)

```
CLAUDE.md                          ← agent/dev onboarding (links here)
docs/design/WEB_V1_SCOPE.md        ← THIS FILE: locked product scope (+ §12 v2 trade architecture)
docs/design/MASTER_GAME_CONCEPT.md ← sim/economy/narrative (+ web pivot §)
docs/design/BUILDING_ARCHETYPE_3D.md
docs/design/MESHY_*.md
docs/design/SIMULATION_ARCHITECTURE.md ← add web v1 scale footnotes when editing
docs/design/GAP_AUDIT_DESIGN_DOCS.md
```

When a design doc contradicts this charter, **this charter wins** for v1 shipping decisions.

---

## 9. Open specs (tracked, not blocking charter lock)

| Spec | Status |
|------|--------|
| `ERA_ARC_V1.md` — tech/building/event subset IDs | Not yet authored |
| `SAVE_FORMAT_WEB.md` — cloud + WASM binary | Not yet authored |
| `WASM_SIM_BRIDGE.md` — snapshot layout, worker protocol | Not yet authored |
| `DATA_BRIDGE.md` — `buildings.json` ↔ TypeId ↔ GLTF | [Authored](./DATA_BRIDGE.md) |

---

## 10. Acceptance checklist (v1 ship)

**Overall (2026-07-05, post–wave-4): ~52% toward public launch · ~74% toward integration-spine / preview beta.** Detail: [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) § v1 progress snapshot.

| Criterion | Status |
|-----------|--------|
| 256×256 map playable start-to-Industrial transition | **~75%** — era gates + research path; full arc QA incomplete |
| Stable ≥30 FPS on integrated GPU with 5k buildings / 10k HH | **~15%** — CI perf stub only; manual sign-off open [SB-3703](https://linear.app/softblaze/issue/SB-3703) |
| WASM sim ticks in worker; R3F renders from snapshot | **~95%** shipped |
| Founder Pass + cosmetic Stripe flow; free tier quotas enforced | **~35%** — stub checkout + cookie entitlements; live webhook TBD |
| Cloud save (3 free / 20 Founder) | **~48%** — CMJR blob path + slot UI; auth + SoA chunks TBD |
| No multiplayer code paths in production build | **~100%** |
| LLM proxy with template fallback; 10/day free cap | **~65%** — API + quota shipped; preview keys + Founder gate TBD |

- [ ] 256×256 map playable start-to-Industrial transition
- [ ] Stable ≥30 FPS on integrated GPU with 5k buildings / 10k HH
- [x] WASM sim ticks in worker; R3F renders from snapshot
- [ ] Founder Pass + cosmetic Stripe flow; free tier quotas enforced
- [ ] Cloud save (3 free / 20 Founder)
- [x] No multiplayer code paths in production build
- [ ] LLM proxy with template fallback; 10/day free cap

---

## 11. Wave-4 depth progress (implementation)

Committed `9b82eb1` · uncommitted polish in `citymajor-web-r3f-spike` WT (citizen/law smoke, `CanvasRenderHealth`, +2 frontier GLBs).

| System | Charter §7 intent | Honest % | Notes |
|--------|-------------------|----------|-------|
| Citizens / cultural DNA | ~10k HH, satisfaction | **58%** | Panel + aggregates; named household WASM export pending |
| Laws (70+ era-filtered) | Factions, elections, laws | **32%** | Catalog + counts; enactment UI deferred |
| Economy / trade | Leontief chains | **52%** | Global market HUD; inter-city routes deferred — see §12 [SB-3728](https://linear.app/softblaze/issue/SB-3728) |
| Herald / LLM narrative | Quota-gated + templates | **68%** | Events + council cmds; preview LLM keys unset |
| Cloud saves | 3 free / 20 Founder | **48%** | CMJR interim JSON chunk; full SoA + auth TBD |

### Dev / QA toggles (non-player)

| Toggle | How | WASM effect |
|--------|-----|-------------|
| **Empty city start** | `/play?empty=1` or Play HUD **Empty start** checkbox | Skips `SeedStarterCity`, `SeedStartingPopulation`, and `BootstrapServiceCoverage` in `WasmSimHost.Init` — terrain-only 256×256 map for greenfield zoning/build tests |

Worker flag: `init.skipStarterCity` → `Program.Init(worldSize, skipStarterCity)` (C# second arg).

---

## 12. v2 architecture note — inter-city trade ([SB-3728](https://linear.app/softblaze/issue/SB-3728))

**Epic:** [SB-3728 — v2 Trade & MP](https://linear.app/softblaze/issue/SB-3728) (Open Region pillar; co-op + headless sim server are sibling tracks in the same epic).

v1 ships **global-market-only** trade: `TradeSystem.ProcessTrade` auto-imports deficits and auto-exports surpluses against the anonymous world index (`PartnerCityId = -1`). Monthly export value and import cost surface in `ResourcesHud` and `EconomyPanel`; the **Trade routes** panel is a read-only stub linking SB-3728.

### v1 baseline (shipped / partial)

| Layer | v1 behavior |
|-------|-------------|
| **Sim** | `Forge.Game.Simulation.TradeSystem` — `GlobalPrices[]`, auto-trade thresholds, `ApplyGlobalEvent` shocks |
| **WASM** | `WasmSimHost.ProcessGlobalMarketTrade` — monthly tick; no bilateral routes exported |
| **Web HUD** | Trade balance strip + economy shortages; `EconomyPanel` → `TradeRoutesStub` ("No routes · read-only preview") |
| **Data model** | `TradeRoute` struct already defines `PartnerCityId`, `GoodType`, `Quantity`, `AgreedPrice`, `DurationMonths` — **unused for partners in v1** |

### v2 target (inter-city routes)

Phase 2 of [`OPEN_WORLD_SCALE_PROPOSAL.md`](OPEN_WORLD_SCALE_PROPOSAL.md) §2, §5 — player-visible bilateral trade before full 1024×1024 shared maps:

1. **Regional map UI** — 512×512 overview; player 256×256 claim highlighted; 3–5 **NPC town** markers with fixed supply/demand profiles (e.g. Coal Ridge → ore export, Harbor Vale → finished-goods import).
2. **Route contracts** — Player selects partner (`PartnerCityId ≥ 0`), good, monthly volume cap, duration; `CreateTradeRoute` / `CancelTradeRoute` already exist in C#; wire through WASM exports + snapshot fields for active routes.
3. **Settlement tick** — Reuse `ExecuteTradeRoutes` + monthly budget line (`BudgetSystem.TradeIncome`); stable `AgreedPrice` vs floating global index creates specialization incentives (`ProductionChainRegistry` multi-tier chains).
4. **Sim LOD** — Full building-level sim in player city; aggregate chunk-level production/consumption for NPC tiles (must not regress ≥30 FPS target — §4).
5. **Herald** — Trade headlines from route state (surplus, embargo risk, commodity spike); quota-gated like other narrative events.

### Explicit v2 out-of-scope (same epic, later milestones)

- Live PvP trade negotiation, player-owned cities on a shared 1024×1024 map ([`OPEN_WORLD_SCALE_PROPOSAL.md`](OPEN_WORLD_SCALE_PROPOSAL.md) Phase 3 / [SB-3729](https://linear.app/softblaze/issue/SB-3729))
- Co-op host-authoritative sync ([`MULTIPLAYER_LIVE_PLAY_PROPOSAL.md`](MULTIPLAYER_LIVE_PLAY_PROPOSAL.md))
- Headless `Forge.SimCore` tick server (required for competitive MP integrity — see [`CTO_IMPROVEMENT_ROADMAP_2026-07.md`](CTO_IMPROVEMENT_ROADMAP_2026-07.md) § v2 pillars)

### Bridge work (v1.5 → v2)

| Work item | Owner surface |
|-----------|---------------|
| Export `tradeRoutes[]` in WASM snapshot | `WasmSimHost`, `sim-worker.ts`, `snapshot-types.ts` |
| `create_trade_route` / `cancel_trade_route` commands | WASM exports → worker message protocol |
| Replace `TradeRoutesStub` with route list + create flow | `EconomyPanel.tsx` |
| Regional map panel (read-only markers first) | New R3F overlay or 2D minimap component |
| Smoke: active route affects budget line | `smoke-play-checks.mjs` (extends existing SB-3728 link assertion) |

**Merge / launch implication:** Inter-city routes are **not** a v1 ship blocker. PR #1 spine may merge with global-market trade only; route UI remains stub until SB-3728 lands (tracked in [`V1_MERGE_CHECKLIST.md`](V1_MERGE_CHECKLIST.md) § Phase 3 wave 4).

---

*Last updated: 2026-07-05 (§11 empty-city dev toggle; §12 inter-city trade architecture note, SB-3728). Changes to locked parameters require explicit product sign-off and an update to this file.*
