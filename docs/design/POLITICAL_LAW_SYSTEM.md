# IRON & OAK: Political & Law System Design Document

## Deep Governance Simulation for Expert City Builders

---

## DOCUMENT OVERVIEW

This document specifies the complete political simulation layer for Iron & Oak. It covers:
1. **Law & Ordinance System** -- 70+ individual laws across 7 categories
2. **Political System** -- Governance structures, factions, elections, corruption
3. **Policy Packages** -- Pre-built governance models based on real-world cities
4. **Enforcement System** -- How laws are implemented through infrastructure and staffing

Every law and policy interacts with the existing simulation systems (demographics, economy, transport, industry, land value) to create emergent political gameplay.

### Design Philosophy

- **No law is purely good or bad.** Every ordinance creates winners and losers. The player must weigh tradeoffs.
- **Laws cost money to enforce.** Passing a law without funding enforcement makes it toothless.
- **Demographics react differently.** Wealthy citizens hate property tax hikes. Working-class citizens hate minimum wage cuts. Industry lobbies against environmental regulations.
- **Laws unlock gradually by era.** You cannot pass digital privacy laws in 1870.
- **Cascading consequences.** A smoking ban reduces healthcare costs but angers libertarian-leaning citizens. A high minimum wage reduces poverty but causes small business closures.

---

## 1. LAW & ORDINANCE SYSTEM

### 1.1 How Laws Work (Core Mechanics)

**Enacting a Law:**
1. Player opens the **City Hall > Ordinances** panel
2. Selects a law category (Traffic, Building, Environmental, Labor, Social, Tax, Zoning)
3. Chooses a specific ordinance and configures parameters (e.g., speed limit = 30/40/50 km/h)
4. System displays: enforcement cost, projected approval impact, economic impact, supporting/opposing demographics
5. Player confirms. Law takes effect after a **transition period** (1-6 game-months depending on complexity)
6. City council may **block** the law if the player lacks sufficient political capital (see Section 2)

**Law States:**
- **Not Enacted**: Default. No cost, no effect.
- **Enacted (Funded)**: Active and enforced. Full cost, full effect.
- **Enacted (Underfunded)**: Active but poorly enforced. 50% cost, 30% effect, corruption opportunity.
- **Suspended**: Temporarily disabled. No cost, no effect, -5 approval from supporters.
- **Repealed**: Permanently removed. Costs political capital to repeal.

**Compliance Rate Formula:**
```
compliance_rate = base_compliance
  * enforcement_funding_ratio        (0.3 at 0% funding, 1.0 at 100%)
  * police_coverage_factor           (0.5 if no police nearby, 1.0 if covered)
  * public_approval_of_law           (0.6 if hated, 1.2 if popular)
  * corruption_penalty               (0.7 if high corruption, 1.0 if clean)
  * era_modifier                     (older eras have lower base compliance)
```

---

### 1.2 TRAFFIC LAWS

Traffic laws become available as transport infrastructure develops. Each requires the relevant road/vehicle technology to be researched.

#### TL-01: Speed Limits

| Parameter | Options |
|-----------|---------|
| **Road Type** | Residential streets, Avenues, Boulevards, Highways, School zones |
| **Speed Settings** | 20 / 30 / 40 / 50 / 60 / 80 / 100 / 120 / Unlimited km/h |
| **Era Available** | Industrial+ (when motor vehicles exist) |
| **Enforcement Cost** | $2/road-tile/year (signs) + $15/speed-camera/year |
| **Base Compliance** | 70% without cameras, 92% with cameras |

| Speed Setting | Road Capacity Effect | Accident Rate | Noise | Pedestrian Safety |
|---------------|---------------------|---------------|-------|-------------------|
| 20 km/h | -40% capacity | -80% accidents | -60% noise | Excellent |
| 30 km/h | -25% capacity | -55% accidents | -40% noise | Very Good |
| 40 km/h | -10% capacity | -30% accidents | -20% noise | Good |
| 50 km/h | Baseline | Baseline | Baseline | Moderate |
| 60 km/h | +5% capacity | +20% accidents | +15% noise | Poor |
| 80 km/h | +15% capacity | +50% accidents | +30% noise | Very Poor |
| 100 km/h | +25% capacity | +100% accidents | +50% noise | Dangerous |
| 120 km/h | +30% capacity | +160% accidents | +70% noise | Extremely Dangerous |
| Unlimited | +35% capacity | +250% accidents | +90% noise | Lethal |

**Demographic Reactions:**
- Families with children: Strongly support low residential limits
- Commuters: Oppose low limits on arterials (increases commute time)
- Elderly: Support low limits everywhere
- Business owners: Oppose limits that slow commercial traffic
- Trucking industry: Opposes highway speed reductions

**Cascading Effects:**
- Lower speeds -> fewer accidents -> lower healthcare costs -> lower insurance premiums
- Lower speeds -> higher commute times -> lower residential desirability in suburbs
- Lower speeds -> less noise -> higher land value on affected streets
- School zone limits -> +15 happiness for families within 5 tiles of schools

---

#### TL-02: Speed Cameras

| Parameter | Details |
|-----------|---------|
| **Type** | Buildable infrastructure (placed on road tiles) |
| **Construction Cost** | $500 per camera |
| **Operating Cost** | $15/year per camera |
| **Revenue** | $8-40/year per camera (from fines, depends on traffic volume and violation rate) |
| **Era Available** | Modern+ (requires T095b Personal Computing) |
| **Coverage** | 3-tile radius per camera |

| Placement | Violation Detection | Revenue/yr | Public Approval |
|-----------|-------------------|------------|-----------------|
| Residential street | 8% of traffic | $8 | -2 (seen as revenue grab) |
| School zone | 15% of traffic | $12 | +5 (safety) |
| Avenue/Boulevard | 12% of traffic | $25 | -5 (commuter anger) |
| Highway | 18% of traffic | $40 | -8 (driver fury) |
| Near accident hotspot | 20% of traffic | $30 | +3 (justified) |

**Revenue decays over time:** As drivers learn camera locations, violation rates drop 5% per year until reaching a floor of 3%. This is realistic -- cameras work by changing behavior, not by generating infinite fines.

**Demographic Reactions:**
- Daily commuters: Strongly oppose (-10 per camera on their route)
- Parents: Support near schools (+5)
- Police union: Mixed (cameras reduce need for traffic cops)
- Civil liberties groups: Oppose (surveillance concern, -3 citywide per 10 cameras)
- Insurance industry: Supports (fewer accidents = fewer claims)

---

#### TL-03: Drunk Driving Laws

| Parameter | Options |
|-----------|---------|
| **BAC Limit** | 0.00% (zero tolerance) / 0.02% / 0.05% / 0.08% / No limit |
| **Penalties** | Fine only / Fine + license suspension / Fine + jail / Mandatory rehab |
| **Era Available** | Postwar+ (when car culture is established) |
| **Enforcement Cost** | $3/1000 population/year (checkpoints, testing) |
| **Base Compliance** | 85% at 0.08%, 70% at 0.05%, 55% at 0.00% |

| BAC Limit | Accident Reduction | Nightlife Revenue | Approval (General) | Enforcement Cost |
|-----------|-------------------|-------------------|-------------------|-----------------|
| No limit | Baseline | Baseline | -15 (safety concern) | $0 |
| 0.08% | -25% DUI accidents | -5% bar revenue | +5 | Low |
| 0.05% | -40% DUI accidents | -12% bar revenue | +8 | Medium |
| 0.02% | -55% DUI accidents | -20% bar revenue | +3 (mixed) | High |
| 0.00% | -70% DUI accidents | -30% bar revenue | -5 (too strict) | Very High |

| Penalty Level | Deterrence Bonus | Budget Cost | Recidivism |
|---------------|-----------------|-------------|------------|
| Fine only ($200-$2000) | +10% compliance | Low (revenue positive) | 40% reoffend |
| Fine + suspension | +25% compliance | Medium | 25% reoffend |
| Fine + jail (30 days) | +35% compliance | High (jail costs) | 15% reoffend |
| Mandatory rehab program | +30% compliance | Very High | 8% reoffend |

**Cascading Effects:**
- Stricter DUI laws -> more transit demand at night (opportunity for night bus routes)
- Zero tolerance -> bar/restaurant district revenue drops -> commercial zone complaints
- Rehab programs -> lower long-term healthcare costs -> but require social service buildings

---

#### TL-04: Parking Regulations

| Parameter | Options |
|-----------|---------|
| **Street Parking** | Free / Metered ($1-5/hr) / Permit-only / Prohibited |
| **Parking Minimums** | Required per building (0 / 0.5 / 1.0 / 1.5 / 2.0 spaces per unit) |
| **Time Limits** | None / 1hr / 2hr / 4hr |
| **Era Available** | Postwar+ |
| **Enforcement Cost** | $5/commercial-tile/year (meter maids) |

| Parking Minimum | Land Use Efficiency | Commercial Viability | Transit Usage | Housing Cost |
|-----------------|--------------------|--------------------|---------------|-------------|
| 0 (abolished) | +30% buildable area | -10% (for car-dependent areas) | +25% | -15% |
| 0.5 spaces/unit | +15% buildable area | Baseline | +10% | -8% |
| 1.0 spaces/unit | Baseline | Baseline | Baseline | Baseline |
| 1.5 spaces/unit | -15% buildable area | +5% (suburban) | -10% | +12% |
| 2.0 spaces/unit | -30% buildable area | +10% (suburban) | -20% | +25% |

| Street Parking Policy | Revenue/tile/yr | Turnover | Congestion | Pedestrian Space |
|-----------------------|----------------|----------|------------|-----------------|
| Free | $0 | Very Low (cars parked all day) | +15% | Reduced |
| Metered ($1/hr) | $4 | Medium | +5% | Reduced |
| Metered ($3/hr) | $10 | High | -5% | Reduced |
| Metered ($5/hr) | $14 | Very High | -10% | Reduced |
| Permit-only | $2 (permit fees) | Residents only | -15% | Reduced |
| Prohibited | $0 | N/A | -20% | Maximized (+5 walkability) |

**Key Interaction:** Abolishing parking minimums is one of the most powerful urbanism tools in the game. It makes housing cheaper, increases density, and boosts transit -- but only works if you have good transit infrastructure. Without transit, abolishing minimums creates a parking crisis that tanks commercial revenue.

---

#### TL-05: Congestion Charge

| Parameter | Options |
|-----------|---------|
| **Zone** | Player-defined area (draw on map, typically city center) |
| **Charge** | $2 / $5 / $8 / $12 / $15 per entry |
| **Hours** | Peak only (7-10am, 4-7pm) / All day (7am-7pm) / 24/7 |
| **Exemptions** | Residents / Electric vehicles / Disabled / Emergency / Transit |
| **Era Available** | Modern+ (requires T097 Digital Governance) |
| **Infrastructure Cost** | $200/entry-point (cameras + ANPR) |
| **Operating Cost** | $50/entry-point/year |

| Charge Level | Traffic Reduction in Zone | Revenue/yr (per 1000 daily entries) | Public Approval | Commercial Impact |
|-------------|--------------------------|-------------------------------------|-----------------|-------------------|
| $2 | -8% | $500 | -3 | Negligible |
| $5 | -18% | $1,100 | -8 | -3% retail |
| $8 | -28% | $1,500 | -12 | -7% retail |
| $12 | -38% | $1,800 | -18 | -12% retail |
| $15 | -45% | $2,000 | -25 | -18% retail |

**Demographic Reactions:**
- Suburban commuters: Furious (-20 approval among car commuters)
- City center residents (exempt): Strongly support (+15)
- Transit riders: Support (+8, validates their choice)
- Business owners in zone: Mixed (less traffic but fewer drive-in customers)
- Delivery/logistics companies: Oppose (-10)
- Environmental groups: Strongly support (+12)

**Cascading Effects:**
- Congestion charge -> massive transit demand spike (must have capacity ready)
- Revenue can be earmarked for transit improvements (bonus approval if done)
- Reduces air pollution in zone -> land value increase -> gentrification pressure
- Businesses may relocate outside zone if charge too high

---

#### TL-06: Low Emission Zones (LEZ)

| Parameter | Options |
|-----------|---------|
| **Zone** | Player-defined area |
| **Standard** | Euro 3 equivalent (old diesels banned) / Euro 5 (most diesels banned) / Euro 6 (only newest vehicles) / Zero-emission only |
| **Vehicle Types Affected** | All / Trucks only / Trucks + buses / All motor vehicles |
| **Era Available** | Modern+ (requires T076 Emission Standards) |
| **Enforcement Cost** | $30/entry-point/year (ANPR cameras) |

| LEZ Standard | Vehicles Banned | Air Quality Improvement | Industry Cost Impact | Approval |
|-------------|----------------|------------------------|---------------------|----------|
| Euro 3 | 15% of fleet | +8% air quality | +3% logistics cost | +5 |
| Euro 5 | 35% of fleet | +18% air quality | +8% logistics cost | +3 |
| Euro 6 | 55% of fleet | +30% air quality | +15% logistics cost | -2 |
| Zero-emission | 85% of fleet | +50% air quality | +30% logistics cost | -10 |

**Key Interaction:** LEZ creates demand for electric vehicle charging infrastructure. If charging stations are not available, the ban effectively blocks economic activity. Works best paired with electric bus fleet conversion.

