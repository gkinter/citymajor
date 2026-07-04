# IRON & OAK: Vehicle & Wealth Correlation System

## Complete Design Document for Road Vehicle Simulation

---

## 1. SYSTEM OVERVIEW

### Design Philosophy

The vehicles on Iron & Oak's roads are not cosmetic decoration -- they are a **readable signal** of neighborhood health, wealth, era, and culture. A player should be able to glance at any street and immediately understand: "This is a struggling working-class neighborhood in the Industrial era" or "This is a wealthy Modern suburb." The vehicle mix tells the story.

This system hooks into the existing traffic simulation (Section 4.9 of the GDD), the wealth class system (Section 6.5), the cultural profile system (Section 6.6), and the era progression system (Section 3.3). Vehicles are the cosmetic layer spawned on top of the statistical flow model.

### Core Principle: Vehicles as Environmental Storytelling

- **Destitute neighborhoods**: Mostly pedestrians, a few rusted old vehicles, shopping carts, hand carts
- **Working-class neighborhoods**: Older model cars, heavy bus usage, work trucks
- **Middle-class neighborhoods**: Family vehicles, moderate car density, some transit use
- **Upper-middle neighborhoods**: Newer models, SUVs, occasional luxury vehicle
- **Wealthy neighborhoods**: Luxury cars, sports cars, chauffeur-driven vehicles, very few pedestrians

### Integration Points

| Existing System | How Vehicles Hook In |
|----------------|---------------------|
| Traffic flow model (4.9) | Vehicle sprites spawn along calculated flow paths |
| Wealth classes (6.5) | Vehicle type pool selected per road segment based on adjacent zone wealth |
| Cultural profile (6.6) | Regional vehicle mix modifiers applied |
| Era system (3.3) | Available vehicle pool gated by current era |
| Road network (4.2) | Vehicle speed/behavior matches road type |
| Industry (5.0) | Commercial vehicles spawn on freight routes |
| Services (GDD 10) | Emergency vehicles spawn during events |

---

## 2. VEHICLE TYPES BY WEALTH CLASS AND ERA

### 2.1 Master Vehicle Table: Frontier Era (1850-1890)

| Wealth Class | Primary Transport | Secondary Transport | Visual Indicators | Spawn Weight |
|-------------|-------------------|--------------------|--------------------|-------------|
| **Destitute** | Walking (barefoot sprites) | Hand cart, wheelbarrow | Ragged clothing, slow pace | Walk 90%, Cart 10% |
| **Working Class** | Walking (boots) | Shared horse cart, mule | Work clothes, tools visible | Walk 70%, Horse 20%, Cart 10% |
| **Middle Class** | Horse and buggy | Walking (decent clothes) | Clean buggy, single horse | Buggy 50%, Walk 40%, Horse 10% |
| **Upper-Middle** | Covered carriage | Riding horse (saddled) | Polished carriage, matched horses | Carriage 60%, Horse 30%, Walk 10% |
| **Wealthy** | Four-horse carriage | Liveried coachman | Ornate carriage, liveried driver, lanterns | Carriage 80%, Horse 15%, Walk 5% |

**Frontier Era Vehicle Pool (12 sprites total):**

| Vehicle ID | Sprite Name | Speed | Capacity | Notes |
|-----------|------------|-------|----------|-------|
| F01 | Pedestrian (poor) | 3 | 1 | Barefoot, hunched posture |
| F02 | Pedestrian (worker) | 4 | 1 | Boots, upright |
| F03 | Pedestrian (middle) | 4 | 1 | Hat, decent clothes |
| F04 | Pedestrian (wealthy) | 3 | 1 | Top hat, cane, parasol variant |
| F05 | Hand cart | 2 | 0.5 tons | Pushed by pedestrian |
| F06 | Mule cart | 5 | 1 ton | Single mule, wooden cart |
| F07 | Horse cart (open) | 6 | 2 passengers | Open bench, single horse |
| F08 | Horse buggy | 8 | 2-4 passengers | Covered, springs |
| F09 | Covered carriage | 8 | 4 passengers | Enclosed, curtained windows |
| F10 | Four-horse carriage | 10 | 4 passengers | Ornate, liveried driver sprite |
| F11 | Freight wagon | 4 | 3 tons | Heavy, two-horse, visible cargo |
| F12 | Horse-drawn omnibus | 6 | 12 passengers | Public transit, route-following |

