# Elevation & Construction Spec

> Pure C# simulation data. No Godot dependencies in elevation/construction logic.
> Rendering hints are for the Godot frontend layer only.

---

## 1. Elevation System

### 1.1 Elevation Levels

8 discrete visual levels mapped to height units (HU). 1 HU = 2 meters.

| Level | ID | Height Range (HU) | Height (m) | Color Hint | Biome Default |
|-------|---:|-------------------:|-----------:|------------|---------------|
| Sea Level | 0 | 0 | 0 | Deep blue | Water |
| Beach | 1 | 1-2 | 2-4 | Sand/tan | Coastal |
| Flat | 2 | 3-5 | 6-10 | Green | Grassland |
| Low Hill | 3 | 6-10 | 12-20 | Light green | Grassland |
| Hill | 4 | 11-18 | 22-36 | Yellow-green | Forest |
| High Hill | 5 | 19-28 | 38-56 | Brown-green | Forest |
| Mountain | 6 | 29-42 | 58-84 | Gray-brown | Alpine |
| Peak | 7 | 43+ | 86+ | White/gray | Snow |

### 1.2 Elevation Effects

| Property | Sea (0) | Beach (1) | Flat (2) | Low Hill (3) | Hill (4) | High Hill (5) | Mountain (6) | Peak (7) |
|----------|:-------:|:---------:|:--------:|:------------:|:--------:|:--------------:|:------------:|:--------:|
| Buildable | No | Limited | Yes | Yes | Yes | Restricted | No* | No |
| Road Cost Mult | N/A | 1.3x | 1.0x | 1.2x | 1.5x | 2.0x | 3.0x* | N/A |
| Land Value Mult | N/A | 1.4x | 1.0x | 1.1x | 1.2x | 1.3x | N/A | N/A |
| Temp Offset (C) | 0 | 0 | 0 | -1 | -2 | -4 | -6 | -10 |
| Wind Mult | 1.0x | 1.3x | 1.0x | 1.2x | 1.5x | 1.8x | 2.2x | 2.8x |
| Water Flow Dir | Sink | Sink | Flat | Down | Down | Down | Down | Source |
| Water Flow Rate | N/A | Low | None | Low | Medium | Medium | High | High |
| Max Building Height | 0 | 2 floors | Unlimited | 8 floors | 4 floors | 2 floors | 0 | 0 |

\* Mountain (6) buildable only with tunnel or terrace tech (Modern era+).

**Beach (1) restrictions**: No heavy industry, no high-rises. Residential and tourism only. Flood risk during storms.

### 1.3 Slope Calculation

Slope between two adjacent tiles:

```
slope = abs(height_a - height_b) / tile_distance
```

- `tile_distance` = 1.0 for cardinal neighbors, 1.414 for diagonal
- Slope expressed as ratio (0.0 = flat, 1.0 = 45 degrees)

| Slope Range | Classification | Buildable | Road OK | Rail OK |
|-------------|---------------|:---------:|:-------:|:-------:|
| 0.00 - 0.10 | Flat | Yes | Yes | Yes |
| 0.11 - 0.25 | Gentle | Yes | Yes | Yes (reduced speed) |
| 0.26 - 0.40 | Moderate | Yes | Yes (1.5x cost) | No (needs tunnel/bridge) |
| 0.41 - 0.60 | Steep | Terracing req'd | Yes (2x cost) | No |
| 0.61 - 0.80 | Very Steep | No | Switchback only | No |
| 0.81+ | Cliff | No | No (tunnel only) | No |

**Max buildable slope**: 0.40 without terracing, 0.60 with terracing (Modern era+).

### 1.4 Terrain Modification

