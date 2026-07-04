# IRON & OAK: Research & Technology Tree System

## Complete Design Document for Technology Progression (1850-2050+)

---

## 1. RESEARCH SYSTEM ANALYSIS: LESSONS FROM EXISTING GAMES

### 1.1 Civilization's Tech Tree Model

Civilization uses a **directed acyclic graph (DAG)** where each technology has one or more prerequisites, and unlocking higher-tier techs requires broad investment across the tree. Key design principles:

- **Tiered eras** act as natural progression gates; you cannot skip ahead without completing prerequisites from prior levels.
- **Wide trees prevent one optimal path** -- spreading across military, economic, and cultural branches means different strategies demand different tech orders.
- **Eureka moments** (Civ 6) give partial progress for completing related in-game actions, connecting research to gameplay rather than making it a passive meter.
- **Weakness for city builders**: Civ's tree is designed for 4X with military pressure. City builders lack external enemies, so the urgency driver must come from economic pressure, citizen demands, or environmental crises instead.

### 1.2 Anno 1800's Scholar System

Anno 1800 ties research directly to **population tiers and infrastructure**:

- **Scholars** (a population tier) generate research points passively by having their needs fulfilled. More scholars with better-met needs produce more research per minute.
- **Engineers** provide workforce for the Research Institute; more engineer workforce means faster completion.
- A single **Research Institute** (a monument-class building) handles all research, with a queue system.
- Research unlocks powerful abilities and items rather than basic buildings.
- **Relevance to Iron & Oak**: The "population tier generates research" model fits naturally with a city builder. Educated citizens producing research points makes thematic sense and ties research speed to city development.

### 1.3 Frostpunk's Tech Tree

Frostpunk creates **urgency-driven research** through survival pressure:

- The tree is visible in its entirety from the start, organized in tiers (0-5) with clear branching paths.
- Four tabs separate different city sectors (heating, exploration, resources, food).
- Tiers gate access -- you must unlock a tier globally before accessing advanced techs in any tab.
- **Time pressure** is the core driver: every wasted day researching the wrong tech can cascade into an unrecoverable death spiral.
- Research speed depends on the number of workshops and engineers assigned.
- **Relevance to Iron & Oak**: The "all techs visible from the start" approach gives players strategic clarity. The tier-gating system prevents overwhelming choices while allowing cross-category planning.

### 1.4 Stellaris's Semi-Random Weighted Draws

Stellaris replaces the traditional tree with a **"hand of cards" system**:

- Each research turn, the player picks from 3 randomly drawn tech options (per category: Physics, Society, Engineering).
- Techs have **weight values** affecting their draw probability. Systematically important techs have higher weights (1.5x-2x normal); rare techs have much lower weights (0.125x).
- Tier gates require researching a minimum number of techs from the previous tier before higher-tier cards can appear.
- Previously offered but unselected techs have their weight halved next draw, increasing variety.
- **Relevance to Iron & Oak**: A fully random system would frustrate city builder players who plan decades ahead. However, the concept of **weighted availability** -- where some techs become cheaper or appear as suggestions based on your city's characteristics -- is worth borrowing.

### 1.5 Workers & Resources: Soviet Republic

Workers & Resources uses a **workforce-based research model**:

- Research is conducted at **universities** (Technical, Medical, Political), each covering different tech categories.
- **Professors** drive research speed: up to 25 can be assigned per university, with more professors meaning faster completion.
- Professors assigned to research are **diverted from teaching**, creating a direct tension between educating your population and advancing technology.
- Research is measured in **workdays** (e.g., 800 workdays for a concrete study), making it feel grounded in labor reality.
- Techs unlock specific buildings and capabilities (prefab construction, monuments, pipeline systems).
- **Relevance to Iron & Oak**: The tension between "use professors for research or for education" is excellent game design. Iron & Oak should have a similar tradeoff: assigning university staff to research slows your education pipeline.

### 1.6 What Works Best for City Builders

Based on the analysis above, the ideal city builder research system should:

1. **Tie research generation to city development** (Anno model) -- educated population and research buildings produce research points.
2. **Show the full tree upfront** (Frostpunk model) -- city builders are planning games; players need to see what is coming.
3. **Use era-gating, not strict linear prerequisites** (Civ model adapted) -- techs belong to eras, and advancing an era unlocks new tiers.
4. **Create meaningful tradeoffs** (Workers & Resources model) -- research staff versus teaching staff; funding research versus other budget priorities.
5. **Avoid full randomness** (reject Stellaris model for primary tree) -- but use weighted suggestions for "bonus" research opportunities.
6. **Connect research to gameplay** -- techs should unlock visible, tangible changes: new building types, new transport modes, new overlays, new policies.

---

## 2. RESEARCH GENERATION MECHANICS

### 2.1 Research Points (RP) Generation

Research Points are generated through a combination of passive and active systems:

**Primary Sources:**

| Source | RP/month | Requirements | Notes |
|--------|----------|-------------|-------|
| University | 5 base + 1 per professor assigned to research | Built, staffed, powered | Professors on research duty do not teach |
| Research Lab | 10 base | Modern era+, staffed by university-educated workers | Dedicated research facility |
| Tech Campus | 20 base | Future era, requires fiber internet | End-game research accelerator |
| Educated Population | +0.01 per university-educated citizen | Citizens graduated from university | Passive "knowledge economy" bonus |
| Library | 1 base | Any era, small coverage radius | Cheap early-game RP trickle |

**Secondary Modifiers:**

| Factor | Modifier | Notes |
|--------|----------|-------|
| Research Funding slider | 0.5x to 2.0x | Budget allocation -- higher funding burns more money |
| Education Level (city average) | 0.5x (uneducated) to 1.5x (highly educated) | Rising tide lifts all boats |
| Research Specialization | +25% to focused category | Choosing a specialization path boosts related research |
| Inter-town Collaboration | +10% per connected research town | Regional play bonus |
| Private R&D (Industrial zones) | +2 RP per heavy industry tile with tech workers | Industry contributes to applied research |

### 2.2 Active vs. Passive Research

Iron & Oak uses a **hybrid model**:

- **Passive generation**: Research points accumulate each month based on your education infrastructure, educated population, and research buildings. You always make some progress.
- **Active allocation**: A **Research Funding slider** (0%-200% of base budget) lets players accelerate or throttle research. At 0%, only passive population-based RP trickles in. At 200%, you spend heavily but research twice as fast.
- **Queue system**: Players queue up to 3 techs. When one completes, the next begins automatically. This prevents micro-management while rewarding planning.
- **Eureka bonuses**: Certain in-game achievements grant 25-50% progress on related techs (e.g., building 10km of rail gives a boost to "Advanced Rail Signaling" research).

### 2.3 Research Speed Factors

```
effective_rp_per_month = (
    base_rp_from_buildings
    + population_rp_bonus
    + private_rd_bonus
) * funding_multiplier
  * education_level_multiplier
  * specialization_bonus
  * collaboration_bonus
  * era_scaling_factor
```

**Era scaling**: Later-era techs cost more RP, but your RP generation capacity also grows. A Frontier-era tech might cost 50 RP while a Future-era tech costs 500 RP, but by the Future era you might generate 80+ RP/month.

