# Design: Mode-Choice Stub + BPR Frank-Wolfe

## Cathedral linkage

[`CATHEDRAL_PROGRAM.md`](../../../docs/design/CATHEDRAL_PROGRAM.md) **P4 — Population & life**:

| Milestone | This change |
|-----------|-------------|
| P4.1 home/work O-D | **Unchanged** — MNL splits existing O-D car demand; does not rebuild pairs |
| P4.2 commute → satisfaction | **Enabled by** multi-iter FW + `travelTimes[]` export |
| **P4.5** mode choice stub | **Delivered** — distance MNL; full `TransitPreference` ASC deferred |

## BPR

```
t = t0 * (1 + α * (V/C)^β)   α = 0.15, β = 4
```

Shared in `TrafficBpr.CalculateTravelTime`. Capacity from `RoadTier.RoadCapacityForLevel(level) * lanes * lawMult`.

## Frank-Wolfe (lite)

1. All-or-nothing assignment on current BPR times.
2. MSA / FW step toward auxiliary flows.
3. Stop when `FrankWolfeRelativeGap < 0.01` or iterations = 3.

Full `TrafficSystem` uses the same gap helper; lite stays on the 5-sim-s schedule (`TrafficLiteInterval`).

## MNL stub

Utilities (tile distance → mode time via fixed speeds):

| Mode | ASC | Speed (tiles/min) |
|------|-----|-------------------|
| Car | +0.5 | 2.0 |
| Transit | 0.0 | 1.2 |
| Walk | −0.2 | 0.15 (share 0 if time &gt; 60 min) |

```
P_i = exp(V_i) / Σ exp(V_j),  V_i = ASC_i + β_time * t_i,  β_time = −0.025
```

ASC calibrated so mid-range (~12 tile) trips stay near the former **0.65** car share. Only the **car** fraction of each O-D cell is assigned onto the road graph; transit/walk are accounted in city shares only (stub — no transit network assignment yet).

## Risks / follow-ups

- Household `TransitPreference` not yet in utilities (P4.5 full).
- Mode shares not yet required on Economy HUD (client follow-up).
- Walk/transit volumes do not load graph edges — intentional for stub.
