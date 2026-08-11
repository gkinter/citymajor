# Cathedral Sprint 2 — Economy depth + population truth

**Status:** Closing — wave-3 sync 2026-08-10 @ `06977a9`; **next sprint is Unity-first**  
**Dates:** 2026-08-11 → 2026-08-24 (2 weeks)  
**Integration branch:** `feat/unity-port-plan-2026-07-12` (`citymajor-unity-port-plan`)  
**Charter:** [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md)  
**Next sprint:** [`CATHEDRAL_UNITY_SPRINT.md`](./CATHEDRAL_UNITY_SPRINT.md) — Unity UI/HUD + tools parity (player experience)  
**Linear:** SB-4211, SB-4222–4224, SB-4231–4232, SB-4262 / **SB-4986** (P7.2 gate Done), SB-4987 (utilities char)

**Platform note:** Sprint 2 landed several **web** HUD/overlays as early mirrors of Cathedral snapshot fields. Those are **not** the ship surface — Unity binds the same fields in Sprint 3. WASM/web remain optional **sim validation harness** only.

**Landed this sync:** P2.1 ✅ · P3.2 export+HUD ✅ · P3.4 friction ✅ · P4.1 O-D+HUD ✅ · P4.2 FW+commute sat ✅ · P4.5 mode choice + mode-share HUD ✅ · P4.3 unemployment HUD ✅ · P1.5 ramp/bridge UI ✅ · congestion heatmap ✅ · SimplexNoise ✅ · P5 politics stub ✅ · P5.1 utilities char ✅ · P5.2 EMS response ✅ · P5.5/P5.6 ✅ · **P7.2 0-skip gate ✅** · smoke WASM wait ✅ · E-key fix ✅ · perf median gate ✅ · OpenSpec `001` archived ✅  
**Still open:** none (P3.3 closed — Unity Economy partition spread + 1→4→9→16 characterization)  
**Hand-off:** Port / bind player-facing Sprint 2 surfaces in Unity — see U3.* in [`CATHEDRAL_UNITY_SPRINT.md`](./CATHEDRAL_UNITY_SPRINT.md)

---

## 1. Sprint goal

Sprint 1 closed **infrastructure truth** (P1) and the first housing/economy HUD slice (P2.2–P2.5, P3.1). Sprint 2 makes the **goods economy legible end-to-end** and starts **population O-D truth** so traffic and satisfaction reflect real home/work pairs — not gravity stubs.

**Primary pillar:** P3 Economy & Trade  
**Secondary pillars:** P4 Population (parallel SimCore lane), P2 Housing (palette only)

**Exit gate:** Goods chain drill-down + partition price spread visible; 90%+ commuters have home/work IDs; commute time feeds satisfaction; zero skipped Cathedral characterization tests in CI.

---

## 2. Sprint 1 carry-over (done)

| Milestone | Integration tip |
|-----------|-----------------|
| P1.1–P1.6 | `b61c4b1` — road tiers, graph v2, ramps, toolbar, snapshot |
| P2.2–P2.5 | `b61c4b1` — density, rent, Herald housing, vacancy export |
| P3.1 | `761289e` — goods panel + prices |

---

## 3. Sprint 2 milestones

### P2.1 — Extended zone palette (SB-4211) ✅

| Field | Value |
|-------|-------|
| **Owner** | UI + SimCore (zone growth) |
| **Deliverable** | Paintable office (5), mixed-use (6), park ploppable, ag subset (7) |
| **Acceptance** | Each type affects RCI + spawn rules per `AGENT_05`; era gating (office @ Industrial, mixed @ Colonial+) |
| **OpenSpec** | New change `002-zone-palette-v1` (delta on `zoning-housing.md`) |
| **Landed** | `a990636` — extended zone palette with era gates |

### P3.2 — Production chain + partition prices (SB-4222) ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore / snapshot + HUD |
| **Deliverable** | Export market-zone partition prices; production-chain HUD with goods flow |
| **Acceptance** | Partition prices + chain inputs/outputs visible in Economy HUD |
| **Depends** | P3.1 ✅ |
| **Landed** | `45d8186` partition prices · `746780c` production-chain HUD |

