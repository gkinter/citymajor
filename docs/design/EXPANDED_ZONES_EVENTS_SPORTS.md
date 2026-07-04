# IRON & OAK: Expanded Zone Types, Special Areas, Events & Sports Leagues

## Complete Design Specification v1.0

---

# PART 1: NEW ZONE TYPES

These zone types extend the existing R/C/I/A/O/Mixed-Use system. Like existing zones, they are painted onto road-adjacent tiles and buildings grow organically based on demand, land value, and services. Each zone type has era requirements, density levels, and cascading effects on the simulation.

---

## 1.1 FARMING ZONES (Deep Agriculture Overhaul)

The current Agricultural (A) zone is a single flat type. This overhaul replaces it with a tiered farming system that evolves across eras, introduces crop mechanics, and creates meaningful strategic decisions about food production.

### Zone Sub-Types

| Sub-Type | Era | Tile Size | Workers | Output/Season | Base Cost | Maint/yr |
|----------|-----|-----------|---------|---------------|-----------|----------|
| **Subsistence Farm** | Frontier | 3x3 | 2-4 (family) | 50 food units | $50 | $5 |
| **Crop Farm** | Frontier | 4x4 | 6-10 | 120 food units | $200 | $20 |
| **Livestock Ranch** | Frontier | 6x6 | 4-8 | 80 meat + 40 dairy | $400 | $45 |
| **Plantation** | Industrial | 8x8 | 20-40 | 400 cash crop units | $1,200 | $100 |
| **Industrial Farm** | Postwar | 6x6 | 8-12 | 350 food units | $2,000 | $150 |
| **Organic Farm** | Modern | 4x4 | 6-10 | 100 food (premium) | $1,500 | $120 |
| **Greenhouse Complex** | Modern | 3x3 | 4-6 | 80 food (year-round) | $3,000 | $250 |
| **Vertical Farm** | Future | 2x2 | 6-8 | 200 food (year-round) | $8,000 | $500 |
| **Aquaponics Facility** | Future | 3x3 | 4-6 | 120 food + 60 fish | $6,000 | $400 |

### Crop Rotation System

Farms that grow the same crop for 3+ consecutive seasons suffer a **Soil Depletion** penalty:

| Consecutive Seasons | Yield Modifier | Soil Health |
|---------------------|---------------|-------------|
| 1 (fresh) | 100% | Healthy |
| 2 | 95% | Good |
| 3 | 80% | Fair |
| 4 | 60% | Poor |
| 5+ | 40% | Exhausted |

**Recovery**: Planting a different crop category restores 15% soil health per season. Leaving fallow restores 25% per season. Organic farms restore 10% faster.

**Crop Categories** (rotating between categories prevents depletion):
- **Grains**: Wheat, corn, rice, barley, oats
- **Legumes**: Soybeans, beans, peas, lentils (nitrogen-fixing: +5% soil health)
- **Root Vegetables**: Potatoes, carrots, beets, turnips
- **Cash Crops**: Cotton, tobacco, sugarcane, hops
- **Fruits**: Apples, grapes, berries, citrus (require 2 seasons to establish orchard)
- **Livestock Feed**: Alfalfa, hay, silage corn

### Livestock Mechanics

| Animal | Space Required | Feed/Season | Output | Revenue/Season | Era |
|--------|---------------|-------------|--------|----------------|-----|
| Chickens | 1 tile | 5 feed | 30 eggs + 10 meat | $80 | Frontier |
| Pigs | 2 tiles | 15 feed | 40 meat | $120 | Frontier |
| Cattle (dairy) | 3 tiles | 20 feed | 60 dairy | $150 | Frontier |
| Cattle (beef) | 4 tiles | 25 feed | 80 meat | $200 | Frontier |
| Sheep | 2 tiles | 10 feed | 20 wool + 15 meat | $90 | Frontier |
| Horses | 3 tiles | 15 feed | Transport (pre-car) | $100 | Frontier |
| Fish (aquaculture) | 2 tiles (water) | 8 feed | 50 fish | $130 | Modern |

**Livestock chains**:
- Cattle -> Slaughterhouse -> Butcher/Grocery
- Dairy cattle -> Dairy plant -> Cheese/Milk -> Grocery
- Sheep -> Shearing -> Textile mill -> Clothing store
- Chickens -> Egg processing -> Grocery

### Farming Zone Bonuses & Penalties

| Condition | Effect |
|-----------|--------|
| Adjacent to river/lake | +20% crop yield (irrigation) |
| Fertile soil deposit | +30% yield |
| Adjacent to industrial zone | -15% yield (pollution) |
| Organic certification (no adjacent industrial, 3+ years organic) | +50% sale price |
| Farmer's market within 10 tiles | +25% sale price, +5 happiness |
| Agricultural research at university | +10% yield per tech level |
| Drought event | -40% yield (irrigated farms only -15%) |
| Frost event | -60% yield (greenhouses immune) |

### Farmer's Markets (Special Ploppable tied to Farming)

| Tier | Era | Size | Capacity | Revenue/Month | Happiness Bonus | Tourism |
|------|-----|------|----------|---------------|-----------------|---------|
| Village Market | Frontier | 2x2 | 8 stalls | $200 | +3 (10 tiles) | +5 |
| Town Market Hall | Industrial | 3x3 | 20 stalls | $600 | +5 (15 tiles) | +15 |
| Artisan Market | Modern | 3x3 | 15 stalls | $800 | +8 (15 tiles) | +25 |
| Organic Co-op | Modern | 2x2 | 10 stalls | $500 | +10 (10 tiles) | +20 |

**Requirement**: Must have at least 3 active farms within 20 tiles to operate. Organic co-op requires at least 2 certified organic farms.

---

## 1.2 WASTE MANAGEMENT ZONES

Waste is currently a simple utility (garbage trucks -> landfill). This expansion turns waste into a full zone type with strategic depth, environmental consequences, and era progression.

### Waste Generation Model

Every zone generates waste proportional to its density and type:

| Zone Type | Waste/Tile/Month | Waste Type |
|-----------|-----------------|------------|
| Residential (Low) | 5 units | General |
| Residential (Med) | 12 units | General |
| Residential (High) | 25 units | General |
| Commercial (Low) | 8 units | General + Packaging |
| Commercial (High) | 20 units | General + Packaging |
| Industrial (Light) | 15 units | Industrial + Hazardous (10%) |
| Industrial (Heavy) | 40 units | Industrial + Hazardous (25%) |
| Office | 6 units | Paper/Electronic |
| Hospital/Research | 10 units | Medical (special handling) |

### Waste Management Facilities

| Facility | Era | Size | Capacity | Cost | Maint/yr | Pollution | Notes |
|----------|-----|------|----------|------|----------|-----------|-------|
| **Open Dump** | Frontier | 4x4 | 200 units/mo | $100 | $10 | Very High | Disease risk, -30 land value radius |
| **Sanitary Landfill** | Industrial | 6x6 | 500 units/mo | $2,000 | $150 | High | Lined, reduces disease, -20 land value |
| **Incinerator** | Industrial | 3x3 | 300 units/mo | $5,000 | $400 | High (air) | Generates small amount of power |
| **Recycling Center** | Postwar | 3x3 | 200 units/mo | $4,000 | $300 | Low | Recovers 30% as raw materials |
| **Composting Facility** | Postwar | 3x3 | 100 units/mo | $2,000 | $150 | Low | Organic waste only, produces fertilizer |
| **Waste-to-Energy Plant** | Modern | 4x4 | 600 units/mo | $15,000 | $800 | Medium | Generates 20MW power |
| **Advanced Recycling** | Modern | 3x3 | 400 units/mo | $10,000 | $600 | Very Low | Recovers 60% as raw materials |
| **Hazardous Waste Disposal** | Modern | 4x4 | 100 haz units/mo | $20,000 | $1,200 | Low (contained) | Required for heavy industry |
| **E-Waste Recycling** | Modern | 2x2 | 80 units/mo | $8,000 | $500 | Low | Recovers rare earth minerals |
| **Plasma Gasification** | Future | 3x3 | 800 units/mo | $30,000 | $1,500 | Minimal | 95% waste elimination, power generation |
| **Molecular Recycler** | Future | 2x2 | 500 units/mo | $50,000 | $2,000 | None | 99% material recovery |

### Waste Consequences

| Condition | Effect |
|-----------|--------|
| Waste uncollected > 3 months | Rats spawn, disease risk +30%, land value -25% |
| Landfill at capacity | New waste dumped illegally, pollution spikes |
| No hazardous waste facility + heavy industry | Groundwater contamination event risk |
| Recycling rate > 50% | "Green City" bonus: +5 happiness, +10 tourism |
| Recycling rate > 80% | "Zero Waste" achievement: +10 happiness, immigration boost |
| Composting active | Nearby farms get +10% yield from fertilizer |

---

## 1.3 ENERGY ZONES

Expands the existing power utility into a full zone type with district-level energy planning.

### Power Generation Facilities

| Facility | Era | Size | Output (MW) | Fuel | Cost | Maint/yr | Pollution | CO2 |
|----------|-----|------|-------------|------|------|----------|-----------|-----|
| **Waterwheel** | Frontier | 1x1 | 0.5 | Water flow | $50 | $5 | None | None |
| **Coal Plant (Small)** | Industrial | 3x3 | 20 | 10 coal/mo | $5,000 | $500 | Very High | Very High |
| **Coal Plant (Large)** | Industrial | 5x5 | 80 | 40 coal/mo | $15,000 | $1,200 | Very High | Very High |
| **Oil Power Plant** | Industrial | 4x4 | 50 | 20 oil/mo | $10,000 | $800 | High | High |
| **Hydroelectric Dam** | Industrial | Special | 40-120 | River flow | $25,000 | $400 | None | None |
| **Natural Gas Plant** | Postwar | 3x3 | 60 | 15 gas/mo | $12,000 | $600 | Medium | Medium |
| **Nuclear Plant** | Modern | 6x6 | 200 | 2 uranium/mo | $80,000 | $3,000 | None (risk) | None |
| **Solar Farm** | Modern | 4x4 | 15 | Sunlight | $8,000 | $200 | None | None |
| **Wind Farm** | Modern | 3x3 | 10 | Wind | $6,000 | $150 | None (noise) | None |
| **Geothermal Plant** | Modern | 3x3 | 30 | Heat deposit | $20,000 | $500 | None | Minimal |
| **Battery Storage** | Modern | 2x2 | 0 (stores 50MWh) | -- | $15,000 | $400 | None | None |
| **Hydrogen Plant** | Future | 3x3 | 40 | Water + electricity | $25,000 | $800 | None | None |
| **Fusion Reactor** | Future | 4x4 | 500 | Deuterium | $200,000 | $5,000 | None | None |
| **Orbital Solar Receiver** | Future | 2x2 | 100 | Space solar | $150,000 | $2,000 | None | None |

