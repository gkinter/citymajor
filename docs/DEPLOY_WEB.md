# CityMajor Web — Coolify Preview Deploy

Deploy the Next.js web client (`web/`) to Coolify for branch previews. Production domain routing is TBD; preview-only apps use `*.apps.softblaze.net`.

## Quick reference

| Setting | Value |
|---------|-------|
| Build pack | **Dockerfile** (repo root) |
| Branch | `feat/wasm-r3f-integration-2026-07-04` (or any feature branch) |
| Port exposes | `3000` |
| Health check | `GET /` or `GET /play` |
| Auto-deploy | Enable for preview branches |

## Coolify app setup

1. **New application** → GitHub repo `DigitalSoftDistribution/citymajor` (or fork).
2. **Build pack**: Dockerfile (not Nixpacks).
3. **Dockerfile location**: `/Dockerfile` (repo root).
4. **Git branch**: feature branch to preview (e.g. `feat/wasm-r3f-integration-2026-07-04`).
5. **Ports exposes**: `3000`.
6. **Preview deployments**: enable so pushes to the watched branch trigger a rebuild.
7. **Domain**: Coolify assigns `citymajor-<branch-slug>.apps.softblaze.net` (wildcard CF tunnel).

Push to the watched branch → Coolify builds the Docker image and deploys. Wait for the deploy to finish before pushing again (see deploy-discipline).

## Docker build stages

```
wasm (optional) → deps → builder → runner
```

| Stage | Purpose |
|-------|---------|
| `wasm` | .NET 8 SDK publishes `Forge.SimWasm` → `web/public/dotnet/` |
| `deps` | `pnpm install --frozen-lockfile` for root + `web/` + `sim-types` |
| `builder` | `pnpm --filter @citymajor/web... build` (Next.js standalone) |
| `runner` | Node 20 slim, runs `node web/server.js` |

### WASM bundle vs procedural fallback

The build stage attempts to compile the C# simulation to browser WASM. If WASM assets are missing or the build fails, `/play` still works using **procedural** city data (~5000 mock buildings).

| Build arg | Default | Effect |
|-----------|---------|--------|
| `BUILD_WASM` | `1` | Run `web/wasm/build-wasm.sh` in the `wasm` stage |
| `BUILD_WASM=0` | — | Skip .NET publish; deploy procedural-only (faster CI preview) |

When `BUILD_WASM=1` and `dotnet publish` fails (SDK/workload issues, OOM), the Dockerfile logs a warning and continues — the image ships without `/dotnet/_framework/` and the HUD shows **procedural**.

**Verify sim source on preview:**

- Open `/play` → FPS HUD shows `Data: WASM sim` or `Data: procedural`.
- WASM present: `/dotnet/_framework/blazor.boot.json` returns 200.

**Local parity:**

```bash
pnpm build:wasm && pnpm build && pnpm start   # WASM sim
pnpm build && pnpm start                      # procedural fallback
```

## Environment variables

The M0 spike runs with in-memory stubs — no secrets required for preview.

| Variable | Required | Default | Notes |
|----------|----------|---------|-------|
| `PORT` | No | `3000` | Set by Coolify / Dockerfile |
| `HOSTNAME` | No | `0.0.0.0` | Bind all interfaces in container |
| `NODE_ENV` | No | `production` | Set in Dockerfile runner stage |
| `BUILD_WASM` | No | `1` | Docker **build arg** (not runtime env) — pass via Coolify build args |
| `STRIPE_SECRET_KEY` | No | — | Enables live Founder Pass Checkout; omit for mock cookie flow |
| `STRIPE_FOUNDER_PASS_PRICE_ID` | No | — | Stripe Price ID for Founder Pass (`price_…`) |
| `STRIPE_WEBHOOK_SECRET` | No | — | Verifies `POST /api/webhooks/stripe` (register Coolify FQDN + `/api/webhooks/stripe`) |

Without Stripe vars, `/shop` uses the mock entitlements cookie path. See `web/.env.example`.

## COOP / COEP headers (SharedArrayBuffer)

The play page uses a Web Worker + optional WASM with `SharedArrayBuffer`. `web/next.config.ts` sets on all routes:

```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
```

**Coolify / Traefik must not strip these headers.** Next.js serves them from the Node standalone server; no extra Traefik middleware is needed unless you add a CDN layer in front.

If COOP/COEP are missing in production:

- WASM worker init may fail → client falls back to procedural (still playable).
- Check response headers on `/play` in browser DevTools → Network.

Embedded third-party assets (fonts, analytics) must send `Cross-Origin-Resource-Policy: cross-origin` or be same-origin, or COEP will block them.

## Resource hints

| Profile | CPU | RAM | Notes |
|---------|-----|-----|-------|
| Procedural-only (`BUILD_WASM=0`) | 1 | 1 GB | ~3–5 min build |
| Full WASM (`BUILD_WASM=1`) | 2 | 2–4 GB | .NET wasm publish is heavy; first build ~8–12 min |

## Manual smoke test

After deploy:

```bash
curl -sI "https://<preview-fqdn>/play" | grep -i cross-origin
curl -sf "https://<preview-fqdn>/" -o /dev/null && echo OK
```

Optional Playwright smoke (from dev machine against preview URL):

```bash
cd web && BASE_URL=https://<preview-fqdn> WASM_EXPECTED=0 node scripts/smoke-play.mjs
```

Set `WASM_EXPECTED=1` only when `BUILD_WASM=1` succeeded.

## Related docs

- [`web/README.md`](../web/README.md) — local dev, WASM build, procedural fallback
- [`web/PERF.md`](../web/PERF.md) — FPS / instancing notes
