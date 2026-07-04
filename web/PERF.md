# M0 R3F Spike — Performance Notes

## Architecture summary

| Layer | Draw calls (typical) | Notes |
|-------|---------------------|-------|
| Terrain | ≤64 planes (chunk culled) | Usually 12–24 visible at city zoom |
| Buildings | **5** InstancedMesh | One per archetype (~5000 instances total) |
| Grid | 1 `gridHelper` | Debug overlay |
| Lights | 3 | ambient + directional + hemisphere |

**CPU hot path:** per-frame matrix + color updates for visible instances only (scale=0 for culled chunks). Chunk frustum test + LOD hysteresis run at 2 Hz (stats interval), not every frame — acceptable for spike; move to `useFrame` throttling if needed.

## WASM sim stats (256×256, SB-3683)

- Grid: 256×256 tiles (same as procedural)
- Starter: ~220 buildings, cross + ring roads, zoned core
- Sim worker: 8 Hz tick, ≤4 Hz snapshot to main thread
- Traffic: stubbed (`WasmTrafficStub`) — full BPR only in desktop build

## Procedural city stats (seed `0x63697479`)

- Grid: 256×256 tiles
- Target: 5000 buildings across 5 archetypes (res-low, res-high, commercial, industrial, office)
- Chunks: 64 × 32×32

## FPS targets vs spike status

| GPU class | Target | Spike expectation |
|-----------|--------|-------------------|
| Discrete (M-series / RTX) | ≥60 FPS max fill | **Expected 60+** at DPR 1.5–2 with ~5 instanced draw calls |
| Integrated | ≥30 FPS max fill | **Expected 30–55**; `AdaptiveDpr` steps down after 2s below 30 FPS |

### Dev flow (WASM + R3F)

From repo root or `web/`:

```bash
unset NODE_OPTIONS          # required on Beast if emcc rejects --max-semi-space-size
pnpm build:wasm             # publishes Forge.SimWasm → web/public/dotnet/
pnpm dev                    # Next.js on http://localhost:3000
```

Shortcut: `pnpm dev:wasm` (build + dev in one command). Re-run `pnpm build:wasm` after any C# sim change.

Without WASM assets, `/play` falls back to **procedural** city data (~5000 mock buildings). With `web/public/dotnet/` present, the HUD shows **WASM sim** (~220 starter buildings, zone growth over time).

### Automated headless smoke test

Script: `web/scripts/smoke-play.mjs` (Playwright + Chromium).

**Terminal 1** — start the app:

```bash
pnpm build:wasm && pnpm dev
```

**Terminal 2** — run smoke (first time: `npx playwright install chromium`):

```bash
cd web && pnpm smoke:play
# or from repo root:
pnpm --filter @citymajor/web smoke:play
```

Environment:

| Variable | Default | Purpose |
|----------|---------|---------|
| `BASE_URL` | `http://localhost:3000` | Dev server URL |
| `TIMEOUT_MS` | `60000` | Page/load timeout |
| `HEADLESS` | `1` | Set `0` to watch the browser |
| `WASM_EXPECTED` | unset | Set `1` to fail if HUD is not `WASM sim` |

Checks: `/play` HTTP 200, COOP/COEP headers, HUD text, `<canvas>` + WebGL context, buildings count in HUD. **Headless Chromium often reports FPS `—`** — the script warns but does not fail; use the interactive benchmark below for FPS targets.

### Local benchmark (fill in after interactive `/play` session)

```
Machine:
Browser:
DPR (initial):
FPS (orbit, full city in view):
FPS (zoomed district, ~8 chunks):
DPR after degrade (if triggered):
Sim source (HUD): WASM sim | procedural
```

## COOP/COEP

`next.config.ts` sets `Cross-Origin-Opener-Policy: same-origin` and `Cross-Origin-Embedder-Policy: require-corp` on all routes. Confirms SharedArrayBuffer path for future WASM sim (not used in M0).

Verify in DevTools → Network → document headers.

## Known limitations / blockers

1. **Per-frame matrix updates** for all ~5000 instances — fine at M0 scale; batch by chunk or use GPU culling before 50k+ instances.
2. **LOD is per-chunk**, not per-building — acceptable for spike; finer LOD needs spatial bins inside chunk.
3. **No shadow map tuning** — shadows enabled but no cascades; disable for integrated GPU if needed.
4. **sharp build script** ignored by pnpm — production `next/image` unused; no blocker for `/play`.
5. **Repo has no initial main commit** — worktree is orphan branch `feat/web-r3f-spike-2026-07-04`; merge strategy with Forge C# tree TBD.

## Linear mapping

| Ticket | Deliverable |
|--------|-------------|
| SB-3658 | Next scaffold + `/play` full-viewport Canvas |
| SB-3659 | COOP/COEP headers |
| SB-3660 | Procedural 256×256 city data |
| SB-3663 | InstancedMesh ×5 + chunk visibility |
| SB-3664 | Terrain chunks + LOD L0–L3 hysteresis |
| SB-3665 | OrbitControls, picking, adaptive DPR, FPS HUD |
