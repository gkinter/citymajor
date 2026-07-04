# IRON & OAK: City Builder Game Design Document v2.0

> **Historical — pre-web pivot.** See [WEB_V1_SCOPE.md](WEB_V1_SCOPE.md) and [CLAUDE.md](../../CLAUDE.md).

## A Deep Retro City Builder for Urban Planning Enthusiasts

---

## 1. GAME IDENTITY

### Concept
**Iron & Oak** is a deep, retro-styled city builder that combines the strategic depth of SimCity 4 and Transport Tycoon with the visual charm of Stardew Valley. Players build a town from a single crossroads into a thriving metropolis across five eras (1850-2050+), managing intricate transport networks, complex industry chains, demographic lifecycles, and a full research tree -- while watching their decisions cascade through interconnected simulation systems.

### The Hook: "What SimCity 4 Would Be If Made Today With Pixel Art"
No retro city builder has nailed the intersection of:
1. **Genuine simulation depth** (budget pressure, cascading consequences, transport optimization)
2. **Charming pixel art identity** (not "cheap-looking" -- premium, Stardew-tier aesthetic)
3. **Meaningful endgame** (emergent crises, era progression, regional play)
4. **Accessible without being simple** (readable feedback, intuitive UI, gentle learning curve)

### Unique Differentiators
- **Era Progression**: Your city evolves from 1850s frontier town through industrial revolution, postwar expansion, and beyond. Infrastructure decisions from earlier eras constrain and enable later ones. That coal plant you built in 1890? It's polluting your 2020 tech district.
- **Regional Play**: Build 3-5 interconnected towns on a shared map. Towns trade resources, compete for migrants, and share transport. One town can specialize in heavy industry while another becomes a university town.
- **Per-City Raw Materials**: Every map generates unique resource deposits. Your city might sit on iron and coal but have no oil -- forcing trade dependencies with neighboring towns. Geography shapes destiny.
- **Deep Demographics**: Citizens age, go to school, get jobs, retire, and die. Cultural profiles shift as neighborhoods gentrify or decline. NIMBYism, brain drain, and aging populations create organic challenges.
- **102 Technologies**: A full research tree spanning 10 categories across 5 eras, with branching decisions that permanently shape your city's character.
- **Visible Consequences**: Every decision has readable feedback. Build a highway through a neighborhood? Watch property values drop, see moving trucks, watch buildings deteriorate.

### Target Audience
- SimCity 4 nostalgists who want depth in pixel art
- Transport Tycoon fans who also want city management
- City builder enthusiasts frustrated by shallow indie options
- Pixel art lovers who want something with substance
- Strategy gamers looking for long-session depth

### Platform & Price
- **Platform**: PC (Steam) primary. Mac secondary.
- **Price**: $14.99 launch, $19.99 post-1.0
- **Release Strategy**: Early Access with layered content updates

---

## 2. VISUAL IDENTITY

### Art Style: "Stardew Valley Meets SimCity 2000"
- **Isometric pixel art** at 32x16px tile base (isometric diamond)
- **4 distinct seasonal palettes** that transform the entire city
- **Time-of-day lighting**: dawn golden hour, bright midday, warm sunset, blue-tinted night with lit windows
- **Era-appropriate architecture**: wooden frontier buildings -> brick Victorian -> Art Deco -> postwar concrete -> modern glass
- **Weather particles**: rain with puddle reflections, falling snow that accumulates, autumn leaves, summer heat shimmer
- **Animated citizens**: tiny pixel people walking streets, waiting at bus stops, driving cars
- **Smoke, steam, and glow effects**: factory smokestacks, train steam, neon signs at night

### Color Philosophy
| Season | Palette |
|--------|---------|
| Spring | Fresh greens, cherry blossom pink, rain-grey skies |
| Summer | Vivid greens, golden sunlight, deep blue water |
| Autumn | Amber, burnt orange, russet red, grey skies |
| Winter | White snow, cool blues, warm yellow window-glow |

### UI Style
- Chunky pixel-art panels with wood/metal textures matching current era
- Retro CRT-inspired menu borders (optional scanline filter)
- "Press Start 2P" style font for headers, clean pixel font for data
- Satisfying click sounds, paper shuffle for budget screens
- Typewriter-style text for advisors and news ticker

---

## 3. CORE LOOP & MAP SYSTEM

### 3.1 Core Loop

```
PLACE infrastructure (roads, zones, services)
    |
SIMULATE population growth, economy, traffic
    |
OBSERVE consequences (overlays, advisors, visual feedback)
    |
OPTIMIZE (redesign roads, adjust taxes, add transit)
    |
RESEARCH new technologies to unlock capabilities
    |
ADVANCE to next era when milestones met
    |
REPEAT with compounding complexity
```

### 3.2 Grid & Map System

- **Grid size**: 256x256 tiles (expandable to 512x512)
- **Tile size**: 32x16px isometric
- **Map types**: Valley, Coastal, Mountain, Plains, River Delta (each with strategic implications)
- **Terrain**: Grass, forest, water, hills (3 elevation levels), rock, sand
- **Regions**: 1 main map with 3-5 town sites connected by undeveloped land
- **Camera**: Smooth zoom (5 levels), pan, rotate (4 directions), minimap
- **Resource deposits**: Randomly distributed per map -- iron, coal, copper, oil, limestone, clay, sand, fertile soil, timber, gold, uranium, rare earth minerals

### 3.3 Era System

| Era | Years | Unlocks | Aesthetic |
|-----|-------|---------|-----------|
| **Frontier** | 1850-1890 | Dirt roads, wooden buildings, horse carts, basic rail, farms | Wooden frontier town |
| **Industrial** | 1890-1930 | Paved roads, factories, streetcars, expanded rail, power plants | Brick and iron, smokestacks |
| **Postwar** | 1930-1970 | Highways, buses, suburbs, high-rises, airports | Concrete, neon, cars |
| **Modern** | 1970-2010 | Metro systems, tech industry, green energy, mixed-use zoning | Glass, steel, LED |
| **Future** | 2010+ | Maglev, autonomous vehicles, vertical farms, smart grid | Cyberpunk-lite pixel art |

**Era Progression**: Unlock next era by reaching population + infrastructure + research milestones. Old buildings persist -- you must choose whether to preserve, renovate, or demolish them.

### 3.4 Zoning System

**Zone Types:**
| Zone | Density Levels | What It Produces |
|------|---------------|-----------------|
| **Residential (R)** | Low / Medium / High | Population, tax revenue |
| **Commercial (C)** | Low / Medium / High | Jobs, goods sales, tax revenue |
| **Industrial (I)** | Light / Heavy | Jobs, goods production, pollution |
| **Agricultural (A)** | -- | Food, raw materials (Frontier/Industrial eras) |
| **Office (O)** | Low / High | High-skill jobs, no pollution (Modern+ era) |
| **Mixed-Use (M)** | -- | R+C combo, walkability bonus (Modern+ era) |

**Zone Mechanics:**
- Paint zones on road-adjacent tiles
- Buildings grow organically based on demand, land value, and services
- 5 growth stages per density level (small shack -> mansion, or kiosk -> department store)
- Buildings upgrade/downgrade dynamically based on changing conditions
- Abandoned buildings appear when conditions deteriorate (visual feedback)

---

## 4. TRANSPORTATION SYSTEM (DEEP)

Transport is the backbone of Iron & Oak. Every mode has upgrade tiers, maintenance costs, and era-appropriate unlocks. The system rewards multimodal thinking and punishes car-only sprawl.

### 4.1 Design Principles