### Energy Zone Mechanics

**Grid Reliability**: Power supply must exceed demand by at least 10% reserve margin. Below that:

| Reserve Margin | Effect |
|----------------|--------|
| 10%+ | Stable grid, no issues |
| 5-10% | Rolling brownouts during peak (summer AC, winter heating) |
| 0-5% | Frequent brownouts, -10 happiness, industry output -15% |
| Negative | Blackouts, -25 happiness, industry halted, crime +20% |

**Renewable Intermittency**: Solar produces 0% at night, 50% on cloudy days. Wind varies 20-100% by weather. Battery storage smooths this.

**Nuclear Risk**: 0.1% annual chance of incident if maintenance is underfunded. Incident contaminates 20-tile radius for 10+ years. NIMBY resistance is extreme (referendum required to build).

### Green Energy Districts (Special Zone Overlay)

A Green Energy District is a zone overlay (like a tax district) applied to a 10x10 or larger area. All energy facilities within the district receive bonuses:

| Bonus | Effect |
|-------|--------|
| Efficiency boost | +15% output from all renewable sources in district |
| Research acceleration | Energy research +20% faster while active |
| Land value | +10 in district (green premium) |
| Immigration | +5% immigration rate for educated workers |
| Tourism | "Green District" tourism attraction |
| Cost | -10% maintenance for renewable facilities |

**Requirements to designate**: 80%+ of district power from renewables. At least 3 different renewable sources. No fossil fuel plants within district.

---

## 1.4 TECH / INNOVATION DISTRICTS

A new zone type unlocked in the Modern era that creates high-value employment, generates research points, and attracts educated workers.

### Tech Zone Sub-Types

| Sub-Type | Era | Size | Workers | Skill Req | Output | Cost | Maint/yr |
|----------|-----|------|---------|-----------|--------|------|----------|
| **Startup Incubator** | Modern | 2x2 | 10-20 | University | +2 RP/mo, small revenue | $5,000 | $400 |
| **Co-Working Space** | Modern | 1x1 | 5-15 | High school+ | Small revenue | $2,000 | $150 |
| **Tech Park** | Modern | 4x4 | 50-200 | University | +5 RP/mo, high revenue | $20,000 | $1,500 |
| **Data Center** | Modern | 3x3 | 10-20 | Trade school+ | Cloud services revenue | $30,000 | $2,500 |
| **R&D Campus** | Modern | 6x6 | 100-300 | University+ | +15 RP/mo, patents | $50,000 | $4,000 |
| **Biotech Lab** | Modern | 3x3 | 30-60 | Graduate | +8 RP/mo, pharma output | $25,000 | $2,000 |
| **Innovation Hub** | Future | 4x4 | 80-150 | University | +10 RP/mo, startup spawns | $35,000 | $3,000 |

### Tech District Mechanics

**Agglomeration Bonus**: Tech buildings within 5 tiles of each other get stacking bonuses:

| Adjacent Tech Buildings | RP Bonus | Revenue Bonus | Startup Spawn Rate |
|------------------------|----------|---------------|-------------------|
| 2 | +5% | +5% | 1/year |
| 3-4 | +15% | +10% | 2/year |
| 5-7 | +25% | +20% | 3/year |
| 8+ | +40% | +30% | 5/year |

**Startup System**: Incubators and Innovation Hubs spawn startup companies. Each startup:
- Has a 60% chance of failing within 2 years (workers return to job pool)
- 30% chance of becoming a stable small company (+10 jobs, moderate revenue)
- 9% chance of becoming a medium company (+50 jobs, good revenue)
- 1% chance of becoming a "unicorn" (+200 jobs, massive revenue, city prestige)

**Requirements**: Fiber internet connection, university within city, low crime, high land value.

**Data Center Special**: Requires massive power (5MW per facility) and water for cooling. Generates heat (can be used for district heating in adjacent residential). Revenue scales with internet connectivity quality.

---

## 1.5 AI SECTOR ZONES (Future Era)

Unlocked only in the Future era after researching "Artificial Intelligence" and "Advanced Robotics" technologies.

### AI Zone Facilities

| Facility | Size | Workers | Skill | Output | Cost | Maint/yr | Special |
|----------|------|---------|-------|--------|------|----------|---------|
| **AI Research Campus** | 5x5 | 50-100 | Graduate | +20 RP/mo (AI-specific) | $80,000 | $6,000 | Unlocks AI techs faster |
| **Robotics Factory** | 4x4 | 30-50 | University | Industrial robots | $60,000 | $4,000 | Supplies automation to other industry |
| **Autonomous Vehicle Testing** | 6x6 | 20-40 | University | AV tech progress | $40,000 | $3,000 | Requires closed road network in zone |
| **Neural Computing Center** | 3x3 | 15-30 | Graduate | Computing services | $100,000 | $8,000 | Requires 10MW power |
| **AI Ethics Institute** | 2x2 | 20-40 | Graduate | +5 RP/mo, reduces AI backlash | $15,000 | $1,200 | Mitigates social unrest from automation |
| **Quantum Computing Lab** | 3x3 | 10-20 | Graduate | +30 RP/mo | $200,000 | $15,000 | Extreme power and cooling needs |

### AI Sector Consequences

The AI sector creates a tension between economic efficiency and social stability:

| AI Adoption Level | Economic Effect | Social Effect |
|-------------------|----------------|---------------|
| 0-20% (Early) | +5% industrial output | No noticeable effect |
| 20-40% (Growing) | +15% industrial output, -10% factory jobs | Mild unrest, retraining demand |
| 40-60% (Mainstream) | +30% output, -25% factory jobs, -10% office jobs | Protests, unemployment spike |
| 60-80% (Dominant) | +50% output, -40% factory, -20% office jobs | Major social crisis unless mitigated |
| 80-100% (Singularity) | +80% output, -60% factory, -35% office jobs | Requires UBI policy or city collapses |

**Mitigation Policies** (unlocked with AI Ethics Institute):
- **Retraining Programs**: $500/mo per worker, converts displaced workers to tech-adjacent roles over 12 months
- **Universal Basic Income (UBI)**: $200/mo per unemployed citizen, prevents emigration and unrest
- **Robot Tax**: 15% tax on automated output, funds social programs, slightly reduces AI adoption rate
- **Human-AI Collaboration Mandate**: Limits full automation, keeps 50% human workforce, reduces efficiency gain by half

---

## 1.6 MILITARY / GOVERNMENT ZONES

Government buildings and military installations that provide security, prestige, and regional influence.

### Government Facilities

| Facility | Era | Size | Workers | Effect | Cost | Maint/yr |
|----------|-----|------|---------|--------|------|----------|
| **Town Hall** | Frontier | 2x2 | 5 | +5 approval, administration hub | $500 | $50 |
| **Courthouse** | Industrial | 2x2 | 15 | -10% crime, legal services | $3,000 | $300 |
| **City Hall** | Postwar | 3x3 | 30 | +10 approval, policy options | $10,000 | $800 |
| **Federal Building** | Modern | 3x3 | 50 | +500 high-skill jobs, prestige | $20,000 | $1,500 |
| **Embassy** | Modern | 2x2 | 20 | +tourism, trade bonuses (regional) | $15,000 | $1,000 |
| **Capitol Complex** | Modern | 5x5 | 200 | Major prestige, tourism, jobs | $50,000 | $4,000 |
| **Space Agency HQ** | Future | 4x4 | 100 | +25 RP/mo, prestige | $100,000 | $8,000 |

### Military Facilities

| Facility | Era | Size | Workers | Effect | Cost | Maint/yr |
|----------|-----|------|---------|--------|------|----------|
| **Militia Barracks** | Frontier | 2x2 | 10 | -5% crime, emergency response | $300 | $30 |
| **Army Fort** | Industrial | 4x4 | 50 | -15% crime, 200 jobs, noise | $8,000 | $600 |
| **Naval Base** | Industrial | 6x4 (coastal) | 100 | 500 jobs, port access, noise | $20,000 | $1,500 |
| **Air Force Base** | Postwar | 8x6 | 200 | 800 jobs, extreme noise zone | $40,000 | $3,000 |
| **Military R&D Lab** | Modern | 3x3 | 50 | +10 RP/mo (military tech) | $30,000 | $2,500 |
| **Cyber Command Center** | Future | 2x2 | 30 | +5 RP/mo, cyber defense | $25,000 | $2,000 |

**Military Zone Effects**:
- Large noise radius (-15 land value in radius)
- Generates significant employment (low-to-mid skill)
- Federal funding: Military facilities receive 50% maintenance subsidy from "federal government"
- Security bonus: -20% crime city-wide per military facility
- NIMBY factor: Residents protest military expansion (referendum may be required)

---

## 1.7 ENTERTAINMENT DISTRICTS

Special zone overlay that permits nightlife, casinos, and adult entertainment with both economic benefits and social consequences.

### Entertainment Facilities