| Operation | Cost/tile | Time (days) | Era Required | Slope Limit |
|-----------|----------:|------------:|-------------|-------------|
| Flatten (+/- 1 HU) | $500 | 5 | Frontier | Any |
| Flatten (+/- 3 HU) | $2,000 | 15 | Industrial | Any |
| Terrace (create flat platform) | $5,000 | 30 | Postwar | <= 0.60 |
| Major excavation (+/- 5 HU) | $10,000 | 45 | Modern | Any |
| Landfill (raise from sea) | $15,000 | 60 | Industrial | Sea level only |
| Seawall (protect from erosion) | $8,000 | 20 | Industrial | Beach only |

---

## 2. Bridges

### 2.1 Bridge Types

| Type | ID | Era | Max Span (tiles) | Capacity (veh/hr) | Base Cost | Per-Tile Cost | Maint/yr | Material | Min Clearance (HU) |
|------|---:|-----|------------------:|-------------------:|----------:|--------------:|---------:|----------|--------------------:|
| Wooden | 0 | Frontier | 6 | 200 | $2,000 | $800 | $300 | Timber | 2 |
| Stone Arch | 1 | Frontier | 12 | 500 | $8,000 | $3,000 | $200 | Stone | 3 |
| Iron Truss | 2 | Industrial | 20 | 1,500 | $15,000 | $5,000 | $600 | Iron | 4 |
| Steel Girder | 3 | Postwar | 30 | 3,000 | $30,000 | $8,000 | $800 | Steel | 5 |
| Cable-Stayed | 4 | Modern | 50 | 5,000 | $80,000 | $12,000 | $1,500 | Steel/Concrete | 6 |
| Suspension | 5 | Modern | 80 | 6,000 | $150,000 | $15,000 | $2,500 | Steel Cable | 8 |

### 2.2 Cost Formula

```
total_cost = base_cost + (per_tile_cost * span_length) + (height_penalty * max_pier_height)
```

Where:
- `span_length` = number of tiles between abutments
- `max_pier_height` = highest pier in HU
- `height_penalty` = `per_tile_cost * 0.15` per HU of pier height

### 2.3 Failure Conditions

| Type | Max Load (tons) | Max Wind (km/h) | Lifespan (yrs) | Flood Vulnerability | Earthquake Vulnerability |
|------|----------------:|-----------------:|----------------:|:-------------------:|:------------------------:|
| Wooden | 10 | 80 | 30 | High | High |
| Stone Arch | 30 | 150 | 200 | Medium | Medium |
| Iron Truss | 60 | 120 | 80 | Medium | High |
| Steel Girder | 100 | 140 | 100 | Low | Medium |
| Cable-Stayed | 150 | 160 | 80 | Low | Low |
| Suspension | 120 | 180 | 100 | Low | Medium |

**Failure triggers**: Any condition exceeding max threshold has a per-tick probability of damage:
- `damage_chance = (actual / max)^3 * 0.01` per game-day when over threshold
- Damage accumulates 0-100%. At 100%, bridge collapses (vehicles reroute).
- Repair cost = `total_cost * damage_pct * 0.5`

### 2.4 Bridge Width Variants

Each bridge type supports 3 widths:

| Width | Lanes | Cost Mult | Capacity Mult |
|-------|------:|----------:|--------------:|
| Narrow | 1 | 0.7x | 0.5x |
| Standard | 2 | 1.0x | 1.0x |
| Wide | 4 | 1.8x | 1.8x |

---

## 3. Tunnels

### 3.1 Tunnel Types

| Type | ID | Era | Max Depth (HU) | Cost/tile | Capacity (veh/hr) | Ventilation | Construction Time (days/tile) | Max Length (tiles) |
|------|---:|-----|----------------:|----------:|-------------------:|-------------|------------------------------:|-------------------:|
| Cut-and-Cover | 0 | Industrial | 5 | $10,000 | 2,000 | Natural | 10 | 20 |
| Bored (Single) | 1 | Postwar | 20 | $25,000 | 1,500 | Mechanical | 20 | 40 |
| Bored (Double) | 2 | Modern | 30 | $45,000 | 3,500 | Mechanical | 25 | 60 |
| Immersed Tube | 3 | Modern | 15 (underwater) | $60,000 | 4,000 | Mech + Emergency | 30 | 30 |