### 2.2 Master Vehicle Table: Industrial Era (1890-1930)

| Wealth Class | Primary Transport | Secondary Transport | Visual Indicators | Spawn Weight |
|-------------|-------------------|--------------------|--------------------|-------------|
| **Destitute** | Walking | Bicycle (rusty) | Patched clothes, slow | Walk 80%, Bike 15%, Tram 5% |
| **Working Class** | Tram/streetcar | Bicycle, walking | Work overalls | Tram 35%, Walk 30%, Bike 25%, Car 10% |
| **Middle Class** | Early automobile (Model T style) | Tram | Clean car, black paint | Car 45%, Tram 25%, Walk 20%, Bike 10% |
| **Upper-Middle** | Touring car | Chauffeur automobile | Polished car, chrome details | Car 65%, Walk 15%, Tram 10%, Bike 10% |
| **Wealthy** | Luxury automobile (Rolls-Royce style) | Chauffeur-driven | Gleaming paint, ornate grille, driver sprite | Car 80%, Walk 10%, Carriage 10% |

**Industrial Era Additional Vehicles (14 new sprites):**

| Vehicle ID | Sprite Name | Speed | Capacity | Unlock |
|-----------|------------|-------|----------|--------|
| I01 | Bicycle (basic) | 6 | 1 | Era start |
| I02 | Bicycle (rusty) | 4 | 1 | Destitute variant |
| I03 | Model T sedan (black) | 15 | 4 | T004 (Asphalt Roads) |
| I04 | Model T sedan (aged) | 12 | 4 | Working-class variant, visible wear |
| I05 | Touring car | 18 | 4 | Upper-middle, open top |
| I06 | Luxury sedan (1920s) | 16 | 4 | Wealthy only, chrome detailing |
| I07 | Delivery truck (early) | 10 | 2 tons | Commercial zones |
| I08 | Electric tram | 12 | 40 passengers | T005 (Electric Tramway) |
| I09 | Horse-drawn fire engine | 10 | 6 crew | Frontier-era fire response |
| I10 | Motorized fire engine | 18 | 6 crew | Industrial-era fire response |
| I11 | Police wagon | 14 | 4 crew | T088 (Police Force) |
| I12 | Ambulance (early) | 16 | 2 patients | Industrial healthcare |
| I13 | Motorcycle (basic) | 18 | 1 | Rare, police or courier |
| I14 | Steam truck | 8 | 4 tons | Heavy freight |

### 2.3 Master Vehicle Table: Postwar Era (1930-1970)

| Wealth Class | Primary Transport | Secondary Transport | Visual Indicators | Spawn Weight |
|-------------|-------------------|--------------------|--------------------|-------------|
| **Destitute** | Walking, bus | Bicycle (old) | Worn clothes | Walk 40%, Bus 35%, Bike 20%, Old car 5% |
| **Working Class** | Older sedan (10-20yr old) | Bus, bicycle | Faded paint, some rust spots | Car 40%, Bus 30%, Walk 15%, Bike 15% |
| **Middle Class** | Family sedan (current era) | Station wagon | Clean, bright colors (pastel 50s/60s) | Car 60%, Walk 15%, Bus 15%, Bike 10% |
| **Upper-Middle** | New sedan, early SUV | Convertible | Chrome bumpers, two-tone paint | Car 70%, Walk 15%, Bus 10%, Bike 5% |
| **Wealthy** | Cadillac/Lincoln style | Sports car, chauffeur | Fins, chrome, pristine condition | Car 85%, Walk 10%, Sports 5% |

**Postwar Era Additional Vehicles (18 new sprites):**