| Facility | Era | Size | Capacity | Revenue/Mo | Happiness | Tourism | Cost | Maint/yr |
|----------|-----|------|----------|------------|-----------|---------|------|----------|
| **Saloon** | Frontier | 1x1 | 30 | $50 | +2 (5 tiles) | +2 | $200 | $20 |
| **Theater (stage)** | Industrial | 2x2 | 200 | $300 | +5 (10 tiles) | +10 | $3,000 | $250 |
| **Dance Hall** | Industrial | 2x2 | 150 | $250 | +3 (8 tiles) | +5 | $2,000 | $200 |
| **Cinema** | Postwar | 2x2 | 300 | $400 | +5 (10 tiles) | +8 | $5,000 | $350 |
| **Nightclub** | Postwar | 1x1 | 200 | $600 | +3 (5 tiles) | +10 | $4,000 | $300 |
| **Casino (Small)** | Modern | 3x3 | 500 | $2,000 | +2 / -3* | +30 | $20,000 | $1,500 |
| **Casino (Resort)** | Modern | 6x6 | 2,000 | $8,000 | +2 / -5* | +80 | $80,000 | $5,000 |
| **Comedy Club** | Modern | 1x1 | 100 | $300 | +4 (8 tiles) | +5 | $3,000 | $200 |
| **VR Entertainment** | Future | 2x2 | 400 | $1,200 | +6 (10 tiles) | +15 | $10,000 | $800 |
| **Holodeck Arena** | Future | 3x3 | 800 | $3,000 | +8 (15 tiles) | +30 | $25,000 | $1,500 |

*Casino happiness: +2 for entertainment-seeking citizens, -3 to -5 for nearby residential (noise, crime, traffic).

### Entertainment District Social Consequences

| Condition | Effect |
|-----------|--------|
| Nightclub density > 3 per block | Crime +15%, noise complaints, property value -10% nearby |
| Casino present | Gambling addiction event risk (2%/year per casino) |
| Gambling addiction event | -5 city happiness, +10% poverty in 10-tile radius, healthcare demand +15% |
| Entertainment district + police presence | Crime impact halved |
| Entertainment district + transit access | Traffic impact -30%, revenue +15% |
| Casino revenue > 10% of city budget | "Casino dependency" warning, economic vulnerability |

---

## 1.8 UNIVERSITY DISTRICTS

Expands the existing university from a single ploppable into a full campus zone system.

### University District Components

| Building | Era | Size | Capacity | Workers | RP/Mo | Cost | Maint/yr |
|----------|-----|------|----------|---------|-------|------|----------|
| **Lecture Hall** | Postwar | 2x2 | 500 students | 20 professors | +3 | $8,000 | $600 |
| **Science Lab** | Postwar | 2x2 | 100 students | 15 researchers | +5 | $12,000 | $1,000 |
| **Library** | Postwar | 2x2 | 300 students | 10 staff | +2 | $5,000 | $400 |
| **Student Housing** | Postwar | 2x2 | 200 students | 2 staff | 0 | $4,000 | $300 |
| **Student Union** | Postwar | 2x2 | -- | 5 staff | 0 | $3,000 | $250 |
| **Sports Center** | Postwar | 3x3 | -- | 10 staff | 0 | $6,000 | $500 |
| **Medical School** | Modern | 3x3 | 200 students | 30 faculty | +8 | $20,000 | $2,000 |
| **Engineering School** | Modern | 3x3 | 300 students | 25 faculty | +8 | $18,000 | $1,800 |
| **Business School** | Modern | 2x2 | 400 students | 20 faculty | +4 | $15,000 | $1,200 |
| **Research Institute** | Modern | 3x3 | 50 grad students | 40 researchers | +15 | $30,000 | $3,000 |
| **Supercomputer Center** | Future | 2x2 | -- | 10 staff | +20 | $50,000 | $5,000 |

### University District Mechanics

**Campus Coherence Bonus**: University buildings within 8 tiles of each other form a "campus." Larger campuses get stacking bonuses:

| Campus Buildings | RP Bonus | Graduate Quality | Prestige |
|-----------------|----------|-----------------|----------|
| 3-4 | +10% | Standard | Local |
| 5-7 | +20% | Good (+10% hiring preference) | Regional |
| 8-10 | +35% | Excellent (+20% hiring preference) | National |
| 11+ | +50% | Elite (+30% hiring preference, attracts intl students) | World-class |

**Student Population Effects**:
- Students count as residents but pay no property tax
- Student neighborhoods have higher crime (+5%), higher cultural activity (+15%), and unique demand for cheap food/entertainment
- Bars, coffee shops, and bookstores auto-spawn in commercial zones near campus
- Student protests can occur when tuition rises or political events happen

**Brain Drain Prevention**: A world-class university retains 60% of graduates in the city (vs. 30% without university). Tech districts near campus retain an additional 15%.

---

# PART 2: SPECIAL PLOPPABLE AREAS

These are individually placed buildings/areas (not zoned). Each has unique gameplay effects, costs, and requirements.

---

## 2.1 WATER FEATURES

| Feature | Era | Size | Cost | Maint/yr | Land Value | Happiness | Notes |
|---------|-----|------|------|----------|------------|-----------|-------|
| **Pond** | Frontier | 2x2 | $200 | $10 | +5 (5 tiles) | +2 | Natural look, attracts birds |
| **Lake (natural)** | -- | Varies | Free | $0 | +10 (10 tiles) | +5 | Map-generated, not buildable |
| **Decorative Fountain** | Industrial | 1x1 | $500 | $30 | +8 (3 tiles) | +3 | Requires water pressure |
| **Grand Fountain** | Postwar | 2x2 | $3,000 | $100 | +15 (8 tiles) | +5 | Landmark potential |
| **Swimming Pool (Public)** | Postwar | 2x2 | $5,000 | $300 | +8 (5 tiles) | +6 | Seasonal (summer), requires lifeguards |
| **Water Park** | Modern | 4x4 | $20,000 | $1,500 | +10 (8 tiles) | +8 | Seasonal, high tourism, 200 jobs |
| **Reflecting Pool** | Modern | 3x1 | $2,000 | $50 | +12 (5 tiles) | +4 | Pairs well with monuments |
| **Japanese Garden** | Modern | 2x2 | $4,000 | $200 | +15 (5 tiles) | +7 | Cultural attraction |

---

## 2.2 SPORTS FACILITIES

| Facility | Era | Size | Capacity | Cost | Maint/yr | Happiness | Tourism | Jobs |
|----------|-----|------|----------|------|----------|-----------|---------|------|
| **Baseball Diamond** | Frontier | 3x3 | 500 | $1,000 | $80 | +3 (8 tiles) | +3 | 5 |
| **Basketball Court** | Postwar | 1x1 | 50 | $300 | $20 | +2 (3 tiles) | 0 | 0 |
| **Tennis Courts** | Industrial | 2x1 | 100 | $800 | $50 | +2 (5 tiles) | +2 | 2 |
| **Running Track** | Industrial | 2x2 | 200 | $1,500 | $100 | +3 (5 tiles) | +2 | 3 |
| **Soccer/Football Field** | Industrial | 3x2 | 1,000 | $2,000 | $150 | +4 (8 tiles) | +5 | 5 |
| **Swimming Complex** | Postwar | 3x3 | 500 | $8,000 | $500 | +5 (8 tiles) | +8 | 15 |
| **Local Stadium** | Postwar | 4x4 | 5,000 | $15,000 | $1,000 | +6 (12 tiles) | +15 | 30 |
| **Major Stadium** | Modern | 6x6 | 30,000 | $80,000 | $4,000 | +8 (20 tiles) | +40 | 100 |
| **Olympic Stadium** | Modern | 8x8 | 60,000 | $200,000 | $8,000 | +10 (25 tiles) | +80 | 200 |
| **Golf Course** | Postwar | 6x6 | 200 | $25,000 | $1,500 | +4 (10 tiles) | +10 | 20 |
| **Skatepark** | Modern | 1x1 | 50 | $500 | $30 | +3 (3 tiles) | +1 | 0 |
| **Ice Rink** | Industrial | 2x2 | 300 | $4,000 | $300 | +4 (5 tiles) | +5 | 8 |
| **Velodrome** | Modern | 3x3 | 3,000 | $12,000 | $800 | +4 (8 tiles) | +10 | 15 |
| **Motorsport Track** | Postwar | 8x4 | 20,000 | $40,000 | $2,000 | +5 (15 tiles) | +25 | 50 |
| **Esports Arena** | Future | 3x3 | 5,000 | $15,000 | $1,200 | +6 (10 tiles) | +20 | 25 |

---

## 2.3 EVENT VENUES

| Venue | Era | Size | Capacity | Cost | Maint/yr | Events/Year | Tourism | Jobs |
|-------|-----|------|----------|------|----------|-------------|---------|------|
| **Town Square** | Frontier | 2x2 | 200 | $300 | $20 | 4-8 | +5 | 2 |
| **Bandstand/Gazebo** | Frontier | 1x1 | 100 | $200 | $10 | 8-12 | +3 | 0 |
| **Concert Hall** | Industrial | 3x3 | 1,500 | $15,000 | $1,000 | 20-40 | +20 | 30 |
| **Amphitheater** | Industrial | 3x3 | 3,000 | $10,000 | $600 | 10-20 (seasonal) | +15 | 15 |
| **Convention Center** | Postwar | 5x5 | 5,000 | $30,000 | $2,000 | 12-24 | +35 | 60 |
| **Exhibition Hall** | Postwar | 4x4 | 3,000 | $20,000 | $1,500 | 8-16 | +25 | 40 |
| **Fairgrounds** | Industrial | 6x6 | 10,000 | $12,000 | $800 | 4-6 (seasonal) | +30 | 20 |
| **Opera House** | Industrial | 3x3 | 1,200 | $25,000 | $2,000 | 30-50 | +25 | 40 |
| **Outdoor Festival Ground** | Modern | 5x5 | 20,000 | $8,000 | $500 | 3-6 (seasonal) | +40 | 15 |
| **Mega Arena** | Modern | 5x5 | 20,000 | $60,000 | $3,500 | 40-80 | +50 | 80 |

---

## 2.4 PARKS & RECREATION

