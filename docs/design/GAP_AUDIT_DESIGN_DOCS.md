# CityMajor / Iron & Oak — Design Docs Gap Audit

**Date:** 2026-07-04  
**Scope:** `docs/design/*.md`, root concept/architecture docs, `base/data/` JSON  
**Pivot reference:** Web-only, R3F mesh 3D, WASM sim, 256×256 map, ~5k buildings, ~10k households, 1 era arc (Frontier → Industrial), Founder Pass monetization ([SB-3704](https://linear.app/softblaze/issue/SB-3704))

---

## 1. Doc inventory

| File | Purpose | Freshness vs web pivot |
|------|---------|------------------------|
| **CLAUDE.md** | Agent onboarding: Next.js + R3F + WASM stack, locked v1 scope, monetization | **Current** — canonical for web v1 |
| **docs/DEPLOY_WEB.md** | Coolify preview deploy, WASM build stages, procedural fallback | **Current** |
| **docs/design/MASTER_GAME_CONCEPT.md** | North-star GDD: identity, eras, sim summary, LLM, business | **Partial** — §“Web v1 pivot” + §8–9 updated; exec summary still pixel/Steam/100K HH/5 eras |
| **docs/design/SIMULATION_ARCHITECTURE.md** | C# sim spec: LOD ticks, traffic BPR, threading, memory, save binary format | **Partial** — sim logic valid; scale targets stale (1M tiles, 500K pop, 100K HH) |
| **docs/design/AI_ART_PIPELINE.md** | AI art tool evaluation + production workflow | **Partial** — §“3D Module Pipeline (Web v1)” added Jul 2026; body still pixel/Steam/Aseprite |
| **docs/design/BUILDING_ARCHETYPE_3D.md** | ADR: TypeId → mesh archetype keys, era bands, roof types | **Current** — Jul 2026, R3F contract |
| **docs/design/MESHY_ASSET_PIPELINE.md** | ADR: Meshy API workflow, naming, prompts, export | **Current** — Jul 2026 (Status: Proposed) |
| **docs/design/MESHY_HERO_LANDMARKS.md** | 8 hero landmark Meshy briefs, poly budgets, QA checklist | **Current** — Jul 2026 (Draft) |
| **docs/design/VISUAL_QUALITY_GUIDE.md** | Pixel-art quality bar, Godot perf, traffic feel | **Stale** — supersession banner at top; 500+ lines still Godot/pixel |
| **docs/design/GAME_IDENTITY.md** | Two depth axes; urban planning always deep | **Mostly valid** — sim philosophy; 100K households, no web scope |
| **docs/design/CITY_BUILDER_GDD.md** | Iron & Oak v2 GDD: hooks, systems, regional play | **Stale** — pixel art, 102 techs, no web pivot |
| **docs/design/TECH_STACK_RESEARCH.md** | Engine comparison; recommends Godot 4 + pure C# sim | **Stale** — pre-Forge, pre-WASM; contradicts shipped stack |
| **docs/design/MASTER_DEVELOPMENT_PLAN.md** | 10-agent parallel plan, 3–4 week prototype | **Stale** — Godot agents, Steam cloud saves |
| **docs/design/AGENT_01_CORE_ENGINE.md** | Godot scaffold, FlatBuffers saves, chunked grid | **Stale** — Godot rendering bridge |
| **docs/design/AGENT_02_SIMULATION.md** | Population/household structs, tick integration | **Mostly valid** — sim structs; Godot integration assumptions |
| **docs/design/AGENT_03_ECONOMY.md** | Budget, Leontief, trade | **Mostly valid** — sim; no web/API monetization |
| **docs/design/AGENT_04_TRANSPORT.md** | BPR traffic, transit modes | **Mostly valid** — scale assumptions high |
| **docs/design/AGENT_05_ZONING_BUILDINGS.md** | Zone growth, building placement rules | **Mostly valid** — 2D tile assumptions |
| **docs/design/AGENT_06_SERVICES.md** | Fire/police/health/education/utilities | **Mostly valid** — deep sim spec |
| **docs/design/AGENT_07_POLITICS.md** | Factions, elections, laws | **Mostly valid** |
| **docs/design/AGENT_08_UI_UX.md** | HUD, overlays, info architecture | **Stale** — Godot UI; no Next.js/HTML-over-canvas |
| **docs/design/AGENT_09_ART_PIPELINE.md** | 930+ pixel sprites, shaders | **Stale** — contradicts 3D pivot entirely |
| **docs/design/AGENT_10_GAME_FEEL.md** | Game feel research agent brief | **Stale** — depends on placeholder bible |
| **docs/design/GAME_FEEL_BIBLE.md** | Placement feel, audio, UX timing | **Placeholder** — 4-line stub only |
| **docs/design/AI_SIMULATION_RESEARCH.md** | LLM + emergence research | **Placeholder** — 4-line stub only |
| **docs/design/RESEARCH_TECH_TREE.md** | Full tech tree design (1850–2050) | **Mostly valid** — content spec; exceeds v1 single-arc scope |
| **docs/design/POLITICAL_LAW_SYSTEM.md** | Laws, factions, compliance | **Mostly valid** |
| **docs/design/EDUCATION_MEDIA_SYSTEM.md** | Schools, newspapers, media | **Mostly valid** |
| **docs/design/CULTURAL_DNA_EMERGENCE.md** | 8 cultural dimensions, archetypes | **Mostly valid** — 100K HH scale |
| **docs/design/VEHICLE_WEALTH_SYSTEM.md** | Vehicle spawn by wealth/era | **Stale** — 2D cosmetic sprites |
| **docs/design/GLOBAL_CITY_ARCHETYPES.md** | 10 regional archetypes, global trade, MP scaling | **Stale** — post-v1 feature; MP-heavy |
| **docs/design/EXPANDED_ZONES_EVENTS_SPORTS.md** | Zone types, events, sports leagues | **Mostly valid** — content depth; asset counts assume pixel |
| **docs/design/MISSING_SYSTEMS.md** | Emergency services, deep gaps | **Mostly valid** — sim depth; Iron & Oak branding |
| **docs/design/FINAL_GAP_FILLS.md** | Parking, remaining specs | **Mostly valid** — cross-references other docs |
| **docs/design/ELEVATION_CONSTRUCTION_SPEC.md** | 8 elevation levels, construction states | **Partial** — “Godot frontend” rendering hints |
| **docs/MESHY_ASSET_PIPELINE.md** | Placeholder GLB stub + regen script | **Current** — implementation stub, points to design ADR |

**Related (not in requested list but relevant):** `docs/research/*.md` (19 files) — includes multiplayer spec (`16-multiplayer-achievements-qa.md`), save UX (`14-ux-systems.md`), gap analysis (`07-gap-analysis.md`). These live outside `docs/design/` and are **not cross-linked** from design docs.

---

## 2. Contradictions between docs

### Platform & business

| Topic | Doc A | Doc B |
|-------|-------|-------|
| **Platform** | `CLAUDE.md`: browser only, no Steam v1 | `MASTER_GAME_CONCEPT` §1: Steam Next Fest Q3 2027, $24.99 EA Q1 2028 |
| **Monetization** | `CLAUDE.md`: free core + Founder Pass $24.99 + cosmetic shop | `MASTER_DEVELOPMENT_PLAN`: Steam achievements, workshop |
| **Audience sizing** | `MASTER_GAME_CONCEPT`: 2–5M Steam players | `CLAUDE.md`: web F2P, conversion TBD |

### Visual / rendering

| Topic | Doc A | Doc B |
|-------|-------|-------|
| **Art direction** | `MASTER_GAME_CONCEPT` §1 USP #6: Songs of Conquest pixel, 64×32 tiles | `MASTER_GAME_CONCEPT` §8 Web v1: mesh 3D GLTF, InstancedMesh LOD |
| **Renderer** | `VISUAL_QUALITY_GUIDE`, `AGENT_09`: Godot 4, GPUParticles2D, palette shaders | `CLAUDE.md`: R3F + Three.js; Forge Engine “obsolete” |
| **Building count** | `AI_ART_PIPELINE` exec: 600–800 unique sprites | `BUILDING_ARCHETYPE_3D` / `CLAUDE.md`: 40–60 archetypes/era, ~5k instanced max |
| **Meshy model** | `MESHY_ASSET_PIPELINE`: Meshy-6, 30 credits/asset | `MESHY_HERO_LANDMARKS`: meshy-5, triangle topology |

### Simulation scale

| Parameter | Web v1 (`CLAUDE.md`) | Legacy docs |
|-----------|----------------------|-------------|
| Map | 256×256 | `SIMULATION_ARCHITECTURE`: 1M tiles; `CITY_BUILDER_GDD`: large regional maps |
| Households | ~10,000 | `GAME_IDENTITY`, `MASTER_GAME_CONCEPT`: 100,000 |
| Buildings | ~5,000 instanced | `SIMULATION_ARCHITECTURE`: 50k+; `MASTER_GAME_CONCEPT` (original): 1024×1024 |
| Eras | 1 arc (Frontier → Industrial) | `MASTER_GAME_CONCEPT`, `RESEARCH_TECH_TREE`: 5 eras 1850–2050+ |
| Tech count | 156 in JSON | `CITY_BUILDER_GDD`: 102; `MASTER_GAME_CONCEPT`: 154 |

### Engine / architecture

| Topic | Stale claim | Current reality |
|-------|-------------|-----------------|
| **Primary engine** | `TECH_STACK_RESEARCH`, `AGENT_01`: Godot 4 C# | Next.js shell + WASM sim + R3F |
| **Forge Engine** | `AGENT_01`, `SIMULATION_ARCHITECTURE`: shipping OpenGL sprite batcher | `CLAUDE.md`: sim wiring reference only, not shipped |
| **Save format** | `AGENT_01`: FlatBuffers + Zstd; `SIMULATION_ARCHITECTURE`: custom binary `CMJR` magic | Web has `/api/saves` route; **no design doc** for cloud save schema vs WASM binary |
| **Threading** | `SIMULATION_ARCHITECTURE`: dedicated sim thread on 8-core CPU | `CLAUDE.md`: Web Workers + SharedArrayBuffer |

### Branding

- **Iron & Oak** (`CITY_BUILDER_GDD`, `VISUAL_QUALITY_GUIDE`, many headers) vs **CityMajor** (`MASTER_GAME_CONCEPT`, `CLAUDE.md`) vs candidates Brickborn / Smoke & Steeple.

### Data vs design

- **`buildings.json`** uses string IDs (`res_frontier_cabin`) and numeric `era: 0` — **`BUILDING_ARCHETYPE_3D`** uses numeric `TypeId` ranges (100–499) and era slugs. Mapping exists in code (`BuildingRenderer.cs`) but **not documented as a data-bridge spec**.
- **`technologies.json`** has 156 entries; `RESEARCH_TECH_TREE.md` prose tree may not match JSON IDs one-to-one (not verified line-by-line).

### Multiplayer

- `MASTER_GAME_CONCEPT` §10: P2P co-op, minimal sync — **still present**, not marked superseded for v1.
- `CLAUDE.md`: **no multiplayer** in v1 scope.
- `docs/research/16-multiplayer-achievements-qa.md`: detailed host-authoritative protocol — **orphaned** from `docs/design/`.

---

## 3. Missing docs (for web v1)

| Gap | Why it blocks | Partial coverage |
|-----|---------------|------------------|
| **Web v1 scope charter** (single doc) | Locked params scattered across `CLAUDE.md`, `MASTER_GAME_CONCEPT` pivot table, Linear | No “what we cut for v1” checklist |
| **Era arc design doc** | v1 = 1 arc only; no doc defines Frontier→Industrial transition triggers, tech gating, or content subset | `RESEARCH_TECH_TREE` is full 5-era; JSON has 5 era tags |
| **Multiplayer / networking ADR** | P2P spec in concept doc contradicts web-only v1; research doc exists but unlinked | `docs/research/16-multiplayer-achievements-qa.md` |
| **Save format spec (web)** | Cloud saves + WASM binary + versioning; three fragments, no unified spec | `SIMULATION_ARCHITECTURE` §17, `AGENT_01`, `research/14-ux-systems` |
| **WASM bridge contract** | Snapshot layout, tick rate, SharedArrayBuffer layout | Implementation in `web/` + `Forge.SimWasm`; **no design doc** |
| **3D rendering spec** | LOD bands, chunk culling, shadow tiers mentioned in banners only | Scattered in `VISUAL_QUALITY_GUIDE` header, `BUILDING_ARCHETYPE_3D`, code |
| **UI/UX for web** | HTML HUD over canvas, responsive layout, touch | `AGENT_08` is Godot-centric |
| **LLM proxy / entitlements** | 10 events/day, Founder unlimited, template fallback | `CLAUDE.md` one-liner; `MASTER_GAME_CONCEPT` §7 still self-hosted model |
| **Modding / Workshop** | Still in `MASTER_GAME_CONCEPT` §11 | No web mod story (CDN assets? user JSON?) |
| **GAME_FEEL_BIBLE** | Placeholder | Blocks feel-consistent implementation |
| **AI_SIMULATION_RESEARCH** | Placeholder | LLM integration depth undocumented |
| **Data bridge spec** | `buildings.json` ↔ TypeId ↔ archetype keys ↔ `sim-types` | Only `BUILDING_ARCHETYPE_3D` covers TypeId taxonomy |
| **Leontief I/O coefficients** | Called out in `research/07-gap-analysis` as blocking economy | Still no matrix doc in `docs/design/` |

---

## 4. `base/data/` JSON structure

### `base/data/tech/technologies.json` — **156 entries**

```json
{
  "id": "T001",
  "name": "Cobblestone Paving",
  "era": "frontier|industrial|postwar|modern|future",
  "category": "transport|...",
  "cost_rp": 25,
  "prerequisites": ["T001"],
  "unlocks": ["cobblestone_road"],
  "effects": { "road_capacity_multiplier": 3.0 },
  "eureka_condition": null | "string",
  "eureka_bonus": 0,
  "description": "...",
  "branch_group": null,
  "branch_exclusive": false
}
```

### `base/data/events/events.json` — **80 entries**

```json
{
  "id": "fire",
  "name": "Building Fire",
  "category": "natural|economic|...",
  "era_min": "frontier",
  "severity": 1-5,
  "base_probability": 0.04,
  "duration_months": 1,
  "effects": { "happiness": -5, "property_damage": 0.1, ... },
  "trigger_conditions": { "fire_risk_min": 0.3, ... },
  "cascade_events": ["homelessness"],
  "description": "..."
}
```

### `base/data/buildings/buildings.json` — **199 entries**

```json
{
  "id": "res_frontier_cabin",
  "name": "Frontier Cabin",
  "category": "residential|commercial|industrial|service",
  "era": 0,
  "zone": "residential",
  "density": 0,
  "sizeX": 1, "sizeY": 1,
  "heightStories": 1,
  "capacityResidents": 4,
  "jobs": 0,
  "constructionCost": 200,
  "monthlyMaintenance": 3,
  "constructionMonths": 1,
  "powerDemandKW": 0,
  "waterDemand": 1,
  "pollutionOutput": 0,
  "noiseOutput": 0,
  "fireRisk": 0.05,
  "material": "wood",
  "wealthMin": 0, "wealthMax": 1,
  "unlockTech": null | "T001",
  "happinessRadius": 0,
  "happinessBonus": 0,
  "landValueEffect": 0,
  "description": "..."
}
```

### `base/data/laws/laws.json` — **70 entries**

```json
{
  "id": "speed_limit",
  "name": "Speed Limits",
  "category": "traffic",
  "era_min": "industrial",
  "parameters": [{ "name": "max_speed_kmh", "min": 20, "max": 120, "default": 50, "step": 10 }],
  "effects": { "accident_rate": -0.3, ... },
  "faction_reactions": { "business": -0.1, ... },
  "cost_monthly": 500,
  "compliance_base": 0.7,
  "tech_prerequisite": null,
  "description": "..."
}
```

### `base/data/localization/en.json`

Not deeply audited; present for string keys.

**Data vs v1 scope:** JSON encodes full 5-era content; web v1 locks to one era arc — **no doc defines which tech/building/event subsets ship in v1**.

---

## 5. Top 10 doc fixes needed

1. **Add `WEB_V1_SCOPE.md`** — single locked charter: 256×256, 10k HH, 5k buildings, 1 era arc, no MP, monetization, performance targets. Supersedes conflicting tables elsewhere.

2. **Rewrite `MASTER_GAME_CONCEPT` executive summary** — remove Steam/pixel/100K HH from §1–2 or mark clearly “original vision”; point to web v1 charter. Keep sim/economy depth as-is.

3. **Mark superseded sections in bulk** — `TECH_STACK_RESEARCH`, `MASTER_DEVELOPMENT_PLAN`, `AGENT_01/08/09`, `CITY_BUILDER_GDD`: banner + “historical reference” like `VISUAL_QUALITY_GUIDE` already has.

4. **Promote `AI_ART_PIPELINE` §3D to top** — demote pixel workflow to appendix; align Meshy model version (meshy-5 vs meshy-6) across `MESHY_*` ADRs.

5. **Author `ERA_ARC_V1.md`** — Frontier→Industrial: era transition triggers, tech subset from `technologies.json`, building roster, event pool, hero landmarks for v1.

6. **Author `SAVE_FORMAT_WEB.md`** — unify `CMJR` binary header, cloud API payload, versioning/migration, 3-save vs 20-save tiers, conflict resolution.

7. **Author `WASM_SIM_BRIDGE.md`** — snapshot schema, tick cadence, worker protocol, procedural fallback contract (per `DEPLOY_WEB.md`).

8. **Resolve multiplayer stance** — either “post-v1” ADR striking §10 P2P from active path, or move `research/16` into `docs/design/` with explicit phase label.

9. **Fill placeholders** — `GAME_FEEL_BIBLE.md`, `AI_SIMULATION_RESEARCH.md` (or delete and link to `docs/research/02-llm-strategy.md`).

10. **Add `DATA_BRIDGE.md`** — map `buildings.json` string IDs → `TypeId` → `res_low_frontier_05` archetype keys → GLTF paths; note gaps (199 JSON buildings vs 500 TypeId variants).

---

## 6. Recommended doc hierarchy (post-fix)

```
CLAUDE.md                          ← agent/dev onboarding
docs/design/WEB_V1_SCOPE.md        ← NEW: locked product scope
docs/design/MASTER_GAME_CONCEPT.md ← sim/economy/narrative (web sections current)
docs/design/BUILDING_ARCHETYPE_3D.md
docs/design/MESHY_*.md
docs/design/SIMULATION_ARCHITECTURE.md ← scale footnotes for web v1
docs/design/* (agent/sim depth)    ← valid with supersession banners
docs/research/*                    ← linked from design index, phase-tagged
```

---

**Summary:** The July 2026 spike added strong 3D/R3F ADRs and updated `CLAUDE.md`, but ~25 of 33 design docs still describe Iron & Oak as a Godot/Steam pixel game at 10–100× web v1 scale. The highest-risk gaps are missing web-specific specs (era arc, save format, WASM bridge, UI) and contradictory platform/multiplayer/art claims in `MASTER_GAME_CONCEPT`’s opening sections.

[REDACTED]