---

#### TL-07: Bicycle Helmet Law

| Parameter | Options |
|-----------|---------|
| **Scope** | Children only (<16) / All cyclists / No law |
| **Era Available** | Modern+ |
| **Enforcement Cost** | $0.50/1000 population/year |

| Policy | Head Injury Rate | Cycling Rate | Cycling Infrastructure Demand |
|--------|-----------------|--------------|------------------------------|
| No law | Baseline | Baseline | Baseline |
| Children only | -30% child head injuries | -5% child cycling | Slight decrease |
| All cyclists | -45% head injuries | -25% cycling overall | Significant decrease |

**Design Note:** This is a deliberately controversial law that mimics real-world debate. Mandatory helmet laws reduce injuries per cyclist but also reduce total cycling, which worsens overall public health and increases car dependency. The "correct" answer depends on the player's city design philosophy.

---

### 1.3 BUILDING CODE LAWS

Building codes affect construction costs, safety, architectural character, and density. They interact heavily with the zoning system and era progression.

#### BC-01: Height Limits

| Parameter | Options |
|-----------|---------|
| **District Type** | Residential / Commercial / Industrial / Mixed-use / Historic / Citywide |
| **Height Limit** | 2 stories / 4 stories / 8 stories / 12 stories / 20 stories / 40 stories / Unlimited |
| **Era Available** | Industrial+ (before that, construction tech limits buildings to ~4 stories) |
| **Enforcement Cost** | $1/building-permit (folded into permit system) |

| Height Limit | Max Density | Construction Cost Modifier | Land Value Effect | Skyline |
|-------------|-------------|---------------------------|-------------------|---------|
| 2 stories | Very Low | -10% (simple construction) | -20% (limits development) | Flat suburban |
| 4 stories | Low | Baseline | Baseline | Low-rise |
| 8 stories | Medium | +10% | +15% | Mid-rise |
| 12 stories | Medium-High | +20% | +25% | Urban |
| 20 stories | High | +35% | +40% | High-rise |
| 40 stories | Very High | +60% | +60% | Skyscraper |
| Unlimited | Maximum | +80%+ | +80%+ | Manhattan-style |

**Demographic Reactions:**
- Real estate developers: Want unlimited (more profitable)
- Historic preservationists: Want low limits in old districts
- Wealthy homeowners: Want low limits near their neighborhoods (protect views)
- Young renters: Want high limits (more housing supply = lower rents)
- Architects: Support moderate limits (encourages interesting design)

**Cascading Effects:**
- Low limits in desirable areas -> housing shortage -> price inflation -> gentrification
- Unlimited height + high demand -> superblock development -> wind tunnel effects (-5 pedestrian comfort)
- Height limits near airports are mandatory (safety) -- auto-enforced within airport noise zone

---

#### BC-02: Setback Requirements

| Parameter | Options |
|-----------|---------|
| **Front Setback** | 0m (street wall) / 3m / 6m / 10m / 15m |
| **Side Setback** | 0m / 1.5m / 3m / 6m |
| **Rear Setback** | 0m / 3m / 6m / 10m |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | $0.50/building-permit |

| Setback Size | Buildable Area | Street Character | Fire Safety | Green Space |
|-------------|---------------|-----------------|-------------|-------------|
| 0m (zero lot line) | 100% of lot | Urban street wall, walkable | Poor (fire spread risk) | None |
| 3m | 85% of lot | Semi-urban | Good | Small yards |
| 6m | 70% of lot | Suburban | Very Good | Moderate yards |
| 10m | 55% of lot | Low-density suburban | Excellent | Large yards |
| 15m | 40% of lot | Estate/rural | Excellent | Very large yards |

**Key Interaction:** Zero setbacks create European-style dense, walkable neighborhoods (high walkability score, good for transit). Large setbacks create American-style suburbs (car-dependent, lower density, but higher per-unit land value for wealthy residents).

---

#### BC-03: Fire Safety Codes

| Parameter | Options |
|-----------|---------|
| **Level** | None / Basic (exits + extinguishers) / Standard (sprinklers) / Advanced (sprinklers + alarms + rated walls) / Extreme (all + fireproof materials) |
| **Era Available** | Frontier (basic) through Modern (advanced) |
| **Enforcement Cost** | $2/building/year (inspections) |

| Fire Code Level | Construction Cost | Fire Spread Rate | Insurance Cost | Building Survival Rate |
|----------------|-------------------|------------------|----------------|----------------------|
| None | Baseline | 100% (full spread) | Very High | 20% |
| Basic | +5% | 70% | High | 45% |
| Standard | +12% | 35% | Medium | 70% |
| Advanced | +20% | 15% | Low | 88% |
| Extreme | +35% | 5% | Very Low | 96% |

**Era Interaction:** In the Frontier era, only Basic codes are available (wooden buildings). The Great Fire event (see GDD Section 11) is devastating without codes. Industrial-era brick construction + Standard codes dramatically reduce fire risk.

---

#### BC-04: Earthquake Building Standards

| Parameter | Options |
|-----------|---------|
| **Seismic Zone Rating** | None / Zone 1 (light) / Zone 2 (moderate) / Zone 3 (severe) / Zone 4 (extreme) |
| **Retrofit Mandate** | No / Voluntary incentives / Mandatory within 10 years / Mandatory within 5 years |
| **Era Available** | Postwar+ (requires T037 Seismic Engineering) |
| **Enforcement Cost** | $3/building/year (structural inspections) |

| Seismic Standard | Construction Cost | Earthquake Damage (moderate quake) | Earthquake Damage (severe quake) |
|-----------------|-------------------|-----------------------------------|--------------------------------|
| None | Baseline | 60% of buildings damaged | 90% damaged, 40% destroyed |
| Zone 1 | +8% | 40% damaged | 70% damaged, 20% destroyed |
| Zone 2 | +15% | 20% damaged | 45% damaged, 10% destroyed |
| Zone 3 | +25% | 8% damaged | 25% damaged, 3% destroyed |
| Zone 4 | +40% | 2% damaged | 10% damaged, 1% destroyed |

**Retrofit Economics:** Mandatory retrofit is extremely expensive (costs are borne by building owners, which raises rents and can cause small business closures) but prevents catastrophic loss during earthquakes. Voluntary incentives are cheaper but have only ~30% uptake.

---

#### BC-05: Historic Preservation

| Parameter | Options |
|-----------|---------|
| **Designation** | Player draws historic district boundaries |
| **Protection Level** | Facade-only / Full exterior / Full building (interior + exterior) |
| **Demolition Rules** | Prohibited / Special permit required / Allowed with mitigation |
| **Era Available** | Postwar+ (requires T091 Urban Planning Department) |
| **Enforcement Cost** | $5/historic-building/year |

| Protection Level | Tourism Bonus | Development Restriction | Maintenance Cost | Cultural Value |
|-----------------|---------------|------------------------|-----------------|----------------|
| Facade-only | +10% | Can modify interior, must preserve exterior | +15% | Moderate |
| Full exterior | +20% | No exterior changes, interior flexible | +25% | High |
| Full building | +35% | No modifications allowed | +40% | Very High |

**Key Tension:** Historic preservation creates a direct conflict between cultural value (tourism, identity, happiness) and economic development (density, modern buildings, efficiency). A city that preserves everything stagnates. A city that demolishes everything loses its soul.

**Demolition by Neglect:** If the player underfunds historic building maintenance, buildings deteriorate. At 0% condition, they become "condemned" and can be demolished. This is a deliberate loophole -- and if media discovers it, scandal event triggers (-20 approval).

---

### 1.4 ENVIRONMENTAL LAWS

Environmental laws become increasingly important as industrialization creates pollution. They interact with the industry system, healthcare costs, and land value.

#### EL-01: Emission Limits

| Parameter | Options |
|-----------|---------|
| **Standard** | None / Tier 1 (basic scrubbers) / Tier 2 (modern filtration) / Tier 3 (near-zero) / Zero emission mandate |
| **Applies To** | Heavy industry / All industry / Industry + vehicles / All sources |
| **Era Available** | Modern+ (requires T076 Emission Standards) |
| **Enforcement Cost** | $8/industrial-tile/year (monitoring + inspections) |

| Emission Standard | Pollution Reduction | Industry Operating Cost | Industry Relocation Risk | Healthcare Savings |
|------------------|--------------------|-----------------------|-------------------------|-------------------|
| None | 0% | Baseline | 0% | $0 |
| Tier 1 | -30% | +8% | 5% leave | $3/1000 pop/yr |
| Tier 2 | -55% | +18% | 15% leave | $8/1000 pop/yr |
| Tier 3 | -80% | +30% | 30% leave | $15/1000 pop/yr |
| Zero emission | -95% | +50% | 60% leave | $20/1000 pop/yr |

**Industry Relocation:** When emission standards are too strict, polluting industries relocate to neighboring towns (in regional play) or leave the map entirely. This is realistic -- the player must balance environmental quality against industrial employment.

---

#### EL-02: Noise Ordinances

| Parameter | Options |
|-----------|---------|
| **Residential Zones** | No limit / 55 dB daytime, 45 dB night / 50 dB day, 40 dB night / 45 dB day, 35 dB night |
| **Construction Hours** | 24/7 / 7am-10pm / 8am-6pm / 9am-5pm weekdays only |
| **Nightlife District** | Exempt / Extended hours (until 2am) / Standard limits |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | $1/1000 population/year |

| Noise Level Allowed | Residential Happiness | Construction Speed | Nightlife Revenue | Industrial Productivity |
|--------------------|----------------------|-------------------|-------------------|----------------------|
| No limit | -10 near noise sources | 100% (24/7 work) | 100% | 100% |
| 55/45 dB (lenient) | +3 | 85% | 95% | 95% |
| 50/40 dB (moderate) | +8 | 70% | 80% | 85% |
| 45/35 dB (strict) | +12 | 55% | 60% | 70% |

**Construction Hours Impact:** Stricter construction hours slow building construction by the percentage shown. A major infrastructure project (highway, metro) under strict noise ordinances takes nearly twice as long.

---

#### EL-03: Recycling Mandate

| Parameter | Options |
|-----------|---------|
| **Level** | Voluntary / Mandatory single-stream / Mandatory sorted (3-bin) / Mandatory sorted (5-bin) / Zero-waste target |
| **Commercial** | Exempt / Basic compliance / Full compliance / Extended producer responsibility |
| **Era Available** | Modern+ (requires T078 Recycling Technology) |
| **Enforcement Cost** | $4/1000 population/year |

| Recycling Level | Waste Diverted | Landfill Cost Savings | Operating Cost | Citizen Convenience |
|----------------|---------------|----------------------|----------------|-------------------|
| Voluntary | 15% | -10% landfill | $1/1000 pop/yr | No impact |
| Single-stream | 35% | -25% landfill | $3/1000 pop/yr | Minor inconvenience |
| 3-bin sorted | 55% | -45% landfill | $6/1000 pop/yr | Moderate inconvenience |
| 5-bin sorted | 70% | -60% landfill | $10/1000 pop/yr | Significant inconvenience |
| Zero-waste target | 85% | -75% landfill | $15/1000 pop/yr | High inconvenience |

**Revenue from Recyclables:** Sorted recycling generates revenue from material sales ($2-8/1000 pop/yr depending on commodity prices). This partially offsets operating costs.

---

#### EL-04: Plastic Ban

| Parameter | Options |
|-----------|---------|
| **Scope** | No ban / Single-use bags / All single-use plastics / All non-essential plastics |
| **Era Available** | Modern+ |
| **Enforcement Cost** | $1/commercial-tile/year |

| Ban Level | Plastic Waste Reduction | Retail Cost Increase | Environmental Score | Approval |
|-----------|------------------------|---------------------|--------------------|---------|
| No ban | 0% | 0% | Baseline | 0 |
| Bags only | -15% plastic waste | +1% retail cost | +5 | +3 |
| All single-use | -45% plastic waste | +4% retail cost | +15 | +2 (mixed) |
| All non-essential | -70% plastic waste | +8% retail cost | +25 | -5 (inconvenient) |

---

#### EL-05: Green Building Requirements

| Parameter | Options |
|-----------|---------|
| **Standard** | None / Basic (insulation) / Silver (efficient HVAC + insulation) / Gold (solar-ready + water recycling) / Platinum (net-zero energy) |
| **Applies To** | New construction only / New + major renovations / All buildings (retrofit mandate) |
| **Era Available** | Modern+ (requires T038 Green Architecture) |
| **Enforcement Cost** | $3/building-permit |

| Green Standard | Construction Cost | Energy Consumption | Water Usage | Land Value Premium |
|---------------|-------------------|-------------------|-------------|-------------------|
| None | Baseline | Baseline | Baseline | 0% |
| Basic | +5% | -20% | -10% | +3% |
| Silver | +12% | -40% | -25% | +8% |
| Gold | +22% | -65% | -50% | +15% |
| Platinum | +35% | -90% | -70% | +25% |

**Long-term Economics:** Green buildings cost more upfront but save on utilities over time. A Gold-standard building breaks even on the extra cost in ~8 game-years through energy savings. Platinum breaks even in ~12 years. This creates a tension between short-term budget pressure and long-term efficiency.

---

### 1.5 LABOR LAWS