| Park Type | Era | Size | Cost | Maint/yr | Land Value | Happiness | Pollution Absorb |
|-----------|-----|------|------|----------|------------|-----------|-----------------|
| **Small City Park** | Frontier | 2x2 | $500 | $30 | +8 (5 tiles) | +3 | 5% (3 tiles) |
| **Medium City Park** | Industrial | 3x3 | $2,000 | $100 | +12 (8 tiles) | +5 | 10% (5 tiles) |
| **Large City Park** | Postwar | 5x5 | $8,000 | $400 | +18 (12 tiles) | +8 | 20% (8 tiles) |
| **Playground** | Industrial | 1x1 | $300 | $20 | +5 (3 tiles) | +3 (families) | 0 |
| **Dog Park** | Modern | 1x1 | $200 | $15 | +3 (3 tiles) | +2 | 0 |
| **Community Garden** | Modern | 2x2 | $1,000 | $50 | +6 (5 tiles) | +5 | 5% (3 tiles) |
| **Botanical Garden** | Industrial | 4x4 | $15,000 | $1,000 | +20 (10 tiles) | +7 | 15% (8 tiles) |
| **Zoo** | Postwar | 5x5 | $25,000 | $2,000 | +15 (12 tiles) | +8 | 5% (5 tiles) |
| **Aquarium** | Modern | 3x3 | $20,000 | $1,500 | +15 (8 tiles) | +7 | 0 |
| **Nature Reserve** | Modern | 8x8+ | $5,000 | $200 | +10 (15 tiles) | +5 | 40% (15 tiles) |
| **National Park** | Modern | Map edge | $10,000 | $500 | +12 (20 tiles) | +6 | 50% (20 tiles) |
| **Rooftop Garden** | Modern | 1x1 (overlay) | $2,000 | $100 | +4 (2 tiles) | +2 | 3% (2 tiles) |
| **Linear Park / Greenway** | Modern | 1xN | $500/tile | $20/tile | +8 (4 tiles) | +4 | 8% (4 tiles) |
| **Sculpture Garden** | Modern | 2x2 | $6,000 | $300 | +12 (6 tiles) | +5 | 0 |

---

## 2.5 CULTURAL LANDMARKS

| Landmark | Era | Size | Cost | Maint/yr | Tourism | Happiness | Prestige | Jobs |
|----------|-----|------|------|----------|---------|-----------|----------|------|
| **General Store Museum** | Frontier | 1x1 | $300 | $20 | +3 | +2 | +1 | 2 |
| **Art Gallery** | Industrial | 2x2 | $8,000 | $500 | +15 | +5 | +5 | 10 |
| **History Museum** | Industrial | 3x3 | $12,000 | $800 | +20 | +5 | +8 | 20 |
| **Natural History Museum** | Postwar | 4x4 | $25,000 | $1,500 | +30 | +6 | +12 | 35 |
| **Science Museum** | Modern | 3x3 | $20,000 | $1,200 | +25 | +6 | +10 | 25 |
| **Modern Art Museum** | Modern | 3x3 | $30,000 | $2,000 | +35 | +5 | +15 | 30 |
| **Opera House** | Industrial | 3x3 | $25,000 | $2,000 | +25 | +7 | +15 | 40 |
| **Cultural Center** | Postwar | 2x2 | $6,000 | $400 | +10 | +5 | +5 | 15 |
| **Monument (Small)** | Frontier | 1x1 | $500 | $10 | +5 | +2 | +2 | 0 |
| **Monument (Grand)** | Postwar | 2x2 | $10,000 | $100 | +15 | +4 | +8 | 0 |
| **Memorial** | Postwar | 1x1 | $3,000 | $30 | +8 | +3 | +5 | 0 |
| **Clock Tower** | Industrial | 1x1 | $5,000 | $200 | +10 | +4 | +5 | 2 |
| **Observatory** | Modern | 2x2 | $15,000 | $1,000 | +20 | +5 | +10 | 15 |
| **Planetarium** | Modern | 2x2 | $12,000 | $800 | +18 | +5 | +8 | 12 |

---

## 2.6 RELIGIOUS BUILDINGS

Religious buildings are era- and culture-dependent. They spawn demand based on the cultural profile of nearby residential zones.

| Building | Era | Size | Congregation | Cost | Maint/yr | Happiness | Community |
|----------|-----|------|-------------|------|----------|-----------|-----------|
| **Chapel** | Frontier | 1x1 | 50 | $300 | $20 | +3 (5 tiles) | +5% civic participation |
| **Church** | Industrial | 2x2 | 200 | $3,000 | $150 | +5 (8 tiles) | +8% civic participation |
| **Cathedral** | Industrial | 3x3 | 1,000 | $20,000 | $800 | +8 (15 tiles) | +12% civic participation |
| **Mosque** | Industrial | 2x2 | 300 | $4,000 | $200 | +5 (8 tiles) | +8% civic participation |
| **Grand Mosque** | Modern | 3x3 | 1,500 | $25,000 | $1,000 | +8 (15 tiles) | +12% civic participation |
| **Temple** | Frontier | 2x2 | 200 | $2,500 | $100 | +5 (8 tiles) | +8% civic participation |
| **Synagogue** | Industrial | 2x2 | 200 | $3,500 | $150 | +5 (8 tiles) | +8% civic participation |
| **Buddhist Monastery** | Frontier | 3x3 | 100 | $5,000 | $200 | +6 (10 tiles) | +10% civic participation |
| **Megachurch** | Modern | 4x4 | 5,000 | $15,000 | $1,200 | +6 (12 tiles) | +5% civic participation |
| **Interfaith Center** | Modern | 2x2 | 300 | $6,000 | $400 | +7 (10 tiles) | +15% civic participation |

**Mechanic**: Religious building demand scales with the religious composition dimension of the cultural profile. A neighborhood with 60% religious residents will demand 1 religious building per 500 residents. Neighborhoods with <20% religious residents have no demand. Mismatched religion (e.g., mosque in area with 0% Muslim population) provides no happiness bonus but does provide cultural diversity points.

---

## 2.7 TOURISM ATTRACTIONS

| Attraction | Era | Size | Tourists/Mo | Revenue/Mo | Cost | Maint/yr | Requirement |
|------------|-----|------|-------------|------------|------|----------|-------------|
| **Beach** | -- | Map edge | 500 | $300 | Free (natural) | $50 | Coastal map |
| **Boardwalk** | Industrial | 1xN | 200/tile | $100/tile | $500/tile | $30/tile | Coastal |
| **Pier** | Industrial | 1x3 | 300 | $2,000 | $150 | Coastal | Coastal |
| **Marina** | Postwar | 4x3 | 200 | $800 | $15,000 | $1,000 | Coastal |
| **Lighthouse** | Frontier | 1x1 | 100 | $100 | $2,000 | $80 | Coastal promontory |
| **Ski Resort** | Modern | 6x6 | 1,000 | $5,000 | $50,000 | $3,000 | Mountain map, winter |
| **Amusement Park** | Postwar | 6x6 | 3,000 | $8,000 | $40,000 | $3,000 | -- |
| **Theme Park** | Modern | 8x8 | 8,000 | $20,000 | $100,000 | $6,000 | -- |
| **Observation Tower** | Postwar | 1x1 | 500 | $600 | $10,000 | $400 | -- |
| **Ferris Wheel** | Industrial | 2x2 | 300 | $400 | $5,000 | $300 | -- |
| **Cable Car (Tourism)** | Modern | 1x1 per station | 200 | $300 | $8,000 | $500 | Hill terrain |
| **Heritage Railway** | Modern | 1xN | 150 | $200 | $3,000/tile | $100/tile | Requires old rail line |
| **Hot Springs Resort** | Modern | 3x3 | 400 | $2,000 | $20,000 | $1,200 | Geothermal deposit |

---

## 2.8 HEALTHCARE SPECIAL FACILITIES

Supplements the existing clinic/hospital tier system with specialized healthcare ploppables.

| Facility | Era | Size | Capacity | Cost | Maint/yr | Effect |
|----------|-----|------|----------|------|----------|--------|
| **Rehabilitation Center** | Postwar | 2x2 | 100 patients | $8,000 | $600 | Reduces long-term disability, returns workers to labor pool |
| **Mental Health Clinic** | Modern | 2x2 | 200 patients | $6,000 | $500 | -5% crime, +3 happiness (10 tiles), reduces stress deaths |
| **Hospice** | Modern | 2x2 | 50 patients | $4,000 | $400 | +5 happiness for elderly (reduces end-of-life healthcare burden) |
| **Spa / Wellness Center** | Modern | 3x3 | 300 visitors | $12,000 | $800 | +5 happiness (8 tiles), tourism attraction, +health |
| **Addiction Treatment** | Modern | 2x2 | 80 patients | $7,000 | $500 | Mitigates casino/entertainment district negative effects |
| **Epidemic Response Center** | Modern | 2x2 | -- | $10,000 | $1,000 | -50% pandemic spread rate, faster outbreak containment |
| **Genetic Research Clinic** | Future | 3x3 | 50 patients | $30,000 | $3,000 | +10 RP/mo (medical), extends average lifespan by 5 years |

---

# PART 3: SPECIAL EVENTS SYSTEM (DEEP)

Events are the living heartbeat of Iron & Oak. They transform the city from a spreadsheet into a place that feels alive. Events come in two forms: **Scheduled Events** (player-initiated or calendar-based) and **Emergent Events** (triggered by simulation conditions).

## 3.0 EVENT SYSTEM ARCHITECTURE

Every event follows a unified data structure:

```
Event {
  id: string
  name: string
  category: recurring | sports | cultural | trade | political | music | disaster | economic
  frequency: weekly | monthly | seasonal | annual | one-time | triggered

  // Prerequisites
  prerequisites: {
    minPopulation: number
    requiredBuildings: string[]      // e.g., ["concert_hall", "fairgrounds"]
    requiredEra: Era
    requiredTech: string[]
    minApproval: number              // some events need political capital
    seasonRestriction: Season[]      // e.g., summer-only festivals
  }

  // Costs & Revenue
  hostingCost: number                // upfront cost to host
  operatingCost: number              // per-day cost during event
  ticketRevenue: number              // daily ticket/entry revenue
  vendorRevenue: number              // food, merch, parking revenue
  sponsorRevenue: number             // corporate sponsorship (Modern+)

  // Effects
  happinessImpact: number            // city-wide happiness modifier
  localHappiness: number             // happiness in event radius
  tourismBoost: number               // additional tourists/day during event
  trafficImpact: number              // % increase in traffic near venue
  crimeImpact: number                // % change in crime near venue
  noiseImpact: number                // noise pollution during event

  // Duration & Timing
  duration: number                   // in game-days
  setupTime: number                  // days before event (construction, prep)
  cooldown: number                   // minimum days between occurrences

  // Visual
  visualEffect: string               // map overlay (flags, lights, crowds)
  spriteOverlay: string              // decorative sprites placed during event
  soundEffect: string                // ambient audio during event
}
```

