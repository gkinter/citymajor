# CityMajor — Web v1 Scope Charter (Locked)

**Status:** Locked  
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
docs/design/WEB_V1_SCOPE.md        ← THIS FILE: locked product scope
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
| `DATA_BRIDGE.md` — `buildings.json` ↔ TypeId ↔ GLTF | Not yet authored |

---

## 10. Acceptance checklist (v1 ship)

- [ ] 256×256 map playable start-to-Industrial transition
- [ ] Stable ≥30 FPS on integrated GPU with 5k buildings / 10k HH
- [ ] WASM sim ticks in worker; R3F renders from snapshot
- [ ] Founder Pass + cosmetic Stripe flow; free tier quotas enforced
- [ ] Cloud save (3 free / 20 Founder)
- [ ] No multiplayer code paths in production build
- [ ] LLM proxy with template fallback; 10/day free cap

---

*Last updated: 2026-07-04. Changes to locked parameters require explicit product sign-off and an update to this file.*
