# PR #1 body draft — CityMajor Web v1 integration spine

**Target PR:** [#1 — feat: CityMajor Web v1 — R3F + WASM integration spine](https://github.com/gkinter/citymajor/pull/1)  
**Branch:** `feat/wasm-r3f-integration-2026-07-04`  
**Integration HEAD:** `8cd8691` (wave 16 HUD + WASM boot fix merged)  
**Checklist:** [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) · [WAVE16_MERGE_MAP.md](./WAVE16_MERGE_MAP.md)  
**Preview:** https://citymajor.apps.softblaze.net/play

> Copy everything below the `---` into the GitHub PR description when updating PR #1.

---

## Gameplay sprint status (`8cd8691`)

**Preview:** https://citymajor.apps.softblaze.net/play

**Smoke:** `WASM_EXPECTED=1 pnpm smoke:all` — **46/46** deploy parity (local WASM + Coolify FQDN + GHA `ci-smoke`); `verify:tech-unlocks` precheck green. Era quest aria-label restored (`8cd8691`); SEO route probes (`/sitemap.xml`, `/robots.txt`) pending merge from `feat/smoke-probes-bundle-2026-07-05`.

**WASM boot:** `848e45c` restores `WasmSimHost.PlaceBuilding` + Docker `BUILD_WASM=1` gate — `dotnet publish` no longer ships empty `web/public/dotnet/` (404 on `blazor.boot.json` → procedural fallback). Triage: [`AGENTS.md`](../AGENTS.md) § WASM boot triage.

### Wave 16 — HUD polish (2026-07-05)

Play chrome + slide-panel theming after wave-15 build loop. Five merge branches (**a → e**), tip `3428d81`:

| Deliverable | What shipped |
|-------------|--------------|
| **Zone overlay + transport** | Distinct tier colors/legend; `TransportToolbar` bus/rail stubs in road mode; `?empty=1` empty-city start |
| **Demand + budget HUDs** | `DemandOverlay` RCI strip; `BudgetPanel` + `PopulationPanel` top-right; research unlock toast shows zones/buildings |
| **Services build tabs** | Fire / Police / Health tabs in `BuildToolbar`; tile hover tooltip; minimap + help overlay scaffolds |
| **Core meters** | `ApprovalMeter`, `HappinessMeter`, time controls (4× speed), save indicator dot, era/quality preset polish |
| **Camera + debug** | HUD Home camera reset; `?debug=chunks` loaded-chunk count; FPS tier color-coding |
| **Panel polish** | Research / Citizen / Herald / Law slide panels aligned to `hud-theme`; `TradeOverlay` toggle scaffold |
| **Home + economy** | Hero CTA copy for `/play`; `EconomyPanel` treasury + tax rate summary row |

**Merge stack:** `848e45c` (WASM boot) → wave **a→e** (`7d459fe` … `3428d81`) → integration (`8cd8691`). Full SHAs: [WAVE16_MERGE_MAP.md](./WAVE16_MERGE_MAP.md).

### Wave 15 — build loop (2026-07-05)

Player-facing **paint + plop** tools on `/play`:

| Deliverable | What shipped |
|-------------|--------------|
| **7 zone tiers** | `ZoningToolbar` paints R-low, R-high, C, I, Office, Mixed, Ag — tech-gated via research (`T029`, `T086`, `T134`, `T047`); locked tiers show required tech name |
| **Build menu** | `BuildToolbar` with Civic / Education / Safety / Utilities / Parks tabs; entries from `build-catalog.ts` filtered by `unlockedTechIds` |
| **WASM `place_building`** | `WasmExports.PlaceBuilding` + worker command; tile click in plop mode places civic TypeId; smoke probe in `smoke-play-checks.mjs` |
| **Education toolbar** | `EducationToolbar` — frontier **Schoolhouse** (503) + industrial **Elementary** (524); dedicated `buildMode: "education"` |
| **Road types** | `RoadTypeToolbar` — dirt / paved / highway tiers before `place_road` paint |
| **PlayClient modes** | Mutual-exclusive `zone \| road \| plop \| education` toolbars wired to `CityCanvas` |

**Merge stack:** `feat/wave15-build-menu-merge-2026-07-05` → integration (`5a0b08e`).

### Prior waves (retained)

- **Phase 2** — road paint, traffic heatmap, zone feedback, approval HUD, citizens, services overlay, onboarding, GLTF fallback.
- **Phase 3 wave 4** — citizen/law panels, economy Leontief HUD, CMJR save API, Herald LLM + council commands, era gates, **7 hero GLBs**, `place_road` smoke, CanvasRenderHealth, session secret hardening.

**Remaining gates (v1 launch NO-GO — not spine blockers):**

| Gate | Status |
|------|--------|
| **Meshy** | **Partial** — 49 GLBs on disk (7 heroes); P0 batch ~25/108 — [SB-3730](https://linear.app/softblaze/issue/SB-3730) |
| **Stripe live** | Mock on preview — live test-mode path open — [SB-3698](https://linear.app/softblaze/issue/SB-3698) |
| **Perf GPU sign-off** | Headless CI fails on software renderer (expected); GPU soak open — [SB-3703](https://linear.app/softblaze/issue/SB-3703) |

---

## Summary

**CityMajor Web v1** ships the integration spine: **React Three Fiber** play client driven by C# sim compiled to **browser WASM**, plus HUD/live-services stubs and design-docs pivot for the web mesh 3D direction.

### R3F client (`/play`)

- Next.js 16: `CityCanvas` / `CityScene`, instanced buildings, chunked terrain, LOD L0–L3, GLTF modules + procedural fallback.
- **Wave 15:** tiered **ZoningToolbar** (7 zones), **BuildToolbar** (civic ploppables), **EducationToolbar**, **RoadTypeToolbar**, `PlayClient` build modes.
- **Wave 16:** demand/budget/population HUDs, approval/happiness meters, time controls, minimap/help scaffolds, panel theming, FPS tier diagnostics.
- Crisis modal, era progress, onboarding overlay, cyan glass HUD design system.

### WASM sim bridge

- `Forge.SimWasm` → `web/public/dotnet/`; worker at 8 Hz sim / throttled snapshots.
- Commands: `place_zone`, `place_road`, **`place_building`**, law toggles, herald budget/approval/research.
- Economy L1, BPR-lite traffic, research-gated era progression, RCI/approval/budget exports.
- **Boot fix (`848e45c`):** `PlaceBuilding` restored on `WasmSimHost`; Docker wasm stage fails when `blazor.boot.json` missing under `BUILD_WASM=1`.

### Herald, economy, citizens

- Herald panel + quota-gated `/api/narrative/event`; council options → WASM commands.
- Economy panel (Leontief shortages/surpluses + treasury summary), citizen/law panels, service coverage overlay.

### Stripe stub (Founder Pass)

- `/shop` mock checkout; webhook route; entitlements API — live keys optional for spine merge.

### Docs & pipeline

- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md), [WASM_SIM_BRIDGE.md](./WASM_SIM_BRIDGE.md), [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md), [WAVE16_MERGE_MAP.md](./WAVE16_MERGE_MAP.md), Meshy pipeline, Coolify deploy runbooks.

## Architecture

```
Forge.SimCore → Forge.SimWasm (dotnet publish) → web/public/dotnet/
                                              ↘ sim-worker → sim-bridge → R3F CityScene
web/packages/sim-types ─────────────────────────→ BuildingInstances / city-data
Next API stubs ─────────────────────────────────→ saves / entitlements / narrative / Stripe
PlayClient toolbars ────────────────────────────→ place_zone | place_road | place_building
Wave 16 HUD stack ──────────────────────────────→ demand | budget | approval | time | panels
```

## Test plan

- [x] `WASM_EXPECTED=1 pnpm smoke:all` — **46/46** on integration HEAD (`8cd8691`)
- [x] `pnpm verify:tech-unlocks` — zone + build content keys resolve
- [x] Coolify preview — https://citymajor.apps.softblaze.net/play
- [x] WASM boot — `curl -sf $FQDN/dotnet/_framework/blazor.boot.json` returns 200 (not procedural fallback)
- [ ] `pnpm build:wasm && pnpm dev` — paint all 7 zone tiers; plop civic via build menu; place education schoolhouse
- [ ] Wave 16 HUD — demand strip, budget/pop meters, approval/happiness, time controls, help overlay
- [ ] WASM `place_building` — select Build tab entry, click empty tile, verify building count increases
- [ ] Locked zone tier shows tech name until research unlocks
- [ ] **Meshy gate** — P0 batch run — [SB-3740](https://linear.app/softblaze/issue/SB-3740)
- [ ] **Stripe live gate** — test-mode Checkout + webhook on preview FQDN
- [ ] **Perf GPU gate** — soak per `web/PERF.md` on target GPU tiers

## Note

PR #1 documents the v1 integration spine for review and preview deploys. **Spine merge gates pass** per [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md). v1 **launch** still requires Meshy P0 batch, Stripe live, and GPU perf sign-off.