- **Induced demand**: Building more road capacity fills it up. Only mode shift provides lasting congestion relief.
- **Fourth-power law**: Heavy trucks damage roads 10,000x more than cars. Freight rail saves road maintenance costs.
- **Last-mile problem**: High-capacity transit is useless if citizens can't walk to a stop.
- **Braess's Paradox**: Sometimes removing a road REDUCES congestion by forcing better route distribution.
- **Mode share targets**: A healthy modern city needs 30-40% transit, 20-30% walking/cycling, 30-40% car.

### 4.2 Road Network

| Road Type | Capacity | Speed | Maint/yr | Era | Upgrade Path |
|-----------|----------|-------|----------|-----|-------------|
| Dirt path | 10 | Slow | $1 | Frontier | -> Cobblestone |
| Cobblestone | 30 | Medium | $3 | Frontier | -> Paved road |
| Paved road | 60 | Medium | $8 | Industrial | -> Avenue |
| Avenue (2-lane) | 120 | Medium | $15 | Industrial | -> Boulevard |
| Boulevard (4-lane) | 200 | Fast | $30 | Postwar | -> Smart boulevard |
| Highway (6-lane) | 500 | Very Fast | $80 | Postwar | -> Smart highway |
| One-way street | 80 | Medium | $10 | Industrial | -- |
| Expressway | 400 | Very Fast | $60 | Postwar | -> Highway |
| Smart boulevard | 250 | Fast | $40 | Future | -- (sensors, adaptive signals) |
| Smart highway | 600 | Very Fast | $100 | Future | -- (autonomous vehicle lanes) |

**Road Features:**
- **Intersections**: 4-way, T-junction, roundabout (reduces congestion 20%), traffic signals (Industrial+)
- **Bridges**: Wood (Frontier), Iron (Industrial), Concrete (Postwar), Cable-stayed (Modern). Each has capacity and maintenance profiles.
- **Tunnels**: Mountain tunnels (Industrial+), underwater tunnels (Modern+). Expensive but bypass terrain.
- **Toll roads**: Optional revenue source. Reduces traffic volume but angers citizens.
- **Road degradation**: Roads deteriorate over time based on traffic volume. Heavy truck traffic accelerates decay (fourth-power law). Neglected roads slow traffic and cause accidents.
- **Parking**: Surface lots (cheap, land-intensive), parking garages (expensive, compact). Insufficient parking reduces commercial zone desirability.

### 4.3 Rail System (Deep -- Transport Tycoon Tier)

#### Track Types
| Track | Speed | Cost/tile | Era | Notes |
|-------|-------|-----------|-----|-------|
| Basic rail | 40 km/h | $20 | Frontier | Single track, manual signals |
| Standard rail | 80 km/h | $40 | Industrial | Double track capable |
| Electrified rail | 120 km/h | $70 | Postwar | Requires power grid connection |
| High-speed rail | 250 km/h | $150 | Modern | Dedicated corridor, no freight |
| Maglev track | 400 km/h | $300 | Future | Elevated, zero noise |

#### Stations
| Station Type | Platforms | Capacity | Era | Features |
|-------------|-----------|----------|-----|---------|
| Halt (1 platform) | 1 | 200/day | Frontier | Basic wooden shelter |
| Small station | 2 | 800/day | Frontier | Ticket office, waiting room |
| City station | 4 | 3,000/day | Industrial | Multi-platform, freight yard |
| Central station | 6-8 | 10,000/day | Postwar | Through or terminus, shops |
| Hub station | 8-12 | 25,000/day | Modern | Multimodal interchange |

#### Rolling Stock (per era)
| Vehicle | Era | Passengers | Speed | Fuel |
|---------|-----|-----------|-------|------|
| Steam locomotive | Frontier | 60 (2 coaches) | 40 | Coal |
| Steam express | Industrial | 200 (6 coaches) | 70 | Coal |
| Diesel railcar | Postwar | 80 | 90 | Diesel |
| Electric multiple unit | Postwar | 300 | 120 | Electric |
| Intercity express | Modern | 500 | 200 | Electric |
| Maglev train | Future | 400 | 400 | Electric |

#### Rail Features
- **Signaling**: Block signals -> Path signals -> Automatic (era progression)
- **Switches/points**: Manual -> Interlocked -> Computer-controlled
- **Freight trains**: Carry goods between industrial zones, mines, and commercial areas
- **Rail yards**: Sorting facilities for freight, maintenance depots for passenger stock
- **Level crossings**: Cheap but cause road delays. Upgrade to grade-separated as traffic grows.
- **Inter-town rail**: Connect regional towns for trade and commuter traffic

### 4.4 Public Transit

#### Bus System
| Tier | Era | Vehicle | Capacity | Range |
|------|-----|---------|----------|-------|
| Horse omnibus | Frontier | Horse-drawn wagon | 12 | 8 tiles |
| Motor bus | Postwar | Diesel bus | 40 | City-wide |
| Articulated bus | Modern | Bendy bus | 80 | City-wide |
| Electric bus | Modern | Battery bus | 40 | 15 tiles (range limited) |
| BRT (Bus Rapid Transit) | Modern | Dedicated lanes | 120 | Trunk routes |
| Autonomous BRT | Future | Self-driving pods | 150 | Trunk routes |

**Bus Features:**
- Route editor: Draw routes on roads, place stops
- Frequency control: More buses = shorter wait times = higher cost
- Transfer stations: Where bus routes meet, passengers can transfer
- Depot required: Buses return to depot for maintenance

#### Tram/Streetcar System
| Tier | Era | Capacity | Track Type |
|------|-----|----------|-----------|
| Horse-drawn tram | Frontier | 20 | Street-embedded rail |
| Electric streetcar | Industrial | 40 | Street-embedded rail + overhead wire |
| Modern tram (LRT) | Modern | 120 | Mixed/dedicated right-of-way |
| Automated LRT | Future | 150 | Fully grade-separated |

**Tram Features:**
- Shares road space (reduces road capacity but adds transit capacity)
- Can be upgraded to dedicated right-of-way (higher speed, higher cost)
- Tram stops integrate with bus stops for transfers

#### Metro/Subway System
| Tier | Era | Capacity | Construction |
|------|-----|----------|-------------|
| Cut-and-cover metro | Modern | 600/train | Disrupts surface during build |
| Deep-bore metro | Modern | 600/train | Expensive but no surface disruption |
| Automated metro | Future | 800/train | Driverless, higher frequency |

**Metro Features:**
- Massive capacity but enormous upfront cost
- Stations require surface entrance buildings (affect land use)
- Underground routing avoids surface obstacles but costs 3x surface rail
- Interchange stations connect metro to surface transit

### 4.5 Water Transport

| Mode | Era | Capacity | Use Case |
|------|-----|----------|---------|
| Rowboat ferry | Frontier | 10 | River crossing |
| Steam ferry | Industrial | 60 | Cross-river commute |
| Motor ferry | Postwar | 200 | Harbor-to-harbor |
| Hydrofoil | Modern | 100 | Fast inter-city water transit |
| Cargo barge | Industrial | 500 tons | Bulk freight (coal, ore, grain) |
| Container ship | Modern | 5,000 tons | Inter-regional trade |

**Water Features:**
- **Canals**: Build canals to connect waterways, enable inland freight (Industrial+)
- **Canal locks**: Handle elevation changes between water bodies
- **Ports**: Small dock -> Commercial port -> Container terminal (upgrade tiers)
- **Dredging**: Maintain navigable depth in harbors and channels
- **Flood barriers**: Protect riverside development from spring floods

### 4.6 Air Transport