---

## 3.1 RECURRING EVENTS

These happen on a regular schedule and are largely automatic once the prerequisites are met. The player can choose to fund or cancel them.

### Weekly Events

| Event | Prerequisite | Cost/Week | Revenue/Week | Happiness | Tourism | Traffic |
|-------|-------------|-----------|-------------|-----------|---------|---------|
| **Farmer's Market** | 3+ farms, market building | $50 | $120 | +2 (local) | +5 | +10% (local) |
| **Street Buskers** | Cultural engagement > 40% | $0 (free) | $0 | +1 (local) | +2 | 0 |
| **Sunday Church Gathering** | Religious building | $0 | $0 | +1 (local) | 0 | +5% (local) |
| **Night Market** | Pop > 5,000, entertainment district | $100 | $250 | +2 (local) | +8 | +15% (local) |
| **Food Truck Rally** | Pop > 10,000, Modern era | $80 | $200 | +2 (local) | +5 | +10% (local) |

### Monthly Events

| Event | Prerequisite | Cost/Event | Revenue | Happiness | Tourism | Traffic |
|-------|-------------|-----------|---------|-----------|---------|---------|
| **Art Walk** | Art gallery, cultural engagement > 50% | $200 | $350 | +3 (local) | +10 | +8% (local) |
| **Craft Fair** | Community garden or market hall | $150 | $300 | +2 (local) | +8 | +10% (local) |
| **Car Show** | Pop > 20,000, Postwar era | $500 | $800 | +2 (city) | +15 | +20% (district) |
| **Flea Market** | Town square or fairgrounds | $100 | $250 | +2 (local) | +5 | +12% (local) |
| **Open Mic Night** | Concert hall or nightclub | $50 | $150 | +2 (local) | +3 | +5% (local) |
| **Community Cleanup Day** | Pop > 2,000, environmental values > 30% | $100 | $0 | +3 (city) | 0 | 0 |

### Seasonal Events (4 per year)

| Event | Season | Prerequisite | Cost | Revenue | Happiness | Tourism | Duration |
|-------|--------|-------------|------|---------|-----------|---------|----------|
| **Spring Fair** | Spring | Fairgrounds, pop > 5,000 | $2,000 | $5,000 | +5 (city) | +30 | 7 days |
| **Summer Music Festival** | Summer | Outdoor festival ground, pop > 10,000 | $8,000 | $15,000 | +8 (city) | +60 | 5 days |
| **Harvest Festival** | Autumn | 5+ farms, fairgrounds | $1,500 | $4,000 | +6 (city) | +25 | 5 days |
| **Winter Carnival** | Winter | Pop > 5,000, park or fairgrounds | $3,000 | $6,000 | +7 (city) | +35 | 7 days |
| **Cherry Blossom Viewing** | Spring | Botanical garden or large park | $500 | $1,200 | +4 (city) | +20 | 14 days |
| **Beach Season Opening** | Summer | Beach (coastal map) | $800 | $3,000 | +5 (city) | +40 | 90 days |
| **Oktoberfest** | Autumn | Pop > 20,000, cultural diversity | $5,000 | $12,000 | +6 (city) | +50 | 14 days |
| **Holiday Lights** | Winter | Pop > 10,000 | $2,000 | $3,000 | +8 (city) | +20 | 30 days |

**Visual Representation**: Seasonal events place decorative overlays on the map -- bunting and flags for Spring Fair, stage and speaker sprites for Music Festival, hay bales and pumpkins for Harvest, ice sculptures and lights for Winter Carnival. Citizens are shown walking toward the event venue.

---

## 3.2 SPORTS EVENTS

Sports events are tied to the Sports League System (Part 4). They occur automatically when stadiums and teams exist.

### Regular Season Games

| Sport | Frequency | Venue Required | Attendance | Revenue/Game | Traffic Spike | Duration |
|-------|-----------|---------------|------------|-------------|--------------|----------|
| **Baseball** | 2/week (spring-autumn) | Baseball diamond/stadium | 500-30,000 | $200-$12,000 | +15-40% | 3 hours |
| **Football/Soccer** | 1/week (all year) | Soccer field/stadium | 1,000-60,000 | $500-$25,000 | +20-50% | 2 hours |
| **Basketball** | 2/week (autumn-spring) | Basketball court/arena | 200-20,000 | $100-$8,000 | +10-30% | 2 hours |
| **Hockey** | 2/week (winter) | Ice rink/arena | 300-18,000 | $150-$7,000 | +10-30% | 2 hours |
| **Tennis** | Seasonal tournament | Tennis courts | 100-15,000 | $50-$6,000 | +5-25% | 5 hours |
| **Motorsport** | Monthly (spring-autumn) | Motorsport track | 5,000-60,000 | $2,000-$20,000 | +30-60% | 4 hours |
| **Esports** | Weekly (Future era) | Esports arena | 1,000-10,000 | $500-$4,000 | +5-15% | 3 hours |

### Championship Events

| Event | Prerequisite | Frequency | Cost to Host | Revenue | Tourism Boost | Happiness | Duration |
|-------|-------------|-----------|-------------|---------|--------------|-----------|----------|
| **League Championship** | Team in finals | Annual | $5,000 | $20,000 | +40 | +8 (city) | 1-3 days |
| **Regional Tournament** | Major stadium, pop > 50,000 | Annual | $15,000 | $50,000 | +80 | +10 (city) | 7 days |
| **National Championship** | Olympic stadium, pop > 100,000 | Every 4 years | $50,000 | $150,000 | +150 | +15 (city) | 14 days |
| **Olympics Bid** | Olympic stadium + 5 venues + intl airport + 10,000 hotel rooms | Once | $500,000 | $2,000,000 | +500 | +20 (city) | 30 days |

**Olympics Bid Mechanic**:
1. Player submits bid (costs $50,000 just to bid)
2. Evaluation based on: stadium capacity, venue count, transport quality, hotel capacity, international airport, city prestige
3. Score 80+ / 100 = 60% chance of winning bid. Score 90+ = 85% chance.
4. If won: 2-year preparation period. Must build athlete village (temporary housing for 5,000), upgrade transport. Massive construction cost but transforms city.
5. During Olympics: 30 days of peak tourism, global prestige, massive revenue. Traffic nightmare.
6. After Olympics: Athlete village converts to housing. Stadiums remain. +50 permanent prestige. Risk of "white elephant" facilities if city can't fill them post-games.

---

## 3.3 CULTURAL EVENTS

| Event | Prerequisite | Cost | Revenue | Happiness | Tourism | Duration | Traffic |
|-------|-------------|------|---------|-----------|---------|----------|---------|
| **Film Festival** | Cinema + convention center, pop > 30,000 | $10,000 | $25,000 | +6 (city) | +60 | 10 days | +25% (district) |
| **Fashion Week** | Pop > 50,000, Modern era, cultural engagement > 60% | $15,000 | $35,000 | +5 (city) | +80 | 7 days | +30% (district) |
| **Food Festival** | 10+ restaurants (commercial), fairgrounds | $3,000 | $8,000 | +7 (city) | +35 | 5 days | +20% (local) |
| **Pride Parade** | Pop > 20,000, Modern era, cultural diversity > 50% | $2,000 | $5,000 | +8 (city) | +30 | 1 day | +40% (route) |
| **Cultural Heritage Days** | 3+ cultural landmarks, diversity index > 0.4 | $1,500 | $4,000 | +6 (city) | +25 | 3 days | +15% (district) |
| **Book Fair** | Library + convention center | $2,000 | $3,500 | +4 (city) | +15 | 5 days | +10% (local) |
| **Street Art Festival** | Cultural engagement > 50%, Modern era | $1,000 | $2,500 | +5 (city) | +20 | 3 days | +10% (district) |
| **International Film Premiere** | Cinema, pop > 100,000, intl airport | $5,000 | $15,000 | +4 (city) | +40 | 1 day | +15% (district) |
| **Lantern Festival** | Cultural diversity, temple/interfaith center | $1,000 | $3,000 | +6 (city) | +20 | 1 day | +20% (local) |
| **Dia de los Muertos** | Cultural diversity, cemetery + cultural center | $800 | $2,500 | +5 (city) | +15 | 2 days | +10% (local) |

---

## 3.4 TRADE EVENTS

| Event | Prerequisite | Cost | Revenue | Economic Effect | Tourism | Duration |
|-------|-------------|------|---------|----------------|---------|----------|
| **Trade Fair** | Convention center, pop > 20,000 | $5,000 | $12,000 | +10% trade volume for 30 days | +30 | 5 days |
| **Auto Show** | Convention center, auto factory or car dealer | $8,000 | $18,000 | Auto industry +15% revenue for 30 days | +40 | 5 days |
| **Tech Expo** | Convention center, tech district, Modern era | $12,000 | $30,000 | +20% tech startup spawn for 60 days | +60 | 5 days |
| **Real Estate Convention** | Convention center, pop > 50,000 | $6,000 | $10,000 | +5% construction speed, +land value speculation | +20 | 3 days |
| **Agricultural Expo** | Fairgrounds, 10+ farms | $3,000 | $7,000 | +10% farm yield for 30 days (knowledge sharing) | +15 | 3 days |
| **Energy Summit** | Convention center, Modern era, green energy | $10,000 | $15,000 | -5% energy costs for 60 days | +25 | 3 days |
| **Startup Demo Day** | Tech incubator, 3+ startups | $2,000 | $5,000 | +15% startup funding, unicorn chance +0.5% | +10 | 1 day |
| **Job Fair** | Convention center, unemployment > 5% | $1,500 | $2,000 | -2% unemployment for 30 days | +5 | 2 days |