### 2.4 Collaboration (Regional Play)

When playing with multiple towns:

- Towns with universities can **share research progress** if connected by rail or telegraph/telephone/internet (era-appropriate communication).
- Each connected research town adds **+10% research speed** (stacking up to +30%).
- Towns can **specialize**: one town focuses on energy research while another focuses on transport. Completed techs apply region-wide.
- **Research agreements** between towns cost money but allow sharing specific in-progress research.

### 2.5 Private vs. Public Research

| Type | Source | Funds From | Unlocks |
|------|--------|-----------|---------|
| **Public Research** | Universities, Libraries, Research Labs | City budget (Research Funding slider) | All tech categories |
| **Private R&D** | Industrial zones with educated workers, Tech Campus | Automatic from industry profits (no direct budget cost) | Industrial, Computing, Transport tech only |

Private R&D is a bonus: if you have thriving high-tech industry, you get free research in applicable categories. This rewards building a knowledge economy without requiring explicit player action.

---

## 3. COMPLETE TECHNOLOGY TREE

### How to Read This Tree

- **Era**: When the tech becomes available (Frontier/Industrial/Postwar/Modern/Future)
- **Prerequisites**: Other techs that must be completed first. "None" means it is available as soon as its era begins.
- **Cost**: Relative RP cost (Low = 25-50, Medium = 75-125, High = 150-250, Very High = 300-500)
- **Unlocks**: Buildings, upgrades, policies, or transport modes the tech enables
- **Effect**: The gameplay impact of researching this tech

Technologies are numbered T001-T102 for cross-referencing prerequisites.

---

### CATEGORY 1: TRANSPORT TECHNOLOGY (15 techs)

#### Frontier Era

**T001 -- Cobblestone Paving**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Cobblestone road type
- Effect: Roads with 3x capacity of dirt paths. Faster travel, lower maintenance than dirt. Required foundation for all future road upgrades.

**T002 -- Basic Rail Engineering**
- Era: Frontier
- Prerequisites: None
- Cost: Medium
- Unlocks: Rail tracks, basic station (1 platform), steam locomotive, freight car
- Effect: Enables intercity rail connections and freight transport. Steam trains move goods 5x faster than horse carts. Unlocks the rail builder tool.

**T003 -- Horse-Drawn Transit**
- Era: Frontier
- Prerequisites: T001
- Cost: Low
- Unlocks: Horse-drawn omnibus, omnibus depot
- Effect: First public transit mode. Low capacity but cheap. Reduces foot traffic on main roads.

#### Industrial Era

**T004 -- Asphalt Roads**
- Era: Industrial
- Prerequisites: T001
- Cost: Medium
- Unlocks: Paved road, avenue (2-lane), one-way street
- Effect: Major road capacity upgrade. Enables automobile traffic. Avenues double throughput on main corridors.

**T005 -- Electric Tramway**
- Era: Industrial
- Prerequisites: T002, T020 (Basic Electrical Grid)
- Cost: Medium
- Unlocks: Tram tracks, tram stops, electric tram vehicles, tram depot
- Effect: First electric transit. Medium capacity, track-based. Shares road space. Higher upfront cost than buses but lower operating cost.

**T006 -- Advanced Rail Signaling**
- Era: Industrial
- Prerequisites: T002
- Cost: Medium
- Unlocks: Block signals, path signals, multi-platform stations (2-4 platforms), rail switches
- Effect: Enables complex rail junctions and higher-frequency service. Prevents collisions on shared track. Required for commuter rail.

#### Postwar Era

**T007 -- Highway Engineering**
- Era: Postwar
- Prerequisites: T004
- Cost: High
- Unlocks: Boulevard (4-lane), highway (6-lane), highway ramps, cloverleaf interchanges
- Effect: Massive road capacity for car-centric development. Highways enable suburban sprawl but can destroy neighborhoods if routed through existing districts. Land value drops adjacent to highways.

**T008 -- Motor Bus Network**
- Era: Postwar
- Prerequisites: T004
- Cost: Low
- Unlocks: Bus, bus stop, bus depot, route editor
- Effect: Flexible route-based transit. Lower capacity than trams but no track infrastructure needed. Can be rerouted without demolition.

**T009 -- Commercial Aviation**
- Era: Postwar
- Prerequisites: T007
- Cost: Very High
- Unlocks: Regional airport, commercial aircraft
- Effect: Enables air travel. Airport generates tourism and attracts high-wealth immigration. Requires large flat area. Noise pollution in surrounding tiles.

**T010 -- Commuter Rail**
- Era: Postwar
- Prerequisites: T006
- Cost: High
- Unlocks: Commuter rail station, commuter train, suburban rail lines
- Effect: High-capacity rail connecting suburbs to city center. Reduces highway congestion. Station areas see land value increases.

#### Modern Era

**T011 -- Metro/Subway System**
- Era: Modern
- Prerequisites: T010
- Cost: Very High
- Unlocks: Metro tunnel, metro station, metro train, metro line editor
- Effect: Highest-capacity urban transit. Underground construction avoids surface disruption. Extremely expensive but transforms commuting in dense areas.

**T012 -- High-Speed Rail**
- Era: Modern
- Prerequisites: T006, T011
- Cost: Very High
- Unlocks: High-speed rail track, high-speed station, bullet train
- Effect: Rapid intercity connections in regional play. Reduces inter-town travel time by 75%. Attracts commuters willing to live in smaller towns.

#### Future Era

**T013 -- Autonomous Vehicles**
- Era: Future
- Prerequisites: T007, T095 (Artificial Intelligence)
- Cost: Very High
- Unlocks: Autonomous vehicle overlay, smart traffic management, AV-only lanes
- Effect: Road capacity effectively increases 40% as AV coordination eliminates human driving inefficiency. Reduces accidents. Gradually replaces need for parking.

**T014 -- Maglev Transit**
- Era: Future
- Prerequisites: T012
- Cost: Very High
- Unlocks: Maglev track (elevated), maglev station, maglev train
- Effect: Ultra-high-speed, ultra-high-capacity transit. Elevated track avoids ground-level conflicts. The pinnacle of public transit technology.

**T015 -- Hyperloop / Vacuum Tube**
- Era: Future
- Prerequisites: T014
- Cost: Very High
- Unlocks: Hyperloop terminal, hyperloop tube
- Effect: Near-instant intercity travel in regional play. Functionally teleports commuters between connected towns. Extremely expensive, limited to point-to-point connections.

---

### CATEGORY 2: ENERGY TECHNOLOGY (12 techs)

#### Frontier Era

**T016 -- Steam Power**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Steam engine (for factories), coal-fired boiler
- Effect: Enables basic industrial production. Factories require steam power to operate. Produces moderate pollution.

**T017 -- Coal Power Plant**
- Era: Frontier
- Prerequisites: T016
- Cost: Medium
- Unlocks: Coal power plant, power line (basic), coal mine
- Effect: First centralized electricity. Powers a radius of buildings. High pollution, requires coal supply chain. Foundation of the power grid.

#### Industrial Era