| Facility | Era | Capacity | Noise Zone |
|----------|-----|----------|-----------|
| Grass airstrip | Postwar | 2 flights/day | Small |
| Regional airport | Postwar | 20 flights/day | Medium |
| International airport | Modern | 100 flights/day | Large |
| Helipad | Modern | 8 flights/day | Minimal |
| Vertiport (eVTOL) | Future | 30 flights/day | Minimal |

**Air Features:**
- **Noise pollution**: Airports generate massive noise zones that crater residential land value
- **Ground access**: Airport needs road/rail connection. Traffic to airport can clog roads.
- **Cargo**: Air freight for high-value, low-weight goods (electronics, mail)
- **Tourism multiplier**: International airport dramatically boosts tourism income
- **Runway upgrades**: Longer runways handle larger aircraft with more passengers

### 4.7 Cable & Specialty Transport

| Mode | Era | Capacity | Best For |
|------|-----|----------|---------|
| Funicular | Industrial | 40 | Steep hills |
| Aerial tramway | Postwar | 30 | Mountain access |
| Gondola lift | Modern | 60 | Urban aerial transit |
| People mover | Modern | 100 | Airport/district connector |
| Hyperloop | Future | 200 | Inter-city ultra-fast |

### 4.8 Freight & Logistics

Freight is separate from passenger transport and critical to industry:

| Mode | Era | Tonnage | Cost/km | Speed |
|------|-----|---------|---------|-------|
| Horse cart | Frontier | 2 tons | $5 | Slow |
| Freight wagon (rail) | Frontier | 20 tons | $1 | Medium |
| Truck | Postwar | 15 tons | $3 | Fast |
| Semi-trailer | Postwar | 30 tons | $4 | Fast |
| Cargo barge | Industrial | 500 tons | $0.50 | Slow |
| Container train | Modern | 2,000 tons | $0.30 | Medium |
| Pneumatic tube | Future | 0.5 tons | $0.10 | Very Fast |
| Drone delivery | Future | 0.05 tons | $2 | Fast |

**Freight Features:**
- **Freight terminals**: Where goods transfer between modes (truck -> rail, rail -> ship)
- **Logistics centers**: Reduce last-mile delivery costs in commercial zones
- **Pipeline**: Oil and gas can be piped directly from well to refinery (cheaper than trucking)
- **Freight routing**: Heavy trucks damage roads. Smart freight policy routes trucks to designated corridors.

### 4.9 Traffic Simulation Model

```
STATISTICAL FLOW MODEL (not agent-based):

For each origin-destination pair:
  1. Calculate shortest path by ALL available modes
  2. Weight by: travel time, cost, comfort, transfers needed
  3. Distribute trips across modes (logit model)
  4. Aggregate flows on each road segment / transit line
  5. Apply BPR congestion formula:
     travel_time = free_flow_time * (1 + 0.15 * (volume/capacity)^4)
  6. Iterate until equilibrium (typically 3-5 rounds)

Visual layer: Spawn cosmetic vehicle/pedestrian sprites along flows
```

**Key mechanics:**
- Citizens choose mode based on total trip time (walk to stop + wait + ride + walk to destination)
- Adding transit reduces car trips ONLY if it's faster door-to-door
- Congestion on one road diverts traffic to parallel routes
- Road widening provides temporary relief, then induced demand fills new capacity
- Mode share displayed in transport overlay

---

## 5. INDUSTRY & RESOURCE SYSTEM (DEEP)

Every city sits on unique geography that determines available raw materials. Industry transforms raw materials through production chains, creating jobs, goods, pollution, and wealth.

### 5.1 Raw Materials by Geography

Each map generates resource deposits based on terrain type:

| Resource | Found In | Abundance | Era Available |
|----------|----------|-----------|--------------|
| Fertile soil | Plains, river valleys | Common | Frontier+ |
| Timber | Forest | Common | Frontier+ |
| Stone/Limestone | Hills, mountains | Common | Frontier+ |
| Clay | River banks | Common | Frontier+ |
| Iron ore | Mountains, hills | Moderate | Frontier+ |
| Coal | Hills, underground | Moderate | Frontier+ |
| Copper | Mountains | Uncommon | Frontier+ |
| Gold | Mountains, rivers | Rare | Frontier+ |
| Oil | Plains (underground) | Moderate | Industrial+ |
| Natural gas | Plains (underground) | Moderate | Postwar+ |
| Sand (silica) | Coastal, desert | Common | Postwar+ |
| Uranium | Mountains (deep) | Rare | Modern+ |
| Rare earth minerals | Mountains (deep) | Very Rare | Modern+ |
| Lithium | Salt flats | Rare | Modern+ |

**Key design rule**: No city has everything. A mountain city has iron and coal but no oil. A coastal city has sand and clay but no ore. This forces inter-city trade in regional play.

### 5.2 Industry Taxonomy

#### Primary Industry (Extraction)
| Industry | Input | Output | Jobs | Pollution | Era |
|----------|-------|--------|------|-----------|-----|
| Farm | Fertile soil | Wheat, corn, cotton, livestock | Low-skill | None | Frontier |
| Lumber mill | Forest tiles | Lumber | Low-skill | Low | Frontier |
| Mine (surface) | Ore deposit | Iron ore, coal, copper, gold | Low-skill | Medium | Frontier |
| Mine (deep shaft) | Deep deposit | Same + uranium, rare earth | Low-skill | Medium | Industrial |
| Quarry | Stone deposit | Stone, limestone, gravel | Low-skill | Medium | Frontier |
| Oil well | Oil deposit | Crude oil | Low-skill | Medium | Industrial |
| Gas well | Gas deposit | Natural gas | Low-skill | Low | Postwar |
| Fishing dock | Coastal water | Fish | Low-skill | None | Frontier |

#### Secondary Industry (Processing/Manufacturing)
| Industry | Input | Output | Jobs | Pollution | Era |
|----------|-------|--------|------|-----------|-----|
| Sawmill | Lumber | Planks, furniture | Low-skill | Low | Frontier |
| Brickworks | Clay | Bricks | Low-skill | Medium | Frontier |
| Smelter | Iron ore + coal | Steel ingots | Low-skill | High | Industrial |
| Cannery | Farm produce | Canned food | Low-skill | Low | Industrial |
| Textile mill | Cotton | Textiles, clothing | Low-skill | Medium | Industrial |
| Oil refinery | Crude oil | Fuel, plastics, chemicals | Mid-skill | High | Industrial |
| Cement plant | Limestone | Cement | Mid-skill | High | Industrial |
| Steel mill | Steel ingots | Steel beams, pipes | Mid-skill | Very High | Industrial |
| Auto factory | Steel + rubber | Automobiles | Mid-skill | Medium | Postwar |
| Electronics fab | Sand (silica) + copper + rare earth | Electronics, chips | High-skill | Low | Modern |
| Pharmaceutical lab | Chemicals | Medicine | High-skill | Low | Modern |
| Battery factory | Lithium + copper | Batteries | High-skill | Medium | Modern |

#### Tertiary Industry (Services)
| Industry | Input | Output | Jobs | Pollution | Era |
|----------|-------|--------|------|-----------|-----|
| General store | Goods from factories | Retail sales | Low-skill | None | Frontier |
| Market hall | Food + goods | Retail hub | Low-skill | None | Industrial |
| Department store | Manufactured goods | Retail sales | Mid-skill | None | Postwar |
| Bank | Capital | Financial services | High-skill | None | Industrial |
| Hospital | Medicine | Healthcare | High-skill | None | Industrial |
| University | -- | Education, research | High-skill | None | Postwar |

