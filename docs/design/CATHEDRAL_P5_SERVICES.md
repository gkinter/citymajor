# Cathedral P5 — Services & Utilities Foundation

**Status:** Spec + characterization (P5.1 coverage export live)  
**Program:** [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md) pillar **P5 — Services**  
**Linear:** [SB-4987](https://linear.app/softblaze/issue/SB-4987)  
**Prerequisites:** ServiceSystem daily grids · L0 partition balance  
**References:** [`AGENT_06_SERVICES.md`](./AGENT_06_SERVICES.md) · [`WASM_SIM_BRIDGE.md`](./WASM_SIM_BRIDGE.md) · [`SIM_FOUNDATION_CHARTER.md`](./SIM_FOUNDATION_CHARTER.md)

---

## 1. Goal

Surface **power / water coverage** from the L0 utility partition balance so players (and Herald) can see blackouts and shortages. Wire **emergency response time** from road-graph distance × BPR congestion (P5.2). Ship **fire v1** hydrant coverage + spread (P5.3). Ship **EMS survival** from response minutes (P5.4).

---

## 2. Milestones (v1)

| ID | Deliverable | Current state |
|----|-------------|---------------|
| **P5.1** | Utilities L0 balance on WASM status + HUD % | **Live** — `WorldState.PowerCoverageFraction` / `WaterCoverageFraction` → `WasmStatusDto` + ResourcesHud `Util` line |
| **P5.2** | Emergency response time = distance + traffic | **Live** — `EmergencyResponseTime` + mean minutes on `WasmStatusDto` / Resources+Economy HUD |
| **P5.3** | Fire v1 (spread + hydrant coverage) | **Live** — `FireResponse` + `HydrantCoverageFraction` / `ActiveFireCount` on snapshot + ResourcesHud Fire line |
| **P5.4** | EMS survival curve from response minutes | **Live** — `EmsSurvival` + `MeanEmsSurvivalRate` on snapshot + ResourcesHud `EMS Xm · N%` |

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
- `GetStatus` exports `meanEmergencyResponseMinutes` below the no-station default when a fire station covers zoned tiles

### 4.3 P5.2 response formula

```
responseMinutes =
  roadGraphPathCost(station → incident, BPR edge times) / vehicleSpeed
  + crewReadiness
```

`pathCost` uses Dijkstra on `WorldState.Roads` with `RoadEdgeTravelTimes` when present; otherwise free-flow edge costs; Euclidean tile distance as last resort. `ServiceSystem.CalculateFireResponseTime` forwards to `EmergencyResponseTime`. City-wide mean is sampled on a stride during `DailyTick` and exported as `meanEmergencyResponseMinutes` for ResourcesHud (`EMS`) and Economy Services.

### 4.4 Live (P5.2 HUD)

```json
{
  "meanEmergencyResponseMinutes": 4.2
}
```

Worker maps the key onto `SimResources`; ResourcesHud shows `EMS 4.2m` when present.

### 4.5 Live (P5.3 fire v1)

```json
{
  "hydrantCoverageFraction": 0.72,
  "activeFireCount": 2
}
```

- Hydrants = buildings with `ServiceHydrant` (`1 << 9`); coverage radius **3 tiles**
- No hydrant at incident → response minutes **×2** (shuttle water)
- Spread chance: `base * material * wind * adjacency * hydrant_inverse` (AGENT_06)
- Burning intensity stored in `BuildingData.FireRisk`; Unity ResourcesHud shows `🚰 {hydrant%} · 🔥 {n}` when fires active

`CathedralUtilitiesTests` pins hydrant response/spread math, adjacent ignition, and snapshot export.

### 4.6 Live (P5.4 EMS survival)

```json
{
  "meanEmsSurvivalRate": 0.72
}
```

- Curve (`MISSING_SYSTEMS` §1.2): &lt;5 min → 90%, 5–10 → 70%, 10–15 → 55%, ≥15 → 40%
- City mean = average of per-tile survival over the same zoned sample as `meanEmergencyResponseMinutes`
- Unity ResourcesHud EMS line: `{minutes} · {survival%}`

`CathedralUtilitiesTests` pins bucket thresholds, faster response → higher mean survival, and snapshot export.

## 5. Out of scope (this stub)

- Wildfire / arson rings / hospital capacity coupling (`MISSING_SYSTEMS.md` depth)
- Sewage / internet / waste as separate HUD meters
- Rewriting tile BFS grids — keep daily ServiceSystem path