Labor laws directly affect the cost of doing business, worker happiness, industrial productivity, and income inequality. They are among the most politically contentious laws in the game.

#### LL-01: Minimum Wage

| Parameter | Options |
|-----------|---------|
| **Wage Level** | Continuous slider: $0.00 - $25.00/hr (in era-adjusted currency) |
| **Adjustment** | Manual or auto-indexed to inflation/cost-of-living |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | $2/1000 employed workers/year (labor inspectors) |

The minimum wage is expressed as a **percentage of median wage** for cross-era consistency:

| Wage as % of Median | Worker Happiness | Small Business Failure Rate | Unemployment Effect | Poverty Rate | Consumer Spending |
|---------------------|-----------------|---------------------------|--------------------|--------------|--------------------|
| 0% (no minimum) | -15 (exploitation) | Baseline | -2% (cheap labor) | High (25%+) | Low |
| 30% | -5 | Baseline | Baseline | Moderate (18%) | Baseline |
| 40% | +3 | +2% | +0.5% | Lower (14%) | +5% |
| 50% | +8 | +5% | +1.5% | Low (10%) | +10% |
| 60% | +12 | +10% | +3% | Very Low (7%) | +14% |
| 70% | +10 | +18% | +6% | Minimal (5%) | +12% |
| 80% | +5 | +30% | +10% | Minimal (4%) | +8% |
| 90%+ | -5 (job losses) | +45% | +18% | Rising (jobs gone) | -5% |

**Key Design:** The minimum wage has a bell curve of effectiveness. Too low and workers suffer. Too high and businesses close, causing unemployment that hurts the same workers you tried to help. The sweet spot depends on your city's cost of living, which is driven by housing costs and services.

**Demographic Reactions:**
- Working-class citizens: Support increases up to ~70% of median
- Small business owners: Oppose any increase above 40%
- Large corporations: Relatively indifferent (already pay above minimum)
- Labor unions: Always push for higher
- Wealthy citizens: Generally oppose (higher costs reduce investment returns)
- Economists in your advisor panel: Warn about unemployment effects above 60%

**Cascading Effects:**
- Higher minimum wage -> higher consumer spending -> higher commercial tax revenue
- Higher minimum wage -> automation pressure (businesses invest in machines to replace workers)
- No minimum wage -> sweatshop industries relocate TO your city (more jobs, terrible conditions)
- Indexed to inflation: prevents wage erosion but creates predictability for businesses

---

#### LL-02: Work Hours Regulation

| Parameter | Options |
|-----------|---------|
| **Standard Work Week** | 60 hrs / 48 hrs / 44 hrs / 40 hrs / 35 hrs / 32 hrs |
| **Overtime Rules** | No overtime pay / 1.25x after standard / 1.5x after standard / 2x after standard |
| **Mandatory Rest** | None / 1 day per week / 2 days per week / 2 days + holidays |
| **Era Available** | Industrial+ (Frontier era has no regulation) |
| **Enforcement Cost** | $1/1000 workers/year |

| Work Week | Worker Productivity/hr | Total Weekly Output | Worker Happiness | Healthcare Cost |
|-----------|----------------------|--------------------|-----------------|-----------------|
| 60 hrs | 70% (exhausted) | 105% of baseline | -20 | +30% (burnout) |
| 48 hrs | 85% | 102% of baseline | -5 | +10% |
| 44 hrs | 92% | 101% of baseline | +2 | +3% |
| 40 hrs | 100% (baseline) | 100% baseline | +8 | Baseline |
| 35 hrs | 108% | 95% of baseline | +15 | -10% |
| 32 hrs | 112% | 90% of baseline | +20 | -18% |

**Design Note:** Shorter work weeks produce higher per-hour productivity (well-rested workers are more efficient) but lower total output. The 40-hour week is the historical equilibrium point. Going below 40 hours makes workers happier and healthier but reduces total economic output -- a tradeoff the player must weigh.

**Overtime Economics:** Overtime pay increases labor costs for businesses but provides extra income for workers. Heavy overtime without proper pay creates a stressed, resentful workforce that may strike.

---

#### LL-03: Child Labor Laws

| Parameter | Options |
|-----------|---------|
| **Minimum Working Age** | No limit / 10 / 12 / 14 / 16 / 18 |
| **Hours for Minors** | No limit / Max 6 hrs/day / Max 4 hrs/day / School hours only / Prohibited |
| **Hazardous Work** | Allowed / Restricted (no mines/factories) / Prohibited under 18 |
| **Era Available** | Frontier+ (era-dependent default) |
| **Enforcement Cost** | $1/1000 population/year |

| Era | Default Setting | Historical Context |
|-----|----------------|-------------------|
| Frontier | No limits (children work farms and mines) | Normal for the era |
| Industrial | Age 10+, factories allowed | Reform movements begin |
| Postwar | Age 14+, no hazardous work | Modern standards emerging |
| Modern | Age 16+, limited hours, no hazardous | International norms |
| Future | Age 18+, education mandatory | Post-scarcity norms |

| Minimum Age | Labor Supply Effect | Education Rate | Child Mortality | International Reputation |
|-------------|--------------------|--------------------|-----------------|------------------------|
| No limit | +15% unskilled labor pool | -40% school attendance | +25% child death rate | -30 (condemned) |
| Age 10 | +10% unskilled labor | -25% school attendance | +15% child death rate | -20 |
| Age 12 | +5% unskilled labor | -15% school attendance | +8% child death rate | -10 |
| Age 14 | +2% unskilled labor | -5% school attendance | +2% child death rate | Neutral |
| Age 16 | Baseline | Baseline | Baseline | Neutral |
| Age 18 | -3% unskilled labor | +10% university attendance | -5% child death rate | +5 |

**Design Intent:** In the Frontier and early Industrial eras, child labor provides a genuine economic advantage -- extra workers for farms and factories. But it comes at the cost of education (creating a long-term workforce quality problem), child mortality, and eventually public outrage as social reform movements emerge. The game does not moralize -- it lets the player experience the tradeoff and the historical reform pressure organically.

**Reform Pressure:** Starting in the Industrial era, random events push for child labor reform. Ignoring these triggers protests, newspaper scandals, and approval drops. By the Postwar era, maintaining child labor is politically untenable (massive approval penalty).

---

#### LL-04: Union Rights

| Parameter | Options |
|-----------|---------|
| **Union Status** | Banned / Discouraged (legal but restricted) / Permitted / Protected (right to organize) / Mandatory (closed shop) |
| **Strike Rights** | Illegal / Legal with cooling-off period / Legal / Protected (no replacement workers) |
| **Collective Bargaining** | None / Voluntary / Mandatory for public sector / Mandatory for all |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | $2/1000 workers/year (labor board) |

| Union Policy | Worker Wages | Industrial Productivity | Strike Frequency | Worker Happiness | Business Investment |
|-------------|-------------|----------------------|-----------------|-----------------|-------------------|
| Banned | -15% | +10% (no disruptions) | 0 (but unrest builds) | -20 | +15% (business-friendly) |
| Discouraged | -8% | +5% | Low (rare) | -10 | +10% |
| Permitted | Baseline | Baseline | Moderate | Baseline | Baseline |
| Protected | +10% | -5% | Moderate-High | +12 | -8% |
| Mandatory | +18% | -12% | High (but structured) | +15 | -20% |

**Strike Mechanics:** When unions are legal, strikes can occur during:
- Contract negotiation periods (every 3 game-years)
- After workplace accidents (if safety standards are low)
- When minimum wage is below 40% of median
- When work hours exceed 48/week
- During economic downturns (layoff resistance)

A strike stops production at affected industries for 5-30 game-days depending on severity. The player can:
1. **Negotiate** (costs money, raises wages, ends quickly)
2. **Wait it out** (no cost, but production lost, may escalate)
3. **Use police** (ends strike forcibly, massive approval drop, possible violence event)
4. **Concede all demands** (expensive, but instant resolution, +20 worker approval)

---

#### LL-05: Workplace Safety Standards

| Parameter | Options |
|-----------|---------|
| **Standard Level** | None / Basic (PPE required) / Standard (safety training + equipment) / Advanced (full OSHA-equivalent) / Extreme (zero-tolerance) |
| **Inspection Frequency** | None / Annual / Quarterly / Monthly |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | $5/industrial-tile/year (inspectors) |

| Safety Standard | Workplace Accident Rate | Industry Operating Cost | Worker Compensation Claims | Worker Happiness |
|----------------|------------------------|------------------------|---------------------------|-----------------|
| None | 12 per 1000 workers/yr | Baseline | Very High ($20/worker/yr) | -15 |
| Basic | 8 per 1000 workers/yr | +3% | High ($12/worker/yr) | -5 |
| Standard | 4 per 1000 workers/yr | +8% | Medium ($6/worker/yr) | +5 |
| Advanced | 1.5 per 1000 workers/yr | +15% | Low ($2/worker/yr) | +12 |
| Extreme | 0.3 per 1000 workers/yr | +25% | Minimal ($0.50/worker/yr) | +15 |

**Workplace Accidents:** When accidents occur, they:
- Kill or injure workers (reduces labor supply)
- Generate worker compensation claims (budget expense)
- Trigger potential strikes (if unions exist)
- Generate negative media coverage (-3 approval per major accident)
- Can cascade into disaster events (factory explosion, mine collapse)

---

### 1.6 SOCIAL LAWS

Social laws govern citizen behavior, public order, and quality of life. They are the most politically divisive category.

#### SL-01: Public Drinking Laws

| Parameter | Options |
|-----------|---------|
| **Status** | Unrestricted / Designated areas only / Prohibited in public / Prohibition (all alcohol banned) |
| **Era Available** | Frontier+ |
| **Enforcement Cost** | $2/1000 population/year (police patrols) |

| Policy | Crime Rate Effect | Tourism Impact | Tax Revenue (Alcohol) | Public Order | Healthcare Cost |
|--------|------------------|----------------|----------------------|-------------|-----------------|
| Unrestricted | +8% | +5% (party tourism) | High | -10 order | +5% |
| Designated areas | +2% | +3% | Medium-High | Neutral | +2% |
| Prohibited in public | Baseline | Baseline | Medium | +5 order | Baseline |
| Prohibition | -15% crime from alcohol | -20% tourism | $0 (black market) | +10 order initially | -10% initially |

**Prohibition Special Mechanics (Historical):**
If the player enacts full Prohibition:
- Alcohol tax revenue drops to $0
- Black market emerges (generates crime in industrial/port areas)
- Speakeasies appear as underground commercial buildings (generate crime + cultural value)
- Organized crime faction grows in power (see Corruption mechanics)
- After 5 game-years, approval drops as citizens grow resentful
- Repealing Prohibition generates a massive approval boost (+25)

---

#### SL-02: Smoking Regulations

| Parameter | Options |
|-----------|---------|
| **Indoor Smoking** | Allowed / Designated areas / Banned in workplaces / Banned in all public indoor spaces |
| **Outdoor Smoking** | Allowed / Banned near buildings / Banned in parks / Banned in all public spaces |
| **Tobacco Tax** | Low / Medium / High / Extreme / Banned |
| **Era Available** | Postwar+ (before that, smoking is universal and unregulated) |
| **Enforcement Cost** | $1/1000 population/year |

| Smoking Policy | Smoking Rate | Healthcare Cost | Restaurant Revenue | Tobacco Tax Revenue | Approval |
|---------------|-------------|-----------------|-------------------|--------------------|---------|
| Fully allowed | 35% of adults | +15% healthcare | Baseline | Low ($2/smoker/yr) | -5 (health groups angry) |
| Workplace ban | 28% of adults | +10% healthcare | +3% (non-smokers eat out more) | Medium | +3 |
| Indoor ban | 20% of adults | +5% healthcare | +8% | Medium | +8 |
| All public spaces | 12% of adults | Baseline | +10% | High ($8/smoker/yr) | +5 (smokers angry) |
| Tobacco banned | 5% (black market) | -10% healthcare | +12% | $0 | -10 (nanny state) |

**Long-term Effect:** Smoking bans take 5-10 game-years to show full healthcare cost reduction, mimicking real-world epidemiology. Short-term, they anger smokers. Long-term, they save enormous amounts on healthcare.

---

#### SL-03: Curfew Laws

| Parameter | Options |
|-----------|---------|
| **Youth Curfew** | None / Under 14 after 10pm / Under 16 after 11pm / Under 18 after midnight |
| **General Curfew** | None / Emergency only / Permanent (midnight-5am) |
| **Era Available** | All eras |
| **Enforcement Cost** | $3/1000 population/year (for youth) / $15/1000 (for general) |

| Curfew Type | Crime Reduction | Nightlife Revenue | Youth Safety | Civil Liberties Score | Approval |
|-------------|----------------|-------------------|-------------|----------------------|---------|
| None | Baseline | Baseline | Baseline | 100% | Neutral |
| Youth <14 at 10pm | -3% youth crime | No impact | +10% | 95% | +5 |
| Youth <16 at 11pm | -8% youth crime | -5% | +18% | 90% | +3 |
| Youth <18 at midnight | -12% youth crime | -15% | +25% | 80% | -2 |
| General permanent | -30% nighttime crime | -60% | +40% | 40% | -25 |