### P3.3 — Market zone partition pricing (SB-4223) ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore + Unity Economy HUD |
| **Deliverable** | 1→4→9→16 market zones with distinct per-zone prices; Economy `[E]` partition spread |
| **Acceptance** | `CathedralEconomyTests.MarketZonePrices_DifferAcrossPartitions` green; trade-shock + 9/16 scale tests green; Unity shows min–max spread |
| **Depends** | P3.1 ✅ |
| **Landed** | SimCore scale/trade-shock characterization · Unity Economy/Cathedral partition spread HUD |

### P3.4 — Inter-zone friction UI (SB-4224) ✅

| Field | Value |
|-------|-------|
| **Owner** | Overlay (web spike archival; Unity U3.3 is product) |
| **Deliverable** | Read-only friction corridor overlay (high-friction corridors highlighted) |
| **Acceptance** | Overlay matches `MeanInterZoneFriction` / `InterZoneTradeVolume` on snapshot |
| **Depends** | P3.1 ✅ (shipped against existing friction fields; P3.3 depth still open) |
| **Landed** | `a9e7878` — goods transport friction overlay |

### P4.1 — Home/work building IDs (SB-4231) ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore (population + traffic) + HUD (web spike; Unity U3.5 product) |
| **Deliverable** | Replace gravity O-D with building-pair assignment for commuters; surface coverage in HUD |
| **Acceptance** | ≥90% commuters have valid `homeBuildingId` + `workBuildingId` on snapshot |
| **Depends** | P1.5 ✅ (typed-edge traffic) |
| **Landed** | `99a6547` O-D · `fe69037` BPR · `ccf7f22` O-D sample HUD |

### P4.2 — Commute time → satisfaction (SB-4232) ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore |
| **Deliverable** | Graph travel time from P4.1 pairs feeds household satisfaction formula |
| **Acceptance** | Longer commute lowers satisfaction ceteris paribus; characterization test added |
| **Depends** | P4.1 |
| **Landed** | `76db8bf` commute→satisfaction; `f72dd3d` multi-iteration Frank-Wolfe + edge travel time export |

### P4.3 — Unemployment HUD ✅

| Field | Value |
|-------|-------|
| **Owner** | Unity HUD + Economy export |
| **Deliverable** | Surface unemployment / job vacancy from snapshot on Economy / Cathedral metrics HUD |
| **Landed** | Unity overlay `e70ed78` · web Economy HUD lane `46a08e1` (pending cherry-pick if not on tip) |

### P4.5 — Mode choice stub ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore (WasmTrafficLite) + web HUD |
| **Deliverable** | Simple MNL car vs transit weight from `TransitPreference`; mode shares in Economy Transport HUD |
| **Landed** | `571352a` mode-choice stub · `370e944` mode-share HUD |

### P1.5 UX stretch — Ramp / bridge toolbar ✅

| Field | Value |
|-------|-------|
| **Owner** | Input tools (web spike archival; Unity U3.1 product) |
| **Deliverable** | Dedicated highway ramp paint tool + bridge/tunnel mode toggle |
| **Landed** | `6084fad` ramp tool · `b34942a` bridge/tunnel toggle · `c8f9e52` ramp PlaceRoad fix |

### Client overlays ✅

| Deliverable | Tip |
|-------------|-----|
| Congestion heatmap from `edgeVolumes` / `travelTimes` | `c8827bf` |
| O-D / commuter coverage in Economy HUD | `ccf7f22` |
| Mode shares in Economy Transport HUD | `370e944` |
| Unemployment / job vacancy HUD | `e70ed78` |

### P5 politics foundation + Herald / council ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore / characterization |
| **Deliverable** | Approval stub · Herald unrest predicates (P5.5) · council seats export (P5.6) |
| **Landed** | Stub `fb64bd9` · P5.5 `0452f88` · P5.6 `ae15498` — see [`CATHEDRAL_P5_POLITICS.md`](./CATHEDRAL_P5_POLITICS.md) |

