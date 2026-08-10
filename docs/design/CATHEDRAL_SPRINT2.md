# Cathedral Sprint 2 — Economy depth + population truth

**Status:** Active sprint plan  
**Dates:** 2026-08-11 → 2026-08-24 (2 weeks)  
**Integration branch:** `feat/unity-port-plan-2026-07-12` (`citymajor-unity-port-plan`)  
**Charter:** [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md)  
**Linear:** SB-4211, SB-4222–4224, SB-4231–4232, SB-4262 (flaky-test gate)

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

### P2.1 — Extended zone palette (SB-4211)

| Field | Value |
|-------|-------|
| **Owner** | UI + SimCore (zone growth) |
| **Deliverable** | Paintable office (5), mixed-use (6), park ploppable, ag subset (7) |
| **Acceptance** | Each type affects RCI + spawn rules per `AGENT_05`; era gating (office @ Industrial, mixed @ Colonial+) |
| **OpenSpec** | New change `002-zone-palette-v1` (delta on `zoning-housing.md`) |

### P3.2 — Production chain visibility (SB-4222)

| Field | Value |
|-------|-------|
| **Owner** | Unity HUD lane |
| **Deliverable** | Building click → inputs/outputs drill-down from `ProductionChain` |
| **Acceptance** | Industrial building shows at least 2 input goods + 1 output; web stub OK |
| **Depends** | P3.1 ✅ |

### P3.3 — Market zone partition pricing (SB-4223)

| Field | Value |
|-------|-------|
| **Owner** | SimCore |
| **Deliverable** | 8–16 active market zones with distinct per-zone prices on snapshot |
| **Acceptance** | `CathedralEconomyTests.MarketZonePrices_DifferAcrossPartitions` **unskipped and green**; two zones show price delta after trade shock |
| **Depends** | P3.1 ✅ |

### P3.4 — Inter-zone friction UI (SB-4224)

| Field | Value |
|-------|-------|
| **Owner** | Web + Unity overlay |
| **Deliverable** | Read-only 16×16 friction matrix overlay (high-friction corridors highlighted) |
| **Acceptance** | Overlay matches `MeanInterZoneFriction` / `InterZoneTradeVolume` on snapshot |
| **Depends** | P3.3 |

### P4.1 — Home/work building IDs (SB-4231)

| Field | Value |
|-------|-------|
| **Owner** | SimCore (population + traffic) |
| **Deliverable** | Replace gravity O-D with building-pair assignment for commuters |
| **Acceptance** | ≥90% commuters have valid `homeBuildingId` + `workBuildingId` on snapshot |
| **Depends** | P1.5 ✅ (typed-edge traffic) |

### P4.2 — Commute time → satisfaction (SB-4232)

| Field | Value |
|-------|-------|
| **Owner** | SimCore |
| **Deliverable** | Graph travel time from P4.1 pairs feeds household satisfaction formula |
| **Acceptance** | Longer commute lowers satisfaction ceteris paribus; characterization test added |
| **Depends** | P4.1 |

### P7.2 gate — Flaky / skipped test cleanup (SB-4262)

| Field | Value |
|-------|-------|
| **Owner** | Orchestrator (CI lane) |
| **Deliverable** | Cathedral test filter runs clean in CI; no `[Fact(Skip=…)]` for shipped milestones |
| **Acceptance** | `dotnet test --filter "FullyQualifiedName~Cathedral"` — 0 skipped, 0 flaky retries; unskip `MarketZonePrices_DifferAcrossPartitions` when P3.3 lands |
| **Depends** | P3.3 merge |

---

## 4. Lane dispatch (parallel agents)

| Lane | Branch prefix | Sprint 2 owns | Merge gate |
|------|---------------|---------------|------------|
| **SimCore — economy** | `feat/cathedral-p3-market-*` | P3.3 partition export, P3.4 snapshot fields | `dotnet test --filter CathedralEconomy` |
| **SimCore — population** | `feat/cathedral-p4-*` | P4.1 home/work IDs, P4.2 satisfaction | `dotnet test --filter CathedralPopulation` (new) |
| **Unity HUD** | `feat/cathedral-p3-chain-*` | P3.2 production chain panel | Play mode — building click shows chain |
| **Web overlay** | `feat/cathedral-p3-friction-*` | P3.4 friction matrix overlay | `pnpm test` + smoke |
| **Zone palette** | `feat/cathedral-p2-palette-*` | P2.1 brush + era gates | Zone paint + growth characterization |
| **CI / tests** | `feat/cathedral-p7-tests-*` | P7.2 flaky gate | Full Cathedral filter green |

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
- P4.3–P4.5 unemployment HUD, L2 scale export, mode choice
- P5 services, P6 governance (Phase 3 per roadmap)
- Dedicated ramp paint tool (deferred from P1.4 — optional stretch)

---

## 7. Acceptance checklist (Fri W2)

- [ ] `dotnet test tests/Forge.SimCore.Tests --filter "FullyQualifiedName~Cathedral"` — all pass, zero skipped
- [ ] Goods panel shows partition price spread (not just city average)
- [ ] Building click shows production chain inputs/outputs (Unity)
- [ ] Friction overlay renders on web + Unity
- [ ] Snapshot: ≥90% commuters with home/work building IDs
- [ ] Commute time negatively correlates with satisfaction in test city
- [ ] Office / mixed / park / ag brushes paintable with era gates
- [ ] OpenSpec `002-zone-palette-v1` + economy delta archived
- [ ] Integration tip Play gate subset passed ([`UNITY_PLAY_CHECKLIST.md`](./UNITY_PLAY_CHECKLIST.md))

---

## 8. Risk register

| Risk | Mitigation |
|------|------------|
| P4.1 blocks P4.2 mid-sprint | Start P4.1 W1 day 1; P4.2 lands only after P4.1 cherry-pick |
| P3.3 sim work delays P3.4 UI | P3.4 can ship read-only overlay against existing `MeanInterZoneFriction` while P3.3 finishes |
| Skipped tests mask regressions | P7.2 gate is a **merge blocker** — no cherry-pick without green Cathedral filter |
| P2.1 scope creep (full Modern era) | v1 subset only: office, mixed, park ploppable, frontier/industrial ag |

---

## 9. Linear issues (create / assign)

| ID | Title | Sprint |
|----|-------|--------|
| SB-4211 | P2.1 Extended zone types | W1 |
| SB-4222 | P3.2 Production chain visibility | W2 |
| SB-4223 | P3.3 Market zone partition pricing | W1 |
| SB-4224 | P3.4 Inter-zone friction UI | W2 |
| SB-4231 | P4.1 Home/work building IDs | W1 |
| SB-4232 | P4.2 Commute time → satisfaction | W2 |
| SB-4262 | P7.2 Cathedral test gate (no skip/flaky) | W2 |