**T018 -- Oil Refining**
- Era: Industrial
- Prerequisites: T016
- Cost: Medium
- Unlocks: Oil well, oil refinery, oil power plant, fuel distribution
- Effect: Oil-based power is more efficient than coal. Oil refinery produces fuel for vehicles. Enables petroleum-based economy. Pollution remains high.

**T019 -- Natural Gas Distribution**
- Era: Industrial
- Prerequisites: T018
- Cost: Medium
- Unlocks: Gas extraction, gas pipeline, gas power plant
- Effect: Cleaner than coal or oil (50% less pollution). Gas pipelines can serve as heating infrastructure. Moderate cost, reliable output.

**T020 -- Basic Electrical Grid**
- Era: Industrial
- Prerequisites: T017
- Cost: Medium
- Unlocks: Power substation, extended power lines, electrical grid overlay
- Effect: Power can be distributed city-wide rather than just near plants. Substations extend range. Required for electric trams and many industrial-era buildings.

#### Postwar Era

**T021 -- Nuclear Fission**
- Era: Postwar
- Prerequisites: T020
- Cost: Very High
- Unlocks: Nuclear power plant, uranium supply chain, nuclear waste storage
- Effect: Enormous power output with zero air pollution. Risk of meltdown event if safety budget is cut. Nuclear waste requires storage facility. NIMBY effect: land value drops near plant.

**T022 -- Hydroelectric Dam**
- Era: Postwar
- Prerequisites: T020
- Cost: High
- Unlocks: Hydroelectric dam (requires river placement)
- Effect: Clean, renewable, zero fuel cost. Requires specific river terrain. Limited by geography but excellent where available. Flood control bonus downstream.

#### Modern Era

**T023 -- Solar Power**
- Era: Modern
- Prerequisites: T020
- Cost: Medium
- Unlocks: Solar panel array, rooftop solar upgrade for buildings
- Effect: Clean energy, no fuel cost, no pollution. Output varies by season (lower in winter). Requires large land area for utility-scale. Rooftop version adds small power to existing buildings.

**T024 -- Wind Power**
- Era: Modern
- Prerequisites: T020
- Cost: Medium
- Unlocks: Wind turbine, offshore wind farm (coastal maps)
- Effect: Clean energy with variable output (weather-dependent). Best on hills and coastal areas. Noise pollution in small radius. Offshore version avoids land use issues.

**T025 -- Smart Grid**
- Era: Modern
- Prerequisites: T020, T092 (Internet Infrastructure)
- Cost: High
- Unlocks: Smart grid upgrade (city-wide), energy storage facility, demand management overlay
- Effect: Reduces power waste by 20%. Enables dynamic load balancing. Battery storage smooths renewable intermittency. Required for effective solar/wind at scale.

#### Future Era

**T026 -- Fusion Power**
- Era: Future
- Prerequisites: T021, T025
- Cost: Very High
- Unlocks: Fusion reactor
- Effect: Near-unlimited clean power. No waste, no pollution, no fuel supply chain. Extremely expensive to build but trivial operating cost. End-game energy solution.

**T027 -- Orbital Solar Collection**
- Era: Future
- Prerequisites: T023, T026
- Cost: Very High
- Unlocks: Orbital solar receiver station
- Effect: Space-based solar beamed to ground station. Consistent output regardless of weather or season. Removes the intermittency problem of ground solar entirely.

---

### CATEGORY 3: CONSTRUCTION TECHNOLOGY (10 techs)

#### Frontier Era

**T028 -- Timber Frame Construction**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Sawmill, wooden buildings (all zones), lumber supply chain
- Effect: Enables basic construction. Wooden buildings are cheap but have high fire risk and max 2 stories. Sawmill processes forest resources.

#### Industrial Era

**T029 -- Brick and Masonry**
- Era: Industrial
- Prerequisites: T028
- Cost: Medium
- Unlocks: Brickworks, brick buildings (up to 5 stories), masonry facades
- Effect: Stronger buildings with lower fire risk. Enables medium-density construction. Brick buildings have distinct Industrial-era aesthetic.

**T030 -- Structural Steel**
- Era: Industrial
- Prerequisites: T029, T016
- Cost: High
- Unlocks: Steel mill, steel-frame buildings (up to 8 stories), steel bridges, larger rail stations
- Effect: Enables taller buildings and longer bridge spans. Steel supply chain feeds construction. Required for skyscrapers and major infrastructure.

#### Postwar Era

**T031 -- Reinforced Concrete**
- Era: Postwar
- Prerequisites: T030
- Cost: Medium
- Unlocks: Concrete plant, concrete buildings (up to 15 stories), highway overpasses, parking garages
- Effect: Mass-produced construction material. Enables high-density zones, highway infrastructure, and brutalist architecture. Cheap but aesthetically polarizing.

**T032 -- Prefabricated Construction**
- Era: Postwar
- Prerequisites: T031
- Cost: Medium
- Unlocks: Prefab housing blocks, rapid construction mode (50% faster build time for residential)
- Effect: Dramatically speeds residential construction. Prefab housing is cheap and fast but has low land value and bland aesthetics. Useful for housing crises.

**T033 -- Earthquake Resistance**
- Era: Postwar
- Prerequisites: T031
- Cost: High
- Unlocks: Seismic retrofit upgrade for existing buildings, earthquake-resistant building code (policy)
- Effect: Reduces earthquake damage by 75%. Retrofit costs money per building. Building code prevents future earthquake losses but increases construction cost 10%.

#### Modern Era

**T034 -- Glass and Curtain Wall**
- Era: Modern
- Prerequisites: T030
- Cost: Medium
- Unlocks: Modern glass-facade buildings (up to 30 stories), skyscraper zone upgrade
- Effect: Enables modern skyline aesthetics. Skyscrapers dramatically increase density. High-wealth residential and office zones require this tech for maximum growth.

**T035 -- Green Building Standards**
- Era: Modern
- Prerequisites: T034, T023
- Cost: Medium
- Unlocks: Green building certification (policy), green roof upgrade, LEED-equivalent buildings
- Effect: Certified buildings use 30% less energy and 20% less water. Green roofs reduce heat island effect. Increases building cost 15% but raises land value.

#### Future Era

**T036 -- 3D-Printed Construction**
- Era: Future
- Prerequisites: T032, T095 (AI)
- Cost: High
- Unlocks: Construction printer facility, 3D-printed buildings
- Effect: Construction speed doubled. Construction cost reduced 30%. Requires specialized facility but dramatically reduces labor needs for building. Can construct complex geometries.

**T037 -- Arcology Design**
- Era: Future
- Prerequisites: T034, T036, T026
- Cost: Very High
- Unlocks: Arcology (mega-building housing 10,000+ citizens in a single structure)
- Effect: Self-contained city-within-a-city. Integrates residential, commercial, and services in one mega-structure. Extreme density, minimal land footprint. The pinnacle of construction tech.

---

### CATEGORY 4: COMMUNICATION TECHNOLOGY (9 techs)

#### Frontier Era

**T038 -- Telegraph Network**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Telegraph office, telegraph lines
- Effect: Enables inter-town communication in regional play. Boosts trade efficiency by 10% (faster price information). Required for rail signaling improvements.

#### Industrial Era

