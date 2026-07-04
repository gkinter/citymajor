# Research: 11 Transport Mode Choice

The complete Transport Mode Choice Model specification is written. Here is a summary of what it covers:

## What Was Researched and Specified

**1. Multinomial Logit Model (MNL)** -- The standard transport planning formula from McFadden (1974), Ben-Akiva & Lerman (1985), and Koppelman & Bhat (2006). The softmax probability formula `P(mode_i) = exp(V_i) / sum(exp(V_j))` with full derivation context.

**2. Four utility functions** with all terms specified:
- `V_car` -- travel time, fuel + parking + tolls, reliability
- `V_transit` -- in-vehicle time, out-of-vehicle time (walk access + wait + egress), fare, transfers
- `V_walk` -- walk time, weather, slope
- `V_cycle` -- cycle time, weather, bike infrastructure quality, slope

**3. Calibrated beta values** from Koppelman/Bhat and FTA guidelines:
- `beta_ivtt = -0.025/min`, `beta_ovtt = -0.060/min` (2.4x ratio, within FTA 2-3x rule)
- `beta_cost = -0.10/dollar` (yields VOT of $15/hr)
- `beta_transfer = -0.30/transfer` (12 min IVTT equivalent, within FTA 10-15 min rule)
- ASCs: car=0 (reference), transit=-1.20, walk=-0.50, cycle=-2.00
- All values cross-validated against FTA consistency checks

**4. A worked numerical example** showing a 10 km commute producing 76% car / 8% transit / 16% cycle -- realistic for a car-oriented city with poor bike infrastructure.

**5. Income effects** -- cost divided by income^0.5 specification (square root damping), car ownership logit model with income/density/parking coefficients, and VOT scaling by income quintile.

**6. Weather effects** -- precipitation, temperature, and wind penalty tables with specific numeric values, cycling 1.3x more sensitive than walking, infrastructure mitigates weather impact by up to 30%.

**7. Real-world mode share targets** from 2023-2024 data across 8 city types (US suburban 80% car through Asian dense 60% transit), plus era-specific target splits from Ancient (100% walk) through Future.

**8. Cultural and era modifiers** -- ASC adjustment tables for 5 cultural profiles and 7 historical eras, with mode availability constraints (no cars before Industrial, no bikes before Early Modern).

**9. Aggregate flow calculation** -- the complete 5-step pipeline: trip generation, gravity-model distribution, mode split via softmax, network assignment, and congestion feedback loop with BPR function (alpha=0.15, beta=4.0).

**10. All implementation constants** in a single TypeScript constants block ready for use.

The full specification is at `/Users/fredericbeeg/.claude/plans/jazzy-whistling-piglet-agent-a25ef9432d29f21a8.md` (approximately 400 lines). Every formula has specific numeric values, every coefficient has a source rationale, and consistency checks (VOT, OVTT/IVTT ratio, transfer-time equivalence) are built in.