# CityMajor Web — Coolify Preview Deploy

Deploy the Next.js web client (`web/`) to Coolify for branch previews. Production domain routing is TBD; preview-only apps use `*.apps.softblaze.net`.

## Quick reference

| Setting | Value |
|---------|-------|
| **Coolify app name** | `citymajor-web` |
| **App UUID** | `w134tsftvj327kp96j45kcsb` |
| **Preview URL** | [https://citymajor.apps.softblaze.net](https://citymajor.apps.softblaze.net) |
| Repository | `gkinter/citymajor` |
| Branch | `feat/wasm-r3f-integration-2026-07-04` |
| Build pack | **Dockerfile** (repo root) |
| Dockerfile | `/Dockerfile` |
| Build arg | `BUILD_WASM=1` (build-time only) |
| Port exposes | `3000` |
| Health check | `GET /play` (or `GET /`) |
| Auto-deploy | Enable for the watched branch |

## Auto-deploy incident — commit `79ef561` (2026-07-04)

### Symptom

Push `79ef561` (`feat/web`: economy panel, Meshy catalog, etc.) landed on `feat/wasm-r3f-integration-2026-07-04` at **2026-07-04T14:24:55Z**, but Coolify did not build until a **manual** deploy at **2026-07-04T14:57:39Z** (`deployment_uuid` `xan322w8hhmjxcfi5l1z71xw`). The prior automatic-looking deploy was `3076c29` at **13:58:41Z**; intermediate commit `86dd2ff` (**14:07:29Z**) also never queued a deployment.

### What we checked (Coolify app `w134tsftvj327kp96j45kcsb`)

| Check | Result |
|-------|--------|
| `git_branch` | `feat/wasm-r3f-integration-2026-07-04` (matches pushed branch) |
| `git_repository` | `gkinter/citymajor` |
| `watch_paths` | empty (full repo) |
| `application_settings.is_auto_deploy_enabled` | **true** |
| `application_settings.is_preview_deployments_enabled` | false |
| GitHub `PushEvent` for the branch | present at 14:25Z (GitHub received the push) |
| Deployment rows (`application_deployment_queues`) | **no row** for `86dd2ff` or `79ef561` until manual deploy |

### Root cause

The app is bound to Coolify source **`Public GitHub`** (`applications.source_id = 0`), not the fleet **GitHub App** source (`source_id = 1`, installation on `digitalsoftdistribution`). Every deployment for this app is flagged **`is_api = true`, `is_webhook = false`** — pushes do not enqueue builds even when **Automatic Deployment** is enabled in settings.

Contrast: apps wired to the GitHub App (e.g. `tracklayer`, `source_id = 1`) record **`is_webhook = true`** on push-triggered deploys.

Creating the app via **Public Repository** / `Public GitHub` without the org GitHub App explains why early deploys only happened when someone clicked **Deploy** or called `coolify deploy --force`.

### Fix (required for push → deploy)

1. **Install** the Coolify GitHub App on the **`gkinter`** org (or grant the existing app access to `gkinter/citymajor`) — Settings → GitHub App → configure repository access.
2. In Coolify UI: **`citymajor-web`** → **Source** → switch from **Public GitHub** to **GitHub App** (same app used for `DigitalSoftDistribution/*` previews).
3. Re-select repository **`gkinter/citymajor`**, branch **`feat/wasm-r3f-integration-2026-07-04`**, keep **Automatic Deployment** enabled.
4. Push a no-op doc commit **or** `coolify deploy citymajor-web --force` once after reconnecting.
5. **Verify** the next push creates a deployment with **`is_webhook = true`** (SQL via Beast: `application_deployment_queues` for this app, or watch Coolify UI “Webhook” trigger).

Optional hardening:

- Retire duplicate preview app **`citymajor-wasm-r3f-integration`** if still on `source_id = 0`.
- Enable HTTP health check (currently disabled on this app).
- Refresh Mac `~/.secrets/coolify-api.env` if local `coolify` returns **401** (Beast `/home/devops/bin/coolify` remains authoritative).

### Ops queries (no secrets)

```bash
# App summary
ssh beast '/home/devops/bin/coolify status citymajor-web'

# Last deployments + SHAs
ssh beast '/home/devops/bin/coolify deploys w134tsftvj327kp96j45kcsb 10'

# Auto-deploy flag + webhook vs API trigger (Beast → Coolify Postgres)
ssh beast 'bash -lc "source /home/devops/.coolify-mcp.env; eval "\$(sed -n "/^vps_psql()/,/^}/p" /home/devops/bin/coolify)"; vps_psql "SELECT s.is_auto_deploy_enabled, a.source_id, a.git_branch FROM application_settings s JOIN applications a ON s.application_id=a.id WHERE a.uuid='"'"'w134tsftvj327kp96j45kcsb'"'"'""'
ssh beast 'bash -lc "source /home/devops/.coolify-mcp.env; eval "\$(sed -n "/^vps_psql()/,/^}/p" /home/devops/bin/coolify)"; vps_psql "SELECT deployment_uuid, commit, is_webhook, is_api, created_at FROM application_deployment_queues WHERE application_id::text=(SELECT id::text FROM applications WHERE uuid='"'"'w134tsftvj327kp96j45kcsb'"'"') ORDER BY created_at DESC LIMIT 5""'
```

After the fix, `source_id` should be **1** (or another non-zero GitHub App row), and push deploys should show **`is_webhook = true`**.

> **Deprecated preview:** `citymajor-wasm-r3f-integration` (branch-slug FQDN e.g. `citymajor-feat-wasm-r3f-integration-2026-07-04.apps.softblaze.net`) is superseded by **`citymajor-web`** at the canonical URL above. Use `coolify doctor citymajor-web` and the UUID in CLI/MCP calls; retire or disable the old app once traffic is confirmed on the canonical preview.

## Coolify app creation (step-by-step)

Use this checklist when creating the **first** preview app for the WASM + R3F integration branch. CityMajor is **preview-only** (no live `citymajor.com` prod path yet) — pushes to the watched branch trigger Coolify builds.

### Prerequisites

1. Access to Coolify at [https://coolify.softblaze.net](https://coolify.softblaze.net).
2. GitHub App **Softblaze Coolify** installed on `gkinter/citymajor` (read + deploy hooks).
3. Branch `feat/wasm-r3f-integration-2026-07-04` pushed to GitHub with root `Dockerfile` present.
4. Server destination: default preview server (AX41, `*.apps.softblaze.net` wildcard via Cloudflare tunnel).

### 1. Create the application

1. **Projects** → open the CityMajor project (or create one, e.g. `citymajor`).
2. **+ New** → **Application** → **Private Repository (GitHub App)** only — do **not** use **Public GitHub** (`source_id = 0`); push webhooks will not auto-deploy (see [Auto-deploy incident](#auto-deploy-incident--commit-79ef561-2026-07-04)).
3. Select repository **`gkinter/citymajor`**.
4. **Name**: `citymajor-web` (canonical Coolify app; custom FQDN `citymajor.apps.softblaze.net`).
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
coolify env-set citymajor-web BUILD_WASM 1
# ensure is_buildtime=true via Coolify UI if the CLI does not set it
coolify deploy citymajor-web --force
```

Use `BUILD_WASM=0` only for faster procedural-only previews (skips .NET SDK stage).

### 5. Networking & domain

| Field | Value |
|-------|-------|
| Ports exposes | `3000` |
| Ports mappings | leave default (Traefik routes to container 3000) |
| Domain | `citymajor.apps.softblaze.net` (canonical; app UUID `w134tsftvj327kp96j45kcsb`) |

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

M0 preview needs **no secrets**. Omit Stripe vars unless testing live Founder Pass checkout.

| Variable | Required | Notes |
|----------|----------|-------|
| `PORT` | No | `3000` (set in Dockerfile) |
| `HOSTNAME` | No | `0.0.0.0` (set in Dockerfile) |
| `NODE_ENV` | No | `production` (set in Dockerfile) |
| `STRIPE_*` | No | See [Stripe test-mode setup (SB-3714)](#stripe-test-mode-setup-sb-3714) |

#### Stripe test-mode setup (SB-3714)

> **Linear:** [SB-3714](https://linear.app/softblaze/issue/SB-3714) — Stripe test-mode env + webhook on `citymajor-web`  
> **Related:** [SB-3693_AUTH_ENTITLEMENTS_GAP.md](./design/SB-3693_AUTH_ENTITLEMENTS_GAP.md) (checkout → webhook → tier-store flow)

Live Founder Pass checkout needs **runtime** secrets plus a **build-time** publishable key. Omit all Stripe vars to keep the mock cookie checkout on `/shop`.

**Quick reference — `coolify env-set` on `citymajor-web`** (`w134tsftvj327kp96j45kcsb`):

```bash
APP=citymajor-web
export STRIPE_SECRET_KEY='sk_test_…'              # Dashboard → Developers → API keys
export STRIPE_FOUNDER_PASS_PRICE_ID='price_…'     # Dashboard → Product catalog → price ID
export STRIPE_WEBHOOK_SECRET='whsec_…'            # Dashboard → Webhooks → signing secret (after §3)

coolify env-set "$APP" STRIPE_SECRET_KEY "$STRIPE_SECRET_KEY"
coolify env-set "$APP" STRIPE_FOUNDER_PASS_PRICE_ID "$STRIPE_FOUNDER_PASS_PRICE_ID"
coolify env-set "$APP" STRIPE_WEBHOOK_SECRET "$STRIPE_WEBHOOK_SECRET"
coolify deploy "$APP" --force
```

Use quoted `"$VAR"` so `whsec_…` values with special characters are passed literally. Full phased setup (publishable key, build-time flag, verification curls) is in [§2](#2-coolify-env-vars-on-citymajor-web) and [§3](#3-stripe-dashboard--webhook-endpoint-preview).

**Canonical preview webhook URL:**

```
https://citymajor.apps.softblaze.net/api/webhooks/stripe
```

Use this exact path when registering webhooks in the Stripe Dashboard or when verifying the Coolify deploy. A `GET` on that URL returns JSON with `checkoutConfigured` / `webhookConfigured` flags and, when misconfigured, `missingCheckout` / `missingWebhook` arrays listing unset env keys.

**Env validation smoke tests** (run after deploy; no secrets required):

```bash
FQDN="https://citymajor.apps.softblaze.net"

# Shop + checkout readiness (lists missing STRIPE_* keys when unset)
curl -s "$FQDN/api/shop" | jq .
curl -s "$FQDN/api/checkout/founder-pass" | jq .

# Webhook readiness
curl -s "$FQDN/api/webhooks/stripe" | jq .

# POST without Stripe keys → 503 with explicit missing[] (not 500)
curl -s -o /dev/null -w "%{http_code}\n" -X POST "$FQDN/api/checkout/founder-pass"
# Expect 503 when checkout env incomplete; body includes missing: ["STRIPE_SECRET_KEY", ...]
```

When Stripe runtime keys are absent, `POST /api/checkout/founder-pass` and `POST /api/webhooks/stripe` return **503** with `{ "error": "…", "missing": ["STRIPE_…"], "configured": false }`. `/shop` falls back to mock cookie checkout when `stripeCheckoutEnabled` is false (see `GET /api/shop`).

##### 1. Stripe Dashboard — test mode product

1. Open [Stripe Dashboard](https://dashboard.stripe.com) and enable **Test mode** (toggle top-right).
2. **Product catalog** → **+ Add product** → name e.g. `CityMajor Founder Pass (test)`.
3. Add a **one-time price** (USD or your test currency) → copy the **Price ID** (`price_…`). This becomes `STRIPE_FOUNDER_PASS_PRICE_ID`.
4. **Developers** → **API keys** → copy:
   - **Publishable key** (`pk_test_…`) → `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`
   - **Secret key** (`sk_test_…`) → `STRIPE_SECRET_KEY`

Never commit real keys; use `web/.env.example` placeholders locally.

##### 2. Coolify env vars on `citymajor-web`

**Target app** (either identifier works with `coolify env-set`):

| Identifier | Value |
|------------|-------|
| App name | `citymajor-web` |
| App UUID | `w134tsftvj327kp96j45kcsb` |

**UI path:** Application **`citymajor-web`** → **Environment Variables** → **Add**

| Key | Example (test mode) | Build time | Runtime | Purpose |
|-----|---------------------|------------|---------|---------|
| `STRIPE_SECRET_KEY` | `sk_test_…` | **No** | **Yes** | Server-side Checkout Session creation (`POST /api/checkout/founder-pass`) |
| `STRIPE_FOUNDER_PASS_PRICE_ID` | `price_…` | **No** | **Yes** | Founder Pass Stripe Price ID |
| `STRIPE_WEBHOOK_SECRET` | `whsec_…` | **No** | **Yes** | Verifies `POST /api/webhooks/stripe` (set in [§3](#3-stripe-dashboard--webhook-endpoint-preview) after Dashboard endpoint exists) |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | `pk_test_…` | **Yes** | **No** | Inlined at Next.js build; safe to expose in browser |

Also ensure production identity signing is set (required for checkout user binding):

| Key | Build time | Runtime | Notes |
|-----|------------|---------|-------|
| `CITYMAJOR_SESSION_SECRET` | **No** | **Yes** | ≥32 chars; signs `citymajor_uid` cookie used as webhook `userKey` |

**CLI — phase A: checkout keys** (from Stripe Dashboard §1; never commit or paste into docs):

```bash
APP=citymajor-web   # or: APP=w134tsftvj327kp96j45kcsb

# Paste test-mode values from Dashboard → Developers → API keys / Product catalog
export STRIPE_SECRET_KEY='sk_test_REPLACE_ME'
export STRIPE_FOUNDER_PASS_PRICE_ID='price_REPLACE_ME'
export NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY='pk_test_REPLACE_ME'

coolify env-set "$APP" STRIPE_SECRET_KEY "$STRIPE_SECRET_KEY"
coolify env-set "$APP" STRIPE_FOUNDER_PASS_PRICE_ID "$STRIPE_FOUNDER_PASS_PRICE_ID"
coolify env-set "$APP" NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY "$NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY"
```

`coolify env-set` writes **runtime** vars by default. For `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`, open Coolify → **Environment Variables** → edit that key → enable **Available at Buildtime** (or set via MCP `coolify-env-vars` `create` with `is_buildtime: true`). Then redeploy:

```bash
coolify deploy "$APP" --force
```

**Verify checkout keys** (no secrets in output):

```bash
FQDN="https://citymajor.apps.softblaze.net"
curl -s "$FQDN/api/shop" | jq '{stripeCheckoutEnabled, missing}'
curl -s "$FQDN/api/checkout/founder-pass" | jq '{configured, missing}'
# Expect stripeCheckoutEnabled: true and missing: [] when STRIPE_SECRET_KEY + STRIPE_FOUNDER_PASS_PRICE_ID are live
```

Changing `NEXT_PUBLIC_*` or `BUILD_WASM` requires a **rebuild** (not just restart). Runtime-only secrets (`STRIPE_SECRET_KEY`, `STRIPE_FOUNDER_PASS_PRICE_ID`, later `STRIPE_WEBHOOK_SECRET`) take effect after redeploy without a full rebuild when only those keys change.

##### 3. Stripe Dashboard — webhook endpoint (preview)

1. **Developers** → **Webhooks** → **+ Add endpoint**.
2. **Endpoint URL:** `https://citymajor.apps.softblaze.net/api/webhooks/stripe`
3. **Events to send:** select **`checkout.session.completed`** (only event handled in v1).
4. After creation, open the endpoint → **Signing secret** → copy `whsec_…`.

**CLI — phase B: webhook signing secret** (Dashboard endpoint secret — **not** the `stripe listen` secret from local dev):

```bash
APP=citymajor-web   # or: APP=w134tsftvj327kp96j45kcsb

export STRIPE_WEBHOOK_SECRET='whsec_REPLACE_ME'   # Dashboard → Webhooks → endpoint → Signing secret

coolify env-set "$APP" STRIPE_WEBHOOK_SECRET "$STRIPE_WEBHOOK_SECRET"
coolify deploy "$APP" --force
```

**Verify endpoint is reachable:**

```bash
curl -s "https://citymajor.apps.softblaze.net/api/webhooks/stripe" | jq .
# Expect: { "status": "ok", "checkoutConfigured": true, "webhookConfigured": true, ... }
# When misconfigured: "missingCheckout" / "missingWebhook" arrays name unset env keys
```

Send a test event from the Dashboard (**Send test webhook** → `checkout.session.completed`) only after env vars are live; unsigned test payloads from the Dashboard still require the signing secret to match.

##### 4. End-to-end test on preview

1. Open [https://citymajor.apps.softblaze.net/shop](https://citymajor.apps.softblaze.net/shop).
2. Confirm the UI shows Stripe checkout (not mock cookie grant) when `checkoutConfigured` is true.
3. Click through Founder Pass checkout; use Stripe test card `4242 4242 4242 4242`, any future expiry, any CVC.
4. After redirect to `/shop?checkout=success`, call entitlements:

```bash
curl -s -b "citymajor_uid=<cookie-from-browser>" \
  "https://citymajor.apps.softblaze.net/api/me/entitlements" | jq .
# Expect tier: "founder_pass" after webhook delivery
```

5. In Stripe Dashboard → **Webhooks** → your endpoint → **Recent deliveries** — confirm `checkout.session.completed` returned **200**.

##### 5. Stripe CLI — local webhook forwarding

Use the [Stripe CLI](https://stripe.com/docs/stripe-cli) to exercise webhooks against a local Next.js dev server without exposing localhost to the internet.

**Prerequisites:** Stripe CLI installed (`brew install stripe/stripe-cli/stripe`), logged in (`stripe login`), and `web/.env.local` filled from `web/.env.example` (all four `STRIPE_*` vars + `CITYMAJOR_SESSION_SECRET`).

**Terminal A — app:**

```bash
cd web
cp .env.example .env.local   # if not already present; edit with test keys
pnpm dev
```

**Terminal B — forward webhooks:**

```bash
stripe listen --forward-to localhost:3000/api/webhooks/stripe
```

The CLI prints a **webhook signing secret** (`whsec_…`). Put **that** value in `web/.env.local` as `STRIPE_WEBHOOK_SECRET` (it differs from the Dashboard endpoint secret). Restart `pnpm dev` after updating.

**Trigger a test checkout session completed event** (after a real test checkout, or with a fixture):

```bash
# Optional: fire a synthetic event (signature matches stripe listen secret)
stripe trigger checkout.session.completed
```

For a full local E2E path:

1. Visit `http://localhost:3000/shop` → start Founder Pass checkout.
2. Complete payment with test card `4242 4242 4242 4242`.
3. Watch Terminal B for `checkout.session.completed` → `200` from your app.
4. Confirm tier:

```bash
curl -s -b "citymajor_uid=$(node -e "
  // or copy cookie from browser DevTools → Application → Cookies
")" http://localhost:3000/api/me/entitlements | jq .
```

**CLI vs Dashboard secrets:** Local dev uses the `whsec_…` from `stripe listen`. Coolify preview uses the `whsec_…` from the Dashboard endpoint registered to `https://citymajor.apps.softblaze.net/api/webhooks/stripe`. Do not mix them.

##### 6. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `/shop` still uses mock checkout | Missing `STRIPE_SECRET_KEY` or `STRIPE_FOUNDER_PASS_PRICE_ID` | `curl …/api/shop \| jq .missing`; set vars on `citymajor-web`, redeploy |
| `POST /api/checkout/founder-pass` returns **503** | Checkout env incomplete | Response body lists `missing` keys; set on Coolify, redeploy |
| Webhook returns **503** | `STRIPE_WEBHOOK_SECRET` and/or `STRIPE_SECRET_KEY` unset | `GET /api/webhooks/stripe` → check `missingWebhook`; add secrets, redeploy |
| Webhook returns **400** signature error | Wrong `whsec_` for that URL (CLI secret on preview, or vice versa) | Use Dashboard secret on Coolify; CLI secret only for `stripe listen` |
| Checkout succeeds but tier stays `free` | Webhook ignored — invalid `userKey` in session metadata | Ensure `CITYMAJOR_SESSION_SECRET` is set; user must have `citymajor_uid` cookie before checkout |
| Tier lost after redeploy | v1 tier-store is file/in-memory stub | Expected for spike; durable store is v1.5 ([SB-3693](./design/SB-3693_AUTH_ENTITLEMENTS_GAP.md)) |

Local parity: copy `web/.env.example` → `web/.env.local` and fill placeholders before `pnpm dev`.

### 9. First deploy

1. **Save** the application configuration.
2. Click **Deploy** (or push a commit to `feat/wasm-r3f-integration-2026-07-04`).
3. Watch the build log — expect stages: `wasm` → `deps` → `builder` → `runner` (~8–12 min first WASM build).
4. When status is **running**, open the FQDN.

**Verify WASM shipped:**

```bash
FQDN="https://citymajor.apps.softblaze.net"
curl -sf "$FQDN/dotnet/_framework/blazor.boot.json" && echo "WASM assets OK"
curl -sI "$FQDN/play" | grep -i cross-origin
```

In the browser: `/play` → FPS HUD shows **Data: WASM sim** (not **procedural**).

**If WASM is missing** but the app is up: check build log for `WARN: WASM build failed`; fix builder RAM/SDK, redeploy with `BUILD_WASM=1`. Procedural fallback remains playable.

### 10. Ongoing deploys

```bash
git push origin feat/wasm-r3f-integration-2026-07-04
```

One commit → one deploy. Poll with `coolify doctor citymajor-web` or the deploy webhook reminder before pushing again.

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
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | No | — | Stripe publishable key (`pk_test_…` / `pk_live_…`); **build-time** Docker arg / Coolify env |

Without Stripe vars, `/shop` uses the mock entitlements cookie path. Checkout reads `STRIPE_SECRET_KEY` + `STRIPE_FOUNDER_PASS_PRICE_ID` server-side; webhooks require `STRIPE_SECRET_KEY` + `STRIPE_WEBHOOK_SECRET`. Diagnostic routes: `GET /api/shop`, `GET /api/checkout/founder-pass`, `GET /api/webhooks/stripe` (each returns `missing` / `missingCheckout` / `missingWebhook` when keys are absent). See `web/.env.example` and [Stripe test-mode setup (SB-3714)](#stripe-test-mode-setup-sb-3714).

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
- [`design/SB-3693_AUTH_ENTITLEMENTS_GAP.md`](./design/SB-3693_AUTH_ENTITLEMENTS_GAP.md) — checkout, webhook, tier-store acceptance
