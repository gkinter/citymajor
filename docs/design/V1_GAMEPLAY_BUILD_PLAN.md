# CityMajor Web v1 — Phase 3 Gameplay Build Plan

**Status:** Active — Phase 3  
**Date:** 2026-07-04 (revised)  
**Branch:** `feat/wasm-r3f-integration-2026-07-04` · Worktree: `citymajor-web-r3f-spike`  
**Epic:** [SB-3654 — M2 Gameplay v1 Systems](https://linear.app/softblaze/issue/SB-3654/epic-m2-gameplay-v1-systems)  
**Scope charter:** [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) · [SB-3704](https://linear.app/softblaze/issue/SB-3704)  
**Source audits:** [GAMEPLAY_LOOP_IMPROVEMENTS.md](./GAMEPLAY_LOOP_IMPROVEMENTS.md) · [CTO_IMPROVEMENT_ROADMAP_2026-07.md](./CTO_IMPROVEMENT_ROADMAP_2026-07.md)

> **Intent:** Phase 1 closed the *mayor pulse* loop (RCI HUD, Herald, onboarding, era quests). Phase 2 closed the *city legibility* loop (population dots, traffic/services overlays, roads, economy tooltips, era landmark placeholder). **Phase 3** deepens sim fidelity and persistence — Leontief economy, population L2 citizens, LLM Herald, hero GLTF landmarks, and cloud save binary — so a 60–90 min session survives reload and feels authored, not templated.

---

## 1. Problem statement (post–Phase 2)

| Layer | State (integration worktree, sprint ending `e4d3d4f`) | Player impact |
|-------|------------------------------------------------------|---------------|
| **WASM sim** | Leontief I/O, household happiness, named citizens, full save blob all exist in engine | Mostly invisible on web |
| **Web `/play` — Phase 1 ✅** | RCI bars, approval/budget HUD, era checklist, research panel, Herald auto-open + council stubs, 5-step onboarding, news ticker, Meshy GLB buckets | Mayor loop works |
| **Web `/play` — Phase 2 ✅** | CitizenDots, traffic heatmap, service overlays, road paint, economy tooltips, era landmark placeholder, event markers, paint feedback | City legible on map |
| **Web `/play` — Phase 3 🚧** | RCI-only economy hints; template Herald; placeholder landmark; local save only | Depth and persistence gaps |
| **Economy** | Leontief production chains tick in WASM | Toolbar shows RCI demand only — no input/output visibility |
| **Population** | Household L2 sim (commutes, per-HH happiness) ticks | Warm dots only — no citizen identity or panel drill-down |
| **Herald** | Template path + quota API wired | Headlines feel generic; Founder tier LLM unused |
| **Landmarks** | `EraLandmarkPlaceholder` + frontier GLB refresh (`e4d3d4f`) | Placeholder box — hero Meshy GLBs not swapped |
| **Persistence** | JSON snapshot save/load | No binary cloud save; overlay state lost on device switch |

**Verdict:** Phase 2 made the city *readable*. Phase 3 makes it *rememberable and deep* — players should understand production chains, care about citizens by name, read bespoke Herald copy, see hero monuments, and resume sessions from cloud.

**v1 scope lock (WEB_V1_SCOPE):** Single-player mayor, Frontier → Industrial arc, ~60–90 min session, deep sim at reduced scale (256×256, ~5K buildings, ~10K HH). Phase 3 stays inside this arc; auth/Stripe remain infra-adjacent.

---

## 2. Phase 1 recap — SHIPPED on integration

The original 2-week plan (orphan branch `feat/gameplay-build-plan-2026-07-04`, commit `b4a1b37`) is **complete** on `feat/wasm-r3f-integration-2026-07-04`:

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

---

## 3. Phase 2 recap — SHIPPED (`e4d3d4f` sprint)

Phase 2 plan authored in `d863fdb`. All P0–P2 work packages landed on integration branch through sprint head `e4d3d4f`:

| Priority | Work package | Status | Key commit / files |
|:--------:|--------------|--------|-------------------|
| P0 | **Population dots** — instanced warm dots on residential footprints | ✅ Shipped | `7539b37` — `CitizenDots.tsx`, `population-growth.ts` |
| P0 | **Traffic heatmap** — `TrafficOverlay` + toggle; density 0–1 | ✅ Shipped | `5a02efb` — `TrafficOverlay.tsx`, `TrafficOverlayToggle.tsx` |
| P0 | **Services overlay** — health/police/fire heat + toolbar | ✅ Shipped | `3066f59` — `ServiceCoverageOverlay.tsx`, `ServicesToolbar.tsx` |
| P0 | **Road tool** — brush paint, optimistic `roads[]`, WASM `place_road` | ✅ Shipped | `be44700` — `zoning.ts`, `CityCanvas.tsx`, `sim-worker.ts` |
| P1 | **Economy tooltips** — demand hints, high-demand CSS | ✅ Shipped | `775eb4a` — `zoning-economy.ts`, `ZoningToolbar.tsx` |
| P1 | **Era landmarks** — gate-ready placeholder mesh + label | ✅ Shipped | `ce31f4f` — `EraLandmarkPlaceholder.tsx`, `era-landmarks.ts` |
| P2 | **Paint feedback** — zone/road paint pop-in + responsive feedback | ✅ Shipped | `c97cc9d` — `paint-feedback.ts`, `ZoningToolbar.tsx` |
| P2 | **Event markers** — 3D pins for active events; click → Herald | ✅ Shipped | `d0dbfc3` — `EventMarkers.tsx` |
| — | **Frontier GLB asset refresh** (landmark swap prep) | ✅ Shipped | `e4d3d4f` — `res_low_frontier_00.glb` |

**Phase 2 exit criteria:** met for manual QA on integration branch. Smoke extensions for overlay toggles + road tool remain incremental hardening, not blockers for Phase 3.

**Snapshot fields delivered (Phase 2):**

```typescript
traffic?: Array<{ tileX: number; tileZ: number; density: number }>;
serviceCoverage?: Array<{ tileX: number; tileZ: number; health: number; police: number; fire: number }>;
roads?: Array<{ tileX: number; tileZ: number; roadFlags: number }>;
// householdCount in resources; CitizenDots derives dot budget
```

---

## 4. Phase 3 sprint goal (~2 weeks)

**Exit criteria:** A player completing a Frontier → Industrial session on `/play` can:

1. **Read Leontief economy** — input/output chain panel or HUD strip showing what industries consume/produce; zoning hints reference shortages, not RCI alone.
2. **Meet population L2** — named household drill-down (panel or picker); per-HH happiness/commute visible; dots link to citizen card stub.
3. **Receive LLM Herald** — Founder-tier `/api/narrative/event` path generates bespoke headlines from sim context; template fallback when quota exhausted.
4. **See hero GLTF landmarks** — `EraLandmarkPlaceholder` swaps to Meshy hero GLB per era gate (`MESHY_HERO_LANDMARKS.md`); frontier asset path validated (`e4d3d4f`).
5. **Resume from cloud save binary** — WASM save blob + overlay prefs persist via SB-3686 chunk format; load on return without local-only JSON.

**Not in Phase 3:** Auth/Stripe billing (SB-3693/3698), full Postwar era arc, perf sign-off at max overlays + LLM latency (SB-3703), multiplayer.

---

## 5. Linear mapping — Phase 3 work packages

### Epic anchor (unchanged)

| Issue | Role |
|-------|------|
| [SB-3654](https://linear.app/softblaze/issue/SB-3654) | Parent epic — M2 Gameplay v1 Systems |
| [SB-3708](https://linear.app/softblaze/issue/SB-3708) | Master tracker — link Phase 3 section in comment |
| [SB-3690](https://linear.app/softblaze/issue/SB-3690) | **Leontief economy** — promoted from deferred |
| [SB-3689](https://linear.app/softblaze/issue/SB-3689) | **Population L2** — full citizen depth beyond dots |
| [SB-3695](https://linear.app/softblaze/issue/SB-3695) | **LLM Herald** — Founder narrative path |
| [SB-3680](https://linear.app/softblaze/issue/SB-3680) | **Hero GLTF landmarks** — placeholder → Meshy GLB swap |
| [SB-3686](https://linear.app/softblaze/issue/SB-3686) | **Cloud save binary** — WASM blob persistence |

### Phase 3 priorities (active)

| Priority | Work package | Linear | Component / lib | Effort |
|:--------:|--------------|--------|-----------------|:------:|
| P0 | **Leontief economy export** — industry I/O matrix in snapshot; shortage/surplus strip | [SB-3690](https://linear.app/softblaze/issue/SB-3690) | `economy-panel.ts`, `ResourcesHud.tsx`, `sim-bridge.ts` | 3 d |
| P0 | **Population L2 depth** — named HH list, happiness/commute fields, dot → card drill-down | [SB-3689](https://linear.app/softblaze/issue/SB-3689) | `CitizenPanel.tsx`, `population-l2.ts` | 3 d |
| P0 | **Cloud save binary** — WASM `SaveGame` blob upload/download; chunk schema v2 overlays | [SB-3686](https://linear.app/softblaze/issue/SB-3686) | `/api/saves`, `save-format.ts`, `SAVE_FORMAT_WEB.md` | 3 d |
| P1 | **LLM Herald** — sim-context prompt to narrative API; quota + template fallback | [SB-3695](https://linear.app/softblaze/issue/SB-3695) | `HeraldPanel.tsx`, `/api/narrative/event`, `narrative-prompt.ts` | 2 d |
| P1 | **Hero GLTF landmarks** — swap `EraLandmarkPlaceholder` for era Meshy GLBs | [SB-3680](https://linear.app/softblaze/issue/SB-3680) | `EraLandmarkGltf.tsx`, `era-landmarks.ts`, `MESHY_HERO_LANDMARKS.md` | 2 d |
| P2 | **Economy ↔ zoning hints** — Leontief shortages drive toolbar copy (extends Phase 2 tooltips) | [SB-3688](https://linear.app/softblaze/issue/SB-3688) ext | `zoning-economy.ts`, `ZoningToolbar.tsx` | 1 d |
| P2 | **Save smoke + Herald LLM smoke** — Playwright asserts cloud round-trip + headline render | [SB-3654](https://linear.app/softblaze/issue/SB-3654) child | `smoke-play-checks.mjs` | 1 d |

**Phase 3 dependency graph:**

```
WASM Leontief matrix ──► economy panel + zoning hint upgrade

WASM household L2 ──► CitizenPanel + CitizenDots onSelect

WASM SaveGame blob ──► /api/saves binary ──► load on session start

sim context + quota ──► LLM Herald ──► template fallback

era gate met + Meshy GLB catalog ──► EraLandmarkGltf swap (replaces placeholder)
```

**Snapshot / save fields (Phase 3 extensions):**

```typescript
leontief?: {
  industries: Array<{ id: string; inputs: Record<string, number>; outputs: Record<string, number>; utilization: number }>;
  shortages: Array<{ goodId: string; deficit: number }>;
};
populationL2?: {
  households: Array<{ id: string; tileX: number; tileZ: number; happiness: number; commuteMin: number }>;
};
// cloud save: opaque WASM binary + snapshotSchemaVersion 3 metadata chunk for overlay prefs
```

### Explicitly deferred (post–Phase 3)

| Issue | Why deferred | When |
|-------|--------------|------|
| [SB-3693](https://linear.app/softblaze/issue/SB-3693) / [SB-3698](https://linear.app/softblaze/issue/SB-3698) | Auth + Stripe — cloud save can ship with anonymous/session token first | M3 infra |
| [SB-3703](https://linear.app/softblaze/issue/SB-3703) | Perf sign-off — LLM latency + L2 panel + all overlays | Parallel after Phase 3 |
| [SB-3715](https://linear.app/softblaze/issue/SB-3715) | Coolify prod cutover | Infra track |
| Postwar era content | WEB_V1_SCOPE locks Frontier → Industrial | v1.2 |

---

## 6. Day-by-day schedule — Phase 3

### Week 1 — Jul 21–25 (Sim depth)

| Day | Focus | Deliverable |
|-----|-------|-------------|
| **Mon** | WASM export: Leontief matrix + shortages | `sim-worker` propagates; types in `sim-bridge.ts` |
| **Tue** | Economy panel + HUD shortage strip | Player sees input/output chain; zoning hints reference deficits |
| **Wed** | Population L2 export + `CitizenPanel` v1 | Named HH list; happiness/commute; dot click opens card |
| **Thu** | Cloud save binary — write path | WASM blob POST `/api/saves`; metadata chunk for overlays |
| **Fri** | Cloud save binary — read path + integration | Session resume; manual QA 15-min save/load cycle |

**Week 1 demo:** Player spots industrial input shortage in economy panel → zones commercial to fix chain → saves → reloads from cloud → state intact.

### Week 2 — Jul 28–Aug 1 (Narrative + landmarks)

| Day | Focus | Deliverable |
|-----|-------|-------------|
| **Mon** | LLM Herald prompt + API wire | Sim context in `/api/narrative/event`; quota guard |
| **Tue** | Herald UI — LLM vs template badge; fallback on 429 | Founder headlines feel bespoke |
| **Wed** | Hero GLTF landmark swap | `EraLandmarkGltf` loads Meshy GLB per era; removes placeholder |
| **Thu** | Leontief → zoning hint upgrade | Toolbar copy uses shortage data, not RCI alone |
| **Fri** | Smoke + polish | Cloud round-trip smoke; Herald LLM smoke; Phase 1–2 regression |

**Week 2 demo:** Industrial gate met → hero monument GLB appears → Herald LLM special edition → player picks council option → cloud save survives browser refresh.

---

## 7. Acceptance checklist

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

### Phase 2 gate (complete — regression only)

- [x] `CitizenDots` renders ≤12K instances; hidden when `householdCount === 0`
- [x] Traffic overlay toggles; color maps density 0→1 on road tiles only
- [x] Services toolbar switches health/police/fire; overlay uses SimCity-style red→green heat
- [x] Road tool paints multi-tile brush; `place_road` reaches WASM; survives reload
- [x] Zoning toolbar shows demand tooltips; high-demand tools show visual cue
- [x] Era landmark placeholder visible when `isEraGateReady(eraProgress)`; no mesh when gates unmet
- [x] Event markers pulse on active events; click opens Herald
- [x] Paint feedback on zone/road paint
- [ ] Smoke: overlay toggles + road tool present in DOM (extend smoke suite)

### Phase 3 gate (sprint complete)

- [ ] Leontief panel shows industry I/O; shortage strip updates each snapshot tick
- [ ] Zoning hints reference Leontief shortages when deficit exists (not RCI-only)
- [ ] `CitizenPanel` lists ≥1 named household; dot click opens card with happiness + commute
- [ ] Cloud save: POST binary blob + GET restore within 5s on preview; overlay toggles persist
- [ ] LLM Herald generates headline when quota available; template fallback on exhaustion
- [ ] Hero GLTF landmark replaces placeholder when era gate met; GLB loads from catalog
- [ ] Manual QA: 60-min session save → close tab → resume → economy + citizens + landmark state match
- [ ] Smoke: cloud save round-trip + Herald headline render (extend smoke suite)

### Out of scope (do not block Phase 3 on)

- Stripe checkout + Founder tier billing (SB-3698)
- Full OAuth account linking (SB-3693) — session/anonymous token OK for binary save
- Postwar era + quest chain beyond Industrial gate
- Perf sign-off at 10K buildings + LLM + all overlays (SB-3703)
- Multiplayer / shared cities

---

## 8. Risk & mitigation

| Risk | Mitigation |
|------|------------|
| Leontief snapshot payload bloat | Export top-N industries + active shortages only; cap households in L2 export |
| LLM latency blocks Herald auto-open | Stream headline; show template immediately, swap when LLM returns |
| Cloud save blob size exceeds API limit | Compress WASM blob; chunk uploads if >4 MB |
| Hero GLB load hitch on era transition | Preload next-era GLB when gate ≥80%; keep placeholder until load complete |
| Population L2 panel FPS hit | Virtualized list; sample 50 HH for drill-down, full export async |
| Phase 1–2 regression during depth work | Keep Phase 1–2 checklists as smoke regression suite |

---

## 9. Success metrics (qualitative)

| Metric | After Phase 2 | After Phase 3 |
|--------|---------------|---------------|
| Player understands production chains | RCI + tooltips only | Leontief panel + shortage-driven hints |
| Player connects to citizens | Warm dots | Named HH + happiness drill-down |
| Herald feels authored | Template headlines | LLM copy with template fallback |
| Era milestone visible | Placeholder monument | Hero Meshy GLTF landmark |
| Session survives reload | Local JSON only | Cloud binary save + overlay prefs |
| 60–90 min arc completable | Yes (manual) | Yes + resume after interrupt |

---

## 10. Doc hierarchy

```
WEB_V1_SCOPE.md              ← locked product parameters
V1_GAMEPLAY_BUILD_PLAN.md    ← THIS FILE: Phase 1–2 (shipped) + Phase 3 (active)
GAMEPLAY_LOOP_IMPROVEMENTS.md ← gap audit + ranked improvements
CTO_IMPROVEMENT_ROADMAP_2026-07.md ← executive priorities
WASM_SIM_BRIDGE.md           ← snapshot contract (Phase 3 Leontief + L2)
SAVE_FORMAT_WEB.md           ← cloud binary chunk schema (SB-3686)
ERA_ARC_DESIGN_V2.md         ← landmark quest + GLB swap spec
MESHY_HERO_LANDMARKS.md      ← hero GLB catalog + swap path
```

---

## 11. References

- [GAMEPLAY_LOOP_IMPROVEMENTS.md](./GAMEPLAY_LOOP_IMPROVEMENTS.md) — Leontief (#SB-3690), population L2, LLM Herald, cloud save
- [WEB_V1_SCOPE.md](./WEB_V1_SCOPE.md) — Frontier→Industrial arc, sim depth retained
- [ERA_ARC_DESIGN_V2.md](./ERA_ARC_DESIGN_V2.md) — era gates, hero landmark placement (§6.B)
- [MESHY_HERO_LANDMARKS.md](./MESHY_HERO_LANDMARKS.md) — GLB swap path (`e4d3d4f` frontier assets)
- [SAVE_FORMAT_WEB.md](./SAVE_FORMAT_WEB.md) — binary save chunk layout (SB-3686)
- [AGENT_03_ECONOMY.md](./AGENT_03_ECONOMY.md) — Leontief I/O model semantics
- [V1_MERGE_CHECKLIST.md](./V1_MERGE_CHECKLIST.md) — merge gates (infra); gameplay runs parallel

---

*Originally authored 2026-07-04 on `feat/gameplay-build-plan-2026-07-04` (`b4a1b37`). Phase 2 revision `d863fdb`. Phase 2 shipped sprint `e4d3d4f`. Phase 3 revision on `feat/wasm-r3f-integration-2026-07-04` · Worktree: `citymajor-web-r3f-spike`.*
