# AGENT 02: POPULATION SIMULATION

## Role
Build the household-based population simulation: birth, aging, death, education, employment, migration, cultural DNA, and the demographic lifecycle that drives the entire game.

---

## Prerequisites
- Agent 01 output: GridData, SimulationLoop, DoubleBuffer, EventBus

---

## Phase 1: Household Data Model (Day 3-4)

### Tasks
1. **Household Structure** (C#):
```csharp
public struct Household {
    public uint Id;
    public byte Size;                // 1-8 members
    public byte WealthClass;         // 0=destitute, 1=poor, 2=working, 3=middle, 4=upper, 5=elite
    public ushort HomeBuilding;      // BuildingInstanceId
    public ushort Workplace1;        // Primary earner workplace
    public ushort Workplace2;        // Secondary earner workplace
    public byte Satisfaction;        // 0-100
    public byte CulturalProfile;     // index into cultural preset table
    public ushort Flags;             // bitflags: has_car, has_children, retired, etc
}

public struct Citizen {
    public uint HouseholdId;
    public byte Age;                 // 0-100
    public byte Education;           // 0=none, 1=primary, 2=secondary, 3=university, 4=postgrad
    public byte Employment;          // 0=child, 1=student, 2=unemployed, 3=employed, 4=retired
    public byte Health;              // 0-100
    public byte SkillType;           // job sector specialization
}
```

2. **Population Pool**: Array-of-Structs for cache-friendly iteration
   - Target: 20,000 households max (representing ~100k-200k pop)
   - Citizens stored separately, linked by HouseholdId
   - Staggered updates: process 1/30th of households per tick (monthly)

### AI Tool Usage
- **Claude**: Generate all C# data structures with SOA layout
- **Claude**: Generate staggered update scheduler
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/population/Household.cs`
- `scripts/simulation/population/Citizen.cs`
- `scripts/simulation/population/PopulationPool.cs`

---

## Phase 2: Lifecycle Simulation (Day 4-5)

### Tasks
1. **Birth Model**:
```
birth_rate = base_rate * healthcare_modifier * housing_modifier * wealth_modifier * cultural_modifier
base_rate = 0.015/month (per household with adults 18-45)
healthcare_modifier = 0.7 (no clinic) to 1.3 (hospital)
housing_modifier = 0.5 (overcrowded) to 1.2 (spacious)
wealth_modifier = 0.8 (poor - less access) to 1.1 (middle) to 0.7 (rich - fewer kids)
cultural_modifier = 0.6 (low collectivism) to 1.4 (high collectivism)
```

2. **Aging**: Citizens age 1 year per game year. Age affects:
   - 0-5: Needs daycare/stay-home parent
   - 6-13: Needs elementary school
   - 14-17: Needs high school
   - 18-22: University (optional, based on wealth + culture)
   - 23-64: Working age
   - 65+: Retired (pension drain, healthcare need)

3. **Death Model**:
```
death_probability = base_by_age * healthcare_inverse * pollution_factor * happiness_factor
base_by_age: 0.001 (0-40), 0.005 (40-60), 0.02 (60-75), 0.08 (75-85), 0.20 (85+)
healthcare_inverse: 2.0 (no healthcare) to 0.5 (full hospital)
pollution_factor: 1.0 (clean) to 1.5 (heavy pollution)
happiness_factor: 1.0 (happy) to 1.3 (miserable)
```

4. **Education Pipeline**:
   - Children auto-enroll in nearest school with capacity
   - School quality affects education level achieved
   - University graduates become skilled workers (higher tax, different job needs)
   - Brain drain: educated citizens leave if no matching jobs

5. **Employment**:
   - Job matching: citizens seek jobs matching education + skill
   - Commute tolerance: won't travel >45 min (affected by transit quality)
   - Unemployment effects: crime +, happiness -, tax revenue -, migration out
   - Retirement: pension system (city expense)

### AI Tool Usage
- **Claude**: Generate all lifecycle formulas, stochastic models
- **Claude**: Generate job matching algorithm (nearest compatible workplace within commute range)
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/population/LifecycleSimulator.cs`
- `scripts/simulation/population/EducationPipeline.cs`
- `scripts/simulation/population/EmploymentMatcher.cs`
- `scripts/simulation/population/DeathSimulator.cs`

---

## Phase 3: Migration & Housing (Day 5-6)

### Tasks
1. **Immigration Model**:
```
immigration_rate = base * job_availability * housing_availability * reputation * tax_modifier
base = 5 households/month (scales with city size)
job_availability = unemployed_jobs / total_jobs (0 if no jobs, capped at 2.0)
housing_availability = empty_homes / total_homes (0 if no housing)
reputation = function(happiness, services, crime, pollution) 0.0-2.0
tax_modifier = 0.5 (high tax) to 1.5 (low tax)
```

2. **Emigration Model**:
```
emigration_per_household = base * dissatisfaction * unemployment * housing_quality_inverse
Households leave when satisfaction < 30 for 6+ months
Educated households leave faster (more options)
Cultural attachment slows emigration (high Cultural Pride)
```

3. **Housing Market**:
   - 12 housing types from shack to mansion
   - Rent/price determined by: land value, building quality, neighborhood services
   - Overcrowding: >2 people per room = happiness penalty
   - Homelessness when no affordable housing available
   - Gentrification: rising land value pushes out poor households

4. **Wealth Dynamics**:
   - Income from employment (sector-dependent)
   - Expenses: rent, taxes, transport, food
   - Wealth class can change over time (upward or downward mobility)
   - Inheritance when household members die

### AI Tool Usage
- **Claude**: Generate migration models, housing market simulation
- **Claude**: Generate wealth mobility calculations
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/population/MigrationSimulator.cs`
- `scripts/simulation/population/HousingMarket.cs`
- `scripts/simulation/population/WealthDynamics.cs`

---

## Phase 4: Cultural DNA System (Day 6-7)

### Tasks
1. **8 Cultural Dimensions** (per city, 0-100 each):
   - Work Ethic, Collectivism, Risk Tolerance, Environmental Values
   - Social Trust, Progressivism, Hierarchy Acceptance, Cultural Pride

2. **Initial Values**: Set by map/scenario selection (12 regional presets)

3. **Cultural Drift**:
```
new_value = old_value + sum(drift_sources) * (1.0 / population_inertia)
population_inertia = log2(population / 1000) + 1
```
   - Drift sources: immigration, education, prosperity, events, media, laws

4. **Culture -> Behavior Mapping**:
   - High Work Ethic: +productivity, -leisure demand, +workaholism
   - High Collectivism: +transit acceptance, +tax tolerance, +community events
   - Low Social Trust: +crime, +corruption, -civic participation
   - High Environmental Values: +green protest, -industry acceptance, +park demand

5. **Archetype Detection**:
   - Check 20 archetype activation conditions each game year
   - Award archetype bonuses when thresholds met
   - Allow archetype transitions (e.g., Industrial -> Rust Belt)

### AI Tool Usage
- **Claude**: Generate cultural dimension system, drift formulas, archetype detector
- **Reference**: CULTURAL_DNA_EMERGENCE.md for all values and thresholds
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/population/CulturalDNA.cs`
- `scripts/simulation/population/CulturalDrift.cs`
- `scripts/simulation/population/ArchetypeDetector.cs`
- `data/cultural_presets.json`

---

## Phase 5: Satisfaction & Happiness (Day 7-8)

### Tasks
1. **Per-Household Satisfaction** (0-100):
```
satisfaction = weighted_average(
    employment_score * 0.20,
    housing_score * 0.15,
    commute_score * 0.12,
    services_score * 0.12,
    safety_score * 0.10,
    environment_score * 0.08,
    education_score * 0.08,
    leisure_score * 0.05,
    tax_fairness * 0.05,
    cultural_alignment * 0.05
)
```

2. **Need Hierarchy**:
   - Basic needs (housing, food, safety) have higher weight
   - Higher needs (education, leisure, culture) matter more at higher wealth
   - Maslow-like: unmet basic needs override everything

3. **Aggregate Metrics**:
   - City-wide happiness (population-weighted average)
   - Per-district happiness
   - Per-wealth-class happiness
   - Happiness trends (improving/declining over 12 months)

### AI Tool Usage
- **Claude**: Generate satisfaction calculator with weighted scoring
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/population/SatisfactionCalculator.cs`
- `scripts/simulation/population/NeedHierarchy.cs`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | GridData, SimulationLoop, EventBus | Phase 1 |
| Agent 03 | Job data, wage data, tax rates | Phase 2 (can stub) |
| Agent 04 | Commute times per household | Phase 2 (can stub) |
| Agent 06 | Service coverage per tile | Phase 5 (can stub) |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| Agent 03 | Labor pool, consumer demand, tax base | C# API |
| Agent 04 | Commute origins/destinations (home->work) | C# data |
| Agent 05 | Residential demand signal (need X housing type) | Signal |
| Agent 06 | Population density, age distribution per tile | C# API |
| Agent 07 | Voter preferences, cultural DNA values | C# API |
| Agent 08 | All demographic stats for UI display | C# API |

---

## Estimated Duration
- **With AI**: 5-6 days
- **Without AI**: 3-4 weeks
- **Can start**: After Agent 01 Phase 2 (Day 2)
- **Stub strategy**: Use hardcoded values for economy, transport, services until those agents deliver
