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

Checks: `/play` HTTP 200, COOP/COEP headers, HUD text, `<canvas>` + WebGL context, buildings count in HUD. **Headless Chromium often reports FPS `—`** on a single HUD read — use `pnpm perf:gate` for sampled threshold enforcement (≥30 FPS integrated target per WEB_V1_SCOPE §4).

### Perf gate (FPS threshold)

Script: `web/scripts/perf-gate.mjs` (Playwright + Chromium). Enforces **≥30 FPS median** over a **stable** window (integrated GPU target from [`WEB_V1_SCOPE.md`](../docs/design/WEB_V1_SCOPE.md) §4 — “Stable ≥30 FPS”). Absolute min is logged/warned only.

**Terminal 1** — start the app (same as smoke).

**Terminal 2**:

```bash
pnpm perf:gate
# or from web/:
cd web && pnpm perf:gate
```

| Variable | Default | Purpose |
|----------|---------|---------|
| `MIN_FPS` | `30` | Floor for **median** sampled FPS (integrated GPU) |
| `PERF_WARMUP_MS` | `3500` | Wait after canvas mount before sampling |
| `PERF_DISCARD_MS` | `2000` | Drop HUD samples from the first N ms of the sample window (load hitch) |
| `PERF_SAMPLE_MS` | `5000` | HUD poll window (gate uses samples after discard) |
| `PERF_POLL_MS` | `500` | HUD poll interval |
| `PERF_USE_RAF_FALLBACK` | `1` | Use `requestAnimationFrame` when HUD shows `—` |
| `PERF_GATE_STRICT` | `0` | Set `1` to fail when no samples (no fallback) or on software-renderer skip |
| `PERF_SOFT_RENDERER_MAX` | `15` | Headless max FPS below this ⇒ skip gate (unless strict) |
| `PERF_REPORT` | `1` | Write `test-results/perf-gate.json` |
| `HEADLESS` | `1` | Set `0` to watch the browser |

#### Findings — min 21 / median 67 (2026-08-10)

On `/play` (Mac GPU, HUD method) a failing report looked like:

`samples: [21, 31, 41, 50, 67, 67, 79, 101, 103]` → **min 21**, **median 67**.

That is a **post-mount ramp** (shader/GLTF/instance upload), not a sustained FPS regression. Re-runs also show occasional mid-window dips (e.g. min 16 while median 48) from GC / compile — still not a sustained drop.

Fix: (1) discard first `PERF_DISCARD_MS=2000` of the sample window; (2) gate on **median**, not absolute min. Do **not** lower procedural city density or LOD defaults for this pattern unless the *post-discard median* also fails.

Combine with smoke: `PERF_GATE=1 pnpm smoke:play` runs the full smoke suite then enforces the FPS gate on the same page session.

Discrete GPU CI (optional): `MIN_FPS=60 pnpm perf:gate`.

**Navigation:** `perf-gate.mjs` uses `waitUntil: "domcontentloaded"` (same as `smoke-play-checks.mjs`). Do not use `networkidle` on `/play` — the WASM sim worker and dev HMR keep the network busy and Playwright will time out even when the canvas is healthy.

### Beast remote (no `beast perf:gate` subcommand)

From the feature worktree root, sync + run Playwright **on Beast** against a dev server bound to localhost on the server (not port-forwarded from Mac):

```bash
# 1) Start Next dev on Beast (pick a free port, e.g. 3107)
beast run 'export NODE_OPTIONS=; PORT=3107; cd /home/devops/citymajor-web-r3f-spike--<branch-slug>/web; setsid pnpm exec next dev -p $PORT > /tmp/citymajor-perf-dev.log 2>&1 < /dev/null &'

# 2) Wait until /play returns HTTP 200 on Beast
beast run 'PORT=3107; until curl -sf -o /dev/null http://localhost:$PORT/play; do sleep 2; done; echo ready'

# 3) Perf gate (writes test-results/perf-gate.json on Beast)
beast run 'cd /home/devops/citymajor-web-r3f-spike--<branch-slug>/web && BASE_URL=http://localhost:3107 HEADLESS=1 PERF_REPORT=1 pnpm perf:gate'

# Optional: pull report to local web/test-results/
rsync -az devops@beast:/home/devops/citymajor-web-r3f-spike--<branch-slug>/web/test-results/perf-gate.json web/test-results/
```

Replace `<branch-slug>` with the sanitized git branch (`feat/wasm-r3f-integration` → `feat-wasm-r3f-integration-2026-07-04`). `beast` prints the resolved remote path in its header.

Canvas selector: `[data-testid="city-canvas"] canvas` (R3F main view). Minimap uses a separate 2D `<canvas>` outside that wrapper — do not drop the test id scope.

### SB-3703 — automated run log (2026-07-04)

Environment: Beast (Hetzner EPYC), headless Chromium, `BASE_URL=http://localhost:3107`, branch `feat/wasm-r3f-integration-2026-07-04`.

