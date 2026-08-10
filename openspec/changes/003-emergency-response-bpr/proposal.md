# Change 003: Emergency Response Time × BPR

**OpenSpec change:** `003-emergency-response-bpr`  
**Cathedral milestones:** **P5.2** (emergency response = distance + traffic)  
**Status:** Implemented (documenting landed work)  
**Linear:** SB-4242 (P5.2) · pillar epic SB-4987 (P5 Services)

---

## Problem

Fire/EMS response used Euclidean (or free-flow-only) distance and ignored congestion. Cathedral **P5.2** requires response minutes to rise when the road path is longer **and** when BPR edge travel times are elevated, so traffic truth from P4 feeds service outcomes. Characterization of that coupling was skipped until the shared helper landed.

## Proposal

1. **`EmergencyResponseTime` (SimCore)** — Nearest station (fire / health flags) → road-graph Dijkstra using `RoadEdgeTravelTimes` (BPR) when present; free-flow edge costs next; Euclidean tile distance last. `pathCost / vehicleSpeed + crewReadiness`.
2. **`ServiceSystem` facade** — `CalculateFireResponseTime` forwards to `EmergencyResponseTime` with `state.RoadEdgeTravelTimes` so the latest traffic assignment raises minutes.
3. **Characterization** — Unskip / pin `EmergencyResponseTime_UsesDistancePlusTraffic` (near &lt; far free-flow; congested &gt; free-flow; ServiceSystem matches helper).

## Non-goals (this change)

- Fire spread / hydrant graph (P5.3)
- Full EMS survival curves
- Client HUD for response minutes (follow-up)
- Replacing daily power/water BFS grids (P5.1 stays as-is)

## Success criteria

- Farther station→incident path yields higher free-flow response minutes than a nearer incident on the same corridor.
- Inflating BPR edge times along the path strictly increases response minutes vs free-flow.
- `ServiceSystem.CalculateFireResponseTime` equals `EmergencyResponseTime.CalculateMinutes` for the same state / edge times.
- `CathedralUtilitiesTests.EmergencyResponseTime_UsesDistancePlusTraffic` green.

## Surfaces touched

| Surface | Change |
|---------|--------|
| `Forge.SimCore` | New `EmergencyResponseTime` helper |
| `Forge.Game` / `ServiceSystem` | Fire response facade → helper + BPR edge times |
| `WorldState` | Consumes `RoadEdgeTravelTimes` (from traffic) |
| `Forge.SimCore.Tests` | `CathedralUtilitiesTests` P5.2 characterization |
| Docs | `CATHEDRAL_P5_SERVICES.md` §4.3 · `CATHEDRAL_PROGRAM.md` P5.2 ✅ |

## References

- [`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) §4 **P5 — Services** (**P5.2**)
- [`CATHEDRAL_P5_SERVICES.md`](../../../docs/design/CATHEDRAL_P5_SERVICES.md)
- Prior change: [`002-traffic-mode-choice-mnl`](../002-traffic-mode-choice-mnl/) (BPR / `travelTimes[]` foundation)
- [`openspec/specs/services.md`](../../specs/services.md)