#### Quaternary Industry (Knowledge Economy)
| Industry | Input | Output | Jobs | Pollution | Era |
|----------|-------|--------|------|-----------|-----|
| Software company | Educated workers | Digital products | High-skill | None | Modern |
| Biotech lab | Medicine + research | Biotech products | High-skill | None | Modern |
| AI research center | Computing power | Automation tech | High-skill | None | Future |
| Data center | Electricity + cooling | Cloud services | Mid-skill | None (heat) | Modern |
| Space tech firm | Advanced materials | Satellite systems | High-skill | None | Future |

### 5.3 Production Chains (20 Chains)

Each chain shows: Raw Material -> Processing -> Final Product -> Consumer

```
FOOD CHAIN
  Wheat (farm) -> Flour (mill) -> Bread (bakery) -> Grocery store
  Livestock (farm) -> Meat (slaughterhouse) -> Butcher shop
  Fish (dock) -> Fish market

CONSTRUCTION CHAIN
  Timber (forest) -> Planks (sawmill) -> Construction supply
  Clay (river) -> Bricks (brickworks) -> Construction supply
  Limestone (quarry) -> Cement (cement plant) -> Construction supply
  Iron ore + Coal -> Steel (smelter) -> Steel beams (steel mill) -> Construction supply

TEXTILE CHAIN
  Cotton (farm) -> Thread (textile mill) -> Clothing (garment factory) -> Department store

ENERGY CHAIN
  Coal (mine) -> Coal power plant -> Electricity grid
  Crude oil (well) -> Fuel (refinery) -> Gas station / Oil power plant
  Natural gas (well) -> Gas power plant -> Electricity grid
  Uranium (mine) -> Nuclear power plant -> Electricity grid
  (No input) -> Solar/Wind farm -> Electricity grid (Modern+)

AUTOMOTIVE CHAIN
  Iron ore -> Steel -> Auto parts (factory) -> Automobiles (auto plant) -> Car dealer
  Crude oil -> Rubber (refinery byproduct) -> Tires -> Auto plant

ELECTRONICS CHAIN
  Sand (silica) -> Silicon wafers (fab) -> Chips -> Electronics (assembly) -> Tech store
  Copper (mine) -> Wire (processor) -> Electronics assembly
  Rare earth (mine) -> Components -> Electronics assembly

PETROCHEMICAL CHAIN
  Crude oil -> Plastics (refinery) -> Consumer goods factory -> General retail
  Crude oil -> Chemicals (refinery) -> Pharmaceutical lab -> Pharmacy

ADVANCED MATERIALS
  Lithium (mine) -> Battery cells (factory) -> EV batteries / Grid storage
  Rare earth -> Magnets -> Electric motors / Wind turbines
```

### 5.4 Industry Location Mechanics

Where you place industry matters:

- **Weber's Triangle**: Industries locate where total transport cost (input + output) is minimized. A smelter belongs near the mine, not downtown.
- **Agglomeration**: Similar industries near each other get +15% efficiency (shared workforce, suppliers)
- **Pollution radius**: Heavy industry creates pollution zones. Residential zones in the radius lose land value and happiness.
- **Noise**: Factories, airports, highways generate noise. Distance attenuates it.
- **Workforce accessibility**: Industry needs workers who can commute there. No transit = no workers.
- **Power & water**: All industry requires utility connections. Heavy industry demands more.

### 5.5 Automation & Industry Evolution

As eras progress, industry transforms:

| Era | Workers/Factory | Output/Worker | Dominant Sector |
|-----|----------------|--------------|----------------|
| Frontier | 50+ | Low | Agriculture (60%) |
| Industrial | 100+ | Medium | Manufacturing (45%) |
| Postwar | 80 | High | Manufacturing (35%), Services (40%) |
| Modern | 30 | Very High | Services (55%), Knowledge (15%) |
| Future | 10 | Extreme | Knowledge (40%), Services (45%) |

**Automation events**: When researching automation technologies, factories shed workers. If you don't have a service/knowledge economy ready to absorb them, unemployment spikes and citizens leave.

---

## 6. DEMOGRAPHICS & POPULATION SYSTEM (DEEP)

### 6.1 Household-Based Simulation

Population is simulated at the **household** level (~20,000 households for a 100k city). Each household tracks:

- Home location (residential tile)
- Workplace (employment tile)
- Wealth class (Destitute / Working / Middle / Upper-Middle / Wealthy)
- Education levels of members
- Age of members (tracked by bracket)
- Cultural profile (ethnicity, language, religion)
- Happiness (0-100)
- Health status

### 6.2 Age Brackets & Lifecycle

| Bracket | Age | Needs | Contribution | Duration |
|---------|-----|-------|-------------|----------|
| **Infant** | 0-4 | Daycare, healthcare | None (cost center) | 4 years |
| **Child** | 5-12 | Elementary school, parks | None (cost center) | 8 years |
| **Teen** | 13-17 | High school, recreation | Part-time labor (minor) | 5 years |
| **Young Adult** | 18-25 | University or entry job, housing | Low tax, high spending | 8 years |
| **Adult** | 26-40 | Housing, employment, childcare | Peak productivity + tax | 15 years |
| **Mature Adult** | 41-55 | Housing, healthcare, career | High tax, peak earning | 15 years |
| **Senior** | 56-67 | Healthcare, pension transition | Declining productivity | 12 years |
| **Elderly** | 68+ | Healthcare, senior housing, pension | Retired (cost center) | Variable (death rate) |

### 6.3 Birth, Death & Migration

**Birth rate formula:**
```
births_per_1000 = base_rate
  * housing_availability_factor     (0.5 if housing scarce, 1.5 if abundant)
  * happiness_factor                (0.7 at low happiness, 1.3 at high)
  * income_factor                   (higher income = fewer births, historical pattern)
  * healthcare_factor               (better healthcare = more births survive)
  * era_modifier                    (Frontier: 40/1000, Modern: 12/1000 base)
```

**Death rate formula:**
```
deaths_per_1000 = base_rate_by_age_bracket
  * healthcare_coverage_factor      (0.6 with full healthcare, 1.5 with none)
  * pollution_exposure_factor       (up to 1.4x in heavy pollution)
  * happiness_factor                (stress kills: unhappy = slightly higher death rate)
  * era_modifier                    (medical advances reduce base rate)
```

**Immigration (attracted by):**
- Job availability (openings > 0)
- Low crime, low pollution
- Good schools and healthcare
- Low taxes relative to services
- Cultural amenities (parks, theaters, stadiums)

**Emigration (triggered by):**
- Unemployment > 8%
- Happiness < 30
- Missing essential services
- Very high taxes with poor services
- Brain drain: university graduates leave if no high-skill jobs exist

### 6.4 Education Pipeline

Education is not just a service building -- it's a full pipeline that determines your city's future workforce quality:

| Level | Building | Duration | Capacity | Produces |
|-------|----------|----------|----------|----------|
| **Preschool/Daycare** | Daycare center | 2 years | 30 children | Frees parents for work |
| **Elementary** | Elementary school | 6 years | 200 students | Basic literacy (required for all jobs above manual labor) |
| **Secondary** | High school | 4 years | 400 students | Skilled workers (factory foremen, clerks, technicians) |
| **Vocational** | Trade school | 2 years | 100 students | Specialized tradespeople (electricians, mechanics) |
| **University** | University | 4 years | 1,000 students | Professionals (engineers, doctors, teachers, researchers) |
| **Graduate** | Research institute | 2 years | 200 students | Researchers, specialists (generates research points) |

**Teacher shortage mechanic**: Schools need teachers (university-educated citizens). If you don't produce enough university graduates, schools become understaffed, class sizes grow, education quality drops, and the cycle reinforces itself.

