# CityMajor

Deep city-builder simulation — web-only mesh 3D (Cities: Skylines lite). Linear: [SB-3704](https://linear.app/softblaze/issue/SB-3704).

## Stack

| Layer | Technology |
|-------|------------|
| **Shell / UI** | Next.js 16 + React 19 + TypeScript (App Router), Tailwind + shadcn/ui |
| **3D renderer** | React Three Fiber + Three.js (`@react-three/drei`, `@react-three/postprocessing`) |
| **Simulation** | C# → .NET 8 WASM (reuse ~9.4k LOC); Web Workers + SharedArrayBuffer for ticks |
| **Backend** | API (saves, entitlements), LLM proxy with template fallback, Stripe; Solana phase 2 |
| **Assets** | Modular GLTF kits per era, CDN-hosted; `InstancedMesh` per archetype |

**Platform:** Browser only. Forge Engine (native OpenGL) is reference for sim wiring — not shipped.

## Web v1 scope (locked)

| Parameter | Value |
|-----------|-------|
| Map | **256×256** tiles (64 chunks @ 32×32) |
| Buildings | ~**5,000** max instanced (40–60 archetypes/era, not unique meshes) |
| Households | ~**10,000** |
| Eras | 1 arc (e.g. Frontier → Industrial) |
| FPS target | ≥30 integrated GPU, ≥60 discrete GPU with LOD active |

## Monetization

**Free core** + **Founder Pass ($24.99)** + Stripe cosmetic shop (sim-neutral facade skins). Solana holder perks phase 2. No pay-to-win.

## Key directories

```
app/              → Next.js pages, HUD overlays (HTML over canvas)
components/       → React + R3F scene components
lib/              → WASM bridge, sim snapshots, utilities
docs/design/      → Game design (MASTER_GAME_CONCEPT, VISUAL_QUALITY_GUIDE, AI_ART_PIPELINE)
src/Forge.*       → Legacy C# sim + Forge renderer (sim → WASM; renderer obsolete)
.claude/          → Claude Code configuration (kit-managed)
```

## Architecture notes

- Sim snapshots → `InstancedMesh` matrices in `useFrame` — **not** React state per building.
- LOD tiers: full GLTF (street) → simplified mesh → instanced boxes → heatmap blocks.
- LLM: backend API at launch (free 10 events/day; Founder unlimited); templates always available.