**General Curfew:** A permanent general curfew is an authoritarian measure that slashes crime but destroys nightlife, reduces civil liberties (which affects immigration of educated workers), and makes the city feel oppressive. It can only be maintained long-term under authoritarian governance styles (see Section 2).

---

#### SL-04: Pet Regulations

| Parameter | Options |
|-----------|---------|
| **Licensing** | None / Voluntary / Mandatory registration |
| **Leash Laws** | None / Parks only / All public spaces |
| **Breed Restrictions** | None / Dangerous breeds require permit / Dangerous breeds banned |
| **Noise** | None / Barking ordinance (nuisance complaints) |
| **Era Available** | Postwar+ |
| **Enforcement Cost** | $0.50/1000 population/year |

| Policy | Pet Ownership Rate | Park Usage | Noise Complaints | Revenue (Licenses) |
|--------|-------------------|-----------|------------------|-------------------|
| No regulation | High (40% of households) | Reduced (pet conflicts) | High | $0 |
| Basic licensing | High (38%) | Neutral | Medium | $1/household/yr |
| Full regulation | Moderate (30%) | Improved (+5% park happiness) | Low | $2/household/yr |

**Minor Law Design Note:** Pet regulations are a "flavor" law -- low stakes, low cost, but they add texture and generate amusing citizen petitions and news ticker items. Players enjoy these small-scale governance decisions.

---

#### SL-05: Homelessness Policy

| Parameter | Options |
|-----------|---------|
| **Approach** | Ignore / Criminalize (anti-camping, loitering laws) / Shelter-first / Housing-first / Comprehensive (housing + services + jobs) |
| **Camp Policy** | Tolerate / Designated camps / Banned with shelters / Banned without shelters |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | Varies dramatically by approach |

| Approach | Annual Cost/homeless person | Homeless Population Trend | Crime Effect | Public Space Quality | Approval |
|----------|---------------------------|--------------------------|-------------|--------------------|---------|
| Ignore | $0 | +10%/year (grows) | +15% near camps | -20 nearby land value | -15 |
| Criminalize | $8,000 (jail costs) | -5%/year (displaced) | +5% (jail cycling) | +10 (out of sight) | Mixed (-5 compassion, +5 order) |
| Shelter-first | $5,000 | -2%/year | -5% | +5 | +8 |
| Housing-first | $12,000 | -15%/year | -12% | +15 | +5 (expensive) |
| Comprehensive | $18,000 | -25%/year | -20% | +20 | +12 (if funded) |

**Homelessness Generation:** Homelessness is generated by the simulation when:
- Unemployment exceeds available welfare support
- Housing costs exceed 40% of household income for lowest wealth class
- Mental health/addiction events (random per household)
- Economic recession events
- Player demolishes residential buildings without relocation

**Visible Feedback:** Homeless camps appear as visible tile overlays near parks, underpasses, and vacant lots. They reduce surrounding land value and generate citizen complaints.

---

### 1.7 TAX LAWS

Tax laws are the primary revenue tuning mechanism. Each tax type affects different demographics and economic sectors. The full tax system interacts with the existing budget system (GDD Section 7).

#### TX-01: Property Tax

| Parameter | Options |
|-----------|---------|
| **Rate** | Continuous slider: 0.0% - 5.0% of assessed property value |
| **Assessment Frequency** | Annual / Biennial / Every 5 years |
| **Exemptions** | None / Homestead exemption (primary residence -20%) / Senior exemption (-30% for 65+) / Religious buildings exempt / Non-profit exempt |
| **Era Available** | Frontier+ |
| **Collection Cost** | $1/property/year (assessor office) |

| Tax Rate | Revenue/property/yr | Development Rate | Land Value Growth | Emigration Pressure | Homeownership Rate |
|----------|--------------------|-----------------|-----------------|--------------------|-------------------|
| 0.0% | $0 | +25% (cheap to own) | +15% | None | Very High |
| 0.5% | Low | +15% | +10% | None | High |
| 1.0% | Moderate | +5% | +5% | Minimal | Moderate-High |
| 1.5% | Good | Baseline | Baseline | Low | Moderate |
| 2.0% | High | -5% | -3% | Moderate | Moderate-Low |
| 2.5% | Very High | -12% | -8% | High | Low |
| 3.0%+ | Maximum | -25% | -15% | Very High | Very Low |
| 5.0% | Unsustainable | -50% | -30% | Extreme (exodus) | Minimal |

**Assessment Frequency:** Less frequent assessments are cheaper but create "assessment shock" when values are updated, causing sudden tax increases that anger citizens. Annual assessment is smoother but costs more to administer.

**Exemptions reduce revenue but serve social goals:**
- Homestead: Protects homeowners, shifts burden to investors/commercial
- Senior: Prevents elderly displacement from gentrifying neighborhoods
- Religious: Traditional/cultural expectation (removing it is controversial)
- Non-profit: Encourages civic organizations

---

#### TX-02: Sales Tax

| Parameter | Options |
|-----------|---------|
| **Rate** | Continuous slider: 0.0% - 15.0% |
| **Exemptions** | None / Food exempt / Food + medicine exempt / Food + medicine + clothing exempt |
| **Era Available** | Industrial+ |
| **Collection Cost** | $0.50/commercial-tile/year |

| Tax Rate | Revenue | Consumer Spending | Commercial Zone Growth | Cross-border Shopping |
|----------|---------|------------------|----------------------|---------------------|
| 0.0% | $0 | +10% | +15% | N/A |
| 3.0% | Low | +3% | +5% | None |
| 5.0% | Moderate | Baseline | Baseline | Minimal |
| 7.0% | Good | -3% | -5% | Some (to neighboring towns) |
| 10.0% | High | -8% | -12% | Significant |
| 12.0%+ | Very High | -15% | -20% | Severe (revenue actually drops) |
| 15.0% | Laffer curve peak | -25% | -30% | Extreme |

**Laffer Curve:** Sales tax follows a Laffer curve -- beyond ~12%, the tax rate is so high that consumer spending and business activity decline enough that total revenue actually decreases. This is a deliberate economic lesson baked into the simulation.

**Food Exemption:** Exempting food from sales tax is progressive (helps low-income households) but reduces revenue. The approval impact depends on demographics: working-class citizens strongly support food exemption; wealthy citizens are indifferent.

---

#### TX-03: Income Tax Brackets

| Parameter | Options |
|-----------|---------|
| **Structure** | Flat rate / 3 brackets / 5 brackets / 7 brackets |
| **Rates** | Per-bracket slider: 0% - 50% |
| **Era Available** | Industrial+ (requires T095a Mainframe Computing for complex brackets) |
| **Collection Cost** | $3/1000 employed workers/year |

**Example 5-Bracket Configuration:**

| Bracket | Income Range (% of median) | Suggested Range | Revenue Weight |
|---------|---------------------------|-----------------|---------------|
| Bracket 1 (Destitute) | 0-30% of median | 0-5% | 2% of total |
| Bracket 2 (Working) | 30-60% of median | 5-15% | 15% of total |
| Bracket 3 (Middle) | 60-120% of median | 10-25% | 35% of total |
| Bracket 4 (Upper-Middle) | 120-250% of median | 15-35% | 30% of total |
| Bracket 5 (Wealthy) | 250%+ of median | 20-50% | 18% of total |

| Tax Progressivity | Income Inequality (Gini) | Worker Satisfaction | Wealthy Emigration | Public Services Fundable |
|-------------------|-------------------------|--------------------|--------------------|------------------------|
| Regressive (higher % on poor) | Increases (+0.05) | -20 | None | Low |
| Flat (same % for all) | Neutral | -5 | Low | Moderate |
| Mildly progressive | Decreases (-0.03) | +5 | Low | Good |
| Strongly progressive | Decreases (-0.08) | +12 | Moderate | Very Good |
| Extremely progressive (50%+ top) | Decreases (-0.12) | +15 | Severe (wealthy flee) | Good (until wealthy leave) |

**Wealth Flight:** If the top income tax bracket exceeds 40%, wealthy citizens begin emigrating at an accelerating rate. They take their high tax contributions with them, potentially causing a revenue death spiral. This mirrors real-world capital flight dynamics.

---

#### TX-04: Corporate Tax

| Parameter | Options |
|-----------|---------|
| **Rate** | Continuous slider: 0% - 35% |
| **Small Business Exemption** | None / First $50K exempt / First $100K exempt |
| **Industry-Specific Rates** | Uniform / Tech (-5%) / Fossil fuels (+5%) / Custom per sector |
| **Era Available** | Postwar+ |
| **Collection Cost** | $5/1000 businesses/year |

| Corporate Tax Rate | Business Investment | Business Relocation (in) | Business Relocation (out) | Revenue | Job Growth |
|-------------------|--------------------|--------------------------|--------------------------|---------|-----------|
| 0% | +30% | +25% (tax haven) | None | $0 | +15% |
| 5% | +20% | +15% | None | Low | +10% |
| 10% | +10% | +5% | 2% leave | Moderate | +5% |
| 15% | Baseline | Baseline | 5% leave | Good | Baseline |
| 20% | -5% | -5% | 10% leave | High | -3% |
| 25% | -12% | -15% | 20% leave | Very High | -8% |
| 30%+ | -25% | -30% | 35% leave | Maximum (short-term) | -15% |
| 35% | -35% | -40% | 50% leave | Declining (businesses gone) | -25% |

**Tax Havens:** In regional play, a neighboring town with 0% corporate tax becomes a tax haven. Businesses register there while operating in your city, reducing your corporate tax revenue by 10-30%. Counter with anti-avoidance regulations (costs $10/1000 businesses/year to enforce).

---

#### TX-05: Tax Incentives & Exemptions

| Incentive | Effect | Revenue Lost | Purpose |
|-----------|--------|-------------|---------|
| **Enterprise Zone** | Designated area: 50% property tax reduction for 10 years | Significant | Attract development to blighted areas |
| **Green Energy Credit** | 20% tax credit for renewable energy installation | Moderate | Accelerate clean energy adoption |
| **Historic Rehab Credit** | 25% credit for renovating historic buildings | Low | Preserve historic architecture |
| **New Business Exemption** | First 3 years tax-free for new businesses | Moderate | Attract startups |
| **Film/Media Incentive** | 15% production cost rebate | Low | Attract cultural industry |
| **R&D Tax Credit** | 10% credit for research spending | Moderate | Boost tech sector |
| **Affordable Housing Bonus** | 30% property tax reduction for affordable units | Moderate | Incentivize affordable housing construction |
| **TIF District** | Tax Increment Financing: future tax growth funds current infrastructure | Complex | Bootstrap development in underserved areas |

**TIF District Mechanics:** Tax Increment Financing freezes the tax revenue of a designated area at its current level. All tax revenue ABOVE that baseline (from new development attracted by infrastructure improvements) goes into a special fund that pays for the infrastructure. After 20 game-years, the TIF expires and all revenue flows to the general budget. This is a real-world financing tool used by hundreds of cities.

---

### 1.8 ZONING LAWS

Zoning laws complement the existing zoning system (GDD Section 3.4) by adding regulatory overlays that control what can be built where.

#### ZL-01: Mixed-Use Permissions

| Parameter | Options |
|-----------|---------|
| **Zones Affected** | Specific districts (player-drawn) |
| **Mix Allowed** | R+C ground floor / R+C+O / Full mixed (R+C+O+light I) |
| **Density Bonus** | +0% / +25% / +50% for mixed-use buildings |
| **Era Available** | Postwar+ (requires T091 Urban Planning Department) |
| **Enforcement Cost** | $1/mixed-use-tile/year |

| Mix Level | Walkability Score | Transit Ridership | Land Value | Traffic Generation |
|-----------|------------------|-------------------|------------|-------------------|
| Single-use only | Baseline | Baseline | Baseline | Baseline (car trips) |
| R+C ground floor | +20 walkability | +15% | +10% | -12% car trips |
| R+C+O | +30 walkability | +25% | +18% | -20% car trips |
| Full mixed | +35 walkability | +30% | +22% | -25% car trips |

**Design Principle:** Mixed-use zoning is one of the most powerful tools for creating walkable, transit-friendly neighborhoods. But it requires careful management -- if the mix is wrong (too much commercial, not enough residential), it creates noise and parking conflicts.

---

#### ZL-02: Density Limits

| Parameter | Options |
|-----------|---------|
| **Zone** | Per-district or citywide |
| **FAR (Floor Area Ratio)** | 0.5 / 1.0 / 2.0 / 4.0 / 8.0 / 12.0 / Unlimited |
| **Units per Acre** | 4 / 8 / 20 / 50 / 100 / 200 / Unlimited |
| **Era Available** | Industrial+ |

| FAR | Building Type | Population Density | Infrastructure Demand | Land Value |
|-----|-------------|-------------------|-----------------------|-----------|
| 0.5 | Single-family detached | Very Low | Low (but spread out) | Low-Moderate |
| 1.0 | Duplexes, townhouses | Low | Low | Moderate |
| 2.0 | Low-rise apartments | Medium | Medium | Moderate-High |
| 4.0 | Mid-rise apartments | High | High | High |
| 8.0 | High-rise towers | Very High | Very High | Very High |
| 12.0+ | Supertall towers | Extreme | Extreme | Extreme |