---

## 3.5 POLITICAL EVENTS

| Event | Trigger | Cost | Effect | Duration | Approval Impact |
|-------|---------|------|--------|----------|----------------|
| **Mayoral Election** | Every 4 years (automatic) | $0 | Campaign period: citizens voice demands, promises made | 30 days | +/-10 based on kept promises |
| **Inauguration** | After election | $500 | Ceremony at city hall, brief happiness boost | 1 day | +3 |
| **State Visit** | Embassy + pop > 50,000 | $5,000 | Tourism boost, trade deal opportunity | 3 days | +5 |
| **G8 Summit** | Capitol complex + intl airport + pop > 100,000 | $50,000 | Massive security, traffic lockdown, global prestige | 5 days | +10 prestige, -3 happiness (security) |
| **City Council Meeting** | Town/city hall (monthly, automatic) | $0 | Citizens petition, policy votes | 1 day | +/-2 based on decisions |
| **Referendum** | Triggered by controversial project | $1,000 | Citizens vote yes/no on specific project | 7 days (campaign) | +5 if popular vote wins, -10 if override |
| **Protest March** | Triggered: happiness < 40 or controversial policy | $0 | Route blocks traffic, -3 happiness, demands issued | 1-3 days | -5 to -15 |
| **Victory Celebration** | After winning Olympics bid, championship, or milestone | $2,000 | City-wide party | 1 day | +10 |
| **Independence Day** | Annual (Postwar+) | $1,000 | Fireworks, parade, patriotic spirit | 1 day | +5 happiness |
| **Disaster Declaration** | After major disaster | $0 | Federal aid unlocked, emergency powers | Until resolved | -5 (crisis) then +5 (recovery) |

---

## 3.6 MUSIC EVENTS

| Event | Prerequisite | Cost | Revenue | Happiness | Tourism | Traffic | Duration |
|-------|-------------|------|---------|-----------|---------|---------|----------|
| **Street Busking** | Cultural engagement > 30% | $0 | $0 | +1 (local) | +1 | 0 | Ongoing |
| **Local Band Night** | Nightclub or bar | $100 | $250 | +2 (local) | +3 | +5% (local) | 1 evening |
| **Orchestra Concert** | Concert hall, pop > 20,000 | $2,000 | $5,000 | +4 (city) | +15 | +10% (district) | 1 evening |
| **Rock Concert** | Concert hall or mega arena | $5,000 | $15,000 | +5 (city) | +30 | +25% (district) | 1 evening |
| **Jazz Festival** | 2+ music venues, cultural diversity | $3,000 | $8,000 | +5 (city) | +25 | +15% (district) | 3 days |
| **Outdoor Music Festival** | Outdoor festival ground, pop > 25,000 | $15,000 | $40,000 | +8 (city) | +80 | +40% (district) | 3 days |
| **EDM / Rave Event** | Modern era, mega arena or festival ground | $8,000 | $20,000 | +4 (city) | +40 | +30% (district) | 2 days |
| **Classical Music Series** | Opera house, cultural engagement > 60% | $1,000/concert | $2,500/concert | +3 (city) | +10 | +8% (local) | 1 evening (8/yr) |
| **Battle of the Bands** | 3+ music venues, pop > 10,000 | $500 | $1,500 | +3 (city) | +10 | +10% (local) | 1 day |
| **Music Conservatory Recital** | University with music program | $200 | $400 | +2 (local) | +3 | +3% (local) | 1 evening |

---

## 3.7 DISASTER EVENTS (Expanded)

Disasters now have multi-phase emergency response mechanics.

### Disaster Types

| Disaster | Era Risk | Trigger | Severity Range | Base Damage | Warning Time |
|----------|----------|---------|---------------|-------------|-------------|
| **Fire (Building)** | All | Random, drought, old buildings | 1-3 | $500-$5,000 | None |
| **Fire (District)** | Frontier-Industrial | Dense wooden buildings + drought | 4-7 | $10,000-$100,000 | Minutes |
| **Flood (River)** | All | Spring, heavy rain | 3-6 | $5,000-$50,000 | Hours |
| **Flood (Coastal)** | All | Storm surge, hurricane | 5-8 | $20,000-$200,000 | Days |
| **Earthquake** | All | Random (tectonic map zones) | 4-9 | $10,000-$500,000 | None |
| **Tornado** | Postwar+ | Plains maps, summer storms | 3-7 | $5,000-$100,000 | Minutes |
| **Hurricane** | Postwar+ | Coastal maps, autumn | 6-10 | $50,000-$1,000,000 | Days |
| **Blizzard** | All | Winter, northern maps | 2-5 | $2,000-$20,000 | Hours |
| **Drought** | All | Summer, climate cycle | 2-4 | $5,000-$30,000 (agriculture) | Weeks |
| **Pandemic** | All | Random, scaled by density | 3-8 | $10,000-$200,000 (healthcare) | Days |
| **Industrial Accident** | Industrial+ | Heavy industry, poor maintenance | 3-7 | $5,000-$80,000 | None |
| **Nuclear Incident** | Modern+ | Nuclear plant, poor maintenance | 8-10 | $100,000-$5,000,000 | None |
| **Cyberattack** | Future | Data centers, poor cybersecurity | 2-5 | $10,000-$50,000 | None |
| **AI Malfunction** | Future | AI systems, over-automation | 3-6 | $20,000-$100,000 | None |

### Emergency Response System

When a disaster strikes, a multi-phase response kicks in:

**Phase 1: Detection & Warning** (0-1 game hours)
- Warning systems (if researched/built) alert citizens
- Siren sounds, news ticker flashes
- Citizens in danger zone begin evacuation if shelters available
- Player can activate emergency protocols

**Phase 2: Impact** (1-4 game hours)
- Damage occurs based on severity, building quality, and preparedness
- Emergency services auto-dispatch (fire trucks, ambulances, police)
- Roads may be blocked by debris
- Power/water infrastructure may be damaged

**Phase 3: Response** (hours to days)
- Fire department fights fires (effectiveness based on coverage and response time)
- Hospitals treat injured (overflow if capacity exceeded)
- Police manage traffic and prevent looting
- Player can deploy emergency funds
- Mutual aid from neighboring towns (regional play)

**Phase 4: Recovery** (days to months)
- Damaged buildings must be repaired or demolished
- Insurance payouts partially cover rebuilding ($0 if no insurance policy)
- Federal disaster aid available if Disaster Declaration issued
- Temporary housing needed for displaced citizens
- Psychological impact: -5 happiness in affected area for 6 months

### Emergency Preparedness Buildings

| Building | Era | Cost | Maint/yr | Effect |
|----------|-----|------|----------|--------|
| **Siren System** | Postwar | $1,000 | $50 | +30 min warning for weather disasters |
| **Emergency Shelter** | Postwar | $3,000 | $200 | Houses 500 displaced citizens |
| **Seismograph Station** | Modern | $5,000 | $300 | 10-second earthquake warning (saves lives) |
| **Flood Barrier** | Industrial | $500/tile | $20/tile | Prevents river flooding in protected area |
| **Sea Wall** | Industrial | $800/tile | $30/tile | Prevents coastal flooding |
| **Storm Drain System** | Postwar | $200/tile | $10/tile | Reduces urban flooding by 60% |
| **Firebreak** | Frontier | $50/tile | $5/tile | Prevents fire spread across gap |
| **Emergency Operations Center** | Modern | $15,000 | $1,000 | +50% response speed, coordination bonus |
| **Disaster Insurance Policy** | Modern | $0 build | $500/yr per $100k insured | Pays 60% of disaster damage |

---

## 3.8 ECONOMIC EVENTS

Emergent events triggered by economic simulation conditions.

| Event | Trigger Condition | Effect | Duration | Player Response Options |
|-------|-------------------|--------|----------|----------------------|
| **Recession** | GDP growth negative for 4 quarters | -20% all zone demand, -15% tax revenue, unemployment +5% | 12-24 months | Stimulus spending, tax cuts, public works |
| **Housing Boom** | Immigration > 5%/yr + low interest rates | +40% residential demand, land value +20%, risk of bubble | 12-36 months | Restrict speculation, build public housing, or ride it |
| **Housing Crash** | Boom ends, overbuilding detected | -30% land value, construction halts, foreclosures | 12-18 months | Bail out banks, buy distressed properties, wait |
| **Stock Market Crash** | Random (2% annual chance in Modern+) | -25% commercial revenue, -10% happiness, wealthy flee | 6-12 months | Emergency measures, stimulus |
| **Crypto Boom** | Future era, data centers + tech district | +30% tech sector revenue, speculation, energy demand spike | 6-18 months | Regulate, tax, or encourage |
| **Crypto Bust** | Follows boom (60% chance) | Tech sector -20%, energy demand drops, investor losses | 6-12 months | Consumer protection, bailout, or ignore |
| **Startup IPO** | Unicorn startup matures | $50,000-$500,000 windfall, +50 high-skill jobs, prestige | Permanent | Celebrate, invest proceeds |
| **Factory Closure** | Industry unprofitable for 8+ months | 100-500 job losses, -10 happiness (local), blight risk | Permanent until replaced | Retrain workers, attract new industry, demolish |
| **Oil Price Shock** | Random (5% annual chance) | Energy costs +50%, transport costs +30%, inflation | 6-12 months | Switch to renewables, subsidize, ration |
| **Trade War** | Random (3% annual chance in Modern+) | Import prices +30%, export revenue -20% | 12-24 months | Tariffs, diversify trade, domestic production |
| **Tech Bubble** | Tech district revenue doubles in < 2 years | Unsustainable growth, warning signs | 6-18 months | Regulate growth, diversify economy |
| **Minimum Wage Strike** | Working class happiness < 35, industrial zone present | Factories halt for 1-5 days, -5 happiness | 1-5 days | Raise wages, negotiate, suppress (approval -10) |
| **Tourism Boom** | Major event success + intl airport + 80+ prestige | +50% tourism revenue, hotel demand spikes | 12-24 months | Build hotels, expand attractions, manage traffic |

---

# PART 4: SPORTS LEAGUE SYSTEM