### 3.2 Boring Cost Formula

```
total_cost = per_tile_cost * length * depth_multiplier * rock_multiplier
```

| Factor | Value | Condition |
|--------|------:|-----------|
| `depth_multiplier` | 1.0 | Depth <= 5 HU |
| `depth_multiplier` | 1.0 + (depth - 5) * 0.08 | Depth 6-20 HU |
| `depth_multiplier` | 2.2 + (depth - 20) * 0.12 | Depth 21+ HU |
| `rock_multiplier` | 0.8 | Soft soil |
| `rock_multiplier` | 1.0 | Clay/sediment |
| `rock_multiplier` | 1.4 | Hard rock |
| `rock_multiplier` | 1.8 | Granite/basalt |

### 3.3 Ventilation Requirements

| Tunnel Length | Ventilation Type | Cost/tile | Power Draw (kW) |
|--------------:|-----------------|----------:|-----------------:|
| 1-8 tiles | Natural (portals) | $0 | 0 |
| 9-20 tiles | Longitudinal fans | $2,000 | 50 |
| 21-40 tiles | Transverse ducts | $5,000 | 150 |
| 41+ tiles | Full transverse + emergency shafts | $10,000 | 300 |

**Safety**: Tunnels > 15 tiles require emergency exits every 10 tiles ($8,000 each). Tunnels without adequate ventilation accumulate pollution, triggering a health penalty for passengers and eventual closure.

### 3.4 Tunnel Use Cases

| Scenario | Recommended Type | Notes |
|----------|-----------------|-------|
| Road under river | Immersed Tube | Cheapest for shallow underwater crossings |
| Road through hill | Cut-and-Cover | If hill is < 5 HU above road level |
| Rail through mountain | Bored (Single/Double) | Only option for deep mountain crossings |
| Metro line | Bored (Single) | Standard for urban subway construction |

---

## 4. Highway System

### 4.1 Interchange Types

| Type | ID | Footprint (tiles) | Capacity (veh/hr) | Cost | Era | Roads Connected |
|------|---:|-------------------:|-------------------:|-----:|-----|----------------:|
| Diamond | 0 | 5x5 | 4,000 | $50,000 | Postwar | 2 |
| Cloverleaf | 1 | 7x7 | 6,000 | $120,000 | Postwar | 2 |
| Stack (2-level) | 2 | 5x5 | 8,000 | $200,000 | Modern | 2 |
| Turbine | 3 | 8x8 | 10,000 | $300,000 | Modern | 2 |
| System (4-way stack) | 4 | 9x9 | 14,000 | $500,000 | Modern | 4 |

**Interchange cost formula**:
```
total = base_cost + (elevation_levels * base_cost * 0.3)
```

### 4.2 On/Off Ramps

| Property | Value |
|----------|-------|
| Footprint | 3 tiles long, 1 tile wide |
| Merge capacity | 1,200 veh/hr per ramp |
| Cost | $15,000 per ramp |
| Min spacing | 8 tiles between consecutive ramps |
| Weave penalty | -20% capacity if ramps within 5 tiles |

### 4.3 Sound Walls

| Property | Value |
|----------|-------|
| Cost/tile | $1,500 |
| Noise reduction | -15 dB (eliminates highway noise penalty for adjacent 2 tiles) |
| Land value recovery | +80% of highway noise penalty removed |
| Height | 2 HU |
| Era | Postwar+ |

### 4.4 HOV Lanes

| Property | Value |
|----------|-------|
| Cost | +40% over standard lane |
| Capacity | 1,500 veh/hr (dedicated) |
| Min occupancy | 2+ passengers (configurable: 2+, 3+) |
| Violation fine | $200 (player-configurable) |
| Conversion | Any highway lane can be converted; 30-day construction |
| Era | Modern+ |

### 4.5 Toll Plazas

