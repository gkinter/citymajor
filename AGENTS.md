# CityMajor — Agent Guide (Web v1)

Browser-only mesh-3D city builder. **Locked v1 scope:** [`docs/design/WEB_V1_SCOPE.md`](docs/design/WEB_V1_SCOPE.md).

## Worktree rules (mandatory)

Never edit tracked files on `main`, `master`, `develop`, `production`, `prod`, or `staging`. The canonical clone at `~/citymajor/citymajor` stays on `main`.

**Active web v1 worktree:** `~/citymajor/citymajor-web-r3f-spike` → branch `feat/wasm-r3f-integration-2026-07-04`.

```bash
# Create a new isolated worktree for your task
TOPIC=<short-kebab-slug>
SLUG="feat/${TOPIC}-$(date +%Y-%m-%d)"
git worktree add "../citymajor-${TOPIC}" -b "$SLUG"
cd "../citymajor-${TOPIC}"
```

- One task = one worktree. Never share a working directory across parallel agents.
- Verify before commit: `git rev-parse --abbrev-ref HEAD` must not be a protected branch.
- After merge: `git worktree remove "../citymajor-${TOPIC}"` && `git branch -d "$SLUG"`.

## Build & verify

| Command | Purpose |
|---------|---------|
| `pnpm dev` | Next.js dev server (`@citymajor/web`, port 3000) |
| `pnpm build:wasm` | Publish C# `Forge.SimWasm` → `web/public/dotnet/` |
| `pnpm dev:wasm` | `build:wasm` then `dev` (use after sim changes) |
| `pnpm build` | Production Next.js build |
| `pnpm smoke:all` | HTTP + copy checks on `/`, `/shop`, `/play` + WebGL (needs `pnpm dev` running) |
| `pnpm smoke:play` | `/play` only smoke |
| `pnpm perf:gate` | WebGL FPS threshold gate on `/play` (≥30 FPS integrated; needs `pnpm dev`) |

**WASM:** `pnpm build:wasm` runs `web/wasm/build-wasm.sh` — `dotnet publish` on `src/Forge.SimWasm`, copies `AppBundle` to `web/public/dotnet/`. Re-run after any C# sim change. Without WASM, `/play` falls back to procedural city data (HUD shows `Data: procedural`).

**Smoke:** Prereq is a running dev server (`pnpm dev` or `pnpm dev:wasm`). First run: `npx playwright install chromium`. Optional: `SCREENSHOT=1 pnpm smoke:all` saves `test-results/smoke-play.png`. Smoke records a single HUD FPS snapshot (warn-only if `—`); **`pnpm perf:gate`** samples FPS over 5s and fails below **30** (WEB_V1_SCOPE §4). Optional: `PERF_GATE=1 pnpm smoke:play` runs smoke + perf gate in one session.

## Key paths

```
web/                          → Next.js 16 app (@citymajor/web)
web/app/play/                 → /play — R3F city canvas entry
web/app/shop/                 → Stripe cosmetic shop
web/app/api/                  → saves, entitlements, narrative, checkout, webhooks
web/components/city/          → R3F scene (CityCanvas, BuildingInstances, TerrainChunks, HUD)
web/lib/                      → sim-bridge, zoning, LOD, saves, entitlements, catalogs
web/workers/                  → WASM sim worker (SharedArrayBuffer ticks)
web/wasm/                     → build-wasm.sh, publish pipeline
web/public/dotnet/            → WASM bundle output (_framework/blazor.boot.json)
web/public/assets/gltf/       → Era archetype GLTF kits
web/packages/sim-types/       → Shared TS types for sim snapshots
web/scripts/smoke-all.mjs     → Full smoke suite
web/scripts/perf-gate.mjs   → WebGL FPS gate (≥30 integrated, WEB_V1_SCOPE §4)
src/Forge.SimWasm/            → C# sim → browser WASM host
src/Forge.SimCore/            → Core simulation logic
src/Forge.Game/               → Game rules, economy, zoning
docs/design/WEB_V1_SCOPE.md   → Locked v1 charter (256×256, 10k HH, Frontier→Industrial)
docs/design/WASM_SIM_BRIDGE.md → WASM ↔ R3F data contract
docs/DEPLOY_WEB.md            → Coolify preview deploy (Dockerfile, BUILD_WASM arg)
```

## Architecture reminders

- Sim snapshots → `InstancedMesh` matrices in `useFrame` — not React state per building.
- LOD: full GLTF → simplified mesh → instanced boxes → heatmap blocks.
- Play URL: `/play`. Preview deploys: `*.apps.softblaze.net` (see `docs/DEPLOY_WEB.md`).

## Related docs

- [`CLAUDE.md`](CLAUDE.md) — stack summary, monetization, architecture notes
- [`docs/design/WEB_V1_SCOPE.md`](docs/design/WEB_V1_SCOPE.md) — **canonical locked scope**; supersedes conflicting rows in older design docs
- [`docs/design/MASTER_GAME_CONCEPT.md`](docs/design/MASTER_GAME_CONCEPT.md) — sim depth, economy, narrative design
- [`web/README.md`](web/README.md) — local dev setup, WASM parity notes
