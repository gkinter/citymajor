# CityMajor Web — M0 R3F Spike

Phase 0 technical spike for Linear SB-3658…SB-3665, WASM sim integration SB-3666.

## Stack

- Next.js 16 App Router
- React 19
- @react-three/fiber + @react-three/drei + three
- Forge.SimWasm (C# → browser WASM via .NET 8)

## Quick start (procedural fallback only)

No .NET SDK required — the client falls back to procedural city data when WASM is missing.

```bash
pnpm install
pnpm dev
```

Open http://localhost:3000/play

## Full stack (WASM sim + R3F)

### Prerequisites

- .NET 8 SDK (or .NET 10 + `wasm-tools-net8` workload)
- Node.js 20+
- pnpm 10+

### Build WASM → Next static assets

From repo root:

```bash
unset NODE_OPTIONS   # required on Beast if emcc rejects --max-semi-space-size
pnpm build:wasm      # publishes to web/public/dotnet/
```

Or from `web/`:

```bash
pnpm build:wasm
```

This runs `web/wasm/build-wasm.sh`, which:

1. `dotnet workload restore` + `dotnet publish` `Forge.SimWasm` for `browser-wasm`
2. Copies `AppBundle/*` → `web/public/dotnet/` (served at `/dotnet/_framework/…`)

### Run dev with WASM

```bash
pnpm build:wasm   # once per sim code change
pnpm dev
```

Shortcut: `pnpm dev:wasm` (build + dev).

The play page loads a Web Worker (`web/workers/sim-worker.ts`) that bootstraps the dotnet bundle, calls `Init(256)`, then ticks each frame. Snapshots feed `BuildingInstances` via `cityDataFromSnapshot()`.

If `/dotnet/_framework` is absent, the HUD shows **procedural** and ~5000 mock buildings render instead.

### Production build

```bash
pnpm build        # WASM optional — worker loads at runtime
pnpm start
```

Deploy `web/public/dotnet/` alongside the Next build when WASM sim is required in prod.

## Features

| Item | Implementation |
|------|----------------|
| WASM sim bridge | `lib/sim-bridge.ts` + `workers/sim-worker.ts` |
| Procedural fallback | `lib/city-data.ts` — used when WASM load fails |
| Snapshot → instancing | `cityDataFromSnapshot()` maps `SimSnapshot` → `CityData` |
| InstancedMesh | `components/city/BuildingInstances.tsx` — 1 mesh per archetype |
| Chunk visibility | 64 chunks (32×32), frustum test in `lib/chunks.ts` |
| LOD L0–L3 | Distance hysteresis per chunk in `lib/chunks.ts` + `lib/lod.ts` |
| COOP/COEP | `next.config.ts` headers for SharedArrayBuffer / WASM |
| FPS HUD | `FpsHud.tsx` — shows `WASM sim` vs `procedural` |

## WASM export API

| Export | Description |
|--------|-------------|
| `Init(worldSize)` | Creates world (clamped to power-of-two 32–256), seeds map + starter city |
| `Tick(dtSeconds)` | Advances simulation |
| `GetRenderSnapshot()` | JSON matching `SimSnapshot` in `lib/sim-bridge.ts` |

See `web/wasm/README.md` for standalone Vite demo and system inclusion/stub notes.

## Performance notes

See `PERF.md` after local benchmark.

## Project layout

```
src/Forge.SimCore/     # Engine-agnostic sim (linked from Forge.Engine/Game)
src/Forge.SimWasm/     # browser-wasm entry + JSExport API
web/public/dotnet/     # published WASM bundle (gitignored, build output)
web/workers/           # sim worker for Next.js
web/wasm/              # Vite demo + build-wasm.sh
```