| Property | Value |
|----------|-------|
| Footprint | 3 tiles wide x 2 tiles deep per booth |
| Booths per plaza | 2-8 (player chooses) |
| Throughput/booth | 400 veh/hr (manual), 1,200 veh/hr (electronic) |
| Cost/booth | $10,000 (manual), $25,000 (electronic) |
| Revenue | Player-set toll rate x traffic volume |
| Electronic tolling | Modern era+ (no stopping, full-speed) |
| Queue overflow | Backs up onto highway; -50% capacity per 5-tile queue |

---

## 5. Elevated Infrastructure

### 5.1 Elevated Structure Types

| Type | ID | Era | Height (HU) | Lanes/Tracks | Capacity | Cost/tile | Maint/yr/tile | Min Curve Radius |
|------|---:|-----|------------:|-------------:|---------:|----------:|--------------:|-----------------:|
| Elevated Highway | 0 | Postwar | 4 | 2-6 | 3,000-8,000 veh/hr | $20,000 | $800 | 4 tiles |
| Elevated Rail | 1 | Industrial | 3 | 1-2 | 20 trains/hr | $15,000 | $600 | 6 tiles |
| Elevated Monorail | 2 | Modern | 3 | 1 | 12 trains/hr | $18,000 | $400 | 3 tiles |
| Elevated Walkway | 3 | Modern | 2 | N/A | 5,000 ped/hr | $5,000 | $200 | 2 tiles |
| Elevated Busway | 4 | Modern | 4 | 2 | 2,000 veh/hr | $16,000 | $500 | 4 tiles |

### 5.2 Support Columns

| Span Between Columns | Cost Mult | Visual Weight |
|----------------------:|----------:|---------------|
| Every tile | 1.0x | Heavy (urban blight) |
| Every 2 tiles | 1.3x | Standard |
| Every 3 tiles | 1.8x | Light |
| Every 4 tiles | 2.5x | Minimal (Modern+ only) |

**Land value penalty** under elevated structures:

| Structure | Land Value Penalty | Noise Penalty | Shadow Penalty |
|-----------|-------------------:|--------------:|---------------:|
| Elevated Highway | -30% | -20% | -10% |
| Elevated Rail | -20% | -15% | -10% |
| Elevated Monorail | -10% | -5% | -5% |
| Elevated Walkway | -5% | 0% | -5% |
| Elevated Busway | -15% | -10% | -10% |

### 5.3 Stacking Rules

- Max 2 elevated layers above ground level (ground + 2 elevated = 3 total)
- Each additional layer: +60% cost, +40% maintenance
- Layer order (bottom to top): ground road, elevated rail, elevated highway
- Walkways attach to any layer at no stacking penalty
- Interchanges between layers require ramp structures (see 4.2 costs, +50% for elevated)

### 5.4 Isometric Rendering Approach

| Layer | Z-Order | Render Method |
|-------|--------:|---------------|
| Ground terrain | 0 | TileMap base layer |
| Ground roads/rails | 1 | TileMap overlay |
| Support columns | 2 | Sprite instances (RenderingServer) |
| Elevated deck (layer 1) | 3 | Separate TileMap, offset Y by height |
| Vehicles on layer 1 | 4 | RenderingServer sprites |
| Elevated deck (layer 2) | 5 | Separate TileMap, offset Y by height x 2 |
| Vehicles on layer 2 | 6 | RenderingServer sprites |
| Buildings | 7-15 | Sorted by footprint Y + height |

**Transparency**: When camera zooms below an elevated structure, upper layers fade to 30% opacity. Configurable per-layer toggle in UI.

**Occlusion**: Buildings behind elevated structures use depth-sorted rendering. Elevated decks cast simplified shadow rectangles onto ground layer (shader-based, not ray-traced).

---

## 6. Underground Construction

### 6.1 Depth Levels

