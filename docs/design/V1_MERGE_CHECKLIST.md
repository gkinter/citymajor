# CityMajor Web v1 — Merge Checklist

**Status:** Pre-merge gate (integration branch review)  
**Branch:** `feat/wasm-r3f-integration-2026-07-04`  
**PR:** [#1 — feat: CityMajor Web v1 — R3F + WASM integration spine](https://github.com/gkinter/citymajor/pull/1)  
**Scope charter:** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) · [SB-3704](https://linear.app/softblaze/issue/SB-3704)  
**Last updated:** 2026-07-04 (`e4d3d4f`)

> PR #1 documents the v1 integration spine for review and preview deploys. **Do not merge to `main` until every **blocking** row below is checked.** Deferred items stay tracked in Linear.

---

## Gate summary

| Gate | Blocking? | Status (2026-07-04, `e4d3d4f`) |
|------|-----------|--------------------------------|
| [Cursor Bugbot](#1-cursor-bugbot) | Yes | **Pending** — HEAD advanced past `82d8df4`; re-run required on gameplay sprint |
| [Smoke suite 22/22](#2-smoke-suite-2222) | Yes | **Not verified in CI** — run locally before merge |
| [Coolify preview URL](#3-coolify-preview-url) | Yes | **Healthy** — [SB-3715](https://linear.app/softblaze/issue/SB-3715) Done · `citymajor-web` |
| [Gameplay sprint](#8-gameplay-sprint-e4d3d4f) | Yes (player loop) | **Shipped** — roads, traffic, events, citizens, services, research UX, onboarding, landmarks |
| [Meshy assets](#4-meshy-assets) | Yes (v1 art minimum) | **Partial** — ~20 GLBs on disk; batch hit 502s; MCP gateway + retries in flight |
| [Stripe stub vs live](#5-stripe-stub-vs-live) | Yes (monetization path) | **Stub + docs** — env vars documented in `3efdf06` / [DEPLOY_WEB.md](../DEPLOY_WEB.md) |
| [Perf gate](#6-perf-gate) | Yes | **Not signed off** — [SB-3703](https://linear.app/softblaze/issue/SB-3703) |
| [Open Linear issues](#7-open-linear-issues) | Track | **30+ Triage** on CityMajor Web v1 project |

---

## 8. Gameplay sprint (`e4d3d4f`)

Phase 2 player-loop work landed on the integration branch (2026-07-04).

| Area | Commit range / issue | Status |
|------|----------------------|--------|
| Road paint brush | `be44700` — WASM `PlaceRoad` | **Shipped** |
| Traffic congestion heatmap | `5a02efb` | **Shipped** |
| Zone paint feedback + building pop-in | `c97cc9d` | **Shipped** |
| Approval consequences in HUD / Herald | `275b1f9` | **Shipped** |
| Era landmark placeholder (city center) | `ce31f4f` — [SB-3719](https://linear.app/softblaze/issue/SB-3719) | **Shipped** (placeholder) |
| Active WASM event markers | `d0dbfc3` | **Shipped** |
| Economy in player loop | `775eb4a` — [SB-3690](https://linear.app/softblaze/issue/SB-3690) | **Shipped** |
| Service coverage overlay | `3066f59` — [SB-3691](https://linear.app/softblaze/issue/SB-3691) | **Shipped** |
| Household population + citizen render | `7539b37` — [SB-3689](https://linear.app/softblaze/issue/SB-3689) | **Shipped** |
| Research UX (panel open/close in smoke) | prior spine | **Shipped** |
| Onboarding — core loop tutorial | `c005cb8` | **Shipped** |
| GLTF catalog fallback + per-tile transforms | `a283d66` | **Shipped** |
| `/api/saves` preview 500 | `ac51180` | **Fixed** on preview |

**HEAD:** `e4d3d4f` — `chore(assets): refresh frontier GLB and drop dotnet gitkeep`

---

## 1. Cursor Bugbot

Automated code review on PR #1.

| Check | Command / link | Pass criteria |
|-------|----------------|---------------|
| Bugbot run completed | [PR #1 checks](https://github.com/gkinter/citymajor/pull/1) | Status `COMPLETED` |
| No Critical / Major findings | Re-run after new commits | Zero blocking severity |
| Latest reviewed SHA | `e4d3d4f` | Re-trigger — gameplay sprint advanced HEAD past `82d8df4` |

**Current:** Bugbot **pending** on `e4d3d4f` (2026-07-04). Last clean run was **NEUTRAL** on `82d8df4` — zero Critical/Major findings. Re-run required before merge.

```bash
# After push — watch PR checks or use review-bugbot skill locally
gh pr checks 1
```

---

## 2. Smoke suite 22/22

Headless Playwright suite: `pnpm smoke:all` (routes `/`, `/shop`, `/play`).

### Check inventory (22 assertions)

| # | Suite | Assertion |
|---|-------|-----------|
| 1–3 | HTTP | `GET /`, `/shop`, `/play` → 200 |
| 4 | `/` | Hero copy: "Build your city across 200 years" |
| 5–6 | `/shop` | "CityMajor Shop", "Founder Pass" visible |
| 7 | `/play` | COOP `same-origin` + COEP `require-corp` |
| 8–9 | `/play` | Diagnostics HUD + CityMajor wordmark |
| 10–11 | `/play` | WebGL canvas mounted + context created |
| 12–13 | API | `GET /api/saves` envelope; malformed `POST` → 400 |
| 14–15 | HUD | Research panel open / close |
| 16–17 | HUD | Herald panel loads headline / closes |
| 18 | `/play` | Sim source reported (`WASM sim` or `procedural`) |
| 19 | `/play` | Buildings count > 0 in HUD |
| 20 | `/play` | FPS reported (warn-only if `—` in headless) |
| 21 | `/play` | No unexpected console errors |
| 22 | Optional | Screenshot saved when `SCREENSHOT=1` |

### Run locally (full WASM path)

```bash
cd /path/to/citymajor-web-r3f-spike
unset NODE_OPTIONS
pnpm build:wasm && pnpm dev          # terminal 1
WASM_EXPECTED=1 pnpm smoke:all       # terminal 2 — expect 21 pass (+1 if SCREENSHOT=1)
```

### Run against Coolify preview

```bash
BASE_URL=https://citymajor.apps.softblaze.net \
  WASM_EXPECTED=1 pnpm smoke:all
```

> **Update (`ac51180`):** `/api/saves` preview 500 fixed — smoke assertions #12–13 should pass against `BASE_URL` on `e4d3d4f+`.

**Pass:** Console ends with `[smoke-all] All checks passed.` — **22/22** with `SCREENSHOT=1`, **21/21** without.

**Scripts:** `web/scripts/smoke-all.mjs`, `smoke-play-checks.mjs` · Docs: `web/PERF.md` § Automated headless smoke test.

---

## 3. Coolify preview URL

Preview-only deploy per [DEPLOY_WEB.md](../DEPLOY_WEB.md).

| Check | Value |
|-------|-------|
| App name | `citymajor-web` |
| Build pack | Root `Dockerfile` |
| Build arg | `BUILD_WASM=1` (build-time) |
| Port | `3000` |
| Health check | `GET /play` → 200 |
| Canonical FQDN | [https://citymajor.apps.softblaze.net](https://citymajor.apps.softblaze.net) |

### Verification

```bash
coolify doctor citymajor-web
FQDN="https://citymajor.apps.softblaze.net"
curl -sf "$FQDN/dotnet/_framework/blazor.boot.json" && echo "WASM assets OK"
curl -sI "$FQDN/play" | grep -i cross-origin
```

| Page | Expected |
|------|----------|
| `/` | Landing loads |
| `/shop` | Founder Pass card |
| `/play` | HUD shows **Data: WASM sim** (not procedural-only) |
| `/api/saves` | Envelope OK on preview (`ac51180`+) |

**Linear:** [SB-3715 — M2: Coolify deploy](https://linear.app/softblaze/issue/SB-3715/m2-coolify-deploy-preview-app-for-citymajor-web) — **Done**

**Status:** Preview **healthy** at canonical FQDN on `e4d3d4f`. Gameplay sprint deploy verified.

---

## 4. Meshy assets

Art pipeline: [MESHY_ASSET_PIPELINE.md](./MESHY_ASSET_PIPELINE.md) · [BUILDING_ARCHETYPE_3D.md](./BUILDING_ARCHETYPE_3D.md) · [SB-3678](https://linear.app/softblaze/issue/SB-3678)

| Milestone | Target | Current (`e4d3d4f`) |
|-----------|--------|---------------------|
| Batch script + manifest | `scripts/meshy/batch-generate.mjs` | **Done** (manifest ~15 seed jobs) |
| MCP gateway path | `6095f2f` — `MESHY_USE_MCP=1` | **Done** — Softblaze Meshy MCP when API key unset |
| v1 minimum GLBs | ~80–120 core archetypes (Frontier + Industrial) | **Partial** — ~20 GLBs on disk (Frontier seed set) |
| Hero landmarks | 3 for v1 arc (civic hall, factory, terminus) | **Placeholder** in scene ([SB-3719](https://linear.app/softblaze/issue/SB-3719)); Meshy heroes pending [SB-3680](https://linear.app/softblaze/issue/SB-3680) |
| On-disk assets | `web/public/assets/gltf/{era}/*.glb` | **Partial** — batch runs hit **502** from gateway; retry remaining jobs |
| Runtime fallback | InstancedMesh + procedural modules | **Works** — merge OK for spine, not for art-complete v1 |

### Pre-merge minimum (CTO roadmap P0 #9, #11)

- [x] MCP gateway path for batch when `MESHY_API_KEY` unset (`6095f2f`)
- [ ] Run Meshy batch for Frontier + Industrial core set (~80 GLBs) — **in progress; 502 retries**
- [ ] Post-process: bottom-center pivot, 1-story unit scale
- [ ] `pnpm generate:gltf-placeholders` replaced for shipped keys in `web/lib/gltf-catalog.ts`
- [ ] 3 hero landmarks placed in scene at era gates (placeholder only today)
- [ ] CDN / immutable cache strategy — [SB-3682](https://linear.app/softblaze/issue/SB-3682)

```bash
# Validate manifest before batch spend
node scripts/meshy/validate-manifest.mjs
# Batch (requires MESHY_API_KEY or MESHY_USE_MCP=1)
node scripts/meshy/batch-generate.mjs --dry-run
MESHY_USE_MCP=1 node scripts/meshy/batch-generate.mjs
```

---

## 5. Stripe stub vs live

Founder Pass ($24.99) per [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) §5.

| Mode | Env vars | Behavior |
|------|----------|----------|
| **Stub (default)** | None | `/shop` mock checkout; `citymajor_founder_pass` cookie; in-memory entitlements |
| **Live (test)** | `STRIPE_SECRET_KEY`, `STRIPE_FOUNDER_PASS_PRICE_ID`, `STRIPE_WEBHOOK_SECRET` | Real Checkout Session + webhook → entitlements |
| **Live (prod)** | Above + Stripe live keys + Coolify FQDN webhook | Required for public launch — **not** PR #1 merge |

### Routes

- `POST /api/checkout/founder-pass` — creates Checkout Session or stub redirect
- `POST /api/webhooks/stripe` — signature-verified entitlement grant
- `GET /api/me/entitlements` — tier + narrative quota

### Pre-merge checks

| Check | Stub | Live (test mode) |
|-------|------|------------------|
| `/shop` loads Founder Pass | [ ] | [ ] |
| Checkout initiates without 500 | [ ] | [ ] |
| Webhook registered to preview FQDN | N/A | [ ] |
| Entitlements reflect purchase | Cookie path | [ ] |
| No pay-to-win gates on sim | [ ] | [ ] |

**Linear:** [SB-3698](https://linear.app/softblaze/issue/SB-3698) (checkout) · [SB-3693](https://linear.app/softblaze/issue/SB-3693) (auth + entitlements hardening)

**Docs:** Stripe env vars + webhook FQDN documented in [DEPLOY_WEB.md](../DEPLOY_WEB.md) and `web/.env.example` (`3efdf06`).

**Status:** Code stub ships in PR #1; Coolify env wiring documented. **Live Stripe optional for integration merge**, **required for launch**.

---

## 6. Perf gate

Targets from [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) §4 and `web/PERF.md`.

| Metric | Target | Gate |
|--------|--------|------|
| FPS (integrated GPU) | ≥30 at max city fill + LOD | Manual benchmark on target hardware |
| FPS (discrete GPU) | ≥60 with LOD | Manual benchmark |
| Draw calls | 20–40 per visible chunk | DevTools / Spector optional |
| WASM snapshot rate | ≤4 Hz to main thread | HUD diagnostics |
| Sim tick | ≤10 ms/tick at 10K HH | [SB-3685](https://linear.app/softblaze/issue/SB-3685) profiling |

### Sign-off checklist — [SB-3703](https://linear.app/softblaze/issue/SB-3703)

- [ ] Interactive `/play` benchmark table filled in `web/PERF.md` (machine, browser, DPR, FPS orbit + district)
- [ ] `AdaptiveDpr` degrades when FPS < 30 for 2s (integrated)
- [ ] WASM path: ~220→5k buildings without frame collapse
- [ ] QA matrix integrated vs discrete — [SB-3705](https://linear.app/softblaze/issue/SB-3705)
- [ ] CTO + eng sign-off on [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) P0 perf items (#10)

**Status:** Spike expectations documented; **formal gate not signed**.

---

## 7. Open Linear issues

Project: [CityMajor Web v1 — Mesh 3D + R3F](https://linear.app/softblaze/project/citymajor-web-v1-mesh-3d-r3f-6a3285f74beb)  
Master tracker: [SB-3708](https://linear.app/softblaze/issue/SB-3708)

### Merge blockers (must close or explicitly defer)

| ID | Title | Milestone |
|----|-------|-----------|
| ~~[SB-3715](https://linear.app/softblaze/issue/SB-3715)~~ | ~~Coolify deploy — preview app~~ | **Done** — `citymajor-web` |
| [SB-3703](https://linear.app/softblaze/issue/SB-3703) | v1 perf checklist gate | M4 Launch |
| [SB-3678](https://linear.app/softblaze/issue/SB-3678) | Meshy asset pipeline (batch 1 ship) | M1 Art — **partial, 502 retries** |
| [SB-3680](https://linear.app/softblaze/issue/SB-3680) | Hero landmark pipeline | M1 Art — placeholder in scene |
| [SB-3693](https://linear.app/softblaze/issue/SB-3693) | Auth, accounts & Founder Pass entitlements | M3 Backend |
| [SB-3698](https://linear.app/softblaze/issue/SB-3698) | Stripe Founder Pass checkout | M4 Monetization |
| ~~[SB-3686](https://linear.app/softblaze/issue/SB-3686)~~ | ~~v1 save/load chunked binary + cloud~~ | M2 Sim — preview 500 fixed (`ac51180`); full cloud TBD |
| ~~[SB-3689](https://linear.app/softblaze/issue/SB-3689)~~ | ~~Household population 10K cap~~ | M2 Sim — **shipped** (`7539b37`) |
| ~~[SB-3690](https://linear.app/softblaze/issue/SB-3690)~~ | ~~Economy L1 Leontief I/O~~ | M2 Sim — **shipped** (`775eb4a`) |
| ~~[SB-3691](https://linear.app/softblaze/issue/SB-3691)~~ | ~~Utility grid & service overlays~~ | M2 Sim — **shipped** (`3066f59`) |
| ~~[SB-3719](https://linear.app/softblaze/issue/SB-3719)~~ | ~~Era landmark placeholder~~ | M1 Art — **shipped** (`ce31f4f`) |

### In progress / triage (non-blocking for PR spine merge)

| ID | Title |
|----|-------|
| [SB-3695](https://linear.app/softblaze/issue/SB-3695) | LLM proxy + daily quota |
| [SB-3700](https://linear.app/softblaze/issue/SB-3700) | Stripe cosmetic shop |
| [SB-3701](https://linear.app/softblaze/issue/SB-3701) | Founder monument & cosmetic pack |
| [SB-3705](https://linear.app/softblaze/issue/SB-3705) | QA matrix integrated vs discrete GPU |
| [SB-3706](https://linear.app/softblaze/issue/SB-3706) | R3F architecture ADR |
| [SB-3707](https://linear.app/softblaze/issue/SB-3707) | Archive Forge OpenGL renderer |

> Full backlog: Linear project filter `state:Triage` + label CityMajor Web v1.

---

## Merge decision

### Safe to merge PR #1 spine when

- [ ] Bugbot NEUTRAL or clean on HEAD (`e4d3d4f`) — **pending re-run**
- [ ] Smoke **21/21** local (22/22 with screenshot) on WASM build
- [x] Coolify preview **healthy** — `https://citymajor.apps.softblaze.net` ([SB-3715](https://linear.app/softblaze/issue/SB-3715) Done)
- [ ] Preview smoke against `BASE_URL` on `e4d3d4f+` (saves fixed — expect full pass)
- [x] Gameplay sprint shipped — roads, traffic, events, citizens, services, research UX, onboarding, landmarks
- [ ] Team acknowledges Meshy partial batch / Stripe / perf as **post-merge** or parallel tracks

### Do **not** call v1 launched until

- [ ] Meshy batch 1 + 3 heroes in scene (not placeholders)
- [ ] Perf gate signed ([SB-3703](https://linear.app/softblaze/issue/SB-3703))
- [ ] Cloud saves + auth ([SB-3686](https://linear.app/softblaze/issue/SB-3686) full cloud, [SB-3693](https://linear.app/softblaze/issue/SB-3693))
- [ ] Stripe live test path verified on preview
- [ ] CTO P0 player-loop items in [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) § v1 Launch Blockers

---

## Related docs

| Doc | Purpose |
|-----|---------|
| [DEPLOY_WEB.md](../DEPLOY_WEB.md) | Coolify + Docker + env vars |
| [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md) | Worker / snapshot contract |
| [SAVE_FORMAT_WEB.md](./SAVE_FORMAT_WEB.md) | Cloud save schema |
| [GAP_AUDIT_DESIGN_DOCS.md](./GAP_AUDIT_DESIGN_DOCS.md) | Doc contradictions resolved |
| [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) | Post-PR engineering priorities |