The Sports League System adds a persistent, evolving sports ecosystem to Iron & Oak. Teams form organically, play seasons, attract fans, generate revenue, and become part of the city's identity.

---

## 4.1 SYSTEM OVERVIEW

### How Teams Form

Teams are not placed by the player. They **emerge automatically** when conditions are met:

| Condition | Result |
|-----------|--------|
| Population reaches 5,000 + local stadium exists | First local team forms (most popular sport based on culture) |
| Population reaches 15,000 + additional stadium | Second team may form |
| Population reaches 30,000 per team | Additional teams (diminishing returns) |
| University with sports center | University team forms (lower tier, feeder system) |

**Sport Popularity by Culture**: The cultural profile of the city determines which sports are popular:

| Cultural Dimension | Favored Sports |
|-------------------|----------------|
| High diversity index | Soccer, basketball |
| American cultural profile | Baseball, American football, basketball |
| European cultural profile | Soccer, tennis, cycling |
| Asian cultural profile | Soccer, baseball, martial arts |
| Working class dominant | Boxing, soccer, baseball |
| Wealthy dominant | Tennis, golf, sailing |
| Young population skew | Skateboarding, esports, basketball |
| Cold climate / winter | Hockey, skiing, ice skating |

---

## 4.2 SPORTS & VENUE REQUIREMENTS

### Sport Definitions

| Sport | Venue Required | Min Venue Capacity | Season | Games/Season | Revenue/Game* | Era |
|-------|---------------|-------------------|--------|-------------|--------------|-----|
| **Baseball** | Baseball diamond or stadium | 500 | Spring-Autumn | 40 | $4/seat | Frontier |
| **Soccer/Football** | Soccer field or stadium | 1,000 | Year-round | 20 | $5/seat | Industrial |
| **Basketball** | Basketball court or arena | 200 | Autumn-Spring | 30 | $5/seat | Postwar |
| **Hockey** | Ice rink or arena | 300 | Winter | 25 | $6/seat | Postwar |
| **American Football** | Stadium | 5,000 | Autumn | 10 | $8/seat | Postwar |
| **Tennis** | Tennis courts | 100 | Spring-Summer | 8 (tournament) | $10/seat | Industrial |
| **Boxing/MMA** | Arena | 2,000 | Year-round | 6 | $15/seat | Industrial |
| **Motor Racing** | Motorsport track | 5,000 | Spring-Autumn | 8 | $12/seat | Postwar |
| **Cycling** | Velodrome or road circuit | 1,000 | Spring-Summer | 6 | $3/seat | Industrial |
| **Esports** | Esports arena | 1,000 | Year-round | 30 | $4/seat | Future |
| **Golf** | Golf course | 200 (spectators) | Spring-Autumn | 4 (tournament) | $20/spectator | Postwar |

*Revenue per seat at full attendance. Actual attendance varies (see 4.4).

### Stadium Tiers & Upgrades

| Tier | Capacity | Cost | Maint/yr | Amenities | Revenue Multiplier | Era |
|------|----------|------|----------|-----------|-------------------|-----|
| **Tier 1: Local Field** | 500-2,000 | $2,000-$8,000 | $200-$500 | Bleachers only | 1.0x | Frontier |
| **Tier 2: Community Stadium** | 2,000-10,000 | $15,000-$30,000 | $1,000-$2,000 | Concessions, restrooms | 1.2x | Industrial |
| **Tier 3: City Stadium** | 10,000-30,000 | $50,000-$100,000 | $3,000-$5,000 | Luxury boxes, parking, merch | 1.5x | Postwar |
| **Tier 4: Major Stadium** | 30,000-60,000 | $100,000-$200,000 | $5,000-$8,000 | VIP suites, restaurants, museum | 1.8x | Modern |
| **Tier 5: Olympic/World-Class** | 60,000-100,000 | $200,000-$500,000 | $8,000-$15,000 | Retractable roof, smart features | 2.2x | Modern |

**Upgrade Path**: Stadiums can be upgraded in-place (cheaper than rebuilding) or demolished and rebuilt. Upgrading takes 6-12 months of construction during which the team plays at a temporary venue (reduced revenue).

---

## 4.3 TEAM ATTRIBUTES

Each team has persistent attributes that evolve over time:

```
Team {
  name: string                    // Auto-generated: "[City Name] [Mascot]"
  sport: Sport
  tier: 1-5                       // Amateur -> Professional -> Elite
  fanBase: number                 // % of city population that are fans (5-40%)

  // Performance
  teamRating: number              // 1-100, determines win probability
  chemistry: number               // 1-100, builds over seasons
  coachRating: number             // 1-100, affects improvement rate
  youthDevelopment: number        // 1-100, determines prospect quality

  // Finances
  annualBudget: number            // Player sets funding level
  playerSalaries: number          // Scales with tier and rating
  merchandiseRevenue: number      // Scales with fan base
  sponsorRevenue: number          // Scales with tier and city prestige

  // History
  championships: number
  bestSeasonRecord: string
  rivalTeams: string[]            // Regional rivals for derby matches
  legendaryPlayers: string[]      // Hall of fame, generates nostalgia tourism
}
```

### Team Rating Factors

| Factor | Weight | Description |
|--------|--------|-------------|
| **Funding** | 30% | Higher budget = better players, facilities |
| **Stadium Quality** | 15% | Better stadium = better recruitment |
| **City Happiness** | 10% | Happy city = motivated players |
| **University Pipeline** | 15% | University sports center = youth talent |
| **Fan Support** | 10% | Large fanbase = home advantage |
| **Coach Rating** | 10% | Better coach = faster improvement |
| **Chemistry** | 10% | Builds over time, resets when roster changes |

### Team Tier Progression

| Tier | Name | Team Rating | Budget Required | Fan Base | Revenue Range |
|------|------|------------|----------------|----------|---------------|
| **1** | Amateur/Recreational | 1-20 | $500/yr | 5% of pop | $500-$2,000/yr |
| **2** | Semi-Professional | 21-40 | $2,000/yr | 10% of pop | $2,000-$8,000/yr |
| **3** | Professional (Minor) | 41-60 | $8,000/yr | 15% of pop | $8,000-$30,000/yr |
| **4** | Professional (Major) | 61-80 | $25,000/yr | 25% of pop | $30,000-$100,000/yr |
| **5** | Elite/Championship | 81-100 | $60,000/yr | 35% of pop | $100,000-$400,000/yr |

---

## 4.4 SEASON SIMULATION

### Match Resolution

Each match is resolved using a weighted probability model (not simulated play-by-play):

```
home_advantage = 1.1  // 10% home bonus
effective_rating_home = team_rating_home * home_advantage * morale_modifier * weather_modifier
effective_rating_away = team_rating_away * morale_modifier

win_probability_home = effective_rating_home / (effective_rating_home + effective_rating_away)

// Roll result
result = random()
if result < win_probability_home * 0.55:  home_win
elif result < win_probability_home * 0.55 + 0.20:  draw (sports that allow draws)
else:  away_win
```

### Attendance Model

```
base_attendance = stadium_capacity * 0.5
fan_bonus = fan_base_percentage * city_population * 0.02
rivalry_bonus = is_rival_match ? capacity * 0.15 : 0
championship_bonus = is_playoff ? capacity * 0.25 : 0
weather_penalty = bad_weather ? -capacity * 0.10 : 0
winning_streak_bonus = consecutive_wins * capacity * 0.02

attendance = min(stadium_capacity, base_attendance + fan_bonus + rivalry_bonus + championship_bonus + weather_penalty + winning_streak_bonus)
```

### Season Structure

| Phase | Duration | Events |
|-------|----------|--------|
| **Pre-Season** | 1 month | Training camp, exhibition games, roster changes |
| **Regular Season** | 6-9 months (sport-dependent) | Weekly/biweekly games, standings updated |
| **Playoffs** | 1 month | Top 4/8 teams compete in bracket tournament |
| **Championship** | 1 game/series | Winner crowned, city-wide celebration event |
| **Off-Season** | 2-3 months | Player trades, draft (university pipeline), facility upgrades |

### Season Results & Effects

| Outcome | Happiness | Tourism | Revenue Modifier | Special |
|---------|-----------|---------|-----------------|---------|
| Losing season (< 30% wins) | -3 city-wide | -5 | 0.7x | Fan base shrinks 5% |
| Below average (30-45%) | -1 city-wide | 0 | 0.9x | -- |
| Average (45-55%) | 0 | +5 | 1.0x | -- |
| Good season (55-70%) | +3 city-wide | +10 | 1.2x | Fan base grows 3% |
| Great season (70-85%) | +5 city-wide | +20 | 1.5x | Fan base grows 8% |
| Championship win | +10 city-wide for 30 days | +50 | 2.0x | Parade event, +10 prestige, fan base grows 15% |
| Dynasty (3+ championships in 5 years) | +5 permanent | +30 permanent | 2.5x | National recognition, tourism landmark |

---

## 4.5 FAN CULTURE & CITY IDENTITY

### Fan Behavior

| Fan Base Size | Behavior |
|--------------|----------|
| < 10% of pop | Quiet support, low merch sales |
| 10-20% | Visible fandom, team colors on buildings near stadium |
| 20-30% | Strong identity, bars/restaurants themed around team |
| 30-40% | Obsessive fandom, game day is city event, traffic chaos |
| 40%+ | Cult following, city identity tied to team, riots if team relocates |

### Game Day Traffic Impact

| Stadium Capacity | Traffic Increase | Radius | Duration |
|-----------------|-----------------|--------|----------|
| 500-2,000 | +10% | 5 tiles | 2 hours before/after |
| 2,000-10,000 | +20% | 10 tiles | 3 hours before/after |
| 10,000-30,000 | +35% | 15 tiles | 4 hours before/after |
| 30,000-60,000 | +50% | 20 tiles | 5 hours before/after |
| 60,000+ | +70% | 25 tiles | 6 hours before/after |

**Mitigation**: Stadium near transit hub reduces traffic impact by 40%. Dedicated stadium parking reduces by 20%. Game-day shuttle bus service reduces by 15%.

### Rivalry System

