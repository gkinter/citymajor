# CityMajor — CTO Improvement Roadmap (July 2026)

**Author:** CTO synthesis (parallel agent docs + PR #1 state)  
**Worktree:** `feat/wasm-r3f-integration-2026-07-04`  
**Sources:** `GAP_AUDIT_DESIGN_DOCS`, `TECH_TREE_GAP_ANALYSIS`, `ERA_ARC_DESIGN_V2`, `MULTIPLAYER_LIVE_PLAY_PROPOSAL`, `LIVE_SERVICES_ARCHITECTURE`, `OPEN_WORLD_SCALE_PROPOSAL`, `COMPETITIVE_POSITIONING`, PR #1 (`feat: CityMajor Web v1 — R3F + WASM integration spine`)

---

## Executive Summary

CityMajor has crossed a critical inflection point: the **web v1 integration spine** (R3F + WASM sim bridge, 256×256 world, zoning UI, Herald/narrative stubs) is in review on PR #1, while parallel design agents have produced coherent post-v1 roadmaps for multiplayer, open regions, era progression, and live services. The product wedge is clear — **deep simulation in the browser with zero install**, **Herald AI narrative**, and **co-op city building** in a genre where CS2 and Manor Lords have no multiplayer.

**v1 launch is blocked** not by vision but by **sim parity gaps** (traffic stub, dead tech unlocks, era logic bypass), **missing player-facing research UI**, **incomplete 3D asset pipeline** (Meshy proposed, ~500 GLBs not shipped), and **persistence/auth stubs** (local JSON, no cloud saves). These are 6–10 weeks of focused engineering, not a replatform.

**v1.1 (30 days post-launch)** should ship quick wins that make the Frontier→Industrial arc feel complete: research panel, era transition fanfare, hero landmarks batch, real traffic BPR, cloud saves, and doc consolidation so agents stop building against stale Godot/Steam specs.

**v2** is three pillars: **Live Co-op** (2–4 players, host-authoritative), **Open Region** (NPC neighbor towns + targeted trade via existing `TradeSystem`), and **Meshy art complete** (~500 archetype GLBs + 8 hero landmarks). This requires a **headless `Forge.SimCore` tick server** for competitive integrity — estimated ~$170–250/mo per 1K DAU vs ~$40 for async-only.

**v3** unlocks **MMO-lite regional open world**: 512–1024 map streaming, simulation LOD, player cities on a shared regional map with server-authoritative trade and migration. This is explicitly **not** v1 or v2 scope; scope creep here is the #1 product risk.

**Resource ask:** v1–v1.1 is achievable solo + AI automation (Meshy batch gen, Herald templates, codegen). v2 needs **one backend/multiplayer engineer** (6-month contract) and **part-time 3D art QA** (~20 hrs/week). v3 needs a **dedicated sim infra engineer** or senior full-stack with .NET + WebTransport experience. Everything else (docs, Linear hygiene, competitive analysis, gap audits) stays AI-automated.

---

## Current State (July 2026)

| Layer | Status | Gap |
|-------|--------|-----|
| **Client (R3F)** | M0→integration: instanced buildings, chunk LOD L0–L3, zoning toolbar, procedural fallback | Meshy GLBs mostly absent; hero landmarks not in scene |
| **Sim (WASM)** | `Forge.SimWasm` ticks daily/monthly; ~220 starter buildings; zoning/roads wired | Traffic is stub; `WasmEraDeriver` bypasses `ResearchSystem` era logic |
| **Research** | `ResearchSystem.cs` + `technologies.json` (156 techs) bundled | No UI; worker drops RP fields; most unlocks map to unimplemented buildings |
| **Narrative** | Herald panel + `/api/narrative/event` stub (10/day quota) | Template fallback only; no grounded sim context pipeline |
| **Persistence** | In-memory `/api/saves`, local `.data/saves.json` | No cloud schema, auth, or conflict resolution |
| **Multiplayer** | Not in v1 scope (`CLAUDE.md`) | Proposals ready (`MULTIPLAYER_LIVE_PLAY`, `LIVE_SERVICES`) |
| **Docs** | Strong 3D ADRs; gap audit complete | ~25/33 design docs still describe Godot/Steam/pixel at 10–100× scale |

**Competitive moat (validated):** Browser deep sim + Herald AI + era arc + frictionless co-op URL — no tier-1 city builder offers co-op ([COMPETITIVE_POSITIONING](./COMPETITIVE_POSITIONING.md)).

---

## v1 Launch Blockers (Must Fix)

These items block a credible public launch (Founder Pass / F2P core). Ordered by dependency.

### P0 — Sim correctness & player loop

1. **Research UI v1** — Wire `researchPoints`, `researchRate`, `techCount` through `sim-worker` → `SimResources` → HUD; add tech panel + `enqueue_research` command ([TECH_TREE_GAP_ANALYSIS](./TECH_TREE_GAP_ANALYSIS.md) §4.1).
2. **Fix era derivation** — Remove `WasmEraDeriver` override; use `ResearchSystem` population + tech-count gates for Frontier→Industrial arc ([ERA_ARC_DESIGN_V2](./ERA_ARC_DESIGN_V2.md) §5–6).
3. **v1 tech/building subset** — Define and implement the Frontier→Industrial content slice: ~40 active techs, ~60 building types with working unlocks (not 156 dead techs).
4. **Replace traffic stub** — Wire BPR edge flow from `AGENT_04` / `SIMULATION_ARCHITECTURE` §4; traffic is core to city-builder feel and Herald context.
5. **Economy loop closure** — Leontief I/O or simplified RCI + budget tick visible in HUD; player must see cause/effect from zoning → growth → revenue.

### P0 — Visual & performance

6. **Meshy batch 1 (minimum viable art)** — Generate and ship ~80–120 core archetype GLBs (res/com/ind × Frontier + Industrial) per [MESHY_ASSET_PIPELINE](./MESHY_ASSET_PIPELINE.md); procedural fallback remains for missing keys.
7. **256×256 perf budget** — Hold 30 FPS on mid-tier laptop: snapshot throttling (≤4 Hz), instance cap ~5k, chunk culling validated on WASM path ([PR #1 `web/PERF.md`](../web/PERF.md)).
8. **8 hero landmarks (v1 arc)** — Ship at least 3 era-gate landmarks (Grand Terminus, first factory complex, civic hall) per [MESHY_HERO_LANDMARKS](./MESHY_HERO_LANDMARKS.md).

### P0 — Persistence & monetization

9. **Cloud save v1** — Supabase Auth + R2 signed URLs; 3 saves free / 20 Founder Pass; schema doc (`SAVE_FORMAT_WEB` — currently missing per gap audit).
10. **Entitlements hardening** — Replace stub `/api/me/entitlements` with verified JWT + Founder Pass flag; gate Herald unlimited events.

### P1 — Launch polish (can ship week-of if tight)

11. **Herald v1 grounded context** — Pass structured sim snapshot (population, budget delta, recent events) to narrative API; template fallback when quota exceeded.
12. **Doc supersession banners** — Mark Godot/Steam/pixel docs historical; point all agents to `CLAUDE.md` + this roadmap ([GAP_AUDIT](./GAP_AUDIT_DESIGN_DOCS.md) top 10 fixes).
13. **Era transition moment** — One LLM-driven "Era Transition Quest" + UI fanfare when Industrial gate clears ([ERA_ARC_DESIGN_V2](./ERA_ARC_DESIGN_V2.md) §6.A).

**Exit criteria:** A new player can zone, research, grow Frontier→Industrial, see traffic and budget respond, save to cloud, read Herald headlines grounded in sim state, and experience 3D buildings (not全 procedural) at stable FPS — without hitting dead tech unlocks or era proxy bugs.

---

## v1.1 Quick Wins (30 Days Post-Launch)

Low-risk, high-perceived-value items that deepen retention without opening multiplayer/regional scope.

| # | Initiative | Effort | Impact |
|---|------------|--------|--------|
| 1 | **Research UX polish** — Category tabs, branching fork visuals, eureka tooltips | 1 wk | Makes 154-tech design legible |
| 2 | **Era quest chain** — 3 LLM era transition quests with landmark gates | 1.5 wk | Fixes "slow morph" problem; shareable moments |
| 3 | **Meshy batch 2** — Remaining Industrial + service buildings (~200 GLBs) | 1 wk + API cost | Visual consistency |
| 4 | **Events integration** — Wire 20 Frontier/Industrial events from `events.json` into WASM tick | 1 wk | Herald + gameplay variety |
| 5 | **Async cloud invites (v1.5 preview)** — Share city via link; session lock; read-only spectator | 2 wk | Validates Supabase path before Live Co-op |
| 6 | **GAME_FEEL_BIBLE v1** — Placement feedback, camera easing, audio stubs | 3 d | Agent consistency |
| 7 | **Mobile/tablet read-only** — Responsive HUD; touch pan/zoom; no editing | 1 wk | Cross-platform continuity bet |
| 8 | **Leaderboard (casual)** — City population / happiness snapshots; client-trusted OK for v1.1 | 3 d | Social proof; not competitive-ranked |
| 9 | **WASM bridge design doc** — Formalize snapshot schema (closes gap audit item) | 2 d | Reduces agent regressions |
| 10 | **Parking + cruising (subset)** — Implement §1 of FINAL_GAP_FILLS for downtown feel | 1 wk | Differentiation vs browser idlers |

**Cost note:** Meshy batch 2 ≈ 6,000 credits (~$60–120 depending on plan); automate via existing regen script.

---

## v2 Pillars

v2 is the first **live services** release. All three pillars share Supabase identity + optional headless sim server.

### Pillar A — Live Co-op (Same City)

**Target:** 2–4 players building one city in real time via shared URL.

| Component | Approach |
|-----------|----------|
| Sync | Host-authoritative WASM or headless `Forge.SimCore`; input forwarding; CRC32 desync detection |
| Transport | WebSocket or WebTransport; delta-compressed snapshots @ 4–8 Hz |
| Roles | Mayor / Zoning / Finance split ([LIVE_SERVICES](./LIVE_SERVICES_ARCHITECTURE.md)) |
| Fallback | Host migration; resync from cloud snapshot on disconnect |

**Phasing:** Builds on v1.5 async invites infrastructure. Do **not** ship public matchmaking in v2 — friend-invite only.

### Pillar B — Open Region (NPC Neighbors)

**Target:** Regional map UI with 3–5 simulated NPC towns; targeted trade routes using existing `TradeSystem.PartnerCityId`.

| Component | Approach |
|-----------|----------|
| Map | 512×512 regional view; player city = 256×256 claim; rest procedural ([OPEN_WORLD_SCALE](./OPEN_WORLD_SCALE_PROPOSAL.md) Phase 2) |
| Trade | Point-to-point routes; supply/demand profiles per NPC town; contract UI |
| Sim LOD | Full sim in player city; aggregate chunk sim for distant NPC tiles |
| Multiplayer | Async-only in v2; no player-owned neighbor cities yet |

This delivers the "Victoria 3 trade on a map" fantasy without MMO complexity.

### Pillar C — Meshy Art Complete

**Target:** Full `BUILDING_ARCHETYPE_3D` taxonomy (~500 GLBs) + 8 hero landmarks in production.

| Milestone | Assets | Notes |
|-----------|--------|-------|
| v1 (blocker) | ~120 core | See above |
| v1.1 | +200 Industrial/service | Batch automation |
| v2 | +180 Postwar/Modern subset | Era 3–4 visuals for early access of full arc |
| v2 complete | 8 heroes + polish pass | QA checklist from MESHY_HERO_LANDMARKS |

**Pipeline automation (AI):** Prompt templates → Meshy preview/refine → gltf-transform pivot fix → LOD decimation → R2 CDN. Human QA: 20 hrs/week spot-check silhouettes and era readability.

### v2 Infrastructure

- **Forge.SimCore headless daemon** on docker-fleet — authoritative ticks for co-op rooms and future ranked features.
- **Supabase** for auth, RLS on saves/trades, Realtime presence.
- **Cost envelope:** ~$170–250/mo per 1K DAU ([LIVE_SERVICES](./LIVE_SERVICES_ARCHITECTURE.md) §Cost Model).

---

## v3 — Open World / MMO-Lite Regions

**Explicitly deferred.** v3 is player cities on a **shared 1024×1024 regional map** with server-authoritative trade, migration, pollution, and regional events ([OPEN_WORLD_SCALE](./OPEN_WORLD_SCALE_PROPOSAL.md) Phase 3, [MULTIPLAYER_LIVE_PLAY](./MULTIPLAYER_LIVE_PLAY_PROPOSAL.md) §3).

| Capability | Requirement |
|------------|-------------|
| Map streaming | Chunk unload from WASM + IndexedDB cache; <500 MB WASM memory budget |
| Simulation LOD | High LOD (building-level) in camera focus; aggregate chunk sim elsewhere |
| Authority | `Forge.Server` C# headless; clients are viewers + input relayers |
| Anti-cheat | Server validates intents, not results; no client-trusted trade |
| Monetization | Season passes, premium biomes — only after core loop proven |

**Gate to start v3:** v2 Live Co-op stable at 2–4 players for 30 days; Open Region NPC trade live; Meshy art ≥80% complete; DAU >5K or equivalent beta cohort.

---

## Risk Register

| ID | Risk | Likelihood | Impact | Mitigation |
|----|------|------------|--------|------------|
| R1 | **WASM perf regression** — 256×256 OK but mobile/low-RAM browsers OOM | Medium | High | Strict 500 MB cap; sim LOD early; procedural fallback; perf CI on `/play` |
| R2 | **Art pipeline cost overrun** — 500 assets × 30 credits = 15K credits | Medium | Medium | Batch automation; hero landmarks manual QA only; era-subset shipping |
| R3 | **Multiplayer scope creep** — Shipping regional PvP before co-op stable | High | Critical | Phase gates in this doc; friend-invite only v2; no public matchmaking until v2.1 |
| R4 | **Dead tech / sim parity drift** — Agents add features against full 5-era JSON | Medium | High | v1 content subset doc; CI check: unlock → building exists |
| R5 | **Doc contradictions** — Steam/pixel/100K HH specs mislead agents | Medium | Medium | Gap audit fixes; supersession banners; `CLAUDE.md` as sole onboarding |
| R6 | **Client-authoritative cheating** — Ranked/trade exploits if server sim delayed | Low (v1) / High (v3) | Critical | No competitive rewards until headless server; golden rule from LIVE_SERVICES |
| R7 | **Herald LLM cost/latency** — 10 events/day free tier unsustainable at scale | Medium | Medium | Template fallback; self-hosted Qwen for Founder tier; cache headline templates |
| R8 | **Meshy quality variance** — Inconsistent era readability at city scale | Medium | Medium | Strict prompt templates; human QA pass; procedural fallback per archetype key |

---

## Resource Ask: Hiring vs AI Automation

### Keep AI-automated (no hire)

| Function | Tooling |
|----------|---------|
| Design doc synthesis & gap audits | Parallel agents + retrieve MCP |
| Meshy batch generation & gltf-transform | Scripts + MCP Meshy |
| Herald template / headline drafts | LLM with sim context injection |
| Codegen for sim wiring, API routes, UI scaffolds | Cursor agents + CodeRabbit gate |
| Competitive / market research | Firecrawl + design docs |
| Linear issue hygiene & doc cross-links | Agent-maintained (this doc) |
| Doc supersession & scope charters | Agent PRs |

### Hire or contract (v2+)

| Role | When | Scope | FTE |
|------|------|-------|-----|
| **Backend / multiplayer engineer** | v2 kickoff | WebSocket rooms, host migration, Supabase RLS, Forge.SimCore headless | 0.5–1.0 × 6 mo |
| **3D art QA / tech artist** | v1 launch → v2 | Meshy QA, pivot/LOD fixes, hero landmark integration | 0.25 FTE |
| **DevOps / sim infra** (optional) | v2.5–v3 | docker-fleet sim nodes, WebTransport, regional sharding | 0.25 FTE or consultant |

### Solo + AI carries v1

The integration spine proves one developer + AI can ship browser WASM + R3F. v1 blockers are **integration and subsetting**, not greenfield architecture. Do not hire before v1 launch unless cloud save + auth slips >4 weeks.

---

## Linear Issue Drafts

> Drafts only — create in Linear when SB epic is opened. Do not block on MCP.

---

### Draft 1: `[v2] Live Co-op — Host-Authoritative Room Sync`

**Priority:** High (v2 pillar)  
**Labels:** multiplayer, backend, wasm  
**Estimate:** 8–12 weeks

**Description:**

Implement 2–4 player live co-op for a single shared city, building on v1.5 async cloud saves.

**Acceptance criteria:**
- [ ] Friend-invite flow via Supabase Auth + shareable room URL
- [ ] Host runs authoritative sim tick (headless `Forge.SimCore` or designated client host with CRC32 validation)
- [ ] Input forwarding for zoning, roads, budget commands; server/host rejects invalid intents
- [ ] Delta-compressed state snapshots @ 4–8 Hz over WebSocket
- [ ] Host migration on disconnect; resync from cloud snapshot
- [ ] Optional role split: Mayor / Zoning / Finance (UI badges only in v2.0)
- [ ] Desync detection + automatic resync path documented

**References:** `MULTIPLAYER_LIVE_PLAY_PROPOSAL.md` §2, `LIVE_SERVICES_ARCHITECTURE.md` v2 Co-op

**Out of scope:** Public matchmaking, regional map, PvP trade.

---

### Draft 2: `[v2] Open Region — NPC Town Trade Routes`

**Priority:** High (v2 pillar)  
**Labels:** simulation, economy, ui  
**Estimate:** 6–8 weeks

**Description:**

Introduce a regional map layer with 3–5 NPC neighbor towns. Players establish targeted trade routes using the existing `TradeSystem.PartnerCityId` contract mechanics.

**Acceptance criteria:**
- [ ] Regional map UI (512×512) with player city claim (256×256) highlighted
- [ ] 3–5 NPC towns with fixed supply/demand profiles (mining hub, industrial partner, agricultural exporter)
- [ ] Trade route UI: select partner, goods, volume caps; monthly settlement tick
- [ ] Aggregate sim LOD for NPC town tiles (no building-level sim outside player city)
- [ ] Regional events (oil shock, boom) affecting global and bilateral prices
- [ ] Herald generates trade-related headlines from route state

**References:** `OPEN_WORLD_SCALE_PROPOSAL.md` Phase 2, `TradeSystem` in Forge.Game.Simulation

**Out of scope:** Player-owned cities on shared map (v3).

---

### Draft 3: `[v1 blocker] Research UI — HUD, Tech Panel, Enqueue Command`

**Priority:** Urgent (v1 launch blocker)  
**Labels:** frontend, wasm, gameplay  
**Estimate:** 1–2 weeks

**Description:**

Expose the fully implemented `ResearchSystem` to the web client. Currently RP accumulates but is invisible and unqueueable; era logic is bypassed by `WasmEraDeriver`.

**Acceptance criteria:**
- [ ] `researchPoints`, `researchRate`, `techCount` propagated: `WasmExports` → `sim-worker` → `SimResources` → `ResourcesHud`
- [ ] Tech panel modal/drawer on `/play`: list available techs, prerequisites, unlocks
- [ ] `enqueue_research` command wired through worker to `EnqueueResearch(int techId)` export
- [ ] Remove or gate `WasmEraDeriver` override; era transitions follow ResearchSystem thresholds
- [ ] v1 subset: only Frontier→Industrial techs shown (~40 active)

**References:** `TECH_TREE_GAP_ANALYSIS.md`, `ERA_ARC_DESIGN_V2.md`

---

### Draft 4: `[v1.1] Era Quests — LLM Transition Quests + Landmark Gates`

**Priority:** Medium (v1.1 quick win)  
**Labels:** narrative, gameplay, llm  
**Estimate:** 2–3 weeks

**Description:**

Make era progression feel earned. When the player approaches Industrial gate thresholds, trigger an LLM-driven Era Transition Quest and require a monumental landmark build.

**Acceptance criteria:**
- [ ] Population + tech-count + stability gates defined for Frontier→Industrial ([ERA_ARC_DESIGN_V2](./ERA_ARC_DESIGN_V2.md) §6.C)
- [ ] Quest generator: Herald proposes quest (e.g., "Centennial Exhibition") with trackable objectives
- [ ] Landmark gate: Grand Terminus (or equivalent) must be constructed to unlock Industrial era visuals/tech band
- [ ] Completion fanfare: UI modal + Herald celebratory edition + era palette shift
- [ ] Template fallback when Herald quota exceeded

**References:** `ERA_ARC_DESIGN_V2.md` §5–6, `MESHY_HERO_LANDMARKS.md`

---

### Draft 5: `[v2] Cloud Sim Server — Headless Forge.SimCore Tick Daemon`

**Priority:** High (v2 infrastructure)  
**Labels:** backend, infra, simulation  
**Estimate:** 6–10 weeks

**Description:**

Deploy server-authoritative simulation for live co-op rooms, spectator streams, and future competitive features. Clients send intents; server ticks and broadcasts deltas.

**Acceptance criteria:**
- [ ] `Forge.SimCore` runs headless on docker-fleet (.NET 8 daemon)
- [ ] Fixed-interval tick engine (15 ticks/sec game-normal); room-based isolation
- [ ] WebSocket RPC: client intents in, delta-compressed state out
- [ ] Read-only spectator stream (no local sim required for viewers)
- [ ] Snapshot persistence to R2 between sessions
- [ ] Load test: 10 concurrent 4-player rooms on single AX41 node
- [ ] Security: no client-trusted sim results for any ranked or trade feature

**References:** `LIVE_SERVICES_ARCHITECTURE.md` v2, `MULTIPLAYER_LIVE_PLAY_PROPOSAL.md` §3

**Cost target:** ≤$250/mo infrastructure at 1K DAU (see LIVE_SERVICES cost model).

---

## Milestone Timeline (Indicative)

```
2026 Q3  │ v1 launch blockers closed → public beta / Founder Pass
2026 Q4  │ v1.1 quick wins → era quests, Meshy batch 2, async invites
2027 Q1  │ v2 Live Co-op + Open Region (NPC) beta
2027 Q2  │ v2 Meshy complete + headless sim server GA
2027 H2+ │ v3 gate review → regional MMO-lite only if v2 metrics hit
```

---

## Document Hierarchy (Post-Roadmap)

```
CLAUDE.md                              ← dev onboarding
CTO_IMPROVEMENT_ROADMAP_2026-07.md     ← THIS DOC: executive priorities
docs/design/COMPETITIVE_POSITIONING.md
docs/design/TECH_TREE_GAP_ANALYSIS.md
docs/design/ERA_ARC_DESIGN_V2.md
docs/design/MULTIPLAYER_LIVE_PLAY_PROPOSAL.md
docs/design/LIVE_SERVICES_ARCHITECTURE.md
docs/design/OPEN_WORLD_SCALE_PROPOSAL.md
docs/design/GAP_AUDIT_DESIGN_DOCS.md
MASTER_GAME_CONCEPT.md                 ← sim/economy north star (web sections)
BUILDING_ARCHETYPE_3D.md + MESHY_*.md  ← art contract
```

---

*Synthesized 2026-07-04 from parallel agent outputs on branch `feat/wasm-r3f-integration-2026-07-04`. Revisit after v1 launch retrospective.*
