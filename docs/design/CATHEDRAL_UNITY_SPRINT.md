# Cathedral Sprint 3 — Unity UI/HUD + tools parity

**Status:** Active next sprint (Unity-first)  
**Dates:** 2026-08-11 → 2026-08-24 (overlaps Sprint 2 close-out)  
**Integration branch:** `feat/unity-port-plan-2026-07-12` (`citymajor-unity-port-plan`)  
**Charter:** [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md) §0 Platform mandate  
**Orchestration (canonical):** [`UNITY_ORCHESTRATION.md`](./UNITY_ORCHESTRATION.md)  
**Play gate:** [`UNITY_PLAY_CHECKLIST.md`](./UNITY_PLAY_CHECKLIST.md)

**Platform rule:** Player experience ships in **Unity Editor + player builds** only. Web R3F is archival/spike. WASM may run as a **sim validation harness** — it does not define UX.

---

## 1. Sprint goal

Sprint 1–2 landed Cathedral **sim depth** (roads, zoning, goods, O-D, commute satisfaction) and several **web** HUD/overlays as early mirrors. Sprint 3 closes the **player gap**: bind those snapshot fields in the Unity client so Play mode is the truthful cathedral surface.

**Primary pillar:** P7 Client truth (Unity HUD/tools)  
**Secondary:** Finish Sprint 2 carry-over **P3.3** (SimCore) if not already cherry-picked  
**Exit gate:** Cathedral economy / traffic / housing fields visible and operable in Unity Play; Play checklist subset green; no new web R3F player features

---

## 2. Why this sprint exists

| Already in sim / snapshot | Often first shown on web spike | Unity product need |
|---------------------------|--------------------------------|--------------------|
| Road tiers, ramp/bridge/tunnel | Web toolbar | `RoadPaintTool` inspector + tool-mode HUD parity |
| Goods / partition prices / chains | Web Economy HUD | Unity Economy / Trade panels |
| Inter-zone friction | Web overlay | Unity GL overlay (or panel readouts) |
| Congestion `edgeVolumes` / `travelTimes` | Web heatmap | Unity `EdgeTrafficOverlay` / congestion mode |
| Home/work O-D coverage | Web O-D HUD | Unity citizen / economy sample strip |
| Extended zones + density | Web brushes | Unity zone paint + density |

---

## 3. Milestones

### U3.1 — Road tools parity (P1 UX → Unity)

| Field | Value |
|-------|-------|
| **Owner** | Unity Input + UI lanes |
| **Deliverable** | Road tier select; bridge / tunnel / ramp paint modes discoverable in Play (not inspector-only) |
| **Acceptance** | PlaceRoad flags match SimHost; ToolMode HUD shows active road mode; Play checklist road items pass |
| **Depends** | P1.1–P1.6 ✅ |

### U3.2 — Economy HUD truth (P3 → Unity)

| Field | Value |
|-------|-------|
| **Owner** | Unity UI lane |
| **Deliverable** | Goods shortages/surpluses, prices, production-chain drill-down, partition price spread (once P3.3 lands) |
| **Acceptance** | Building click or Economy panel (`E`) shows chain + prices from snapshot; matches SimCore characterization |
| **Depends** | P3.1–P3.2 ✅; P3.3 for multi-partition spread |

### U3.3 — Traffic / friction overlays (P1/P3 → Unity)

| Field | Value |
|-------|-------|
| **Owner** | Unity Rendering lane |
| **Deliverable** | Congestion from edge volumes/times; optional friction corridor highlight |
| **Acceptance** | Overlay toggles in Play; visually consistent with snapshot samples |
| **Depends** | P1.6 ✅ · P3.4 fields ✅ |

### U3.4 — Zoning / density tools (P2 → Unity)

| Field | Value |
|-------|-------|
| **Owner** | Unity Input |
| **Deliverable** | Office / mixed / park / ag paint + density brush wired to `SimHost.PaintZone` |
| **Acceptance** | Era gates honored; RCI + growth characterization still green |
| **Depends** | P2.1 ✅ |

### U3.5 — Population / commute readouts (P4 → Unity)