| Level | Name | Depth (m) | Typical Use |
|-------|------|-----------|-------------|
| 1 | Shallow | 0-10 | Utilities, pedestrian tunnels, basements |
| 2 | Medium | 10-30 | Metro lines, parking garages, shopping arcades |
| 3 | Deep | 30-60 | Deep metro, cisterns, bunkers, geothermal |

### 6.2 Buildable Structures by Depth

| Structure | Depth 1 | Depth 2 | Depth 3 | Era Available |
|-----------|:-------:|:-------:|:-------:|---------------|
| Water pipes | x | x | - | Industrial |
| Sewer lines | x | x | - | Industrial |
| Power cables | x | - | - | Industrial |
| Fiber optic | x | - | - | Modern |
| Pedestrian tunnel | x | x | - | Industrial |
| Road tunnel | - | x | x | Modern |
| Metro station | - | x | x | Modern |
| Metro track | - | x | x | Modern |
| Underground parking | x | x | - | Modern |
| Underground shopping | - | x | - | Modern (Future) |
| Cistern / reservoir | - | - | x | Industrial |
| Geothermal plant | - | - | x | Modern |
| Civil defense bunker | - | - | x | Modern |

### 6.3 Cost Multipliers

| Depth Level | Base Cost Multiplier | Maintenance Multiplier | Construction Time Multiplier |
|-------------|:--------------------:|:----------------------:|:----------------------------:|
| 1 (Shallow) | 1.5x | 1.2x | 1.3x |
| 2 (Medium) | 3.0x | 1.8x | 2.0x |
| 3 (Deep) | 6.0x | 2.5x | 3.5x |

Hard rock adds +50% to all multipliers. Water table present adds +30%.

### 6.4 Overlap / Stacking Rules

| Above | Below | Allowed | Notes |
|-------|-------|:-------:|-------|
| Road | Depth 1 utility | Yes | Default for all roads |
| Road | Depth 2 metro | Yes | Requires reinforced surface |
| Building | Depth 1 parking | Yes | Building gains parking capacity |
| Building | Depth 2 metro station | Yes | Building gains transit bonus |
| Metro track | Metro track (lower) | No | Same alignment prohibited |
| Water pipe | Sewer line (same depth) | No | Minimum 2-tile horizontal separation |
| Any surface | Depth 3 anything | Yes | Deep level never conflicts with surface |
| Park / plaza | Depth 1 shopping | Yes | Access via stairwell tiles |

---

## 7. Slope Mechanics

### 7.1 Road Grade Limits

| Infrastructure | Max Grade (%) | Max Grade (degrees) | Exceeding Limit |
|----------------|:-------------:|:-------------------:|-----------------|
| Local road | 8% | 4.6 | Construction blocked |
| Collector road | 6% | 3.4 | Construction blocked |
| Highway | 6% | 3.4 | Construction blocked |
| Rail (standard) | 4% | 2.3 | Construction blocked |
| Rail (high-speed) | 3% | 1.7 | Construction blocked |
| Pedestrian path | 12% | 6.8 | Requires stairs above 12% |
| Bicycle lane | 6% | 3.4 | Speed penalty above 4% |

### 7.2 Speed Reduction by Grade

| Grade (%) | Vehicle Speed Modifier | Truck Speed Modifier | Rail Speed Modifier |
|:---------:|:----------------------:|:--------------------:|:-------------------:|
| 0-2 | 1.00x | 1.00x | 1.00x |
| 2-4 | 0.95x | 0.85x | 0.90x |
| 4-6 | 0.85x | 0.70x | 0.75x |
| 6-8 | 0.70x | 0.55x | N/A |
| 8-10 | 0.55x | N/A | N/A |
| 10-12 | 0.40x (paths only) | N/A | N/A |

Downhill: speed modifier inverted (faster), but braking distance increases proportionally.

### 7.3 Slope Infrastructure

