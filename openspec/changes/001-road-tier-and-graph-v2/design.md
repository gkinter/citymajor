# Design: Road Tier + Graph v2

## RoadFlags encoding (existing)

`TileData.RoadFlags`:

| Bits | Meaning |
|------|---------|
| 0–3 | N/E/S/W connectivity |
| 4–5 | Road level (tier 0–3) |
| 6 | Bridge |
| 7 | Tunnel |

## P1.1 — Tier plumbing

### SimHost API

```csharp
void PlaceRoad(int x, int y, byte tier = 1, byte extraFlags = 0);
```

- `ComputeRoadFlags` ORs tier into bits 4–5.
- `RebuildRoadGraphFromTiles`: for each edge, `level = min(tierA, tierB)` or max per AGENT_04 capacity table.
- `RoadCapacityForLevel(byte level)`: dirt 200, paved 800, highway 2200 veh/hr/lane (per `SIMULATION_ARCHITECTURE` §4).

### Cost table (travel time multiplier inverse)

| Tier | Label | Cost mult |
|------|-------|-----------|
| 0 | Dirt | 1.5 |
| 1 | Cobblestone | 1.0 |
| 2 | Asphalt | 0.85 |
| 3 | Highway | 0.7 |

### WASM / web ABI

- Prefer optional `tier` on existing `place_road` JSON command (backward compatible default `1`).
- If ABI break unacceptable, add `place_road_tier` export — document choice in PR.

## P1.2 — Graph builder v2 (intersection segments)

### Current (per-tile nodes)

Every road tile becomes a node; edges between adjacent tiles. Loses intersection typing.

### Target

1. Detect intersection tiles (degree ≥ 3, or T-junction, or ramp node).
2. Collapse degree-2 chains into single edges between intersection nodes.
3. Store `RoadNode.NodeType` (Intersection, Ramp, DeadEnd) for future P1.4.
4. Edge capacity = `RoadCapacityForLevel(level) * laneCount` (default 1 lane until P1.4).

### Migration

- Phase A (this change): tier-aware per-tile graph (P1.1 only) — ship first.
- Phase B: segment collapse behind feature flag `UseSegmentGraph` — same PR if low risk, else follow-up change `002`.

## Testing

`RoadTierGraphTests.cs`:

1. `PlaceRoad_tier0_vs_tier2_different_edge_levels`
2. `RoadCapacityForLevel_monotonic_increase`
3. (P1.2) `SegmentGraph_four_way_has_single_intersection_node`

## Risks

- WASM ABI size — keep tier as single byte in existing command.
- Save compatibility — tier in `RoadFlags` is backward compatible (defaults to 1).
