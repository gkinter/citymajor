# Design: Emergency Response Time × BPR

## Cathedral linkage

[`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) **P5 — Services**:

| Milestone | This change |
|-----------|-------------|
| P5.1 utilities coverage | **Unchanged** — power/water fractions stay on L0 balance |
| **P5.2** emergency response | **Delivered** — graph path + BPR edge times |
| P5.3 fire spread / hydrants | **Deferred** |

Depends on traffic exporting per-edge times (`RoadEdgeTravelTimes` / snapshot `travelTimes[]` from change 002 / P4 FW).

## Formula

```
responseMinutes =
  pathCost(station → incident) / vehicleSpeedTilesPerMinute
  + crewReadinessMinutes
```

Defaults: `vehicleSpeed = 2` tiles/min · `crewReadiness = 1` min · no station → `30` min.

### Path cost priority

1. Resolve nearest road nodes for station and incident.
2. Dijkstra on `WorldState.Roads`: edge cost = `edgeTravelTimes[ei]` when the array is present and indexed with graph neighbor order; else free-flow neighbor cost.
3. If graph path unavailable → Euclidean tile distance.

Stations: active buildings whose `ServiceFlags` include fire (`1<<3`) or health (`1<<4`).

## ServiceSystem integration

`CalculateFireResponseTime(state, x, y)` calls:

```csharp
EmergencyResponseTime.CalculateMinutes(
    state, x, y,
    EmergencyResponseTime.ServiceFire,
    state.RoadEdgeTravelTimes);
```

Congestion from the latest lite/full assignment therefore raises fire response without a second traffic pass inside services.

## Characterization pins

Corridor test (paved road, station west, incidents mid/east):

- Near free-flow minutes &lt; far free-flow minutes
- Far congested (BPR-scaled edge times) &gt; far free-flow
- ServiceSystem facade == helper at equal precision

## Risks / follow-ups

- Edge-time index must stay aligned with `GetNeighbors` enumeration order (same contract as commute / FW export).
- No station coverage HUD yet — response minutes are sim-side only until a client surface lands.
- Health/EMS uses the same helper API but fire facade is the primary P5.2 wire.
