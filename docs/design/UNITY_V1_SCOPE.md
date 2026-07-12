# CityMajor — Unity v1 Scope Charter (Locked)

**Status:** Locked  
**Date:** 2026-07-12  
**Linear:** [SB-3704](https://linear.app/softblaze/issue/SB-3704) follow-on  
**Supersedes:** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) for **platform and era decisions** — web client is maintenance-only  
**Prerequisite read:** [UNITY_PORT_MEGA_PLAN.md](./UNITY_PORT_MEGA_PLAN.md), [MASTER_GAME_CONCEPT.md](./MASTER_GAME_CONCEPT.md)

> **Canonical onboarding:** [`CLAUDE.md`](../../CLAUDE.md) — stack, directories, architecture notes.  
> **Sim depth (unchanged):** [`MASTER_GAME_CONCEPT.md`](MASTER_GAME_CONCEPT.md) — economy, politics, LLM narrative design.

---

## 1. Product intent

CityMajor Unity v1 is a **Steam desktop** mesh-3D city builder (Cities: Skylines lite) with **deep simulation** at a **deliberately reduced scale**. **Modern era only** — glass towers, contemporary services, and industrial/commercial archetypes — proves the native sim + URP renderer + Steam loop before expanding eras or platforms.

**In scope:** Single-player mayor experience, zoning, services, economy slider, modern-era research subset, LLM-flavored narrative (quota-gated), cloud saves, Steam distribution + Founder Pass equivalent (TBD).

**Out of scope for v1:** See §6 (explicit cuts).

---

## 2. Locked parameters

| Parameter | v1 value | Notes |
|-----------|----------|-------|
| **Platform** | **Unity 6 + URP**, Steam desktop | macOS, Windows, Linux |
| **Map** | **256×256** tiles (65,536) | 64 chunks @ 32×32; same scale as web v1 for now |
| **Buildings** | **~5,000** max instanced | 40–60 modern archetypes; not unique meshes per building |
| **Households** | **~10,000** | Full cultural DNA / satisfaction at reduced population |
| **Era** | **Modern only** | `svc_modern_*`, `com_modern_*`, `ind_modern_*`, `res_*_modern_*` archetypes |
| **Multiplayer** | **None** | No co-op, no P2P, no shared cities |
| **Renderer** | URP + GPU instancing | Chunked LOD mandatory |
| **Simulation** | **Forge.SimCore** native in-process | Dedicated sim thread; not WASM |
| **Tech tree (playable)** | Subset gated to **modern** era tag | Full JSON has 156 techs across 5 eras — v1 unlocks modern band only |

---

## 3. Era: Modern only

v1 ships a **single-era** experience — contemporary skyline, glass-and-steel towers, modern services and zoning. No era progression arc in v1.

| Band | Gameplay focus | Visual band (`TypeId` suffix) |
|------|----------------|-------------------------------|
| **Modern** | Dense zoning, services at scale, contemporary economy, glass towers | `x60–x79` — glass, steel, flat roofs, 5–40 stories |

**Content pools (v1):** Buildings, events, and laws filtered to `era` / `era_min` = modern. Hero landmarks: modern band only ([`MESHY_HERO_LANDMARKS.md`](MESHY_HERO_LANDMARKS.md)).

**Asset paths:** `web/public/assets/gltf/modern/` — symlinked or copied into `unity/CityMajor.Unity/Assets/Art/Gltf/modern/`.

---

## 4. Performance targets

| Metric | Target | Enforcement |
|--------|--------|-------------|
| **FPS (integrated GPU)** | **≥30** at max city fill with LOD active | Quality tier auto-downgrade; box LOD at distance |
| **FPS (discrete GPU)** | **≥60** with LOD active | Default quality tier |
| **Draw calls** | 20–40 per visible chunk | GPU instancing + 32×32 chunk frustum cull |
| **Sim tick** | Background thread; render reads snapshot only | No per-building GameObject churn |
| **LOD bands** | L0 full GLTF → L1 simplified → L2 boxes → L3 heatmap blocks | Mandatory at street/neighborhood/city zoom |
| **Steam Deck** | **≥30 FPS** minimum | Phase 3 verification gate |

**Non-goals:** 4K ultra, uncapped entity counts, or 1024×1024 / 50k building stress tests in v1.

---

## 5. Monetization (v1)

| Tier | Price | Includes |
|------|-------|----------|
| **Base game** | TBD (Steam) | Full sim depth for modern era; cloud saves; **10 LLM narrative events/day** |
| **Founder Pass equivalent** | **TBD** | Unlimited LLM events; expanded save slots; cosmetic bundle; **no pay-to-win** |
| **Cosmetic DLC** | Variable (Steam) | Facade skins, modern ornaments — **sim-neutral** only |
| **Solana perks** | Phase 2 | Not in v1 launch scope |

**Principles:** Simulation systems, zoning, research, and laws are never paywalled. Templates always available when LLM quota exhausted or API down.

---

## 6. Explicit cuts (out of scope v1)

| Cut | Rationale |
|-----|-----------|
| **Frontier, Industrial, Postwar, Future eras** | Deferred — modern-only v1 proves desktop loop |
| **Browser web client (active development)** | **Maintenance mode only** — no new features; critical fixes only |
| **WASM sim path (new features)** | Frozen — Unity uses native `Forge.SimCore` |
| **Multiplayer / co-op** | Post-v1 |
| **1024×1024 map / 50k buildings** | Post-v1 scale gate |
| **Steam Workshop** | Phase 3+ consideration |
| **Mobile / console** | Not v1 |

---

## 7. Architecture summary

```
┌─────────────────────────────────────────────────────────────┐
│ Forge.SimCore (pure C# class library)                       │
│  Economy, traffic, zones, research, events, laws, trade     │
└───────────────────────────┬─────────────────────────────────┘
                            │ in-process (native)
                 ┌──────────▼──────────┐
                 │ Unity 6 client      │
                 │ URP + UI Toolkit    │
                 │ Steam desktop       │
                 └─────────────────────┘

web/ (maintenance mode) — Forge.SimWasm + R3F, no new v1 features
```

---

## 8. Related docs

| Doc | Role |
|-----|------|
| [CITY_ECOSYSTEM_VISION.md](./CITY_ECOSYSTEM_VISION.md) | **Lively SimCity feel** — people, traffic, social/blueprint north star |
| [UNITY_PORT_MEGA_PLAN.md](./UNITY_PORT_MEGA_PLAN.md) | Phased port plan, risk register |
| [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) | **Superseded** for platform — historical web charter |
| [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md) | Web snapshot protocol (maintenance reference) |
| [UNITY_MCP_SETUP.md](../UNITY_MCP_SETUP.md) | Cursor ↔ Unity MCP workflow |
| [unity/README.md](../../unity/README.md) | Unity project quick start |
