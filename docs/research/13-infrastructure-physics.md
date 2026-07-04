# Research: 13 Infrastructure Physics

The complete specification is ready in the plan file. Here is what it covers:

**All nine systems with exact C# formulas and numerical constants:**

1. **Noise Propagation** -- Inverse-square attenuation with atmospheric absorption, 12 source types with dB at origin, traffic-scaled road noise formula, vegetation absorption (3 dB/row, max 15 dB), building reflection (+2 dB/row), night penalty (+10 dB effective), logarithmic multi-source aggregation, 6-tier residential threshold table with happiness and land value modifiers.

2. **Water Pressure** -- 500 kPa base pump pressure, friction loss (0.05 kPa/m scaled by pipe diameter), hydrostatic loss (9.81 kPa/m elevation), 4 pipe tiers with flow rates and friction multipliers, booster station placement rules, pressure zone boundaries at 30m elevation delta, burst probability above 700 kPa, demand table for 9 building types.

3. **Sewage Flow** -- Manning's equation for gravity velocity, 0.5% minimum slope, sewage generation at 80% of water demand, 3 pump station tiers, combined sewer overflow via rational method with 5 runoff coefficients by surface type, 3 treatment plant tiers (60%/90%/99% removal), disease probability logistic curve from overflow pollution.

4. **Flood Simulation** -- Per-tile shallow water with steepest-descent neighbor flow, river discharge from upstream rainfall + snowmelt - dam absorption, coastal storm surge from wind^2 / depth, flood damage by 5 depth brackets and 4 material types, exponential damage formula, 5 flood zones with insurance multipliers and building restrictions.

5. **Wind and Pollution** -- Seasonal wind profiles with variability, full Gaussian plume model (Pasquill-Gifford class D), simplified tile-based formula for the game loop, 7 source types with emission rates and stack heights, 6-tier AQI table with health/land value/happiness effects.

6. **Soil Contamination** -- Radial plume spread scaled by source strength and soil permeability (clay/loam/sand/gravel), 7 source types, exponential decay with contaminant half-lives (15-500 years), 6 remediation methods with cost/duration/effectiveness, brownfield designation at 10% threshold, 5-year land value recovery curve.

7. **Snow/Ice** -- Accumulation by temperature and snow-to-liquid ratio, degree-day melt, 6-tier road speed reduction table, 5-level plowing priority system, salt effectiveness curve (fails below -18C), sand alternative, long-term salt damage to infrastructure and vegetation, heating demand formula with 6 fuel types.

8. **Fire Spread** -- 4-state fire state machine, ignition probability per tick from material flammability / adjacent burning cells / wind / humidity, wind direction multiplier (1.5x downwind, 0.5x upwind, scaled by speed), 7 firebreak types (road 80%, park 90%, water 100%), fire department suppression rate from quality/distance/response time with 5 quality factors, spontaneous ignition formula.

9. **Earthquake** -- Boore-Atkinson PGA attenuation, damage probability from PGA x material x age x seismic code, 6-bracket severity distribution table, liquefaction by soil type and water table depth, Omori's Law aftershock decay, Gutenberg-Richter magnitude sampling, Bath's Law largest aftershock, 5 seismic zone settings with return periods.

Also includes a **12-entry cross-system interaction table** (earthquake breaks gas lines triggering fires, floods overwhelm sewage, frozen pipes lose pressure, etc.) with a master interaction function, plus **performance notes** for C# implementation (grid caching strategies, update frequencies, double-buffering).

To execute: approve the plan and I will create `docs/design/INFRASTRUCTURE_PHYSICS.md` with the full content.