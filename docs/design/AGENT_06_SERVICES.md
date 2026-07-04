# AGENT 06: CITY SERVICES & UTILITIES

## Role
Build all city service systems: fire, police, healthcare, education, childcare, emergency services, utilities (power, water, sewer, waste, internet), and infrastructure condition tracking.

---

## Prerequisites
- Agent 01: GridData, ChunkManager, EventBus
- Agent 05 (partial): Building data, fire risk values

---

## Phase 1: Service Coverage Model (Day 5-6)

### Tasks
1. **Coverage Calculation**:
```csharp
// Per service building, compute coverage influence on surrounding tiles
public static float CalculateCoverage(Vector2I servicePos, Vector2I targetPos,
    int baseRadius, float quality, float staffing) {
    float distance = servicePos.DistanceTo(targetPos);
    if (distance > baseRadius) return 0f;
    float falloff = 1.0f - (distance / baseRadius);
    return falloff * quality * staffing;
}
```
   - Coverage is NOT binary -- it falls off with distance
   - Overlapping coverage from multiple stations stacks (diminishing returns)
   - Quality affected by: funding level, staffing, building condition
   - Understaffing: coverage radius shrinks proportionally

2. **Service Budget Allocation**:
   - Each service has a funding slider (50%-200% of baseline)
   - 50% = skeleton crew, reduced coverage, longer response times
   - 100% = standard operations
   - 200% = elite service, maximum coverage, fastest response

3. **Staffing**:
   - Each service building needs workers (education requirement varies)
   - Unfilled positions = degraded service
   - Workers commute from home (affected by transit)
   - Shift work for 24/7 services (fire, police, hospital)

### AI Tool Usage
- **Claude**: Generate coverage model with falloff curves
- **Claude**: Generate staffing/budget interaction formulas
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/services/CoverageCalculator.cs`
- `scripts/simulation/services/ServiceBudgetManager.cs`
- `scripts/simulation/services/StaffingManager.cs`

---

## Phase 2: Fire Department (Day 6-8)

### Tasks (from MISSING_SYSTEMS.md)
1. **Fire Station Tiers**:
```
Tier 1: Volunteer station     (Frontier)  - 8 tile radius, 6 min response
Tier 2: Professional station  (Industrial)- 12 tile radius, 4 min response
Tier 3: Full station          (Postwar)   - 15 tile radius, 3 min response
Tier 4: Advanced station      (Modern)    - 18 tile radius, 2 min response
Tier 5: Smart fire station    (Future)    - 22 tile radius, 1.5 min response
```

2. **Fire Spread Model**:
```
spread_chance = base * material_factor * wind_factor * adjacency * hydrant_inverse
base = 0.3 per game hour while burning
material_factor: 1.5 (wood), 1.0 (brick), 0.5 (concrete), 0.3 (steel)
wind_factor: 0.5 (calm) to 2.0 (high wind)
adjacency: 1.0 per adjacent burning building
hydrant_inverse: 0.5 if hydrant within 3 tiles, 1.0 otherwise
```

3. **Response Time Impact**:
```
damage = base_damage * (1 + 0.15 * response_minutes)
Every minute delay = +15% total damage
```

4. **Fire Hydrant System**:
   - Placed along roads (every 5-8 tiles recommended)
   - Connected to water system
   - No hydrant = 2x fire damage in area
   - Visual: small sprite on road edge

5. **Wildfire** (seasonal):
   - Dry summers increase forest fire risk
   - Spreads through unbuilt forest tiles
   - Firebreaks (cleared land) stop spread
   - Requires helicopter (Modern+) for large wildfires

### AI Tool Usage
- **Claude**: Generate fire spread simulation, response time model
- **Claude**: Generate fire event system with visual effects
- **Kimi**: NOT needed for simulation

### Output Files
- `scripts/simulation/services/fire/FireDepartment.cs`
- `scripts/simulation/services/fire/FireSpreadModel.cs`
- `scripts/simulation/services/fire/HydrantSystem.cs`
- `data/fire_stations.json`

---

## Phase 3: Police & Justice (Day 8-9)

### Tasks
1. **Police Station Tiers**:
   - Patrol post (Frontier) -> Precinct (Industrial) -> Full station (Postwar) -> Smart station (Future)

2. **Crime Model**:
```
crime_rate[tile] = base_crime
    * (1 - police_coverage * 0.7)
    * (1 + unemployment * 0.5)
    * (1 + poverty * 0.3)
    * (1 - education * 0.2)
    * (1 - lighting * 0.1)
    * (1 + abandoned_buildings * 0.4)
```

3. **Crime Types**:
   - Petty crime (shoplifting, vandalism) - affects commercial
   - Violent crime (assault, robbery) - affects residential happiness
   - Organized crime (drug trade) - corruption, requires special enforcement
   - White-collar crime (fraud) - affects economy at higher wealth levels

4. **Justice Pipeline** (from MISSING_SYSTEMS.md):
   - Crime -> Arrest -> Court -> Sentence -> Prison/Community service -> Release
   - Recidivism: 60% base, reduced by education + employment programs
   - Court buildings: needed for justice processing
   - Prison: capacity-limited, overcrowding = riots

### AI Tool Usage
- **Claude**: Generate crime model, justice pipeline simulation
- **Claude**: Generate crime overlay renderer
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/services/police/PoliceDepartment.cs`
- `scripts/simulation/services/police/CrimeModel.cs`
- `scripts/simulation/services/police/JusticeSystem.cs`
- `data/police_buildings.json`