| Vehicle ID | Sprite Name | Speed | Capacity | Notes |
|-----------|------------|-------|----------|-------|
| P01 | Sedan (1940s, worn) | 25 | 4 | Working class, faded paint |
| P02 | Sedan (1950s, clean) | 30 | 4 | Middle class, pastel colors |
| P03 | Station wagon | 28 | 6 | Middle class, family vehicle |
| P04 | Luxury sedan (1950s) | 30 | 4 | Wealthy, chrome fins, two-tone |
| P05 | Sports car (1960s) | 40 | 2 | Wealthy, convertible option |
| P06 | Pickup truck | 28 | 2 + bed | Working class, American culture mod |
| P07 | City bus | 20 | 50 passengers | T008 (Motor Bus) |
| P08 | School bus (yellow) | 22 | 40 students | T085 education + Postwar era |
| P09 | Delivery van | 22 | 1 ton | Commercial zones |
| P10 | Semi-truck | 25 | 20 tons | Freight routes only |
| P11 | Fire engine (modern) | 35 | 6 crew | Red, ladder visible |
| P12 | Police car (1960s) | 35 | 2 officers | Black/white, roof light |
| P13 | Ambulance (1960s) | 35 | 2 patients | White, red cross |
| P14 | Ice cream truck | 15 | -- | Spawns in residential, summer only |
| P15 | Taxi (yellow) | 28 | 3 passengers | Commercial/dense areas |
| P16 | Motorcycle (civilian) | 35 | 1-2 | All wealth classes |
| P17 | Scooter (Vespa style) | 20 | 1 | European culture modifier |
| P18 | Construction equipment | 8 | -- | Active construction zones only |

### 2.4 Master Vehicle Table: Modern Era (1970-2010)

| Wealth Class | Primary Transport | Secondary Transport | Visual Indicators | Spawn Weight |
|-------------|-------------------|--------------------|--------------------|-------------|
| **Destitute** | Walking, bus | Old beater (20+ yr old car) | Rust visible, exhaust smoke sprite | Walk 35%, Bus 35%, Old car 20%, Bike 10% |
| **Working Class** | Economy sedan (5-15yr old) | Bus, carpool | Clean but plain, muted colors | Car 45%, Bus 25%, Walk 15%, Bike 10%, Metro 5% |
| **Middle Class** | Family sedan, minivan, SUV | Metro, bicycle | Modern colors, clean | Car 55%, Walk 15%, Metro 15%, Bike 10%, Bus 5% |
| **Upper-Middle** | New SUV, crossover, BMW-style | Metro (choice), bicycle (fitness) | Shiny, current-year models | Car 60%, Walk 15%, Metro 10%, Bike 10%, Bus 5% |
| **Wealthy** | Luxury sedan (Mercedes style), sports car | Chauffeur SUV, town car | Black/silver, tinted windows | Car 75%, Walk 10%, Sports 10%, Limo 5% |

**Modern Era Additional Vehicles (20 new sprites):**

| Vehicle ID | Sprite Name | Speed | Capacity | Notes |
|-----------|------------|-------|----------|-------|
| M01 | Beater sedan (rusty) | 22 | 4 | Exhaust smoke particle, rattling SFX |
| M02 | Economy sedan | 35 | 4 | Honda Civic style, muted colors |
| M03 | Family sedan | 35 | 5 | Toyota Camry style |
| M04 | Minivan | 32 | 7 | Spawns near schools, residential |
| M05 | SUV (mid-range) | 35 | 5 | Ford Explorer style |
| M06 | SUV (luxury) | 35 | 5 | Range Rover style, wealthy areas |
| M07 | Sports car (modern) | 50 | 2 | Wealthy only, red/yellow |
| M08 | Luxury sedan | 38 | 4 | Mercedes/BMW style, black/silver |
| M09 | Limousine | 30 | 6 | Wealthy events, airports |
| M10 | Pickup truck (modern) | 35 | 2 + bed | American culture modifier |
| M11 | Hybrid/compact | 32 | 4 | Green culture modifier, eco districts |
| M12 | City bus (modern) | 22 | 60 passengers | Articulated variant for busy routes |
| M13 | Metro train | 40 | 200 passengers | T011 (Metro System) |
| M14 | Delivery van (branded) | 28 | 2 tons | FedEx/UPS style |
| M15 | Food truck | 15 | -- | Commercial zones, lunch hours |
| M16 | Police cruiser | 45 | 2 officers | Modern light bar |
| M17 | Fire engine (modern) | 40 | 6 crew | Full ladder truck |
| M18 | Ambulance (modern) | 40 | 2 patients | White/red, sirens |
| M19 | Garbage truck | 15 | -- | Spawns on collection routes |
| M20 | Bicycle (road bike) | 10 | 1 | Bike lane usage, fitness culture |

