# AGENT 04: TRANSPORT & TRAFFIC

## Role
Build the complete transport system: road network, traffic simulation, 15+ transit modes, pathfinding, vehicle rendering, freight logistics routing, and commute calculation.

---

## Prerequisites
- Agent 01: GridData, CoordinateHelper, EventBus
- Agent 02 (partial): Origin-destination pairs (home->work) for commute modeling

---

## Phase 1: Road Network (Day 3-5)

### Tasks
1. **Road Types** (8 tiers, era-gated):
```
Tier 1: Dirt road        (Frontier)   - 1 lane, 20 km/h, cheap
Tier 2: Cobblestone      (Frontier)   - 1 lane, 30 km/h
Tier 3: Paved road       (Industrial) - 2 lanes, 50 km/h
Tier 4: Avenue           (Postwar)    - 4 lanes, 50 km/h, median
Tier 5: Highway          (Postwar)    - 4 lanes, 100 km/h, no intersections
Tier 6: Expressway       (Modern)     - 6 lanes, 120 km/h, interchanges
Tier 7: Smart road       (Future)     - 4 lanes, 140 km/h, AI traffic management
Tier 8: Pedestrian zone  (Any)        - 0 lanes, foot traffic only
```

2. **Road Graph**:
   - Graph data structure: nodes (intersections) + edges (road segments)
   - Auto-generate intersections when roads meet
   - Roundabouts as special intersection type
   - One-way streets
   - Speed limits (per road segment, adjustable by player)
   - Road condition: degrades over time, affects speed + accidents

3. **Road Building**:
   - Click-drag to draw roads
   - Auto-connect to existing road network
   - Demolition tool
   - Bridge over water (extra cost)
   - Tunnel through hills (extra cost)
   - Cost: per tile, varies by road type

### AI Tool Usage
- **Claude**: Generate road graph data structure, auto-intersection logic
- **Claude**: Generate road drawing tool (click-drag with preview)
- **Kimi**: Screenshot of Transport Tycoon road builder -> generate Godot placement script
- **Kimi**: Screenshot of Cities Skylines road tool -> adapt to isometric

### Output Files
- `scripts/simulation/transport/RoadNetwork.cs`
- `scripts/simulation/transport/RoadGraph.cs`
- `scripts/game/build_system/RoadBuilder.gd`
- `data/road_types.json`

---

## Phase 2: Traffic Simulation (Day 5-7)

### Tasks
1. **Statistical Flow Model** (NOT agent-based):
```
BPR Congestion Formula:
travel_time = free_flow_time * (1 + 0.15 * (volume / capacity) ^ 4)

Where:
- free_flow_time = road_length / speed_limit
- volume = vehicles/hour on this segment
- capacity = lanes * 800 vehicles/hour/lane
```

2. **Origin-Destination Matrix**:
   - Every household has home tile + work tile
   - O-D pairs generated from population data
   - Pathfinding: shortest time path considering congestion
   - Route caching: recalculate only when network changes or congestion shifts

3. **Traffic Assignment**:
   - Distribute O-D flows across network
   - Iterative: assign, compute congestion, reassign (3-5 iterations)
   - Result: volume per road segment per hour
   - Time-of-day variation: rush hour peaks (7-9am, 5-7pm)

4. **Congestion Effects**:
   - Green/Yellow/Red overlay per segment
   - Congestion -> longer commutes -> lower satisfaction
   - Congestion -> air pollution near roads
   - Congestion -> economic drag (delivery delays)

5. **Intersections**:
   - Traffic lights (timed, can be adjusted by player)
   - Stop signs (lower capacity than lights)
   - Roundabouts (higher capacity, no signals)
   - Grade separation (overpass/underpass, eliminates intersection delay)

### AI Tool Usage
- **Claude**: Generate BPR model, O-D matrix builder, traffic assignment algorithm
- **Claude**: Generate congestion overlay renderer
- **Research agent**: Study real BPR parameters from transport engineering papers
- **Kimi**: NOT needed (computational work)

### Output Files
- `scripts/simulation/transport/TrafficSimulator.cs`
- `scripts/simulation/transport/ODMatrix.cs`
- `scripts/simulation/transport/TrafficAssignment.cs`
- `scripts/simulation/transport/IntersectionController.cs`
- `scripts/render/overlays/TrafficOverlay.gd`

---

## Phase 3: Public Transit (Day 7-10)

### Tasks
1. **Transit Modes** (15+ modes, era-gated):
```
Horse-drawn bus    (Frontier)    - 6 passengers, 8 km/h
Streetcar/tram     (Industrial)  - 40 passengers, 25 km/h, needs tracks
Steam train        (Industrial)  - 200 passengers, 60 km/h, needs rail
Bus                (Postwar)     - 50 passengers, 40 km/h, uses roads
Trolleybus         (Postwar)     - 60 passengers, 35 km/h, needs wires
Diesel train       (Postwar)     - 300 passengers, 100 km/h
Metro/subway       (Modern)      - 500 passengers, 80 km/h, underground
Light rail         (Modern)      - 150 passengers, 50 km/h
Electric train     (Modern)      - 400 passengers, 160 km/h
Ferry              (Any+water)   - 100 passengers, 20 km/h
Cable car          (Any+hills)   - 30 passengers, 15 km/h
Monorail           (Future)      - 200 passengers, 90 km/h
Maglev             (Future)      - 600 passengers, 300 km/h
Autonomous bus     (Future)      - 30 passengers, 50 km/h, no driver cost
Hyperloop          (Future)      - 50 passengers, 500 km/h
```