| Field | Value |
|-------|-------|
| **Owner** | Unity UI |
| **Deliverable** | O-D coverage + commute→satisfaction sample in HUD or citizen panel |
| **Acceptance** | Player can see that home/work pairs and commute time affect happiness |
| **Depends** | P4.1–P4.2 ✅ |

### U3.6 — Play gate hardening (P7.3)

| Field | Value |
|-------|-------|
| **Owner** | Orchestrator + human |
| **Deliverable** | Update [`UNITY_PLAY_CHECKLIST.md`](./UNITY_PLAY_CHECKLIST.md) for Cathedral Sprint 2/3 surfaces; Editor menu verify where possible |
| **Acceptance** | Checklist subset signed for integration tip before next sim-depth sprint |

### S2 carry — P3.3 market partitions (SimCore)

| Field | Value |
|-------|-------|
| **Owner** | SimCore lane |
| **Deliverable** | 8–16 market zones with distinct partition prices; unskip/green characterization |
| **Acceptance** | See [`CATHEDRAL_SPRINT2.md`](./CATHEDRAL_SPRINT2.md) §P3.3 |
| **Note** | Sim-only; Unity U3.2 binds the export once landed |

---

## 4. Lane dispatch

| Lane | Branch prefix | Owns | Merge gate |
|------|---------------|------|------------|
| **Unity UI / HUD** | `feat/cathedral-u3-hud-*` | `Assets/Scripts/UI/`, `Assets/UI/*` | Play — panels toggle, no red console |
| **Unity Input / tools** | `feat/cathedral-u3-tools-*` | `Assets/Scripts/Input/` | Play — paint modes + ToolMode HUD |
| **Unity Rendering** | `feat/cathedral-u3-overlay-*` | `Assets/Scripts/Rendering/` overlays | Play — overlay toggles |
| **SimCore** | `feat/cathedral-p3-market-*` | P3.3 only | `dotnet test --filter CathedralEconomy` |
| **Harness (optional)** | `feat/cathedral-harness-*` | WASM smoke / snapshot asserts | Does **not** block Unity ship |

Follow merge protocol in [`UNITY_ORCHESTRATION.md`](./UNITY_ORCHESTRATION.md). Bootstrap edits stay orchestrator-owned.

---

## 5. Out of scope

- New web R3F HUD, toolbar, or overlay features (archival spike — do not extend for product)
- Photoreal / skeletal NPCs / multiplayer
- P3.5 bilateral trade, P5.b fire spread, P6 law depth (later phases)
- Rewriting sim into DOTS/ECS

---

## 6. Acceptance checklist

- [ ] Unity Play: road tier + bridge/tunnel/ramp modes usable without digging inspector-only fields
- [ ] Unity Play: Economy panel shows goods + chain (+ partition spread if P3.3 merged)
- [ ] Unity Play: congestion overlay reads snapshot edge data
- [ ] Unity Play: extended zones + density paint work under era gates
- [x] Unity Play: O-D / commute satisfaction sample visible (Cathedral HUD — tip after U3.5 bind)
- [ ] [`UNITY_PLAY_CHECKLIST.md`](./UNITY_PLAY_CHECKLIST.md) Cathedral subset signed on integration tip
- [ ] `dotnet test --filter "FullyQualifiedName~Cathedral"` green
- [ ] No commits that treat web `/play` as the player ship surface

---

## 7. Related docs

| Doc | Role |
|-----|------|
| [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md) | Program charter + Unity-first §0 |
| [`CATHEDRAL_SPRINT2.md`](./CATHEDRAL_SPRINT2.md) | Prior sprint (P3.3 carry) |
| [`UNITY_ORCHESTRATION.md`](./UNITY_ORCHESTRATION.md) | **Canonical** Unity tracker |
| [`UNITY_V1_SCOPE.md`](./UNITY_V1_SCOPE.md) | Locked Steam EA scope |
| [`UNITY_AGENT_DISPATCH.md`](./UNITY_AGENT_DISPATCH.md) | Lane file ownership |
| [`WEB_V1_SCOPE.md`](./WEB_V1_SCOPE.md) | Superseded / archival |
