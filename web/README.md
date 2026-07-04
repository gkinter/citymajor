# CityMajor Web — M0 R3F Spike

Phase 0 technical spike for Linear SB-3658…SB-3665.

## Stack

- Next.js 16 App Router
- React 19
- @react-three/fiber + @react-three/drei + three

## Run

From repo root (worktree):

```bash
pnpm install
pnpm dev
```

Open http://localhost:3000/play

## Features

| Item | Implementation |
|------|----------------|
| 256×256 procedural city | `lib/city-data.ts` — ~5000 buildings, 5 archetypes |
| InstancedMesh | `components/city/BuildingInstances.tsx` — 1 mesh per archetype |
| Chunk visibility | 64 chunks (32×32), frustum test in `lib/chunks.ts` |
| Terrain chunks | `components/city/TerrainChunks.tsx` — flat planes, culled |
| LOD L0–L3 | Distance hysteresis per chunk in `lib/chunks.ts` + `lib/lod.ts` |
| OrbitControls + picking | `CityScene.tsx` + `TilePicker.tsx` |
| Dynamic DPR | `AdaptiveDpr.tsx` — downscale when FPS &lt; 30 for 2s |
| COOP/COEP | `next.config.ts` headers for future SharedArrayBuffer / WASM |
| FPS HUD | `FpsHud.tsx` |

## Performance notes

See `PERF.md` after local benchmark. Architecture targets:

- **≥30 FPS** integrated GPU at full city fill (with DPR adapt)
- **≥60 FPS** discrete GPU at max fill

## Out of scope (M0)

WASM sim, backend, Stripe, Solana.
