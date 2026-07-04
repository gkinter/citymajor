# CityMajor — Simulation Architecture Specification

> Pure C# (.NET 8), no engine. 1M tiles, 500K pop, 10-20 ticks/sec on 8-core CPU.

---

## Table of Contents

1. [Hierarchical Simulation (LOD)](#1-hierarchical-simulation)
2. [Core Data Structures](#2-core-data-structures)
3. [Spatial Partitioning](#3-spatial-partitioning)
4. [Traffic System](#4-traffic-system)
5. [Economic Simulation](#5-economic-simulation)
6. [Population System](#6-population-system)
7. [Service Coverage](#7-service-coverage)
8. [Overlay Systems](#8-overlay-systems)
9. [Threading Model](#9-threading-model)
10. [Memory Budget](#10-memory-budget)
11. [Scaling Benchmarks](#11-scaling-benchmarks)
12. [Event & Disaster System](#12-event-system)
13. [Law & Compliance](#13-law-compliance)
14. [Technology & Research](#14-technology-research)
15. [Sports Leagues](#15-sports-leagues)
16. [Vehicle Cosmetic Routing](#16-vehicle-routing)
17. [Save/Load & Serialization](#17-serialization)

---

## 1. Hierarchical Simulation

### Tick Rate Foundation

Game speed mapping (configurable):

| Game Speed | Ticks/sec | Game-day | Game-month | Game-year |
|-----------|----------|---------|-----------|----------|
| Pause | 0 | - | - | - |
| Slow | 10 | 10 ticks | 300 ticks | 3,600 ticks |
| Normal | 15 | 15 ticks | 450 ticks | 5,400 ticks |
| Fast | 20 | 20 ticks | 600 ticks | 7,200 ticks |

One game-day = 1 real second at normal speed. One game-year = 6 real minutes.

### Level 0 — Every Tick (50-100ms budget)

**Systems**: Traffic flow, utility grid balance, active events/disasters, vehicle interpolation.

```csharp
// Pseudocode for Level 0 tick
struct L0TickResult {
    TrafficFlowSnapshot Traffic;       // BPR edge volumes
    UtilityBalance Utilities;          // power MW delta, water ML delta
    ActiveEventList Events;            // disaster progression
    VehiclePositions Vehicles;         // interpolated positions for renderer
}

void TickLevel0(SimState state, float dt) {
    // 1. Traffic: BPR formula on edge graph — O(|E|) where E ~ 50K
    //    v/c ratio per edge, update travel times
    //    NO pathfinding here — paths are cached from L1
    ParallelFor(state.RoadEdges, edge => {
        float vc = edge.Volume / edge.Capacity;
        edge.TravelTime = edge.FreeFlowTime * (1 + 0.15f * MathF.Pow(vc, 4));
    });

    // 2. Utility grids: sum supply - sum demand per grid partition
    //    Power: check MW balance per substation zone
    //    Water: check ML/tick balance per pump zone
    //    O(partitions) ~ O(1024) not O(tiles)
    ParallelFor(state.GridPartitions, p => {
        p.PowerBalance = p.PowerSupply - p.PowerDemand;
        p.WaterBalance = p.WaterSupply - p.WaterDemand;
        if (p.PowerBalance < 0) p.BlackoutTimer += dt;
        if (p.WaterBalance < 0) p.WaterShortageTimer += dt;
    });

    // 3. Active events: tick progression, spread calculation
    //    Typically 0-3 active events — negligible cost
    foreach (var evt in state.ActiveEvents) {
        evt.Tick(state, dt);
    }

    // 4. Vehicle interpolation: lerp 10K vehicles along cached paths
    //    O(vehicles) ~ O(10K), each is a single lerp
    ParallelFor(state.Vehicles, v => {
        v.Progress += dt * v.Speed / v.PathSegmentLength;
        if (v.Progress >= 1.0f) v.AdvanceSegment();
    });
}
```

**Budget**: ~1.5ms on 8 cores. Traffic BPR: 0.8ms. Utilities: 0.1ms. Events: 0.01ms. Vehicles: 0.3ms. Overhead: 0.3ms.

### Level 1 — Every Game-Day (~15 ticks)

**Systems**: Economy tick, construction progress, zone growth/decline, traffic assignment refresh.

```csharp
void TickLevel1(SimState state) {
    // 1. Economy: Leontief I-O snapshot
    //    45x45 matrix solve: ~0.02ms (trivial)
    //    Aggregate buy/sell from 50K buildings into market zones: O(buildings)
    //    8-16 market zones, each solves independently
    ParallelFor(state.MarketZones, zone => {
        zone.AggregateDemand();    // sum from buildings in zone
        zone.AggregateSupply();    // sum from buildings in zone
        zone.SolveLeontief();      // 45x45 matrix, ~20μs
        zone.UpdatePrices();       // price adjustment toward equilibrium
        zone.SettleTrades();       // transfer goods, deduct money
    });

    // 2. Construction: advance build timers
    //    Typically < 500 active constructions
    foreach (var site in state.ConstructionSites) {
        site.Progress += site.BuildRate;
        if (site.Progress >= 1.0f) site.Complete(state);
    }

    // 3. Zone growth: check demand vs supply per zone type
    //    Per partition (1024 partitions), check RCI demand
    ParallelFor(state.GridPartitions, p => {
        p.EvaluateZoneGrowth(state.Economy); // may queue building spawn
    });

    // 4. Traffic assignment: Frank-Wolfe iteration
    //    Rebuild O-D flows if road network changed (dirty flag)
    if (state.RoadNetwork.IsDirty) {
        state.TrafficAssigner.Rebuild(state.TrafficZones); // ~5ms
        state.RoadNetwork.IsDirty = false;
    } else {
        state.TrafficAssigner.Iterate(); // 1 Frank-Wolfe iteration, ~1ms
    }
}
```

**Budget**: ~3ms. Economy: 1ms. Construction: 0.05ms. Zone growth: 0.5ms. Traffic assign: 1ms. Overhead: 0.45ms.

### Level 2 — Every Game-Month (~450 ticks)

**Systems**: Population lifecycle, migration, employment, satisfaction, culture, research, politics.

```csharp
void TickLevel2(SimState state) {
    // 1. Population lifecycle: age all citizens, process births/deaths
    //    500K citizens but only check mortality/fertility tables
    //    Staggered: already processed 1/30th per tick via L0 household updates
    //    Here we do the AGGREGATE pass: demographic summary
    state.Demographics.Recalculate(state.Citizens);

    // 2. Migration: compare city attractiveness vs global baseline
    //    Net migration = f(jobs, housing, satisfaction, taxes, services)
    int netMigration = state.MigrationModel.Calculate(state);
    state.MigrationModel.Apply(state, netMigration); // spawn/remove households

    // 3. Employment matching: unmatched workers seek jobs
    //    O(unemployed) typically 2-8% of workforce = 10K-40K
    //    Batch by zone, match to nearest available job
    state.LaborMarket.MatchUnemployed(state);

    // 4. Satisfaction: zone-level aggregates applied to households
    //    Pre-computed from overlays (crime, pollution, services, etc.)
    ParallelFor(state.GridPartitions, p => {
        p.CalculateSatisfaction(state.Overlays);
    });
    // Then propagate to households in batches
    ParallelFor(state.HouseholdBatches, batch => {
        foreach (var h in batch)
            h.Satisfaction = state.GetPartition(h.Position).Satisfaction
                           + h.PersonalModifiers;
    });

    // 5. Cultural DNA drift: shift cultural values per neighborhood
    ParallelFor(state.GridPartitions, p => {
        p.CultureDrift(state.Laws, state.Events);
    });

    // 6. Research: advance tech progress
    state.ResearchSystem.Tick(state);

    // 7. Political approval: recalculate mayor approval
    state.Politics.RecalculateApproval(state);
}
```

**Budget**: ~8ms (runs once per 450 ticks, amortized to ~0.02ms/tick). Demographics: 2ms. Migration: 0.5ms. Employment: 3ms. Satisfaction: 1ms. Culture: 0.5ms. Research: 0.1ms. Politics: 0.1ms.

### Level 3 — Every Game-Year (~5400 ticks)

**Systems**: Archetype detection, era transitions, infrastructure aging, sports, city rating.

```csharp
void TickLevel3(SimState state) {
    // 1. Archetype detection: classify city into 1 of 20 archetypes
    //    Based on aggregate stats (economy mix, culture, density, etc.)
    //    Pure math on ~50 aggregate values — instant
    state.CityArchetype = ArchetypeClassifier.Detect(state);

    // 2. Era transition: check if conditions met for next era
    state.EraSystem.CheckTransition(state);

    // 3. Infrastructure aging: degrade buildings, roads, pipes
    //    50K buildings, batch parallel
    ParallelFor(state.Buildings, b => {
        b.Condition -= b.AgingRate;
        if (b.Condition < b.CollapseThreshold)
            b.QueueRepairOrDemolish();
    });

    // 4. Sports season: simulate 11 leagues
    foreach (var league in state.SportsLeagues) {
        league.SimulateSeason(state); // ~0.1ms per league
    }

    // 5. City rating: composite score for achievements/rankings
    state.CityRating = CityRatingCalculator.Calculate(state);
}
```

**Budget**: ~5ms (runs once per 5400 ticks, amortized to ~0.001ms/tick).

### Tick Orchestrator

```csharp
class SimulationOrchestrator {
    int _tickCount;
    int _ticksPerDay;      // 15 at normal speed
    int _daysPerMonth;     // 30
    int _monthsPerYear;    // 12

    void Tick(SimState state, float dt) {
        _tickCount++;

        // L0: every tick
        TickLevel0(state, dt);

        // L1: every game-day
        if (_tickCount % _ticksPerDay == 0)
            TickLevel1(state);

        // L2: every game-month
        if (_tickCount % (_ticksPerDay * _daysPerMonth) == 0)
            TickLevel2(state);

        // L3: every game-year
        if (_tickCount % (_ticksPerDay * _daysPerMonth * _monthsPerYear) == 0)
            TickLevel3(state);

        // Produce snapshot for renderer (lock-free double buffer)
        state.PublishSnapshot();
    }
}
```

---

## 2. Core Data Structures

### Tile (32 bytes, SoA layout)

```csharp
// Structure-of-Arrays for cache-friendly iteration
// 1,048,576 tiles (1024x1024)
struct TileData {
    // Array per field — iterate one field across all tiles without loading others
    byte[]   TerrainType;    // 1M bytes — grass, water, rock, sand, snow (enum, 8 values)
    byte[]   ZoneType;       // 1M bytes — none, residential, commercial, industrial, office, mixed (enum)
    byte[]   ZoneDensity;    // 1M bytes — low=1, medium=2, high=3
    byte[]   Elevation;      // 1M bytes — 0-255 (0.25m per step = 64m range)
    ushort[] BuildingId;     // 2M bytes — 0 = empty, else index into building array (max 65K)
    byte[]   RoadType;       // 1M bytes — none, dirt, 2lane, 4lane, highway, rail, tram
    byte[]   WaterPipeFlag;  // 1M bytes — bitmask: hasPipe | hasWater | hasSewage
    byte[]   PowerLineFlag;  // 1M bytes — bitmask: hasLine | hasPower
    byte[]   TreeDensity;    // 1M bytes — 0-255 trees per tile
    byte[]   PollutionLevel; // 1M bytes — 0-255
    byte[]   CrimeLevel;     // 1M bytes — 0-255
    byte[]   LandValue;      // 1M bytes — 0-255 (mapped to currency range)
    byte[]   NoiseLevel;     // 1M bytes — 0-255
    byte[]   Desirability;   // 1M bytes — composite score 0-255
    ushort[] PartitionId;    // 2M bytes — which 32x32 partition this tile belongs to
    byte[]   Flags;          // 1M bytes — bitmask: onFire | flooded | condemned | historic
    // Padding to 32 bytes per tile average
    // Total: ~18M for core tile data (well under 32M budget)
}
```

**Why SoA**: When computing pollution overlay, we iterate `PollutionLevel[0..1M]` contiguously. L1 cache line = 64 bytes = 64 pollution values. With AoS (struct-per-tile), each cache line loads 2 tiles (32B each) but we only need 1 byte from each. SoA is 32x more cache-efficient for single-field scans.

### Household (64 bytes, AoS)

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Household {                    // 64 bytes exactly
    uint   Id;                        // 4B — unique household ID
    ushort HomeBuildingId;            // 2B — building where they live
    ushort WorkBuildingId1;           // 2B — primary earner workplace
    ushort WorkBuildingId2;           // 2B — secondary earner workplace (0 = none)
    ushort PartitionId;              // 2B — spatial partition for fast lookup
    ushort TrafficZoneId;            // 2B — O-D zone for traffic
    byte   HouseholdSize;            // 1B — 1-8 members
    byte   AgeCategory;              // 1B — young/working/retired
    byte   IncomeLevel;              // 1B — 0-255 (mapped to bracket)
    byte   EducationLevel;           // 1B — none/primary/secondary/tertiary
    byte   CultureProfile;           // 1B — index into 20 cultural archetypes
    byte   PoliticalLeaning;         // 1B — 0=far-left .. 255=far-right
    short  Satisfaction;             // 2B — -10000..+10000 (fixed point, 0.01 resolution)
    short  HealthScore;              // 2B — 0..10000
    int    Savings;                  // 4B — household wealth in cents
    int    MonthlyIncome;            // 4B — after tax, in cents
    int    MonthlyExpenses;          // 4B — rent + goods + services, in cents
    ushort ChildSchoolId;            // 2B — school building (0 = none/no children)
    byte   CarOwnership;             // 1B — 0-4 cars
    byte   TransitPreference;        // 1B — car/bus/rail/bike/walk weights packed
    byte   ReligionId;               // 1B — 0 = none, 1-8 = religion types
    byte   EthnicityId;              // 1B — for cultural simulation
    byte   HousingPreference;        // 1B — density preference (suburb vs downtown)
    byte   Flags;                    // 1B — isImmigrant | isHomeless | isEvicted | wantsToMove
    uint   MoveInTick;               // 4B — when they moved to current home
    ushort LeisureBuildingId;        // 2B — preferred leisure destination
    ushort ShoppingBuildingId;       // 2B — preferred shopping destination
    ushort ClinicBuildingId;         // 2B — assigned healthcare
    ushort _padding;                 // 2B — alignment to 64 bytes
}
// 100,000 households x 64B = 6.25 MB — fits in L3 cache
```

### Citizen (32 bytes, AoS)

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Citizen {                      // 32 bytes
    uint   Id;                        // 4B
    uint   HouseholdId;              // 4B — back-reference
    ushort Age;                       // 2B — in game-months (0-1200 = 0-100 years)
    byte   Gender;                    // 1B
    byte   EducationLevel;           // 1B
    byte   HealthStatus;             // 1B — 0-255
    byte   HappinessStatus;          // 1B — 0-255
    byte   EmploymentStatus;         // 1B — employed/unemployed/student/retired/child
    byte   SkillCategory;            // 1B — unskilled/clerical/technical/professional/executive
    ushort WorkplaceId;              // 2B
    ushort SchoolId;                 // 2B
    byte   CommuteMode;              // 1B
    byte   CommuteTime;              // 1B — in game-minutes (0-255)
    uint   YearlyIncome;             // 4B — gross, in cents
    byte   CrimePropensity;          // 1B — 0-255 (influenced by satisfaction, income, policing)
    byte   Flags;                    // 1B — isPregnant | isSick | isInSchool | isRetired
    uint   _reserved;                // 4B — future use
    ushort _padding;                 // 2B
}
// 500,000 citizens x 32B = 15.26 MB
```

### Building (128 bytes, AoS)

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct Building {                     // 128 bytes
    uint   Id;                        // 4B
    ushort TypeId;                    // 2B — index into BuildingDefinition table
    ushort TileX;                     // 2B — anchor tile position
    ushort TileY;                     // 2B
    byte   Width;                     // 1B — footprint (1-8 tiles)
    byte   Height;                    // 1B — footprint (1-8 tiles)
    byte   Floors;                    // 1B — visual floors / density
    byte   Style;                     // 1B — architectural style (era-dependent)
    byte   Condition;                 // 1B — 0-255 (255 = new)
    byte   FireRisk;                  // 1B — 0-255
    ushort PartitionId;              // 2B
    ushort MarketZoneId;             // 2B

    // Capacity
    ushort ResidentialCapacity;      // 2B — max households (0 for non-residential)
    ushort WorkerCapacity;           // 2B — max jobs
    ushort ResidentialOccupancy;     // 2B — current households
    ushort WorkerOccupancy;          // 2B — current workers

    // Economy
    byte   ProductionChainId;        // 1B — which of 20 production chains (0 = none)
    byte   OutputGoodId;             // 1B — primary output good (0-44)
    ushort ProductionRate;           // 2B — units/game-day
    ushort StorageCapacity;          // 2B — max inventory
    ushort CurrentStorage;           // 2B — current inventory

    // Resource consumption/production per tick
    short  PowerDelta;               // 2B — negative = consumes, positive = produces (kW)
    short  WaterDelta;               // 2B — (liters/tick)
    short  SewageDelta;              // 2B
    short  GarbageDelta;             // 2B

    // Service buildings
    byte   ServiceType;              // 1B — police/fire/health/education/parks/transit/religion/govt
    byte   ServiceRadius;            // 1B — influence radius in tiles (0-255)
    ushort ServiceCapacity;          // 2B — people served
    ushort ServiceLoad;              // 2B — current load

    // Economics
    int    LandValue;                // 4B — current assessed value in cents
    int    PropertyTax;              // 4B — annual tax in cents
    int    Revenue;                  // 4B — monthly revenue for commercial/industrial
    int    OperatingCost;            // 4B — monthly cost

    // Construction
    byte   ConstructionState;        // 1B — planned/underConstruction/built/condemned/demolished
    byte   ConstructionProgress;     // 1B — 0-255 (0% to 100%)

    // Pollution
    byte   AirPollutionOutput;       // 1B
    byte   NoisePollutionOutput;     // 1B
    byte   WaterPollutionOutput;     // 1B

    // Flags
    byte   Flags;                    // 1B — isHistoric | isLandmark | onFire | noWater | noPower
    byte   UpgradeLevel;             // 1B — 0-5
    byte   HappinessBonus;           // 1B — parks, landmarks, etc.

    // Input goods (up to 4 input types for production chains)
    byte   InputGood0;               // 1B
    byte   InputGood1;               // 1B
    byte   InputGood2;               // 1B
    byte   InputGood3;               // 1B
    ushort InputRate0;               // 2B
    ushort InputRate1;               // 2B
    ushort InputRate2;               // 2B
    ushort InputRate3;               // 2B

    // Tech/law modifiers (computed, cached monthly)
    short  TechEfficiencyBonus;      // 2B — from researched techs, %*100
    short  LawComplianceCost;        // 2B — additional operating cost from regulations

    uint   BuiltOnTick;              // 4B — when constructed
    uint   LastMaintenanceTick;      // 4B — last repair

    fixed byte _reserved[6];         // 6B — pad to 128
}
// 50,000 buildings x 128B = 6.1 MB
```

### Road Graph

```csharp
struct RoadNode {                     // 24 bytes
    uint   Id;                        // 4B
    ushort TileX;                     // 2B
    ushort TileY;                     // 2B
    byte   NodeType;                  // 1B — intersection, endpoint, highway_on, highway_off
    byte   SignalType;                // 1B — none, stop, light, roundabout
    ushort SignalPhase;               // 2B — current phase timing
    uint   FirstEdgeIndex;            // 4B — index into edge array
    byte   EdgeCount;                 // 1B — number of edges from this node (degree)
    byte   Elevation;                 // 1B
    ushort PartitionId;              // 2B
    uint   _padding;                  // 4B
}
// 25,000 nodes x 24B = 586 KB

struct RoadEdge {                     // 32 bytes
    uint   Id;                        // 4B
    uint   FromNodeId;               // 4B
    uint   ToNodeId;                 // 4B
    ushort Length;                    // 2B — in tiles (0-65535)
    byte   RoadType;                  // 1B — dirt/2lane/4lane/highway/rail/tram
    byte   Lanes;                     // 1B — 1-8
    ushort Capacity;                 // 2B — vehicles/hour (per BPR model)
    ushort FreeFlowTime;             // 2B — seconds at speed limit (fixed point)
    ushort CurrentTravelTime;        // 2B — BPR-adjusted
    ushort Volume;                   // 2B — current vehicles/hour assigned
    ushort SpeedLimit;               // 2B — km/h
    byte   Condition;                 // 1B — 0-255 (affects speed)
    byte   Flags;                     // 1B — isOneWay | isBridge | isTunnel | isHighway
    ushort ContractionLevel;         // 2B — for Contraction Hierarchies
    ushort _padding;                 // 2B
}
// 50,000 edges x 32B = 1.53 MB
```

---

## 3. Spatial Partitioning

### Uniform Grid: 32x32 Tile Partitions

1024 / 32 = 32 partitions per axis = **1,024 total partitions**.

Each partition covers 1,024 tiles (32x32). This is the fundamental spatial unit for aggregation.

**Why 32x32 (not 64x64 or adaptive)**:

| Size | Count | Tiles/partition | Granularity | Update cost |
|------|-------|----------------|-------------|-------------|
| 16x16 | 4,096 | 256 | Too fine — overhead dominates | High |
| **32x32** | **1,024** | **1,024** | **Good balance** | **Moderate** |
| 64x64 | 256 | 4,096 | Too coarse — loses spatial detail | Low |
| Adaptive | ~500-2000 | Variable | Best accuracy | Complex, cache-unfriendly |

32x32 chosen because: 1024 partitions fit in a single L2 cache-friendly sweep, each partition is large enough to amortize overhead, small enough to give meaningful spatial resolution for overlays. A quadtree adds complexity with marginal benefit at this scale.

```csharp
struct Partition {                     // 256 bytes
    ushort Id;                         // 2B
    ushort GridX;                      // 2B — partition grid position (0-31)
    ushort GridY;                      // 2B
    ushort TileStartX;                // 2B — first tile coordinate
    ushort TileStartY;                // 2B

    // Aggregate demographics
    ushort Population;                 // 2B
    ushort HouseholdCount;            // 2B
    ushort WorkerCount;               // 2B
    ushort JobCount;                   // 2B

    // Zone distribution (tile counts)
    ushort ResidentialTiles;           // 2B
    ushort CommercialTiles;            // 2B
    ushort IndustrialTiles;            // 2B
    ushort OfficeTiles;                // 2B
    ushort MixedUseTiles;              // 2B
    ushort EmptyTiles;                 // 2B

    // Aggregate metrics (0-255 scale, area-weighted)
    byte   AvgLandValue;              // 1B
    byte   AvgPollution;              // 1B
    byte   AvgCrime;                  // 1B
    byte   AvgNoise;                  // 1B
    byte   AvgDesirability;           // 1B
    byte   AvgSatisfaction;           // 1B
    byte   AvgHealthScore;            // 1B
    byte   AvgEducation;              // 1B

    // Service coverage scores (0-255, computed from coverage maps)
    byte   PoliceCoverage;            // 1B
    byte   FireCoverage;              // 1B
    byte   HealthCoverage;            // 1B
    byte   EducationCoverage;         // 1B
    byte   ParkCoverage;              // 1B
    byte   TransitCoverage;           // 1B
    byte   ReligionCoverage;          // 1B
    byte   GarbageCoverage;           // 1B

    // Utility status
    short  PowerBalance;              // 2B — MW surplus/deficit
    short  WaterBalance;              // 2B — ML surplus/deficit
    byte   PowerConnected;            // 1B — % of tiles with power
    byte   WaterConnected;            // 1B — % of tiles with water

    // Economy
    byte   DominantIndustry;          // 1B — most common production chain
    byte   UnemploymentRate;          // 1B — 0-255 (0-100%, 0.39% resolution)
    int    AvgPropertyValue;          // 4B — in cents
    int    TotalRevenue;              // 4B — monthly, all buildings

    // Growth pressure
    short  ResidentialDemand;         // 2B — positive = wants more housing
    short  CommercialDemand;          // 2B
    short  IndustrialDemand;          // 2B

    // Cultural
    byte   DominantCulture;           // 1B — culture archetype index
    byte   CulturalDiversity;         // 1B — Shannon diversity index * 25

    // Dirty flags (bitmask)
    uint   DirtyFlags;                // 4B — which aggregates need recalc
    uint   LastUpdateTick;            // 4B — when aggregates last computed

    // Building lists (indices into building array)
    // Stored separately in PartitionBuildingLists to keep struct fixed-size
    ushort BuildingCount;             // 2B
    ushort ServiceBuildingCount;      // 2B

    fixed byte _reserved[146];        // pad to 256B
}
// 1,024 partitions x 256B = 256 KB
```

### Dirty Flag System

```csharp
[Flags]
enum PartitionDirty : uint {
    None             = 0,
    Population       = 1 << 0,   // household added/removed/moved
    Zones            = 1 << 1,   // tile zoned/dezoned
    Buildings        = 1 << 2,   // building built/demolished
    LandValue        = 1 << 3,   // land value changed
    Pollution        = 1 << 4,   // pollution source changed
    Crime            = 1 << 5,   // crime level changed
    Services         = 1 << 6,   // service building placed/removed
    Roads            = 1 << 7,   // road network changed
    Utilities        = 1 << 8,   // power/water network changed
    Economy          = 1 << 9,   // building production changed
    Culture          = 1 << 10,  // demographic shift
    All              = 0xFFFFFFFF
}

// On any mutation, mark the partition dirty:
void OnBuildingPlaced(Building b) {
    var p = GetPartition(b.TileX, b.TileY);
    p.DirtyFlags |= PartitionDirty.Buildings
                   | PartitionDirty.Zones
                   | PartitionDirty.LandValue
                   | PartitionDirty.Economy;
    // Also dirty neighbor partitions if building is near edge
    if (IsNearPartitionEdge(b)) MarkNeighborsDirty(p, PartitionDirty.LandValue);
}

// During Level 1 tick, only recompute dirty partitions:
void RecomputePartitions(Partition[] partitions) {
    ParallelFor(partitions, p => {
        if (p.DirtyFlags == PartitionDirty.None) return; // skip clean partitions
        if (p.DirtyFlags.HasFlag(PartitionDirty.Population))
            p.RecalcPopulation();
        if (p.DirtyFlags.HasFlag(PartitionDirty.LandValue))
            p.RecalcLandValue();
        // ... etc
        p.DirtyFlags = PartitionDirty.None;
        p.LastUpdateTick = CurrentTick;
    });
}
```

**Performance**: In steady state (no player action), 0 partitions are dirty = 0 work. During active building, typically 1-5 partitions dirty = microseconds. Worst case (mass demolition): all 1024 dirty = ~0.5ms.

### Spatial Lookup

```csharp
// O(1) tile-to-partition
ushort GetPartitionId(int tileX, int tileY) => (ushort)((tileY >> 5) * 32 + (tileX >> 5));

// Get all tiles in partition (no allocation, computed on fly)
void ForEachTileInPartition(ushort partitionId, Action<int, int> action) {
    int px = (partitionId % 32) * 32;
    int py = (partitionId / 32) * 32;
    for (int y = py; y < py + 32; y++)
        for (int x = px; x < px + 32; x++)
            action(x, y);
}

// Radius query: which partitions overlap a circle
void ForEachPartitionInRadius(int cx, int cy, int radius, Action<Partition> action) {
    int minPX = Math.Max(0, (cx - radius) >> 5);
    int maxPX = Math.Min(31, (cx + radius) >> 5);
    int minPY = Math.Max(0, (cy - radius) >> 5);
    int maxPY = Math.Min(31, (cy + radius) >> 5);
    for (int py = minPY; py <= maxPY; py++)
        for (int px = minPX; px <= maxPX; px++)
            action(Partitions[py * 32 + px]);
}
```

---

## 4. Traffic System

### Architecture Overview

Traffic uses a **macroscopic flow model** (not agent-based). Individual vehicles are cosmetic only (Section 16). The simulation operates on aggregate flows through a road graph.

**Three-layer design**:

1. **Road Graph** — directed graph of nodes/edges extracted from tile map
2. **Traffic Zones** — 512 zones clustering households for O-D matrix compression
3. **Assignment Engine** — Frank-Wolfe with Contraction Hierarchies

### Road Graph Construction

```csharp
class RoadGraphBuilder {
    // Called when road network changes (dirty flag on RoadNetwork)
    // Scans tile data, extracts graph topology

    RoadGraph Build(TileData tiles) {
        // Phase 1: Identify road tiles with 3+ road neighbors = intersection → node
        //          Road tiles with exactly 2 neighbors = edge segment
        //          Dead ends = node
        // Phase 2: Trace edges between nodes, measure length
        // Phase 3: Assign capacity from road type:
        //   dirt: 200 veh/hr/lane
        //   2lane: 800 veh/hr/lane
        //   4lane: 1600 veh/hr/lane
        //   highway: 2200 veh/hr/lane
        // Phase 4: Build adjacency list (CSR format for cache efficiency)

        // Typical result: 20K-30K nodes, 40K-60K edges
        return graph;
    }
}

// Compressed Sparse Row format for cache-friendly traversal
struct RoadGraphCSR {
    RoadNode[] Nodes;          // sorted by ID
    int[]      EdgeOffsets;    // EdgeOffsets[i] = first edge index for node i
    RoadEdge[] Edges;          // all edges, grouped by source node

    ReadOnlySpan<RoadEdge> GetEdgesFrom(int nodeId) {
        int start = EdgeOffsets[nodeId];
        int end = EdgeOffsets[nodeId + 1];
        return Edges.AsSpan(start, end - start);
    }
}
```

### Traffic Zone Compression

100K households cannot each be an O-D origin. Compress to ~512 zones.

```csharp
class TrafficZoneBuilder {
    // Group households into zones based on spatial proximity
    // Target: ~200 households per zone = ~512 zones for 100K households

    // Algorithm: use the 1024 spatial partitions, merge adjacent partitions
    //           with similar characteristics into ~512 traffic zones
    //           (2 partitions per zone on average)

    // The O-D matrix is then 512 x 512 = 262,144 entries
    // At 4 bytes each = ~1 MB — fits entirely in L3 cache

    TrafficZone[] BuildZones(Partition[] partitions, Household[] households) {
        // Step 1: assign each partition to initial zone = itself
        // Step 2: merge partitions with population < 100 into neighbors
        // Step 3: split partitions with population > 500 at centroid
        // Target: 400-600 zones with roughly equal population

        // Each zone stores:
        //   - centroid node (nearest road graph node)
        //   - total trip generation (households * trips/day)
        //   - trip attraction (jobs + shopping + services)
        return zones;
    }
}

struct TrafficZone {           // 32 bytes
    ushort Id;                 // 2B
    uint   CentroidNodeId;    // 4B — nearest road graph node
    ushort HouseholdCount;    // 2B
    ushort JobCount;           // 2B
    ushort ShoppingTrips;     // 2B — daily attraction
    ushort WorkTrips;          // 2B — daily generation
    ushort ServiceTrips;       // 2B
    ushort LeisureTrips;       // 2B
    float  TotalGeneration;   // 4B — total daily trips out
    float  TotalAttraction;   // 4B — total daily trips in
    ushort[] PartitionIds;    // allocated separately
    uint   _padding;           // 4B
}
```

### Origin-Destination Matrix

```csharp
class ODMatrix {
    // 512 x 512 = 262,144 cells
    // Each cell = flow (vehicles/day from zone i to zone j)
    float[,] Flows;   // 512 x 512 x 4B = 1 MB

    void Build(TrafficZone[] zones) {
        // Gravity model: T_ij = O_i * D_j * f(c_ij) / sum_k(D_k * f(c_ik))
        // where:
        //   O_i = total trips generated by zone i
        //   D_j = total trips attracted to zone j (jobs, shops, services)
        //   c_ij = travel cost (time) from zone i to zone j
        //   f(c) = exp(-beta * c) — exponential deterrence, beta = 0.1

        // Step 1: compute shortest-path cost matrix (512x512)
        //   Using Contraction Hierarchies: 512 queries, each ~10μs = 5ms total
        float[,] costMatrix = ComputeCostMatrix(zones);

        // Step 2: apply gravity model
        Parallel.For(0, zones.Length, i => {
            float denominator = 0;
            for (int k = 0; k < zones.Length; k++)
                denominator += zones[k].TotalAttraction * MathF.Exp(-0.1f * costMatrix[i, k]);

            for (int j = 0; j < zones.Length; j++) {
                Flows[i, j] = zones[i].TotalGeneration
                             * zones[j].TotalAttraction
                             * MathF.Exp(-0.1f * costMatrix[i, j])
                             / denominator;
            }
        });
    }
}
```

### Contraction Hierarchies (CH)

Precomputed hierarchical shortcuts for O(log n) shortest-path queries.

```csharp
class ContractionHierarchy {
    // Preprocessing (runs once when road network changes): O(n log n)
    // Typical time for 25K nodes: ~200ms

    // Query time: O(log n) per query vs O(n log n) for Dijkstra
    // For 25K nodes: ~10μs per query vs ~2ms for Dijkstra
    // 512 zone queries: 5ms with CH vs 1024ms with Dijkstra

    RoadGraphCSR UpGraph;    // edges going UP the hierarchy
    RoadGraphCSR DownGraph;  // edges going DOWN the hierarchy
    int[] NodeOrder;          // contraction order (least important first)

    struct ShortcutEdge {
        uint FromNode;
        uint ToNode;
        float Weight;         // travel time
        uint MidNode;         // for path unpacking
    }
    ShortcutEdge[] Shortcuts; // typically 2-3x original edge count

    void Preprocess(RoadGraphCSR graph) {
        // 1. Order nodes by "importance" (edge difference + deleted neighbors)
        // 2. Contract nodes bottom-up:
        //    For each contracted node v:
        //      For each pair (u, w) where u→v→w exists:
        //        If u→v→w is the shortest u-w path through v:
        //          Add shortcut u→w with weight = w(u,v) + w(v,w)
        // 3. Build up-graph and down-graph

        // Memory: shortcuts ≈ 100K-150K edges × 20B = 2-3 MB
    }

    float Query(uint source, uint target) {
        // Bidirectional Dijkstra:
        //   Forward search on UpGraph from source (only go UP in hierarchy)
        //   Backward search on DownGraph from target (only go UP)
        //   Meet in the middle
        // Priority queue size: typically < 100 entries
        // Total relaxations: ~200-500 (vs 25,000 for full Dijkstra)
    }
}
```

### Traffic Assignment: Frank-Wolfe Algorithm

```csharp
class FrankWolfeAssigner {
    // Assigns O-D flows to road network edges
    // Converges to User Equilibrium (Wardrop's principle)

    // Called every game-day (L1 tick)
    // Full rebuild if road network changed; otherwise 1 iteration for refinement

    void FullAssignment(RoadGraphCSR graph, ODMatrix od, ContractionHierarchy ch) {
        // Step 1: All-or-nothing assignment (shortest paths under free-flow)
        //   For each O-D pair with flow > threshold (0.1 vehicles):
        //     Find shortest path using CH
        //     Add flow to all edges on path
        //   ~50K significant O-D pairs, each query ~10μs = ~500ms
        //   PARALLELIZE: split O-D pairs across worker threads
        //   With 6 workers: ~85ms

        float[] edgeVolumes = new float[graph.Edges.Length];
        AssignAllOrNothing(graph, od, ch, edgeVolumes);

        // Step 2: Frank-Wolfe iterations (3-5 iterations for good convergence)
        for (int iter = 0; iter < 5; iter++) {
            // a. Update edge travel times using BPR formula
            UpdateBPR(graph, edgeVolumes);

            // b. All-or-nothing with current travel times
            float[] auxVolumes = new float[graph.Edges.Length];
            AssignAllOrNothing(graph, od, ch, auxVolumes);

            // c. Line search: find optimal step size λ
            float lambda = LineSearch(graph, edgeVolumes, auxVolumes);

            // d. Update: volumes = (1-λ)*volumes + λ*auxVolumes
            for (int e = 0; e < edgeVolumes.Length; e++)
                edgeVolumes[e] = (1 - lambda) * edgeVolumes[e] + lambda * auxVolumes[e];

            // e. Check convergence (relative gap < 1%)
            if (RelativeGap(graph, edgeVolumes, auxVolumes) < 0.01f) break;
        }

        // Step 3: Write final volumes to edge structs
        for (int e = 0; e < graph.Edges.Length; e++)
            graph.Edges[e].Volume = (ushort)edgeVolumes[e];
    }

    void SingleIteration(RoadGraphCSR graph, ODMatrix od, ContractionHierarchy ch) {
        // For daily refinement when network hasn't changed
        // Just 1 Frank-Wolfe iteration: ~85ms / 6 cores = ~15ms
        // Good enough: traffic shifts incrementally as city grows
    }

    void UpdateBPR(RoadGraphCSR graph, float[] volumes) {
        // BPR formula: t = t0 * (1 + α*(v/c)^β)
        // Standard parameters: α=0.15, β=4
        // Per edge: ~5ns (3 multiplies + 1 power)
        // 50K edges: ~0.25ms, trivially parallelizable
        Parallel.For(0, graph.Edges.Length, e => {
            float vc = volumes[e] / graph.Edges[e].Capacity;
            graph.Edges[e].CurrentTravelTime =
                graph.Edges[e].FreeFlowTime * (1f + 0.15f * MathF.Pow(vc, 4));
        });
    }
}
```

### Traffic Performance Budget

| Operation | Frequency | Time (8 cores) | Notes |
|-----------|-----------|----------------|-------|
| BPR update (L0) | Every tick | 0.25ms | Parallel over 50K edges |
| CH preprocessing | On road change | 200ms | Amortized (rare) |
| O-D matrix build | On zone change | 5ms | 512 CH queries |
| Full Frank-Wolfe (5 iter) | On road change | 85ms | Amortized (rare) |
| Single FW iteration (L1) | Every game-day | 15ms | 1 iteration refinement |
| Path cache invalidation | On road change | 0ms | Just clear cache flag |

---

## 5. Economic Simulation

### Victoria 3 Snapshot Economy

The economy runs as a **Leontief Input-Output model** divided into market zones.

### Market Zones

```csharp
// City divided into 8-16 market zones based on transport connectivity
// Zones are larger than traffic zones — they represent economic regions
// A market zone = cluster of ~64-128 spatial partitions

struct MarketZone {                    // ~2 KB
    ushort Id;                         // 2B
    ushort[] PartitionIds;            // which spatial partitions belong

    // Supply and demand per good (45 goods)
    float[] Supply;                    // 45 floats = 180B — units/day produced
    float[] Demand;                    // 45 floats = 180B — units/day consumed
    float[] Price;                     // 45 floats = 180B — current market price
    float[] PriceEquilibrium;         // 45 floats = 180B — target price
    float[] Inventory;                 // 45 floats = 180B — stockpile

    // Leontief matrix: how much of good j is needed to produce 1 unit of good i
    // 45 x 45 = 2,025 floats = 8.1 KB per zone — stored separately

    // Trade with other zones and global market
    float[] ImportVolume;              // 45 floats
    float[] ExportVolume;              // 45 floats
    float[] ImportPrice;               // 45 floats — price from cheapest source
}
```

### Goods System (45 goods, 20 production chains)

```csharp
enum GoodCategory : byte {
    RawMaterial,    // 8 goods: grain, ore, timber, oil, coal, livestock, fish, cotton
    ProcessedGood,  // 12 goods: flour, steel, lumber, fuel, plastic, fabric, meat, etc.
    ManufacturedGood, // 10 goods: furniture, electronics, vehicles, clothing, tools, etc.
    ConsumerGood,   // 8 goods: food, beverages, medicine, luxury, entertainment, etc.
    Service,        // 7 goods: healthcare, education, legal, finance, transport, telecom, tourism
}

struct GoodDefinition {        // 32 bytes
    byte   Id;                 // 0-44
    byte   Category;           // GoodCategory
    ushort BasePrice;          // in cents per unit
    ushort WeightPerUnit;      // affects transport cost
    byte   Perishability;      // 0-255 (0 = never expires, 255 = expires in 1 day)
    byte   LuxuryFactor;       // 0-255 (higher = more elastic demand)
    float  ElasticityOfDemand; // price elasticity (-0.1 to -3.0)
    float  ElasticityOfSupply; // price elasticity (0.1 to 2.0)
    ushort GlobalBasePrice;    // import/export reference price
    byte   TechLevelRequired;  // min tech era to unlock (0-7)
    byte   PollutionFactor;    // production pollution intensity
    fixed byte _padding[8];
}

// 20 production chains define how goods transform
struct ProductionChain {       // 64 bytes
    byte   Id;                 // 0-19
    byte   OutputGoodId;       // what this chain produces
    byte   InputCount;         // 1-4 inputs
    byte   InputGood0;
    byte   InputGood1;
    byte   InputGood2;
    byte   InputGood3;
    float  InputRatio0;        // units of input per unit of output
    float  InputRatio1;
    float  InputRatio2;
    float  InputRatio3;
    float  BaseProductionRate; // units/day per worker
    byte   BuildingCategory;   // what building type runs this chain
    byte   SkillRequired;      // worker skill level needed
    ushort WorkersPerUnit;     // labor input ratio
    float  TechMultiplier;     // current tech bonus (updated on research)
    fixed byte _padding[16];
}
```

### Leontief I-O Solver

```csharp
class LeontiefSolver {
    // The I-O matrix A is 45x45 where A[i,j] = units of good j needed per unit of good i
    // Solution: x = (I - A)^(-1) * d  where d = final demand vector
    //
    // For 45x45, direct LU decomposition: ~0.02ms (91,125 operations)
    // We cache the inverse and only recompute when production chains change (rare)

    float[,] InverseMatrix;    // 45x45, cached
    bool _dirty;

    void Solve(MarketZone zone) {
        if (_dirty) {
            InverseMatrix = InvertLeontief(zone.TechMatrix); // 0.02ms
            _dirty = false;
        }

        // Compute required production: x = (I-A)^-1 * demand
        // 45x45 matrix-vector multiply: 2,025 multiply-adds = ~0.002ms
        float[] requiredProduction = MatVecMul(InverseMatrix, zone.Demand);

        // Compare required vs actual supply
        for (int g = 0; g < 45; g++) {
            float surplus = zone.Supply[g] - requiredProduction[g];

            if (surplus > 0) {
                // Oversupply: price drops, excess goes to inventory or export
                zone.Price[g] *= 1f - 0.02f * (surplus / zone.Supply[g]);
                zone.ExportVolume[g] = surplus * 0.5f; // export half of surplus
            } else if (surplus < 0) {
                // Shortage: price rises, try to import
                float shortage = -surplus;
                zone.Price[g] *= 1f + 0.05f * (shortage / zone.Demand[g]);
                zone.ImportVolume[g] = shortage; // import all of shortage (if affordable)
            }

            // Clamp price to reasonable range (50% - 300% of base)
            zone.Price[g] = Math.Clamp(zone.Price[g],
                GoodDefs[g].BasePrice * 0.5f,
                GoodDefs[g].BasePrice * 3.0f);
        }
    }
}
```

### Building Aggregation (50K buildings into market zones)

```csharp
class MarketAggregator {
    // Run at L1 (every game-day)
    // Aggregate supply/demand from buildings into their market zone

    void Aggregate(Building[] buildings, MarketZone[] zones) {
        // Clear zone accumulators
        foreach (var z in zones) {
            Array.Clear(z.Supply);
            Array.Clear(z.Demand);
        }

        // Single pass over buildings: O(50K) = ~0.3ms
        // Buildings are sorted by MarketZoneId for cache locality
        for (int i = 0; i < buildings.Length; i++) {
            ref var b = ref buildings[i];
            if (b.ConstructionState != BuiltState.Built) continue;

            var zone = zones[b.MarketZoneId];

            // Supply: what this building produces
            if (b.OutputGoodId > 0 && b.WorkerOccupancy > 0) {
                float efficiency = (float)b.WorkerOccupancy / b.WorkerCapacity;
                efficiency *= (1f + b.TechEfficiencyBonus / 10000f);
                zone.Supply[b.OutputGoodId] += b.ProductionRate * efficiency;
            }

            // Demand: what this building consumes (inputs + residential consumption)
            if (b.ProductionChainId > 0) {
                var chain = ProductionChains[b.ProductionChainId];
                float output = b.ProductionRate * (float)b.WorkerOccupancy / b.WorkerCapacity;
                if (chain.InputGood0 > 0) zone.Demand[chain.InputGood0] += output * chain.InputRatio0;
                if (chain.InputGood1 > 0) zone.Demand[chain.InputGood1] += output * chain.InputRatio1;
                if (chain.InputGood2 > 0) zone.Demand[chain.InputGood2] += output * chain.InputRatio2;
                if (chain.InputGood3 > 0) zone.Demand[chain.InputGood3] += output * chain.InputRatio3;
            }

            // Residential demand: households consume consumer goods
            if (b.ResidentialOccupancy > 0) {
                float pop = b.ResidentialOccupancy * 3.5f; // avg household size
                // Consumer goods demand based on income level
                AddConsumerDemand(zone, pop, GetAvgIncome(b));
            }
        }
    }
}
```

### Inter-Zone Trade

```csharp
class InterZoneTrader {
    // After each zone solves locally, zones trade surpluses
    // Trade cost = base price * distance factor * road quality factor

    float[,] TradeCoefficients; // 16 x 16 (zone pair trade friction)

    void Trade(MarketZone[] zones) {
        // For each good with surplus in zone A and deficit in zone B:
        // Transfer goods if price_A + transport_cost < price_B

        for (int g = 0; g < 45; g++) {
            // Build surplus/deficit lists
            Span<(ushort zoneId, float amount)> surpluses = stackalloc ...;
            Span<(ushort zoneId, float amount)> deficits = stackalloc ...;

            // Match surpluses to deficits (cheapest first)
            // Sort surpluses by (price + transport cost to deficit zone)
            // Greedy matching — O(zones^2) = O(256) per good, negligible
        }
    }
}
```

### Global Market (Import/Export)

```csharp
struct GlobalMarket {
    float[] WorldPrice;        // 45 goods — slowly fluctuating reference prices
    float   ImportTariffRate;  // set by laws
    float   ExportSubsidyRate; // set by laws
    float   TradeBalance;      // running total

    void Tick() {
        // World prices drift with random walk + mean reversion
        for (int g = 0; g < 45; g++) {
            float drift = (GoodDefs[g].GlobalBasePrice - WorldPrice[g]) * 0.01f;
            float noise = Random.NextSingle(-0.02f, 0.02f) * WorldPrice[g];
            WorldPrice[g] += drift + noise;
        }
    }

    // Import: city buys at WorldPrice * (1 + tariff) + transportCost
    // Export: city sells at WorldPrice * (1 - exportTax) - transportCost
    // Capped by port/airport capacity (infrastructure)
}
```

### Economic Performance Budget

| Operation | Frequency | Time | Notes |
|-----------|-----------|------|-------|
| Building aggregation | Every game-day | 0.3ms | Single pass, 50K buildings |
| Leontief solve (per zone) | Every game-day | 0.02ms | 45x45 matrix-vector |
| All zones solve | Every game-day | 0.3ms | 16 zones parallel |
| Inter-zone trade | Every game-day | 0.1ms | 16x16 matching |
| Global market tick | Every game-day | 0.01ms | 45 price updates |
| **Total economy L1** | **Every game-day** | **~0.7ms** | |

---

## 6. Population System

### Staggered Household Updates

100K households cannot all update every tick. Instead:

```csharp
class PopulationSystem {
    const int STAGGER_FRAMES = 30; // process 1/30th per tick
    int _staggerOffset;

    // Per tick: 100,000 / 30 = 3,333 households
    // At 64 bytes each = 208 KB per tick — fits in L2 cache

    void TickHouseholds(SimState state) {
        int batchSize = state.Households.Length / STAGGER_FRAMES;
        int start = _staggerOffset * batchSize;
        int end = Math.Min(start + batchSize, state.Households.Length);
        _staggerOffset = (_staggerOffset + 1) % STAGGER_FRAMES;

        // Sort households by PartitionId for spatial locality
        // (done once when households are created/moved)

        // Process batch in parallel chunks
        Parallel.For(start, end, i => {
            ref var h = ref state.Households[i];

            // 1. Income update: based on employment, skill, economy
            UpdateIncome(ref h, state);

            // 2. Expense update: rent + goods consumption + taxes
            UpdateExpenses(ref h, state);

            // 3. Savings delta
            h.Savings += h.MonthlyIncome / 30 - h.MonthlyExpenses / 30;

            // 4. Quick satisfaction check (full recalc is L2)
            //    Just check: did services change? did commute change?
            if (state.GetPartition(h.PartitionId).DirtyFlags != 0)
                h.Satisfaction = QuickSatisfaction(ref h, state);

            // 5. Move desire check
            if (h.Satisfaction < -2000 && h.Savings > 0)
                h.Flags |= HouseholdFlags.WantsToMove;
        });
    }
}
```

### Satisfaction Calculation (L2, monthly)

```csharp
struct SatisfactionWeights {
    // Configurable weights (can be influenced by culture, laws, era)
    float Commute;         // 0.15 — how bad is the commute?
    float Housing;         // 0.12 — quality and affordability
    float Employment;      // 0.12 — job satisfaction, income adequacy
    float Safety;          // 0.10 — crime level in neighborhood
    float Health;          // 0.10 — healthcare access, pollution
    float Education;       // 0.08 — school quality for families
    float Environment;     // 0.08 — parks, pollution, noise
    float Services;        // 0.07 — fire, police, garbage, water
    float Culture;         // 0.06 — entertainment, religion, cultural fit
    float Taxes;           // 0.05 — effective tax burden
    float Social;          // 0.04 — neighborhood income match, density preference
    float Governance;      // 0.03 — mayor approval, law satisfaction
}

short CalculateSatisfaction(ref Household h, SimState state) {
    var p = state.GetPartition(h.PartitionId);
    var w = state.SatisfactionWeights;

    float score = 0;

    // Commute: 0 (unemployed/retired) or -1 (120min+) to +1 (< 15min)
    score += w.Commute * CommuteScore(h);

    // Housing: rent-to-income ratio, overcrowding, condition
    score += w.Housing * HousingScore(h, state);

    // Employment: employed = +0.5 base, income vs expectations
    score += w.Employment * EmploymentScore(h, state);

    // Safety: inverse of partition crime level
    score += w.Safety * (1f - p.AvgCrime / 255f) * 2f - 1f;

    // Health: healthcare coverage * (1 - pollution)
    score += w.Health * (p.HealthCoverage / 255f - p.AvgPollution / 512f);

    // Education: school coverage * school quality (for families)
    if (h.HouseholdSize > 2) // likely has children
        score += w.Education * p.EducationCoverage / 255f;

    // Environment: parks - pollution - noise
    score += w.Environment * ((p.ParkCoverage - p.AvgPollution - p.AvgNoise) / 255f);

    // Services: composite of fire/police/garbage/water coverage
    score += w.Services * ((p.FireCoverage + p.PoliceCoverage + p.GarbageCoverage
                           + p.WaterConnected) / (255f * 4));

    // Tax burden: effective tax rate vs tolerance (income-dependent)
    score += w.Taxes * TaxScore(h, state.TaxSystem);

    // Cultural fit: how well neighborhood culture matches household
    score += w.Culture * CultureFitScore(h, p);

    // Governance: mayor approval
    score += w.Governance * (state.Politics.MayorApproval / 100f - 0.5f);

    // Scale to short range: -10000 to +10000
    return (short)Math.Clamp(score * 10000f, -10000, 10000);
}
```

### Migration Model

```csharp
class MigrationModel {
    // City attractiveness vs global baseline
    // Positive = net immigration, negative = net emigration
    // Max migration per month: 2% of population = 10,000 people

    int Calculate(SimState state) {
        float attractiveness = 0;

        // Pull factors (positive)
        attractiveness += state.Economy.JobVacancyRate * 3.0f;     // jobs available
        attractiveness += state.AvgSatisfaction / 10000f * 2.0f;   // quality of life
        attractiveness += state.Economy.AvgWage / state.Economy.CostOfLiving * 1.0f;
        attractiveness += state.ServiceCoverage.Overall * 0.5f;
        attractiveness += state.CityRating / 100f * 0.5f;

        // Push factors (negative)
        attractiveness -= state.Economy.UnemploymentRate * 4.0f;    // no jobs
        attractiveness -= state.Economy.HousingVacancyRate < 0.02f ? 2.0f : 0; // no housing
        attractiveness -= state.AvgPollution / 255f * 1.5f;
        attractiveness -= state.AvgCrime / 255f * 1.5f;
        attractiveness -= state.TaxSystem.EffectiveBurden * 1.0f;

        // Net migration (people/month)
        // S-curve: gentle at low/high attractiveness, steeper in middle
        float maxMigration = state.Population * 0.02f;
        return (int)(maxMigration * MathF.Tanh(attractiveness));
    }

    void Apply(SimState state, int netMigration) {
        if (netMigration > 0) {
            // Spawn households: find vacant housing, create household, add citizens
            int householdsToAdd = netMigration / 4; // avg household size ~4
            for (int i = 0; i < householdsToAdd; i++) {
                var vacancy = state.HousingMarket.FindVacancy();
                if (vacancy == null) break; // no housing available
                state.SpawnHousehold(vacancy.Value);
            }
        } else {
            // Remove least-satisfied households that want to move
            int householdsToRemove = -netMigration / 4;
            var candidates = state.Households
                .Where(h => h.Flags.HasFlag(HouseholdFlags.WantsToMove))
                .OrderBy(h => h.Satisfaction)
                .Take(householdsToRemove);
            foreach (var h in candidates)
                state.RemoveHousehold(h.Id);
        }
    }
}
```

### Employment Matching (L2, monthly)

```csharp
class LaborMarket {
    void MatchUnemployed(SimState state) {
        // Collect unemployed workers and job vacancies
        // Typically 2-8% unemployed = 10K-40K workers
        // Job vacancies typically 3-10% = 1.5K-5K openings

        // Strategy: spatial + skill matching
        // 1. Group unemployed by partition (spatial locality)
        // 2. Group vacancies by partition
        // 3. Match within same/adjacent partitions first (short commute)
        // 4. Expand search radius for unmatched

        // Sorting: O(n log n) where n = unemployed count
        // Matching: greedy, O(n * avg_vacancies_per_zone)
        // Total: ~3ms for 30K unemployed with 6 worker threads

        var unemployed = CollectUnemployed(state); // stackalloc buffer
        var vacancies = CollectVacancies(state);

        // Sort both by partition for spatial matching
        unemployed.Sort((a, b) => a.PartitionId.CompareTo(b.PartitionId));
        vacancies.Sort((a, b) => a.PartitionId.CompareTo(b.PartitionId));

        // Two-pointer merge-match
        int vi = 0;
        for (int ui = 0; ui < unemployed.Length && vi < vacancies.Length; ui++) {
            ref var worker = ref unemployed[ui];
            // Find closest vacancy matching skill level
            while (vi < vacancies.Length && vacancies[vi].PartitionId < worker.PartitionId - 3)
                vi++;
            // Try to match
            for (int j = vi; j < vacancies.Length && vacancies[j].PartitionId <= worker.PartitionId + 3; j++) {
                if (vacancies[j].Filled) continue;
                if (vacancies[j].SkillRequired <= worker.SkillLevel) {
                    Match(ref worker, ref vacancies[j], state);
                    break;
                }
            }
        }
    }
}
```

---

## 7. Service Coverage

### Influence Map Approach

Each service type has a coverage map: `float[1024*1024]` = 4 MB per type.

```csharp
class CoverageSystem {
    // 8 service types, each with a 1M-float coverage map
    // Total: 32 MB (within budget)

    enum ServiceType : byte {
        Police,      // radius: 30-60 tiles depending on building
        Fire,        // radius: 25-50 tiles
        Healthcare,  // radius: 40-80 tiles
        Education,   // radius: 20-40 tiles
        Parks,       // radius: 10-25 tiles
        Transit,     // radius: 5-15 tiles (stop coverage)
        Garbage,     // radius: 40-80 tiles
        Religion,    // radius: 20-40 tiles
    }

    float[][] CoverageMaps; // [serviceType][tileIndex]

    // Precompute when service building is placed or removed
    void OnServiceBuildingPlaced(Building b) {
        if (b.ServiceType == 0) return; // not a service building

        int radius = b.ServiceRadius;
        int cx = b.TileX + b.Width / 2;
        int cy = b.TileY + b.Height / 2;
        float strength = b.ServiceCapacity / (float)MaxCapacity[b.ServiceType];

        // Additive influence with distance falloff
        // Only update tiles within radius (not all 1M)
        // Area = π * r² ≈ 3.14 * 60² = 11,310 tiles for r=60 (worst case)

        UpdateCoverageRegion(b.ServiceType, cx, cy, radius, strength, add: true);
    }

    void OnServiceBuildingRemoved(Building b) {
        // Same as above but subtract
        UpdateCoverageRegion(b.ServiceType,
            b.TileX + b.Width/2, b.TileY + b.Height/2,
            b.ServiceRadius,
            b.ServiceCapacity / (float)MaxCapacity[b.ServiceType],
            add: false);
    }

    void UpdateCoverageRegion(ServiceType type, int cx, int cy, int radius, float strength, bool add) {
        float[] map = CoverageMaps[(int)type];
        int rSq = radius * radius;
        int sign = add ? 1 : -1;

        int minX = Math.Max(0, cx - radius);
        int maxX = Math.Min(1023, cx + radius);
        int minY = Math.Max(0, cy - radius);
        int maxY = Math.Min(1023, cy + radius);

        for (int y = minY; y <= maxY; y++) {
            for (int x = minX; x <= maxX; x++) {
                int dx = x - cx;
                int dy = y - cy;
                int distSq = dx * dx + dy * dy;
                if (distSq > rSq) continue;

                // Linear falloff: coverage = strength * (1 - dist/radius)
                float dist = MathF.Sqrt(distSq);
                float coverage = strength * (1f - dist / radius);

                int idx = y * 1024 + x;
                map[idx] = Math.Clamp(map[idx] + sign * coverage, 0f, 1f);
            }
        }

        // Mark affected partitions dirty
        MarkPartitionsDirty(minX, minY, maxX, maxY, PartitionDirty.Services);
    }
}
```

### Coverage Performance

| Event | Tiles updated | Time | Frequency |
|-------|-------------|------|-----------|
| Place small service (r=20) | ~1,257 tiles | 0.02ms | Player action |
| Place large service (r=60) | ~11,310 tiles | 0.15ms | Player action |
| Remove service | Same as place | Same | Player action |
| Full recalculation (all) | 1M tiles x 8 types | ~40ms | Save/load only |

Coverage maps are **event-driven** (only update on build/demolish), not tick-driven. Zero per-tick cost in steady state.

---

## 8. Overlay Systems

### 15+ Overlay Types

All overlays are `byte[1024*1024]` or `float[1024*1024]` arrays rendered as colored tiles over the map.

| # | Overlay | Data type | Update trigger | Update cost |
|---|---------|-----------|----------------|-------------|
| 1 | Land Value | byte[] | L1 (daily) + build/demolish | 0.5ms (partition-level) |
| 2 | Zone Types | byte[] | Instant (from tile data) | 0ms (direct read) |
| 3 | Pollution (Air) | byte[] | L1 (daily), wind diffusion | 1ms (diffusion step) |
| 4 | Pollution (Water) | byte[] | L1 (daily) | 0.3ms |
| 5 | Pollution (Noise) | byte[] | Event-driven (build/demolish/road) | 0.15ms per event |
| 6 | Crime | byte[] | L2 (monthly) | 2ms |
| 7 | Fire Risk | byte[] | L2 (monthly) | 0.5ms |
| 8 | Desirability | byte[] | L1 (daily), composite | 1ms |
| 9 | Traffic Density | ushort[] | L0 (every tick from BPR) | 0.25ms |
| 10 | Power Grid | byte[] | Event-driven (network change) | 0.5ms |
| 11 | Water Grid | byte[] | Event-driven (network change) | 0.5ms |
| 12 | Population Density | byte[] | L2 (monthly) | 0.3ms |
| 13 | Happiness | byte[] | L2 (monthly), from households | 0.5ms |
| 14 | Education Level | byte[] | L2 (monthly) | 0.3ms |
| 15 | Health | byte[] | L2 (monthly) | 0.3ms |
| 16 | Service Coverage (per type) | float[] | Event-driven | 0.15ms per event |

**Memory**: 15 overlays x 1MB average = **15 MB**. Service coverage maps are separate (32 MB, Section 7).

### Pollution Diffusion (most expensive overlay)

```csharp
class PollutionSystem {
    // Air pollution uses a simplified advection-diffusion model
    // Sources: industrial buildings, power plants, traffic
    // Diffusion: Gaussian blur step each game-day
    // Wind: shift pollution in wind direction each game-day

    byte[] AirPollution;       // 1M tiles
    byte[] PollutionSources;   // pre-computed from buildings (event-driven)
    Vector2 WindDirection;     // updated randomly each game-day

    void TickDaily(SimState state) {
        // Step 1: Reset to sources (0.2ms)
        Array.Copy(PollutionSources, AirPollution, 1024 * 1024);

        // Step 2: Wind advection — shift array in wind direction (0.3ms)
        //   Use integer pixel shift (1-3 tiles per day based on wind speed)
        //   memcpy rows with offset — very cache-friendly
        int shiftX = (int)(WindDirection.X * state.WindSpeed);
        int shiftY = (int)(WindDirection.Y * state.WindSpeed);
        ShiftArray(AirPollution, 1024, 1024, shiftX, shiftY);

        // Step 3: Diffusion — 1 pass of box blur (approximates Gaussian) (0.5ms)
        //   3x3 kernel, separable: horizontal pass then vertical pass
        //   Each pass: 1M multiply-adds with SIMD (Vector<byte>)
        BoxBlur3x3(AirPollution, 1024, 1024);

        // Step 4: Decay — pollution decays 5% per day (0.1ms)
        //   SIMD: multiply all bytes by 243/256
        SimdDecay(AirPollution, 1024 * 1024, 0.95f);

        // Total: ~1.1ms per game-day (amortized to 0.07ms/tick at 15 ticks/day)
    }
}
```

### Land Value Calculation

```csharp
class LandValueSystem {
    // Land value is a composite overlay influenced by:
    // + proximity to services, parks, transit, water features
    // + low crime, low pollution, low noise
    // + high desirability neighbors (positive feedback)
    // - proximity to industrial, landfills, highways

    // Computed at partition level (1024 partitions), interpolated to tiles
    // Partition-level: instant. Tile interpolation: bilinear from 4 corner partitions.

    void RecalculatePartition(ref Partition p, SimState state) {
        float value = 50f; // base

        // Service bonuses
        value += p.PoliceCoverage / 255f * 8f;
        value += p.FireCoverage / 255f * 5f;
        value += p.HealthCoverage / 255f * 10f;
        value += p.EducationCoverage / 255f * 12f;
        value += p.ParkCoverage / 255f * 15f;
        value += p.TransitCoverage / 255f * 10f;

        // Negative factors
        value -= p.AvgPollution / 255f * 20f;
        value -= p.AvgCrime / 255f * 25f;
        value -= p.AvgNoise / 255f * 10f;

        // Density premium (urban core)
        value += (p.Population / 1024f) * 5f;

        // Economic activity
        value += MathF.Log2(1 + p.TotalRevenue / 100000f) * 3f;

        p.AvgLandValue = (byte)Math.Clamp(value, 0, 255);
    }
}
```

### Desirability (Composite Overlay)

```csharp
// Desirability = weighted sum of other overlays
// Determines where zone growth happens and what density develops
// Computed per partition, propagated to tiles

byte ComputeDesirability(ref Partition p) {
    float d = 0;
    d += p.AvgLandValue / 255f * 0.25f;
    d += (255 - p.AvgPollution) / 255f * 0.15f;
    d += (255 - p.AvgCrime) / 255f * 0.15f;
    d += (255 - p.AvgNoise) / 255f * 0.10f;
    d += p.TransitCoverage / 255f * 0.10f;
    d += p.ParkCoverage / 255f * 0.10f;
    d += p.EducationCoverage / 255f * 0.08f;
    d += p.HealthCoverage / 255f * 0.07f;
    return (byte)(d * 255);
}
```

---

## 9. Threading Model

### Thread Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Main Thread                                            │
│  - SDL2_PollEvent()                                     │
│  - ImGui frame: NewFrame → widgets → Render             │
│  - OpenGL: bind snapshot buffers → draw calls            │
│  - Audio: queue commands to audio thread                 │
│  - Reads LATEST snapshot (lock-free)                    │
│  - Target: 16.67ms (60 FPS) or 33.33ms (30 FPS)        │
└──────────────┬──────────────────────────────────────────┘
               │ (snapshot double-buffer, atomic swap)
┌──────────────▼──────────────────────────────────────────┐
│  Simulation Thread (dedicated, pinned to core 1)        │
│  - Orchestrates tick loop                               │
│  - Dispatches parallel jobs to thread pool               │
│  - Waits for job completion                             │
│  - Publishes snapshot when tick complete                 │
│  - Budget: 50ms per tick (20 ticks/sec) to              │
│            100ms per tick (10 ticks/sec)                 │
└──────────────┬──────────────────────────────────────────┘
               │ (job queue)
┌──────────────▼──────────────────────────────────────────┐
│  Worker Thread Pool (N = CPU cores - 2 = 6 on 8-core)  │
│  Thread 2: ┤                                            │
│  Thread 3: ┤ Execute jobs from queue                    │
│  Thread 4: ┤ - Traffic BPR (per edge chunk)             │
│  Thread 5: ┤ - Household updates (per batch)            │
│  Thread 6: ┤ - Coverage recalc (per region)             │
│  Thread 7: ┤ - Economy aggregation (per zone)           │
│            └─ Jobs are fork-join: all must complete      │
│               before tick advances                      │
└─────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────┐
│  I/O Thread (dedicated, low priority)                   │
│  - Save game serialization (background, no stutter)     │
│  - Load game deserialization                            │
│  - Autosave scheduling                                  │
│  - Asset streaming (if chunked map loading)             │
└─────────────────────────────────────────────────────────┘
```

### Double-Buffer Snapshot (Lock-Free Render Decoupling)

```csharp
class SimulationSnapshot {
    // Immutable data that the renderer reads each frame
    // Sim thread writes to back buffer, then atomic-swaps

    // Tile visual state (only what renderer needs)
    byte[]   TileVisualType;    // 1M bytes — combined terrain+zone+road visual
    byte[]   TileBuildingLevel; // 1M bytes — building sprite level per tile

    // Vehicle positions for cosmetic rendering
    VehicleRenderState[] Vehicles; // 10K x 16B = 160 KB

    // Overlay data (whichever overlay is active)
    byte[]   ActiveOverlay;     // 1M bytes — currently selected overlay

    // UI data
    CityStats Stats;            // population, money, date, ratings (~256 bytes)
    MiniMapData MiniMap;        // 64x64 downsampled view (~4 KB)

    // Total snapshot size: ~3.2 MB (fits in L3, fast to swap)
}

class DoubleBufferedSnapshots {
    SimulationSnapshot _front; // renderer reads this
    SimulationSnapshot _back;  // sim thread writes this
    volatile int _swapFlag;    // 0 = front is A, 1 = front is B

    // Sim thread: write to back, then swap
    void PublishSnapshot() {
        // Copy sim state into _back
        FillSnapshot(_back);
        // Atomic swap
        Interlocked.Exchange(ref _swapFlag, 1 - _swapFlag);
    }

    // Render thread: read front (never blocks)
    SimulationSnapshot GetLatestSnapshot() {
        return _swapFlag == 0 ? _snapshotA : _snapshotB;
    }
}
```

### Job System

```csharp
class JobSystem {
    // Simple fork-join: sim thread posts N jobs, waits for all to complete
    // Uses .NET ThreadPool under the hood (or custom work-stealing queue)

    private CountdownEvent _barrier;

    void ScheduleParallel<T>(T[] items, int chunkSize, Action<T[], int, int> work) {
        int chunks = (items.Length + chunkSize - 1) / chunkSize;
        _barrier = new CountdownEvent(chunks);

        for (int c = 0; c < chunks; c++) {
            int start = c * chunkSize;
            int end = Math.Min(start + chunkSize, items.Length);
            ThreadPool.QueueUserWorkItem(_ => {
                work(items, start, end);
                _barrier.Signal();
            });
        }

        _barrier.Wait(); // sim thread blocks until all chunks complete
    }
}

// Usage in sim tick:
void TickLevel0(SimState state, float dt) {
    // Traffic BPR: 50K edges, chunks of 8K = 7 jobs across 6 workers
    _jobs.ScheduleParallel(state.RoadEdges, 8192, (edges, start, end) => {
        for (int i = start; i < end; i++) {
            float vc = edges[i].Volume / (float)edges[i].Capacity;
            edges[i].CurrentTravelTime =
                (ushort)(edges[i].FreeFlowTime * (1f + 0.15f * MathF.Pow(vc, 4)));
        }
    });

    // Vehicle interpolation: 10K vehicles, chunks of 2K = 5 jobs
    _jobs.ScheduleParallel(state.Vehicles, 2048, (vehs, start, end) => {
        for (int i = start; i < end; i++) {
            vehs[i].Progress += dt * vehs[i].Speed / vehs[i].PathSegmentLength;
            if (vehs[i].Progress >= 1f) vehs[i].AdvanceSegment();
        }
    });
}
```

### Thread Safety Rules

1. **Sim thread owns all simulation state.** Workers only touch data within their assigned range.
2. **No shared writes.** Each job writes to a disjoint slice of the array.
3. **Renderer reads snapshots only.** Never touches live sim state.
4. **Partition dirty flags use `Interlocked.Or`.** Multiple workers can dirty different partitions concurrently.
5. **I/O thread takes a deep copy** of SimState before serializing (or serializes from snapshot).
6. **Player input queue**: main thread enqueues commands (place building, zone area), sim thread dequeues and applies at tick boundary.

```csharp
// Player command queue (SPSC: main thread produces, sim thread consumes)
class CommandQueue {
    ConcurrentQueue<SimCommand> _queue = new();

    // Main thread (UI)
    void Enqueue(SimCommand cmd) => _queue.Enqueue(cmd);

    // Sim thread (start of each tick)
    void ProcessCommands(SimState state) {
        while (_queue.TryDequeue(out var cmd)) {
            cmd.Execute(state); // place building, zone tiles, enact law, etc.
        }
    }
}
```

---

## 10. Memory Budget

### Detailed Allocation

| System | Calculation | Memory |
|--------|-------------|--------|
| **Tile Data (SoA)** | 1,048,576 tiles x ~18 bytes avg | **18.0 MB** |
| **Households** | 100,000 x 64 bytes | **6.1 MB** |
| **Citizens** | 500,000 x 32 bytes | **15.3 MB** |
| **Buildings** | 50,000 x 128 bytes | **6.1 MB** |
| **Road Nodes** | 25,000 x 24 bytes | **0.6 MB** |
| **Road Edges** | 50,000 x 32 bytes | **1.5 MB** |
| **Road Edge CSR offsets** | 25,001 x 4 bytes | **0.1 MB** |
| **Contraction Hierarchy** | 150,000 shortcuts x 20 bytes | **2.9 MB** |
| **Service Coverage Maps** | 1,048,576 x 4 bytes x 8 types | **32.0 MB** |
| **Overlay Maps** | 1,048,576 x 1 byte x 15 types | **15.0 MB** |
| **Partitions** | 1,024 x 256 bytes | **0.3 MB** |
| **Partition Building Lists** | 50,000 x 2 bytes (indices) | **0.1 MB** |
| **Traffic Zones** | 512 x 32 bytes | **0.02 MB** |
| **O-D Matrix** | 512 x 512 x 4 bytes | **1.0 MB** |
| **Market Zones (16)** | 16 x ~2 KB + 16 x 8.1 KB (Leontief) | **0.2 MB** |
| **Good Definitions** | 45 x 32 bytes | **0.001 MB** |
| **Production Chains** | 20 x 64 bytes | **0.001 MB** |
| **Vehicle State** | 10,000 x 64 bytes | **0.6 MB** |
| **Vehicle Path Cache** | 10,000 x 128 bytes avg | **1.2 MB** |
| **Simulation Snapshots (x2)** | 2 x 3.2 MB | **6.4 MB** |
| **Event State** | 80 event defs x 256B + 10 active x 1KB | **0.03 MB** |
| **Law State** | 70 laws x 128 bytes | **0.01 MB** |
| **Technology State** | 154 techs x 64 bytes | **0.01 MB** |
| **Sports Leagues** | 11 leagues x 2 KB (teams, schedules) | **0.02 MB** |
| **Scratch Buffers** | Frank-Wolfe temp arrays, sort buffers | **10.0 MB** |
| **String Tables** | Building names, good names, UI text | **2.0 MB** |
| **Definition Tables** | Building defs, zone defs, etc. | **1.0 MB** |
| | | |
| **Simulation Total** | | **120.5 MB** |
| | | |
| **Rendering** | Tile atlas, sprite sheets, shaders, VBOs | **~200 MB** |
| **Audio** | Sound effects, music buffers | **~50 MB** |
| **UI** | ImGui buffers, font atlas | **~20 MB** |
| **.NET Runtime** | GC heap, JIT code, thread stacks | **~100 MB** |
| | | |
| **Grand Total** | | **~490 MB** |

**Well under 2 GB budget.** Leaves ~1.5 GB headroom for: larger cities, mods, additional overlays, undo history, multiple save states in memory.

### Cache Analysis

| Data | Size | Fits in | Access pattern |
|------|------|---------|---------------|
| Active household batch (3,333) | 208 KB | **L2 cache** (256 KB) | Sequential scan |
| Partition array (all) | 256 KB | **L2 cache** | Random by dirty flag |
| Road edges (BPR scan) | 1.5 MB | **L3 cache** (8-16 MB) | Sequential scan |
| O-D matrix | 1 MB | **L3 cache** | Row-major scan |
| Single overlay | 1 MB | **L3 cache** | 2D scan (row-major) |
| All households | 6.1 MB | **L3 cache** | Staggered batch |
| All citizens | 15.3 MB | Partial L3 | Staggered batch |

---

## 11. Scaling Benchmarks

### Per-Tick Time Budget at Different City Sizes

Assumptions: 8-core CPU (6 workers), DDR5, 16 MB L3 cache. Normal speed = 15 ticks/sec = 66ms budget.

| Population | Households | Buildings | Road Edges | L0 tick | L1 tick (amort.) | L2 tick (amort.) | Total/tick | Memory |
|-----------|-----------|-----------|-----------|---------|-----------------|-----------------|-----------|--------|
| 1,000 | 200 | 100 | 500 | 0.05ms | 0.003ms | 0.001ms | **0.06ms** | ~55 MB |
| 10,000 | 2,000 | 1,000 | 3,000 | 0.15ms | 0.02ms | 0.005ms | **0.18ms** | ~62 MB |
| 50,000 | 10,000 | 5,000 | 12,000 | 0.4ms | 0.08ms | 0.03ms | **0.5ms** | ~78 MB |
| 100,000 | 20,000 | 10,000 | 20,000 | 0.7ms | 0.15ms | 0.06ms | **0.9ms** | ~95 MB |
| 250,000 | 50,000 | 25,000 | 35,000 | 1.2ms | 0.3ms | 0.12ms | **1.6ms** | ~145 MB |
| 500,000 | 100,000 | 50,000 | 50,000 | 1.8ms | 0.5ms | 0.2ms | **2.5ms** | ~250 MB |

**L0 breakdown at 500K** (1.8ms):

| Component | Time | Notes |
|-----------|------|-------|
| BPR update | 0.8ms | 50K edges, 6 workers, SIMD |
| Vehicle interpolation | 0.3ms | 10K vehicles, trivial math |
| Utility balance | 0.15ms | 1024 partitions, sum only |
| Active events | 0.05ms | 0-3 events |
| Household stagger batch | 0.35ms | 3,333 households |
| Snapshot publish | 0.15ms | memcpy 3.2 MB |

**L1 breakdown at 500K** (7.5ms every 15 ticks = 0.5ms amortized):

| Component | Time | Notes |
|-----------|------|-------|
| Economy aggregation | 0.3ms | 50K buildings |
| Leontief solve (all zones) | 0.3ms | 16 zones parallel |
| Inter-zone trade | 0.1ms | 16x16 matching |
| Zone growth eval | 0.5ms | 1024 partitions |
| Pollution diffusion | 1.1ms | 1M tile blur |
| Frank-Wolfe iteration | 3.0ms | 1 iteration, 6 workers |
| Construction progress | 0.05ms | <500 sites |
| Land value / desirability | 0.5ms | 1024 partitions |
| Dirty partition recomp | 0.5ms | Variable |
| Overlay updates | 1.1ms | Non-event overlays |

**L2 breakdown at 500K** (28ms every 450 ticks = 0.06ms amortized):

| Component | Time | Notes |
|-----------|------|-------|
| Demographics | 2ms | 500K citizens scan |
| Migration | 0.5ms | Spawn/remove households |
| Employment matching | 5ms | 30K unemployed worst case |
| Satisfaction (all HH) | 8ms | 100K households, parallel |
| Cultural drift | 2ms | 1024 partitions |
| Research tick | 0.5ms | 154 techs |
| Politics | 0.5ms | Aggregate calculation |
| Crime recalculation | 3ms | Partition + diffusion |
| Health/education overlays | 2ms | From coverage + demographics |
| Overlay propagation | 4.5ms | Update byte arrays from partitions |

**L3 at 500K** (15ms every 5400 ticks = negligible amortized):

| Component | Time | Notes |
|-----------|------|-------|
| Archetype detection | 0.1ms | 50 aggregate values |
| Era transition | 0.1ms | Simple threshold check |
| Infrastructure aging | 3ms | 50K buildings |
| Sports (11 leagues) | 1.1ms | Schedule + simulate |
| City rating | 0.2ms | Composite formula |

### Worst-Case Scenarios

| Scenario | Impact | Mitigation |
|----------|--------|-----------|
| Road network change (L1) | +200ms CH rebuild + 85ms full FW | Run async, use stale data for 1 day |
| Mass demolition (100 buildings) | +15ms coverage recalc | Batch updates, defer to next tick |
| All partitions dirty | +0.5ms L1 recomp | Rare — only after load/disaster |
| L1 + L2 coincide | 7.5ms + 28ms = 35.5ms | Under budget (66ms) even combined |
| L1 + L2 + L3 coincide | 35.5ms + 15ms = 50.5ms | Still under 66ms budget |

**Conclusion**: At 500K population, total tick time is ~2.5ms average, ~50ms worst case. Comfortably within the 66ms budget for 15 ticks/sec. Even at 20 ticks/sec (50ms budget), worst case fits.

---

## 12. Event & Disaster System

### Architecture

Events are state machines with spatial effects. 80+ event types organized into categories.

```csharp
enum EventCategory : byte {
    NaturalDisaster,   // earthquake, flood, tornado, wildfire, blizzard, drought, tsunami
    ManMade,           // fire, chemical spill, blackout, water contamination, building collapse
    Economic,          // recession, boom, trade embargo, commodity shock, market crash
    Social,            // protest, riot, festival, parade, election, crime wave, epidemic
    Political,         // scandal, corruption, reform movement, annexation vote
    Environmental,     // pollution crisis, heatwave, cold snap, tree blight, algae bloom
    Infrastructure,    // bridge failure, sinkhole, dam breach, pipe burst, grid overload
    Cultural,          // landmark discovery, cultural festival, heritage designation
    Sports,            // championship, stadium event, Olympic bid
    Technology,        // breakthrough, cyber attack, automation wave
    Global,            // war, pandemic, refugee crisis, climate accord, space event
}

struct EventDefinition {               // 256 bytes
    byte   Id;                         // 0-79+
    byte   Category;                   // EventCategory
    byte   MinEra;                     // earliest era this can trigger
    byte   MaxSimultaneous;            // max instances at once (usually 1)

    // Trigger conditions (all must be true)
    byte   TriggerType;               // random | threshold | chain | player
    float  BaseProbabilityPerMonth;   // for random triggers (0.001 - 0.1)
    // Threshold triggers: fires when metric crosses value
    byte   ThresholdMetric;           // pollution, crime, satisfaction, etc.
    float  ThresholdValue;            // trigger above/below this
    byte   ThresholdDirection;        // above | below

    // Duration
    ushort MinDurationTicks;          // minimum active time
    ushort MaxDurationTicks;          // maximum active time

    // Spatial
    byte   SpatialType;              // point | radius | partition | citywide
    ushort EffectRadius;             // tiles (for radius type)
    byte   SpreadRate;               // tiles/tick (for spreading events like fire)

    // Effects (applied while active)
    short  SatisfactionModifier;     // per tick, to affected households
    short  LandValueModifier;        // to affected tiles
    short  ProductionModifier;       // % change to affected buildings
    short  CrimeModifier;            // to affected area
    short  HealthModifier;           // to affected citizens
    byte   DestroysBuildingsChance;  // 0-255 per tick per affected building
    byte   CausesFireChance;         // 0-255 per tick per affected building
    byte   BlocksRoads;              // 0 = no, 1 = yes (affects traffic)
    int    FiscalImpact;             // cost/revenue to city budget per tick

    // Aftermath
    byte   AftermathEventId;         // chain event triggered on completion (0=none)
    short  AftermathSatisfaction;    // permanent satisfaction shift
    int    RebuildCost;              // estimated cost to repair damage

    // Player response options (index into response table)
    byte   ResponseOption0;
    byte   ResponseOption1;
    byte   ResponseOption2;

    fixed byte _reserved[174];       // pad to 256
}
```

### Event State Machine

```csharp
class ActiveEvent {
    byte   DefinitionId;
    EventPhase Phase;           // Brewing | Active | Waning | Aftermath
    int    StartTick;
    int    DurationTicks;
    ushort CenterX, CenterY;   // epicenter
    ushort CurrentRadius;       // for spreading events
    float  Intensity;           // 0.0 - 1.0, can change over time
    byte   PlayerResponseChosen; // which response the player picked

    // Affected area cache (avoid re-scanning)
    ushort[] AffectedPartitions; // typically 1-20 partitions

    void Tick(SimState state, float dt) {
        switch (Phase) {
            case EventPhase.Brewing:
                // Warning signs: minor effects, player can act
                Intensity += 0.01f;
                if (Intensity >= 0.3f) Phase = EventPhase.Active;
                break;

            case EventPhase.Active:
                // Full effects applied
                var def = EventDefs[DefinitionId];

                // Spatial spread (for fire, flood, etc.)
                if (def.SpreadRate > 0 && CurrentRadius < def.EffectRadius)
                    CurrentRadius += def.SpreadRate;

                // Apply effects to affected partitions
                foreach (var pid in AffectedPartitions) {
                    ref var p = ref state.Partitions[pid];
                    // Modify partition aggregates
                    // These will propagate to households at next L2 tick
                }

                // Building damage
                if (def.DestroysBuildingsChance > 0)
                    DamageBuildings(state, def);

                // Check end condition
                DurationTicks--;
                if (DurationTicks <= 0) Phase = EventPhase.Waning;
                break;

            case EventPhase.Waning:
                Intensity -= 0.05f;
                if (Intensity <= 0) {
                    Phase = EventPhase.Aftermath;
                    if (EventDefs[DefinitionId].AftermathEventId > 0)
                        state.EventSystem.Trigger(EventDefs[DefinitionId].AftermathEventId, CenterX, CenterY);
                }
                break;

            case EventPhase.Aftermath:
                // Clean up, apply permanent changes
                Deactivate(state);
                break;
        }
    }
}
```

### Event Trigger Engine

```csharp
class EventTriggerEngine {
    // Evaluated once per game-day (L1 frequency)
    // Checks all 80+ event definitions against current state
    // Cost: ~0.05ms (80 condition checks, mostly simple comparisons)

    void Evaluate(SimState state) {
        foreach (var def in EventDefs) {
            if (state.ActiveEvents.CountOfType(def.Id) >= def.MaxSimultaneous) continue;
            if (state.CurrentEra < def.MinEra) continue;

            bool shouldTrigger = def.TriggerType switch {
                TriggerType.Random =>
                    Random.NextSingle() < def.BaseProbabilityPerMonth / 30f, // per day
                TriggerType.Threshold =>
                    CheckThreshold(state, def),
                TriggerType.Chain =>
                    false, // only triggered by other events
                TriggerType.Player =>
                    false, // only triggered by player action
                _ => false
            };

            if (shouldTrigger)
                SpawnEvent(state, def);
        }
    }
}
```

---

## 13. Law & Compliance System

### 70+ Laws Organized by Category

```csharp
enum LawCategory : byte {
    Taxation,        // 10 laws: income tax rates, property tax, sales tax, corporate tax, etc.
    Zoning,          // 8 laws: density limits, mixed-use rules, historic preservation, etc.
    Environment,     // 10 laws: emission standards, green building req, recycling mandate, etc.
    Labor,           // 8 laws: minimum wage, work hours, safety standards, unions, etc.
    PublicSafety,    // 7 laws: policing policy, fire code, building code, speed limits, etc.
    Education,       // 6 laws: school funding, curriculum, compulsory age, etc.
    Healthcare,      // 5 laws: public health, hospital standards, insurance mandate, etc.
    Transport,       // 6 laws: transit funding, congestion pricing, parking rules, etc.
    Economy,         // 5 laws: subsidies, tariffs, business permits, price controls, etc.
    Social,          // 5 laws: housing assistance, welfare, cultural funding, etc.
}

struct LawDefinition {                 // 128 bytes
    byte   Id;                         // 0-69+
    byte   Category;                   // LawCategory
    byte   MinEra;                     // when this becomes available

    // Law has a slider value (0-100) or is binary (on/off)
    byte   ControlType;               // slider | toggle
    byte   DefaultValue;              // initial slider position or 0/1
    byte   CurrentValue;              // player-set value

    // Effects: each law modifies one or more simulation parameters
    // Stored as an array of (target, magnitude) pairs
    byte   EffectCount;               // 1-6 effects per law
    LawEffect Effects_0;              // 8 bytes each
    LawEffect Effects_1;
    LawEffect Effects_2;
    LawEffect Effects_3;
    LawEffect Effects_4;
    LawEffect Effects_5;

    // Political cost
    short  PopularityImpact;          // -100 to +100 base (scaled by value)
    byte   PoliticalFaction;          // which faction cares most

    // Economic cost
    int    BudgetImpact;              // annual cost/revenue in cents (scaled by value)

    // Compliance
    byte   ComplianceDifficulty;      // 0-255: how hard for buildings to comply
    byte   EnforcementCostPerTick;    // cost of enforcement

    fixed byte _padding[34];          // pad to 128
}

struct LawEffect {                     // 8 bytes
    byte   TargetSystem;              // which simulation system is affected
    byte   TargetParameter;           // which parameter within that system
    short  Magnitude;                 // effect size (scaled by law value / 100)
    float  _reserved;
}

enum LawTarget : byte {
    TaxRate_Income,
    TaxRate_Property,
    TaxRate_Sales,
    TaxRate_Corporate,
    MaxBuildingDensity,
    MinParkRatio,
    EmissionLimit,
    MinimumWage,
    PoliceAggressiveness,
    TransitSubsidy,
    GreenBuildingBonus,
    IndustrialZoneRestriction,
    // ... 40+ targets covering all simulation parameters
}
```

### Compliance Simulation

```csharp
class ComplianceSystem {
    // Evaluated at L2 (monthly)
    // For each law, check which buildings comply / don't comply
    // Non-compliant buildings get penalties (fines, reduced satisfaction, forced closure)

    // Key insight: don't check every building against every law.
    // Pre-compute which laws affect which building types.
    // Then only check relevant buildings.

    // LawBuildingTypeMatrix[lawId] = bitset of affected building types
    ulong[] LawBuildingTypeAffinity; // 70 laws x 8 bytes = 560 bytes

    void EvaluateCompliance(SimState state) {
        // For each active law with compliance requirements:
        foreach (var law in state.Laws) {
            if (law.CurrentValue == 0) continue; // law disabled
            if (law.ComplianceDifficulty == 0) continue; // no compliance check

            ulong affectedTypes = LawBuildingTypeAffinity[law.Id];
            if (affectedTypes == 0) continue;

            // Scan buildings of affected types
            // Buildings sorted by TypeId for efficient scanning
            for (int i = 0; i < state.Buildings.Length; i++) {
                ref var b = ref state.Buildings[i];
                if (!IsTypeAffected(b.TypeId, affectedTypes)) continue;

                bool compliant = CheckCompliance(ref b, law);
                if (!compliant) {
                    // Apply penalty: additional operating cost
                    b.LawComplianceCost += law.EnforcementCostPerTick;
                    // May affect satisfaction of workers/residents
                    state.GetPartition(b.PartitionId).DirtyFlags |= PartitionDirty.Economy;
                }
            }
        }
    }

    bool CheckCompliance(ref Building b, LawDefinition law) {
        // Example checks:
        // - Emission law: b.AirPollutionOutput <= law.CurrentValue * EmissionLimit
        // - Density law: b.Floors <= law.CurrentValue * MaxFloors / 100
        // - Safety law: b.Condition >= law.CurrentValue * MinCondition / 100
        // - Green building: b.Flags.HasFlag(GreenCertified) if law.CurrentValue > 50
        // Each is a simple comparison — ~5ns per building
        return true; // simplified
    }
}
```

### Budget Impact of Laws

```csharp
class BudgetSystem {
    // Calculate annual budget from all law settings
    // Revenue: taxes (income, property, sales, corporate)
    // Expenses: services, enforcement, subsidies, debt service

    struct AnnualBudget {
        // Revenue
        int IncomeTaxRevenue;          // f(population, income levels, tax rate law)
        int PropertyTaxRevenue;        // f(total land value, property tax rate law)
        int SalesTaxRevenue;           // f(commercial revenue, sales tax rate law)
        int CorporateTaxRevenue;       // f(industrial/office revenue, corporate tax rate law)
        int TariffRevenue;             // f(import volume, tariff law)
        int FineRevenue;               // f(non-compliance count, fine levels)

        // Expenses
        int PoliceExpense;             // f(police buildings, policing law intensity)
        int FireExpense;               // f(fire buildings)
        int HealthExpense;             // f(hospitals, healthcare law)
        int EducationExpense;          // f(schools, education law)
        int TransitExpense;            // f(transit network, transit subsidy law)
        int GarbageExpense;
        int InfraMaintenanceExpense;   // f(road km, pipe km, age)
        int WelfareExpense;            // f(population below poverty, welfare law)
        int SubsidyExpense;            // f(economy laws)
        int DebtService;               // f(outstanding bonds)
    }
}
```

---

## 14. Technology & Research System

### 154 Technologies in 7 Eras

```csharp
enum TechEra : byte {
    Foundation,    // 0: basic infrastructure (20 techs)
    Industrial,    // 1: factories, rail (22 techs)
    Modern,        // 2: cars, electricity, telecom (24 techs)
    Information,   // 3: computers, internet, globalization (26 techs)
    Green,         // 4: renewables, sustainability (22 techs)
    Smart,         // 5: AI, automation, smart grid (20 techs)
    Future,        // 6: fusion, hyperloop, arcologies (20 techs)
}

struct TechDefinition {                // 64 bytes
    byte   Id;                         // 0-153
    byte   Era;                        // TechEra
    ushort ResearchCost;              // research points needed
    byte   PrereqCount;               // 0-3 prerequisites
    byte   Prereq0;                    // tech IDs that must be researched first
    byte   Prereq1;
    byte   Prereq2;

    // Effects (up to 4 per tech)
    TechEffect Effect0;                // 8 bytes
    TechEffect Effect1;
    TechEffect Effect2;
    TechEffect Effect3;

    // Unlock
    byte   UnlocksBuildingType;       // new building type available (0 = none)
    byte   UnlocksProductionChain;    // new production chain (0 = none)
    byte   UnlocksLaw;                // new law available (0 = none)
    byte   UnlocksEvent;              // new event type possible (0 = none)

    fixed byte _padding[12];          // pad to 64
}

struct TechEffect {                    // 8 bytes
    byte   TargetSystem;              // which system is affected
    byte   TargetParam;               // which parameter
    short  Modifier;                  // additive or multiplicative (encoded)
    float  _reserved;
}

struct TechState {                     // 1 byte per tech
    byte Status;  // 0=locked, 1=available, 2=researching, 3=completed
}
// 154 techs x 1 byte = 154 bytes for state (negligible)
```

### Research Progress

```csharp
class ResearchSystem {
    byte   CurrentResearchId;          // which tech is being researched (0 = none)
    float  CurrentProgress;            // 0.0 to 1.0
    float  ResearchRate;               // points/month, from universities + labs + funding

    void Tick(SimState state) {
        // Called at L2 (monthly)
        if (CurrentResearchId == 0) return;

        // Research rate = base from education buildings + law funding + citizen education level
        ResearchRate = CalculateRate(state);

        CurrentProgress += ResearchRate / TechDefs[CurrentResearchId].ResearchCost;

        if (CurrentProgress >= 1.0f) {
            CompleteTech(state, CurrentResearchId);
            CurrentResearchId = 0;
            CurrentProgress = 0;
        }
    }

    void CompleteTech(SimState state, byte techId) {
        var def = TechDefs[techId];
        state.TechStates[techId].Status = 3; // completed

        // Apply effects to simulation
        ApplyEffects(state, def);

        // Unlock dependent techs
        for (int t = 0; t < 154; t++) {
            if (state.TechStates[t].Status != 0) continue; // already available or done
            if (AllPrereqsMet(state, t))
                state.TechStates[t].Status = 1; // now available
        }

        // Unlock buildings, chains, laws
        if (def.UnlocksBuildingType > 0)
            state.UnlockedBuildings.Add(def.UnlocksBuildingType);
        if (def.UnlocksProductionChain > 0)
            state.UnlockedChains.Add(def.UnlocksProductionChain);
        if (def.UnlocksLaw > 0)
            state.UnlockedLaws.Add(def.UnlocksLaw);

        // Mark Leontief matrix dirty (tech may change production rates)
        foreach (var zone in state.MarketZones)
            zone.LeontiefDirty = true;
    }

    void ApplyEffects(SimState state, TechDefinition def) {
        // Each tech effect modifies a global simulation parameter
        // Examples:
        //   "Improved Steel": production rate +20% for steel chain
        //   "Solar Panels": power output +15% for solar buildings
        //   "Mass Transit": transit capacity +25%
        //   "Pollution Controls": industrial pollution -30%
        //   "Telemedicine": healthcare coverage radius +20%
        // Applied by updating BuildingDefinition tables and global multipliers
    }
}
```

---

## 15. Sports Leagues

### 11 Leagues

```csharp
enum SportType : byte {
    Football,        // Soccer — 20 teams, 38 match-days
    Basketball,      // 16 teams, 30 match-days
    Baseball,        // 12 teams, 50 match-days
    IceHockey,       // 12 teams, 44 match-days
    AmericanFootball,// 16 teams, 17 match-days
    Tennis,          // individual, 4 grand slams + tour
    Athletics,       // individual, seasonal meets
    Swimming,        // individual, seasonal meets
    Cricket,         // 10 teams, league + cups
    Rugby,           // 12 teams, 22 match-days
    Esports,         // 10 teams, 20 match-days
}

struct League {                        // ~2 KB
    byte     SportType;
    byte     TeamCount;
    byte     CurrentMatchDay;
    byte     SeasonState;              // preseason | inSeason | playoffs | offseason
    Team[]   Teams;                    // 10-20 teams
    int[]    Schedule;                 // match pairings per match-day
    int[]    Standings;                // points/wins per team
}

struct Team {                          // 64 bytes
    byte     Id;
    ushort   StadiumBuildingId;       // 0 = no stadium (minor team)
    byte     Strength;                 // 0-255 (composite of factors)
    byte     Popularity;              // 0-255 (fan base)
    byte     LeaguePosition;          // current standing
    int      Revenue;                 // season revenue
    int      Attendance;              // avg match attendance
    ushort   HomeTileX, HomeTileY;   // location
    byte     IsPlayerTeam;            // 1 = player can manage
    fixed byte _padding[44];
}
```

### Season Simulation (L3, yearly)

```csharp
class SportsSeason {
    void SimulateSeason(League league, SimState state) {
        // Called once per game-year
        // Simulate entire season in one pass (~0.1ms per league)

        for (int matchDay = 0; matchDay < league.Schedule.Length; matchDay++) {
            for (int match = 0; match < league.TeamCount / 2; match++) {
                var home = league.Teams[league.Schedule[matchDay * league.TeamCount / 2 + match * 2]];
                var away = league.Teams[league.Schedule[matchDay * league.TeamCount / 2 + match * 2 + 1]];

                // Match result: weighted random based on team strength + home advantage
                float homeWinProb = 0.35f + 0.3f * (home.Strength - away.Strength) / 255f + 0.1f; // +10% home bonus
                float result = Random.NextSingle();
                // Update standings, attendance, revenue
            }
        }

        // Season effects on city:
        // - Stadium buildings generate event-day traffic spikes
        // - Championship win: city-wide happiness boost (+500 satisfaction for 1 month)
        // - Revenue: stadium ticket sales + merchandise + broadcast rights
        // - Tourism boost from successful teams
        if (league.Teams[0].IsPlayerTeam && league.Teams[0].LeaguePosition == 1)
            state.EventSystem.Trigger(EventId.ChampionshipWin);
    }
}
```

---

## 16. Vehicle Cosmetic Routing

### Vehicles are Visual Only

Traffic flow is determined by the macroscopic BPR model (Section 4). Vehicles are cosmetic sprites that follow pre-assigned paths for visual immersion. They do NOT affect simulation.

```csharp
struct Vehicle {                       // 64 bytes
    uint   Id;                         // 4B
    byte   VehicleType;               // 1B — car, bus, truck, emergency, bicycle, tram, train
    byte   ColorVariant;              // 1B — sprite color (0-15)
    byte   SpriteId;                  // 1B — visual variant within type
    byte   Direction;                 // 1B — 0-7 (8 cardinal + diagonal directions)

    // Current position (for renderer)
    float  WorldX;                    // 4B — interpolated world position
    float  WorldY;                    // 4B

    // Path following
    ushort CurrentEdgeId;             // 2B — which road edge we're on
    float  Progress;                  // 4B — 0.0 to 1.0 along current edge
    float  Speed;                     // 4B — tiles/tick (adjusted by edge travel time)
    ushort PathLength;                // 2B — total edges in path
    ushort PathIndex;                 // 2B — current edge in path

    // Path stored externally in VehiclePathPool (shared, ring buffer)
    uint   PathOffset;                // 4B — offset into path pool

    // Lifecycle
    ushort OriginZoneId;             // 2B — traffic zone
    ushort DestZoneId;               // 2B — traffic zone
    byte   TripPurpose;              // 1B — commute, shopping, leisure, freight, service
    byte   Flags;                     // 1B — isParked | isWaiting | isEmergency

    fixed byte _padding[20];         // pad to 64
}
// 10,000 vehicles x 64B = 625 KB
```

### Vehicle Spawning & Routing

```csharp
class VehicleManager {
    Vehicle[] Vehicles;                // pool of 10K vehicles
    ushort[]  PathPool;               // shared path buffer (ring buffer, 128K entries)
    int       PathPoolHead;
    int       ActiveCount;

    // Spawn rate proportional to traffic volume on edges
    // More traffic → more visible vehicles (but still cosmetic)

    void TickSpawning(SimState state) {
        // Target: maintain ~10K active vehicles for visual density
        // Spawn on high-volume edges, despawn on arrival

        // Find edges with high volume but few visible vehicles
        // Spawn vehicle at edge start, assign path to random destination in zone

        int deficit = 10000 - ActiveCount;
        if (deficit <= 0) return;

        // Sample edges proportional to their volume
        for (int i = 0; i < Math.Min(deficit, 100); i++) { // spawn up to 100/tick
            var edge = SampleHighVolumeEdge(state.RoadEdges);
            var dest = SampleDestinationZone(edge, state.TrafficZones);
            var path = FindPath(edge.FromNodeId, dest.CentroidNodeId, state.CH);
            SpawnVehicle(edge, path);
        }
    }

    void TickMovement(float dt) {
        // Parallel: interpolate all active vehicles along their paths
        // Each vehicle: advance progress, compute world position from edge geometry
        // O(10K) with trivial math per vehicle = ~0.3ms on 6 workers

        Parallel.For(0, ActiveCount, i => {
            ref var v = ref Vehicles[i];
            ref var edge = ref RoadEdges[v.CurrentEdgeId];

            // Speed adjusted by congestion on current edge
            v.Speed = edge.Length / (float)edge.CurrentTravelTime;
            v.Progress += dt * v.Speed;

            if (v.Progress >= 1.0f) {
                v.PathIndex++;
                if (v.PathIndex >= v.PathLength) {
                    // Arrived: despawn
                    Despawn(i);
                    return;
                }
                // Advance to next edge
                v.CurrentEdgeId = PathPool[v.PathOffset + v.PathIndex];
                v.Progress = 0;
            }

            // Interpolate world position from edge endpoints
            var fromNode = RoadNodes[edge.FromNodeId];
            var toNode = RoadNodes[edge.ToNodeId];
            v.WorldX = fromNode.TileX + (toNode.TileX - fromNode.TileX) * v.Progress;
            v.WorldY = fromNode.TileY + (toNode.TileY - fromNode.TileY) * v.Progress;
            v.Direction = ComputeDirection(fromNode, toNode);
        });
    }
}
```

### Vehicle Type Distribution

```csharp
// Proportional to city traffic patterns
static readonly float[] VehicleTypeWeights = {
    0.60f,  // car
    0.10f,  // bus (if transit exists)
    0.12f,  // truck (freight)
    0.02f,  // emergency
    0.08f,  // bicycle (influenced by bike infrastructure law)
    0.04f,  // tram (if tram network exists)
    0.04f,  // train (if rail exists)
};

// Weights adjusted dynamically by:
// - Transit subsidy law → more buses
// - Green transport tech → more bicycles
// - Industrial activity → more trucks
// - Time of day → rush hour patterns
```

---

## 17. Save/Load & Serialization

### Save Format

Binary format for speed. Targeting < 2 seconds save, < 3 seconds load at 500K population.

```csharp
struct SaveFileHeader {                // 256 bytes
    uint   Magic;                      // 0x434D4A52 ("CMJR")
    uint   Version;                    // save format version
    uint   Checksum;                   // CRC32 of payload
    uint   UncompressedSize;           // for allocation
    uint   CompressedSize;             // actual file size
    ulong  SaveTimestamp;              // Unix timestamp
    uint   GameTick;                   // current tick number
    uint   Population;                 // for save file UI display
    int    CityFunds;                  // for save file UI display
    fixed byte CityName[64];          // UTF-8 city name
    fixed byte _reserved[152];
}
```

### Serialization Strategy

```csharp
class SaveSystem {
    // Runs on I/O thread to prevent frame stutter
    // Takes a SNAPSHOT of SimState, then serializes in background

    async Task SaveGame(SimState state, string path) {
        // Step 1: Deep copy simulation state (on sim thread, ~5ms)
        // Done at tick boundary when no jobs are running
        var snapshot = state.DeepCopy();

        // Step 2: Serialize on I/O thread (background)
        await Task.Run(() => {
            using var ms = new MemoryStream(256 * 1024 * 1024); // 256 MB pre-alloc
            using var writer = new BinaryWriter(ms);

            // Write header placeholder (fill checksum later)
            writer.Write(new byte[256]);

            // Serialize each system as a tagged chunk
            WriteChunk(writer, ChunkId.Tiles, state.Tiles);       // ~18 MB
            WriteChunk(writer, ChunkId.Households, state.Households); // ~6 MB
            WriteChunk(writer, ChunkId.Citizens, state.Citizens);    // ~15 MB
            WriteChunk(writer, ChunkId.Buildings, state.Buildings);   // ~6 MB
            WriteChunk(writer, ChunkId.RoadGraph, state.RoadGraph);  // ~2 MB
            WriteChunk(writer, ChunkId.Economy, state.Economy);
            WriteChunk(writer, ChunkId.Laws, state.Laws);
            WriteChunk(writer, ChunkId.Tech, state.Tech);
            WriteChunk(writer, ChunkId.Events, state.Events);
            WriteChunk(writer, ChunkId.Sports, state.Sports);
            WriteChunk(writer, ChunkId.Vehicles, state.Vehicles);
            WriteChunk(writer, ChunkId.Overlays, state.Overlays);    // ~47 MB
            WriteChunk(writer, ChunkId.Partitions, state.Partitions);

            // Compress with Brotli (quality 4 = fast + decent ratio)
            // Typical compression: 120 MB → 25-40 MB
            var compressed = BrotliCompress(ms.ToArray(), quality: 4);

            // Write header with sizes and checksum
            FillHeader(writer, compressed);

            // Write to disk
            File.WriteAllBytes(path, compressed);
        });
    }

    async Task<SimState> LoadGame(string path) {
        return await Task.Run(() => {
            var compressed = File.ReadAllBytes(path);
            var header = ReadHeader(compressed);
            ValidateChecksum(compressed, header);

            var data = BrotliDecompress(compressed);
            var state = new SimState();

            // Read chunks (order-independent, tagged)
            var reader = new BinaryReader(new MemoryStream(data));
            while (reader.BaseStream.Position < reader.BaseStream.Length) {
                var chunkId = reader.ReadByte();
                var chunkSize = reader.ReadInt32();
                ReadChunk(reader, chunkId, chunkSize, state);
            }

            // Rebuild derived state
            state.RoadGraph.RebuildCSR();
            state.ContractionHierarchy.Preprocess(state.RoadGraph);
            state.CoverageSystem.FullRecalculate();
            state.PartitionSystem.RecalculateAll();

            return state;
        });
    }
}
```

### Save File Size Estimates

| Component | Raw size | Compressed (est.) |
|-----------|----------|-------------------|
| Tiles | 18 MB | 3-5 MB (highly compressible — many zeros) |
| Households | 6.1 MB | 4-5 MB |
| Citizens | 15.3 MB | 10-12 MB |
| Buildings | 6.1 MB | 3-4 MB |
| Road graph | 2.1 MB | 1-1.5 MB |
| Coverage maps | 32 MB | 5-8 MB (smooth gradients compress well) |
| Overlays | 15 MB | 3-5 MB |
| Other (economy, laws, etc.) | 0.5 MB | 0.3 MB |
| **Total** | **~95 MB** | **~25-40 MB** |

### Performance Targets

| Operation | 500K pop target | Technique |
|-----------|----------------|-----------|
| Save (to disk) | < 2.0s | Snapshot + async I/O + Brotli q4 |
| Load (from disk) | < 3.0s | Async decompress + parallel chunk parse |
| Autosave | < 2.0s | Same as save, runs on I/O thread |
| Quick-save | < 0.5s | Delta save (only changed chunks since last full save) |

---

## Appendix A: System Interaction Matrix

How each system reads from / writes to other systems:

```
              Tiles  House  Citiz  Build  Roads  Econ  Parti  Cover  Overl  Laws  Tech  Event
Tiles    (SoA)  -     r      .      r      r      .     r      .      w      .     .     w
Household       r     -      r      r      .      r     r      r      r      r     .     r
Citizen         .     r      -      r      .      .     .      .      .      .     .     r
Building        r     r      .      -      .      rw    r      w      w      r     r     rw
Road Graph      r     .      .      .      -      .     r      .      w      .     r     r
Economy         .     r      .      rw     .      -     r      .      .      r     r     r
Partitions      r     r      r      r      r      r     -      r      .      .     .     r
Coverage        r     .      .      r      .      .     w      -      w      .     r     .
Overlays        rw    .      .      r      r      .     r      r      -      .     .     r
Laws            .     r      .      r      .      r     r      .      .      -     r     .
Tech            .     .      .      rw     .      r     .      r      .      rw    -     r
Events          rw    rw     r      rw     r      r     rw     .      rw     .     .     -
Sports          .     r      .      r      .      r     r      .      .      .     .     w

r = reads from, w = writes to, rw = both, . = no direct interaction
```

## Appendix B: Tick Budget Summary

**Target**: 15 ticks/sec = 66.7ms per tick budget.

| Tick Type | Content | Avg time | Worst case | Frequency |
|-----------|---------|----------|------------|-----------|
| Normal tick (L0 only) | BPR + utilities + vehicles + HH batch | 2.5ms | 3ms | 14 of 15 ticks |
| Day tick (L0 + L1) | Above + economy + zones + FW + pollution | 10ms | 15ms | 1 per 15 ticks |
| Month tick (L0 + L1 + L2) | Above + demographics + employment + satisfaction | 38ms | 45ms | 1 per 450 ticks |
| Year tick (L0+L1+L2+L3) | Above + aging + sports + archetype | 53ms | 60ms | 1 per 5400 ticks |

**Amortized average per tick**: ~3.1ms. **Peak**: ~60ms (year tick, still under 66.7ms budget).

## Appendix C: SIMD Opportunities

Performance-critical inner loops that benefit from `System.Numerics.Vector<T>`:

| Loop | Data | SIMD width (AVX2) | Speedup |
|------|------|--------------------|---------|
| BPR power calculation | float[50K] | 8-wide float | 4-6x |
| Pollution decay | byte[1M] | 32-wide byte | 20x |
| Box blur pass | byte[1M] | 32-wide byte | 15x |
| Coverage distance calc | float | 8-wide float | 4x |
| Vehicle position interp | float pairs | 8-wide float | 4x |
| Overlay composite | byte[1M] | 32-wide byte | 20x |
| Array copy (snapshot) | byte[] | 32-wide | 10x |

All critical paths should use `Vector<T>` with `Vector.IsHardwareAccelerated` fallback to scalar.

## Appendix D: Key Design Decisions & Rationale

| Decision | Alternatives considered | Why this choice |
|----------|------------------------|-----------------|
| SoA tiles (not AoS) | AoS struct per tile | 32x better cache utilization for single-field scans (overlays, pathfinding) |
| 32x32 partitions (not quadtree) | Quadtree, 64x64, adaptive | Simplicity + cache-friendly iteration + good granularity balance |
| Macroscopic traffic (not agent) | Per-vehicle simulation | O(edges) vs O(vehicles*pathLength). Agent-based at 100K HH = 10-50ms/tick just for pathfinding |
| Contraction Hierarchies (not A*) | A*, Dijkstra, HPA* | 200x faster queries. 10μs vs 2ms per query. Enables 512-zone O-D in 5ms |
| Frank-Wolfe (not MSA/gradient) | Method of Successive Averages, gradient projection | Simple, convergence guaranteed, 3-5 iterations sufficient for game accuracy |
| Leontief I-O (not agent economy) | Per-building market clearing, auction | 45x45 matrix solves in 20μs. Agent economy at 50K buildings = unscalable |
| Staggered HH updates (not all) | All households every tick | 3,333/tick fits L2 cache. 100K/tick = 6MB = L3 thrashing |
| Event-driven coverage (not tick) | Recalculate every tick | 0ms steady-state vs 40ms/tick for full recalc. Buildings rarely change |
| Double-buffer snapshots (not lock) | Mutex on sim state, triple buffer | Lock-free, renderer never waits. Sim never blocked by slow frame |
| Brotli compression (not LZ4/zstd) | LZ4 (faster), zstd (better ratio) | Good balance: 3-4x compression at 200MB/s speed. Saves are ~30MB not ~95MB |
