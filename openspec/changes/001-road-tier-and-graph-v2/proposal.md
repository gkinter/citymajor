# Change 001: Road Tier API + Graph Builder v2

**OpenSpec change:** `001-road-tier-and-graph-v2`  
**Cathedral milestones:** P1.1, P1.2  
**Status:** Proposed  
**Linear epic:** SB-4200 (P1 Infrastructure) · issues SB-4201, SB-4202

---

## Problem

`SimHost.PlaceRoad(x, y)` paints roads but ignores tier. `RebuildRoadGraphFromTiles` hardcodes edge `level=1` and `cost=1f`. The web road toolbar has tiers 0–2 (`road-types.ts`) but the WASM `place_road` command does not pass tier. Players cannot see congestion differences between dirt roads and highways — undermining the infrastructure truth pillar.

## Proposal

1. **P1.1** — Thread road tier through `PlaceRoad`, `TileData.RoadFlags` (bits 4–5), WASM bridge, and web paint command.
2. **P1.2** — Replace per-tile graph nodes with intersection-segment topology; derive edge level and capacity from adjacent tile tiers.

## Non-goals (this change)

- Highway on/off ramps (P1.4)
- Full `TrafficSystem` as default (P1.5)
- Snapshot `edgeVolumes[]` export (P1.6)

## Success criteria

- Placing tier-0 vs tier-2 roads produces different edge levels and capacities after graph rebuild.
- `Forge.SimCore.Tests/RoadTierGraphTests` green.
- Web road paint sends selected tier to sim.

## Surfaces touched

| Surface | Change |
|---------|--------|
| `Forge.SimCore` / `SimHost` | `PlaceRoad(x,y,tier)`, `RebuildRoadGraphFromTiles` reads flags |
| `Forge.SimWasm` | Tier param on place road export |
| `web/workers/sim-worker.ts` | Pass `roadTier` in command |
| `web/lib/sim-bridge.ts` | `place_road` type + tier |
| `web` PlayClient | Wire toolbar tier to bridge |

## References

- [`AGENT_04_TRANSPORT.md`](../../docs/design/AGENT_04_TRANSPORT.md)
- [`SIMULATION_ARCHITECTURE.md`](../../docs/design/SIMULATION_ARCHITECTURE.md) §4
- [`openspec/specs/infrastructure.md`](../specs/infrastructure.md)