**FAR Bonus System:** The player can grant FAR bonuses for developments that include public amenities:
- +0.5 FAR for including affordable housing units (20%+ of units)
- +1.0 FAR for including public plaza or park
- +0.5 FAR for achieving Green Building Gold+ standard
- +1.0 FAR for including transit station integration

---

#### ZL-03: Industrial Buffer Zones

| Parameter | Options |
|-----------|---------|
| **Buffer Width** | 0 tiles / 2 tiles / 4 tiles / 6 tiles / 8 tiles |
| **Buffer Type** | Empty (wasteland) / Green belt (park) / Commercial transition / Mixed-use transition |
| **Era Available** | Industrial+ |
| **Enforcement Cost** | $0 (zoning is free to enforce) |

| Buffer Width | Residential Health Impact | Land Use Efficiency | Pollution Exposure | Visual Quality |
|-------------|--------------------------|--------------------|--------------------|---------------|
| 0 tiles | -20 health in adjacent residential | 100% | Full | Ugly (factory next to homes) |
| 2 tiles | -10 health | 95% | 60% | Poor |
| 4 tiles | -3 health | 88% | 30% | Acceptable |
| 6 tiles | Negligible | 80% | 10% | Good |
| 8 tiles (green belt) | None | 72% | 0% | Excellent (park buffer) |

**Green Belt Buffer:** If the buffer zone is designated as green belt, it functions as a park strip that provides recreation value while insulating residential areas from industrial pollution. This is expensive in land use but creates premium residential zones adjacent to the belt.

---

## 2. POLITICAL SYSTEM

### 2.1 Governance Styles

The player's governance style is determined by their pattern of decisions, not an explicit choice. The game tracks a **Governance Index** across two axes:

**Axis 1: Authority (0-100)**
- 0 = Direct Democracy (citizens vote on everything)
- 25 = Representative Democracy (elected council with strong citizen input)
- 50 = Managed Democracy (player has broad authority, council is advisory)
- 75 = Technocracy (experts make decisions, citizens have limited input)
- 100 = Authoritarian (player has absolute power, no citizen input)

**Axis 2: Economic Model (0-100)**
- 0 = Free Market (minimal regulation, low taxes, private services)
- 25 = Mixed Economy (moderate regulation, balanced public/private)
- 50 = Social Democracy (strong regulation, high taxes, extensive public services)
- 75 = State Capitalism (government owns key industries, directs economy)
- 100 = Command Economy (government controls all production and distribution)

These axes combine to produce a governance label:

| Authority \ Economy | Free Market (0-25) | Mixed (25-50) | Social Dem (50-75) | State-Directed (75-100) |
|--------------------|-------------------|---------------|-------------------|------------------------|
| **Democratic (0-25)** | Libertarian Democracy | Liberal Democracy | Nordic Social Democracy | Democratic Socialism |
| **Managed (25-50)** | Business Republic | Centrist Republic | Progressive Republic | Welfare State |
| **Technocratic (50-75)** | Singapore Model | Developmental State | Techno-Socialism | Planned Economy |
| **Authoritarian (75-100)** | Oligarchy | Managed Autocracy | Populist Autocracy | Totalitarian State |

**Governance Drift:** The governance index shifts based on player actions:
- Passing strict laws without council approval: +2 Authority per law
- Holding referendums: -3 Authority per referendum
- Nationalizing an industry: +5 Economic (toward state-directed)
- Cutting taxes and deregulating: -5 Economic (toward free market)
- Censoring media: +5 Authority
- Expanding welfare programs: +3 Economic

**Governance Effects:**

| Governance Style | Immigration | Business Investment | Citizen Happiness | International Prestige | Corruption Risk |
|-----------------|------------|--------------------|--------------------|----------------------|----------------|
| Libertarian Democracy | +15% (freedom) | +25% | Variable (unequal) | High | Low |
| Liberal Democracy | +10% | +15% | +10 | High | Low |
| Nordic Social Democracy | +5% | -5% | +20 | Very High | Very Low |
| Singapore Model | +8% (skilled only) | +30% | +5 (content but not free) | High | Low |
| Oligarchy | -10% | +20% (for connected) | -15 | Low | Very High |
| Authoritarian | -20% (brain drain) | -10% (unless subsidized) | -25 | Very Low | Very High |
| Totalitarian State | -30% | -30% | -40 | Pariah | Extreme |

---

### 2.2 City Council & Factions

The City Council is a 9-seat governing body. Council members belong to factions, and faction strength is determined by elections (see 2.3).

#### Council Factions

**1. Progressive Alliance (Color: Purple)**
| Attribute | Details |
|-----------|---------|
| **Core Values** | Social equality, environmental protection, transit investment, housing affordability |
| **Voter Base** | Young adults (18-35), university-educated, renters, low-income workers |
| **Policies Supported** | High minimum wage, rent control, green building codes, cycling infrastructure, public housing, progressive taxation |
| **Policies Opposed** | Highway expansion, corporate tax cuts, deregulation, gentrification |
| **Strength Grows When** | Inequality rises, housing costs spike, environmental quality drops |
| **Strength Shrinks When** | Economy booms, wages rise naturally, environmental quality is good |

**2. Conservative Coalition (Color: Blue)**
| Attribute | Details |
|-----------|---------|
| **Core Values** | Fiscal responsibility, property rights, traditional values, low taxes |
| **Voter Base** | Homeowners, wealthy citizens, suburban residents, business owners, elderly |
| **Policies Supported** | Low taxes, property rights protection, road investment, police funding, historic preservation (selective), development deregulation |
| **Policies Opposed** | Tax increases, rent control, mandatory affordable housing, new transit spending |
| **Strength Grows When** | Taxes rise, crime increases, property values threatened, rapid demographic change |
| **Strength Shrinks When** | Economy is stable, crime is low, taxes are moderate |

**3. Business First Party (Color: Gold)**
| Attribute | Details |
|-----------|---------|
| **Core Values** | Economic growth, business-friendly environment, infrastructure investment, efficiency |
| **Voter Base** | Business owners, corporate employees, developers, skilled professionals |
| **Policies Supported** | Low corporate tax, enterprise zones, highway/airport expansion, deregulation, skilled immigration |
| **Policies Opposed** | High minimum wage, strict environmental regulations, union protections, restrictive zoning |
| **Strength Grows When** | Unemployment is high, businesses are leaving, economic stagnation |
| **Strength Shrinks When** | Economy is strong, inequality becomes visible, pollution is high |

**4. Green Party (Color: Green)**
| Attribute | Details |
|-----------|---------|
| **Core Values** | Environmental protection, sustainability, public transit, renewable energy, quality of life |
| **Voter Base** | Environmentally conscious citizens, young families, health-conscious residents, academics |
| **Policies Supported** | Emission limits, green building codes, cycling infrastructure, renewable energy mandates, congestion charges, plastic bans, park investment |
| **Policies Opposed** | Highway expansion, industrial zoning near residential, fossil fuel subsidies, airport expansion |
| **Strength Grows When** | Pollution rises, climate events occur, health issues from environment |
| **Strength Shrinks When** | Environment is clean, energy is cheap, green policies cause economic pain |

**5. Populist Movement (Color: Orange)**
| Attribute | Details |
|-----------|---------|
| **Core Values** | "Regular people" vs. elites, anti-establishment, local identity, anti-immigration (in some variants), strong welfare |
| **Voter Base** | Working class, unemployed, economically displaced, culturally conservative working families |
| **Policies Supported** | Protectionist trade policy, anti-gentrification measures, strong policing, immigration controls, industrial job protection, welfare for "deserving" citizens |
| **Policies Opposed** | Free trade, tech industry subsidies, cosmopolitan policies, high-density development, elite cultural spending |
| **Strength Grows When** | Inequality is extreme, unemployment rises, rapid demographic/cultural change, elite neighborhoods thrive while others decline |
| **Strength Shrinks When** | Broad economic prosperity, low inequality, stable demographics |

#### Council Mechanics

**Seat Allocation:**
```
Each faction gets seats proportional to their vote share:
  seats = round(vote_share * 9)
  Minimum: 1 seat for any faction with >10% vote share
  Maximum: 7 seats (absolute majority)
```

**Council Voting on Laws:**
When the player proposes a law, each faction votes based on alignment:
- **Strongly Support**: Faction votes YES, boosts approval among their voters
- **Support**: Faction votes YES
- **Neutral**: Faction abstains
- **Oppose**: Faction votes NO
- **Strongly Oppose**: Faction votes NO, may trigger protest from their voters

**Required Votes by Law Type:**
| Law Type | Votes Needed | Player Override |
|----------|-------------|----------------|
| Minor ordinance (pet laws, parking) | Simple majority (5/9) | Can override with -5 political capital |
| Standard law (speed limits, building codes) | Simple majority (5/9) | Can override with -10 political capital |
| Major policy (tax rates, zoning overhaul) | 6/9 votes | Can override with -20 political capital |
| Constitutional change (governance structure) | 7/9 votes | Cannot override |
| Emergency decree (disaster response) | No vote needed | Free during declared emergencies |

**Political Capital:**
Political capital is a resource that regenerates slowly and is spent to force through unpopular legislation or override council opposition.

| Source | Political Capital Gained |
|--------|------------------------|
| High approval rating (>70%) | +3/game-month |
| Moderate approval (50-70%) | +1/game-month |
| Low approval (<50%) | -1/game-month |
| Winning a referendum | +15 one-time |
| Fulfilling citizen petition | +5 one-time |
| Successful event response (disaster, crisis) | +10 one-time |
| Economic boom | +5 one-time |
| Completing major infrastructure | +8 one-time |

| Action | Political Capital Cost |
|--------|----------------------|
| Override minor council vote | 5 |
| Override standard council vote | 10 |
| Override major policy vote | 20 |
| Call snap referendum | 8 |
| Dismiss a council member | 25 |
| Declare emergency powers | 15 |
| Suppress protest | 30 |

---

### 2.3 Elections

Elections occur on a cycle determined by governance style:

| Governance Style | Election Frequency | What is Elected |
|-----------------|-------------------|-----------------|
| Democratic | Every 4 game-years | Mayor (player's approval) + Council seats |
| Managed | Every 6 game-years | Council seats only (player stays in power) |
| Technocratic | Every 8 game-years | Advisory board (cosmetic) |
| Authoritarian | None (or sham elections) | Nothing (player has absolute power) |

#### Election Mechanics (Democratic Mode)

**Pre-Election Phase (6 game-months before):**
- Campaign events appear: debates, rallies, scandals
- Factions announce platforms (auto-generated based on current city issues)
- Media coverage shifts public opinion
- Lobbying intensifies (see 2.5)

**Vote Calculation:**
```
For each faction:
  base_vote = faction_natural_base (from demographic composition)

  modifiers:
    + issue_alignment * 0.3    (how well faction addresses top 3 city issues)
    + recent_performance * 0.2  (did their policies work when they had seats?)
    + media_influence * 0.15    (newspaper/TV/social media endorsements)
    + campaign_spending * 0.1   (funded by donations from aligned demographics)
    + incumbent_bonus * 0.1     (if they hold seats, +5% base)
    + scandal_penalty * 0.15    (corruption scandals reduce faction vote)

  final_vote = base_vote * sum(modifiers)
  normalized to 100% across all factions
```

**Mayoral Approval (Player's Election):**
If the player's approval rating is below 40% at election time in a democratic system, an **opposition candidate** emerges. The election becomes:
- Player approval > 60%: Easy win, +10 political capital
- Player approval 50-60%: Win, no bonus
- Player approval 40-50%: Narrow win, -5 political capital (mandate weakened)
- Player approval 30-40%: **Loss.** Player gets "voted out" event.

**"Voted Out" Mechanic:** The player does NOT lose the game. Instead:
- New AI mayor takes over for 4 game-years
- AI mayor implements platform policies (may undo player's work)
- Player observes consequences (often bad decisions by AI)
- Next election: player can run again and likely wins (citizens realize they miss competent management)
- This creates a "lost decade" dynamic that punishes poor governance without ending the game

---

### 2.4 Lobbying System

Industry groups and special interests lobby the player and council to influence policy.

#### Lobby Groups

| Lobby Group | Represents | Budget (relative) | Primary Goals |
|------------|-----------|-------------------|---------------|
| **Real Estate Association** | Developers, landlords | Very High | Reduce building regulations, lower property tax, reduce affordable housing mandates |
| **Chamber of Commerce** | Small businesses | High | Lower business taxes, reduce labor regulations, more parking |
| **Industrial Alliance** | Factory owners, mining | High | Reduce emission standards, lower corporate tax, oppose union protections |
| **Transit Riders Union** | Public transit commuters | Low | More transit funding, lower fares, better service |
| **Teachers Union** | Educators | Medium | Higher education funding, smaller class sizes, teacher pay |
| **Police Benevolent Assoc.** | Law enforcement | Medium | Higher police budget, tougher crime laws, oppose civilian oversight |
| **Environmental Coalition** | Green organizations | Medium | Stricter emission laws, more parks, renewable energy mandates |
| **Hospitality Industry** | Hotels, restaurants, bars | Medium | Lower alcohol regulations, more tourism spending, oppose noise ordinances |
| **Tech Industry Council** | Tech companies | Very High | Fiber infrastructure, skilled immigration, low regulation, R&D tax credits |
| **Fossil Fuel Lobby** | Oil/gas/coal companies | Very High (era-dependent) | Oppose renewable mandates, lower emission standards, subsidies |
| **Automobile Association** | Car manufacturers, dealers | High | More roads, more parking, oppose congestion charges, oppose transit |
| **Homeowners Association** | Suburban homeowners | Medium | Low property tax, restrict density, oppose affordable housing nearby |

#### Lobbying Mechanics

**Influence Actions (per game-year):**
Each lobby group can take the following actions based on their budget:

| Action | Cost to Lobby | Effect |
|--------|-------------|--------|
| **Campaign Donation** | $$$ | Boosts aligned faction's election votes by 2-5% |
| **Media Campaign** | $$ | Shifts public opinion on one issue by +/- 8 points |
| **Direct Lobbying** | $ | Sends "request" to player (accept for political capital, reject for lobby hostility) |
| **Council Influence** | $$ | Makes 1 council member change vote on upcoming law |
| **Grassroots Campaign** | $$ | Generates citizen petitions aligned with lobby goals |
| **Research Report** | $ | Publishes biased study that shifts advisor recommendations |

**Player Interaction with Lobbies:**
When a lobby group makes a request, the player sees:
- What they want (e.g., "Reduce emission standards to Tier 1")
- What they offer (e.g., "+15 political capital, +$50,000 campaign fund")
- What happens if refused (e.g., "Media campaign against your environmental policy")

Accepting lobby requests:
- Grants immediate benefit (political capital, money, faction support)
- But shifts governance toward their interests
- Can trigger corruption investigation (see 2.6) if too many lobby requests accepted
- Opposing factions lose trust in the player

---

### 2.5 Corruption Mechanics

Corruption is a systemic risk that increases with certain governance choices and decreases with transparency and oversight.

#### Corruption Index (0-100)

```
corruption_index = base_corruption
  + lobby_influence_score * 0.25       (how many lobby requests accepted)
  + underfunded_enforcement * 0.20     (laws enacted but not enforced)
  + low_transparency * 0.15            (no press freedom, no oversight)
  + authoritarian_bonus * 0.15         (higher authority = more corruption opportunity)
  + economic_inequality * 0.10         (high inequality breeds corruption)
  + rapid_growth_pressure * 0.10       (fast-growing cities have more corruption opportunities)
  + random_events * 0.05               (some officials are just corrupt)
```

| Corruption Level | Range | Effects |
|-----------------|-------|---------|
| Clean | 0-15 | +10% business confidence, +5 international reputation, -5% construction cost |
| Low | 15-30 | No significant effects |
| Moderate | 30-50 | -5% tax collection efficiency, +10% construction cost (kickbacks), random scandal events |
| High | 50-70 | -15% tax collection, +25% construction cost, regular scandals, -10 approval, skilled emigration |
| Severe | 70-85 | -25% tax collection, +40% construction cost, infrastructure quality drops, major approval crisis |
| Systemic | 85-100 | -40% tax collection, +60% construction cost, services fail, possible state intervention event |

#### Corruption Events

| Event | Trigger | Effect | Player Response Options |
|-------|---------|--------|----------------------|
| **Building Permit Scandal** | Corruption >30, building boom | Inspector caught taking bribes. Buildings may not meet code. | Investigate (-$5000, find unsafe buildings) / Cover up (corruption +5) |
| **Embezzlement** | Corruption >40 | City official diverts $10,000-$100,000 from budget | Prosecute (corruption -10, costs trial) / Ignore (corruption +8) |
| **No-Bid Contract** | Corruption >30, infrastructure project | Construction contract goes to mayor's friend at 30% markup | Cancel and rebid (-political capital) / Allow it (corruption +5, saves time) |
| **Police Corruption** | Corruption >50, police force exists | Police officers taking protection money. Crime stats are falsified. | Reform department ($50,000, crime temporarily spikes) / Ignore (crime slowly rises) |
| **Election Fraud** | Corruption >60, election year | Ballot stuffing discovered. Faction alignment shifts. | Annul results (crisis, new election) / Certify results (corruption +15, legitimacy crisis) |
| **Organized Crime** | Corruption >50, prohibition OR port city | Crime syndicate infiltrates city government. | Crackdown (expensive, violent, effective) / Coexist (crime revenue, corruption permanent) |
| **Whistleblower** | Random (higher chance at high corruption) | Employee leaks corruption evidence to media | Protect whistleblower (-corruption, +approval) / Retaliate (+corruption, chance of bigger scandal) |
| **Federal/State Investigation** | Corruption >70 | External authority investigates city. Budget frozen temporarily. | Cooperate (corruption reset to 30, lose political capital) / Obstruct (50% chance of forced removal) |

#### Anti-Corruption Measures

| Measure | Cost | Corruption Reduction | Side Effects |
|---------|------|---------------------|-------------|
| **Independent Auditor** | $10/1000 pop/yr | -2/year | Slows construction approvals |
| **Press Freedom Laws** | $0 (policy) | -5/year | Media can also criticize player |
| **Transparency Portal** | $5/1000 pop/yr (Modern+) | -3/year | Citizens become more politically engaged |
| **Civilian Oversight Board** | $8/1000 pop/yr | -4/year | Police/services resist oversight |
| **Anti-Bribery Laws** | $3/1000 pop/yr enforcement | -3/year | Lobby groups become hostile |
| **Term Limits** (for council) | $0 (policy) | -2/year | Lose experienced council members |
| **Campaign Finance Reform** | $2/1000 pop/yr | -5/year | Lobby influence reduced 40% |

---

### 2.6 Opposition Movements & Protests

Citizens are not passive. When policies cause sufficient discontent, organized opposition emerges.

#### Protest Triggers

| Cause | Protest Threshold | Severity |
|-------|-------------------|----------|
| Tax increase >3% in single year | 60% of affected demographic unhappy | Moderate |
| Major demolition (100+ homes) | 80% of displaced residents | Severe |
| Environmental disaster | 50% of nearby population | Large |
| Police brutality event | 40% of young adults | Large |
| Rapid gentrification | 70% of displaced class | Moderate-Severe |
| Unfunded pension crisis | 60% of retirees | Moderate |
| Housing affordability crisis | 50% of renters spending >40% on rent | Large |
| Corruption scandal | 30% of all citizens | Variable |
| Austerity cuts (services reduced) | 50% of service users | Moderate |
| Highway through neighborhood | 90% of affected residents | Severe (NIMBY) |

#### Protest Mechanics

**Protest Phases:**
1. **Petition** (Week 1-2): Citizens gather signatures. News ticker announces grievance. No gameplay effect.
2. **Rally** (Week 3-4): Citizens gather at city hall or affected area. -5 approval, productivity drops in area.
3. **March** (Week 5-8): Protest grows. Roads in area have -30% capacity (blocked). -10 approval. Media coverage.
4. **Sustained Protest** (Week 9+): Permanent occupation of public space. -15 approval. Tourism drops. Business closure in area.
5. **Escalation** (if ignored 12+ weeks): Possible riot event. Property damage. Police response required.

**Player Response to Protests:**

| Response | Cost | Effect | Approval Impact |
|----------|------|--------|-----------------|
| **Listen & Negotiate** | Time + partial concession | Protest ends in 1-2 weeks. Must address grievance partially. | +5 among protesters, -3 among opponents |
| **Full Concession** | Reverse the policy | Protest ends immediately. Policy is reversed. | +15 among protesters, -10 political capital |
| **Ignore** | $0 | Protest continues and may escalate. | -2/week ongoing |
| **Token Gesture** | Small policy change | 50% chance protest subsides, 50% seen as insult (escalates) | Neutral or -5 |
| **Police Dispersal** | $10,000 + -25 approval | Protest ends. Risk of violence event. International media coverage. | -25 general, +5 among "law and order" voters |
| **Martial Law** | Extreme (only authoritarian governance) | All protests suppressed. Civil liberties suspended. | -40 general, massive emigration of educated citizens |

---

### 2.7 Media System

Media acts as an intermediary between the player's decisions and public opinion. Media quality and freedom affect how accurately citizens perceive the city's condition.

#### Media Outlets (by Era)

| Era | Media Type | Reach | Influence Weight |
|-----|-----------|-------|-----------------|
| Frontier | Town crier / Notice board | Local only | 5% |
| Industrial | Newspaper | City-wide | 20% |
| Postwar | Newspaper + Radio | City-wide + regional | 35% |
| Modern | Newspaper + Radio + TV | Full reach | 50% |
| Future | All above + Social media + Citizen journalism | Hyper-local + viral | 60% |

#### Media Mechanics

**Media Ownership:**
- **Independent Press** (default): Reports factually with slight bias based on ownership
- **State-Controlled Media** (authoritarian option): Reports what the player wants (+10 approval illusion, but citizens lose trust over time, -15 immigration)
- **Corporate-Owned Media** (if media acquired by lobby): Reports with heavy bias toward owning industry

**Story Generation:**
Each game-month, the media generates 2-4 stories based on:
```
story_priority = event_severity * 0.4
  + citizen_impact * 0.3
  + novelty * 0.2
  + media_bias * 0.1
```

**Story Types:**
| Type | Trigger | Effect |
|------|---------|--------|
| **Positive Coverage** | Good event, service improvement, economic growth | +3 to +8 approval |
| **Negative Coverage** | Scandal, disaster, policy failure | -3 to -15 approval |
| **Investigative Report** | Corruption, underfunded services, hidden problems | Forces player to respond or lose -10 approval |
| **Human Interest** | Random citizen story linked to policy | Small approval shift, +/- 2-5 |
| **Editorial/Opinion** | Current policy debate | Shifts opinion on specific issue +/- 5-10 |
| **Crisis Coverage** | Disaster, emergency | Can amplify or dampen crisis depending on response speed |

**Press Freedom Index (0-100):**
- Free press (80-100): Stories are accurate. Citizens make informed decisions. High immigration of educated workers.
- Partially free (40-79): Some stories suppressed. Citizens have incomplete picture. Moderate trust.
- Controlled (0-39): Most stories are propaganda. Citizens distrust government. Emigration of educated workers. International reputation damaged.

**Player Interaction with Media:**
| Action | Cost | Effect |
|--------|------|--------|
| **Press Conference** | 0 (1 per month max) | Frame a story positively. +5 approval on chosen issue if credible. |
| **Spin/PR Campaign** | $5,000 | Counter negative story. 60% effective. |
| **Leak to Press** | 0 | Plant a story about opposition faction. Risky if discovered (scandal). |
| **Censor Story** | $0 but -5 press freedom | Block one negative story. 80% effective. If caught, scandal. |
| **Media Subsidy** | $10,000/year | Media reports more favorably. +3 approval but -5 press freedom. |
| **Invest in Public Media** | $20,000/year | Neutral, factual reporting. +5 press freedom. Trusted by citizens. |

---

## 3. POLICY PACKAGES

Policy Packages are pre-built combinations of laws and settings that represent real-world governance philosophies. The player can apply a package as a starting template and then customize individual settings. Each package also serves as a late-game achievement: "Build a city that matches the Singapore Model criteria."

### 3.1 The Singapore Model

**Philosophy:** Maximum efficiency through strict governance. High economic freedom with low personal freedom. Technocratic leadership that prioritizes results over process.

| Category | Settings |
|----------|----------|
| **Governance** | Technocracy (Authority: 70, Economy: 30) |
| **Traffic** | Speed limits: strict. Congestion charge: $12. Vehicle ownership tax: extreme ($50,000 permit). Public transit: massive investment. |
| **Building** | Height limits: strategic (high in designated centers, low elsewhere). Fire/seismic: maximum. Green building: Gold standard mandatory. |
| **Environmental** | Emission limits: Tier 3. Noise: moderate. Recycling: mandatory 5-bin. Green space: 30% of land area mandated. |
| **Labor** | Min wage: 40% of median. Work hours: 44. Unions: discouraged. Safety: advanced. |
| **Social** | Public drinking: designated areas. Smoking: comprehensive ban. Curfew: none. Littering: extreme fines ($1,000+). Chewing gum: banned. |
| **Tax** | Property: 1.0% (low). Sales: 7%. Income: flat 15%. Corporate: 10% (competitive). |
| **Zoning** | Master-planned districts. Mixed-use in transit corridors. Industrial isolated on periphery. |

| Metric | Expected Outcome |
|--------|-----------------|
| GDP per capita | Top 10% |
| Crime rate | Very Low |
| Citizen happiness | 62 (content but not thrilled) |
| Civil liberties score | 35/100 |
| Corruption | Very Low (15) |
| Income inequality | High (Gini 0.45) |
| Environmental quality | Good |
| Transit mode share | 65% public transit |
| Immigration | High (skilled workers), Low (unskilled) |

**Achievement Condition:** Population >50,000 AND GDP per capita top 10% AND crime rate bottom 10% AND civil liberties <40 AND transit share >60%.

---

### 3.2 The Nordic Model

**Philosophy:** High taxes fund comprehensive public services. Strong social safety net with high personal freedom. Emphasis on equality, education, and quality of life.

| Category | Settings |
|----------|----------|
| **Governance** | Social Democracy (Authority: 20, Economy: 60) |
| **Traffic** | Speed limits: 30 km/h residential, 50 arterials, 110 highways. Congestion charge: $8 in city center. Cycling infrastructure: extensive. |
| **Building** | Height limits: moderate (8-12 stories). Full fire/seismic codes. Green building: Platinum mandatory for new construction. |
| **Environmental** | Emission limits: Tier 3. Noise: strict. Recycling: mandatory 5-bin + composting. Plastic ban: all single-use. Carbon tax. |
| **Labor** | Min wage: 60% of median (or sectoral bargaining). Work hours: 35. Unions: protected + mandatory collective bargaining. Safety: advanced. |
| **Social** | Public drinking: permitted with responsibility. Smoking: indoor ban. No curfew. Comprehensive homelessness policy (housing-first). Generous parental leave. |
| **Tax** | Property: 1.5%. Sales: 10% (food exempt). Income: strongly progressive (10%-48%). Corporate: 22%. |
| **Zoning** | Mixed-use encouraged. High density near transit. 40% green space target. Social housing: 20% of all new developments. |

| Metric | Expected Outcome |
|--------|-----------------|
| GDP per capita | High (top 25%) |
| Crime rate | Very Low |
| Citizen happiness | 82 (among highest possible) |
| Civil liberties score | 92/100 |
| Corruption | Very Low (10) |
| Income inequality | Very Low (Gini 0.26) |
| Environmental quality | Excellent |
| Transit mode share | 40% transit, 25% cycling, 20% walking, 15% car |
| Budget pressure | High (expensive to maintain) |
| Immigration | Very High (quality of life attracts) |

**Achievement Condition:** Population >30,000 AND happiness >80 AND Gini <0.30 AND environmental quality "Excellent" AND homelessness <0.1%.

---

### 3.3 The American Sprawl Model

**Philosophy:** Maximum personal freedom, car-centric development, low taxes, high inequality, private solutions over public services.

| Category | Settings |
|----------|----------|
| **Governance** | Libertarian Democracy (Authority: 15, Economy: 15) |
| **Traffic** | Speed limits: high (45 mph residential, 65 highway). No congestion charge. Parking minimums: 2.0 spaces/unit. No speed cameras. |
| **Building** | Height limits: low in suburbs (2-4 stories), high downtown. Minimal fire codes. No seismic (unless forced by disaster). No green building requirement. |
| **Environmental** | Emission limits: Tier 1 (minimal). Noise: lenient. Recycling: voluntary. No plastic ban. |
| **Labor** | Min wage: 30% of median. Work hours: 48 standard. Unions: discouraged. Safety: basic. |
| **Social** | Public drinking: prohibited. Smoking: workplaces only. Youth curfew. Homelessness: criminalized. |
| **Tax** | Property: 2.0% (primary revenue). Sales: 8%. Income: mildly progressive (10%-28%). Corporate: 12%. Many exemptions. |
| **Zoning** | Strict single-use Euclidean zoning. Large lots. Mandatory setbacks. Separated by car-scale distances. |

| Metric | Expected Outcome |
|--------|-----------------|
| GDP per capita | High (economy grows fast) |
| Crime rate | Moderate-High |
| Citizen happiness | 55 (divided: wealthy happy, poor unhappy) |
| Civil liberties score | 78/100 |
| Corruption | Moderate (35, lobbying influence) |
| Income inequality | Very High (Gini 0.48) |
| Environmental quality | Poor-Moderate |
| Transit mode share | 85% car, 8% transit, 5% walking, 2% cycling |
| Infrastructure cost | Very High (roads, highways, parking everywhere) |
| Healthcare cost | Very High (car accidents, pollution, obesity from car dependency) |

**Achievement Condition:** Population >80,000 AND GDP per capita top 15% AND car mode share >80% AND Gini >0.40 AND average lot size >400 sq meters.

---

### 3.4 The Green City Model

**Philosophy:** Environmental sustainability as the organizing principle. Transit-oriented development, renewable energy, circular economy, high quality of life.

| Category | Settings |
|----------|----------|
| **Governance** | Progressive Republic (Authority: 30, Economy: 50) |
| **Traffic** | Speed limits: 30 km/h citywide, 20 near schools. Congestion charge: $15. Zero parking minimums. Car-free zones in center. Cycling superhighways. |
| **Building** | Height limits: mid-rise (6-8 stories) to reduce elevator energy. Green building: Platinum mandatory. Solar panels required. Green roofs mandatory. |
| **Environmental** | Zero-emission mandate (long-term). Strict noise. Mandatory composting. All plastics banned. Circular economy requirements. |
| **Labor** | Min wage: 55% of median. Work hours: 35. Unions: permitted. Safety: advanced. Green job training programs. |
| **Social** | No curfew. Smoking: all public spaces banned. Housing-first homelessness policy. Urban farming encouraged. Community gardens in every district. |
| **Tax** | Property: 1.5%. Sales: 7% (local goods exempt). Income: progressive (12%-40%). Corporate: 18%. Carbon tax: $50/ton. Green tax credits generous. |
| **Zoning** | Mixed-use everywhere. 15-minute city design (all services within 15-min walk). Industrial: clean tech only. 50% green space target. |

| Metric | Expected Outcome |
|--------|-----------------|
| GDP per capita | Moderate (slower growth but sustainable) |
| Crime rate | Low |
| Citizen happiness | 75 |
| Civil liberties score | 85/100 |
| Corruption | Low (20) |
| Income inequality | Low (Gini 0.30) |
| Environmental quality | Outstanding |
| Transit mode share | 30% transit, 35% cycling, 25% walking, 10% car |
| Energy source | 90%+ renewable |
| Carbon emissions | Near-zero |

**Achievement Condition:** Population >25,000 AND carbon emissions <10% of era average AND renewable energy >90% AND car mode share <15% AND green space >45%.

---

### 3.5 The Industrial Powerhouse Model

**Philosophy:** Heavy industry and manufacturing as the economic engine. High employment, moderate wages, significant pollution, strong unions, working-class identity.

| Category | Settings |
|----------|----------|
| **Governance** | Managed Democracy (Authority: 45, Economy: 55) |
| **Traffic** | Speed limits: moderate. Heavy truck routes designated. Rail freight prioritized. |
| **Building** | Height limits: medium. Strong fire codes (industrial fires are catastrophic). Standard earthquake codes. |
| **Environmental** | Emission limits: Tier 1 (industry-friendly). Noise: lenient near industrial zones. Basic recycling. |
| **Labor** | Min wage: 50% of median. Work hours: 40. Unions: protected (closed shop in heavy industry). Safety: standard. |
| **Social** | Public drinking: designated areas. No special restrictions. Strong worker culture: pubs, sports clubs, community centers. |
| **Tax** | Property: 1.0%. Sales: 5%. Income: flat 18%. Corporate: 8% (attract industry). Industrial land: discounted. |
| **Zoning** | Large industrial zones. Worker housing nearby. Buffer zones minimal (cost trade-off). Rail-served industrial parks. |

| Metric | Expected Outcome |
|--------|-----------------|
| GDP per capita | Moderate-High |
| Unemployment | Very Low (2-3%) |
| Citizen happiness | 60 (employed but polluted) |
| Environmental quality | Poor |
| Healthcare cost | High (industrial disease, pollution) |
| Transit mode share | 50% car, 30% transit, 15% walking, 5% cycling |
| Export revenue | Very High |
| Vulnerability | Automation crisis in Modern era; requires economic transition |

---

### 3.6 The Tourist Paradise Model

**Philosophy:** Economy driven by tourism, hospitality, and cultural attractions. Beautiful city with heavy investment in aesthetics, entertainment, and visitor infrastructure.

| Category | Settings |
|----------|----------|
| **Governance** | Liberal Democracy (Authority: 20, Economy: 25) |
| **Traffic** | Pedestrian zones in tourist areas. Scenic transit routes (trams, ferries). Moderate speed limits. |
| **Building** | Strict height limits in historic core. Historic preservation: full building. Facade standards in tourist zones. |
| **Environmental** | Strong noise ordinances (peaceful ambiance). Clean streets (heavy waste collection). Green space: parks and waterfront promenades. |
| **Labor** | Min wage: 45% of median. Seasonal employment allowed. Hospitality industry exempt from overtime. |
| **Social** | Public drinking: permitted (cafe culture). Smoking: outdoor cafes allowed. Vibrant nightlife policies. Cultural festival funding. |
| **Tax** | Hotel tax: 12%. Tourist attraction tax. Low resident taxes. Sales tax: 8% (tourists pay disproportionately). |
| **Zoning** | Historic core preservation. Waterfront development. Mixed-use in entertainment districts. Cultural facility zones. |

| Metric | Expected Outcome |
|--------|-----------------|
| Tourism revenue | Very High (30%+ of GDP) |
| Citizen happiness | 68 (tourist crowds annoy residents) |
| Environmental quality | Good (well-maintained) |
| Housing cost | Very High (tourist demand inflates prices) |
| Employment type | 60% hospitality/service sector |
| Vulnerability | Economic shock from pandemic/security events |

---

### 3.7 The Techno-Utopia Model

**Philosophy:** Technology-driven city with AI governance, smart infrastructure, and a knowledge economy. High-tech, high-efficiency, potentially unsettling surveillance state.

| Category | Settings |
|----------|----------|
| **Governance** | Technocracy (Authority: 65, Economy: 40) |
| **Traffic** | Autonomous vehicles. Dynamic congestion pricing (AI-adjusted). Smart traffic signals. Hyperloop connections. |
| **Building** | Smart buildings with IoT sensors. Dynamic energy management. Modular construction. |
| **Environmental** | Real-time emission monitoring. Zero-waste circular economy. Vertical farms. Atmospheric carbon capture. |
| **Labor** | UBI (Universal Basic Income) replaces minimum wage. 32-hour work week. AI handles most manual labor. |
| **Social** | Comprehensive surveillance for safety. Social credit system (optional, controversial). Digital civic participation. |
| **Tax** | AI automation tax: 30% on automated labor. Data tax on tech companies. Low personal income tax. |
| **Zoning** | AI-optimized land use. Dynamic zoning that adapts to demand in real-time. |

| Metric | Expected Outcome |
|--------|-----------------|
| GDP per capita | Highest possible |
| Citizen happiness | 55-70 (depends on surveillance vs. privacy balance) |
| Civil liberties score | 30-80 (depends on implementation) |
| Innovation rate | Maximum |
| Employment | Traditional employment: 40% (rest UBI or gig) |
| Vulnerability | Cyberattack events, AI failure events, social alienation |

---

## 4. ENFORCEMENT SYSTEM

Laws without enforcement are suggestions. The enforcement system determines how effectively laws are actually implemented through physical infrastructure, staffing, and budget allocation.

### 4.1 Police Force System

The police force is the primary enforcement mechanism for social laws, traffic laws, and criminal codes.

#### Police Station Tiers

| Tier | Building | Officers | Coverage Radius | Era | Operating Cost/yr | Crime Reduction |
|------|----------|----------|----------------|-----|------------------|----------------|
| 1 | Sheriff's Office | 5 | 8 tiles | Frontier | $500 | -15% in radius |
| 2 | Police Station | 20 | 12 tiles | Industrial | $3,000 | -30% in radius |
| 3 | District Precinct | 50 | 18 tiles | Postwar | $12,000 | -45% in radius |
| 4 | Modern Police HQ | 100 | 24 tiles | Modern | $30,000 | -55% in radius |
| 5 | Smart Policing Center | 80 (+ AI) | 30 tiles | Future | $40,000 | -65% in radius |

#### Police Allocation

The player assigns police resources across enforcement priorities:

| Priority | Budget Share (slider) | Effect |
|----------|---------------------|--------|
| **Street Patrol** | 0-50% | General crime deterrence. Visible police presence. |
| **Traffic Enforcement** | 0-30% | Speed limit compliance, DUI checkpoints, parking enforcement |
| **Detective/Investigation** | 0-30% | Solves crimes, reduces repeat offenders, investigates corruption |
| **Community Policing** | 0-20% | Builds trust, reduces tensions, prevents protests from escalating |
| **Special Operations** | 0-20% | Riot control, organized crime task force, counter-terrorism (Future era) |

**Allocation Tradeoffs:**
- Heavy patrol + low investigation = crimes happen less but go unsolved
- Heavy investigation + low patrol = crimes happen but get solved
- Heavy traffic + low patrol = safe roads but unsafe streets
- Heavy community + low special ops = great public trust but vulnerable to organized crime
- All special ops = police state feel, -15 civil liberties, citizens resent

#### Police Budget Formula

```
police_effectiveness = (
    officer_count / required_officers_for_population
) * training_level
  * equipment_quality
  * corruption_penalty
  * allocation_efficiency

required_officers = population / 400   (1 officer per 400 citizens baseline)
```

| Funding Level | Officers/400 pop | Training | Equipment | Effectiveness |
|--------------|-----------------|----------|-----------|---------------|
| Severely underfunded (<50%) | 0.5 | Poor | Minimal | 25% |
| Underfunded (50-75%) | 0.75 | Basic | Standard | 55% |
| Adequately funded (75-100%) | 1.0 | Good | Full | 85% |
| Well-funded (100-125%) | 1.25 | Advanced | Modern | 100% |
| Heavily funded (125-150%) | 1.5 | Elite | Best available | 110% |
| Overfunded (>150%) | 2.0+ | Elite | Military-grade | 115% (diminishing returns, +police state risk) |

#### Police Misconduct Risk

If police are overfunded relative to civilian oversight:
```
misconduct_risk = (police_budget / civilian_oversight_budget) * corruption_index * 0.01
```

At misconduct_risk > 50, police brutality events become possible:
- Random citizen harassed/injured by police
- Protest trigger (especially among young adults and minorities)
- Media coverage: major scandal
- Options: Reform + oversight (expensive, effective) / Defend police (cheap, escalates)

---

### 4.2 Speed Camera Infrastructure

Speed cameras are buildable objects placed on road tiles.

#### Camera Types

| Type | Cost | Detection | Revenue | Era | Maintenance |
|------|------|-----------|---------|-----|-------------|
| **Fixed Camera** | $500 | Single point, one direction | $8-40/yr | Modern | $15/yr |
| **Average Speed Camera** | $1,200 (pair) | Measures speed over distance | $15-60/yr | Modern | $25/yr |
| **Mobile Camera Van** | $800 (vehicle) | Relocatable, covers 5-tile route | $20-50/yr | Modern | $30/yr |
| **AI Camera** | $2,000 | Detects speed, running lights, phone use, no seatbelt | $30-80/yr | Future | $40/yr |

#### Camera Placement Rules

- Must be placed on a road tile
- Cannot be hidden (must have signage warning 2 tiles before, per most law variants)
- Each camera covers a 3-tile stretch
- Average speed cameras need a pair placed 5-15 tiles apart
- Mobile vans are not permanent -- they relocate weekly (randomized, unpredictable for drivers)
- AI cameras can be placed at intersections and detect multiple violation types

#### Camera Economics

```
camera_revenue = violations_detected * fine_amount
violations_detected = (
    daily_traffic_volume
    * speeding_rate          (based on speed limit and road type)
    * detection_rate          (camera type efficiency)
    * decay_factor           (drivers learn location, violations drop 5%/yr to floor of 3%)
)
```

**Waze Effect (Future era):** When citizens have smartphones (requires T044 Internet), a "navigation app" effect reduces fixed camera effectiveness by 30% (drivers slow down at camera, speed up after). Average speed cameras and mobile vans are not affected.

---

### 4.3 Building Code Inspection System

Building codes require an inspection infrastructure to enforce.

#### Inspection Office

| Tier | Building | Inspectors | Coverage | Era | Cost/yr |
|------|----------|-----------|----------|-----|---------|
| 1 | Code Enforcement Office | 3 | 500 buildings | Industrial | $2,000 |
| 2 | Building Inspection Dept | 10 | 2,000 buildings | Postwar | $8,000 |
| 3 | Digital Compliance Center | 5 (+ sensors) | 5,000 buildings | Modern | $15,000 |

#### Inspection Mechanics

```
inspection_coverage = inspectors * buildings_per_inspector / total_buildings

If coverage < 100%:
  uninspected_buildings = total_buildings * (1 - coverage)
  code_violation_rate = 15% of uninspected buildings
  annual_violations = uninspected_buildings * 0.15

  consequences_per_violation:
    - 5% chance of fire (if fire code violation)
    - 3% chance of structural failure (if building code violation)
    - 2% chance of health hazard (if safety violation)
    - 10% chance of discovered by media (scandal)
```

**Inspection Corruption:** At high corruption levels, inspectors can be bribed:
- 20% of inspections are "passed" without actual inspection
- Buildings that pass corrupt inspections have full violation risk
- Whistleblower event can expose corrupt inspection ring

---

### 4.4 Environmental Monitoring System

Environmental laws require monitoring infrastructure.

#### Monitoring Stations

| Type | Cost | Coverage | Data Provided | Era | Operating Cost |
|------|------|----------|---------------|-----|----------------|
| **Air Quality Monitor** | $300 | 8 tiles | PM2.5, NOx, SO2, O3 levels | Modern | $10/yr |
| **Noise Monitor** | $200 | 5 tiles | Decibel levels by time of day | Modern | $8/yr |
| **Water Quality Station** | $500 | Waterway segment (20 tiles) | pH, heavy metals, bacteria, chemicals | Modern | $15/yr |
| **Soil Contamination Probe** | $400 | 4 tiles | Heavy metals, hydrocarbons, pesticides | Modern | $12/yr |
| **IoT Sensor Network** | $50/tile | Per-tile | All environmental metrics | Future | $3/tile/yr |

#### Monitoring Mechanics

**Without monitoring stations:** Environmental laws have only 40% effectiveness because violations go undetected. The player knows pollution exists (from the overlay) but cannot issue fines or force compliance.

**With monitoring stations:** Each station within range of a polluting source:
- Detects violations with 85% accuracy
- Generates fine revenue ($50-500 per violation)
- Provides data for the environmental overlay (more accurate readings)
- Triggers enforcement actions (warning -> fine -> shutdown order)

**Enforcement Escalation:**
1. **Warning** (first violation): No fine. 60-day compliance window.
2. **Fine** (second violation): $200-$2,000 depending on severity.
3. **Heavy Fine** (third violation): $2,000-$20,000.
4. **Shutdown Order** (fourth violation): Business closed until compliance achieved.
5. **Criminal Prosecution** (if continued): Owner jailed (removes business permanently).

**Industry Response to Monitoring:** When an area gets monitoring stations, industries within range either:
- Install pollution controls (cost increase but compliance, 60% choose this)
- Accept fines as "cost of business" (if fines < control cost, 25% choose this)
- Relocate to unmonitored area or different town (15% choose this)

---

### 4.5 Labor Inspection System

| Building | Inspectors | Coverage | Cost/yr | Era |
|----------|-----------|----------|---------|-----|
| Labor Board Office | 5 | 200 businesses | $5,000 | Industrial |
| Labor Standards Agency | 15 | 1,000 businesses | $18,000 | Postwar |
| Digital Labor Platform | 8 (+ AI) | 3,000 businesses | $25,000 | Modern |

**Inspects:** Minimum wage compliance, work hours, child labor, workplace safety, union rights

**Without labor inspectors:** Labor laws have 30% effectiveness. Sweatshops, child labor, and safety violations persist in uninspected businesses.

---

### 4.6 Tax Collection System

| Era | Collection Method | Efficiency | Cost | Evasion Rate |
|-----|------------------|-----------|------|-------------|
| Frontier | Manual tax collector | 70% | $5/1000 pop/yr | 30% |
| Industrial | Tax office with records | 82% | $8/1000 pop/yr | 18% |
| Postwar | Centralized revenue service | 90% | $12/1000 pop/yr | 10% |
| Modern | Computerized tax system (T095a) | 95% | $10/1000 pop/yr | 5% |
| Future | AI-powered tax compliance | 98% | $8/1000 pop/yr | 2% |

**Tax Evasion Mechanics:**
```
actual_revenue = theoretical_revenue * collection_efficiency * (1 - evasion_rate)

evasion_rate = base_evasion_for_era
  + tax_complexity_penalty     (more brackets/exemptions = more evasion opportunities)
  + corruption_bonus           (high corruption = more evasion)
  - enforcement_reduction      (auditors reduce evasion)
  - penalty_deterrence         (harsh penalties for evasion reduce rate)
```

**Audit System:** The player can invest in tax audits:
- Each auditor costs $3,000/year
- Each auditor reduces evasion rate by 0.5%
- Each auditor recovers $5,000-$15,000 in unpaid taxes per year
- Diminishing returns: after evasion drops below 3%, auditors recover less than they cost

---

## 5. SYSTEM INTEGRATION

### 5.1 How Political/Law System Connects to Existing GDD Systems

| GDD System | Integration Points |
|-----------|-------------------|
| **Transport (Section 4)** | Speed limits affect road capacity. Congestion charges affect traffic volume. Parking laws affect commercial zones. Transit investment politically contentious. |
| **Industry (Section 5)** | Emission laws force industry upgrade or relocation. Labor laws affect operating costs. Zoning determines where industry can locate. Tax incentives attract specific industries. |
| **Demographics (Section 6)** | Each demographic reacts differently to laws. Wealth classes have different tax burdens. Age brackets have different policy preferences. Cultural profiles affect social law acceptance. |
| **Economy (Section 7)** | Tax rates directly feed budget. Enforcement costs are new expense categories. Fines are new revenue sources. Regulatory burden affects business investment. |
| **Research (Tech Tree)** | Many laws require specific technologies. T086 Basic Zoning Laws unlocks zoning system. T091 Urban Planning unlocks mixed-use. T097 Digital Governance unlocks congestion charges, speed cameras. |
| **Events (Section 11)** | Political events (protests, elections, scandals) integrate with existing event system. Economic events trigger political pressure for policy changes. |
| **Services (Section 9)** | Police stations gain enforcement allocation system. New buildings: inspection offices, monitoring stations, labor board. |

### 5.2 UI Design for Political System

**City Hall Panel (Main Access Point):**
```
+--------------------------------------------------+
|  CITY HALL                                        |
|                                                   |
|  [Ordinances]  [Council]  [Elections]  [Media]    |
|  [Budget]      [Lobbying] [Corruption] [Reports]  |
|                                                   |
|  Current Governance: Liberal Democracy             |
|  Authority: 22/100  |  Economy: 35/100            |
|  Political Capital: 45                             |
|  Approval Rating: 67%                              |
|  Next Election: 2 years, 3 months                  |
+--------------------------------------------------+
```

**Ordinances Sub-Panel:**
```
+--------------------------------------------------+
|  ORDINANCES                                       |
|                                                   |
|  [Traffic] [Building] [Environmental]             |
|  [Labor]   [Social]   [Tax]   [Zoning]           |
|                                                   |
|  Active Laws: 23/78 available                      |
|  Enforcement Budget: $45,000/yr                    |
|  Total Fine Revenue: $12,000/yr                    |
|                                                   |
|  >> Click category to view/enact laws <<           |
+--------------------------------------------------+
```

**Law Detail View:**
```
+--------------------------------------------------+
|  SPEED LIMITS (TL-01)                             |
|                                                   |
|  Status: [ENACTED - FUNDED]                       |
|                                                   |
|  Residential:  [30 km/h v]  School Zone: [20 v]  |
|  Avenues:      [50 km/h v]  Highways:    [100 v] |
|                                                   |
|  Enforcement Cost:    $2,400/yr                   |
|  Fine Revenue:        $1,800/yr                   |
|  Compliance Rate:     78%                          |
|                                                   |
|  PUBLIC OPINION:                                   |
|  [====----] Families:      +12 support             |
|  [==------] Commuters:     -8  oppose              |
|  [====----] Elderly:       +10 support             |
|  [===-----] Business:      -5  oppose              |
|                                                   |
|  EFFECTS:                                          |
|  Accident rate:  -35%  |  Noise: -25%             |
|  Road capacity:  -15%  |  Commute time: +8%       |
|                                                   |
|  [Modify] [Suspend] [Repeal]                      |
+--------------------------------------------------+
```

### 5.3 Political Overlay (New Map Overlay)

A new overlay for the map system that shows:
- **Approval heatmap**: Per-district approval rating (red = low, green = high)
- **Faction strength**: Colored dots showing which faction dominates each district
- **Protest locations**: Flashing icons where active protests are occurring
- **Enforcement coverage**: Combined coverage of police, inspectors, monitors
- **Corruption hotspots**: Red zones where corruption events are more likely
- **Election results**: After elections, show vote results by district

### 5.4 Advisor Integration

The existing advisor system (GDD Section 6.8) gains a **Political Advisor** who:
- Warns about upcoming elections and approval trends
- Suggests laws that would boost approval
- Alerts about faction strength shifts
- Reports corruption risk
- Recommends enforcement spending
- Warns when lobby pressure is building

Advisor dialogue examples:
- "The Progressive Alliance is gaining strength in the university district. They are pushing for a rent control ordinance."
- "The Industrial Alliance lobby has requested a meeting. They want emission standards relaxed to Tier 1."
- "Your corruption index is rising. Consider appointing an independent auditor."
- "Elections are in 8 months. Your approval is at 52%. Risky. Suggest fulfilling the transit petition from District 3."

### 5.5 Era-Gated Law Availability

Not all laws are available from the start. Availability follows the era system:

| Era | Laws Available |
|-----|---------------|
| **Frontier** | Basic zoning, property tax, child labor (unregulated default), public drinking, fire codes (basic), building height (up to 4), noise (basic) |
| **Industrial** | Speed limits, parking, minimum wage, work hours, unions, workplace safety, smoking, income tax (simple), corporate tax, industrial buffer, noise ordinance, recycling (basic) |
| **Postwar** | DUI laws, congestion (manual tolls), earthquake codes, historic preservation, comprehensive labor laws, homelessness policy, sales tax, mixed-use zoning, density limits, curfew |
| **Modern** | Speed cameras, congestion charge (digital), LEZ, emission limits, green building, plastic ban, full recycling, digital tax brackets, TIF districts, pet regulations, helmet laws |
| **Future** | AI cameras, dynamic congestion pricing, zero-emission mandates, UBI, automation tax, smart zoning, blockchain governance, social credit (optional) |

---

*End of Political & Law System Design Document*
*Document version: 1.0*
*Created: March 2026*
*Companion to: CITY_BUILDER_GDD.md v2.0*
*Cross-references: RESEARCH_TECH_TREE.md (102 technologies)*
