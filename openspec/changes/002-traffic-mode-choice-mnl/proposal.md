# Change 002: Traffic Mode-Choice Stub (MNL) + BPR Frank-Wolfe

**OpenSpec change:** `002-traffic-mode-choice-mnl`  
**Cathedral milestones:** P4.5 (mode choice stub); BPR/FW foundation supporting P4.1–P4.2 traffic truth  
**Status:** Implemented (documenting landed work)  
**Linear epic:** SB-4230 (P4 Population Life) · related SB-3685 (WASM BPR-lite)

---

## Problem

`WasmTrafficLite` used a flat **65% car** share and a single Frank-Wolfe iteration. That blocked Cathedral **P4** population truth: commute mode shares could not respond to trip distance, edge travel times were under-converged, and there was no shared BPR helper for lite/full traffic. Herald and HUD consumers had no city-wide car/transit/walk split.

## Proposal

1. **Shared BPR** — Extract `TrafficBpr` (α=0.15, β=4) for edge travel time and tier-table capacity; use from lite and full `TrafficSystem`.
2. **Multi-iteration Frank-Wolfe** — Raise lite FW to **3** iterations with **1%** relative-gap stop; export `edgeVolumes[]` / `travelTimes[]` on road-graph snapshots.
3. **Mode-choice stub (P4.5)** — Replace flat car share with a distance-based **3-mode MNL** (car / transit / walk); expose city-wide mode shares without changing P4.1 home/work O-D construction.

## Non-goals (this change)

- Full McFadden ASC from household `TransitPreference` / wealth (post-stub calibration)
- Desktop-scale zones (~500) as WASM default (stays lite 64-zone)
- Per-household pathfinding or agent vehicles
- Mode-share HUD polish (follow-up client work)

## Success criteria

- Mid-range O-D distances (~10–20 tiles) yield ~55–75% car (near former flat share).
- Short trips show higher walk share than long trips; mode shares sum to 1.
- Lite FW runs ≤3 iterations and may early-stop when relative gap &lt; 1%.
- `CathedralTrafficBprTests` + `CathedralModeChoiceTests` green.

## Surfaces touched

| Surface | Change |
|---------|--------|
| `Forge.SimCore` | `TrafficBpr` utility + FW relative gap |
| `Forge.Game` / `TrafficSystem` | Shared BPR + multi-iter FW |
| `Forge.SimWasm` / `WasmTrafficLite` | MNL mode split, FW iters, mode-share props |
| `SimSnapshotDto` / status | `edgeVolumes` / `travelTimes`; mode shares on status |
| `Forge.SimCore.Tests` | `CathedralTrafficBprTests`, `CathedralModeChoiceTests` |

## References

- [`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) §4 **P4 — Population & life** (esp. **P4.5** mode choice stub)
- [`docs/research/11-transport-mode-choice.md`](../../../docs/research/11-transport-mode-choice.md)
- [`SIMULATION_ARCHITECTURE.md`](../../../docs/design/SIMULATION_ARCHITECTURE.md) §4
- [`openspec/specs/population.md`](../../specs/population.md)
- Prior change: [`001-road-tier-and-graph-v2`](../001-road-tier-and-graph-v2/) (tier → edge capacity)