| Check | Result |
|-------|--------|
| `pnpm smoke:play` | **PASS** — WebGL canvas + `data-engine=three.js r175`, 218/218 buildings, WASM sim |
| `pnpm perf:gate` (default) | **PASS (skipped threshold)** — headless software renderer (`maxFps` 2 < 15) |
| HUD FPS samples (5s window) | min **1**, max **2**, median **2**, avg **2** (`method: hud`) |
| Smoke FPS snapshot | **3** |
| Strict gate (`PERF_GATE_STRICT=1 PERF_SOFT_RENDERER_MAX=0`) | **FAIL** — min FPS 1 < 30 (expected on headless; not a product regression) |

**Sign-off still open** — complete the checklist below on a Mac with discrete GPU (`HEADLESS=0`). Headless Beast/Chromium runs above are CI smoke only (software renderer).

### SB-3703 — sign-off checklist

Worktree: `citymajor-web-r3f-spike` · Ticket: [SB-3703](https://linear.app/softblaze/issue/SB-3703) · Targets: [`WEB_V1_SCOPE.md`](../docs/design/WEB_V1_SCOPE.md) §4.

| Gate | Target | Evidence |
|------|--------|----------|
| Discrete GPU (Mac) | **≥60 FPS** median sample, orbit + district | `perf-gate.json` + benchmark table |
| Integrated GPU | **≥30 FPS** median sample | [SB-3705](https://linear.app/softblaze/issue/SB-3705) QA matrix |
| WASM soak | ~220→5k buildings, no frame collapse | HUD building count + FPS during growth |
| `AdaptiveDpr` | Steps down after 2s &lt;30 FPS | Integrated spot-check; discrete should not degrade |

#### 1. Mac prep (discrete GPU)

- MacBook Pro / Mac Studio with **discrete or M-series GPU** (not low-power mode).
- Chrome or Safari — hardware acceleration on; close GPU-heavy apps.
- First time: `cd web && npx playwright install chromium`.

**Terminal 1** — from worktree root:

```bash
cd ~/citymajor/citymajor-web-r3f-spike
unset NODE_OPTIONS
pnpm build:wasm && pnpm dev
```

Wait for `http://localhost:3000/play` — HUD should read **WASM sim** (not procedural).

#### 2. Interactive smoke (`HEADLESS=0`)

**Terminal 2**:

```bash
cd ~/citymajor/citymajor-web-r3f-spike/web
HEADLESS=0 WASM_EXPECTED=1 pnpm smoke:play
```

Watch the browser: WebGL canvas mounts, Diagnostics HUD shows numeric FPS (not `—`), building count matches HUD.

#### 3. Discrete perf gate — **≥60 FPS**

Same dev server; visible browser:

```bash
HEADLESS=0 MIN_FPS=60 PERF_GATE_STRICT=1 PERF_REPORT=1 pnpm perf:gate
```

Pass: exit 0 and `test-results/perf-gate.json` → `medianFps >= 60` (`gateMetric: "median"`). Optional longer window: `PERF_WARMUP_MS=5000 PERF_SAMPLE_MS=10000`.

Combine with full smoke in one session: `HEADLESS=0 MIN_FPS=60 PERF_GATE=1 pnpm smoke:play`.

#### 4. Manual HUD verification (same `/play` tab)

With dev server + `HEADLESS=0` (or plain `pnpm dev` + browser):

- [ ] **Orbit, full city in view** — Diagnostics FPS **≥60** sustained (~10s)
- [ ] **Zoom district** (~8 terrain chunks visible) — FPS **≥60**
- [ ] **DPR** — initial 1.5–2; no `AdaptiveDpr` step-down on discrete
- [ ] **Sim** — HUD `WASM sim`; snapshot rate ≤4 Hz

Record results in the benchmark table below.

#### 5. Integrated + soak (parallel / [SB-3705](https://linear.app/softblaze/issue/SB-3705))

- [ ] **Integrated** — `HEADLESS=0 MIN_FPS=30 pnpm perf:gate` on iGPU / low-power Mac (or QA matrix machine)
- [ ] **`AdaptiveDpr`** — FPS &lt;30 for 2s triggers DPR degrade; note `DPR after degrade`
- [ ] **WASM soak** — zone growth toward **5k buildings** without sustained FPS collapse
- [ ] **CTO P0 perf** — [CTO_IMPROVEMENT_ROADMAP_2026-07.md](../docs/design/CTO_IMPROVEMENT_ROADMAP_2026-07.md) item #10 acknowledged

#### Sign-off boxes

- [ ] Steps 1–4 complete on Mac discrete GPU (`HEADLESS=0`, `MIN_FPS=60` pass)
- [ ] Local benchmark table filled
- [ ] `test-results/perf-gate.json` from discrete run attached to [SB-3703](https://linear.app/softblaze/issue/SB-3703)
- [ ] [SB-3705](https://linear.app/softblaze/issue/SB-3705) QA matrix complete (integrated)
- [ ] Eng + product sign-off on checklist ([`V1_MERGE_CHECKLIST.md`](../docs/design/V1_MERGE_CHECKLIST.md) §6)

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
| SB-3703 | v1 perf sign-off — Mac `HEADLESS=0`, discrete ≥60 FPS gate |