### 2.5 Master Vehicle Table: Future Era (2010+)

| Wealth Class | Primary Transport | Secondary Transport | Visual Indicators | Spawn Weight |
|-------------|-------------------|--------------------|--------------------|-------------|
| **Destitute** | Walking, bus, e-scooter share | Old car (15+ yr old) | Still-functioning older models | Walk 30%, Bus 30%, Old car 15%, Scooter 15%, Bike 10% |
| **Working Class** | Electric economy car, bus | Bike share, e-scooter | Small EVs, charging animation | EV 35%, Bus 25%, Walk 15%, Bike 10%, Scooter 10%, AV 5% |
| **Middle Class** | Electric SUV, autonomous pod | E-bike, metro | Sleek design, LED accents | EV 40%, AV 20%, Walk 15%, Metro 10%, Bike 10%, Bus 5% |
| **Upper-Middle** | Luxury EV (Tesla style), AV | E-bike (premium) | Smooth lines, gull-wing doors | EV 45%, AV 25%, Walk 10%, Bike 10%, Metro 10% |
| **Wealthy** | Autonomous luxury, hypercar | Personal drone (cosmetic) | Futuristic silhouette, LED underlighting | AV 40%, EV 30%, Walk 10%, Hyper 10%, Drone 10% |

**Future Era Additional Vehicles (16 new sprites):**

| Vehicle ID | Sprite Name | Speed | Capacity | Notes |
|-----------|------------|-------|----------|-------|
| FU01 | Electric economy car | 35 | 4 | Silent, no exhaust particles |
| FU02 | Electric SUV | 38 | 5 | LED light bar, smooth body |
| FU03 | Luxury EV sedan | 40 | 4 | Gull-wing animation on stop |
| FU04 | Hypercar (electric) | 55 | 2 | Wealthy only, rare spawn |
| FU05 | Autonomous pod (2-seat) | 30 | 2 | No driver sprite, sensor dome |
| FU06 | Autonomous shuttle | 25 | 8 | Public transit, fixed routes |
| FU07 | Autonomous bus | 22 | 40 | No driver, LED route display |
| FU08 | E-scooter | 12 | 1 | Shared, docked at stations |
| FU09 | E-bike | 15 | 1 | Pedal-assist, bike lanes |
| FU10 | Cargo drone | 25 (air) | 50 kg | Delivery routes, buzzing SFX |
| FU11 | Delivery robot | 5 | 20 kg | Sidewalk, last-mile delivery |
| FU12 | Maglev train | 80 | 300 passengers | T014 (Maglev Transit) |
| FU13 | Police drone | 30 (air) | -- | Surveillance, crime response |
| FU14 | Emergency AV | 50 | 2 patients | Autonomous ambulance |
| FU15 | Construction mech | 10 | -- | Future construction zones |
| FU16 | Personal drone (cosmetic) | 20 (air) | 1 | Ultra-wealthy, rare |

**Total Vehicle Sprites: ~80 unique vehicles across all 5 eras** (fits within the existing GDD estimate of ~120 vehicles including trains and water transport).

---

## 3. VEHICLE AGING & CONDITION SYSTEM

### 3.1 Vehicle Age Model

Every vehicle sprite has an implicit "age" derived from the wealth of the zone it spawns in. This is not tracked per-vehicle (too expensive) but calculated as a **probability distribution** per road segment.

**Vehicle Age Distribution by Wealth Class:**

