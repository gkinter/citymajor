# Services delta — P5.2 emergency response × BPR

**Change:** `003-emergency-response-bpr`  
**Base spec:** [`openspec/specs/services.md`](../../specs/services.md)  
**Cathedral:** [`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) §4 P5 (esp. P5.2)

---

## ADDED Requirements

### Requirement: Emergency response minutes from road path + BPR (P5.2)

The simulation SHALL compute emergency response time in minutes from the nearest matching service station to an incident tile using road-graph path cost and BPR edge travel times when available.

#### Scenario: Distance raises free-flow response

- **WHEN** a fire station and two incidents lie on the same paved corridor
- **AND** edge travel times are free-flow (or omitted)
- **THEN** response minutes to the farther incident SHALL be strictly greater than to the nearer incident

#### Scenario: Congestion raises response vs free-flow

- **WHEN** the same far incident is evaluated with inflated BPR `edgeTravelTimes[]` along the path
- **THEN** response minutes SHALL be strictly greater than the free-flow evaluation for that incident

#### Scenario: ServiceSystem facade matches helper

- **WHEN** `WorldState.RoadEdgeTravelTimes` holds the congested edge array
- **THEN** `ServiceSystem.CalculateFireResponseTime` SHALL equal `EmergencyResponseTime.CalculateMinutes` for the same tile (within floating precision)

#### Scenario: No station fallback

- **WHEN** no active building has the requested service flag
- **THEN** response minutes SHALL be the configured no-station constant (30)

---

### Requirement: Prefer traffic-assigned edge times

Fire response SHALL pass `state.RoadEdgeTravelTimes` into path costing so the latest traffic assignment (lite or full) influences service outcomes without a separate services-side traffic solve.

#### Scenario: Missing travel-time array

- **WHEN** `RoadEdgeTravelTimes` is null or empty
- **THEN** path costing SHALL use free-flow graph edge costs, then Euclidean distance if the graph path is unavailable

---

## NOT IN SCOPE (deferred)

- P5.3 fire spread and hydrant coverage
- EMS survival curves / hospital capacity coupling
- Client HUD readout of response minutes
- Changes to P5.1 power/water coverage fractions
