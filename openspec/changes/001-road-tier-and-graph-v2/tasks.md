# Tasks: 001-road-tier-and-graph-v2

## P1.1 — Road tier API

- [ ] Add `PlaceRoad(int x, int y, byte tier = 1)` to `SimHost`
- [ ] Update `ComputeRoadFlags` to encode tier in bits 4–5
- [ ] `RebuildRoadGraphFromTiles`: read tier from flags; set edge level + cost
- [ ] Add `RoadCapacityForLevel(byte level)` helper
- [ ] WASM: accept tier on `place_road` command
- [ ] Web: extend `sim-bridge` + `sim-worker` + road toolbar
- [ ] Tests: `RoadTierGraphTests` (tier → level/capacity)

## P1.2 — Graph builder v2 (start)

- [ ] Document intersection detection algorithm in `RoadGraphBuilder`
- [ ] Implement segment collapse between intersection nodes
- [ ] Assign `RoadNode.NodeType` for 4-way and T-junction
- [ ] Tests: intersection node count on cross pattern
- [ ] Feature flag `UseSegmentGraph` (default off until validated)

## Verification

```bash
dotnet test tests/Forge.SimCore.Tests/Forge.SimCore.Tests.csproj --filter "FullyQualifiedName~RoadTier"
cd web && pnpm test
```

## Merge gate

- Characterization tests pass
- No regression on existing traffic lite tests
- OpenSpec delta archived to `openspec/specs/infrastructure.md` on merge