---

## Phase 4: Healthcare System (Day 9-10)

### Tasks
1. **Healthcare Tiers**:
   - Clinic (Frontier) -> Hospital (Industrial) -> Medical center (Postwar) -> Research hospital (Modern) -> Smart hospital (Future)

2. **Health Model**:
```
citizen_health = base_health
    - pollution_exposure * 0.3
    - commute_stress * 0.1
    + healthcare_access * 0.4
    + food_quality * 0.1
    + exercise_access * 0.1   (parks, sports facilities)
```

3. **Disease System**:
   - Waterborne: from sewage overflow, contaminated water
   - Airborne: from pollution, crowding
   - Pandemic events: special disease that spreads city-wide
   - Vaccination programs (Modern+): reduce disease impact

4. **EMS** (from MISSING_SYSTEMS.md):
   - Ambulance response time model
   - Survival rate: 95% (<4 min), 80% (4-8 min), 50% (8-12 min), 20% (>12 min)
   - Ambulance routes affected by traffic congestion

### AI Tool Usage
- **Claude**: Generate health model, disease system, EMS simulation
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/services/health/HealthcareSystem.cs`
- `scripts/simulation/services/health/DiseaseModel.cs`
- `scripts/simulation/services/health/EMSSystem.cs`
- `data/health_buildings.json`

---

## Phase 5: Education System (Day 10-11)

### Tasks (from EDUCATION_MEDIA_SYSTEM.md)
1. **School Types**:
   - Daycare (Postwar+): Ages 0-5, frees parents for work
   - Elementary school (Frontier+): Ages 6-13
   - High school (Industrial+): Ages 14-17
   - Vocational school (Industrial+): Alternative to university, skilled trades
   - University (Postwar+): Ages 18-22, produces educated workers + research
   - Community college (Modern+): Adult education, reskilling

2. **School Capacity & Quality**:
   - Each school has student capacity
   - Overcrowding degrades quality
   - Teacher-student ratio affects outcomes
   - School bus system (from transport agent): extends catchment area

3. **Education Effects**:
   - Higher education -> better jobs -> higher income -> higher taxes
   - Uneducated workers limited to manual labor + low-tier service
   - Brain drain: educated citizens leave if no matching jobs

### AI Tool Usage
- **Claude**: Generate education pipeline simulation
- **Reference**: EDUCATION_MEDIA_SYSTEM.md for full spec
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/services/education/EducationSystem.cs`
- `scripts/simulation/services/education/SchoolManager.cs`
- `data/education_buildings.json`

---

## Phase 6: Utilities (Day 11-13)

### Tasks
1. **Power Grid**:
   - Power plants: Coal -> Oil -> Gas -> Nuclear -> Solar -> Wind -> Fusion
   - Power lines connect plants to zones
   - Brownouts if demand > supply (buildings lose power)
   - Grid visualization overlay

2. **Water System**:
   - Water pumps (at rivers/wells) -> Treatment plant -> Pipe network
   - Pressure model: drops with distance from pump
   - Contamination from industrial runoff
   - Water tower: boost pressure in area

3. **Sewer System**:
   - Sewer pipes (follow roads typically)
   - Treatment plant capacity
   - Overflow = pollution + disease risk
   - Stormwater: heavy rain can overwhelm combined sewers

4. **Waste Management**:
   - Garbage trucks collect from buildings
   - Landfill (cheap, polluting, finite) vs. Recycling plant (expensive, clean)
   - Incinerator (Modern+): burns waste for energy, some pollution
   - Waste-to-energy (Future): clean, generates power

5. **Internet/Telecom** (from MISSING_SYSTEMS.md):
   - Telegraph (Industrial) -> Telephone (Postwar) -> Internet (Modern) -> Fiber (Modern) -> 5G (Future)
   - Required for: office zones, tech industry, smart infrastructure
   - Coverage map overlay

### AI Tool Usage
- **Claude**: Generate all utility simulation models
- **Claude**: Generate pipe/wire network graph systems
- **Claude**: Generate utility overlay renderers
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/services/utilities/PowerGrid.cs`
- `scripts/simulation/services/utilities/WaterSystem.cs`
- `scripts/simulation/services/utilities/SewerSystem.cs`
- `scripts/simulation/services/utilities/WasteManagement.cs`
- `scripts/simulation/services/utilities/TelecomNetwork.cs`
- `data/utility_buildings.json`
- `data/power_plants.json`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | GridData, ChunkManager, EventBus | Phase 1 |
| Agent 04 | Response times (road distance for emergency vehicles) | Phase 2 (can stub) |
| Agent 05 | Building fire risk, building data | Phase 2 (can stub) |
| Agent 03 | Budget allocation per service | Phase 1 (can stub) |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| Agent 02 | Healthcare coverage, education capacity, utility status | C# API |
| Agent 03 | Service operating costs, utility revenue | C# API |
| Agent 05 | Service coverage per tile (for land value) | C# API |
| Agent 07 | Service quality metrics (for voter satisfaction) | C# API |
| Agent 08 | All service overlays, utility status panels | C# API |

---

## Estimated Duration
- **With AI**: 7-8 days
- **Without AI**: 5-6 weeks
- **Can start**: After Agent 01 Phase 2 (Day 2)