| Structure | Max Grade Handled | Era | Cost (relative) | Notes |
|-----------|:-----------------:|-----|:----------------:|-------|
| Switchback road | 15% effective | Pre-industrial | 2.5x | Zigzag path, 3x land use |
| Funicular | 50% | Industrial | 4.0x | Fixed route, low capacity (500/hr) |
| Cable car | 60% | Industrial | 5.0x | Point-to-point, 800/hr |
| Rack railway | 25% | Industrial | 3.5x | Slow (30 km/h max) |
| Escalator (outdoor) | 30% | Modern | 2.0x | Pedestrian only, 2000/hr |
| Gondola lift | 80% | Modern | 6.0x | Weather-sensitive, 1200/hr |

### 7.4 Cut-and-Fill Earthwork

Cost formula per tile:

```
earthwork_cost = base_rate * abs(elevation_change) * soil_modifier * area
```

| Soil Type | Modifier | Excavation Speed |
|-----------|:--------:|:----------------:|
| Sand / loose | 0.8x | Fast |
| Clay / soil | 1.0x | Normal |
| Gravel / mixed | 1.3x | Normal |
| Soft rock | 2.0x | Slow |
| Hard rock | 4.0x | Very slow |

Fill material can be sourced from cut operations within 20 tiles at no extra cost. Beyond 20 tiles, transport cost = `distance * 0.05 * volume`.

---

## 8. Water Crossings

### 8.1 Crossing Types

| Crossing | Era | Base Cost | Capacity (vehicles/hr) | Max Span (tiles) | Special Effects |
|----------|-----|:---------:|:----------------------:|:-----------------:|-----------------|
| Ford | Pre-industrial | 0.1x | 50 | 2 | Impassable during floods; no maintenance |
| Ferry | Pre-industrial | 0.5x | 200 | Unlimited | Slow crossing (2 min); requires docks both sides |
| Pontoon bridge | Industrial | 1.5x | 400 | 6 | Blocks river traffic; removed during floods |
| Stone bridge | Pre-industrial | 3.0x | 600 | 4 | Permanent; +aesthetic bonus to nearby tiles |
| Steel bridge | Industrial | 4.0x | 1200 | 12 | Enables rail; unlocks with steel tech |
| Suspension bridge | Modern | 8.0x | 2400 | 30 | Landmark potential; high maintenance |
| Cable-stayed bridge | Modern | 7.0x | 2400 | 20 | Faster to build than suspension |
| Tunnel (submerged) | Modern | 10.0x | 3000 | Unlimited | No weather impact; highest capacity |
| Dam | Industrial | 6.0x | 200 (road on top) | 8 | Generates power; creates reservoir upstream |
| Flood barrier | Modern | 5.0x | 0 | 10 | Protects area; can open/close for shipping |
| Weir | Pre-industrial | 1.0x | 0 | 6 | Controls water level; fish passage if upgraded |
| Aqueduct | Pre-industrial | 2.5x | 0 (water only) | 15 | Supplies water across valleys; landmark |

### 8.2 Bridge Construction Constraints

| Constraint | Rule |
|------------|------|
| Foundation type | Shallow water: standard piers. Deep water: caisson (+50% cost) |
| River traffic | Bridges below 8m clearance block large vessels |
| Wind exposure | Suspension bridges closed at wind > 80 km/h |
| Ice | Pontoon bridges destroyed by ice; others take 10% damage/winter |
| Seismic zone | All bridges require +20% cost for seismic reinforcement |

---

## 9. Terrain Modification

### 9.1 Operations

| Operation | Description | Cost per Tile per 1m Change | Time per Tile per 1m | Environmental Impact |
|-----------|-------------|:---------------------------:|:--------------------:|---------------------|
| Flatten | Level terrain to target elevation | 1.0x | 1.0x | Low |
| Embankment | Raise terrain with fill material | 1.2x | 1.2x | Low-Medium |
| Cutting | Excavate through hill/ridge | 1.5x | 1.5x | Medium |
| Landfill | Extend land into shallow water | 3.0x | 3.0x | High |
| Dredging | Deepen waterway or create channel | 2.5x | 2.0x | High |
| Seawall | Armored coastal protection | 4.0x | 2.5x | Medium |
| Terracing | Step-cut hillside for building | 2.0x | 2.0x | Low-Medium |
| Canal | Excavate navigable waterway | 3.5x | 3.5x | High |