### P5.1 / P5.2 — Utilities + emergency response ✅

| Field | Value |
|-------|-------|
| **Owner** | SimCore |
| **Deliverable** | Utilities coverage export characterization; EMS response minutes via road graph + BPR |
| **Landed** | P5.1 `ae635b0` (SB-4987) · P5.2 `06977a9` (SB-4242) — see [`CATHEDRAL_P5_SERVICES.md`](./CATHEDRAL_P5_SERVICES.md) |

### Harness / play polish ✅

| Deliverable | Tip |
|-------------|-----|
| Smoke waits for `Data: WASM sim` before source assert | `0ecc0ba` |
| E closes economy panel when open | `5205201` |
| Perf gate uses median FPS after hitch discard | `6c1f077` |

### P7.2 gate — Flaky / skipped test cleanup (SB-4262 / SB-4986) ✅

| Field | Value |
|-------|-------|
| **Owner** | Orchestrator (CI lane) |
| **Deliverable** | Cathedral test filter runs clean in CI; no `[Fact(Skip=…)]` for shipped milestones |
| **Acceptance** | `dotnet test --filter "FullyQualifiedName~Cathedral"` — 0 skipped, 0 flaky retries |
| **Landed** | Test isolation `49edba1`; SimplexNoise `b4be623`; **0-skip gate `817e968`** (SB-4986 Done) |

**Remaining deferrals (documented, no Skip attributes):**

| Item | Why not a Skip | Tracking |
|------|----------------|----------|
| **P5.7 Economic Control Spectrum** | No characterization test yet — **v2 boundary** | [`CATHEDRAL_P5_POLITICS.md`](./CATHEDRAL_P5_POLITICS.md) §2 |
| Full faction HUD / protest polish | Seats export stubbed on snapshot + WASM status; UI still Phase 2 | P5.6 follow-on when Politics HUD ships |

---

## 4. Lane dispatch (parallel agents)

| Lane | Branch prefix | Sprint 2 owns | Merge gate |
|------|---------------|---------------|------------|
| **SimCore — economy** | `feat/cathedral-p3-market-*` | P3.3 partition export | `dotnet test --filter CathedralEconomy` |
| **SimCore — population** | `feat/cathedral-p4-*` | P4.1–P4.5 (landed) | `dotnet test --filter CathedralPopulation` |
| **Unity HUD** | `feat/cathedral-p3-chain-*` | P3.2 production chain panel (landed) | Play mode — building click shows chain |
| **Web overlay** (archival) | `feat/cathedral-p3-friction-*` | P3.4 friction matrix overlay (landed — not ship UX) | harness smoke only |
| **Unity HUD** (Sprint 3) | `feat/cathedral-u3-*` | Bind Sprint 2 fields in Unity — [`CATHEDRAL_UNITY_SPRINT.md`](./CATHEDRAL_UNITY_SPRINT.md) | Play gate |
| **Zone palette** | `feat/cathedral-p2-palette-*` | P2.1 brush + era gates (landed) | Zone paint + growth characterization |
| **CI / tests** | `feat/cathedral-p7-tests-*` | P7.2 flaky gate ✅ | Full Cathedral filter green |

**Orchestrator rule:** cherry-pick one lane at a time to integration; human Play gate subset after each SimCore batch.

---

## 5. Week-by-week plan

| Week | Focus | Demo |
|------|-------|------|
| **W1** (Aug 11–15) | P3.3 partition pricing + P4.1 home/work IDs (SimCore); P2.1 palette stub | Market zones show price spread; snapshot has home/work IDs |
| **W2** (Aug 18–22) | P3.2 chain HUD + P3.4 friction overlay + P4.2 satisfaction; P7.2 gate | Click building → chain; friction overlay; commute affects happiness; CI Cathedral filter clean |

