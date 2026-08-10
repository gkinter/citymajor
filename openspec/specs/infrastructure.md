# Infrastructure (OpenSpec domain spec)

> **Status:** Stub — points to Tier 1 architecture.  
> **Cathedral pillar:** P1 Infrastructure Truth  
> **Tier 1 sources:** [`SIMULATION_ARCHITECTURE.md`](../../docs/design/SIMULATION_ARCHITECTURE.md) §4 · [`AGENT_04_TRANSPORT.md`](../../docs/design/AGENT_04_TRANSPORT.md)

## Scope

Road graph, tiers, intersections, traffic assignment (BPR + Frank-Wolfe), capacity tables, snapshot export of edge volumes and travel times.

## Implemented requirements

*(Populated as OpenSpec changes archive into this file.)*

## Target milestones

- P1.1 — Road tier in `PlaceRoad` + `RoadFlags`
- P1.2 — Graph builder v2 (intersection-segment topology)
- P1.3 — Bridge/tunnel/one-way flags
- P1.4 — Highway ramps
- P1.5 — Full traffic default (Unity)
- P1.6 — Snapshot export

## Active change

[`001-road-tier-and-graph-v2`](../changes/001-road-tier-and-graph-v2/) — P1.1 + P1.2