**T039 -- Telephone System**
- Era: Industrial
- Prerequisites: T038, T020
- Cost: Medium
- Unlocks: Telephone exchange, telephone lines (underground option)
- Effect: Improves commercial zone productivity by 10%. Enables faster emergency response (fire, police). Required for regional research collaboration.

**T040 -- Radio Broadcasting**
- Era: Industrial
- Prerequisites: T039
- Cost: Medium
- Unlocks: Radio station, radio tower
- Effect: City-wide entertainment boost (+5 happiness). Radio station generates small tourism/cultural income. Enables emergency broadcast system (faster disaster response).

#### Postwar Era

**T041 -- Television Broadcasting**
- Era: Postwar
- Prerequisites: T040
- Cost: Medium
- Unlocks: TV station, TV broadcast tower
- Effect: Major entertainment boost (+10 happiness). TV station is a landmark building. Commercial zones near TV station get advertising bonus (+15% revenue).

**T042 -- Satellite Communications**
- Era: Postwar
- Prerequisites: T041, T009
- Cost: High
- Unlocks: Satellite uplink facility, long-range communication
- Effect: Removes distance penalty for inter-town trade. Enables instant regional coordination. Required for future computing technologies.

#### Modern Era

**T043 -- Mobile Telecommunications**
- Era: Modern
- Prerequisites: T039, T042
- Cost: High
- Unlocks: Cell tower, mobile network overlay
- Effect: Citizens with mobile coverage gain +5 happiness. Commercial zones with coverage gain +10% productivity. Cell towers required every 20 tiles for coverage.

**T044 -- Internet Infrastructure**
- Era: Modern
- Prerequisites: T043
- Cost: High
- Unlocks: Internet exchange point, fiber optic network, ISP buildings
- Effect: Enables office zones and tech industry. Fiber-connected areas attract high-wealth residents and tech companies. Required for Smart Grid and many Future-era techs. See also T092 for fiber-specific unlock.

#### Future Era

**T045 -- 5G/6G Networks**
- Era: Future
- Prerequisites: T043, T044
- Cost: High
- Unlocks: 5G small cell network, ubiquitous connectivity overlay
- Effect: Enables autonomous vehicles and IoT smart city features. Eliminates need for cell towers (small cells on existing infrastructure). +15% productivity for all connected zones.

**T046 -- Quantum Communication**
- Era: Future
- Prerequisites: T045, T096 (Quantum Computing)
- Cost: Very High
- Unlocks: Quantum communication hub
- Effect: Unhackable city infrastructure. Eliminates cybersecurity events. +10% research speed from perfect information sharing. Prestige building.

---

### CATEGORY 5: AGRICULTURE TECHNOLOGY (9 techs)

#### Frontier Era

**T047 -- Crop Rotation**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Improved farmland (policy), crop rotation farming method
- Effect: Farm output increases 25%. Soil does not deplete over time. Basic but essential agricultural improvement.

**T048 -- Livestock Management**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Cattle ranch, dairy farm, livestock market
- Effect: Diversifies agricultural output. Ranches produce food and leather (trade goods). Livestock market serves as commercial hub for farming communities.

#### Industrial Era

**T049 -- Mechanical Agriculture**
- Era: Industrial
- Prerequisites: T047, T016
- Cost: Medium
- Unlocks: Tractor (farm upgrade), grain elevator, industrial mill
- Effect: Farm labor requirement drops 50%. Each farm tile produces 2x output. Displaced farm workers migrate to industrial jobs. Grain elevator enables bulk food storage.

**T050 -- Chemical Fertilizers**
- Era: Industrial
- Prerequisites: T049, T018
- Cost: Medium
- Unlocks: Fertilizer plant, fertilized farmland upgrade
- Effect: Farm output increases another 50%. Fertilizer plant requires oil/gas inputs. Risk: ground water pollution if overused (random event chance).

#### Postwar Era

**T051 -- Industrial Food Processing**
- Era: Postwar
- Prerequisites: T049
- Cost: Medium
- Unlocks: Food processing plant, cannery, cold storage warehouse
- Effect: Extends food supply chain. Processed food has higher value than raw crops. Cold storage prevents seasonal food shortages. Enables food export industry.

#### Modern Era

**T052 -- Genetically Modified Crops**
- Era: Modern
- Prerequisites: T050, T077 (Biotechnology)
- Cost: High
- Unlocks: GMO crop research facility, drought-resistant crops, pest-resistant crops
- Effect: Farm output increases 30%. Crops resist drought and pest events. Controversy: some citizens unhappy (happiness -3 for environmentally-minded demographics). BRANCHING CHOICE with T053.

**T053 -- Organic Farming Standards**
- Era: Modern
- Prerequisites: T050
- Cost: Medium
- Unlocks: Organic certification (policy), organic farms, farmers market
- Effect: Farm output decreases 20% but land value near farms increases. Organic produce has 2x trade value. Eliminates fertilizer pollution. Happiness +5 for nearby residents. BRANCHING CHOICE with T052.

#### Future Era

**T054 -- Vertical Farming**
- Era: Future
- Prerequisites: T052 or T053, T034
- Cost: High
- Unlocks: Vertical farm building (urban agriculture)
- Effect: Produces food inside city limits with zero land footprint. Operates year-round (no seasonal variation). Requires significant power and water. Enables food self-sufficiency in dense cities.

**T055 -- Automated Hydroponics**
- Era: Future
- Prerequisites: T054, T095 (AI)
- Cost: Very High
- Unlocks: Automated hydroponic complex
- Effect: Zero-labor food production. Combines vertical farming with full automation. No workers needed, just power and water. Produces 3x traditional farm output per tile.

---

### CATEGORY 6: INDUSTRIAL TECHNOLOGY (10 techs)

#### Frontier Era

**T056 -- Watermill Industry**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Watermill, basic workshop, tannery
- Effect: First industrial buildings. Water-powered manufacturing. Must be placed adjacent to rivers. Produces basic goods from raw materials.

#### Industrial Era

**T057 -- Assembly Line**
- Era: Industrial
- Prerequisites: T056, T016
- Cost: Medium
- Unlocks: Factory (large), assembly line production method
- Effect: Industrial output doubles. Factories become larger buildings requiring more workers. Enables mass production of goods. Foundation of industrial economy.

**T058 -- Chemical Industry**
- Era: Industrial
- Prerequisites: T057, T018
- Cost: High
- Unlocks: Chemical plant, pharmaceutical factory, plastics production
- Effect: New industrial sector producing high-value chemical goods. Chemical plant generates significant pollution. Pharmaceutical factory improves city health (+5% health coverage effectiveness).

#### Postwar Era

**T059 -- Automotive Manufacturing**
- Era: Postwar
- Prerequisites: T057, T030
- Cost: High
- Unlocks: Auto factory, car dealership
- Effect: Major employer (500+ jobs per factory). Generates high tax revenue. Citizens switch from transit to cars (can increase traffic). Creates demand for highways and parking.

**T060 -- Electronics Manufacturing**
- Era: Postwar
- Prerequisites: T057, T020
- Cost: High
- Unlocks: Electronics factory, consumer electronics store
- Effect: High-value manufacturing sector. Requires educated workers. Low pollution compared to heavy industry. Foundation for computing technology chain.