| Wealth Class | New (0-3yr) | Recent (4-7yr) | Aging (8-15yr) | Old (16-25yr) | Beater (25+yr) |
|-------------|------------|---------------|---------------|--------------|----------------|
| Destitute | 0% | 5% | 15% | 40% | 40% |
| Working Class | 5% | 15% | 40% | 30% | 10% |
| Middle Class | 15% | 35% | 35% | 12% | 3% |
| Upper-Middle | 35% | 40% | 20% | 5% | 0% |
| Wealthy | 60% | 30% | 10% | 0% | 0% |

### 3.2 Visual Condition Tiers

Each vehicle sprite has **3 condition variants** (applied as palette swaps or overlay effects, not separate sprites):

| Condition | Visual Effect | Applied To |
|-----------|-------------|-----------|
| **Good** | Clean paint, shiny reflections, no particles | New + Recent vehicles |
| **Worn** | Faded paint, slight discoloration, matte finish | Aging vehicles |
| **Beater** | Rust spots (brown pixel overlay), exhaust smoke particle, dull colors | Old + Beater vehicles |

**Implementation**: Use a shader that shifts saturation down and adds brown noise pixels for "Worn." For "Beater," add a small grey-brown smoke particle emitter at the rear and apply heavier desaturation + noise.

### 3.3 Vehicle Safety Ratings & Accident System

Vehicle condition directly affects the accident simulation:

**Accident Rate Formula (per 1000 vehicle-trips):**
```
accident_rate = base_rate_by_road_type
  * vehicle_condition_factor    (Good: 0.8, Worn: 1.0, Beater: 1.8)
  * weather_factor              (Rain: 1.5, Snow: 2.5, Clear: 1.0)
  * congestion_factor           (1.0 + 0.5 * (volume/capacity))
  * road_condition_factor       (Good: 0.8, Fair: 1.0, Poor: 1.5, Terrible: 2.5)
  * era_safety_factor           (Frontier: 3.0, Industrial: 2.0, Postwar: 1.2, Modern: 1.0, Future: 0.5)
  * speed_factor                (1.0 + 0.02 * (speed_over_limit))
```

**Base Accident Rates by Road Type:**

| Road Type | Base Rate (per 1000 trips) | Severity Multiplier |
|-----------|---------------------------|-------------------|
| Dirt path | 5.0 | 0.3 (low speed) |
| Cobblestone | 3.0 | 0.4 |
| Paved road | 2.0 | 0.7 |
| Avenue | 2.5 | 0.8 |
| Boulevard | 3.0 | 1.0 |
| Highway | 1.5 | 1.8 (high speed) |
| Smart boulevard | 1.0 | 0.6 |
| Smart highway | 0.5 | 0.8 (AV assistance) |

**Accident Severity & Consequences:**

| Severity | Probability | Effect | Visual |
|----------|------------|--------|--------|
| Fender bender | 70% | Traffic slowdown 15min, minor repair cost | Two cars stopped, hazard flashers |
| Serious crash | 25% | Road blocked 1hr, hospital trip, insurance claim | Stopped cars, ambulance dispatched |
| Fatal crash | 5% | Road blocked 2hr, population -1, happiness hit (-2 neighborhood) | Police + ambulance + fire, blocked road |

**Beater vehicles in fatal crashes**: Fatality rate is 3x higher for Beater-condition vehicles. This creates a genuine gameplay incentive to address poverty -- poor neighborhoods have more dangerous roads.

### 3.4 Vehicle Inspection Law (Policy)

**Policy: Mandatory Vehicle Inspection**

| Setting | Effect | Cost | Public Opinion |
|---------|--------|------|---------------|
| **No inspections** (default) | No effect | $0 | Neutral |
| **Basic inspection** | Beater vehicles reduced 30%, accident rate -10% | $2/vehicle/year | Working class: -5 happiness |
| **Strict inspection** | Beater vehicles reduced 70%, accident rate -25% | $5/vehicle/year | Working class: -15 happiness, Destitute: vehicles nearly eliminated |
| **Emissions testing** (Modern+) | Beater -50%, pollution from vehicles -40% | $8/vehicle/year | All classes: -5 happiness, Environmentalists: +10 |

