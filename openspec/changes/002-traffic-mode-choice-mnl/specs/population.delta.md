# Population delta — P4 mode choice + traffic BPR/FW

**Change:** `002-traffic-mode-choice-mnl`  
**Base spec:** [`openspec/specs/population.md`](../../specs/population.md)  
**Cathedral:** [`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) §4 P4 (esp. P4.5)

---

## ADDED Requirements

### Requirement: Distance-based mode-choice stub (P4.5)

`WasmTrafficLite` SHALL split each O-D trip demand across car, transit, and walk using a multinomial logit of distance-based utilities (not a flat car share).

#### Scenario: Mid-distance car share near legacy default

- **WHEN** O-D tile distance is approximately 10–20
- **THEN** car mode probability SHALL be in \[0.55, 0.75\]
- **AND** car share SHALL exceed transit and walk

#### Scenario: Short trips favor walk more than long trips

- **WHEN** distance is very short (≈2 tiles) versus long (≈40 tiles)
- **THEN** walk share for the short trip SHALL be greater than for the long trip
- **AND** long-trip walk share MAY be zero when walk time exceeds the stub maximum

#### Scenario: City shares sum to one

- **WHEN** a lite traffic tick completes with a non-empty O-D matrix
- **THEN** `CarModeShare + TransitModeShare + WalkModeShare` SHALL equal 1 within floating tolerance

---

### Requirement: Shared BPR edge travel time

The simulation SHALL compute edge travel times with the Bureau of Public Roads formula α=0.15, β=4 via a shared helper used by lite and full traffic.

#### Scenario: Congestion raises travel time

- **WHEN** assigned volume approaches or exceeds edge capacity
- **THEN** BPR travel time SHALL be strictly greater than free-flow time

---

### Requirement: Multi-iteration Frank-Wolfe for lite traffic

Lite traffic assignment SHALL run up to three Frank-Wolfe iterations and MAY stop early when relative gap is below 1%.

#### Scenario: Convergence export

- **WHEN** a full lite assignment completes
- **THEN** per-edge `edgeVolumes[]` and `travelTimes[]` SHALL be available on the road-graph snapshot / WASM status (parallel to graph edge index)

---

## MODIFIED Requirements

### Requirement: Car demand into road assignment

Only the **car** fraction from the mode-choice stub SHALL be loaded onto the road graph for Frank-Wolfe assignment. Transit and walk contribute to reported mode shares only until a transit network assignment exists.

---

## NOT IN SCOPE (deferred)

- Full ASC from household `TransitPreference` / wealth
- Transit network Frank-Wolfe
- Mode-share HUD (client)
- Raising WASM zone count to desktop (~500)
