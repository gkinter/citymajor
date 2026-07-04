# CityMajor Web — Coolify Preview Deploy

Deploy the Next.js web client (`web/`) to Coolify for branch previews. Production domain routing is TBD; preview-only apps use `*.apps.softblaze.net`.

## Quick reference

| Setting | Value |
|---------|-------|
| Repository | `DigitalSoftDistribution/citymajor` |
| Branch | `feat/wasm-r3f-integration-2026-07-04` |
| Build pack | **Dockerfile** (repo root) |
| Dockerfile | `/Dockerfile` |
| Build arg | `BUILD_WASM=1` (build-time only) |
| Port exposes | `3000` |
| Health check | `GET /play` (or `GET /`) |
| Preview URL | `https://citymajor-feat-wasm-r3f-integration-2026-07-04.apps.softblaze.net` (typical slug) |
| Auto-deploy | Enable for the watched branch |

## Coolify app creation (step-by-step)

Use this checklist when creating the **first** preview app for the WASM + R3F integration branch. CityMajor is **preview-only** (no live `citymajor.com` prod path yet) — pushes to the watched branch trigger Coolify builds.

### Prerequisites

1. Access to Coolify at [https://coolify.softblaze.net](https://coolify.softblaze.net).
2. GitHub App **Softblaze Coolify** installed on `DigitalSoftDistribution/citymajor` (read + deploy hooks).
3. Branch `feat/wasm-r3f-integration-2026-07-04` pushed to GitHub with root `Dockerfile` present.
4. Server destination: default preview server (AX41, `*.apps.softblaze.net` wildcard via Cloudflare tunnel).

### 1. Create the application

1. **Projects** → open the CityMajor project (or create one, e.g. `citymajor`).
2. **+ New** → **Application** → **Public Repository** or **Private Repository (GitHub App)**.
3. Select repository **`DigitalSoftDistribution/citymajor`**.
4. **Name**: `citymajor-wasm-r3f-integration` (human label; FQDN slug derives from branch).
5. **Environment**: `production` (Coolify env name — still a preview URL, not live prod).

### 2. Source & branch

| Field | Value |
|-------|-------|
| Git branch | `feat/wasm-r3f-integration-2026-07-04` |
| Base directory | `/` (repo root) |
| Watch paths | leave empty (full-repo Docker build) |

Enable **Automatic Deployment** so every push to this branch queues one build. Do not rapid-fire pushes — wait for the prior deploy to finish (see deploy-discipline).

### 3. Build configuration

| Field | Value |
|-------|-------|
| Build pack | **Dockerfile** (not Nixpacks) |
| Dockerfile location | `/Dockerfile` |
| Docker target | leave empty (default final `runner` stage) |
| Install / Build / Start commands | leave empty (Dockerfile owns the pipeline) |

### 4. Docker build argument — `BUILD_WASM=1`

The WASM sim is compiled in the Dockerfile `wasm` stage. Set the build arg in Coolify:

**UI path:** Application → **Environment Variables** → **Add**

| Key | Value | Build time | Runtime |
|-----|-------|------------|---------|
| `BUILD_WASM` | `1` | **Yes** | **No** |

- **Build time = Yes** maps to Docker `ARG BUILD_WASM` (required for full WASM publish).
- **Runtime = No** — the running container does not read this; sim mode is determined by files under `web/public/dotnet/`.

**CLI equivalent** (after app exists):

```bash
coolify env-set citymajor-wasm-r3f-integration BUILD_WASM 1
# ensure is_buildtime=true via Coolify UI if the CLI does not set it
coolify deploy citymajor-wasm-r3f-integration --force
```

Use `BUILD_WASM=0` only for faster procedural-only previews (skips .NET SDK stage).

### 5. Networking & domain

| Field | Value |
|-------|-------|
| Ports exposes | `3000` |
| Ports mappings | leave default (Traefik routes to container 3000) |
| Domain | Coolify auto-suggests e.g. `citymajor-feat-wasm-r3f-integration-2026-07-04.apps.softblaze.net` |

Preview hostnames use the `*.apps.softblaze.net` wildcard (Cloudflare tunnel `softblaze-preview-wildcard`). Do not attach a custom apex domain here until prod routing is defined.

### 6. Health check (recommended)

| Field | Value |
|-------|-------|
| Enabled | Yes |
| Path | `/play` |
| Port | `3000` |
| Method | `GET` |
| Return code | `200` |
| Interval / timeout | `30s` / `10s` |
| Start period | `60s` (WASM images are slower to become ready) |

`/play` confirms the Next.js standalone server and play route; `/` also works for a lighter check.

### 7. Resources (WASM build profile)

| Resource | Minimum for `BUILD_WASM=1` |
|----------|----------------------------|
| CPU | 2 cores |
| Memory | 2–4 GB |

The `wasm` stage runs `dotnet publish` for browser WASM — under-provisioned builders OOM or time out; the Dockerfile logs a warning and ships procedural-only if publish fails.

### 8. Runtime environment variables (optional)

M0 preview needs **no secrets**. Omit Stripe vars unless testing checkout:

| Variable | Required | Notes |
|----------|----------|-------|
| `PORT` | No | `3000` (set in Dockerfile) |
| `HOSTNAME` | No | `0.0.0.0` (set in Dockerfile) |
| `NODE_ENV` | No | `production` (set in Dockerfile) |
| `STRIPE_*` | No | See [Environment variables](#environment-variables) |

### 9. First deploy

1. **Save** the application configuration.
2. Click **Deploy** (or push a commit to `feat/wasm-r3f-integration-2026-07-04`).
3. Watch the build log — expect stages: `wasm` → `deps` → `builder` → `runner` (~8–12 min first WASM build).
4. When status is **running**, open the FQDN.

**Verify WASM shipped:**

```bash
FQDN="https://citymajor-feat-wasm-r3f-integration-2026-07-04.apps.softblaze.net"
curl -sf "$FQDN/dotnet/_framework/blazor.boot.json" && echo "WASM assets OK"
curl -sI "$FQDN/play" | grep -i cross-origin
```

In the browser: `/play` → FPS HUD shows **Data: WASM sim** (not **procedural**).

**If WASM is missing** but the app is up: check build log for `WARN: WASM build failed`; fix builder RAM/SDK, redeploy with `BUILD_WASM=1`. Procedural fallback remains playable.

### 10. Ongoing deploys

```bash
git push origin feat/wasm-r3f-integration-2026-07-04
```

One commit → one deploy. Poll with `coolify doctor citymajor-wasm-r3f-integration` or the deploy webhook reminder before pushing again.

### API / MCP shortcut

Once project and server UUIDs are known, apps can be created via Coolify API (`coolify-application` action `create_github`) with the same fields above. The UI walkthrough remains the source of truth for first-time setup.

## Docker build stages

```
wasm (optional) → deps → builder → runner
```

| Stage | Purpose |
|-------|---------|
| `wasm` | .NET 8 SDK publishes `Forge.SimWasm` → `web/public/dotnet/` (copies `base/data/` for embedded events/tech JSON) |
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
