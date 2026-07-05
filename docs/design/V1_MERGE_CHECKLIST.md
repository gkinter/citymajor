# CityMajor Web v1 — Merge Checklist

**Status:** Pre-merge gate (integration branch review)  
**Branch:** `feat/wasm-r3f-integration-2026-07-04`  
**PR:** [#1 — feat: CityMajor Web v1 — R3F + WASM integration spine](https://github.com/gkinter/citymajor/pull/1)  
**Scope charter:** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) · [SB-3704](https://linear.app/softblaze/issue/SB-3704)  
**Last updated:** 2026-07-05 (`4b78131` wave-4 fixes · uncommitted WT doc OK)

> PR #1 documents the v1 integration spine for review and preview deploys. **Do not merge to `main` until every **blocking** row below is checked.** Deferred items stay tracked in Linear under phase epics [SB-3726](https://linear.app/softblaze/issue/SB-3726)+.

---

## Gate summary

| Gate | Blocking? | Status (2026-07-05, `4b78131`) |
|------|-----------|--------------------------------|
| [Cursor Bugbot](#1-cursor-bugbot) | Yes | **NEUTRAL** on `4b78131` — zero Critical/Major |
| [Smoke suite](#2-smoke-suite) | Yes | **Green local** — ~35+/play + `verify:tech-unlocks`; **GHA CI red** (`--experimental-strip-types` on Node 20) |
| [Coolify preview URL](#3-coolify-preview-url) | Yes | **Healthy but stale** — FQDN 200; auto-deploy gap ([DEPLOY_WEB.md](../DEPLOY_WEB.md)); manual redeploy for `4b78131`+ |
| [Gameplay sprint](#8-gameplay-sprint) | Yes (player loop) | **Phase 3 wave 4** — law toggles, citizen/law smoke, CanvasRenderHealth, hero probe, road/service WASM |
| [Meshy assets](#4-meshy-assets) | Yes (v1 art minimum) | **Partial** — 10 Meshy GLBs committed; **3 LFS pending push** (04/05/hero); P0 batch [SB-3730](https://linear.app/softblaze/issue/SB-3730) |
| [Stripe stub vs live](#5-stripe-stub-vs-live) | Yes (monetization path) | **Stub + docs** — test-mode wiring in [DEPLOY_WEB.md](../DEPLOY_WEB.md) ([SB-3714](https://linear.app/softblaze/issue/SB-3714)) |
| [Perf gate](#6-perf-gate) | Yes | **CI stub only** — `perf-gate.yml` workflow_dispatch (`817b1a1`); sign-off still open [SB-3703](https://linear.app/softblaze/issue/SB-3703) |
| [Open Linear issues](#7-open-linear-issues) | Track | Phase epics [SB-3726](https://linear.app/softblaze/issue/SB-3726)–[SB-3730](https://linear.app/softblaze/issue/SB-3730) · master [SB-3708](https://linear.app/softblaze/issue/SB-3708) |

---

## 8. Gameplay sprint

### Phase 2 — shipped (`e4d3d4f`)

Phase 2 player-loop work landed on the integration branch (2026-07-04).

| Area | Commit range / issue | Status |
|------|----------------------|--------|
| Road paint brush | `be44700` — WASM `PlaceRoad` | **Shipped** |
| Traffic congestion heatmap | `5a02efb` | **Shipped** |
| Zone paint feedback + building pop-in | `c97cc9d` | **Shipped** |
| Approval consequences in HUD / Herald | `275b1f9` | **Shipped** |
| Era landmark placeholder (city center) | `ce31f4f` — [SB-3719](https://linear.app/softblaze/issue/SB-3719) | **Shipped** (placeholder) |
| Active WASM event markers | `d0dbfc3` | **Shipped** |
| Economy tooltips (RCI demand) | `775eb4a` — [SB-3690](https://linear.app/softblaze/issue/SB-3690) | **Shipped** |
| Service coverage overlay | `3066f59` — [SB-3691](https://linear.app/softblaze/issue/SB-3691) | **Shipped** |
| Household population + citizen render | `7539b37` — [SB-3689](https://linear.app/softblaze/issue/SB-3689) | **Shipped** (dots) |
| Research UX (panel open/close in smoke) | prior spine | **Shipped** |
| Onboarding — core loop tutorial | `c005cb8` | **Shipped** |
| GLTF catalog fallback + per-tile transforms | `a283d66` | **Shipped** |
| `/api/saves` preview 500 | `ac51180` | **Fixed** on preview |

### Phase 3 kickoff — `79ef561`

Depth + stability work after Phase 2 close. See [V1_GAMEPLAY_BUILD_PLAN.md](./V1_GAMEPLAY_BUILD_PLAN.md) § Phase 3.

| Area | Commit / issue | Status |
|------|----------------|--------|
| **Economy panel** — Leontief goods imbalances in HUD | `79ef561` — [SB-3690](https://linear.app/softblaze/issue/SB-3690) | **Shipped** (shortages/surpluses; RCI fallback when WASM export empty) |
| **Black canvas fix** — minimap → Canvas2D, EffectComposer auto-clear | `2909622` | **Shipped** — smoke asserts non-zero `readPixels` |
| Services / Traffic On·Off `aria-pressed` mutual exclusive | `630c327` | **Shipped** |
| Research / Herald button click targets | `9c65ed7` | **Shipped** |
| WASM sim init + procedural fallback hardening | `b62a7ea` | **Shipped** |
| `populationGrowthRate` WASM export | `63c3499` | **Shipped** |
| Population L2 citizen panel | [SB-3689](https://linear.app/softblaze/issue/SB-3689) | **Superseded** — see wave-2 (**Partial**) |
| LLM Herald | [SB-3695](https://linear.app/softblaze/issue/SB-3695) | **Superseded** — see wave-2 (**Partial**) |
| Hero GLTF landmark swap | [SB-3680](https://linear.app/softblaze/issue/SB-3680) | **Superseded** — see wave-2 (**Partial**) |
| Cloud save binary | [SB-3686](https://linear.app/softblaze/issue/SB-3686) | **Superseded** — see wave-2 (**Partial**) |

**HEAD (wave-4):** `4b78131` — `fix(sim): LawSystem LoadFromJson via JsonDocument for WASM`  
**Branch tip:** same + uncommitted WT (`smoke-play-checks.mjs` citizen-dot assert; LFS GLBs) — **doc-only drift OK**

### Phase 3 wave 4 — continuation (`9b82eb1` → `4b78131`, 2026-07-05)

Stability + depth after wave-2 HUD paths. Key commits on integration branch:

| Area | Commit / issue | Status |
|------|----------------|--------|
| **CitizenPanel smoke** — open/close + dot drill-down | `b46446b`, `21f49b2` — [SB-3689](https://linear.app/softblaze/issue/SB-3689) | **Shipped** — panel assertions in `smoke-play-checks.mjs`; citizen-dot render assert in uncommitted WT |
| **LawPanel toggles** — `SetLawActive` WASM export | `dcb4c4f`, `c976290`, `4b78131` | **Partial** — toggle + smoke sim API export; effect application deferred |
| **CanvasRenderHealth** — black-frame watchdog | `21f49b2`, `c39b6cb` | **Shipped** — `readPixels` inline in smoke + HUD watchdog |
| **CMJR save smoke** — Playwright cookies + JSON body | `61c5600`, `d8c57c6` | **Shipped** — save API session path; WASM round-trip skips when export unavailable |
| **Road graph + service coverage** | `6261885` | **Shipped** — WASM host connectivity + coverage exports |
| **Hero landmark probe** — FrontierCityHall GLB path | `72eda21` — [SB-3680](https://linear.app/softblaze/issue/SB-3680) | **Partial** — catalog + scene probe; `hero_frontier_city_hall.glb` on disk (LFS, uncommitted) |
| **Meshy GLBs** — `res_low_frontier_02/03` | `737425d` | **Shipped** (committed); 04/05 + hero pending LFS push |
| **CI smoke workflow** | `34d85a2` | **Red** — GHA Node 20 lacks `--experimental-strip-types` for `verify:tech-unlocks` |
| **Coolify webhook incident** | `605149b` | **Documented** — Public GitHub source blocks auto-deploy; manual `coolify deploy` required |

### Phase 3 wave 4 — shipped (`9b82eb1`, 2026-07-05)

Depth systems after Phase 3 kickoff. See [V1_GAMEPLAY_BUILD_PLAN.md](./V1_GAMEPLAY_BUILD_PLAN.md) § Phase 3.

| Area | Key files / issue | Status | % |
|------|-------------------|--------|---|
| **CitizenPanel** — L2 household drill-down | `CitizenPanel.tsx`, `CitizenButton.tsx`, `population-l2.ts` — [SB-3689](https://linear.app/softblaze/issue/SB-3689) | **Partial** | **72%** — panel + dot→tile drill-down + smoke open/close (`b46446b`); named HH list awaits `populationL2.households` WASM export |
| **CMJR save** — Layer B blob round-trip | `CmjrSave.cs`, `save-format.ts`, `save-store.ts`, `SaveLoadControls.tsx` — [SB-3686](https://linear.app/softblaze/issue/SB-3686) | **Partial** | **62%** — `export_cmjr` / load + POST/GET blob wired; save API smoke green (`61c5600`+); full SoA chunks + deterministic WASM round-trip TBD |
| **Laws** — ordinance catalog in WASM | `LawSystem.cs`, `LawSystemTests.cs`, `LawPanel.tsx`, `LawButton.tsx` — [SB-3689](https://linear.app/softblaze/issue/SB-3689) adj. | **Partial** | **55%** — catalog load + `SetLawActive` toggle (`dcb4c4f`); smoke panel open/close; effect application deferred |
| **Trade** — global market monthlies | `TradeSystem.cs`, `WasmSimHost.ProcessGlobalMarketTrade`, `ResourcesHud.tsx`, `EconomyPanel.tsx` | **Partial** | **52%** — auto import/export (`PartnerCityId=-1`); HUD trade strip + economy panel; inter-city routes deferred [SB-3728](https://linear.app/softblaze/issue/SB-3728) — **v2 architecture:** [`WEB_V1_SCOPE.md`](./WEB_V1_SCOPE.md) §12 |
| **LLM Herald** — Founder narrative path | `narrative-prompt.ts`, `/api/narrative/event`, `NewsTicker.tsx` — [SB-3695](https://linear.app/softblaze/issue/SB-3695) | **Partial** | **68%** — OpenAI/Anthropic path + quota + template fallback; requires `NARRATIVE_LLM_*` env + Founder tier; smoke in uncommitted WT |
| **Herald commands** — council option → sim | `herald-option-commands.ts`, `PlayClient.tsx`, `WasmExports.AdjustBudget` / `ApplyApprovalDelta` / `BoostResearch` | **Shipped** — budget, approval, research-boost commands reach WASM; resolves linked `approval_event` |
| **Era gates** — pop + tech checklist | `WasmEraDeriver.cs`, `EraProgressPanel.tsx`, `era-landmarks.ts` | **Shipped** — gates mirror `ResearchSystem.EraRequirements`; landmark spawns when all gates met |
| **Hero GLTF landmark** — Meshy swap path | `EraLandmarkPlaceholder.tsx` — [SB-3680](https://linear.app/softblaze/issue/SB-3680) | **Partial** — `hero_frontier_city_hall.glb` on disk (LFS, uncommitted WT); procedural box fallback when absent |
| **Tech unlock bridge** — v1 building/capability map | `base/data/tech/v1-unlock-bridge.json`, `verify-tech-unlocks.mjs`, `tech-catalog.ts` | **Shipped** — CI guard + `smoke:all` precheck; all Frontier/Industrial unlocks resolve |
| **Smoke expansions** — era quest + toolbar depth | `smoke-play-checks.mjs`, `smoke-all.mjs` | **Shipped** — citizen/law panel, canvas health, era quest, economy, services/traffic, tech precheck, CMJR API; citizen-dot render in uncommitted WT |
| **Stripe / shop probes** | `stripe.ts`, `/api/shop`, checkout + webhook routes | **Partial** — stub + test-mode wiring; shop route smoke; live webhook on preview TBD |
| **Meshy batch `--manifest`** | `batch-generate.mjs`, `.gitattributes` LFS policy | **Shipped** — manifest flag + Git LFS for GLB batches |
| **HUD theming** | `hud-tokens.css`, `hud-theme.ts` | **Shipped** — shared tokens for citizen/law/economy panels |

**Uncommitted WT** (`citymajor-web-r3f-spike`, post-`4b78131`): `smoke-play-checks.mjs` citizen-dot render assert; `res_low_frontier_04/05.glb` + `hero_frontier_city_hall.glb` (Git LFS, not pushed). **Doc-only drift in WT is OK** — commit LFS assets + smoke before next preview redeploy.

---

## 1. Cursor Bugbot

Automated code review on PR #1.

| Check | Command / link | Pass criteria |
|-------|----------------|---------------|
| Bugbot run completed | [PR #1 checks](https://github.com/gkinter/citymajor/pull/1) | Status `COMPLETED` |
| No Critical / Major findings | Re-run after new commits | Zero blocking severity |
| Latest reviewed SHA | `4b78131` | NEUTRAL — re-run after new commits |

**Current:** Bugbot **NEUTRAL** on `4b78131` (2026-07-05) — zero Critical/Major findings. Prior clean run was **NEUTRAL** on `82d8df4`. Gate **passes** for merge review.

```bash
# After push — watch PR checks or use review-bugbot skill locally
gh pr checks 1
```

---

## 2. Smoke suite

Headless Playwright suite: `pnpm smoke:all` (routes `/`, `/shop`, `/play`).

### Check inventory (~35 mandatory on `/play` + 7 route/copy + 1 optional screenshot)

| # | Suite | Assertion |
|---|-------|-----------|
| 0 | Pre-check | `verify:tech-unlocks` — all v1 tech unlocks map via bridge (`smoke-all.mjs`) |
| 1–3 | HTTP | `GET /`, `/shop`, `/play` → 200 |
| 4 | `/` | Hero copy: "Build your city across 200 years" |
| 5–6 | `/shop` | "CityMajor Shop", "Founder Pass" visible |
| 7 | `/play` | COOP `same-origin` + COEP `require-corp` |
| 8–9 | `/play` | Diagnostics HUD + CityMajor wordmark |
| 10–11 | `/play` | WebGL canvas mounted + Three.js context |
| 12 | `/play` | Main canvas `readPixels` non-zero (black-frame guard, `2909622`) |
| 13–14 | API | `GET /api/saves` envelope; malformed `POST` → 400 |
| 15–16 | HUD | Research panel open / close |
| 17–18 | HUD | Herald panel loads headline / closes |
| 19 | HUD | Era Quest panel — population + tech-count gates (`assertEraQuestPanel`, wave-2) |
| 20–21 | HUD | Economy panel shortages/surpluses (or RCI fallback) / closes (`79ef561`) |
| 22–23 | HUD | Citizen panel open / close (`b46446b`) |
| 24–25 | HUD | Law panel open / close (`dcb4c4f`) |
| 26 | HUD | Canvas render health — non-zero `readPixels` watchdog (`c39b6cb`) |
| 27–31 | HUD | Services toolbar boot off → On/Health → Police → Fire → Off (when mounted; skip OK) |
| 32–33 | HUD | Traffic toggle boot off + On/Off mutual exclusive (when mounted; skip OK) |
| 34 | `/play` | Sim source reported (`WASM sim` or `procedural`) |
| 35 | `/play` | Buildings count > 0 in HUD |
| 36 | `/play` | FPS reported (warn-only if `—` in headless) |
| 37 | `/play` | No unexpected console errors |
| 38 | Optional | Screenshot saved when `SCREENSHOT=1` |

### Run locally (full WASM path)

```bash
cd /path/to/citymajor-web-r3f-spike
unset NODE_OPTIONS
pnpm build:wasm && pnpm dev          # terminal 1
WASM_EXPECTED=1 pnpm smoke:all       # terminal 2 — expect ~35+ pass on /play (+screenshot +optional overlays)
```

### Run tech-unlocks guard alone

```bash
cd /path/to/citymajor-web-r3f-spike/web
pnpm verify:tech-unlocks
```

### Run against Coolify preview

```bash
BASE_URL=https://citymajor.apps.softblaze.net \
  WASM_EXPECTED=1 pnpm smoke:all
```

> **Update (`4b78131`):** Citizen + law panel smoke #22–25; canvas health #26. **Update (`2909622`):** Black WebGL canvas regression guarded by assertion #12 / #26. **Update (`79ef561`):** Economy panel assertions #20–21. **Update (wave-2):** Era Quest gate assertion #19; `verify:tech-unlocks` precheck #0.

**Pass:** Console ends with `[smoke-all] All checks passed.` — **~38/38** with `SCREENSHOT=1`, **~37/37** without (overlay toolbar checks skip when UI absent).

**Local (2026-07-05):** **Green** on `4b78131` with `pnpm build:wasm && pnpm dev` + `WASM_EXPECTED=1 pnpm smoke:all`.

**GHA CI (`34d85a2`):** **Red** — `verify:tech-unlocks` precheck fails: Node 20 runner rejects `--experimental-strip-types`. Fix: compile `verify-tech-unlocks.mts` to `.mjs` or bump workflow Node ≥ 22. Not a local regression.

**Deferred smoke (backlog):** Citizen-dot render assert (uncommitted WT); deterministic CMJR WASM round-trip; LLM Herald headline when keys set.

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
| `/play` | HUD shows **Data: WASM sim**; WebGL canvas lit (not black) |
| `/api/saves` | Envelope OK on preview (`ac51180`+) |

**Linear:** [SB-3715 — M2: Coolify deploy](https://linear.app/softblaze/issue/SB-3715/m2-coolify-deploy-preview-app-for-citymajor-web) — **Done**

**Status:** Preview **healthy** at canonical FQDN (HTTP 200, WASM assets present) but **stale vs `4b78131`**.

### Preview deploy gaps

| Gap | Detail | Action |
|-----|--------|--------|
| **Auto-deploy broken** | App bound to Public GitHub (`source_id = 0`); pushes do not enqueue builds (`is_webhook = false`) | Follow [DEPLOY_WEB.md § Fix](../DEPLOY_WEB.md#fix-required-for-push--deploy) — wire GitHub App source |
| **SHA lag** | Wave-4 commits (`9b82eb1`→`4b78131`) not on preview until manual deploy | `coolify deploy citymajor-web --force` after LFS push |
| **LFS on preview** | `res_low_frontier_04/05` + `hero_frontier_city_hall` uncommitted / not pushed | `git lfs push origin feat/wasm-r3f-integration-2026-07-04` then redeploy |
| **Preview smoke** | `BASE_URL` smoke not re-run post-`4b78131` | Run §2 against FQDN after redeploy |

---

## 4. Meshy assets

Art pipeline: [MESHY_ASSET_PIPELINE.md](./MESHY_ASSET_PIPELINE.md) · [MESHY_ASSET_CATALOG.md](./MESHY_ASSET_CATALOG.md) · [BUILDING_ARCHETYPE_3D.md](./BUILDING_ARCHETYPE_3D.md) · [SB-3678](https://linear.app/softblaze/issue/SB-3678) · epic [SB-3730](https://linear.app/softblaze/issue/SB-3730)

| Milestone | Target | Current (`4b78131`) |
|-----------|--------|---------------------|
| Batch script + seed manifest | `scripts/meshy/batch-generate.mjs` | **Done** |
| **Asset catalog + batch manifests** | `MESHY_ASSET_CATALOG.md`, `generate-manifest-batches.mjs` | **Done** (`79ef561`) — 500-key taxonomy, P0–P3 batches [SB-3739](https://linear.app/softblaze/issue/SB-3739)–[SB-3747](https://linear.app/softblaze/issue/SB-3747) |
| MCP gateway path | `6095f2f` — `MESHY_USE_MCP=1` | **Done** — Softblaze Meshy MCP when API key unset |
| `SHIPPED_GLTF_KEYS` registry | `web/lib/gltf-catalog.ts` | **Done** — 20 keys (one per category × era sample) |
| v1 minimum GLBs | ~80–120 core archetypes (Frontier + Industrial) | **Partial** — **10 Meshy GLBs** committed at `4b78131` (+ 3 LFS pending push); 12 procedural placeholders |
| Hero landmarks | 8 pilot heroes | **Partial** — `hero_frontier_city_hall.glb` on disk (LFS, uncommitted); era-gate spawn + GLB probe ([SB-3719](https://linear.app/softblaze/issue/SB-3719)) |
| On-disk assets | `web/public/assets/gltf/{era}/*.glb` | **Partial** — 25 GLBs total (13 Meshy-quality ≥8 MB + 12 procedural); P0 batch [SB-3740](https://linear.app/softblaze/issue/SB-3740) |
| Runtime fallback | InstancedMesh + procedural modules | **Works** — merge OK for spine, not for art-complete v1 |

### Meshy GLBs on disk (production-quality)

| Key | Era | Source |
|-----|-----|--------|
| `com_frontier_00` | frontier | Meshy MCP (`3c5a851`) |
| `ind_frontier_00` | frontier | Meshy MCP (`f93b855`) |
| `res_low_frontier_00` | frontier | Meshy MCP |
| `res_low_frontier_02` | frontier | Meshy MCP (`737425d`) |
| `res_low_frontier_03` | frontier | Meshy MCP (`737425d`) |
| `ind_industrial_00` | industrial | Meshy MCP (`79ef561`) |
| `res_high_industrial_00` | industrial | Meshy MCP (`86dd2ff`) |
| `res_low_industrial_00` | industrial | Meshy MCP (`3076c29`) |

### LFS pending push (not on remote / preview)

| Key | Era | Status |
|-----|-----|--------|
| `res_low_frontier_04` | frontier | On disk; Git LFS tracked; **not pushed** |
| `res_low_frontier_05` | frontier | On disk; Git LFS tracked; **not pushed** |
| `hero_frontier_city_hall` | heroes | On disk; **untracked**; commit + LFS push before preview |

> `com_industrial_00` remains procedural placeholder (`075941a`). Production Meshy GLB deferred to [SB-3740](https://linear.app/softblaze/issue/SB-3740).

Remaining `SHIPPED_GLTF_KEYS` resolve to small procedural placeholders until batch run completes.

### Pre-merge minimum (CTO roadmap P0 #9, #11)

- [x] MCP gateway path for batch when `MESHY_API_KEY` unset (`6095f2f`)
- [x] Canonical catalog + manifest batch JSONs (`79ef561`, [SB-3730](https://linear.app/softblaze/issue/SB-3730))
- [x] `SHIPPED_GLTF_KEYS` aligned to catalog samples (`79ef561`)
- [x] Git LFS policy for GLB batches (`.gitattributes`)
- [ ] Push pending LFS objects (`git lfs push`) — 04/05 committed, hero uncommitted
- [ ] Run Meshy batch for Frontier + Industrial core set (~80 GLBs) — [SB-3740](https://linear.app/softblaze/issue/SB-3740)
- [ ] Post-process: bottom-center pivot, 1-story unit scale
- [ ] 3+ hero landmarks placed in scene at era gates — **partial** (wave-2): GLB probe + procedural fallback; Meshy heroes pending [SB-3741](https://linear.app/softblaze/issue/SB-3741)
- [ ] CDN / immutable cache strategy — [SB-3682](https://linear.app/softblaze/issue/SB-3682)

```bash
# Validate manifest before batch spend
node scripts/meshy/validate-manifest.mjs
# Generate batch manifests from catalog taxonomy
node scripts/meshy/generate-manifest-batches.mjs
# Batch (requires MESHY_API_KEY or MESHY_USE_MCP=1)
MESHY_USE_MCP=1 node scripts/meshy/batch-generate.mjs --manifest scripts/meshy/manifest-batch-v1-frontier-industrial.json
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

**Linear:** [SB-3698](https://linear.app/softblaze/issue/SB-3698) (checkout) · [SB-3693](https://linear.app/softblaze/issue/SB-3693) (auth + entitlements) · [SB-3714](https://linear.app/softblaze/issue/SB-3714) (test-mode docs)

**Docs:** Stripe env vars + webhook FQDN — quick ops [`STRIPE_COOLIFY_SETUP.md`](../STRIPE_COOLIFY_SETUP.md); full walkthrough [`DEPLOY_WEB.md`](../DEPLOY_WEB.md) (`2ce1bb1`).

**Status:** Code stub ships in PR #1; Coolify test-mode wiring documented. **Live Stripe optional for integration merge**, **required for launch**.

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

### CI automation (`817b1a1`)

Manual-dispatch workflow `.github/workflows/perf-gate.yml` runs `PERF_GATE=1 pnpm smoke:all` on `workflow_dispatch`. Software-renderer GHA runners auto-skip when max FPS < 15 unless `PERF_GATE_STRICT=1`. **Meaningful ≥30 FPS sign-off requires GPU host** — not blocking PR until self-hosted runner exists.

**Status:** Spike expectations documented; **formal gate not signed**; CI stub landed for future GPU runner.

---

## 7. Open Linear issues

Project: [CityMajor Web v1 — Mesh 3D + R3F](https://linear.app/softblaze/project/citymajor-web-v1-mesh-3d-r3f-6a3285f74beb)  
Master tracker: [SB-3708](https://linear.app/softblaze/issue/SB-3708) — links [canonical roadmap doc](https://linear.app/softblaze/document/citymajor-canonical-roadmap-v1-full-vision-eb7277adafd0)

### Phase epics (SB-3726+)

| Epic | Phase | Scope |
|------|-------|-------|
| [SB-3726](https://linear.app/softblaze/issue/SB-3726) | v1 Launch | PR #1 spine merge + preview deploy + smoke/Bugbot gates |
| [SB-3727](https://linear.app/softblaze/issue/SB-3727) | v1.5 Depth | Phase 3 remainder — `populationL2` WASM export, LLM Herald smoke, CMJR SoA chunks |
| [SB-3728](https://linear.app/softblaze/issue/SB-3728) | v2 Trade & MP | Co-op, open region, headless sim server — **inter-city trade architecture:** [`WEB_V1_SCOPE.md`](./WEB_V1_SCOPE.md) §12 |
| [SB-3729](https://linear.app/softblaze/issue/SB-3729) | Full vision | MMO-lite regional scale |
| [SB-3730](https://linear.app/softblaze/issue/SB-3730) | Meshy batches | Catalog manifests [SB-3739](https://linear.app/softblaze/issue/SB-3739)–[SB-3747](https://linear.app/softblaze/issue/SB-3747) |

### Merge blockers (must close or explicitly defer)

| ID | Title | Milestone |
|----|-------|-----------|
| ~~[SB-3715](https://linear.app/softblaze/issue/SB-3715)~~ | ~~Coolify deploy — preview app~~ | **Done** — `citymajor-web` |
| [SB-3703](https://linear.app/softblaze/issue/SB-3703) | v1 perf checklist gate | M4 Launch — CI stub only |
| [SB-3678](https://linear.app/softblaze/issue/SB-3678) | Meshy asset pipeline (batch 1 ship) | M1 Art — catalog done; batch run pending [SB-3740](https://linear.app/softblaze/issue/SB-3740) |
| [SB-3680](https://linear.app/softblaze/issue/SB-3680) | Hero landmark pipeline | M1 Art — placeholder in scene |
| [SB-3693](https://linear.app/softblaze/issue/SB-3693) | Auth, accounts & Founder Pass entitlements | M3 Backend |
| [SB-3698](https://linear.app/softblaze/issue/SB-3698) | Stripe Founder Pass checkout | M4 Monetization |
| ~~[SB-3686](https://linear.app/softblaze/issue/SB-3686)~~ | ~~v1 save/load chunked binary + cloud~~ | M2 Sim — **CMJR partial** (`9b82eb1`): `export_cmjr` + blob API; interim JSON chunk; SoA + smoke TBD |
| ~~[SB-3689](https://linear.app/softblaze/issue/SB-3689)~~ | ~~Household population 10K cap~~ | M2 Sim — **dots shipped** (`7539b37`); **L2 panel partial** (`9b82eb1`) — UI ready, WASM `populationL2` export TBD |
| [SB-3690](https://linear.app/softblaze/issue/SB-3690) | Leontief economy + trade | M2 Sim — **panel + trade HUD shipped** (`9b82eb1`); full I/O matrix + zoning hints TBD |
| ~~[SB-3691](https://linear.app/softblaze/issue/SB-3691)~~ | ~~Utility grid & service overlays~~ | M2 Sim — **shipped** (`3066f59`) |
| ~~[SB-3719](https://linear.app/softblaze/issue/SB-3719)~~ | ~~Era landmark placeholder~~ | M1 Art — **shipped** (`ce31f4f`) |

### In progress / triage (non-blocking for PR spine merge)

| ID | Title |
|----|-------|
| [SB-3695](https://linear.app/softblaze/issue/SB-3695) | LLM proxy + daily quota — **partial** (`9b82eb1`): code path shipped; preview keys + smoke TBD |
| [SB-3700](https://linear.app/softblaze/issue/SB-3700) | Stripe cosmetic shop |
| [SB-3701](https://linear.app/softblaze/issue/SB-3701) | Founder monument & cosmetic pack |
| [SB-3705](https://linear.app/softblaze/issue/SB-3705) | QA matrix integrated vs discrete GPU |
| [SB-3706](https://linear.app/softblaze/issue/SB-3706) | R3F architecture ADR |
| [SB-3707](https://linear.app/softblaze/issue/SB-3707) | Archive Forge OpenGL renderer |
| [SB-3739](https://linear.app/softblaze/issue/SB-3739) | Meshy batch `v1-core` (20 sample keys) |
| [SB-3740](https://linear.app/softblaze/issue/SB-3740) | Meshy batch `v1-frontier-industrial-80` |

> Full backlog: Linear project filter `state:Triage` + label CityMajor Web v1.

---

## Merge decision

### Safe to merge PR #1 spine when

- [x] Bugbot NEUTRAL or clean on HEAD (`4b78131`) — **NEUTRAL**, zero Critical/Major
- [x] Smoke **~37/37** local (~38/38 with screenshot) on WASM build + `verify:tech-unlocks` pass
- [x] Coolify preview **healthy** — `https://citymajor.apps.softblaze.net` ([SB-3715](https://linear.app/softblaze/issue/SB-3715) Done)
- [ ] Preview redeployed to `4b78131`+ and `BASE_URL` smoke green (blocked by auto-deploy gap + LFS push)
- [x] Phase 2 gameplay sprint shipped — roads, traffic, events, citizens, services, research UX, onboarding, landmarks
- [x] Phase 3 kickoff stability — black canvas fix, economy panel, overlay a11y (`79ef561`)
- [x] Phase 3 wave 4 — herald commands, era gates, tech bridge, CMJR/citizen/law/trade/LLM paths + smoke (`4b78131`)
- [ ] GHA smoke workflow green (Node 20 `--experimental-strip-types` fix)
- [ ] Team acknowledges Meshy partial batch / Stripe / perf / preview deploy gap as **post-merge** or parallel tracks ([SB-3726](https://linear.app/softblaze/issue/SB-3726))

### Do **not** call v1 launched until

- [ ] Meshy batch 1 + 3 heroes in scene (not placeholders) — [SB-3730](https://linear.app/softblaze/issue/SB-3730)
- [ ] Perf gate signed ([SB-3703](https://linear.app/softblaze/issue/SB-3703))
- [ ] Cloud saves + auth ([SB-3686](https://linear.app/softblaze/issue/SB-3686) full CMJR SoA + multi-device, [SB-3693](https://linear.app/softblaze/issue/SB-3693))
- [ ] Stripe live test path verified on preview
- [ ] Phase 3 depth complete — `populationL2` WASM export, LLM Herald on preview, hero GLTF on disk ([SB-3727](https://linear.app/softblaze/issue/SB-3727))
- [ ] Laws toggle + effect application in sim (panel counts shipped `9b82eb1`)
- [ ] CTO P0 player-loop items in [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) § v1 Launch Blockers

---

## Related docs

| Doc | Purpose |
|-----|---------|
| [DEPLOY_WEB.md](../DEPLOY_WEB.md) | Coolify + Docker + env vars |
| [MESHY_ASSET_CATALOG.md](./MESHY_ASSET_CATALOG.md) | 500-key taxonomy + batch manifests |
| [V1_GAMEPLAY_BUILD_PLAN.md](./V1_GAMEPLAY_BUILD_PLAN.md) | Phase 2 shipped · Phase 3 active · wave-4 `4b78131` |
| [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md) | Worker / snapshot contract |
| [SAVE_FORMAT_WEB.md](./SAVE_FORMAT_WEB.md) | CMJR cloud save schema (§3 — wave-2 interim JSON chunk) |
| [GAP_AUDIT_DESIGN_DOCS.md](./GAP_AUDIT_DESIGN_DOCS.md) | Doc contradictions resolved |
| [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md) | Post-PR engineering priorities |