When two cities in a regional play share the same sport, a rivalry naturally forms:

| Rivalry Intensity | Condition | Effect |
|------------------|-----------|--------|
| Friendly | Both teams exist | +10% attendance for matchups |
| Heated | 5+ years of competition | +20% attendance, merch boost, mild hooliganism risk |
| Fierce | Championship decided between rivals | +30% attendance, major merch, security costs +50% |
| Historic | 20+ years, multiple championships contested | +40% attendance, tourism attraction, cultural identity |

---

## 4.6 FINANCIAL MODEL

### Revenue Breakdown (Per Season, Tier 4 Team Example)

| Source | Amount | % of Total |
|--------|--------|-----------|
| Ticket Sales | $40,000 | 40% |
| Concessions & Parking | $15,000 | 15% |
| Merchandise | $12,000 | 12% |
| Broadcasting Rights (Modern+) | $18,000 | 18% |
| Sponsorship | $10,000 | 10% |
| VIP/Luxury Boxes | $5,000 | 5% |
| **Total Revenue** | **$100,000** | **100%** |

### Expense Breakdown (Per Season, Tier 4 Team Example)

| Expense | Amount | % of Total |
|---------|--------|-----------|
| Player Salaries | $35,000 | 47% |
| Coaching Staff | $8,000 | 11% |
| Stadium Operations | $12,000 | 16% |
| Travel (Away Games) | $5,000 | 7% |
| Marketing | $4,000 | 5% |
| Youth Development | $3,000 | 4% |
| Medical Staff | $3,000 | 4% |
| Equipment | $2,000 | 3% |
| Insurance | $2,000 | 3% |
| **Total Expenses** | **$74,000** | **100%** |

**Net Profit**: $26,000/season (Tier 4 example). Player can choose to reinvest into team or take profit as city revenue.

### Funding Slider

The player controls team funding via a slider:

| Funding Level | Budget Modifier | Effect |
|--------------|----------------|--------|
| Minimal (50%) | 0.5x base | Team rating declines, fan base shrinks, risk of relocation |
| Reduced (75%) | 0.75x base | Slow decline, no improvement |
| Standard (100%) | 1.0x base | Maintains current level |
| Increased (125%) | 1.25x base | Steady improvement, attracts better players |
| Maximum (150%) | 1.5x base | Rapid improvement, star player recruitment |
| Championship Push (200%) | 2.0x base | All-in, major improvement but unsustainable long-term |

---

## 4.7 INTEGRATION WITH OTHER SYSTEMS

### Sports & Economy
- Stadium construction creates temporary construction jobs (50-200)
- Game-day spending boosts nearby commercial zone revenue by 15-30%
- Championship wins attract corporate relocations (+5% office demand for 12 months)
- Professional teams create 30-200 permanent jobs (staff, media, management)

### Sports & Demographics
- Young adult population is primary fan demographic
- University sports pipeline feeds professional teams
- Aging population reduces fan engagement slightly
- Cultural diversity affects sport preferences

### Sports & Transport
- Stadium must be accessible by road (minimum avenue)
- Transit access to stadium dramatically improves attendance and reduces traffic
- Dedicated park-and-ride for game days (player can build)
- Regional rivals cause inter-city traffic spikes

### Sports & Politics
- Championship wins boost approval rating +3 for 6 months
- Stadium funding is politically contentious (referendum may be needed for public funding)
- Team relocation threat if funding/facilities inadequate (massive approval hit)
- Citizens petition for new sports facilities based on cultural demand

### Sports & Tourism
- Winning teams are a tourism attraction (fans from rival cities visit)
- Historic stadiums become landmarks (+tourism even on non-game days)
- Championship merchandise sold at commercial zones
- Sports museum ploppable unlocked after 10+ seasons (permanent tourism)

---

# APPENDIX A: COMPLETE EVENT CALENDAR (Example Year)

This shows what a typical year of events looks like in a mature Modern-era city (pop ~80,000):

| Month | Week | Events |
|-------|------|--------|
| **January** | 1 | Hockey game, Basketball game |
| | 2 | Farmer's market, Basketball game, Hockey game |
| | 3 | Art walk, Hockey game, Basketball game |
| | 4 | City council meeting, Hockey game, Basketball game |
| **February** | 1 | Hockey game, Basketball game, Farmer's market |
| | 2 | Basketball game, Hockey game |
| | 3 | Art walk, Hockey game, Basketball game |
| | 4 | Hockey game, Basketball game, Monthly craft fair |
| **March** | 1 | Basketball game, Farmer's market |
| | 2 | Basketball playoffs begin, Art walk |
| | 3 | Basketball playoff game |
| | 4 | City council meeting, Basketball championship |
| **April** | 1 | **Spring Fair** (7 days), Baseball season opens |
| | 2 | Baseball game x2, Farmer's market, Cherry blossom viewing begins |
| | 3 | Art walk, Baseball game x2, Soccer season opens |
| | 4 | Baseball game x2, Soccer game, Job fair |
| **May** | 1 | Baseball game x2, Soccer game, Farmer's market |
| | 2 | Baseball game x2, Soccer game, Art walk |
| | 3 | Baseball game x2, Soccer game, Car show |
| | 4 | City council meeting, Baseball game x2, Soccer game |
| **June** | 1 | Baseball game x2, Soccer game, Farmer's market |
| | 2 | **Summer Music Festival** (5 days), Baseball game x2 |
| | 3 | Art walk, Baseball game x2, Soccer game, Pride parade |
| | 4 | Baseball game x2, Soccer game, Beach season peaks |
| **July** | 1 | Baseball game x2, Soccer game, **Independence Day**, Farmer's market |
| | 2 | Baseball All-Star game, Art walk, Food festival |
| | 3 | Baseball game x2, Soccer game, Tennis tournament |
| | 4 | City council meeting, Baseball game x2, Soccer game |
| **August** | 1 | Baseball game x2, Soccer game, Farmer's market |
| | 2 | Baseball game x2, Soccer game, Art walk, Tech expo |
| | 3 | Baseball game x2, Soccer game, Outdoor music festival |
| | 4 | Baseball pennant race, Soccer game, Trade fair |
| **September** | 1 | Baseball playoffs, Soccer game, Farmer's market, American football opens |
| | 2 | Baseball championship series, Art walk, American football game |
| | 3 | **Harvest Festival** (5 days), Soccer game, American football game |
| | 4 | City council meeting, Film festival, Soccer game |
| **October** | 1 | Soccer game, American football game, Farmer's market, **Oktoberfest** begins |
| | 2 | Soccer game, American football game, Art walk, Basketball season opens |
| | 3 | Hockey season opens, American football game, Basketball game |
| | 4 | Soccer game, American football game, Basketball game, Hockey game |
| **November** | 1 | All sports active (peak schedule), Farmer's market |
| | 2 | American football game, Soccer game, Basketball game, Hockey game, Art walk |
| | 3 | American football playoffs, Basketball game, Hockey game |
| | 4 | City council meeting, Basketball game, Hockey game |
| **December** | 1 | **Winter Carnival** (7 days), Basketball game, Hockey game |
| | 2 | **Holiday Lights** begin, Basketball game, Hockey game, Fashion week |
| | 3 | Basketball game, Hockey game, Classical music series |
| | 4 | Basketball game, Hockey game, New Year's Eve celebration |

---

# APPENDIX B: ASSET COUNT IMPACT

These expanded systems require additional art assets:

| Category | New Assets Needed | AI-Assistable? |
|----------|------------------|---------------|
| Farming zone buildings (9 sub-types x 3 growth stages) | 27 | Yes |
| Waste management buildings (11 types) | 11 | Yes |
| Energy buildings (14 types) | 14 | Yes |
| Tech district buildings (7 types x 2 growth) | 14 | Yes |
| AI sector buildings (6 types) | 6 | Yes |
| Military/Government buildings (13 types) | 13 | Yes |
| Entertainment buildings (10 types) | 10 | Yes |
| University buildings (11 types) | 11 | Yes |
| Water features (8 types) | 8 | Yes |
| Sports facilities (15 types) | 15 | Yes |
| Event venues (10 types) | 10 | Yes |
| Parks (14 types) | 14 | Yes |
| Cultural landmarks (14 types) | 14 | Yes |
| Religious buildings (10 types) | 10 | Yes |
| Tourism attractions (13 types) | 13 | Yes |
| Healthcare special (7 types) | 7 | Yes |
| Event decorations (per event type, ~30) | 30 | Yes |
| Stadium tiers (5 tiers x 3 sports) | 15 | Yes |
| Team logos/mascots (procedural) | 20 templates | Partial |
| Fan crowd sprites | 8 | Yes |
| Disaster effects (14 types) | 14 | Yes |
| Emergency vehicles | 6 | Yes |
| **Total New Sprites** | **~290** | **~95% AI-assisted** |

This brings the total project sprite count from ~930 to ~1,220.

---

# APPENDIX C: SIMULATION PERFORMANCE IMPACT

| System | Tick Rate | Data Per Entity | Max Entities | Memory Impact |
|--------|-----------|----------------|-------------|---------------|
| Farming (crop rotation) | 1/game-season | 32 bytes | 200 farms | ~6 KB |
| Waste flow | 1/game-day | 16 bytes | 65,536 tiles | ~1 MB |
| Energy grid | 1/game-hour | 8 bytes | 500 power sources | ~4 KB |
| Sports leagues | 1/game-week | 256 bytes | 20 teams | ~5 KB |
| Events calendar | 1/game-day | 128 bytes | 50 active events | ~6 KB |
| Emergency response | On-trigger | 512 bytes | 5 concurrent | ~2.5 KB |
| **Total additional** | -- | -- | -- | **~1.02 MB** |

Well within the 250MB memory budget. The heaviest addition is the waste flow grid, which piggybacks on the existing tile-based dirty-flag system.

---

*Document version: 1.0*
*Created: March 2026*
*Companion to: CITY_BUILDER_GDD.md, RESEARCH_TECH_TREE.md*
*Total new zone types: 8 (with 9 farming sub-types)*
*Total new ploppable types: 91*
*Total event types: 80+*
*Sports with full league support: 11*
