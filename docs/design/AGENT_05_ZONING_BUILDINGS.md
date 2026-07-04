# AGENT 05: ZONING & BUILDINGS

## Role
Build the zone placement system, building growth/decline mechanics, building variety per era/wealth/culture, and the physical representation of the city on the grid.

---

## Prerequisites
- Agent 01: GridData, TileMap layers, EventBus
- Agent 03 (partial): RCI demand signals

---

## Phase 1: Zone System (Day 4-6)

### Tasks
1. **Zone Types** (6):
```
R  - Residential (green)   : Housing for citizens
C  - Commercial (blue)     : Shops, offices, services
I  - Industrial (yellow)   : Factories, workshops, extraction
A  - Agricultural (brown)  : Farms, orchting, livestock
O  - Office (cyan)         : Modern era, tech/service sector
P  - Park/Recreation (lime): Green spaces, sports, leisure
```

2. **Zone Placement**:
   - Paint zones with brush tool (1x1, 3x3, fill modes)
   - Zones must touch road to develop
   - Zone density setting: Low / Medium / High
   - Low density: small buildings, 1-2 stories
   - High density: towers, 5-20 stories (era-dependent)

3. **Zone Growth Logic**:
```
growth_chance = demand * desirability * services * road_access
demand = RCI demand from economy (0.0 - 1.0)
desirability = land_value_normalized (0.0 - 1.0)
services = (has_power + has_water + has_road) / 3.0
road_access = 1.0 if adjacent to road, 0.0 otherwise
```
   - Each tick: empty zone tiles roll for growth
   - Growing: place building sprite, start construction animation
   - Construction takes 1-6 game months depending on building size

4. **Zone Decline**:
```
decline_chance = max(0, -demand * 0.5 + pollution * 0.3 + crime * 0.2)
```
   - Declining buildings: visual degradation (cracks, broken windows shader)
   - Abandoned buildings: no tax revenue, crime increase, can be demolished
   - Auto-demolish after 2 years abandoned (configurable)

5. **Zone Upgrade**:
   - Buildings upgrade when demand high + services good + land value rising
   - Upgrade = replace building sprite with larger version
   - Same zone tile, higher building tier

### AI Tool Usage
- **Claude**: Generate zone placement tool, growth/decline simulation
- **Claude**: Generate density calculation and upgrade logic
- **Kimi**: Screenshot of SimCity 4 zone painting -> adapt to isometric Godot
- **Kimi**: NOT needed for simulation logic

### Output Files
- `scripts/game/zone_system/ZoneManager.gd`
- `scripts/game/zone_system/ZonePainter.gd`
- `scripts/simulation/economy/ZoneGrowth.cs`
- `scripts/simulation/economy/ZoneDecline.cs`

---

## Phase 2: Building Database (Day 6-8)

### Tasks
1. **Building Registry** (JSON data):
```json
{
    "id": "res_frontier_small_01",
    "name": "Frontier Cabin",
    "era": "frontier",
    "zone": "residential",
    "density": "low",
    "size": [1, 1],
    "height": 1,
    "capacity": 4,
    "jobs": 0,
    "cost": 100,
    "maintenance": 2,
    "power_demand": 0,
    "water_demand": 1,
    "pollution": 0,
    "fire_risk": 0.05,
    "sprite": "buildings/res_frontier_small_01.png",
    "construction_time_months": 1,
    "wealth_min": 0,
    "wealth_max": 2,
    "unlock_tech": null
}
```

2. **Building Counts by Category**:
```
Residential:  60 variants (5 eras x 3 density x 4 wealth tiers)
Commercial:   40 variants (5 eras x 2 density x 4 types)
Industrial:   30 variants (5 eras x 3 industry types x 2 sizes)
Agricultural: 15 variants (3 farm types x 5 progression stages)
Office:       20 variants (2 eras x 2 density x 5 types)
Service:      40 variants (fire, police, hospital, school, etc.)
Infrastructure: 30 variants (power, water, waste, telecom)
Special:      25 variants (landmarks, arenas, monuments)
Total:        ~260 unique building types
```

3. **Cultural Variants**:
   - Each culture can have unique building skins
   - Same stats, different art
   - E.g., European residential vs. Asian residential vs. Caribbean residential
   - Art swaps based on city's cultural archetype

4. **Building Footprints**:
   - 1x1 (small residential, shops)
   - 2x1 (medium buildings)
   - 2x2 (large buildings, factories)
   - 3x3 (major infrastructure: hospital, university, stadium)
   - 4x4 (mega buildings: airport, power plant)

