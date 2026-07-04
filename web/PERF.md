# M0 R3F Spike — Performance Notes

## Architecture summary

| Layer | Draw calls (typical) | Notes |
|-------|---------------------|-------|
| Terrain | ≤64 planes (chunk culled) | Usually 12–24 visible at city zoom |
| Buildings | **5** InstancedMesh | One per archetype (~5000 instances total) |
| Grid | 1 `gridHelper` | Debug overlay |
| Lights | 3 | ambient + directional + hemisphere |

**CPU hot path:** per-frame matrix + color updates for visible instances only (scale=0 for culled chunks). Chunk frustum test + LOD hysteresis run at 2 Hz (stats interval), not every frame — acceptable for spike; move to `useFrame` throttling if needed.

## Procedural city stats (seed `0x63697479`)

- Grid: 256×256 tiles
- Target: 5000 buildings across 5 archetypes (res-low, res-high, commercial, industrial, office)
- Chunks: 64 × 32×32

## FPS targets vs spike status

| GPU class | Target | Spike expectation |
|-----------|--------|-------------------|
| Discrete (M-series / RTX) | ≥60 FPS max fill | **Expected 60+** at DPR 1.5–2 with ~5 instanced draw calls |
| Integrated | ≥30 FPS max fill | **Expected 30–55**; `AdaptiveDpr` steps down after 2s below 30 FPS |

### Local benchmark (fill in after `/play` smoke test)

```
Machine:
Browser:
DPR (initial):
FPS (orbit, full city in view):
FPS (zoomed district, ~8 chunks):
DPR after degrade (if triggered):
```

**Headless CI cannot measure WebGL FPS** — validate interactively at http://localhost:3000/play.

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