**Gameplay tension**: Strict inspections make roads safer but effectively remove transportation from your poorest citizens, making them reliant on transit or walking. If transit coverage is poor, this policy backfires -- unemployment rises in destitute/working neighborhoods because they cannot reach jobs.

---

## 4. VEHICLE VARIETY BY REGION & CULTURE

### 4.1 Cultural Vehicle Modifiers

When the player starts a new city, they select a **regional culture template** that modifies vehicle spawn weights. These modifiers apply ON TOP of the wealth-based spawn tables.

**Regional Templates:**

#### North American (Default)

| Modifier | Value | Effect |
|----------|-------|--------|
| Pickup truck prevalence | +40% | Pickups spawn in working + middle class |
| SUV prevalence | +30% | SUVs replace sedans in middle + upper-middle |
| Public transit usage | -20% | Fewer bus/tram riders, more car trips |
| Bicycle usage | -30% | Fewer cyclists except in "green" neighborhoods |
| Vehicle size | +15% | Average vehicle sprite slightly larger |
| Muscle/sports car prevalence | +20% | More sports cars in upper wealth tiers |

**Visual signature**: Wide roads full of large vehicles. Pickup trucks everywhere. Sparse bus stops with few riders. Rare cyclists. Parking lots visible.

#### Western European

| Modifier | Value | Effect |
|----------|-------|--------|
| Compact car prevalence | +40% | Smaller car sprites dominate |
| Bicycle usage | +80% | Heavy cycling in all wealth classes |
| Scooter/motorcycle | +50% | Vespas and motorcycles common |
| Public transit usage | +40% | Full buses, active tram lines |
| SUV prevalence | -40% | Rare SUVs |
| Pickup truck | -80% | Almost no pickups |

**Visual signature**: Narrow streets with small cars, packed bike lanes, busy tram lines, scooters weaving through traffic. Outdoor cafe seating visible.

#### East Asian

| Modifier | Value | Effect |
|----------|-------|--------|
| Kei car prevalence (Postwar+) | +60% | Tiny car sprites in working/middle class |
| Scooter prevalence | +70% | Dominant in Frontier through Postwar |
| Bicycle/rickshaw (Frontier-Industrial) | +100% | Cycle rickshaws as transit |
| Public transit usage | +60% | Extremely heavy train/bus usage |
| Luxury car prevalence | +20% | Wealthy show status through cars |
| Vehicle density | +30% | More vehicles per road segment |

**Visual signature**: Seas of scooters in early eras, tiny cars later. Extremely crowded train platforms. Cycle rickshaws in historic districts. Dense traffic.

#### Southeast Asian

| Modifier | Value | Effect |
|----------|-------|--------|
| Motorcycle/scooter | +120% | Dominant transport mode all eras |
| Tuk-tuk/auto-rickshaw | +80% | Three-wheeled taxis, Postwar+ |
| Minibus/songthaew | +60% | Informal transit, converted pickup buses |
| Bicycle | +30% | Common in rural/destitute areas |
| Car prevalence | -40% | Cars only for middle class and above |
| Traffic lane discipline | -50% | Vehicles weave, ignore lanes (visual only) |

**Visual signature**: Rivers of motorcycles with 2-3 riders each. Tuk-tuks everywhere. Colorful minibuses. Cars rare and prominent when present. Chaotic but flowing traffic.

#### Sub-Saharan African

| Modifier | Value | Effect |
|----------|-------|--------|
| Walking prevalence | +60% | Majority of trips on foot |
| Minibus taxi | +100% | Primary transit mode, informal routes |
| Motorcycle taxi (boda-boda) | +80% | Common point-to-point transport |
| Hand cart/wheelbarrow | +50% | Freight in destitute/working areas |
| Bicycle | +40% | Common utility transport |
| Car prevalence | -50% | Cars signal wealth strongly |

**Visual signature**: Crowded pedestrian streets. Packed minibuses (visible passengers on roof in early eras). Motorcycle taxis with passenger. Hand carts carrying goods. Few private cars, those present are prestigious.

#### Latin American