### AI Tool Usage
- **Claude**: Generate entire building database as JSON (260+ entries)
- **Claude**: Generate cultural variant mapping table
- **Art AI**: Generate building sprite concepts for each era
- **Kimi**: NOT needed for data generation

### Output Files
- `data/buildings.json` (master building database)
- `data/building_cultural_variants.json`
- `scripts/simulation/core/BuildingRegistry.cs`

---

## Phase 3: Building Placement & Construction (Day 8-9)

### Tasks
1. **Placement System**:
   - Click to place service/infrastructure buildings
   - Ghost preview (semi-transparent sprite at cursor)
   - Green highlight = valid placement, Red = invalid
   - Invalid reasons: blocked terrain, no road access, not enough money, missing prerequisite
   - Placement cost deducted from budget
   - Snap-to-grid for all placements

2. **Construction Process** (from MISSING_SYSTEMS.md):
   - Phase 1: Site preparation (clear terrain) - 1 week
   - Phase 2: Foundation (scaffold sprite) - 2 weeks
   - Phase 3: Structure (partial building sprite) - varies
   - Phase 4: Finishing (nearly complete) - 1 week
   - Phase 5: Operational
   - Construction disruption: noise, road closures, dust

3. **Demolition**:
   - Click to demolish
   - Cost: demolition fee (proportional to building size)
   - Rubble remains for 1 game month
   - Historic buildings: community protest if demolished (approval hit)
   - Bulldozer tool for mass demolition

4. **Building Aging** (from MISSING_SYSTEMS.md):
```
condition = 100 - (age_years * decay_rate * usage_factor * weather_factor / maintenance_factor)
decay_rate: 1.0 (wood), 0.5 (brick), 0.3 (concrete), 0.2 (steel)
maintenance_factor: 0.5 (deferred) to 2.0 (premium)
```
   - Visual aging: shader that adds wear, cracks, discoloration over time
   - Renovation: restore condition to 90%+ (costs money)

### AI Tool Usage
- **Claude**: Generate placement system with validation
- **Claude**: Generate construction phase system
- **Claude**: Generate aging/condition model
- **Kimi**: Screenshot of Cities Skylines building placement -> adapt ghost preview system
- **Art AI**: Generate construction phase sprites (scaffolding, partial buildings)

### Output Files
- `scripts/game/build_system/BuildingPlacer.gd`
- `scripts/game/build_system/ConstructionManager.gd`
- `scripts/game/build_system/DemolitionTool.gd`
- `scripts/simulation/core/BuildingCondition.cs`
- `assets/shaders/building_aging.gdshader`

---

## Phase 4: Land Value System (Day 9-10)

### Tasks
1. **Influence Map Calculation**:
```
land_value[tile] = base_terrain_value
    + sum(service_bonuses within radius)
    - sum(pollution_penalties within radius)
    - crime_penalty
    + transport_accessibility_bonus
    + park_proximity_bonus
    - industrial_proximity_penalty
    - noise_penalty
    * demand_multiplier
```

2. **Dirty Region Updates**:
   - Only recalculate affected chunks when something changes
   - Change triggers: building placed/demolished, service built, zone changed
   - Propagation: changes ripple outward (park built -> surrounding tiles update)

3. **Land Value Effects**:
   - High value -> expensive housing, luxury shops
   - Low value -> cheap housing, discount stores
   - Value gradient -> gentrification pressure
   - Commercial follows residential value (shops appear where wealth is)

### AI Tool Usage
- **Claude**: Generate influence map algorithm with dirty-flag optimization
- **Claude**: Generate land value overlay renderer
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/core/LandValueCalculator.cs`
- `scripts/render/overlays/LandValueOverlay.gd`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | GridData, TileMap, EventBus | Phase 1 |
| Agent 03 | RCI demand values | Phase 1 (can stub) |
| Agent 04 | Road connectivity (is tile road-accessible?) | Phase 1 (can stub) |
| Agent 06 | Service coverage values per tile | Phase 4 (can stub) |
| Agent 09 | Building sprites, construction sprites | Phase 3 (placeholder ok) |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| Agent 02 | Housing capacity, housing quality per building | C# API |
| Agent 03 | Commercial/industrial building data | C# API |
| Agent 04 | Building locations (for O-D matrix) | C# data |
| Agent 06 | Building fire risk, crime attractors | C# API |
| Agent 08 | Building info panel data, zone overlay | C# API |
| Agent 09 | Sprite requirements list (what buildings need art) | JSON |

---

## Estimated Duration
- **With AI**: 6-7 days
- **Without AI**: 3-4 weeks
- **Can start**: After Agent 01 Phase 2 (Day 2)