**Brain drain**: University graduates seek high-skill jobs. If your city lacks office zones, tech companies, or hospitals, graduates emigrate to other towns in the region. Losing educated citizens shrinks your tax base and research capacity.

### 6.5 Wealth Classes & Social Stratification

| Class | % of Pop | Housing | Tax Contribution | Needs |
|-------|----------|---------|-----------------|-------|
| **Destitute** | 2-8% | Shelters, slums | Near zero | Food, shelter, basic healthcare |
| **Working Class** | 25-40% | Low-density R | Low | Affordable housing, transit, schools |
| **Middle Class** | 30-45% | Medium-density R | Moderate | Good schools, parks, low commute |
| **Upper-Middle** | 10-20% | High land value R | High | Cultural amenities, prestige, safety |
| **Wealthy** | 2-5% | Estates, penthouses | Very high (but flight risk) | Luxury, exclusivity, low taxes |

**Wealth mobility**: Citizens can move between classes based on education, employment, and city conditions. A working-class child who attends university and gets a tech job becomes upper-middle class. Inversely, job loss can push middle-class households down.

**Income inequality**: If the gap between wealthy and working class grows too large, social unrest increases. Protests, crime spikes, and political instability follow.

### 6.6 Cultural Profile System

Each neighborhood develops a cultural profile based on its inhabitants:

**8 Cultural Dimensions:**
1. **Ethnic diversity index** (0-1): Homogeneous -> Melting pot
2. **Language variety**: Number of languages spoken
3. **Religious composition**: Determines demand for religious buildings
4. **Age skew**: Young/student neighborhoods vs. aging suburbs
5. **Economic character**: Blue-collar vs. white-collar vs. mixed
6. **Arts & culture engagement**: Demand for theaters, galleries, music venues
7. **Civic participation**: Voter turnout, volunteerism, community events
8. **Environmental values**: Demand for green space, recycling, clean energy

**Cultural Mechanics:**
- **Ethnic neighborhoods**: Immigrant communities cluster, creating cultural districts (Chinatown, Little Italy). These generate tourism + cultural bonuses but may face integration challenges.
- **Gentrification**: When wealthier residents move into low-income neighborhoods, property values rise, original residents get displaced. Visual: old buildings get renovated, coffee shops replace laundromats. Gameplay: land value rises but community happiness drops temporarily.
- **Cultural festivals**: Neighborhoods with strong cultural identity host festivals (tourism + happiness boost). Requires event infrastructure.
- **NIMBY (Not In My Back Yard)**: Wealthy neighborhoods resist nearby construction of homeless shelters, industrial zones, or transit depots. NIMBY reduces approval rating and can block construction unless overridden.

### 6.7 Healthcare System

| Tier | Building | Coverage | Capacity | Era |
|------|----------|----------|----------|-----|
| **Tier 1** | Clinic | 10 tiles | 500 patients | Frontier |
| **Tier 2** | General hospital | 20 tiles | 5,000 patients | Industrial |
| **Tier 3** | Specialist hospital | City-wide | 10,000 patients | Postwar |
| **Tier 4** | Research hospital | City-wide + research bonus | 15,000 patients | Modern |

**Health Mechanics:**
- Unserved citizens have higher death rates and lower happiness
- Pollution exposure increases healthcare demand
- **Pandemic events**: Occasional disease outbreaks spread through connected populations. Healthcare capacity determines severity. Quarantine policies (lockdown) reduce spread but crash economy temporarily.
- **Aging population crisis**: As your city matures, elderly citizens increase healthcare demand. Without enough hospitals and senior care, the system is overwhelmed.

### 6.8 Political System

Citizens aren't passive -- they have opinions and they vote:

**Approval Rating (0-100):**
```
approval = weighted_average(
  happiness:          30%   (overall citizen satisfaction)
  economy:            25%   (unemployment rate, growth trend)
  services:           20%   (coverage gaps, response times)
  environment:        10%   (pollution, green space)
  infrastructure:     10%   (road quality, transit reliability)
  recent_decisions:    5%   (major policy changes, demolitions)
)
```

**Political Mechanics:**
- **NIMBY protests**: Citizens near undesirable projects (landfills, highways, factories) protest. Reduces approval and can block construction.
- **Petitions**: Citizens petition for specific improvements (new school, park, transit line). Fulfilling petitions boosts approval.
- **Referendums**: Major decisions (new highway route, industrial zone expansion, tax hike) can trigger referendums. Winning requires >50% approval.
- **City council advisors**: 5 advisors (Transport, Economy, Environment, Social, Infrastructure) provide recommendations and warnings. Following their advice is optional but affects outcomes.
- **Policy levers**: Set citywide policies that have tradeoffs:
  - Minimum wage: Higher wage = happier workers + higher business costs
  - Environmental regulations: Reduces pollution + increases industry cost
  - Historic preservation: Protects old buildings + blocks redevelopment
  - Rent control: Keeps housing affordable + discourages new construction
  - Public housing: Reduces homelessness + costs budget

---

## 7. ECONOMY & BUDGET (DEEP)

### 7.1 Revenue Sources

| Source | Details | Era |
|--------|---------|-----|
| Property tax | Per-tile based on land value and zone type | All |
| Sales tax | % of commercial transactions | Industrial+ |
| Income tax | Per-employed-citizen based on wealth class | Industrial+ |
| Corporate tax | Per-business based on revenue | Postwar+ |
| Transit fares | Per-rider on public transit | All |
| Trade income | Export surplus goods to external/regional market | All |
| Tolls | Optional highway/bridge/tunnel tolls | Postwar+ |
| Tourism | Hotels + landmarks + historic districts + festivals | All |
| Parking fees | Revenue from parking structures | Postwar+ |
| Airport fees | Landing fees + passenger taxes | Postwar+ |
| Fines & permits | Building permits, traffic fines | Industrial+ |

### 7.2 Expenses

| Category | Details |
|----------|---------|
| Road maintenance | Per-tile based on road type, condition, and traffic volume |
| Transit operations | Per-vehicle + per-stop + driver wages |
| Rail infrastructure | Track maintenance + signal systems + station staffing |
| Services | Fire, police, health, education -- per-building operating cost |
| Utilities | Power plant fuel, water treatment, sewage processing |
| Waste management | Collection trucks + landfill/recycling operation |
| Debt interest | Loans accrue interest; bankruptcy is possible |
| Administration | Scales with city size and complexity |
| Pensions | Retired citizens draw pension (grows as population ages) |
| Social services | Homeless shelters, food programs, welfare payments |

### 7.3 Budget Pressure

- **Loans**: Available with interest rates that increase with city debt ratio (5% -> 8% -> 12%)
- **Bonds**: Issue bonds for major infrastructure (highway, metro, airport). Fixed repayment schedule.
- **Bankruptcy**: Possible. Services shut down, citizens flee, buildings decay. Recovery is hard.
- **No infinite money**: This is the #1 thing expert players want. Every dollar matters.
- **Budget forecast**: Show projected revenue/expenses for next 1/5/10 years based on current trends.

### 7.4 Trade & Regional Economy

- Cities can export surplus raw materials and manufactured goods
- Import prices fluctuate based on global events and supply
- Regional trade between connected towns uses transport infrastructure (rail freight, truck routes, barges)
- **Trade routes**: Establish recurring trade agreements with other towns for stable pricing
- **Market prices**: Supply and demand affect commodity prices. Oversupply crashes prices.
- **Tariffs**: Optional policy to protect local industry at the cost of higher consumer prices

---

## 8. RESEARCH & TECHNOLOGY SYSTEM

*Full tech tree specification: see `RESEARCH_TECH_TREE.md` (102 technologies across 10 categories)*

