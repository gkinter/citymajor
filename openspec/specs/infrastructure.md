# Infrastructure (OpenSpec domain spec)

> **Status:** Implemented (P1.1–P1.6) — archived from `001-road-tier-and-graph-v2`  
> **Cathedral pillar:** P1 Infrastructure Truth  
> **Tier 1 sources:** [`SIMULATION_ARCHITECTURE.md`](../../docs/design/SIMULATION_ARCHITECTURE.md) §4 · [`AGENT_04_TRANSPORT.md`](../../docs/design/AGENT_04_TRANSPORT.md)  
> **Archive:** [`../changes/archive/2026-08-10-001-road-tier-and-graph-v2/`](../changes/archive/2026-08-10-001-road-tier-and-graph-v2/)

## Scope

Road graph, tiers, intersections, traffic assignment (BPR + Frank-Wolfe), capacity tables, snapshot export of edge volumes and travel times.

## Implemented requirements

### Requirement: Road tier in placement API

The simulation SHALL accept a road tier (0–3) when placing a road tile and SHALL persist the tier in `TileData.RoadFlags` bits 4–5.

#### Scenario: Web toolbar selects asphalt tier

- **WHEN** the player paints a road with tier 2 (asphalt) at tile (x, y)
- **THEN** `RoadFlags` bits 4–5 encode tier 2
- **AND** a subsequent graph rebuild sets adjacent edge level from the stored tier

#### Scenario: Default tier backward compatibility

- **WHEN** `PlaceRoad(x, y)` is called without tier
- **THEN** tier defaults to 1 (cobblestone/paved)

### Requirement: Edge capacity varies by road tier

The road graph SHALL assign edge capacity from a tier table when rebuilding from tiles.

#### Scenario: Dirt vs highway capacity

- **WHEN** two adjacent road tiles both have tier 0 (dirt)
- **THEN** the connecting edge capacity SHALL be ≤ 250 veh/hr/lane equivalent
- **WHEN** both tiles have tier 3 (highway)
- **THEN** the connecting edge capacity SHALL be ≥ 2000 veh/hr/lane equivalent

#### Scenario: Mixed tier edge

- **WHEN** adjacent tiles have tiers 0 and 2
- **THEN** edge level SHALL be derived from the minimum (bottleneck) tier unless spec table dictates otherwise

### Requirement: Graph segments between intersections (P1.2)

The road graph builder SHALL collapse degree-2 tile chains into edges between intersection nodes.

#### Scenario: Four-way intersection

- **WHEN** a 4-way road crossing exists on the tile grid
- **THEN** exactly one intersection node represents the crossing
- **AND** each arm is a single graph edge (not one edge per tile in the chain)

#### Scenario: T-junction typing

- **WHEN** a T-junction exists (three connected arms)
- **THEN** the node type SHALL be `Intersection`
- **AND** edge capacities SHALL reflect arm tiers

### Requirement: WASM place_road command

The WASM `place_road` command SHALL accept an optional `tier` field (0–3) in the JSON payload.

#### Scenario: Web sends tier

- **WHEN** the web client sends `{ "cmd": "place_road", "x": 10, "y": 20, "tier": 2 }`
- **THEN** the sim places road tier 2 at (10, 20)

### Requirement: Bridge / tunnel / ramp UX (P1.3–P1.5)

- Bridge and tunnel flags affect graph cost/capacity (`RoadFlags` bits 6–7).
- Highway ramp nodes typed in graph; dedicated ramp paint tool + bridge/tunnel toolbar toggle on web.
- Illegal highway merges rejected with HUD toast.

### Requirement: Snapshot export (P1.6)

- Compact `nodeTypes[]`, `edgeVolumes[]`, and `travelTimes[]` exported for congestion overlay / vehicle speed.

## Target milestones

| Milestone | Status |
|-----------|--------|
| P1.1 — Road tier in `PlaceRoad` + `RoadFlags` | ✅ |
| P1.2 — Graph builder v2 (intersection-segment topology) | ✅ |
| P1.3 — Bridge/tunnel/one-way flags | ✅ |
| P1.4 — Highway ramps | ✅ |
| P1.5 — Traffic default (Unity) + ramp/bridge toolbar UX | ✅ |
| P1.6 — Snapshot export | ✅ |

## Active change

None — `001-road-tier-and-graph-v2` archived 2026-08-10.
