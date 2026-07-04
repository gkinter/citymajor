# AGENT 03: ECONOMY & INDUSTRY

## Role
Build the full economic simulation: city budget, taxation, commercial/industrial demand, 20 production chains, resource extraction, trade between cities, and the financial system.

---

## Prerequisites
- Agent 01: GridData, SimulationLoop, EventBus
- Agent 02 (partial): Population count, wealth distribution (can stub)

---

## Phase 1: Budget System (Day 3-5)

### Tasks
1. **Revenue Sources** (11 types):
```csharp
public class BudgetSystem {
    // Revenue
    float residentialTax;      // per household, scaled by wealth
    float commercialTax;       // per commercial building, scaled by profit
    float industrialTax;       // per industry building, scaled by output
    float salesTax;            // % of commercial transactions
    float propertyTax;         // based on land value
    float transitFares;        // per rider per trip
    float parkingFees;         // per parking space used
    float utilitySales;        // power/water to citizens (if public)
    float tradeTariffs;        // % of import/export value
    float tourismTax;          // per tourist per night
    float governmentGrants;    // era-dependent, milestone bonuses
}
```

2. **Expense Categories** (14 types):
   - Road maintenance, transit operations, power generation, water/sewer
   - Fire, police, healthcare, education, social services
   - Debt interest, government salaries, research funding
   - Emergency fund, capital projects

3. **Budget Balance**:
   - Monthly tick: revenue - expenses = surplus/deficit
   - Surplus -> treasury (capped at 2 years of expenses)
   - Deficit -> auto-loan at interest rate (increases with debt ratio)
   - Bankruptcy at debt > 5x annual revenue -> game over (or bailout event)

4. **Tax Slider UI Interface**:
   - Each tax has a slider (0-20%)
   - Real-time preview of revenue change
   - Happiness impact preview
   - Comparison to regional average

### AI Tool Usage
- **Claude**: Generate budget system, tax calculations, loan mechanics
- **Claude**: Research real-world city budget breakdowns for realistic defaults
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/economy/BudgetSystem.cs`
- `scripts/simulation/economy/TaxCalculator.cs`
- `scripts/simulation/economy/LoanManager.cs`
- `data/budget_defaults.json`

---

## Phase 2: Commercial & Industrial Demand (Day 5-6)

### Tasks
1. **RCI Demand Model**:
```
residential_demand = job_availability * immigration_pressure - housing_supply
commercial_demand = population * wealth_average * 0.3 - commercial_supply
industrial_demand = commercial_demand * 0.5 + export_demand - industrial_supply
```

2. **Commercial Buildings**:
   - 5 tiers: Corner shop -> Strip mall -> Department store -> Shopping center -> Mall
   - Each tier serves different population radius
   - Wealth-dependent: luxury stores in rich areas, discount in poor
   - Employment: 2-50 jobs per commercial building

3. **Industrial Buildings**:
   - 4 tiers: Workshop -> Factory -> Heavy industry -> Automated plant
   - Input/output defined by production chain
   - Pollution output proportional to tier
   - Employment: 5-200 workers per building (decreases with automation)

4. **Office Buildings** (Modern era+):
   - Requires fiber internet
   - Employs educated workers
   - Low pollution, high land value
   - Tech company HQ as special building

### AI Tool Usage
- **Claude**: Generate RCI demand calculations, building tier definitions
- **Reference**: GDD sections 5, 6 for zone and industry details
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/economy/DemandCalculator.cs`
- `scripts/simulation/economy/CommercialSimulator.cs`
- `scripts/simulation/economy/IndustrialSimulator.cs`
- `data/buildings_commercial.json`
- `data/buildings_industrial.json`

---

## Phase 3: Production Chains (Day 6-8)

### Tasks
1. **20 Production Chains** (from GDD):
```
Chain examples:
Iron Ore -> [Smelter] -> Steel -> [Factory] -> Machinery
Timber -> [Sawmill] -> Lumber -> [Furniture Factory] -> Furniture
Crude Oil -> [Refinery] -> Fuel + Plastics
Farm -> [Mill] -> Flour -> [Bakery] -> Food
Cotton -> [Textile Mill] -> Fabric -> [Garment Factory] -> Clothing
Sand -> [Glass Works] -> Glass -> [Electronics Factory] -> Electronics
Coal + Iron -> [Steel Mill] -> Steel -> [Auto Factory] -> Vehicles
Limestone -> [Cement Plant] -> Concrete -> [Construction] -> Buildings
```

