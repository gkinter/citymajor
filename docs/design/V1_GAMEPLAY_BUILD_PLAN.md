# CityMajor Web v1 — Phase 2 Gameplay Build Plan

**Status:** Active — Phase 2  
**Date:** 2026-07-04 (revised)  
**Branch:** `feat/wasm-r3f-integration-2026-07-04` · Worktree: `citymajor-web-r3f-spike`  
**Epic:** [SB-3654 — M2 Gameplay v1 Systems](https://linear.app/softblaze/issue/SB-3654/epic-m2-gameplay-v1-systems)  
**Scope charter:** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) · [SB-3704](https://linear.app/softblaze/issue/SB-3704)  
**Source audits:** [GAMEPLAY_LOOP_IMPROVEMENTS.md](./GAMEPLAY_LOOP_IMPROVEMENTS.md) · [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md)

> **Intent:** Phase 1 turned `/play` from a **3D zoning sandbox** into a **mayor sim** (RCI HUD, Herald, onboarding, era quests, research, news ticker, Meshy GLBs). **Phase 2** makes the city *feel alive on the map* — population dots, traffic heat, service coverage, roads, economy tooltips, and era landmarks — by exporting more of what WASM already computes into R3F overlays and toolbar UX.

---

## 1. Problem statement (post–Phase 1)

| Layer | State (integration worktree, 2026-07-04) | Player impact |
|-------|------------------------------------------|---------------|
| **WASM sim** | Economy, zone growth, 80 events, politics, budget, era derivation, traffic lite, service coverage all tick | Partially visible |
| **Web `/play` — Phase 1 ✅** | RCI bars, approval/budget HUD, era checklist, research panel, Herald auto-open + council stubs, 5-step onboarding, news ticker, Meshy GLB buckets | Mayor loop works |
| **Web `/play` — Phase 2 🚧** | Scaffolding landed; polish + WASM wire + smoke gates in flight | City still feels like buildings, not citizens |
| **Map readability** | No population dots; traffic/services hidden by default | Hard to diagnose congestion or coverage gaps |
| **Build tools** | Road paint in canvas; economy hints on zoning toolbar | Road tool needs full UX; tooltips need richer copy |
| **Landmarks** | `EraLandmarkPlaceholder` when gates met | Placeholder mesh until hero GLBs + quest fanfare ship |

**Verdict:** Phase 1 closed the *mayor pulse* loop. Phase 2 closes the *city legibility* loop — players should *see* growth, congestion, and service gaps before opening panels.

**v1 scope lock (WEB_V1_SCOPE):** Single-player mayor, Frontier → Industrial arc, ~60–90 min session, deep sim at reduced scale (256×256, ~5K buildings, ~10K HH). Phase 2 stays inside this arc.

---

## 2. Phase 1 recap — SHIPPED on integration

The original 2-week plan (orphan branch `feat/gameplay-build-plan-2026-07-04`, commit `b4a1b37`) is **largely complete** on `feat/wasm-r3f-integration-2026-07-04`:

### Week 1 — "Mayor pulse" ✅

| Work package | Status | Key files |
|--------------|--------|-----------|
| Snapshot schema v2 (RCI, approval, budget, `eraProgress`) | ✅ Shipped | `sim-bridge.ts`, `sim-worker.ts`, `WASM_SIM_BRIDGE.md` |
| RCI demand bars | ✅ Shipped | `ResourcesHud.tsx`, `zoning-economy.ts` |
| Approval + budget strip | ✅ Shipped | `ResourcesHud.tsx`, `ApprovalMoodOverlay.tsx` |
| Era progress gate checklist | ✅ Shipped | `EraProgressPanel.tsx` |
| Research HUD + tech panel | ✅ Shipped | `ResearchPanel.tsx`, `tech-catalog.ts`, `tech-unlocks.ts` |

### Week 2 — "Herald + onboarding" ✅

| Work package | Status | Key files |
|--------------|--------|-----------|
| `activeEvents[]` export | ✅ Shipped | `sim-bridge.ts`, `event-catalog.ts` |
| Herald ↔ `events.json` templates | ✅ Shipped | `HeraldPanel.tsx`, `/api/narrative/event` |
| Auto-Herald on event spawn | ✅ Shipped | `PlayClient.tsx`, `HudToast.tsx`, `EventMarkers.tsx` |
| Council option → sim command stub | ✅ Shipped | `handleHeraldOptionSelect` in `PlayClient.tsx` |
| 5-step onboarding overlay | ✅ Shipped | `OnboardingOverlay.tsx` |
| Era transition fanfare | ✅ Shipped | `openEraHerald`, special edition Herald |
| News ticker | ✅ Shipped | `NewsTicker.tsx` |
| Meshy GLB building buckets | ✅ Shipped | `GltfBuildingBucket.tsx`, `building-spawn.ts` |

**Phase 1 exit criteria:** met for manual QA on integration branch. Smoke extensions for RCI/Herald remain incremental hardening, not blockers for Phase 2.

---

## 3. Phase 2 sprint goal (~2 weeks)

**Exit criteria:** A player mid-session on `/play` can:

1. **See citizens** as warm dots clustered on residential buildings (scaled to household count).
2. **Toggle traffic heatmap** — green→red congestion overlay on road tiles from `WasmTrafficLite`.
3. **Toggle service coverage** — health / police / fire heat per tile + city-wide % strip.
4. **Paint roads** from the zoning toolbar with brush size, optimistic overlay, and WASM `place_road` dispatch.
5. **Read economy tooltips** on R/C/I tools — demand hint, high-demand highlight, surplus warning.
6. **Spot era landmark** placeholder when quest gates are met; fanfare ties to Herald special edition.

**Not in Phase 2:** Population L2 named citizens (SB-3689), Leontief I/O depth (SB-3690), cloud save binary (SB-3686), auth/Stripe (SB-3693/3698), LLM Herald (SB-3695), full Meshy hero GLB art pass (SB-3680 polish), perf sign-off (SB-3703).

---

## 4. Linear mapping — Phase 2 work packages

### Epic anchor (unchanged)

| Issue | Role |
|-------|------|
| [SB-3654](https://linear.app/softblaze/issue/SB-3654) | Parent epic — M2 Gameplay v1 Systems |
| [SB-3708](https://linear.app/softblaze/issue/SB-3708) | Master tracker — link Phase 2 section in comment |
| [SB-3691](https://linear.app/softblaze/issue/SB-3691) | **Service overlays** — promoted from deferred |
| [SB-3680](https://linear.app/softblaze/issue/SB-3680) | **Hero landmarks** — placeholder → GLB swap path |

### Phase 2 priorities (active agent work)

| Priority | Work package | Linear | Component / lib | Effort |
|:--------:|--------------|--------|-----------------|:------:|
| P0 | **Population dots** — instanced warm dots on residential footprints; cap 12K; pulse with growth | [SB-3689](https://linear.app/softblaze/issue/SB-3689) (visual slice) | `CitizenDots.tsx`, `population-growth.ts` | 2 d |
| P0 | **Traffic heatmap** — `TrafficOverlay` + toggle; density 0–1 from snapshot | [SB-3654](https://linear.app/softblaze/issue/SB-3654) child | `TrafficOverlay.tsx`, `TrafficOverlayToggle.tsx` | 1.5 d |
| P0 | **Services overlay** — per-tile health/police/fire heat + `ServicesToolbar` | [SB-3691](https://linear.app/softblaze/issue/SB-3691) | `ServiceCoverageOverlay.tsx`, `ServicesToolbar.tsx` | 2 d |
| P0 | **Road tool** — toolbar enable, brush paint, optimistic `roads[]`, WASM `place_road` | [SB-3654](https://linear.app/softblaze/issue/SB-3654) child | `zoning.ts`, `CityCanvas.tsx`, `sim-worker.ts` | 2 d |
| P1 | **Economy tooltips** — `zoningDemandHint`, high-demand CSS, RCI-driven toolbar titles | [SB-3688](https://linear.app/softblaze/issue/SB-3688) ext | `zoning-economy.ts`, `ZoningToolbar.tsx`, `hud-tokens.css` | 1 d |
| P1 | **Era landmarks** — gate-ready placeholder mesh + label; swap hook for Meshy GLB | [SB-3680](https://linear.app/softblaze/issue/SB-3680) | `EraLandmarkPlaceholder.tsx`, `era-landmarks.ts` | 1.5 d |
| P2 | **Paint feedback** — optional sound/haptics on zone/road paint | [SB-3654](https://linear.app/softblaze/issue/SB-3654) child | `paint-feedback.ts`, `ZoningToolbar.tsx` | 0.5 d |
| P2 | **Event markers** — 3D pins for active events; click → Herald | SB-3697 ext | `EventMarkers.tsx` | 1 d |

**Phase 2 dependency graph:**

```
sim-bridge (traffic[], serviceCoverage[]) ──┬──► TrafficOverlay + toggle
                                             ├──► ServiceCoverageOverlay + toolbar
                                             └──► ResourcesHud coverage % strip

WASM roads + place_road ──► Road tool UX + RoadOverlay

householdCount + buildings ──► CitizenDots instancing

eraProgress gates met ──► EraLandmarkPlaceholder ──► (later) Meshy GLB swap
```

**Snapshot fields (Phase 2 extensions):**

```typescript
traffic?: Array<{ tileX: number; tileZ: number; density: number }>;
serviceCoverage?: Array<{ tileX: number; tileZ: number; health: number; police: number; fire: number }>;
roads?: Array<{ tileX: number; tileZ: number; roadFlags: number }>;
// householdCount already in resources; CitizenDots derives dot budget from it
```

### Explicitly deferred (post–Phase 2)

| Issue | Why deferred | When |
|-------|--------------|------|
| [SB-3689](https://linear.app/softblaze/issue/SB-3689) full | Named citizens, commutes, happiness per HH — sim depth beyond dots | Sprint +2 |
| [SB-3690](https://linear.app/softblaze/issue/SB-3690) | Leontief I/O — economy tooltips use RCI only for now | Sprint +3 |
| [SB-3686](https://linear.app/softblaze/issue/SB-3686) | Cloud save binary — infra | M3 |
| [SB-3695](https://linear.app/softblaze/issue/SB-3695) | LLM Herald — templates sufficient | After Phase 2 |
| [SB-3703](https://linear.app/softblaze/issue/SB-3703) | Perf sign-off — CitizenDots cap + overlay LOD first | Parallel |

---

## 5. Day-by-day schedule — Phase 2

### Week 1 — Jul 7–11 (Map legibility)

| Day | Focus | Deliverable |
|-----|-------|-------------|
| **Mon** | WASM export: `traffic[]`, `serviceCoverage[]` | `sim-worker` propagates; types in `sim-bridge.ts` |
| **Tue** | Traffic overlay + toggle | `TrafficOverlay` visible; persisted toggle; green→red heat |
| **Wed** | Services overlay + toolbar | H/P/F modes; city-wide % in toolbar strip |
| **Thu** | CitizenDots v1 | Dots on residential buildings; scale to `householdCount` |
| **Fri** | Integration + smoke | Overlays toggle without FPS cliff; manual QA congestion scenario |

**Week 1 demo:** Toggle traffic → see red bottleneck; toggle health → see coverage gap near industrial zone; dots pulse as population grows.

### Week 2 — Jul 14–18 (Build tools + landmarks)

| Day | Focus | Deliverable |
|-----|-------|-------------|
| **Mon** | Road tool UX | Toolbar road enabled; brush paint; optimistic overlay |
| **Tue** | WASM `place_road` wire + bulldoze roads | Paint persists across snapshot ticks |
| **Wed** | Economy tooltips | R/C/I demand hints; `hud-zoning-btn--demand` highlight |
| **Thu** | Era landmark placeholder | Monument appears when gates met; Herald special edition on click |
| **Fri** | Event markers + polish | Active event pins; smoke: road + overlay toggles |

**Week 2 demo:** Player paints road to fix congestion → traffic heat shifts; zones using economy hints; landmark appears at Industrial gate ready.

---

## 6. Acceptance checklist

### Phase 1 gate (complete — regression only)

- [x] HUD shows R/C/I demand bars updating each snapshot tick
- [x] HUD shows approval, happiness, monthly income vs expenses
- [x] Era progress panel lists gates with current/required
- [x] Research panel lists v1-subset techs; enqueue completes
- [x] Active sim events drive Herald headlines; auto-open on spawn
- [x] Onboarding overlay: 5 steps, skippable
- [x] Era increment triggers fanfare modal / special Herald edition
- [x] News ticker scrolls recent event names
- [ ] Smoke: `/play` asserts RCI + Herald elements (extend `smoke-play-checks.mjs`)

### Phase 2 gate (sprint complete)

- [ ] `CitizenDots` renders ≤12K instances; hidden when `householdCount === 0`
- [ ] Traffic overlay toggles; color maps density 0→1 on road tiles only
- [ ] Services toolbar switches health/police/fire; overlay uses SimCity-style red→green heat
- [ ] Road tool paints multi-tile brush; `place_road` reaches WASM; survives reload
- [ ] Zoning toolbar shows demand tooltips; high-demand tools show visual cue
- [ ] Era landmark placeholder visible when `isEraGateReady(eraProgress)`; no mesh when gates unmet
- [ ] Manual QA: 15-min session — player diagnoses coverage gap via overlay, fixes with zoning/services build
- [ ] Smoke: overlay toggles + road tool present in DOM (extend smoke suite)

### Out of scope (do not block Phase 2 on)

- Cloud save persistence of `traffic` / `serviceCoverage` chunks (SB-3686)
- LLM-generated headlines (SB-3695)
- Final Meshy hero landmark GLBs — placeholder acceptable (SB-3680)
- Coolify / Stripe / auth (SB-3715, SB-3693, SB-3698)
- Perf sign-off at 10K buildings + all overlays on (SB-3703) — cap dots first

---

## 7. Risk & mitigation

| Risk | Mitigation |
|------|------------|
| Overlay draw calls blow FPS budget | Instanced meshes only; cap traffic/service tiles; dots hard-cap 12K |
| `serviceCoverage` payload bloat | Sparse export (non-zero tiles only); throttle snapshot to 4 Hz |
| Road optimistic state desyncs WASM | `mergeRoads` in `sim-worker`; reconcile on full snapshot |
| CitizenDots clutter at max pop | Scale dot count sub-linearly above 8K HH; fade at extreme zoom |
| Landmark placeholder underwhelms | Pair with Herald special edition + era panel checklist already shipped |
| Phase 1 regression during overlay work | Keep Phase 1 checklist as smoke regression suite |

---

## 8. Success metrics (qualitative)

| Metric | After Phase 1 | After Phase 2 |
|--------|---------------|---------------|
| Player can answer "what does my city need?" | Yes (RCI bars) | Yes + tooltips on tools |
| Player sees consequences of growth | Approval + budget + events | + dots + traffic + services on map |
| Player can fix congestion/coverage | Panel-only | Overlay-guided zoning + roads |
| City feels inhabited | Buildings + GLBs | Warm population dots |
| Era progression legible | Checklist + fanfare | + landmark monument on map |

---

## 9. Doc hierarchy

```
WEB_V1_SCOPE.md              ← locked product parameters
V1_GAMEPLAY_BUILD_PLAN.md    ← THIS FILE: Phase 1 (shipped) + Phase 2 (active)
GAMEPLAY_LOOP_IMPROVEMENTS.md ← gap audit + ranked improvements
CTO_IMPROVEMENT_ROADMAP_2026-07.md ← executive priorities
WASM_SIM_BRIDGE.md           ← snapshot contract (Phase 2 traffic/services/roads)
ERA_ARC_DESIGN_V2.md         ← landmark quest + GLB swap spec
```

---

## 10. References

- [GAMEPLAY_LOOP_IMPROVEMENTS.md](./GAMEPLAY_LOOP_IMPROVEMENTS.md) — #8 services overlay, #9 traffic, population visualization
- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — Frontier→Industrial arc, sim depth retained
- [ERA_ARC_DESIGN_V2.md](./ERA_ARC_DESIGN_V2.md) — era gates, landmark placement (§6.B)
- [MESHY_HERO_LANDMARKS.md](./MESHY_HERO_LANDMARKS.md) — GLB swap path for `EraLandmarkPlaceholder`
- [AGENT_06_SERVICES.md](./AGENT_06_SERVICES.md) — coverage model semantics
- [AGENT_04_TRANSPORT.md](./AGENT_04_TRANSPORT.md) — traffic lite export contract
- [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) — merge gates (infra); gameplay runs parallel

---

*Originally authored 2026-07-04 on `feat/gameplay-build-plan-2026-07-04` (`b4a1b37`). Revised for Phase 2 on `feat/wasm-r3f-integration-2026-07-04` · Worktree: `citymajor-web-r3f-spike`.*
