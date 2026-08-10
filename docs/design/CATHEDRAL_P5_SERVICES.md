# Cathedral P5 — Services & Utilities Foundation

**Status:** Spec + characterization (P5.1 coverage export live)  
**Program:** [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md) pillar **P5 — Services**  
**Linear:** [SB-4987](https://linear.app/softblaze/issue/SB-4987)  
**Prerequisites:** ServiceSystem daily grids · L0 partition balance  
**References:** [`AGENT_06_SERVICES.md`](./AGENT_06_SERVICES.md) · [`WASM_SIM_BRIDGE.md`](./WASM_SIM_BRIDGE.md) · [`SIM_FOUNDATION_CHARTER.md`](./SIM_FOUNDATION_CHARTER.md)

---

## 1. Goal

Surface **power / water coverage** from the L0 utility partition balance so players (and Herald) can see blackouts and shortages. Wire **emergency response time** from road-graph distance × BPR congestion (P5.2). Defer fire-spread depth (P5.3).

---

## 2. Milestones (v1)

| ID | Deliverable | Current state |
|----|-------------|---------------|
| **P5.1** | Utilities L0 balance on WASM status + HUD % | **Live** — `WorldState.PowerCoverageFraction` / `WaterCoverageFraction` → `WasmStatusDto` + ResourcesHud `Util` line |
| **P5.2** | Emergency response time = distance + traffic | **Live** — `EmergencyResponseTime` + `ServiceSystem.CalculateFireResponseTime` use road graph / BPR edge times |
| **P5.3** | Fire v1 (spread + hydrant coverage) | **Not started** (post-EA Phase 5b) |

---

## 3. Existing sim inventory (do not rewrite)

| Component | Path | Role |
|-----------|------|------|
| `UtilityPartitionBalance` | `src/Forge.Game/Simulation/UtilityPartitionBalance.cs` | Per-partition power/water supply vs demand on L0 tick |
| `ServiceSystem` | `src/Forge.Game/Simulation/ServiceSystem.cs` | Daily `RecalculatePowerGrid` / `RecalculateWaterGrid` BFS; owns `UtilityBalance` |
| `TileData.PowerGrid` / `WaterGrid` | `Forge.Engine` | Binary connected flags (0/1) for overlays |
| `WorldState.*CoverageFraction` | `Forge.Engine` | **0–1** city-wide partition coverage (default 1) |
| `UtilityCoverageExport` | `Forge.SimWasm` | Stepped tile samples for Unity GL overlay (**U** key) |
| Web HUD | `web/components/city/ResourcesHud.tsx` | `⚡ {power%} · 💧 {water%}` from status fractions × 100 |
| Unity HUD | `ResourcesHudController` | Same percent readout |

### Coverage formula (pinned)

```
powerCoverageFraction = poweredPartitions / partitionCount   // supply ≥ demand
waterCoverageFraction = wateredPartitions / partitionCount
utilityStressIndex    = max(blackoutFraction, waterShortageFraction)  // smoothed
```

Internal scale is **0–1**. HUD shows **percent** (`Math.round(fraction * 100)`).

---

## 4. WASM / client data contract

### 4.1 Live (P5.1)

`GetStatus` / snapshot JSON (camelCase):

```json
{
  "powerCoverageFraction": 0.875,
  "waterCoverageFraction": 0.75,
  "utilityStressIndex": 0.18
}
```

Worker maps the same keys onto `SimResources`; ResourcesHud renders when either coverage field is present.

### 4.2 Characterization

`CathedralUtilitiesTests` pins:

- Snapshot JSON includes `powerCoverageFraction` / `waterCoverageFraction` in `[0, 1]`
- `SimSnapshotDto` mirrors `WorldState` utility fields
- L0 tick without plants drops coverage below 1; plants restore local coverage
- Emergency response minutes rise with road-graph distance and BPR congestion (`EmergencyResponseTime_UsesDistancePlusTraffic`)

### 4.3 P5.2 response formula

```
responseMinutes =
  roadGraphPathCost(station → incident, BPR edge times) / vehicleSpeed
  + crewReadiness
```

`pathCost` uses Dijkstra on `WorldState.Roads` with `RoadEdgeTravelTimes` when present; otherwise free-flow edge costs; Euclidean tile distance as last resort. `ServiceSystem.CalculateFireResponseTime` forwards to `EmergencyResponseTime`.

---

## 5. Out of scope (this stub)

- Full EMS survival curves / hydrant graph (`MISSING_SYSTEMS.md`)
- Sewage / internet / waste as separate HUD meters
- Rewriting tile BFS grids — keep daily ServiceSystem path