**Fri W2 ritual:** Update `SIM_V1_GAP_MATRIX`, archive OpenSpec deltas, Linear project update.

---

## 6. Out of scope (Sprint 2)

- P3.5 traffic→goods delivery lag (needs P1 edge volumes fully wired — defer Sprint 3)
- P3.6 bilateral trade routes (v2 boundary)
- P4.4 L2 scale export (zone-level unemployment drill-down still open)
- P5.3 fire spread / hydrants (Phase 4)
- P6.1–P6.2 law/event depth beyond Herald predicates + seats export

---

## 7. Acceptance checklist (Fri W2)

- [x] `dotnet test tests/Forge.SimCore.Tests --filter "FullyQualifiedName~Cathedral"` — all pass, zero skipped (`817e968`)
- [x] Goods panel / snapshot shows partition prices (export landed `45d8186`; Unity spread HUD P3.3)
- [x] Production-chain HUD with goods flow (`746780c`)
- [x] Friction overlay landed (web spike `a9e7878`; Unity U3.3 binds product UX)
- [x] Snapshot: home/work building O-D (`99a6547`) + O-D HUD (`ccf7f22`)
- [x] Commute time → satisfaction + FW travel times (`76db8bf` / `f72dd3d`)
- [x] Mode-choice stub (`571352a`) + mode-share HUD (`370e944`)
- [x] Unemployment / job vacancy HUD (`e70ed78`)
- [x] Congestion heatmap (`c8827bf`)
- [x] Ramp paint + bridge/tunnel toolbar (`6084fad` / `b34942a`)
- [x] Office / mixed / park / ag brushes paintable with era gates (`a990636`)
- [x] Test isolation: SimHost serialize (`49edba1`) + SimplexNoise instance-local (`b4be623`) + **P7.2 0-skip gate** (`817e968` / SB-4986)
- [x] P5 politics foundation stub (`fb64bd9`) + P5.5 Herald predicates (`0452f88`) + P5.6 council seats (`ae15498`)
- [x] P5.1 utilities characterization (`ae635b0`) + P5.2 EMS response (`06977a9`)
- [x] Smoke wait WASM (`0ecc0ba`) · E-key economy toggle (`5205201`) · perf median gate (`6c1f077`)
- [x] OpenSpec `001-road-tier-and-graph-v2` archived
- [ ] OpenSpec `002-zone-palette-v1` + economy delta archived
- [ ] Integration tip Play gate subset passed ([`UNITY_PLAY_CHECKLIST.md`](./UNITY_PLAY_CHECKLIST.md))

---

## 8. Risk register

| Risk | Mitigation |
|------|------------|
| P4.1 blocks P4.2 mid-sprint | Start P4.1 W1 day 1; P4.2 lands only after P4.1 cherry-pick |
| P3.3 sim work delays P3.4 UI | P3.4 shipped read-only overlay against existing `MeanInterZoneFriction` while P3.3 finishes |
| Skipped tests mask regressions | P7.2 gate is a **merge blocker** — no cherry-pick without green Cathedral filter |
| P2.1 scope creep (full Modern era) | v1 subset only: office, mixed, park ploppable, frontier/industrial ag |

---

## 9. Linear issues (create / assign)

| ID | Title | Sprint |
|----|-------|--------|
| SB-4211 | P2.1 Extended zone types | W1 ✅ |
| SB-4222 | P3.2 Production chain visibility | W2 ✅ |
| SB-4223 | P3.3 Market zone partition pricing | W1 |
| SB-4224 | P3.4 Inter-zone friction UI | W2 ✅ |
| SB-4231 | P4.1 Home/work building IDs | W1 ✅ |
| SB-4232 | P4.2 Commute time → satisfaction | W2 ✅ |
| SB-4262 / **SB-4986** | P7.2 Cathedral test gate (no skip/flaky) | W2 ✅ |
| SB-4987 | P5.1 utilities coverage characterization (sim export; web HUD canceled) | ✅ char `ae635b0` |
| SB-4242 | P5.2 Emergency response time | ✅ `06977a9` |
