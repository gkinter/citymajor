# CityMajor — CTO Improvement Roadmap (July 2026)

**Author:** CTO synthesis (parallel agent docs + PR #1 state)  
**Worktree:** `citymajor-web-r3f-spike` · Branch: `feat/wasm-r3f-integration-2026-07-04`  
**Last progress update:** 2026-07-05 — wave-4 depth (`9b82eb1` + uncommitted WT)  
**Sources:** `GAP_AUDIT_DESIGN_DOCS`, `GAMEPLAY_LOOP_IMPROVEMENTS`, `TECH_TREE_GAP_ANALYSIS`, `ERA_ARC_DESIGN_V2`, `MULTIPLAYER_LIVE_PLAY_PROPOSAL`, `LIVE_SERVICES_ARCHITECTURE`, `OPEN_WORLD_SCALE_PROPOSAL`, `COMPETITIVE_POSITIONING`, `MESHY_ASSET_PIPELINE`, `MESHY_HERO_LANDMARKS`, `FINAL_GAP_FILLS`, PR #1 (`feat: CityMajor Web v1 — R3F + WASM integration spine`), [V1_MERGE_CHECKLIST](./V1_MERGE_CHECKLIST.md)

---

## Executive Summary

CityMajor has crossed a critical inflection point: the **web v1 integration spine** (R3F + WASM sim bridge, 256×256 world, zoning UI, mayor HUD, Herald narrative) is in review on PR #1, with **wave-4 depth** (citizens, CMJR saves, laws read-only, trade strip, Herald LLM + council commands) landed at `9b82eb1` and polish uncommitted in `citymajor-web-r3f-spike`. Parallel design agents have produced coherent post-v1 roadmaps for multiplayer, open regions, era progression, and live services. The product wedge is clear — **deep simulation in the browser with zero install**, **Herald AI narrative**, and **co-op city building** in a genre where CS2 and Manor Lords have no multiplayer ([COMPETITIVE_POSITIONING](./COMPETITIVE_POSITIONING.md)).

**The sim is still ahead of the shipped player experience on art and persistence.** WASM runs economy, zone growth, 80 events, politics, budget, Leontief trade, law catalog, and era derivation — `/play` is now a **mayor sim with depth panels**, not just a zoning sandbox, but Meshy art (~7% of batch 1), cloud auth, and perf sign-off remain gaps ([GAMEPLAY_LOOP_IMPROVEMENTS](./GAMEPLAY_LOOP_IMPROVEMENTS.md)). v1 launch is blocked by **incomplete art pipeline** (~500 GLBs not shipped), **persistence/auth stubs** (CMJR interim JSON, no Supabase), and **perf gate** — not by greenfield sim architecture. **~52% toward public launch; ~74% toward preview-beta spine** (see § v1 progress snapshot).

**v1.1 (30 days post-launch)** should ship quick wins that complete the mayor fantasy: RCI demand bars, Herald wired to `events.json`, era progress meter, approval/budget HUD, guided onboarding, and async cloud invites — before opening multiplayer or regional scope.

**v2** is three pillars: **Live Co-op** (2–4 players, host-authoritative), **Open Region** (NPC neighbor towns + targeted trade via existing `TradeSystem`), and **Meshy art complete** (~500 archetype GLBs + 8 hero landmarks). This requires a **headless `Forge.SimCore` tick server** for competitive integrity — estimated ~$170–250/mo per 1K DAU vs ~$40 for async-only.

**v3** unlocks **MMO-lite regional open world**: 512–1024 map streaming, simulation LOD, player cities on a shared regional map with server-authoritative trade and migration. Explicitly **not** v1 or v2 scope; scope creep here is the #1 product risk.

**Resource ask:** v1–v1.1 is achievable solo + AI automation (Meshy batch gen, Herald templates, codegen). v2 needs **one backend/multiplayer engineer** (6-month contract) and **part-time 3D art QA** (~20 hrs/week). v3 needs a **dedicated sim infra engineer** or senior full-stack with .NET + WebTransport experience.

### v1 progress snapshot (honest %, post–wave-4)

> **Wave-4** = Phase 3 depth sprint: citizens, CMJR saves, laws (read-only), trade HUD, Herald LLM + commands. Committed at `9b82eb1`; smoke/canvas/GLB polish **uncommitted** in `citymajor-web-r3f-spike` WT (tip `90faca9` + dirty tree).

| Roll-up | % | Basis |
|---------|---|--------|
| **v1 public launch** (Founder Pass + cloud + art-complete + perf sign-off) | **~52%** | Weighted: player loop ~78%, art ~7%, persistence/monetization ~38%, perf/QA ~22% |
| **v1 integration spine** (PR #1 merge / preview beta) | **~74%** | WASM+R3F+zoning+HUD core shipped; wave-4 HUD paths land; Meshy/Stripe/perf still deferred |
| **Phase 3 depth goal** ([V1_GAMEPLAY_BUILD_PLAN](./V1_GAMEPLAY_BUILD_PLAN.md) §4) | **~48%** | Leontief panel partial; L2 citizens partial; CMJR partial; Herald partial; hero GLB absent |

#### Wave-4 feature areas

| Area | % | Shipped | Still missing |
|------|---|---------|---------------|
| **Citizens** | **58%** | `CitizenPanel`, dot→tile drill-down, aggregate HH/resident stats, toolbar badge | WASM `populationL2.households` export (named list, per-HH happiness/commute); citizen smoke lands in uncommitted WT only |
| **Saves (CMJR)** | **48%** | `export_cmjr` / `applyWasmSave`, POST/GET blob API, slot UI | Interim JSON in chunk `0x01` only — no SoA chunks; no Supabase auth/multi-device; deterministic WASM round-trip smoke skips when export unavailable |
| **Laws** | **32%** | `LawSystem` catalog + active tracking in sim; read-only panel (definition/active counts) | Ordinance toggle UI; effect application to budget/approval/zoning; law panel smoke (uncommitted WT) |
| **Trade HUD** | **52%** | Global market monthlies (`PartnerCityId=-1`); trade balance strip in `ResourcesHud` + `EconomyPanel` | Inter-city route UI; contract negotiation; Herald trade headlines |
| **Herald** | **68%** | Active events → headlines; council options → WASM (`AdjustBudget`, `ApplyApprovalDelta`, `BoostResearch`, `ResolveHeraldEvent`); LLM path + quota + template fallback | Preview `NARRATIVE_LLM_*` keys unset; LLM smoke; Founder-tier entitlements still stubbed on preview |

#### P0 launch blockers (§ v1 Launch Blockers) — item status

| # | Item | % |
|---|------|---|
| 1–7 | RCI, approval/budget, research UI, era derivation, era meter, v1 tech subset, Herald↔events | **~95%** shipped (phase 1 + wave-4) |
| 8 | Traffic (BPR, not stub) | **~55%** — heatmap + road paint; full BPR edge flow TBD |
| 9–11 | Meshy batch 1, perf budget, 3 heroes | **~8% / ~15% / ~20%** — 8 Meshy GLBs on disk (~7% of 120 target); perf CI stub only; landmark GLB probe + box fallback |
| 12–13 | Cloud save, entitlements | **~40% / ~35%** — CMJR blob path; no verified JWT + Founder on preview |
| 14–16 | Herald clicks, onboarding, doc banners | **~90%** — council cmds shipped; onboarding shipped; doc supersession partial |

**Honest read:** The mayor loop is **playable and legible** on preview; wave-4 closed the biggest *visibility* gaps (citizens, trade strip, save blob, Herald cmds). v1 launch is still blocked by **art batch** (~93% of GLBs missing), **auth/cloud parity**, and **perf sign-off** — not by sim greenfield work.

### Canonical tracking (Linear + Meshy)

Engineering priorities in this doc are mirrored in Linear for phase planning and issue hygiene:

- **Linear canonical roadmap:** [CityMajor — Canonical Roadmap (v1 → Full Vision)](https://linear.app/softblaze/document/citymajor-canonical-roadmap-v1-full-vision-eb7277adafd0) — initiative, phase epics (v1 / v1.5 / v2 / full vision), and issue mapping. Master tracker: [SB-3708](https://linear.app/softblaze/issue/SB-3708).
- **Meshy asset batches:** [`MESHY_ASSET_CATALOG.md`](./MESHY_ASSET_CATALOG.md) — 500-key taxonomy, P0–P3 batch table (counts, credits, manifest paths), hero landmarks, and Linear batch issues under [SB-3730](https://linear.app/softblaze/issue/SB-3730).

---

## Current State (July 2026, post–wave-4)

| Layer | Status | Gap |
|-------|--------|-----|
| **Client (R3F)** | Instanced buildings, chunk LOD L0–L3, zoning/road/tools, citizen/law/economy panels, Herald + research drawers, CMJR save UI | **8 Meshy GLBs** (~7% of v1 art target); hero landmark = GLB probe + box fallback |
| **Sim (WASM)** | Daily/monthly tick; zoning/roads; 80 events; Leontief + global trade; law catalog; era gates via `ResearchSystem` | Traffic BPR incomplete; `populationL2` household list not exported; law toggles not wired to HUD |
| **Player loop** | RCI, approval/budget, era checklist, services/traffic overlays, economy shortages, trade strip, onboarding | Law enactment; inter-city trade routes; Leontief→zoning hint upgrade |
| **Research** | HUD + tech panel + `enqueue_research`; v1 unlock bridge + CI guard | Postwar/Modern techs hidden; some unlock effects still shallow |
| **Narrative** | Herald ↔ `events.json`; council options → WASM; LLM path + template fallback; news ticker | Preview LLM keys unset; Herald LLM smoke; Founder entitlements stub |
| **Persistence** | CMJR `export_cmjr` + blob API + slot list (interim JSON chunk) | SoA chunks; Supabase auth; multi-device conflict resolution |
| **Multiplayer** | Not in v1 scope (`CLAUDE.md`) | Proposals ready (`MULTIPLAYER_LIVE_PLAY`, `LIVE_SERVICES`) |
| **Docs** | `WEB_V1_SCOPE`, merge checklist, gameplay plan, this roadmap | Godot/Steam/pixel supersession banners incomplete on legacy agent briefs |

**Competitive moat (validated):** Browser deep sim + Herald AI + era arc + frictionless co-op URL — no tier-1 city builder offers co-op.

---

## v1 Launch Blockers (Must Fix)

These items block a credible public launch (Founder Pass / F2P core). Ordered by dependency.

### P0 — Player loop (sim export, not new systems)

The gameplay loop audit ranks **exporting existing sim state** above building new systems. Without these, the product is a city painter, not a mayor sim.

1. **RCI demand bars in HUD** — Export `residentialDemand` / `commercialDemand` / `industrialDemand` from WASM `GetStatus`; render classic R/C/I meters. Unblocks the core SimCity loop ([GAMEPLAY_LOOP_IMPROVEMENTS](./GAMEPLAY_LOOP_IMPROVEMENTS.md) #1).
2. **Approval + happiness + monthly budget in HUD** — Extend `SimSnapshotDto` with approval, happiness, monthly income/expenses; mirror desktop `HudPanel` essentials (#6).
3. **Research UI v1** — Wire `researchPoints`, `researchRate`, `techCount` through `sim-worker` → `SimResources` → HUD; add tech panel + `enqueue_research` command ([TECH_TREE_GAP_ANALYSIS](./TECH_TREE_GAP_ANALYSIS.md) §4.1).
4. **Fix era derivation** — Remove `WasmEraDeriver` override; use `ResearchSystem` population + tech-count gates for Frontier→Industrial arc ([ERA_ARC_DESIGN_V2](./ERA_ARC_DESIGN_V2.md) §5–6).
5. **Era progress meter** — Show pop/RP/industry checklist + % toward gates from `WasmConfig` (#5).
6. **v1 tech/building subset** — Define and implement the Frontier→Industrial content slice: ~40 active techs, ~60 building types with working unlocks (not 156 dead techs).
7. **Wire Herald to active sim events** — Export `activeEvents[]` from WASM; map event `typeId` → headline from `events.json` (#2). Template path only for v1; LLM is Founder-tier v1.1.
8. **Replace traffic stub** — Wire BPR edge flow from `AGENT_04` / `SIMULATION_ARCHITECTURE` §4; traffic is core to city-builder feel and Herald context.

### P0 — Visual & performance

9. **Meshy batch 1 (minimum viable art)** — Generate and ship ~80–120 core archetype GLBs (res/com/ind × Frontier + Industrial) per [MESHY_ASSET_PIPELINE](./MESHY_ASSET_PIPELINE.md); procedural fallback remains for missing keys.
10. **256×256 perf budget** — Hold 30 FPS on mid-tier laptop: snapshot throttling (≤4 Hz), instance cap ~5k, chunk culling validated on WASM path.
11. **3 hero landmarks (v1 arc)** — Grand Terminus, first factory complex, civic hall per [MESHY_HERO_LANDMARKS](./MESHY_HERO_LANDMARKS.md).

### P0 — Persistence & monetization

12. **Cloud save v1** — Supabase Auth + R2 signed URLs; 3 saves free / 20 Founder Pass; schema doc (`SAVE_FORMAT_WEB` — currently missing per gap audit).
13. **Entitlements hardening** — Replace stub `/api/me/entitlements` with verified JWT + Founder Pass flag; gate Herald unlimited events.

### P1 — Launch polish (can ship week-of if tight)

14. **Clickable Herald council options** — Dispatch sim commands (budget line items, law toggles, event response stubs) (#3).
15. **Guided onboarding: 5-step overlay** — Zone residential → watch growth → open Herald → save city → reach 500 pop (#7).
16. **Doc supersession banners** — Mark Godot/Steam/pixel docs historical; point all agents to `CLAUDE.md` + this roadmap ([GAP_AUDIT](./GAP_AUDIT_DESIGN_DOCS.md) top 10 fixes).

**Exit criteria:** A new player can read RCI demand, zone accordingly, research tech, grow Frontier→Industrial with visible era progress, see traffic and budget respond, save to cloud, read Herald headlines grounded in active sim events, and experience 3D buildings (not all procedural) at stable FPS — without hitting dead tech unlocks or era proxy bugs.

---

## v1.1 Quick Wins (30 Days Post-Launch)

Low-risk, high-perceived-value items that deepen retention without opening multiplayer/regional scope. Prioritized from gameplay loop audit impact/effort ranking.

| # | Initiative | Effort | Impact | Source |
|---|------------|--------|--------|--------|
| 1 | **Auto-Herald on event spawn** — Toast/slide-in when `EventSystem` enters Active; morning digest on session start | 1 wk | High | GAMEPLAY_LOOP #4 |
| 2 | **Era transition fanfare** — 3s modal + Herald special edition when era increments | 3 d | High | GAMEPLAY_LOOP #11, ERA_ARC §6 |
| 3 | **Era quest chain** — 3 LLM era transition quests with landmark gates | 1.5 wk | High | ERA_ARC §6.A–B |
| 4 | **Research UX polish** — Category tabs, branching fork visuals, eureka tooltips | 1 wk | Medium | TECH_TREE_GAP §4.2–4.3 |
| 5 | **Meshy batch 2** — Remaining Industrial + service buildings (~200 GLBs) | 1 wk + API cost | High | MESHY_ASSET_PIPELINE |
| 6 | **Zone growth feedback** — Picked-tile tooltip: demand, road access, desirability, growth chance | 1 wk | Medium | GAMEPLAY_LOOP #8 |
| 7 | **News ticker** — Scroll recent event names from sim (no LLM required) | 3 d | Medium | GAMEPLAY_LOOP #10 |
| 8 | **Async cloud invites (v1.5 preview)** — Share city via link; session lock; read-only spectator | 2 wk | High | MULTIPLAYER §1, LIVE_SERVICES v1.5 |
| 9 | **Bankruptcy / low-approval warnings** — Modal when `IsBankrupt` or approval <30% for 3 months | 1 wk | Medium | GAMEPLAY_LOOP #9 |
| 10 | **LLM Herald path (Founder tier)** — Pass structured event payload when quota allows | 2 wk | High | GAMEPLAY_LOOP #15 |
| 11 | **GAME_FEEL_BIBLE v1** — Placement feedback, camera easing, audio stubs | 3 d | Medium | GAP_AUDIT #9 |
| 12 | **WASM bridge design doc** — Formalize snapshot schema | 2 d | Low | GAP_AUDIT #7 |
| 13 | **Parking + cruising (subset)** — Implement §1 of FINAL_GAP_FILLS for downtown feel | 1 wk | Medium | FINAL_GAP_FILLS |
| 14 | **Mobile/tablet read-only** — Responsive HUD; touch pan/zoom; no editing | 1 wk | Medium | COMPETITIVE bet #5 |
| 15 | **Leaderboard (casual)** — Population / happiness snapshots; client-trusted OK for v1.1 | 3 d | Low | LIVE_SERVICES v1.5 |

**Recommended v1.1 sprint (two weeks):** RCI + era meter + approval/budget HUD (if not in v1), Herald event wiring + auto-toast, 5-step onboarding, era fanfare.

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
| v1 (blocker) | ~120 core | Frontier + Industrial res/com/ind |
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
| R1 | **Mayor fantasy gap** — Sim depth invisible; players churn as "zoning toy" | High | Critical | v1 blockers prioritize sim export (RCI, events, approval) before new systems |
| R2 | **WASM perf regression** — 256×256 OK but mobile/low-RAM browsers OOM | Medium | High | Strict 500 MB cap; sim LOD early; procedural fallback; perf CI on `/play` |
| R3 | **Art pipeline cost overrun** — 500 assets × 30 credits = 15K credits | Medium | Medium | Batch automation; hero landmarks manual QA only; era-subset shipping |
| R4 | **Multiplayer scope creep** — Shipping regional PvP before co-op stable | High | Critical | Phase gates in this doc; friend-invite only v2; no public matchmaking until v2.1 |
| R5 | **Dead tech / sim parity drift** — Agents add features against full 5-era JSON | Medium | High | v1 content subset doc; CI check: unlock → building exists |
| R6 | **Doc contradictions** — Steam/pixel/100K HH specs mislead agents | Medium | Medium | Gap audit fixes; supersession banners; `CLAUDE.md` as sole onboarding |
| R7 | **Client-authoritative cheating** — Ranked/trade exploits if server sim delayed | Low (v1) / High (v3) | Critical | No competitive rewards until headless server; golden rule from LIVE_SERVICES |
| R8 | **Herald LLM cost/latency** — 10 events/day free tier unsustainable at scale | Medium | Medium | Template fallback; self-hosted Qwen for Founder tier; cache headline templates |
| R9 | **Meshy quality variance** — Inconsistent era readability at city scale | Medium | Medium | Strict prompt templates; human QA pass; procedural fallback per archetype key |
| R10 | **Tick-based era advance** — Player reaches Industrial before understanding zoning | Medium | Medium | Era progress meter + quest gates; remove tick-only proxy in `WasmEraDeriver` |

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

> Drafts only — create in Linear when SB epic is opened. Titles use `[vX]` phase tags.

---

### Draft 1: `[v2] Live Co-op — Host-Authoritative Room Sync`

**Priority:** High (v2 pillar A)  
**Labels:** `multiplayer`, `backend`, `wasm`, `websocket`  
**Estimate:** 8–12 weeks  
**Epic:** CityMajor Live Services

#### Problem

CityMajor's competitive wedge includes **frictionless co-op** — no tier-1 city builder offers multiplayer ([COMPETITIVE_POSITIONING](./COMPETITIVE_POSITIONING.md) §4). v1 is single-player only. v1.5 async invites validate cloud persistence; v2 must deliver **2–4 players building the same city simultaneously** via a shared URL, without public matchmaking or regional scope.

#### Proposed solution

Implement host-authoritative room sync per `MULTIPLAYER_LIVE_PLAY_PROPOSAL.md` §2 and `LIVE_SERVICES_ARCHITECTURE.md` v2 Co-op:

- **Room lifecycle:** Supabase Auth → create/join room → host election → sim start.
- **Authority model:** Designated host runs authoritative tick (headless `Forge.SimCore` preferred; client-host acceptable for v2.0 beta with CRC32 validation).
- **Input model:** Clients send **intents** (zone paint, road place, budget approve) — never sim results.
- **State model:** Delta-compressed snapshots @ 4–8 Hz over WebSocket; subsystem CRC32 checksums for desync detection.
- **Roles (UI only v2.0):** Mayor (budget/approval), Zoning (paint/tools), Finance (taxes/bonds) — badges on player cursors.
- **Resilience:** Host migration on disconnect; full resync from R2 cloud snapshot; documented desync recovery path.

#### Acceptance criteria

- [ ] Friend-invite flow: authenticated user creates room, shares URL, 1–3 friends join
- [ ] All players see consistent building/zone state within 250 ms of input
- [ ] Invalid intents rejected server-side (out-of-bounds zone, insufficient funds)
- [ ] Host disconnect triggers migration or graceful pause with 60s rejoin window
- [ ] CRC32 mismatch triggers automatic resync without data loss
- [ ] Load test: 4-player room stable for 30-minute session on mid-tier hardware
- [ ] No public matchmaking; rooms are invite-only

#### Out of scope

Public matchmaking, regional map, PvP trade, spectator elections (v2.1+).

#### References

- `docs/design/MULTIPLAYER_LIVE_PLAY_PROPOSAL.md` §2
- `docs/design/LIVE_SERVICES_ARCHITECTURE.md` — v2 Co-op, Security Posture
- `docs/research/16-multiplayer-achievements-qa.md` (orphaned; promote to design/)

#### Dependencies

v1.5 async cloud saves + Supabase Auth live.

---

### Draft 2: `[v2] Open Region — NPC Town Trade Routes`

**Priority:** High (v2 pillar B)  
**Labels:** `simulation`, `economy`, `ui`, `trade`  
**Estimate:** 6–8 weeks  
**Epic:** CityMajor Regional Play

#### Problem

Single-city play against the anonymous global market (`PartnerCityId = -1`) limits economic depth. `Forge.Game.Simulation.TradeSystem` already supports point-to-point trade via `PartnerCityId`, and `ProductionChainRegistry` defines multi-tier chains that **require regional specialization** (mining hub → industrial partner). None of this is player-visible in v1.

#### Proposed solution

Ship Phase 2 of `OPEN_WORLD_SCALE_PROPOSAL.md`:

- **Regional map UI:** 512×512 overview; player city highlighted as 256×256 claim.
- **NPC towns (3–5):** Fixed profiles — e.g., Coal Ridge (exports ore), Harbor Vale (imports finished goods), Wheat County (agricultural).
- **Trade route UI:** Select partner, goods, volume caps; monthly settlement tick; contract renewal/cancellation.
- **Sim LOD:** Full building-level sim in player city; aggregate chunk-level production/consumption for NPC tiles.
- **Regional events:** Oil shock, commodity boom — affect global index and bilateral route profitability.
- **Herald integration:** Trade headlines from route state (surplus, embargo risk, price spike).

#### Acceptance criteria

- [ ] Regional map renders with player claim + 3 NPC town markers
- [ ] Player can establish ≥2 active trade routes with different goods
- [ ] Route revenue/cost visible in monthly budget breakdown
- [ ] NPC towns have stable supply/demand that creates meaningful specialization incentives
- [ ] Aggregate sim for NPC tiles does not degrade player-city FPS below 30
- [ ] Herald generates ≥1 trade-related headline per active route per in-game month

#### Out of scope

Player-owned cities on shared map (v3), live PvP trade negotiation, multiplayer regional sessions.

#### References

- `docs/design/OPEN_WORLD_SCALE_PROPOSAL.md` §2, §5 Phase 2
- `Forge.Game.Simulation.TradeSystem`, `ProductionChainRegistry`

#### Dependencies

v1 economy loop visible in HUD; Leontief or simplified RCI stable.

---

### Draft 3: `[v1 blocker] Research UI — HUD, Tech Panel, Enqueue Command`

**Priority:** Urgent (v1 launch blocker)  
**Labels:** `frontend`, `wasm`, `gameplay`, `research`  
**Estimate:** 1–2 weeks  
**Epic:** CityMajor v1 Launch

#### Problem

`ResearchSystem.cs` is fully implemented in WASM — RP accumulates, queue logic works, 156 techs load from JSON — but the web client is blind:

- `sim-worker.ts` drops `researchPoints`, `researchRate`, `techCount` from `WasmStatus`
- `ResourcesHud` shows Pop/Funds/Tick/Era only
- No tech panel; no `enqueue_research` command
- `WasmEraDeriver` overrides era transitions, bypassing research-gated progression

Players cannot research, cannot see era progress tied to tech, and most tech unlocks map to buildings that don't exist in the v1 pool ([TECH_TREE_GAP_ANALYSIS](./TECH_TREE_GAP_ANALYSIS.md)).

#### Proposed solution

**Phase A — Wire existing sim (3–5 days):**

1. Propagate research fields: `WasmExports.GetStatus()` → `sim-worker.readStatus()` → `SimResources` → `ResourcesHud`
2. Add `enqueue_research` to `SimCommand` enum; export `EnqueueResearch(int techId)` from WASM
3. Remove or gate `WasmEraDeriver` — era follows `ResearchSystem` thresholds aligned with `ERA_ARC_DESIGN_V2` §6.C

**Phase B — Tech panel v1 (5–7 days):**

4. Modal/drawer on `/play`: list available techs filtered by prerequisites
5. Show cost, unlocks, effects; enqueue on click
6. **v1 subset filter:** Only Frontier→Industrial band (~40 techs); hide Postwar/Modern/Future

#### Acceptance criteria

- [ ] HUD displays RP, RP/month rate, techs researched count
- [ ] Tech panel lists enqueueable techs with prerequisite tree
- [ ] Enqueued tech completes after RP threshold; unlock effects apply (building availability)
- [ ] Era transition requires research + population gates, not tick proxy alone
- [ ] v1 subset: no dead unlocks (CI or manual matrix: tech → building exists)

#### References

- `docs/design/TECH_TREE_GAP_ANALYSIS.md`
- `docs/design/ERA_ARC_DESIGN_V2.md` §5–6
- `base/data/tech/technologies.json`

#### Dependencies

v1 tech/building subset doc (can ship in same PR).

---

### Draft 4: `[v1.1] Era Quests — LLM Transition Quests + Landmark Gates`

**Priority:** Medium (v1.1 quick win)  
**Labels:** `narrative`, `gameplay`, `llm`, `era-arc`  
**Estimate:** 2–3 weeks  
**Epic:** CityMajor Era Arc

#### Problem

Frontier→Industrial progression is a **debug counter** — `ResourcesHud` shows an era badge but no progress meter, no fanfare, no quest. `ERA_ARC_DESIGN_V2` §5 documents: "The player never achieves the next era. The city just slowly morphs." Tick-based `WasmEraDeriver` can fire before the player understands zoning or demand ([GAMEPLAY_LOOP_IMPROVEMENTS](./GAMEPLAY_LOOP_IMPROVEMENTS.md) §3).

#### Proposed solution

Implement `ERA_ARC_DESIGN_V2` §6 proposals:

**A. Era gates (mechanical):**

- Population gate (e.g., 400+ for Industrial in v1 WASM config)
- Tech gate: ≥80% of Frontier-era techs researched
- Stability gate: approval ≥50% for 3 consecutive months

**B. Era Transition Quest (narrative):**

- When ≥2 of 3 gates met, Herald proposes quest (e.g., "The Centennial Exhibition")
- Trackable objectives: stockpile steel, build train station, pass education act
- LLM generates progress articles; template fallback when quota exceeded

**C. Landmark gate:**

- Industrial era locked until **Grand Terminus** (or equivalent hero landmark) constructed
- Meshy GLB from `MESHY_HERO_LANDMARKS.md`; placement UI on `/play`

**D. Completion fanfare:**

- 3s modal, era palette shift, Herald celebratory edition, unlock Industrial tech band

#### Acceptance criteria

- [ ] Era progress UI shows 3 gates with current/required values
- [ ] Quest triggers when approaching thresholds; objectives trackable in HUD
- [ ] Landmark build required before Industrial era unlock
- [ ] Completion triggers fanfare + Herald special edition
- [ ] Template fallback works when Herald quota exceeded

#### References

- `docs/design/ERA_ARC_DESIGN_V2.md` §4–6
- `docs/design/MESHY_HERO_LANDMARKS.md`
- `docs/design/GAMEPLAY_LOOP_IMPROVEMENTS.md` §3, #11

#### Dependencies

Research UI (Draft 3); at least 1 hero landmark GLB shipped.

---

### Draft 5: `[v2] Cloud Sim Server — Headless Forge.SimCore Tick Daemon`

**Priority:** High (v2 infrastructure)  
**Labels:** `backend`, `infra`, `simulation`, `docker-fleet`  
**Estimate:** 6–10 weeks  
**Epic:** CityMajor Live Services

#### Problem

v1/v1.5 are **client-authoritative** — WASM runs in the browser, saves are trusted blobs. This is acceptable for casual async play but violates the golden rule for competitive features: **never trust client sim** ([LIVE_SERVICES_ARCHITECTURE](./LIVE_SERVICES_ARCHITECTURE.md) Security Posture). Live co-op (Draft 1), spectator streams, ranked leaderboards, and v3 regional trade all require server-authoritative ticks.

#### Proposed solution

Deploy `Forge.SimCore` as a headless .NET 8 daemon on docker-fleet:

**Architecture:**

```
Client (R3F viewer)  ←WebSocket→  Forge.SimCore Room Daemon  ←→  R2 snapshots
       │                                      │
   intents only                         fixed tick (15 Hz game-normal)
   delta state in                       room isolation (1 city = 1 process)
```

**Components:**

1. **Room daemon:** One sim instance per co-op room; fixed-interval tick; input queue per player
2. **WebSocket RPC:** `POST intent` / `SUBSCRIBE state_delta` — protobuf or MessagePack payloads
3. **Spectator stream:** Read-only subscription; no local WASM required for viewers
4. **Persistence:** Snapshot to R2 on interval + graceful shutdown; restore on room recreate
5. **Validation:** Reject invalid intents (funds, bounds, tech prerequisites) before tick

#### Acceptance criteria

- [ ] Headless `Forge.SimCore` builds and runs on docker-fleet AX41 node
- [ ] Single room: 4 clients receive consistent state @ 4–8 Hz
- [ ] Spectator client renders city without running WASM locally
- [ ] Snapshot restore produces identical state hash after reload
- [ ] Load test: 10 concurrent 4-player rooms on single node without tick slip >5%
- [ ] No ranked/trade feature reads client-submitted sim results
- [ ] Infrastructure cost ≤$250/mo at 1K DAU (per LIVE_SERVICES cost model)

#### Out of scope

Regional sharding (v3), global matchmaking, anti-cheat kernel drivers.

#### References

- `docs/design/LIVE_SERVICES_ARCHITECTURE.md` — v2 Dedicated Sim Tick Server
- `docs/design/MULTIPLAYER_LIVE_PLAY_PROPOSAL.md` §3 (regional authority model)
- `docs/design/SIMULATION_ARCHITECTURE.md` (tick/LOD reference)

#### Dependencies

Live Co-op room protocol (Draft 1); Supabase Auth; R2 snapshot schema.

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
docs/design/GAMEPLAY_LOOP_IMPROVEMENTS.md
docs/design/COMPETITIVE_POSITIONING.md
docs/design/TECH_TREE_GAP_ANALYSIS.md
docs/design/ERA_ARC_DESIGN_V2.md
docs/design/MULTIPLAYER_LIVE_PLAY_PROPOSAL.md
docs/design/LIVE_SERVICES_ARCHITECTURE.md
docs/design/OPEN_WORLD_SCALE_PROPOSAL.md
docs/design/GAP_AUDIT_DESIGN_DOCS.md
MASTER_GAME_CONCEPT.md                 ← sim/economy north star (web sections)
BUILDING_ARCHETYPE_3D.md + MESHY_*.md  ← art contract
MESHY_ASSET_CATALOG.md               ← Meshy batch table + manifests
Linear: Canonical Roadmap doc        ← phase epics + SB mapping
```

---

*Synthesized 2026-07-04 from parallel agent outputs on branch `feat/wasm-r3f-integration-2026-07-04`. Progress % updated 2026-07-05 after wave-4 (`9b82eb1` + uncommitted `citymajor-web-r3f-spike` WT). Revisit after v1 launch retrospective.*