2. **Route System**:
   - Player draws routes (sequence of stops)
   - Frequency setting: every 5/10/15/20/30/60 min
   - Cost: vehicles * driver_salary + fuel + maintenance
   - Revenue: fare * ridership
   - Profit/loss per route visible in UI

3. **Ridership Model**:
```
ridership = potential_riders * convenience_factor * price_factor
potential_riders = pop_near_stops * (1 - car_ownership_rate)
convenience_factor = 1.0 / (1 + wait_time/10 + walk_to_stop/5)
price_factor = 1.0 - (fare / average_income * 100)
```

4. **Multimodal Trips**:
   - Citizens can combine modes: walk -> bus -> train -> walk
   - Transfer penalty: each transfer adds discomfort
   - Park-and-ride: drive to station, take transit
   - Integration: shared fare card across modes

5. **Infrastructure**:
   - Bus stops (cheap, road-side)
   - Train stations (expensive, dedicated building)
   - Metro stations (very expensive, underground)
   - Depots (stores vehicles, maintenance)
   - Terminus vs. through-running stations

### AI Tool Usage
- **Claude**: Generate route system, ridership model, multimodal pathfinding
- **Claude**: Generate transit vehicle scheduling (timetable generation)
- **Research agent**: Study transit ridership formulas from transport planning literature
- **Kimi**: Screenshot of OpenTTD route builder -> adapt for isometric Godot
- **Kimi**: Screenshot of Mini Metro UI -> inspire transit map overlay

### Output Files
- `scripts/simulation/transport/TransitSystem.cs`
- `scripts/simulation/transport/RouteManager.cs`
- `scripts/simulation/transport/RidershipModel.cs`
- `scripts/simulation/transport/MultimodalPathfinder.cs`
- `scripts/game/build_system/TransitRouteBuilder.gd`
- `data/transit_modes.json`

---

## Phase 4: Rail Network (Day 10-12)

### Tasks
1. **Track Types**:
   - Single track (cheap, low capacity)
   - Double track (standard)
   - High-speed rail (Modern+, expensive)
   - Underground rail (metro tunnels)

2. **Signaling**:
   - Block signals: prevent collisions
   - Path signals: allow bi-directional single track
   - Signal placement by player (simplified from OpenTTD)

3. **Stations**:
   - Platform count: 1-8 platforms
   - Through stations vs. terminus
   - Freight stations (separate from passenger)
   - Station upgrades: waiting room, shops, park-and-ride

4. **Train Scheduling**:
   - Assign trains to routes
   - Timetable: departure times at each stop
   - Bunching prevention (real issue in transit)
   - Train capacity and crowding

### AI Tool Usage
- **Claude**: Generate rail network graph, signaling system, scheduling
- **Kimi**: Screenshot of Transport Fever 2 rail builder -> adapt for isometric
- **Kimi**: NOT used for simulation logic

### Output Files
- `scripts/simulation/transport/RailNetwork.cs`
- `scripts/simulation/transport/SignalSystem.cs`
- `scripts/simulation/transport/TrainScheduler.cs`
- `scripts/game/build_system/RailBuilder.gd`

---

## Phase 5: Vehicle Rendering (Day 12-13)

### Tasks
1. **Vehicle Pool** (GDScript):
   - Object pool: 500 max visible vehicles
   - LOD: full detail at zoom 1-2, simplified at zoom 3, hidden at zoom 4-5
   - Vehicles follow road paths (cosmetic, not simulation)
   - Speed matches congestion level of road segment

2. **Vehicle Types by Wealth/Era** (from VEHICLE_WEALTH_SYSTEM.md):
   - Poor: old cars, scooters, bicycles
   - Middle: sedans, SUVs, family cars
   - Rich: luxury cars, sports cars
   - Era progression: horse carts -> Model T -> 50s cars -> modern -> electric

3. **Transit Vehicle Sprites**:
   - Each transit mode needs 4 directional sprites
   - Animation: wheels turning, pantograph on trams
   - Occupancy indicator: empty/half/full coloring

### AI Tool Usage
- **Claude**: Generate vehicle pool manager, path-following system
- **Kimi**: Screenshot of pixel art vehicles -> generate sprite sheet layout
- **Art AI (Midjourney/DALL-E)**: Generate base vehicle sprites in pixel art style
- **Claude**: Post-process sprite sheets into Godot animation frames

### Output Files
- `scripts/render/vehicle_renderer/VehiclePool.gd`
- `scripts/render/vehicle_renderer/VehiclePathFollower.gd`
- `scripts/render/vehicle_renderer/VehicleSpriteManager.gd`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | GridData, CoordinateHelper, EventBus | Phase 1 |
| Agent 02 | Home-work O-D pairs | Phase 2 (can stub with random pairs) |
| Agent 03 | Freight flows between production buildings | Phase 2 (can stub) |
| Agent 09 | Vehicle sprites, road tile sprites | Phase 5 (can use placeholders) |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| Agent 02 | Commute time per household | C# API |
| Agent 03 | Freight delivery times and costs | C# API |
| Agent 05 | Road connectivity data (is zone accessible?) | C# API |
| Agent 06 | Response time for emergency vehicles | C# API |
| Agent 08 | Traffic overlay data, transit stats, route info | C# API |

---

## Estimated Duration
- **With AI**: 8-10 days
- **Without AI**: 6-8 weeks
- **Can start**: After Agent 01 Phase 2 (Day 2)
- **Longest agent**: This is the most complex system