### 8.1 Research Generation

| Source | RP/month | Requirements |
|--------|----------|-------------|
| Library | 1 | Any era, cheap |
| University | 5 + 1/professor | Staffed, powered |
| Research Lab | 10 | Modern era+, university-educated workers |
| Tech Campus | 20 | Future era, fiber internet |
| Educated population | +0.01/university-educated citizen | Passive bonus |
| Private R&D | +2/heavy industry tile with tech workers | Free bonus from industry |

**Research Funding slider**: 0% (passive only) to 200% (expensive but fast). Budget allocation.

### 8.2 Technology Categories (10)

1. **Transport** (15 techs): Roads, rail, transit, aviation
2. **Energy** (12 techs): Power generation from coal to fusion
3. **Construction** (10 techs): Building materials and techniques
4. **Communication** (8 techs): Telegraph to fiber internet
5. **Agriculture** (8 techs): Farming methods and food processing
6. **Industrial** (12 techs): Manufacturing and automation
7. **Medical** (10 techs): Healthcare and sanitation
8. **Environmental** (9 techs): Pollution control and sustainability
9. **Social/Civic** (10 techs): Governance and social systems
10. **Computing/Digital** (8 techs): Computers to AI

### 8.3 Key Branching Decisions

At 4 critical moments, players must choose between mutually exclusive paths:

1. **Energy Fork (Industrial era)**: Coal expansion (cheap, dirty) vs. Hydroelectric (expensive, clean)
2. **Transport Fork (Postwar era)**: Highway-centric (car culture) vs. Transit-centric (European model)
3. **Industry Fork (Modern era)**: Heavy industry automation (high output, unemployment) vs. Knowledge economy transition (slower but sustainable)
4. **Future Fork**: AI automation (extreme efficiency, social upheaval) vs. Human-centered tech (balanced, stable)

Each choice permanently shapes your city's character and available late-game options.

### 8.4 Eureka Bonuses

Completing in-game milestones grants 25-50% progress toward related research:
- Building 10km of rail -> "Advanced Rail Signaling" 50% complete
- Reaching 10,000 population -> "Urban Planning" 25% complete
- First factory -> "Assembly Line" 25% complete
- Zero pollution deaths for 5 years -> "Environmental Protection" 50% complete

---

## 9. SERVICES & UTILITIES

### 9.1 Essential Services

| Service | Effect | Coverage Radius | Era |
|---------|--------|----------------|-----|
| Fire Station | Reduces fire risk, responds to fires | 15 tiles | Frontier |
| Police Station | Reduces crime rate | 12 tiles | Frontier |
| Clinic | Basic healthcare | 10 tiles | Frontier |
| Hospital | Full healthcare, surgery | 20 tiles | Industrial |
| Elementary School | Educates children | 15 tiles | Frontier |
| High School | Higher education | 20 tiles | Industrial |
| University | Research + highest education | City-wide | Postwar |
| Daycare | Childcare, frees working parents | 8 tiles | Postwar |
| Senior center | Elderly care, social | 10 tiles | Modern |
| Homeless shelter | Reduces destitution | City-wide | Industrial |

### 9.2 Utilities

| Utility | Details |
|---------|---------|
| **Power** | Coal -> Oil -> Gas -> Nuclear -> Solar/Wind -> Fusion. Grid must connect to all zones. Brownouts if demand > supply. |
| **Water** | Pumps + treatment + pipe network. Pressure drops with distance. Contamination from industrial runoff. |
| **Sewage** | Treatment plants process waste. Overflow causes pollution + disease. |
| **Waste** | Garbage trucks -> landfill or recycling plant. Uncollected waste -> rats, disease, land value crash. |
| **Internet** | Modern era. Fiber network enables tech industry, office zones, smart infrastructure. |
| **District heating** | Efficient heating for dense neighborhoods (Modern+). Reduces individual energy consumption. |

---

## 10. OVERLAYS & DATA VISUALIZATION

**Essential Overlays (toggle with hotkeys):**
| Overlay | What It Shows |
|---------|---------------|
| Traffic | Green/yellow/red congestion per road segment |
| Land Value | Blue (low) -> Green -> Yellow -> Red (high) heatmap |
| Crime | Intensity heatmap with police coverage circles |
| Pollution | Air + noise + ground pollution layers |
| Services | Coverage radius for fire/police/health/education |
| Zoning | R/C/I/A/O zone colors |
| Transit | All transit routes with ridership thickness |
| Power/Water | Grid connectivity, brownout/pressure warnings |
| Happiness | Per-district happiness scores |
| Commute | Average commute time heatmap |
| Demographics | Age distribution, wealth class, cultural profile |
| Industry | Production chain connections, freight flows |
| Resources | Deposit locations, extraction rates, depletion levels |
| Education | School coverage, literacy rates, brain drain indicators |
| Health | Healthcare coverage, disease risk, pollution exposure |

**Statistics Panel:**
- Historical graphs for ANY metric (population, revenue, traffic, crime over time)
- Per-district breakdowns
- Budget detail with line items
- Transport efficiency (cost per rider, ridership per line)
- Demographic pyramids (age/gender distribution)
- Trade balance and commodity prices
- Advisor recommendations

---

## 11. EVENTS & CHALLENGES

### 11.1 Natural Events (Seasonal)
| Event | Season | Effect |
|-------|--------|--------|
| Spring floods | Spring | Damages riverside buildings, disrupts roads |
| Heatwave | Summer | Power demand spikes, health risk for elderly |
| Harvest season | Autumn | Agricultural output boost, festival tourism |
| Blizzard | Winter | Roads slow, heating demand spikes, construction halted |
| Drought | Summer | Water supply drops, fire risk increases |
| Thunderstorm | Any | Lightning can cause fires, brief flooding |
| Earthquake | Any (rare) | Building damage proportional to construction quality |

### 11.2 Economic Events
| Event | Effect |
|-------|--------|
| Recession | Demand drops across all zones, tax revenue falls |
| Industry boom | One sector sees massive demand spike |
| Tech company HQ offer | Meet conditions to attract major employer |
| Trade disruption | Import prices spike, supply chains stressed |
| Housing crisis | Immigration wave strains residential capacity |
| Market crash | Property values plummet, construction halts |
| Oil price shock | Energy costs spike, alternative energy demand surges |

### 11.3 Social Events
| Event | Effect |
|-------|--------|
| Labor strike | Factories shut down, goods production stops |
| Pandemic | Disease spreads, healthcare overwhelmed, lockdown option |
| Cultural festival | Tourism boost, happiness boost for host neighborhood |
| University protest | Students demand policy changes, approval impact |
| NIMBY rally | Residents block specific construction project |
| Brain drain wave | Educated citizens leave en masse if conditions poor |

### 11.4 Era-Specific Events
| Event | Era | Effect |
|-------|-----|--------|
| Gold rush | Frontier | Massive immigration wave |
| Railroad arrives | Frontier | Trade boom |
| Great fire | Industrial | Large-scale fire |
| Car boom | Postwar | Citizens demand highways |
| Environmental movement | Modern | Citizens demand green policies |
| Automation wave | Future | Factories shed workers |
| AI disruption | Future | Knowledge workers face unemployment |

### 11.5 Endgame Content

#### City Milestones
| Milestone | Requirement | Reward |
|-----------|-------------|--------|
| Hamlet | 100 pop | Unlock basic services |
| Village | 500 pop | Unlock Industrial era |
| Town | 2,000 pop | Unlock first transit mode |
| City | 10,000 pop | Unlock Postwar era |
| Metropolis | 50,000 pop | Unlock Modern era |
| Megalopolis | 100,000 pop | Unlock Future era |
| Utopia | 200,000 pop + 90% happiness | Victory screen + sandbox continues |

