# Cathedral P5 — Politics & Governance Foundation

**Status:** Spec + characterization stub (approval + council seats export live)  
**Program:** WP-E cathedral stretch · [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md)  
**Program pillar:** **P6 — Governance** (Sprint 2 dispatch labeled this slice “P5 politics foundation”)  
**Prerequisites:** P4 satisfaction / commute drivers · P2 housing Herald triggers  
**References:** [`AGENT_07_POLITICS.md`](./AGENT_07_POLITICS.md) · [`POLITICAL_LAW_SYSTEM.md`](./POLITICAL_LAW_SYSTEM.md) · [`WASM_SIM_BRIDGE.md`](./WASM_SIM_BRIDGE.md)

---

## 1. Goal

Pin the **mayor approval loop** as the politics foundation: happiness / services / economy / decisions → `WorldState.ApprovalRating` → WASM status + Herald deltas. Defer full faction UI, protest escalation polish, and Economic Control Spectrum (v2).

---

## 2. Milestones (v1)

| ID | Deliverable | Program map | Current state |
|----|-------------|-------------|---------------|
| **P5.1** | Approval on WASM `GetStatus` + snapshot (percent 0–100) | P6 foundation | **Live** — `WasmStatusDto.Approval`, `SimSnapshotDto.Approval` |
| **P5.2** | `ApplyApprovalDelta` Herald bridge characterization | P6.2 partial | **Live** — `SimHost.ApplyApprovalDelta` / WASM export |
| **P5.3** | Law toggles → budget / traffic / spawn multipliers | P6.1 | **Partial** — `LawEffectsTests`; spawn hooks shallow |
| **P5.4** | `ApplyEventEffectsToState` parity (web + Unity) | P6.2 | **Partial** — WASM day tick applies aggregate happiness/approval |
| **P5.5** | Herald buckets only when snapshot predicates true | P6.3 | **Not implemented** — templates fire without hard sim gates |
| **P5.6** | Faction / council seat export on status | AGENT_07 Phase 2 | **Live** — `councilSeats` on WASM status + snapshot DTO |
| **P5.7** | Economic Control Spectrum slider | P6.4 / SB-3729 | **v2 boundary** |

---

## 3. Existing sim inventory (do not rewrite)

| Component | Path | Role |
|-----------|------|------|
| `PoliticsSystem` | `src/Forge.Game/Simulation/PoliticsSystem.cs` | Approval weights, factions (6), council (9), laws, protests, corruption, elections |
| `LawSystem` | `src/Forge.Game/Simulation/LawSystem.cs` | Catalog + toggles; aggregate effects |
| `EventSystem` | `src/Forge.Game/Simulation/EventSystem.cs` | `ApprovalRatingChange` effect index |
| `SimHost` | `src/Forge.SimCore/SimHost.cs` | Daily/monthly politics ticks; `ApplyApprovalDelta`; feeds Service/Safety scores from coverage |
| `WorldState.ApprovalRating` | `Forge.Engine` | **0–1** internal scale (default 0.6) |
| Desktop UI | `PoliticsPanel.cs` | Factions, laws, protests (ImGui) |

### Approval formula (pinned)

```
approval_0_100 =
    happiness   * 0.30
  + economy     * 0.20
  + services    * 0.15
  + safety      * 0.10
  + decisions   * 0.15
  − scandal     * 0.10
```

`MonthlyTick` writes `state.ApprovalRating = approval_0_100 / 100`.  
Herald / WASM deltas use **percentage points** via `ApplyApprovalDelta(deltaPercent)`.

### Threshold bands (AGENT_07)

| Approval % | Consequence |
|------------|-------------|
| >80 | Bonus funding (`CityFunds += CityFunds / 20`) |
| 60–80 | Normal |
| 40–60 | Protest escalation possible |
| 20–40 | Strikes / council friction (partial) |
| <20 | Forced election scheduling |

---

## 4. WASM / client data contract

### 4.1 Live (P5.1)

```json
{
  "approval": 62.5,
  "happiness": 0.55,
  "activeLawCount": 2,
  "lawDefinitionCount": 12,
  "systems": ["…", "PoliticsSystem", "LawSystem", "EventSystem", "…"]
}
```

| Field | Scale | Source |
|-------|-------|--------|
| `approval` | 0–100 | `ApprovalRating * 100` |
| `happiness` | 0–1 | `WorldState.Happiness` |
| Snapshot `approval` | 0–100 | `SimSnapshotDto.From` |
| Native `SimSnapshot.ApprovalRating` | 0–1 | Desktop / Unity bridge |

### 4.2 Live (P5.6)

```typescript
interface PoliticsStatusStub {
  approval: number;           // 0–100
  corruptionIndex?: number;   // 0–100
  protestPhase?: string;      // None…Riot
  councilSeats?: number[];    // faction id per seat (length 9)
  nextElectionYear?: number;
}
```

`councilSeats` is exported on `WasmStatusDto` / `SimSnapshotDto` (JSON `councilSeats`). Corruption / protest / next election remain deferred. Keep payload &lt;200 bytes/tick until HUD consumes seats.

---

## 5. Herald bridge

Web Herald options emit `approval_event` with `approvalDelta` (`herald-option-commands.ts` → sim-worker → `ApplyApprovalDelta`).

**P5.5 acceptance:** Herald crisis / prosperity buckets must read live `approval` / treasury / RCI from snapshot predicates (`sim-metrics.ts`) — no false-positive unrest when approval ≥ threshold.

---

## 6. Characterization tests

| Test | Intent | Status |
|------|--------|--------|
| `ApplyApprovalDelta_MovesSnapshotApproval` | Delta percent → 0–1 snapshot | **Pinned** (`CathedralPoliticsTests`) |
| `SnapshotDto_Approval_IsPercentScale` | DTO / JSON scale 0–100 | **Pinned** (same scale as WASM `GetStatus`) |
| `GetSnapshotJson_IncludesApprovalField` | JSON contract field present | **Pinned** |
| `HeraldUnrestBucket_RequiresApprovalBelowThreshold` | Bucket gated on snapshot | Skip until P5.5 |
| `WasmStatus_ExportsCouncilSeats` | Council seats on WASM | **Pinned** |

Unit math already covered in `Forge.Game.Tests/PoliticsSystemTests` (weights, elections, clout).

---

## 7. Non-goals (this slice)

- Rewriting `PoliticsSystem` or faction AI
- Full 70+ law catalog from `POLITICAL_LAW_SYSTEM.md`
- Economic Control Spectrum UI
- New Unity/web Politics panel beyond approval HUD already present

---

## 8. Implementation order

1. **P5.1–P5.2** — this stub (export + delta tests) ✅  
2. **P5.3** — deepen law→spawn multipliers; unskip/extend `LawEffectsTests`  
3. **P5.4** — prove event aggregate effects match desktop in Unity + web  
4. **P5.5** — Herald predicate hard gates  
5. **P5.6** — council/faction status fields on WASM status + snapshot ✅
