# Tasks: 001-road-tier-and-graph-v2

## P1.1 — Road tier API

- [x] Add `PlaceRoad(int x, int y, byte tier = 1)` to `SimHost`
- [x] Update `ComputeRoadFlags` to encode tier in bits 4–5
- [x] `RebuildRoadGraphFromTiles`: read tier from flags; set edge level + cost
- [x] Add `RoadCapacityForLevel(byte level)` helper
- [x] WASM: accept tier on `place_road` command
- [x] Web: extend `sim-bridge` + `sim-worker` + road toolbar
- [x] Tests: `RoadTierGraphTests` (tier → level/capacity)

## P1.2 — Graph builder v2 (start)

- [x] Document intersection detection algorithm in `RoadGraphBuilder`
- [x] Implement segment collapse between intersection nodes
- [x] Assign `RoadNode.NodeType` for 4-way and T-junction
- [x] Tests: intersection node count on cross pattern
- [x] Feature flag `UseSegmentGraph` (default off until validated)

## P1.3 — Bridge/tunnel flags

- [x] Define `RoadFlags.Bridge` and `RoadFlags.Tunnel` (bits 6–7)
- [x] `PlaceRoad`: optional bridge/tunnel bool (WASM + web stub; default false)
- [x] `RoadGraphBuilder`: segment cost multiplier — bridge ×1.15, tunnel ×1.25
- [x] Tests: bridge/tunnel tiles increase segment travel cost

## P1.4 — Highway ramp nodes (partial)

- [x] `RoadNodeType` enum: Intersection, DeadEnd, Corner, Ramp, HighwayOn, HighwayOff
- [x] Store node type on graph nodes during `RoadGraphBuilder` classification
- [x] Tier-boundary heuristic: highway tile adjacent to lower tier → Ramp / HighwayOn / HighwayOff
- [x] `PlaceRoad` rejects illegal highway merge (non-highway tile with 2+ highway neighbors)
- [x] Tests: T-highway-local junction + illegal merge rejection
- [ ] Dedicated ramp paint tool / `RoadFlags.Ramp` bit (deferred to P1.6+ road toolbar UI)
- [ ] Snapshot export of `nodeTypes[]` (deferred to P1.6)

## P1.5 — Road toolbar UX (partial)

- [x] `PlaceRoad` bool return wired WASM → worker → `sim-bridge` → caller
- [x] HUD toast on illegal highway merge: "Illegal highway merge — use a ramp"
- [x] Road tier labels: Local / Collector / Highway (tier 2 = highway)
- [x] Vitest: `road-types` tier labels + merge toast constant
- [ ] Dedicated ramp paint tool / bridge mode toggle (deferred to P1.6)
- [ ] Highway tier disabled hint when paint would fail (optional; skipped — needs sim probe)

## Verification

```bash
dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj --filter "FullyQualifiedName~Road"
cd web && pnpm test
```

## Merge gate

- Characterization tests pass
- No regression on existing traffic lite tests
- OpenSpec delta archived to `openspec/specs/infrastructure.md` on merge