#### Regional Goals
- Connect all towns by rail
- Achieve positive trade balance across region
- Every town above 10,000 population
- Zero homelessness region-wide
- Complete transit network (every citizen within 5 tiles of a transit stop)
- Achieve mode share: <40% car usage region-wide

#### Scenarios (Curated Challenges)
| Scenario | Challenge |
|----------|-----------|
| "The Dust Bowl" | Drought devastates agriculture. Transition economy before bankruptcy. |
| "Gridlock" | Terrible traffic. Fix transport without demolishing historic core. |
| "The Merger" | Two rival towns must unify infrastructure. |
| "Green New Deal" | Convert fossil-fuel city to 100% renewable. Budget-neutral. |
| "The Commuter Problem" | 45-minute average commutes. Get under 15 minutes. |
| "Rust Belt Revival" | Factories closing. Reinvent the economy. |
| "Silver Tsunami" | Aging population overwhelming healthcare and pensions. |
| "Boomtown" | Oil discovery causes explosive growth. Build sustainably. |

---

## 12. TECHNICAL ARCHITECTURE (Godot 4)

### 12.1 Simulation Architecture

```
+-----------------------------------------------+
|          SIMULATION LAYER (C#)                 |
|                                                |
|  +----------+ +---------+ +--------------+     |
|  | Traffic   | | Economy | | Population   |    |
|  | Flow Grid | | R/C/I   | | Households   |    |
|  |           | | Budget  | | Demographics |    |
|  |           | | Trade   | | Culture      |    |
|  +-----+-----+ +----+----+ +------+-------+    |
|        |             |             |            |
|  +-----+-------------+-------------+--------+  |
|  |         Land Value Calculator             |  |
|  |      (influence maps, dirty regions)      |  |
|  +-------------------------------------------+  |
|                                                |
|  +----------+ +-----------+ +------------+     |
|  | Industry | | Research  | | Politics   |     |
|  | Chains   | | Tech Tree | | Approval   |    |
|  | Resources| | Eureka    | | Referendums|    |
|  +----------+ +-----------+ +------------+     |
|                                                |
|  Tick rates:                                   |
|    Traffic: 2/sec                              |
|    Economy: 1/game-day                         |
|    Population: 1/game-month                    |
|    Research: 1/game-month                      |
|    Land Value: on-change (dirty flags)         |
|                                                |
|  Double-buffered: sim writes back buffer,      |
|  render reads front buffer, swap each tick     |
+------------------+----------------------------+
                   | signals
+------------------+----------------------------+
|          GAME LAYER (GDScript)                 |
|                                                |
|  Build System | Time/Era | Events/Weather     |
|  Zone System  | Research UI | Policy Manager  |
+------------------+----------------------------+
                   | signals
+------------------+----------------------------+
|          RENDER LAYER (GDScript)                |
|                                                |
|  TileMapLayers (terrain, roads, zones,         |
|  buildings, overlays)                          |
|  Vehicle sprites (cosmetic, follow roads)      |
|  Citizen sprites (cosmetic, walk on paths)     |
|  Weather particles, lighting shaders           |
|  UI overlay heatmaps                           |
+------------------------------------------------+
```

### 12.2 Performance Budget

| Component | Target | Strategy |
|-----------|--------|----------|
| Grid size | 256x256 (65,536 tiles) | Chunked 16x16, dirty flags |
| Population | 200,000 (max ~20k households) | Staggered updates, SOA layout |
| Industry chains | 20 chains, ~500 industry tiles | Event-driven, not per-tick |
| Vehicles (visual) | 500 max on screen | Object pooling, LOD culling |
| Citizens (visual) | 200 max on screen | Object pooling, LOD culling |
| Frame rate | 60fps | Simulation on separate thread |
| Simulation tick | <2ms | C# for hot paths, lazy evaluation |
| Memory | <250MB | Tight data structures, streaming |

### 12.3 Save System

- **Format**: Custom binary + JSON metadata
- **Auto-save**: Every 5 game-minutes
- **Data saved**: Full grid state, all household data, economy state, industry state, research progress, time/era, approval/policy state, undo history
- **Cloud saves**: Steam Cloud integration
- **Mod compatibility**: Save format versioned, forward-compatible

---

## 13. ART PIPELINE (AI-Assisted)

### 13.1 Asset Count Estimate

| Category | Count | AI Generated? |
|----------|-------|---------------|
| Terrain tiles (per season) | 20 x 4 = 80 | Yes + cleanup |
| Road/rail tiles | 60 | Yes + cleanup |
| Buildings (5 eras x zones x growth stages) | ~300 | Yes + cleanup |
| Industry buildings (all types) | 60 | Yes + cleanup |
| Service buildings | 40 | Yes + cleanup |
| Vehicles (all modes, per era) | ~120 | Yes + cleanup |
| Citizen sprites (walk cycles) | 48 (16 dir x 3 wealth) | Partial + manual |
| UI elements | ~100 | Kimi image-to-code |
| Weather/particle effects | 25 | Manual in Godot |
| Portraits (advisors) | 6 | AI + cleanup |
| Title screen + backgrounds | 10 | AI |
| Icons (resources, tools, overlays) | ~80 | AI + cleanup |
| **Total unique sprites** | **~930** | **~80% AI-assisted** |

### 13.2 Audio Pipeline

| Category | Count | Tool | Style |
|----------|-------|------|-------|
| Era background music | 5 x 2 = 10 | Suno/Udio | Era-appropriate instrumentation |
| Season ambient loops | 4 | ElevenLabs SFX | Nature sounds per season |
| Construction SFX | 12 | ElevenLabs SFX | Hammer, saw, crane, demolition |
| Traffic SFX | 10 | ElevenLabs SFX | Cars, trucks, horns, horse hooves |
| Train SFX | 8 | ElevenLabs SFX | Steam, diesel, electric, station sounds |
| Water transport SFX | 4 | ElevenLabs SFX | Ferry horn, waves, canal lock |
| Air transport SFX | 4 | ElevenLabs SFX | Propeller, jet, helicopter |
| Industry SFX | 8 | ElevenLabs SFX | Factory hum, smelter, sawmill |
| UI SFX | 15 | ElevenLabs SFX | Clicks, confirms, menus, cash register |
| Event stings | 10 | Suno | Disaster, milestone, era transition |
| Political sounds | 4 | ElevenLabs SFX | Protest chants, applause, bell |
| **Total** | **~89 audio assets** | | |

---

## 14. DEVELOPMENT ROADMAP

### Phase 0: Foundation (Week 1-2)
Godot project, isometric grid, camera, basic tile placement, data porting.

### Phase 1: Core City Loop (Week 3-5)
Zoning, roads, building growth, R/C/I demand, basic budget, time system, HUD.

### Phase 2: Simulation Depth (Week 6-9)
Traffic flow, land value, services, utilities, population happiness, immigration/emigration, building abandonment.

### Phase 3: Transport (Week 10-14)
Full road hierarchy, bus system, rail (track + stations + trains + signals), tram, ferry, freight, multimodal commute, inter-town rail.

### Phase 4: Industry & Resources (Week 15-17)
Resource deposits, extraction buildings, all 20 production chains, freight logistics, trade system, automation mechanics.

### Phase 5: Demographics & Culture (Week 18-20)
Age brackets, lifecycle, education pipeline, wealth classes, cultural profiles, gentrification, healthcare tiers, political system (approval, NIMBY, referendums).