### 9.2 Cost Scaling

Cost increases non-linearly with elevation change:

| Elevation Change (m) | Cost Multiplier | Notes |
|:---------------------:|:---------------:|-------|
| 1 | 1.0x | Base rate |
| 2 | 2.2x | Slightly super-linear |
| 3 | 3.6x | Heavy equipment needed |
| 4 | 5.2x | Major earthworks |
| 5+ | 7.0x + 2.0x per additional m | Megaproject territory |

### 9.3 Environmental Impact

| Impact Level | Effects |
|:------------:|---------|
| None | No penalties |
| Low | Minor approval delay (+10% construction time) |
| Low-Medium | Requires environmental review; nearby parks offset |
| Medium | -5 approval rating; requires mitigation (tree planting, retention ponds) |
| High | -15 approval rating; protest risk; mandatory environmental impact study (30 days); may be blocked by green policies |

Mitigation options: tree planting (-3 impact), retention pond (-5 impact), wildlife corridor (-4 impact), habitat restoration (-8 impact, slow).

---

## 10. Construction Animation Phases

### 10.1 Phase Definitions

All major structures (bridges, tunnels, elevated roads, large buildings) progress through 5 visual phases.

| Phase | Name | Visual Description |
|:-----:|------|--------------------|
| 1 | Site Prep | Ground cleared, safety barriers placed, excavation markers visible |
| 2 | Foundation | Piers/footings rise from ground, scaffolding appears, concrete pours |
| 3 | Structural | Main span or frame erected, cranes active, steel/concrete skeleton visible |
| 4 | Finishing | Deck/surface laid, facade applied, railings and signage installed |
| 5 | Completion | Scaffolding removed, road markings painted, landscaping around structure |

### 10.2 Duration by Structure Size

| Structure Category | Small (1-4 tiles) | Medium (5-12 tiles) | Large (13-30 tiles) | Mega (30+ tiles) |
|--------------------|--------------------|---------------------|---------------------|-------------------|
| Road segment | 5s / 5s / 5s / 3s / 2s | - | - | - |
| Bridge | 8s / 10s / 15s / 10s / 7s | 10s / 15s / 25s / 15s / 10s | 15s / 20s / 40s / 25s / 15s | 20s / 30s / 60s / 35s / 20s |
| Tunnel | 10s / 15s / 20s / 10s / 5s | 15s / 20s / 35s / 15s / 10s | 20s / 30s / 50s / 25s / 15s | 25s / 40s / 70s / 35s / 20s |
| Elevated road | 8s / 12s / 18s / 10s / 7s | 10s / 18s / 30s / 15s / 10s | 15s / 25s / 45s / 25s / 15s | 20s / 35s / 60s / 30s / 20s |
| Large building | 10s / 15s / 25s / 15s / 10s | 15s / 20s / 35s / 20s / 15s | 20s / 30s / 50s / 30s / 20s | - |

Format: Phase 1 / Phase 2 / Phase 3 / Phase 4 / Phase 5 duration. Game-time seconds at 1x speed.

### 10.3 Construction Details

| Element | Appears in Phase | Disappears in Phase |
|---------|:----------------:|:-------------------:|
| Safety barriers | 1 | 5 |
| Excavators | 1-2 | 3 |
| Cranes | 2 | 5 |
| Scaffolding | 2 | 5 |
| Concrete trucks | 2-3 | 4 |
| Worker figures | 1 | 5 |
| Dust particles | 1-3 | 4 |
| Welding sparks | 3 | 4 |

Player can click any construction site to see: current phase, time remaining, total cost spent / remaining, and a progress bar.