#### Modern Era

**T061 -- Industrial Robotics**
- Era: Modern
- Prerequisites: T060, T092 (Internet Infrastructure)
- Cost: High
- Unlocks: Robotic assembly plant, factory automation upgrade
- Effect: Factories require 40% fewer workers but produce 50% more. Displaced workers need retraining. Increases industrial tax revenue per factory. BRANCHING CHOICE with T062.

**T062 -- Lean Manufacturing**
- Era: Modern
- Prerequisites: T057
- Cost: Medium
- Unlocks: Lean production upgrade, waste reduction policy
- Effect: Factories produce 25% less pollution and 20% more output with same workers. No job losses. Lower improvement ceiling than robotics but socially stable. BRANCHING CHOICE with T061.

**T063 -- Clean Industry Standards**
- Era: Modern
- Prerequisites: T062 or T061
- Cost: High
- Unlocks: Clean industry certification, industrial pollution scrubbers, green factory upgrade
- Effect: Industrial pollution reduced by 60%. Required to maintain industry in residential-adjacent areas without happiness penalties. Increases industrial operating cost 15%.

#### Future Era

**T064 -- AI Manufacturing**
- Era: Future
- Prerequisites: T061, T095 (AI)
- Cost: Very High
- Unlocks: Fully automated factory, dark factory (lights-out manufacturing)
- Effect: Factories require zero workers. Maximum output with zero labor cost. Massive unemployment pressure if not paired with social safety net techs. Extremely high tax revenue per factory.

**T065 -- Nanomanufacturing**
- Era: Future
- Prerequisites: T064, T096 (Quantum Computing)
- Cost: Very High
- Unlocks: Nanotech fabrication lab
- Effect: Produces ultra-high-value goods with minimal raw material input. Single facility replaces entire supply chain for some goods. End-game industry pinnacle.

---

### CATEGORY 7: MEDICAL TECHNOLOGY (9 techs)

#### Frontier Era

**T066 -- Basic Sanitation**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Outhouses, basic water well, waste collection route
- Effect: Reduces disease risk by 30%. Prevents cholera outbreaks. Foundation for all public health. Without this, population growth is capped by disease mortality.

**T067 -- General Hospital**
- Era: Frontier
- Prerequisites: T066
- Cost: Medium
- Unlocks: Hospital (basic), doctor's office
- Effect: Health coverage for surrounding area. Reduces death rate. Doctor's office is small/cheap for rural coverage. Hospital handles emergencies and serious illness.

#### Industrial Era

**T068 -- Vaccination Programs**
- Era: Industrial
- Prerequisites: T067
- Cost: Medium
- Unlocks: Vaccination campaign (policy), public health office
- Effect: Eliminates epidemic events (smallpox, cholera). Child mortality drops dramatically. Population growth rate increases 20%. Public health office provides ongoing disease prevention.

**T069 -- Modern Surgery**
- Era: Industrial
- Prerequisites: T067
- Cost: Medium
- Unlocks: Surgery wing (hospital upgrade), ambulance service
- Effect: Hospital effectiveness doubles. Ambulances respond to emergencies faster. Workplace accident deaths reduced 50%. Requires educated (trained) staff.

#### Postwar Era

**T070 -- Antibiotics and Pharmacology**
- Era: Postwar
- Prerequisites: T068, T058
- Cost: Medium
- Unlocks: Pharmacy, advanced hospital upgrade, pharmaceutical industry
- Effect: Life expectancy increases 10 years. Disease recovery time halved. Pharmaceutical industry generates export revenue. Hospital operating cost increases (drug costs).

**T071 -- Emergency Medical Services**
- Era: Postwar
- Prerequisites: T069, T008
- Cost: Medium
- Unlocks: Paramedic station, emergency dispatch, helicopter medevac (with T009)
- Effect: Dramatically reduces death from accidents and acute illness. Coverage radius extends beyond hospital. Required for large city health coverage.

#### Modern Era

**T072 -- Advanced Diagnostics**
- Era: Modern
- Prerequisites: T070, T092
- Cost: High
- Unlocks: Medical imaging center, diagnostic lab, telemedicine
- Effect: Early disease detection reduces chronic illness by 30%. Telemedicine extends health coverage to underserved areas. Hospital effectiveness increases 25%.