### Phase 6: Research & Tech Tree (Week 21-23)
102 technologies, research buildings, funding slider, eureka bonuses, branching decisions, era transition triggers.

### Phase 7: Art & Audio Pass (Week 24-27)
All sprites (930+), day/night shader, seasonal palettes, weather particles, full soundtrack, all SFX, smoke/steam/glow effects.

### Phase 8: Advanced Transport & Air/Water (Week 28-30)
Metro/subway, airport, seaport, canals, cable transport, logistics centers, advanced traffic features.

### Phase 9: Regional Play & Events (Week 31-34)
Multiple town sites, inter-town trade/migration, seasonal weather events, economic events, social events, era-specific events, advisor system, news ticker.

### Phase 10: Polish & Ship (Week 35-38)
8 scenarios, tutorial, statistics panel, advanced overlays, settings, save/load, Steam integration, performance optimization, balancing.

**Total: ~38 weeks (9 months) to Early Access**

### Post-Launch Roadmap
| Update | Content |
|--------|---------|
| **Update 1** (Month 2) | Modding support (custom buildings, scenarios) |
| **Update 2** (Month 4) | Multiplayer co-op (shared region) |
| **Update 3** (Month 6) | Expansion: Deep Water (advanced naval, offshore oil, port cities) |
| **Update 4** (Month 8) | Expansion: The People (deeper politics, elections, factions) |
| **1.0 Release** (Month 10) | Full release with all systems complete |

---

## 15. COMPETITIVE POSITIONING

| Feature | Theotown | Pocket City 2 | Urbek | **Iron & Oak** |
|---------|----------|---------------|-------|----------------|
| Pixel art | Yes | No (3D) | Voxel | **Yes (premium)** |
| Simulation depth | Shallow | Shallow | Medium | **Very Deep** |
| Transport modes | 4 | None | 3 (DLC) | **15+** |
| Production chains | No | No | Yes | **20 chains** |
| Budget pressure | Low | None | Low | **High** |
| Era progression | No | No | No | **5 eras** |
| Research tree | No | No | No | **102 techs** |
| Demographics | Basic | Basic | Basic | **Full lifecycle** |
| Cultural simulation | No | No | No | **8 dimensions** |
| Regional play | No | No | No | **3-5 towns** |
| Per-city resources | No | No | Yes | **14 resource types** |
| Political system | No | No | No | **Approval + NIMBY + referendums** |
| Scenarios | No | No | DLC | **8 included** |

### Market Position Statement
> Iron & Oak is the city builder for people who loved SimCity 4's depth but want it wrapped in the warm pixel art charm of Stardew Valley -- with transport networks deep enough to satisfy OpenTTD fans, industry chains complex enough for Anno enthusiasts, and demographic simulation that makes every citizen's life story matter.

---

## 16. MONETIZATION

- **Base game**: $14.99 (Early Access), $19.99 (1.0)
- **Expansion packs**: $7.99 each (new scenarios, transport modes, eras)
- **No microtransactions. No ads. No season passes.**

### Revenue Estimate (Conservative)
| Milestone | Units | Revenue (gross) |
|-----------|-------|-----------------|
| Early Access launch | 5,000 | $75,000 |
| 6 months | 20,000 | $300,000 |
| 1.0 release | 50,000 | $750,000 |
| 12 months post-1.0 | 100,000 | $1,500,000 |

---

---

## 17. COMPANION DESIGN DOCUMENTS

This GDD is the master reference. The following companion documents contain deep-dive specifications for individual systems:

| Document | Contents | Size |
|----------|----------|------|
| `RESEARCH_TECH_TREE.md` | Full 102-technology specification across 10 categories, research generation mechanics, branching decisions, eureka bonuses | ~66KB |
| `GLOBAL_CITY_ARCHETYPES.md` | 10 regional city archetypes (North America, Europe, East Asia, SE Asia, South Asia, Middle East, Africa, South America, Scandinavia, Eastern Europe), progressive city unlock system, global trade mechanics, multiplayer scaling design | ~1,500 lines |
| `POLITICAL_LAW_SYSTEM.md` | 32 individual laws across 7 categories, governance styles (2-axis political compass), city council with 5 factions, elections, lobbying (12 groups), corruption mechanics, protest escalation, 7 policy packages (Singapore Model, Nordic Model, etc.), enforcement infrastructure | ~1,950 lines |
| `VEHICLE_WEALTH_SYSTEM.md` | ~80 vehicle types mapped to wealth class x era, vehicle aging/condition shader system, safety & inspection policies, regional vehicle culture (6 templates), emergency & commercial vehicles, performance model | ~800 lines |
| `EDUCATION_MEDIA_SYSTEM.md` | 14 school types with 8 philosophies, school transport (6 modes incl. bus fleet management), education quality formula, curriculum policy sliders, 10 media types with bias system, public opinion model, university expansion | ~900 lines |
| `EXPANDED_ZONES_EVENTS_SPORTS.md` | 8 new zone types (farming, waste, energy, tech, AI, military, entertainment, university districts), 91 special ploppable areas, 80+ event types, full sports league system with 11 sports and persistent seasons | ~1,200 lines |
| `MISSING_SYSTEMS.md` | 21 gap-fill categories: deep fire department (response time model, hydrants, wildfire), EMS, childcare/kindergarten, death infrastructure (cemeteries, crematoriums), postal system, street infrastructure (lighting, trees, furniture), sanitation (snow plowing, pest control), cultural/religious buildings, recreation (beaches, pools, nightlife, restaurants), justice pipeline (courts, prisons, rehabilitation), financial infrastructure (banks, insurance, stock exchange), telecommunications (telegraph to 5G), construction process (permits, scaffolding, disruption), tourism (hotels, Airbnb, cruise terminals), deep housing market, full utility systems (water pressure, sewer overflow, stormwater), weather as continuous simulation, accessibility, night economy, infrastructure aging/lifecycle, citizen daily routines and life events | ~1,800 lines |
| `CULTURAL_DNA_EMERGENCE.md` | Geography + Culture + Policy = Emergent City Identity system. 8 climate types, 10 terrain traits, resource generation by geography, 8 cultural DNA dimensions (work ethic, collectivism, risk tolerance, environmental values, social trust, progressivism, hierarchy acceptance, cultural pride), 12 regional presets, 20 emergent city archetypes with activation conditions (Tourist Paradise, Tech Hub, Rust Belt, Sin City, etc.), cultural drift mechanics, cross-system integration formulas, 8 feedback loops (positive and negative spirals), archetype transition map, multiplayer cultural interactions | ~1,500 lines |

### Total Design Specification
- **8 companion documents**, ~11,000+ lines of game design
- **32 laws**, **102 technologies**, **10 global regions**, **15+ transport modes**
- **20 production chains**, **8 new zone types**, **91+ special buildings**
- **80+ events**, **11 sports**, **20 emergent city archetypes**
- **8 cultural DNA dimensions** with drift, feedback loops, and emergence
- **21 gap-fill systems**: fire, EMS, childcare, death, postal, streets, sanitation, religion, recreation, justice, finance, telecom, construction, tourism, housing, utilities, weather, accessibility, night economy, infrastructure aging, citizen life simulation
- **Full lifecycle demographics**: birth -> kindergarten -> school -> career -> marriage -> retirement -> death -> cemetery
- **Political simulation**: elections, factions, corruption, protests, referendums, 7 policy packages
- **Cultural emergence**: geography + culture + policy = organic city identity
- **~1,500+ unique sprites** estimated

---

*Document version: 3.0*
*Created: March 2026*
*Engine: Godot 4.x with C# simulation core*
*Target: Steam Early Access Q1 2027*