| Modifier | Value | Effect |
|----------|-------|--------|
| Bus prevalence | +60% | Painted buses as primary transit |
| Motorcycle prevalence | +40% | Common for working/middle class |
| Compact car prevalence | +20% | VW Beetle style in Postwar era |
| Minibus/colectivo | +50% | Informal transit routes |
| SUV prevalence | +20% | Wealthy neighborhoods |
| Bicycle | -10% | Below average cycling |

**Visual signature**: Colorful painted buses (chicken buses in early eras). VW Beetles in Postwar neighborhoods. Motorcycles weaving. SUVs in gated wealthy areas.

### 4.2 Culture Blending

As neighborhoods develop distinct cultural profiles (GDD Section 6.6), vehicle types can **blend**. An immigrant neighborhood from Southeast Asia in a North American city would show:
- Base: North American template
- Neighborhood override: +40% scooter, +20% small car, -20% pickup truck
- The blend creates a visually distinct zone that reads as culturally different

**Blending formula:**
```
final_spawn_weight = city_template_weight * 0.6 + neighborhood_culture_weight * 0.4
```

---

## 5. EMERGENCY VEHICLE SYSTEM

### 5.1 Emergency Vehicle Types by Era

| Service | Frontier | Industrial | Postwar | Modern | Future |
|---------|----------|-----------|---------|--------|--------|
| **Fire** | Bucket brigade (pedestrians) | Horse-drawn pump | Fire engine (ladder) | Full ladder truck | Drone fire suppression + AV truck |
| **Police** | Town marshal (horse) | Police wagon | Police car (B&W) | Police cruiser + helicopter | Police drone + AV cruiser |
| **Medical** | Doctor on horseback | Horse ambulance | Ambulance (van) | Ambulance + helicopter | Autonomous ambulance + medical drone |

### 5.2 Emergency Response Quality

Emergency vehicle quality scales with **era** and **service funding**:

**Response Time Formula:**
```
response_time_minutes = base_time
  * (1 / funding_ratio)              (funding_ratio = actual_funding / ideal_funding, capped 0.5-2.0)
  * distance_factor                  (1.0 + 0.1 * tiles_from_station)
  * traffic_congestion_factor        (1.0 + 0.5 * congestion_level)
  * era_speed_factor                 (Frontier: 2.0, Industrial: 1.5, Postwar: 1.0, Modern: 0.8, Future: 0.5)
```

**Emergency Vehicle Upgrade Table:**

| Era | Fire Response | Police Response | Medical Response | Cost/Vehicle/Year |
|-----|-------------|----------------|-----------------|-------------------|
| Frontier | 15 min base | 20 min base | 25 min base | $50 |
| Industrial | 10 min base | 12 min base | 15 min base | $200 |
| Postwar | 6 min base | 5 min base | 8 min base | $800 |
| Modern | 4 min base | 3 min base | 5 min base | $2,000 |
| Future | 2 min base | 2 min base | 3 min base | $5,000 |

**Visual feedback**: When an emergency occurs, the player sees the emergency vehicle leave the station, navigate through traffic (other vehicles pull aside on well-maintained roads), arrive at the scene, and handle the situation. Underfunded services show older vehicle sprites (one era behind current) and slower response.

### 5.3 Emergency Vehicle Behavior

- **Siren effect**: Vehicles within 3 tiles of an emergency vehicle slow down and pull to the side (visual + traffic flow modifier)
- **Traffic blocking**: Serious incidents block the road segment, forcing rerouting
- **Multiple dispatch**: Large events (factory fire, multi-car pileup) dispatch multiple vehicles
- **Mutual aid**: If the nearest station is too far, neighboring station sends backup (longer response time)

---

## 6. COMMERCIAL & SPECIAL VEHICLES

### 6.1 Commercial Vehicle Spawning Rules

Commercial vehicles spawn based on **nearby zone type and activity**:

| Vehicle Type | Spawn Trigger | Routes | Hours Active | Era |
|-------------|--------------|--------|-------------|-----|
| Delivery van | Commercial zone demand > 50% | Warehouse -> Commercial | 6am-8pm | Postwar+ |
| Semi-truck | Industrial output > 0 | Industrial -> Freight terminal | 24hr | Postwar+ |
| Mail truck | Post office exists | Circuit through residential | 8am-2pm | Industrial+ |
| Garbage truck | Waste service funded | Circuit through all zones | 5am-10am | Industrial+ |
| Food truck | Commercial zone + lunch hour | Parks near commercial | 11am-2pm | Postwar+ |
| Ice cream truck | Residential + summer + children | Circuit through residential | 2pm-7pm, summer only | Postwar+ |
| Moving truck | Immigration/emigration event | Highway -> Residential | Event-triggered | Postwar+ |
| Construction equipment | Active construction site | Nearest depot -> site | During construction | All eras |
| Street sweeper | Road maintenance funded | Circuit on main roads | 4am-7am | Modern+ |
| Tow truck | Accident event | Nearest garage -> accident | Event-triggered | Postwar+ |

### 6.2 Freight Vehicle Details

| Vehicle | Era | Capacity | Speed | Road Damage | Cost/Trip |
|---------|-----|----------|-------|-------------|----------|
| Hand cart | Frontier | 200 kg | 2 | None | $0.50 |
| Horse freight wagon | Frontier | 2 tons | 5 | Low | $3 |
| Steam truck | Industrial | 5 tons | 8 | Medium | $8 |
| Delivery truck | Postwar | 3 tons | 25 | Low | $5 |
| Semi-trailer | Postwar | 20 tons | 30 | Very High | $15 |
| Container truck | Modern | 25 tons | 35 | Very High | $20 |
| Electric delivery van | Future | 3 tons | 30 | Low | $3 |
| Cargo drone | Future | 50 kg | 40 (air) | None | $2 |
| Delivery robot | Future | 20 kg | 5 | None | $0.50 |

### 6.3 Special Event Vehicles

These spawn during specific game events:

| Event | Vehicle | Behavior |
|-------|---------|----------|
| Parade/Festival | Floats, marching bands (pedestrian groups) | Follow parade route, block traffic |
| Political rally | Buses bringing supporters, campaign vans | Converge on rally location |
| VIP visit | Motorcade (police + limousines) | Escorts along highway, clears traffic |
| Military (if applicable) | Military trucks, APCs | Rare, only during extreme unrest |
| Street race (crime event) | Fast cars ignoring traffic | Late night, high speed, accident risk |
| Funeral procession | Hearse + following cars | Slow convoy, other traffic yields |

---

## 7. IMPLEMENTATION & PERFORMANCE

### 7.1 Sprite Budget

The existing GDD allocates ~120 vehicle sprites. This system defines ~80 road vehicles. The remaining ~40 cover trains, boats, aircraft, and transit vehicles from the transport system (GDD Section 4).

**Sprite Optimization:**
- Vehicles use **palette swaps** for color variety (5 palette sets per base sprite)
- Condition tiers use **shader effects**, not separate sprites
- Cultural variants reuse base shapes with minor modifications (e.g., kei car = sedan sprite at 75% scale)

### 7.2 Spawning Algorithm

```
For each visible road segment:
  1. Get wealth_class from adjacent zones (average if mixed)
  2. Get culture_modifier from neighborhood cultural profile
  3. Get era from current game era
  4. Build vehicle_pool = era_vehicles filtered by wealth + culture
  5. For each vehicle slot on segment (based on traffic flow volume):
     a. Roll random against spawn_weight table
     b. Select vehicle type
     c. Roll condition (Good/Worn/Beater) from age distribution table
     d. Apply palette swap (random from 5 options)
     e. Spawn sprite, assign to flow path
  6. Cap at 500 visible vehicles total (object pool)
```

### 7.3 Performance Targets

| Metric | Target | Strategy |
|--------|--------|----------|
| Max visible vehicles | 500 | Object pooling from pre-allocated pool |
| Vehicle spawn/despawn | <0.1ms each | Pool recycling, no allocation |
| Palette swap cost | Zero runtime | Pre-computed palette textures |
| Condition shader | 1 uniform per vehicle | GPU-side, no CPU cost |
| Culling | Off-screen vehicles dormant | Camera frustum check per frame |