**T073 -- Specialized Medicine**
- Era: Modern
- Prerequisites: T072
- Cost: High
- Unlocks: Specialized hospital (children's, cardiac, cancer), medical research center
- Effect: Specialized hospitals have 2x effectiveness in their specialty. Medical research center generates RP in the medical category. Attracts medical tourism (high-wealth visitors).

#### Future Era

**T074 -- Genetic Medicine**
- Era: Future
- Prerequisites: T073, T077 (Biotech)
- Cost: Very High
- Unlocks: Gene therapy clinic, personalized medicine program
- Effect: Life expectancy increases 15 years. Chronic disease nearly eliminated. Elderly population remains productive longer (retirement age effectively rises). Very high operating cost.

---

### CATEGORY 8: ENVIRONMENTAL TECHNOLOGY (10 techs)

#### Frontier Era

**T075 -- Basic Sewage System**
- Era: Frontier
- Prerequisites: T066
- Cost: Medium
- Unlocks: Sewer pipes, basic sewage outfall
- Effect: Removes waste from city streets. Prevents ground contamination in residential areas. Outfall dumps raw sewage into waterways (causes downstream pollution).

#### Industrial Era

**T076 -- Sewage Treatment**
- Era: Industrial
- Prerequisites: T075
- Cost: Medium
- Unlocks: Sewage treatment plant
- Effect: Treats sewage before discharge. Eliminates water pollution from waste. Required for clean drinking water in growing cities. Treatment plant is a NIMBY facility (reduces nearby land value).

#### Postwar Era

**T077 -- Biotechnology**
- Era: Postwar
- Prerequisites: T068, T058
- Cost: High
- Unlocks: Biotech research lab, bioremediation program
- Effect: Enables biological cleanup of contaminated land. Bioremediation slowly removes ground pollution from former industrial sites. Foundation for GMO and genetic medicine techs.

**T078 -- Pollution Control Regulations**
- Era: Postwar
- Prerequisites: T076
- Cost: Medium
- Unlocks: Pollution control policy, emissions monitoring station, smokestack scrubber upgrade
- Effect: Industrial air pollution reduced 40%. Scrubber upgrade can be applied to existing factories. Increases industrial operating cost 10%. Happiness +5 in formerly polluted areas.

**T079 -- Waste Management**
- Era: Postwar
- Prerequisites: T076
- Cost: Medium
- Unlocks: Sanitary landfill, garbage truck fleet, waste collection routes
- Effect: Replaces open dumps with managed landfills. Garbage truck coverage prevents rat infestations and disease. Landfill has finite capacity (must eventually upgrade or add new sites).

#### Modern Era

**T080 -- Recycling Programs**
- Era: Modern
- Prerequisites: T079
- Cost: Medium
- Unlocks: Recycling center, curbside recycling policy, materials recovery facility
- Effect: Reduces landfill usage by 40%. Recycling center produces raw materials from waste (partial supply chain input). Environmental happiness bonus +5.

**T081 -- Carbon Capture**
- Era: Modern
- Prerequisites: T078, T058
- Cost: Very High
- Unlocks: Carbon capture facility (attaches to fossil fuel plants), carbon offset program
- Effect: Reduces CO2 emissions from fossil fuel plants by 80%. Allows continued use of coal/gas/oil power with minimal environmental penalty. Expensive but lets players keep existing infrastructure.

**T082 -- Urban Green Space Planning**
- Era: Modern
- Prerequisites: None (Modern era gate only)
- Cost: Low
- Unlocks: City park expansion, urban forest, greenway corridor, community garden
- Effect: Green spaces reduce urban heat island effect. Parks within 10 tiles increase land value 15% and happiness +5. Greenways connect parks for pedestrian/bike corridors.

#### Future Era

**T083 -- Ecosystem Restoration**
- Era: Future
- Prerequisites: T077, T082
- Cost: High
- Unlocks: Ecosystem restoration zone, wildlife corridor, rewilding program
- Effect: Converts abandoned industrial land into natural habitat. Wildlife corridors connect green spaces. Tourism bonus from restored nature. Fully removes all historical ground pollution in the zone.

**T084 -- Atmospheric Engineering**
- Era: Future
- Prerequisites: T081, T026
- Cost: Very High
- Unlocks: Atmospheric processor, weather modification station
- Effect: Can reduce seasonal weather severity (milder winters, cooler summers). Reduces weather-related damage events by 50%. Prestige technology with city-wide quality-of-life improvement.

---

### CATEGORY 9: SOCIAL & CIVIC TECHNOLOGY (11 techs)

#### Frontier Era

**T085 -- Public Education Act**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Elementary school, school bus (later eras), truancy enforcement
- Effect: Children become educated over 12 game-months. Educated workers are more productive (+10% commercial/industrial output). Foundation of the education pipeline.

**T086 -- Basic Zoning Laws**
- Era: Frontier
- Prerequisites: None
- Cost: Low
- Unlocks: Zoning tool (R/C/I separation), building codes (reduces fire risk 20%)
- Effect: Enables the core zoning mechanic. Without this, buildings grow randomly and incompatibly. Fire codes reduce wooden building fire risk.

#### Industrial Era

**T087 -- Professional Fire Service**
- Era: Industrial
- Prerequisites: T086
- Cost: Medium
- Unlocks: Fire station (upgraded), fire hydrant network, fire engine
- Effect: Fire response time halved. Fire hydrants extend coverage. Fire engine can respond to fires outside station radius. Dramatically reduces fire destruction in wooden districts.

**T088 -- Professional Police Force**
- Era: Industrial
- Prerequisites: T086
- Cost: Medium
- Unlocks: Police station (upgraded), patrol routes, jail
- Effect: Crime rate reduced in covered areas. Patrol routes extend coverage beyond station radius. Jail prevents repeat offenders. Required for safe high-density development.

**T089 -- Secondary Education**
- Era: Industrial
- Prerequisites: T085
- Cost: Medium
- Unlocks: High school, vocational school
- Effect: Produces skilled workers for industrial and commercial jobs. Vocational school produces specialized factory workers (+20% industrial efficiency). 24 game-month education pipeline.

#### Postwar Era

**T090 -- Higher Education**
- Era: Postwar
- Prerequisites: T089
- Cost: High
- Unlocks: University, research capability, student housing
- Effect: Produces university-educated citizens. University generates base research points. University-educated citizens fill office/tech jobs and contribute passive RP. Student housing adds temporary population.

**T091 -- Urban Planning Department**
- Era: Postwar
- Prerequisites: T086
- Cost: Medium
- Unlocks: City planning office, zoning variance tool, historic district designation, mixed-use zoning
- Effect: Mixed-use zones combine R+C for walkability bonus. Historic district designation preserves old buildings (tourism +25%, no demolition allowed). Variance tool allows exceptions to zoning rules.

#### Modern Era

**T092 -- Internet Infrastructure**
- Era: Modern
- Prerequisites: T044
- Cost: High
- Unlocks: Fiber optic network, data center, tech startup incubator
- Effect: This is the detailed fiber/data unlock beyond T044's basic internet. Data centers are high-value commercial buildings. Tech incubator spawns high-wealth office demand. Required for smart city techs.

**T093 -- Public Welfare System**
- Era: Modern
- Prerequisites: T090
- Cost: Medium
- Unlocks: Welfare office, unemployment benefits (policy), public housing program, homeless shelter
- Effect: Prevents emigration during economic downturns. Unemployed citizens receive benefits instead of leaving. Public housing provides low-cost residential. Reduces crime from poverty. Increases city budget expense.

#### Future Era

**T094 -- Smart City Platform**
- Era: Future
- Prerequisites: T092, T025 (Smart Grid), T095 (AI)
- Cost: Very High
- Unlocks: Smart city operations center, IoT sensor network, predictive analytics overlay
- Effect: City services operate 25% more efficiently. Predictive analytics warns of problems before they manifest (traffic jams, power brownouts, crime spikes). All overlays gain predictive mode showing likely future state.

---

### CATEGORY 10: COMPUTING & DIGITAL TECHNOLOGY (8 techs)

(Note: T044 Internet Infrastructure and T092 Fiber/Data are listed under Communication and Social categories respectively, but are cross-referenced here.)

#### Postwar Era

**T095a -- Mainframe Computing**
- Era: Postwar
- Prerequisites: T060 (Electronics Manufacturing)
- Cost: Medium
- Unlocks: Computer center (government building), automated tax collection
- Effect: City administration cost reduced 20%. Tax collection efficiency increases (less tax evasion). Enables digital record-keeping. Foundation for computing chain.

#### Modern Era

**T095b -- Personal Computing**
- Era: Modern
- Prerequisites: T095a, T060
- Cost: Medium
- Unlocks: Computer store (commercial), office zone productivity boost, home office policy
- Effect: Office zones produce 25% more output. Home office policy reduces commute demand by 10%. Computer stores generate high commercial revenue.

**T095 -- Artificial Intelligence**
- Era: Modern (late)
- Prerequisites: T095b, T092
- Cost: Very High
- Unlocks: AI research lab, machine learning applications, predictive city management
- Effect: Unlocks numerous Future-era techs (autonomous vehicles, AI manufacturing, smart city). AI research lab generates 15 RP/month. Predictive management reduces all service costs 10%.

**T096 -- Quantum Computing**
- Era: Future
- Prerequisites: T095
- Cost: Very High
- Unlocks: Quantum computing center
- Effect: Research speed doubled for all categories. Enables quantum communication tech. Generates 25 RP/month. Single most powerful research accelerator in the game.

**T097 -- Digital Governance**
- Era: Modern
- Prerequisites: T095b, T092
- Cost: Medium
- Unlocks: E-government portal, digital permits, online civic engagement
- Effect: Administration costs reduced 30%. Building permit processing instant (no construction delay). Citizen satisfaction with government +10. Reduces bureaucracy overhead.

**T098 -- Cybersecurity Infrastructure**
- Era: Modern
- Prerequisites: T092, T095b
- Cost: Medium
- Unlocks: Cybersecurity operations center, firewall infrastructure
- Effect: Prevents cyberattack events (which can disable power grid, traffic systems, or financial systems). Without this, digitally-dependent cities are vulnerable to random cyber events.

**T099 -- Blockchain Governance**
- Era: Future
- Prerequisites: T097, T096
- Cost: High
- Unlocks: Transparent ledger system, decentralized city services
- Effect: Corruption eliminated (all tax revenue collected with zero leakage). City services operate with full transparency. +10 citizen trust/happiness. Reduces administration costs a further 15%.

**T100 -- Autonomous Systems Integration**
- Era: Future
- Prerequisites: T095, T013 (Autonomous Vehicles), T094 (Smart City)
- Cost: Very High
- Unlocks: Autonomous everything overlay -- self-driving transit, drone delivery, robotic maintenance
- Effect: All city services operate with 50% fewer workers needed. Transit runs autonomously. Road maintenance automated. The culmination of the computing tree.

---

### SUPPLEMENTARY TECHS (2 additional)

**T101 -- Space Industry**
- Era: Future
- Prerequisites: T009 (Aviation), T064 (AI Manufacturing)
- Cost: Very High
- Unlocks: Spaceport, space tourism facility, satellite launch pad
- Effect: Prestige mega-project. Generates massive tourism revenue. Spaceport is a wonder-class building. Enables T027 (Orbital Solar).

**T102 -- Universal Basic Income**
- Era: Future
- Prerequisites: T093 (Welfare), T064 (AI Manufacturing), T099 (Blockchain Governance)
- Cost: Very High
- Unlocks: UBI policy, post-scarcity economy mode
- Effect: All citizens receive basic income. Eliminates poverty-driven crime and emigration. Massive budget cost but eliminates unemployment unhappiness entirely. Combined with AI manufacturing, enables a post-labor economy where city revenue comes from automated industry.

---

**Total: 102 technologies across 10 categories and 5 eras.**

Era distribution:
| Era | Tech Count |
|-----|-----------|
| Frontier | 18 |
| Industrial | 22 |
| Postwar | 22 |
| Modern | 24 |
| Future | 16 |

---

## 4. BRANCHING AND CHOICE

### 4.1 Meaningful Decision Points

The tech tree contains several deliberate **branching choices** where players must commit to a direction or accept tradeoffs:

#### Transport Philosophy (Postwar Era - Critical Fork)
- **Path A: Highway-Centric** (T007 Highway Engineering prioritized)
  - Leads to: suburban sprawl, car dealerships, parking garages, autonomous vehicles
  - Pros: Cheap per-citizen, fast to build, citizens happy with freedom
  - Cons: Pollution, congestion at scale, land consumption, oil dependency

- **Path B: Transit-Centric** (T010 Commuter Rail + T011 Metro prioritized)
  - Leads to: dense urban core, walkable neighborhoods, high land value downtown
  - Pros: High capacity, low pollution, land value boost near stations
  - Cons: Extremely expensive upfront, long construction time, requires density to be viable

Players can research both paths, but **budget pressure forces prioritization**. Building both a highway network and a metro system simultaneously is prohibitively expensive. This mirrors real urban planning dilemmas.

#### Energy Future (Modern Era - Critical Fork)
- **Path A: Nuclear** (T021 Nuclear Fission)
  - Leads to: massive reliable baseload power, fusion reactor end-game
  - Pros: Enormous output, zero air pollution, enables industrial scale
  - Cons: Meltdown risk, NIMBY effect, nuclear waste storage needed, very expensive

- **Path B: Renewables** (T023 Solar + T024 Wind)
  - Leads to: distributed clean power, smart grid, orbital solar
  - Pros: Zero fuel cost, zero pollution, politically popular, scalable
  - Cons: Intermittent (need storage), large land area, weather-dependent output

Both paths eventually converge at T026 Fusion Power, but the journey shapes your city's character for 50+ game-years.

#### Agriculture Philosophy (Modern Era)
- **T052 GMO Crops** vs **T053 Organic Farming** (soft exclusivity)
  - Both can be researched, but implementing both policies simultaneously creates citizen backlash (environmentalists vs. pragmatists)
  - GMO: Maximum food output, minimum land use, some citizens unhappy
  - Organic: Premium trade value, land value boost, lower output

#### Industrial Philosophy (Modern Era)
- **T061 Industrial Robotics** vs **T062 Lean Manufacturing**
  - Robotics: Higher output, fewer jobs, unemployment pressure
  - Lean: Moderate improvement, no job losses, socially stable
  - Both can be researched, but the first one you implement shapes your industrial workforce for a generation

### 4.2 Specialization Paths

Players naturally gravitate toward one of four city identities based on their tech choices:

| Specialization | Key Techs | City Character |
|---------------|-----------|----------------|
| **Industrial Powerhouse** | T057, T059, T061, T064 | Factories, manufacturing, exports, blue-collar workforce. High revenue but pollution challenges. |
| **Green City** | T053, T023, T024, T082, T083 | Renewables, organic farming, parks, low pollution. High happiness but expensive to maintain. |
| **Tech Hub** | T092, T095, T096, T094, T097 | Data centers, AI, digital governance, startups. High-wealth citizens, high RP generation, requires fiber everywhere. |
| **Cultural Capital** | T091 (Historic Districts), T082, T073 (Medical Tourism), T041 | Tourism, historic preservation, entertainment, medical tourism. High land value, tourist revenue, slower industrial growth. |

### 4.3 Preventing One Optimal Path

Several design mechanisms prevent a single dominant strategy:

1. **Budget pressure**: You cannot research everything. Funding tradeoffs force prioritization.
2. **Map-dependent advantages**: Coastal maps favor wind/port tech; river maps favor hydro; mountain maps make highways expensive but rail cheap; plains maps favor sprawl.
3. **Population demands**: Citizens in different wealth tiers demand different technologies. High-wealth citizens demand fiber internet and clean air; low-wealth citizens demand affordable housing and jobs.
4. **Era pacing variation**: Players who rush through eras miss out on fully exploiting earlier-era synergies. A well-developed Industrial city with mature rail and tram networks may outperform a hastily-advanced Modern city with underdeveloped infrastructure.
5. **Regional specialization**: In regional play, different towns benefit from different tech focuses. One town becomes the industrial base, another the research hub, a third the residential suburb.
6. **Random events** favor different preparations: drought rewards water tech investment; economic recession rewards diversified economies; blizzards reward robust heating infrastructure.

---

## 5. ERA TRANSITION THROUGH TECH

### 5.1 Era Milestone Requirements

Era transitions require BOTH population milestones AND tech milestones:

| Era Transition | Population Req | Tech Requirements | Additional Condition |
|---------------|---------------|-------------------|---------------------|
| Frontier -> Industrial | 500+ pop | T017 (Coal Power) + T002 (Basic Rail) + T085 (Public Education) | At least one factory built |
| Industrial -> Postwar | 10,000+ pop | T020 (Electrical Grid) + T004 (Asphalt Roads) + T089 (Secondary Education) | City budget positive for 12 months |
| Postwar -> Modern | 50,000+ pop | T090 (Higher Education) + T044 (Internet) + any 2 of: T011/T021/T060 | University built and operational |
| Modern -> Future | 100,000+ pop | T095 (AI) + T025 (Smart Grid) + T092 (Internet Infra) | 80%+ citizens have university education |

### 5.2 Staying in Earlier Eras

Players CAN stay in earlier eras indefinitely. Era advancement is **never forced**. Benefits of delaying:

- **Frontier lingering**: Maximally develop agriculture and rail before industrialization. Build a comprehensive rail network while land is cheap. Establish trade routes.
- **Industrial lingering**: Build out complete tram networks and factory supply chains before car-centric pressure arrives. Mature industrial economy generates strong revenue.
- **Postwar lingering**: Fully build highway OR transit infrastructure before digital economy demands hit. Establish suburban patterns that will persist.
- **Modern lingering**: Perfect your green energy and digital infrastructure before Future-era automation disrupts your labor market.

**Gameplay incentive to advance**: Later eras unlock higher population caps, more efficient buildings, and higher revenue. A Frontier-era city caps around 5,000 population because it lacks the infrastructure to support more.

### 5.3 Anachronistic Technology

Yes, a city CAN have mixed-era technology. This is by design and reflects real-world cities:

- **Infrastructure layers**: Your Frontier-era cobblestone district might sit next to a Modern glass tower district. The cobblestone area has charm (tourism) but capacity constraints (traffic).
- **Per-district eras**: Different parts of the city can effectively be in different eras. The old town center might still use gas lamps while the new tech park has fiber internet.
- **Upgrade paths**: Earlier infrastructure can be upgraded without full demolition. Cobblestone roads can be paved. Coal plants can be retrofitted with scrubbers. Wooden buildings can be reinforced.
- **Preservation bonus**: Deliberately maintaining historical areas provides tourism and cultural bonuses. A player might intentionally keep their Frontier main street while building a Future-era tech campus on the outskirts.
- **Constraint propagation**: Old infrastructure constrains new development. Narrow Frontier-era streets cannot accommodate bus routes. Industrial-era rail gauge may limit high-speed rail. This creates organic urban planning challenges.

---

## 6. RESEARCH UI DESIGN

### 6.1 Tech Tree View

The tech tree is displayed as a **scrollable graph** organized left-to-right by era, with vertical grouping by category:

```
[FRONTIER]     [INDUSTRIAL]     [POSTWAR]      [MODERN]       [FUTURE]
   |               |               |              |              |
 Transport ────────────────────────────────────────────────────────>
   |               |               |              |              |
 Energy    ────────────────────────────────────────────────────────>
   |               |               |              |              |
 Construction ─────────────────────────────────────────────────────>
   ...
```

Each tech node shows:
- Icon (pixel art, era-appropriate aesthetic)
- Name
- RP cost (shown as a progress bar when researching)
- Prerequisite lines (white for met, grey for unmet)
- Status: locked (grey), available (bright outline), researching (animated), completed (filled)

### 6.2 Research Panel

A persistent UI element showing:
- Current research: name, progress bar, estimated completion (months)
- Research queue (up to 3 items)
- Total RP/month generation with breakdown
- Research Funding slider (0%-200%)
- "Eureka" notifications for bonus progress

### 6.3 Advisor Integration

The Research Advisor (a university professor character) provides:
- Suggestions based on current city needs ("Your traffic is congested -- consider researching Highway Engineering or Metro System")
- Warnings about tech dependencies ("You will need Electrical Grid before you can research Electric Tramway")
- Era transition readiness ("You meet 2 of 3 tech requirements for the Modern Era")

---

## 7. TECH TREE DEPENDENCY GRAPH (SUMMARY)

### Critical Paths (longest dependency chains)

**Transport chain**: T001 -> T004 -> T007 -> T013 (or T001 -> T002 -> T006 -> T010 -> T011 -> T012 -> T014 -> T015)

**Energy chain**: T016 -> T017 -> T020 -> T021 -> T026 (or T020 -> T023/T024 -> T025 -> T026)

**Computing chain**: T060 -> T095a -> T095b -> T095 -> T096 -> T100

**Medical chain**: T066 -> T067 -> T068 -> T070 -> T072 -> T073 -> T074

**Industrial chain**: T056 -> T057 -> T060 -> T061 -> T064 -> T065

### Cross-Category Dependencies

These techs require research from MULTIPLE categories, creating interesting cross-pollination:

| Tech | Requires From |
|------|--------------|
| T005 Electric Tramway | Transport + Energy |
| T013 Autonomous Vehicles | Transport + Computing |
| T025 Smart Grid | Energy + Communication |
| T052 GMO Crops | Agriculture + Medical/Environmental |
| T061 Industrial Robotics | Industrial + Communication |
| T094 Smart City | Civic + Energy + Computing |
| T064 AI Manufacturing | Industrial + Computing |
| T074 Genetic Medicine | Medical + Environmental (Biotech) |
| T037 Arcology | Construction + Energy + Computing |
| T102 UBI | Civic + Industrial + Computing |

These cross-category dependencies ensure players cannot hyperfocus on a single research category and must develop their city broadly.

---

## 8. BALANCING GUIDELINES

### Research Cost Scaling

| Era | Low | Medium | High | Very High |
|-----|-----|--------|------|-----------|
| Frontier | 25 RP | 50 RP | 75 RP | -- |
| Industrial | 40 RP | 80 RP | 130 RP | 200 RP |
| Postwar | 60 RP | 120 RP | 200 RP | 350 RP |
| Modern | 80 RP | 160 RP | 280 RP | 450 RP |
| Future | 100 RP | 200 RP | 350 RP | 500 RP |

### Expected RP Generation by Era

| Era | Typical RP/month | Time to Research "Medium" |
|-----|-----------------|--------------------------|
| Frontier | 5-10 | 5-10 months |
| Industrial | 15-30 | 3-5 months |
| Postwar | 40-80 | 2-3 months |
| Modern | 80-160 | 1-2 months |
| Future | 150-300 | 1-2 months |

The ratio of research cost to generation rate should keep individual techs feeling achievable (1-6 months each) while making the TOTAL tree feel like a long-term investment spanning the full game timeline.

### Eureka Bonus Triggers (Examples)

| Tech | Eureka Condition | Bonus |
|------|-----------------|-------|
| T002 Basic Rail | Build 20 road tiles | 25% progress |
| T007 Highway Engineering | Reach 500 vehicles on roads | 30% progress |
| T011 Metro System | Have 10,000+ transit riders per day | 25% progress |
| T021 Nuclear Fission | Generate 10,000 MW total power lifetime | 30% progress |
| T095 Artificial Intelligence | Have 3+ data centers operational | 25% progress |
| T037 Arcology | Reach 50-story building height | 40% progress |
| T102 UBI | Achieve 0% poverty rate for 12 months | 50% progress |

---

*Document version: 1.0*
*Created: March 2026*
*For: Iron & Oak City Builder GDD*
*Total technologies: 102*
*Categories: 10*
*Eras: 5 (Frontier, Industrial, Postwar, Modern, Future)*