2. **Resource Extraction**:
   - 14 raw materials: iron, coal, copper, oil, limestone, clay, sand, fertile soil, timber, gold, uranium, rare earth, fish, stone
   - Deposit depletion: finite resources that run out over decades
   - Extraction buildings: mine, quarry, oil well, lumber camp, fishing dock, farm
   - Extraction rate affected by technology level

3. **Logistics**:
   - Goods move from producer to consumer via road/rail/port
   - Freight traffic: production chain flows create truck/train movements
   - Storage: warehouse buildings buffer supply/demand mismatches
   - Spoilage: food products decay if not consumed in time

4. **Trade System**:
   - Export surplus goods (auto-sell at market price)
   - Import needed goods (auto-buy at market price + transport cost)
   - Market prices fluctuate based on global supply/demand events
   - Trade routes between player cities (if multi-city)
   - Trade agreements unlock better prices

### AI Tool Usage
- **Claude**: Generate all 20 production chains as data
- **Claude**: Generate logistics pathfinding (freight routing)
- **Claude**: Generate market price simulation with volatility
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/economy/ProductionChain.cs`
- `scripts/simulation/economy/ResourceExtraction.cs`
- `scripts/simulation/economy/FreightLogistics.cs`
- `scripts/simulation/economy/TradeSystem.cs`
- `scripts/simulation/economy/MarketPrices.cs`
- `data/production_chains.json`
- `data/resources.json`

---

## Phase 4: Financial System (Day 8-9)

### Tasks
1. **Banking** (from MISSING_SYSTEMS.md):
   - Bank buildings: Community bank -> Regional bank -> Investment bank
   - Mortgage system: citizens need mortgages for housing
   - Interest rates: set by player (affects growth vs. stability)
   - Banking crises: triggered by over-lending + economic downturn

2. **Insurance**:
   - Property insurance (reduces disaster recovery cost)
   - Health insurance (supplements healthcare)
   - Business insurance (reduces bankruptcy cascades)
   - Insurance pool: premium income vs. claim payouts

3. **Property Market**:
   - Land value calculation: `base * services * transport * pollution_inv * demand`
   - Property value = land value * building quality * age factor
   - Speculation: rapid price increases can create bubbles
   - Bubble burst: 30%+ crash, construction halts, negative equity

4. **Poverty & Welfare**:
   - Welfare programs: unemployment benefits, food assistance, housing vouchers
   - Cost: per-recipient per-month
   - Effect: prevents homelessness, reduces crime, but costs money
   - Political dimension: welfare expansion vs. austerity

### AI Tool Usage
- **Claude**: Generate financial simulation models
- **Claude**: Research real housing market dynamics for crash mechanics
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/economy/BankingSystem.cs`
- `scripts/simulation/economy/InsuranceSystem.cs`
- `scripts/simulation/economy/PropertyMarket.cs`
- `scripts/simulation/economy/WelfareSystem.cs`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | GridData, SimulationLoop, EventBus | Phase 1 |
| Agent 02 | Population count, wealth distribution, employment | Phase 1 (can stub) |
| Agent 04 | Freight route costs, commute data | Phase 3 (can stub) |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| Agent 02 | Job availability, wages, cost of living | C# API |
| Agent 04 | Freight traffic demand (goods flow between buildings) | C# data |
| Agent 05 | RCI demand signals (what zones to grow) | Signal |
| Agent 06 | Budget allocation per service | C# API |
| Agent 07 | Tax revenue, economic health metrics | C# API |
| Agent 08 | All economic stats for budget panel, charts | C# API |

---

## Estimated Duration
- **With AI**: 6-7 days
- **Without AI**: 4-5 weeks
- **Can start**: After Agent 01 Phase 2 (Day 2)
