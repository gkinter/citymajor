# IRON & OAK: Final Gap Fills

## All Remaining Design Specifications — Zero Gaps After This Document

**Cross-references**: VEHICLE_WEALTH_SYSTEM.md (spawn tables), AGENT_04_TRANSPORT.md (traffic flow, BPR formula), CULTURAL_DNA_EMERGENCE.md (cultural dimensions), MISSING_SYSTEMS.md (emergency services, childcare), EXPANDED_ZONES_EVENTS_SPORTS.md (zone sub-types), Research 11 (MNL mode choice model), AGENT_02_SIMULATION.md (Household/Citizen structs)

---

# 1. PARKING SYSTEM

## 1.1 Parking Supply Buildings

| Type | Size | Spaces | Cost | Maint/yr | Era | Land Value Effect | Notes |
|------|------|--------|------|----------|-----|-------------------|-------|
| **On-street parking** | Per road tile | 2 per tile | Free (auto) | $0 | All | None | Default on all non-highway, non-pedestrian roads |
| **Metered on-street** | Per road tile | 2 per tile | $50/meter | $10 | Industrial+ | None | Requires parking meter placement |
| **Surface parking lot** | 3x3 | 50 | $2,000 | $200 | Industrial+ | -8 adjacent tiles | Asphalt, no structure, heat island |
| **Parking garage** | 2x2 | 200 | $15,000 | $1,500 | Postwar+ | -3 adjacent tiles | Multi-story concrete structure |
| **Underground parking** | 2x2 (under building) | 150 | $25,000 | $2,000 | Postwar+ | None (hidden) | Built beneath any building, no visual footprint |
| **Park-and-ride** | 3x2 | 100 | $8,000 | $800 | Postwar+ | -2 adjacent tiles | Must be adjacent to a transit stop |
| **Automated garage** | 2x2 | 300 | $40,000 | $3,000 | Future | None | Robotic stacking, 50% more capacity per footprint |

**On-street parking auto-generation**: Every road tile (Tier 1-4) automatically provides 2 on-street spaces unless the player designates the road as no-parking, bike lane, or bus lane. Tier 5+ roads (highways, expressways) never have on-street parking. Pedestrian zones (Tier 8) have zero vehicle parking.

## 1.2 Parking Demand Model

Demand is calculated per-building, aggregated to zone level:

```
Residential:
  parking_demand = households_with_car * 1.0
  // Each car-owning household needs exactly 1 home space
  // Household.Flags.has_car from AGENT_02 struct

Commercial (low density):
  parking_demand = workers * 0.3 + (served_population * visit_rate * 0.5)
  // visit_rate: corner store 0.4, supermarket 0.6, mall 0.8

Commercial (high density):
  parking_demand = workers * 0.3 + customers_per_hour * 0.5
  // customers_per_hour from commercial sub-type table (Section 6)

Office:
  parking_demand = workers * 0.8
  // Transit-adjacent offices: workers * 0.4 (transit substitution)

Industrial:
  parking_demand = workers * 0.6 + freight_bays * 1.0
  // Freight bays: 1 per 500 sqm of industrial floor space

Civic/Service:
  parking_demand = staff * 0.5 + daily_visitors * 0.3
  // Hospitals, schools, libraries, government buildings
```

## 1.3 Parking Search (Cruising) Model

When supply < demand in a zone, excess drivers cruise for parking. This generates phantom traffic that congests roads without productive movement.

```
supply_ratio = parking_supply / parking_demand  // per zone, clamped [0.1, 5.0]

IF supply_ratio < 1.0:
  cruising_fraction = (1.0 - supply_ratio) ^ 2
  // At 50% supply (ratio 0.5): cruising_fraction = 0.25 (25% of parkers cruise)
  // At 20% supply (ratio 0.2): cruising_fraction = 0.64 (64% cruise)

  cruising_time_minutes = 5.0 * (parking_demand / parking_supply) ^ 2
  // Base 5 min when demand = supply, escalates quadratically
  // Clamped to max 30 minutes (after which driver gives up, parks illegally or leaves)

  cruising_vehicles = car_trips_to_zone * cruising_fraction
  average_cruising_distance_tiles = cruising_time_minutes * 0.5
  // 0.5 tiles/minute at slow cruising speed (15 km/h in a 30 km/h zone)

  extra_traffic_volume = cruising_vehicles * average_cruising_distance_tiles
  // Added to road segment volumes in the BPR congestion formula (AGENT_04 Phase 2)

ELSE:
  cruising_fraction = 0
  cruising_time_minutes = 0
```

**Real-world calibration**: Shoup (2005) found 30% of downtown traffic in major US cities is cruising for parking. The model caps cruising contribution at 35% of zone traffic volume to match this empirical ceiling.

**Illegal parking**: When cruising_time > 15 minutes and enforcement is low, 20% of drivers park illegally (double-park, block fire hydrants). Illegal parking reduces road capacity by 10% per illegally parked vehicle on that segment. Police enforcement reduces illegal parking by 5% per patrol unit assigned to the district.

## 1.4 Parking Meters

**Placement**: Player places meters on individual road tiles. Each meter covers both on-street spaces on that tile.

| Setting | Range | Default | Effect |
|---------|-------|---------|--------|
| Hourly rate | $0.50 - $5.00 | $1.00 | Higher rate = shorter average stay = more turnover |
| Time limit | 30min - 4hr | 2hr | Shorter limit = higher turnover, penalizes long stays |
| Operating hours | 6am-midnight | 8am-6pm | Off-hours = free parking |
| Enforcement level | None / Low / High | Low | High = $50 fine, 90% compliance; Low = $25 fine, 60% compliance |

**Revenue formula**:
```
meter_revenue_daily = spaces * occupancy_rate * hourly_rate * operating_hours * compliance_rate

// Example: 2 spaces, 75% occupancy, $2/hr, 10 operating hours, 80% compliance
// = 2 * 0.75 * 2.0 * 10 * 0.80 = $24/day per meter tile
```

**Turnover effect**: Meters increase effective capacity by encouraging shorter stays.
```
effective_supply_multiplier = 1.0 + (hourly_rate / 5.0) * 0.5
// At $5/hr (max rate): effective supply = 1.5x nominal spaces
// At $1/hr: effective supply = 1.1x
```

## 1.5 Parking Policy (District-Level)

Policies are set per district via the district policy panel (AGENT_07 political system).

| Policy | Effect on Buildings | Effect on Transport | Effect on City |
|--------|--------------------|--------------------|----------------|
| **Parking minimums** (default) | Each new building must include X spaces per unit/sqm | Encourages car ownership, increases car mode share +5% | Sprawl: buildings larger footprint, lower density, more impervious surface |
| **Parking maximums** | Cap spaces per building at Y (< minimum) | Discourages car ownership if transit available, car share -5% | Density: smaller footprints, more buildable land, requires good transit or backlash |
| **Abolish minimums** | No parking requirement; builders choose | Market-driven; dense areas build less parking, suburban areas still build it | TOD-friendly; if transit poor, -15% residential desirability in affected zones |
| **Resident permits** | N/A | Prevents commuter parking on residential streets | Protects neighborhoods; commuters forced to use garages or transit |
| **Dynamic pricing** | N/A | Meter rates auto-adjust: target 85% occupancy (Shoup optimal) | Revenue maximized, cruising minimized; requires Future era tech |

**Parking minimum table** (when minimums policy active):

| Building Type | Required Spaces | Example |
|---------------|----------------|---------|
| Residential (per unit) | 1.0 | 20-unit apartment = 20 spaces |
| Office (per 100 sqm) | 3.0 | 1000 sqm office = 30 spaces |
| Retail (per 100 sqm) | 5.0 | 500 sqm supermarket = 25 spaces |
| Restaurant (per seat) | 0.33 | 60-seat restaurant = 20 spaces |
| Hospital (per bed) | 1.5 | 100-bed hospital = 150 spaces |

**Parking maximum table** (when maximums policy active, values are 40-60% of minimums):

| Building Type | Max Spaces | Transit Adjacency Bonus |
|---------------|-----------|------------------------|
| Residential (per unit) | 0.5 | 0.3 if within 5 tiles of rail/BRT |
| Office (per 100 sqm) | 1.5 | 1.0 if within 5 tiles of rail/BRT |
| Retail (per 100 sqm) | 2.5 | 1.5 if within 5 tiles of rail/BRT |

## 1.6 Parking Effects on Simulation

```
Insufficient parking (supply_ratio < 0.7):
  commercial_desirability_modifier = -0.20
  traffic_congestion_bonus = +0.15 (from cruising)
  citizen_satisfaction_penalty = -5 (per affected zone)
  business_revenue_modifier = -0.10 (customers can't park, shop elsewhere)

Excess parking (supply_ratio > 2.0):
  land_value_modifier = -0.05 per excess ratio point
  heat_island_effect = +1°C per 10 surface lot tiles in zone
  stormwater_runoff = +15% impervious surface contribution
  visual_quality = -3 (ugly asphalt deserts)

Well-managed parking (supply_ratio 0.85 - 1.15, meters active):
  commercial_revenue_modifier = +0.10
  congestion_reduction = -0.10 (less cruising)
  meter_revenue = see formula above
  citizen_satisfaction_bonus = +2
```

---

# 2. INFORMAL ECONOMY SYSTEM

## 2.1 Spawn Conditions

Informal commercial activity emerges organically when ALL of these conditions are met:

```
district_poverty_rate > 0.40          // >40% households are destitute or working class
AND formal_commercial_within_8_tiles == 0   // No formal shops nearby (commercial desert)
AND population_density > era_threshold       // Frontier: 50/km², Industrial: 200/km², Modern: 500/km²
AND cultural_informal_economy > 50          // Cultural dimension (see 2.5)
```

**Spawn check**: Runs once per game-week for each eligible district. Spawn probability when all conditions met: 30% per check. Maximum informal entities per district: population / 500.

## 2.2 Informal Entity Types

| Type | Size | Workers | Revenue | Tax Revenue | Customers/day | Spawn Condition |
|------|------|---------|---------|-------------|---------------|-----------------|
| **Street vendor** | 1 tile (sidewalk) | 1 | $5/day | $0 | 15-30 | Base condition met |
| **Informal market cluster** | 2x2 | 5-10 | $30/day | $0 | 80-150 | 3+ adjacent street vendors auto-merge |
| **Hawker center** | 3x3 | 12-20 | $80/day | $8/day (if formalized) | 200-400 | Player upgrades informal market ($1,000) |
| **Mobile vendor** | 1 tile (moving) | 1 | $3/day | $0 | 8-15 | Pedestrian route with foot traffic > 50/hr |

**Street vendor sub-types** (cosmetic, selected randomly at spawn):
- Food vendor (cooking smoke particle, food stall sprite)
- Goods vendor (tarp with items displayed, no particles)
- Repair vendor (tools visible, occasional spark particle)
- Water/drink vendor (cart with bottles/containers)

## 2.3 Informal Economy Economics

```
Tax revenue: $0 (informal = untaxed)
Service provision: vendors provide essential goods to underserved neighborhoods
  - Food vendor within 5 tiles: reduces "food desert" penalty by 50%
  - Goods vendor within 5 tiles: reduces "retail desert" penalty by 30%

Crime effect:
  crime_modifier = -0.05 per informal vendor in zone
  // "Eyes on the street" effect — vendors are present during daytime hours
  // Only applies during vendor operating hours (6am-10pm)

Happiness effect:
  destitute_happiness += 3 per vendor within 5 tiles (capped at +9)
  working_class_happiness += 2 per vendor within 5 tiles (capped at +6)
  middle_class_happiness += 0 (neutral — neither positive nor negative)
  wealthy_happiness -= 1 per vendor within 5 tiles (capped at -3, "unsightly")

Land value effect:
  land_value_modifier = -2% per informal vendor on tile (minor negative)
  // Partially offset by the commercial activity they generate
```

## 2.4 Formalization Policy

Player can enact district-level policies affecting informal economy:

| Policy | Effect | Trade-off |
|--------|--------|-----------|
| **Ignore** (default) | Informal economy grows/shrinks organically | Zero tax revenue, maximum service to poor |
| **Register vendors** | 60% of vendors register; registered pay 10% tax on revenue | 20% of vendors leave (can't afford fees); remaining get legal protection |
| **Provide infrastructure** | Build hawker centers; vendors move in, pay subsidized rent | $1,000 per center; vendors formalize, hygiene improves, +5 happiness but lose street character |
| **Enforce removal** | Police clear informal vendors | All vendors removed; crime +10% (lost eyes on street), happiness -8 for destitute/working class, political backlash from poor voters |
| **License system** | Vendors buy $20/month license; limited spots per district | Controlled growth, some tax revenue ($2/vendor/day), queue system for vendor spots |

## 2.5 Cultural Modifiers for Informal Economy

The `informal_economy` dimension is derived from the Cultural DNA system (CULTURAL_DNA_EMERGENCE.md Section 3.1). It is NOT a standalone dimension but computed from existing dimensions:

```
informal_economy_score = (100 - institutional_trust) * 0.4 + risk_tolerance * 0.3 + (100 - work_formality) * 0.3
// work_formality is inverse of the entrepreneurial/hustle aspect of Work Ethic
```

**Regional presets** (override initial cultural DNA):

| Cultural Profile | Informal Economy Score | Notes |
|-----------------|----------------------|-------|
| Sub-Saharan African | 75 | 40% of commercial activity is informal at game start |
| Southeast Asian | 65 | Famous night markets, 30% informal |
| South Asian | 70 | Chai stalls, street food, repair shops everywhere, 35% informal |
| Latin American | 60 | Tianguis, mercados, 25% informal |
| Western European | 15 | Only farmers markets and food trucks, 5% informal |
| North American | 20 | Food trucks, flea markets, 5-8% informal |
| East Asian | 30 | Night markets exist but heavily regulated, 10% informal |

**Threshold for spawning**: informal_economy_score > 50 (see Section 2.1).

## 2.6 Visual Specification

**Street vendor sprite** (1 tile, sidewalk overlay):
- Colored tarp/umbrella: 8x8 pixel canopy sprite, randomly colored from palette (red, blue, green, yellow, orange, white)
- Goods display: 4x2 pixel strip in front of vendor (colored rectangles representing merchandise)
- Vendor sprite: standard citizen sprite behind the display, static or 2-frame idle animation
- Customer sprites: 2-4 citizen sprites positioned in front, facing vendor, with 0.5s dwell time before walking away

**Informal market cluster** (2x2, emerges from adjacent vendors):
- 5-10 tarp canopies arranged in a grid pattern
- Narrow walkways between stalls (1-pixel gaps)
- Higher customer density (8-15 citizen sprites browsing)
- Occasional delivery cart sprite at edge

**Mobile vendor** (walking):
- Push-cart sprite (3x5 pixels, wooden with goods on top)
- Follows pedestrian pathfinding routes
- Stops for 10-30 game-seconds when a customer sprite approaches
- Moves at 60% normal citizen walking speed

**Weather behavior**:
- Rain (light): vendors deploy larger tarp sprites, customer count -30%
- Rain (heavy): vendors pack up (despawn), respawn when rain stops
- Snow/blizzard: no informal vendors active
- Night: vendors with lantern sprite remain until 10pm; others despawn at sunset
- Heatwave: drink vendors +50% customer count; food vendors -20% (food spoilage risk)

---

# 3. WEATHER EFFECTS ON CITIZEN VISUALS

All visual behaviors layer on top of the base citizen simulation (AGENT_02). Weather state is set by the climate/season system (CULTURAL_DNA_EMERGENCE.md Section 2.1 climate types, Research 12 weather model).

## 3.1 Clear Weather

```
citizen_outdoor_density = base_density * 1.0
walking_speed = base_speed * 1.0
sprite_set = standard (per wealth class, per era)
outdoor_activities = all active (parks, plazas, markets, sports)
special_sprites = none
particle_effects = none
```

## 3.2 Light Rain

```
citizen_outdoor_density = base_density * 0.80
walking_speed = base_speed * 0.90
sprite_modifiers:
  30% of citizen sprites: umbrella accessory overlay
    - umbrella sprite: 4x6 pixel, colored (black, blue, red, transparent)
    - held above head, bobs with walk cycle
  70% of citizen sprites: normal (short trips, accept getting wet)
outdoor_activities:
  parks: children playing = disabled
  plazas: -30% citizen count
  covered_commercial: +20% citizen count (shift indoors)
  markets: open-air markets customer count -20%
road_surface:
  wet_road_shader: slight reflective sheen on road tiles (specular highlight at 20% opacity)
  no puddles yet
```

## 3.3 Heavy Rain

```
citizen_outdoor_density = base_density * 0.50
walking_speed = base_speed * 0.80
sprite_modifiers:
  60% of citizen sprites: umbrella accessory overlay
  20% of citizen sprites: running animation (1.5x speed, no umbrella, hunched posture)
  20% of citizen sprites: normal walk (short distance trips)
outdoor_activities:
  all outdoor activities disabled
  covered_commercial: +40% citizen count
road_surface:
  wet_road_shader: reflective sheen at 40% opacity
  puddle_sprites: 2x2 pixel dark circles on road tiles, 1-3 per tile (random placement)
  puddle_animation: occasional ripple (1-frame expanding ring, 0.5s interval)
rain_particles:
  diagonal streaks: 1-pixel white lines at 75° angle, density 20 per screen-chunk
  splash_on_ground: tiny 1-pixel white dot on horizontal surfaces, 0.1s lifetime
vehicle_effects:
  spray_particle: small mist trail behind moving vehicles (2-pixel white cloud, 0.3s fade)
  traffic_speed = base_speed * 0.85 (wet roads)
```

## 3.4 Snow

```
citizen_outdoor_density = base_density * 0.70
walking_speed = base_speed * 0.85
sprite_modifiers:
  ALL citizen sprites: winter clothing variant
    - coat overlay: thicker silhouette, muted colors (grey, brown, dark blue)
    - hat sprite: beanie/ushanka depending on cultural profile
    - scarf accessory: 1-pixel colored strip at neck level
  children_in_parks: snowball fight special animation
    - 2 child sprites facing each other, 3 tiles apart
    - small white dot projectile arcs between them (1-pixel, parabolic path, 0.3s travel)
    - hit: tiny white burst particle (2-frame)
  breath_vapor: at zoom levels 1-2 only
    - small white puff particle from citizen head position
    - 2x2 pixel, fades over 0.4s, drifts slightly upward-right
    - spawns every 1.5s per citizen
ground_surface:
  snow_accumulation_shader: white overlay on all ground tiles, 30% opacity (light snow) to 80% (heavy)
  road_tiles: plowed roads show dark center strip, snow banks at edges
  footprints: optional at zoom 1 only — tiny dark dots along citizen paths, fade after 5s
vehicle_effects:
  traffic_speed = base_speed * 0.75
  tire_tracks: dark lines on snowy road tiles behind vehicles (fade after 3s)
```

## 3.5 Blizzard

```
citizen_outdoor_density = base_density * 0.05
  // Only citizens entering/exiting buildings visible
  // No loitering, no outdoor activities, no markets
walking_speed = base_speed * 0.50
  // The few citizens outdoors move very slowly
sprite_modifiers:
  ALL outdoor citizens: heavy winter gear, hunched posture animation
  no children outdoors
  no elderly outdoors
vehicle_effects:
  traffic_density = base * 0.20
  only emergency vehicles + essential freight on roads
  traffic_speed = base_speed * 0.40
  visibility_shader: white-out effect, reduced draw distance
    - citizen sprites beyond 5 tiles: invisible
    - buildings beyond 15 tiles: silhouette only (60% white overlay)
blizzard_particles:
  dense diagonal white streaks, density 80 per screen-chunk
  wind_direction: consistent angle for duration of blizzard
  ground_drift: snow accumulation increases 1% per game-hour
```

## 3.6 Fog

```
citizen_outdoor_density = base_density * 1.0 (normal, fog doesn't deter walking)
walking_speed = base_speed * 1.0
sprite_modifiers: none
fog_shader:
  distance_fade: citizens beyond 10 tiles rendered at 30% opacity
  buildings beyond 20 tiles: 50% opacity with soft edge
  color_overlay: light grey (rgb(200, 200, 200)) at 15% blended over entire scene
  ground_fog_variant (morning only): fog bank at ground level, buildings poke above
vehicle_effects:
  traffic_speed = base_speed * 0.80
  headlights: vehicle sprites show small yellow glow dot at front (2x1 pixel, 80% opacity)
  fog_horn: port/harbor areas play fog horn ambient sound every 30s
```

## 3.7 Heatwave

```
citizen_outdoor_density = base_density * 0.70
  // But distribution changes: shade-seeking behavior
walking_speed = base_speed * 0.85
sprite_modifiers:
  citizens_near_trees: +200% density under tree canopy tiles
  citizens_near_fountains: +300% density within 2 tiles of fountain/water feature
  citizens_near_awnings: cluster under commercial building awnings
  elderly_citizens: NOT visible outdoors (heat danger, stay inside)
  children: reduced outdoor time, only in shaded parks
  ice_cream_truck_spawns: 1 per 10,000 population (mobile vendor, plays jingle sound)
    - ice_cream_truck sprite: small vehicle, pastel colored, music note particles
    - customer queue: 3-6 citizen sprites lined up at truck side
heat_shimmer_shader:
  on road tiles: subtle vertical wave distortion (1-pixel amplitude, 2s period)
  only visible at zoom level 3+ (far view)
hydration_particles:
  citizens near water features: occasional drinking animation (hand to mouth, 1s)
```

---

# 4. WEALTH CLASS TRANSPORT TRANSITIONS

These rules govern what happens to a citizen's transport mode when their wealth class changes. Hooks into VEHICLE_WEALTH_SYSTEM.md spawn tables and AGENT_02 household wealth transitions.

## 4.1 Downward Transition (Job Loss / Wealth Drop)

```
TRIGGER: Household.WealthClass decreases by 1+ levels

Month 0 (immediate):
  household.transport_stress = true
  // Flag triggers happiness penalty: -5 satisfaction

Month 1-3 (transition period):
  IF new_wealth_class <= WORKING_CLASS AND car_maintenance_cost > monthly_income * 0.25:
    sell_car = true
    // Car removed from household inventory
    // Car enters "used car pool" for the neighborhood
    car_sprite_migration:
      old_car_sprite added to poor_neighborhood_spawn_table with age += 5 years
      // A middle-class sedan becomes a "beater" in working-class area

  IF sell_car:
    transport_mode = evaluate_alternatives()
    // Re-run MNL model for this household
    // If transit_coverage > 0.6 in home zone: likely switches to transit
    // If transit_coverage < 0.3: walking, happiness drops -10 additional

Month 3+ (stabilized):
  household.transport_stress = false
  new mode is permanent until next wealth change

SPECIAL CASE — sudden unemployment (fired, factory closure):
  sell_car probability = 60% within month 1 (urgent need for cash)
  remaining 40%: try to keep car for 3 months, then 80% sell if still unemployed
```

## 4.2 Upward Transition (Promotion / Wealth Rise)

```
TRIGGER: Household.WealthClass increases by 1+ levels

Month 0 (immediate):
  household.upgrade_desire = true
  // No immediate transport change — savings needed first

Month 1-6 (saving period):
  car_purchase_probability_per_month = 0.15
  // Cumulative: ~60% chance of buying within 6 months
  // Modified by: transit_satisfaction (+good transit = lower purchase urgency)

  IF purchase_triggered:
    car_type = wealth_class_entry_vehicle[new_wealth_class]
    // NOT the luxury vehicle — first car in new class is entry-level:
    //   Working → Middle: used economy sedan (age 5-10 years)
    //   Middle → Upper-Middle: new mid-range sedan (age 0-2 years)
    //   Upper-Middle → Wealthy: new luxury sedan (age 0-1 year)

Month 6+ (stabilized):
  household.upgrade_desire = false
  subsequent_upgrades: every 5 years, upgrade to next vehicle tier within class
```

## 4.3 Generational Transport Habits

```
child_transport_habit_formation:
  IF household.primary_transport == CAR for child's age 0-16:
    adult_car_preference_bonus = +0.30 (added to V_car ASC in MNL model)
    // 30% more likely to choose car as adult, even if transit is good

  IF household.primary_transport == TRANSIT for child's age 0-16:
    adult_transit_preference_bonus = +0.25 (added to V_transit ASC)
    // More comfortable with transit, 25% more likely to use it

  IF household.primary_transport == BICYCLE for child's age 0-16:
    adult_cycle_preference_bonus = +0.35 (added to V_cycle ASC)
    // Cycling habit is strongly inherited

suburban_car_dependency:
  citizens raised in car-dependent suburbs (no transit within 10 tiles during age 0-18):
    car_ASC_bonus = +0.50 (very strong car preference)
    transit_ASC_penalty = -0.30 (uncomfortable with transit even when available)
    // This models the real-world pattern where suburban-raised adults resist transit
    // Fades over 10 years of living in transit-rich neighborhood: -0.05/year
```

## 4.4 Vehicle Aging Model

```
vehicle_age: increments by 1 per game-year
vehicle_condition = max(0, 100 - vehicle_age * condition_decay_rate)

condition_decay_rate by wealth class:
  Destitute:     8/year (poor maintenance, condition drops fast)
  Working class:  5/year (basic maintenance)
  Middle class:   3/year (regular maintenance)
  Upper-middle:   2/year (dealer maintenance)
  Wealthy:        1/year (garaged, detailed, pristine)

Vehicle replacement thresholds:
  Working class:  replace when condition < 20 (keeps car 15-20 years)
  Middle class:   replace when condition < 40 (keeps car 8-12 years)
  Upper-middle:   replace when condition < 60 (keeps car 5-8 years)
  Wealthy:        replace when condition < 80 (keeps car 3-5 years)

Old vehicle visual effects (applied to vehicle sprite):
  condition > 80:  normal sprite
  condition 50-80: slightly darker color tint (wear)
  condition 20-50: rust_overlay sprite (brown spots, 4-5 pixels scattered)
  condition < 20:  rust_overlay + exhaust_smoke_particle (grey puff from rear, 0.5s interval)

Old vehicle gameplay effects:
  condition < 50: accident_probability * 1.5
  condition < 30: pollution_output * 2.0
  condition < 20: breakdown_probability = 0.01 per trip (blocks lane for 5 game-minutes)
  speed_penalty: max_speed * (0.5 + 0.5 * condition / 100)
    // At condition 50: 75% max speed
    // At condition 20: 60% max speed
```

---

# 5. STREET PERFORMERS / BUSKERS

## 5.1 Spawn Conditions

```
Evaluated once per game-day for each eligible tile:

cultural_engagement > 40 in district
  // cultural_engagement = (institutional_trust * 0.3 + collectivism * 0.3 + arts_funding_per_capita * 0.4)
AND foot_traffic_on_tile > 30 pedestrians/hour
AND adjacent_to_commercial_or_cultural (within 3 tiles of commercial zone, museum, theater, park)
AND weather != SNOW, BLIZZARD, HEAVY_RAIN
AND time_of_day between 10:00-21:00

spawn_probability_per_eligible_tile = 0.05 per day
max_active_performers = min(5, city_population / 5000)
```

## 5.2 Performer Types

| Type | Sprite | Animation | Crowd Size | Duration |
|------|--------|-----------|------------|----------|
| **Musician** | Citizen with instrument overlay (guitar/violin/accordion, 3x5 pixel accessory) | 4-frame strumming/bowing loop, 0.5s per frame | 3-5 citizens in semicircle | 2-3 game-hours |
| **Artist** | Citizen with easel (3x4 pixel prop in front) | 2-frame painting gesture, 1s per frame | 1-2 citizens watching, 1 walking past slowly | 3-4 game-hours |
| **Statue performer** | Citizen in grey/silver tint (monochrome), standing still | Static pose; every 30s: 2-frame "surprise move" animation | 2-3 citizens staring, occasional child pointing | 2-3 game-hours |

## 5.3 Visual Details

```
performer_sprite: positioned on sidewalk tile, facing road
audience_sprites: 2-5 citizen sprites arranged in semicircle (120° arc), facing performer
  - audience members arrive one at a time over 30 game-seconds
  - each watches for 15-45 game-seconds, then walks away
  - new audience members replace departing ones (steady crowd)

coin_toss_particle:
  trigger: random, average once per 20 game-seconds
  sprite: 1x1 pixel, yellow (#FFD700)
  animation: parabolic arc from audience member to performer position
    - start: audience sprite hand position
    - apex: 3 pixels above start
    - end: ground level at performer position
    - duration: 0.4s
  on_landing: tiny sparkle (2-frame white flash, 1x1 pixel)

musician_sound (ambient):
  when zoomed to level 1-2 and within 5 tiles of performer:
  subtle instrument loop (5-10s, low volume, genre varies by cultural profile)
```

## 5.4 Gameplay Effects

```
happiness_bonus = +2 for all citizens within 5 tiles (while performer active)
commercial_revenue_bonus = +5% for commercial buildings within 3 tiles
  // Foot traffic lingers longer near performers → more impulse purchases
tourism_attractiveness = +2 per active performer (city-wide stat)

If district has > 3 active performers simultaneously:
  "Street Performance Scene" district trait unlocked
  → additional +3 tourism, +1 cultural rating
```

## 5.5 Performer Location Rotation

```
Performers do not stay in one spot permanently:
- After duration expires (2-4 hours), performer sprite walks away (normal citizen pathfinding)
- New performer may spawn at a DIFFERENT eligible tile next check
- Preferred tiles (higher spawn weight): plaza centers, park entrances, transit station frontages, pedestrian zone intersections
- Same tile cannot host a performer again for 1 game-day (cooldown)
```

---

# 6. COMMERCIAL BUILDING SUB-TYPES

Commercial zone buildings (AGENT_05_ZONING_BUILDINGS.md) should resolve to specific sub-types based on context. The commercial zone itself remains generic — the building that grows is determined by the sub-type selection algorithm.

## 6.1 Sub-Type Selection Algorithm

```
When a commercial zone tile grows a building:

1. Determine density level from zone density setting (Low / Medium / High)
2. Filter eligible sub-types by:
   - era >= current_era
   - density_level matches zone setting
3. Score each eligible sub-type:
   score = base_desirability
     + wealth_match_bonus    // +10 if neighborhood wealth matches sub-type target
     + population_need_bonus // +15 if served_population / capacity > 1.5 (underserved)
     + transit_bonus         // +10 for walkable types near transit, +10 for car-dependent near highways
     + cultural_bonus        // +5 for culturally appropriate types (food markets in high cultural pride)
4. Select sub-type with highest score (with ±10% random noise for variety)
```

## 6.2 Low Density Commercial Sub-Types

| Sub-Type | Era | Size | Serves | Workers | Parking Demand | Target Wealth | Special |
|----------|-----|------|--------|---------|---------------|---------------|---------|
| **General store** | Frontier-Postwar | 1x1 | 200 residents | 2 | 3 | All | +3 happiness within 3 tiles (community hub) |
| **Corner store** | Industrial+ | 1x1 | 300 residents | 2 | 2 | Working-Middle | Open long hours (6am-11pm) |
| **Convenience store** | Modern+ | 1x1 | 500 residents | 3 | 4 | All | Open 24h, +2 safety (lit at night) |
| **Neighborhood bakery** | All | 1x1 | 400 residents | 3 | 2 | All | +2 happiness within 3 tiles, morning foot traffic peak |
| **Cafe** | Industrial+ | 1x1 | 600 residents | 4 | 2 | Middle+ | +3 happiness, gathering spot for remote workers (Modern+) |
| **Specialty shop** | Postwar+ | 1x1 | 2,000 residents | 3 | 3 | Middle-Wealthy | Bookstore, boutique, antique shop; higher land value |
| **Hair salon / barber** | All | 1x1 | 1,000 residents | 3 | 2 | All | Appointment-based, steady foot traffic |
| **Pharmacy** | Industrial+ | 1x1 | 2,000 residents | 4 | 4 | All | Essential service, elderly dependency high |
| **Food truck pod** | Modern+ | 1x1 | Variable | 1 per truck | 0 | All | Informal-adjacent, appears in areas with foot traffic, zero parking |

## 6.3 Medium Density Commercial Sub-Types

| Sub-Type | Era | Size | Serves | Workers | Parking Demand | Target Wealth | Special |
|----------|-----|------|--------|---------|---------------|---------------|---------|
| **Supermarket** | Postwar+ | 2x2 | 2,000 residents | 15 | 30 | Working-Middle | Anchor store, generates daily trips |
| **Strip mall** | Postwar+ | 3x1 | 3,000 residents | 20 | 25 | Working-Middle | 5-8 small shops, car-dependent (requires adjacent parking) |
| **Department store** | Industrial+ | 2x2 | 5,000 residents | 30 | 20 | Middle-Wealthy | Multi-floor, fashion + home goods |
| **Hardware store** | Postwar+ | 2x1 | 5,000 residents | 8 | 15 | Working-Middle | Home improvement, generates truck traffic |
| **Auto dealer** | Postwar+ | 3x2 | 20,000 residents | 10 | 40 (display lot) | Middle-Wealthy | Large lot, car-dependent, road frontage |
| **Medical clinic** | Modern+ | 2x1 | 5,000 residents | 12 | 15 | All | Private healthcare, supplements public hospitals |
| **Restaurant row** | Industrial+ | 3x1 | 4,000 residents | 25 | 15 | Middle+ | 3-5 restaurants, evening peak, +5 happiness |
| **Farmer's market** | All | 2x2 | 3,000 residents | 10 (vendors) | 10 | All | Weekly event (see Section 9), +5 happiness, fresh food access |

## 6.4 High Density Commercial Sub-Types

| Sub-Type | Era | Size | Serves | Workers | Parking Demand | Target Wealth | Special |
|----------|-----|------|--------|---------|---------------|---------------|---------|
| **Shopping mall** | Modern+ | 4x4 | 20,000 residents | 200 | 400 | All | Anchor tenants, food court, entertainment; massive traffic generation |
| **Big box store** | Modern+ | 3x3 | 15,000 residents | 40 | 200 | Working-Middle | Cheap goods, car-dependent, -5 land value within 3 tiles |
| **Luxury retail center** | Modern+ | 3x3 | 50,000 residents | 80 | 100 | Wealthy | High-end brands, valet parking, +8 land value within 2 tiles |
| **Online fulfillment center** | Future | 3x3 | 30,000 residents | 50 | 30 (staff only) | N/A | No customer traffic; generates delivery van traffic (20 vans/day) |
| **Mixed-use commercial tower** | Modern+ | 2x2 | 10,000 residents | 150 | 80 | Middle-Wealthy | Ground-floor retail + upper office; transit-oriented |

## 6.5 Sub-Type Emergence Rules

```
Wealth-driven emergence:
  IF median_zone_wealth >= WEALTHY: specialty shops, luxury retail, cafes
  IF median_zone_wealth <= WORKING: corner stores, general stores, discount (big box)
  IF median_zone_wealth == MIDDLE: supermarkets, strip malls, department stores

Transit-driven emergence:
  IF transit_stop_within_5_tiles: walkable types preferred (cafes, bakeries, specialty)
  IF highway_ramp_within_10_tiles: car-dependent types preferred (strip mall, big box, auto dealer)
  IF pedestrian_zone: only food trucks, cafes, specialty shops (zero-parking types)

Era-driven transition:
  Frontier: general stores dominate, no alternatives
  Industrial: department stores appear in city center, general stores remain in outskirts
  Postwar: supermarkets + strip malls explode in suburbs, downtowns decline
  Modern: malls peak, then begin declining as online rises
  Future: fulfillment centers replace big box; mixed-use towers replace malls

Cultural-driven emergence:
  cultural_pride > 70: food markets, local specialty shops over chain stores
  institutional_trust < 40: informal vendors supplement formal commercial (Section 2)
  collectivism > 60: community-oriented shops (co-ops, farmer's markets)
```

---

# 7. ELDERLY DAILY ROUTINES

Retired citizens (age 65+, Employment == RETIRED in Citizen struct) follow distinct daily activity patterns that differ from working-age citizens.

## 7.1 Daily Schedule

```
TIME BLOCK: EARLY MORNING (6:00 - 9:00)
  activity_weights:
    park_walk:          0.30  // 30% probability, weather permitting
    cafe_visit:         0.25  // Bakery/cafe within 8 tiles
    home_idle:          0.35  // Stay home
    religious_building: 0.10  // If religious profile matches and building within 10 tiles
  weather_override:
    rain/snow/heatwave: park_walk = 0, redistribute to home_idle

TIME BLOCK: MIDDAY (9:00 - 14:00)
  activity_weights:
    community_center:   0.20  // If exists within 10 tiles
    medical_appointment:0.15  // Healthcare demand: elderly generate 3x visits vs working-age
    local_shopping:     0.25  // Prefer walkable commercial within 5 tiles; avoid malls (too far, too crowded)
    park_bench_sitting: 0.15  // If park with bench furniture within 5 tiles
    library_visit:      0.10  // If library within 8 tiles
    home_idle:          0.15

TIME BLOCK: AFTERNOON (14:00 - 17:00)
  activity_weights:
    grandchild_school_pickup: 0.15  // If school within 10 tiles AND household has grandchildren flag
    religious_building:       0.10  // Second visit for highly religious profiles
    library_visit:            0.10
    park_bench_sitting:       0.20
    home_idle:                0.45

TIME BLOCK: EVENING (17:00 - 21:00)
  activity_weights:
    restaurant_visit:   0.10  // Early dinner (17:00-18:30 only, not late dining)
    home_idle:          0.80  // Overwhelmingly stay home
    neighbor_visit:     0.10  // Visit adjacent residential building (social)

TIME BLOCK: NIGHT (21:00 - 6:00)
  activity_weights:
    home_idle:          1.00  // Always home, no nightlife for elderly
```

## 7.2 Elderly Movement Visual

```
walking_speed = base_citizen_speed * 0.60
stop_frequency: pauses every 8-12 tiles at benches, bus stops, or shade spots
  - stop_duration: 5-15 game-seconds (resting)
  - if no bench within 3 tiles of path: no stop (walks continuously but slower)

sprite_modifiers:
  posture: slightly hunched variant of wealth-class sprite
  walking_aid: 15% of elderly sprites carry cane accessory (1x3 pixel, held at side)
  companion: 20% of elderly walk in pairs (two elderly sprites side by side, matching speed)

clustering_behavior:
  community_centers: 5-10 elderly sprites gathered inside/around during midday
  park_benches: 2-3 elderly sprites seated on bench sprite, facing park
  cafe_terraces: 2-4 elderly sprites seated at outdoor table (if weather permits)
```

## 7.3 Elderly District Effects

```
IF district_elderly_percentage > 30%:

  Infrastructure demand:
    bench_demand: 1 bench per 50 elderly residents (benches are ploppable furniture, $20 each)
    community_center_demand: 1 per 2,000 elderly residents
    ground_floor_retail_bonus: +10% commercial demand for ground-floor shops (no stairs)
    elevator_demand: buildings without elevator access: -20% elderly residential desirability

  Healthcare:
    medical_facility_demand = district_elderly_count * 3.0 (3x baseline per-capita demand)
    pharmacy_demand = district_elderly_count * 2.0
    hospital_bed_demand: elderly occupy beds 5x longer than average

  Safety:
    daytime_crime_modifier = -0.10 (elderly as "eyes on street" during day hours)
    nighttime_crime_modifier = 0 (elderly indoors after 9pm)

  Commercial:
    evening_commercial_activity: -30% after 18:00 (early bedtime reduces evening economy)
    morning_commercial_activity: +20% before 10:00 (early risers)
    commercial_type_preference: pharmacies, bakeries, cafes, medical clinics over nightlife/bars

  Transport:
    transit_demand: elderly prefer transit over driving (lower accident risk, vision issues)
    transit_accessibility_requirement: low-floor buses, elevator-equipped stations
    IF transit_accessibility < 0.5: elderly satisfaction -10, emigration risk +15%
```

---

# 8. MODE CHOICE TO VEHICLE SPAWN RECONCILIATION

This section bridges Research 11 (MNL mode choice model) and VEHICLE_WEALTH_SYSTEM.md (sprite spawn tables). The MNL model produces abstract mode split percentages; the vehicle system needs concrete sprite counts on specific road edges.

## 8.1 Pipeline Overview

```
STEP 1: TRIP GENERATION (per zone, per tick)
  trips_zone_i = population_zone_i * trip_rate_per_capita
  // trip_rate: 2.5 trips/person/day (NHTS average)
  // 20,000 households × 3.5 avg size × 2.5 = 175,000 daily trips city-wide

STEP 2: TRIP DISTRIBUTION (gravity model, AGENT_04 Phase 2)
  OD_matrix[origin_zone][dest_zone] = trips * attraction / impedance
  // Already specified in AGENT_04; produces O-D pairs

STEP 3: MODE SPLIT (MNL from Research 11)
  For each O-D pair, run MNL softmax:
    P(car)    = exp(V_car)    / sum(exp(V_all))
    P(transit)= exp(V_transit)/ sum(exp(V_all))
    P(walk)   = exp(V_walk)   / sum(exp(V_all))
    P(cycle)  = exp(V_cycle)  / sum(exp(V_all))

  Aggregate to zone level:
    car_trips_zone_i    = sum(OD_matrix[i][j] * P(car)    for all j)
    transit_trips_zone_i= sum(OD_matrix[i][j] * P(transit) for all j)
    walk_trips_zone_i   = sum(OD_matrix[i][j] * P(walk)    for all j)
    cycle_trips_zone_i  = sum(OD_matrix[i][j] * P(cycle)   for all j)

STEP 4: NETWORK ASSIGNMENT (car trips → road edges)
  For each car O-D pair:
    route = shortest_time_path(origin, dest)  // Dijkstra with BPR-weighted edges
    for each edge in route:
      edge.flow_volume += 1

  // After all assignments:
  // edge.flow_volume = total vehicles/hour on this road segment

STEP 5: VEHICLE SPRITE SPAWNING (VEHICLE_WEALTH_SYSTEM.md integration)
  For each road edge with flow_volume > 0:

    // Determine neighborhood wealth context
    adjacent_zone_wealth = average(wealth_class of zones touching this edge)

    // Look up spawn weight table from VEHICLE_WEALTH_SYSTEM.md
    // (Section 2: Master Vehicle Tables, per era per wealth class)
    spawn_table = get_vehicle_spawn_weights(current_era, adjacent_zone_wealth)

    // Calculate how many vehicle sprites to show
    visible_vehicles = edge.flow_volume / vehicle_capacity_per_sprite
    // vehicle_capacity_per_sprite: 1 car = 1 sprite; 1 bus = 40 passengers
    // This prevents 10,000 sprites on a highway — abstract representation

    // Cap visible vehicles per edge for performance
    max_sprites_per_edge = road_tier_sprite_cap[edge.road_tier]
    //   Tier 1 (dirt):     4 sprites max
    //   Tier 3 (paved):    8 sprites max
    //   Tier 4 (avenue):   12 sprites max
    //   Tier 5 (highway):  20 sprites max
    //   Tier 6 (express):  30 sprites max

    visible_vehicles = min(visible_vehicles, max_sprites_per_edge)

    // Sample vehicle types from spawn table
    for i in range(visible_vehicles):
      vehicle_type = weighted_random_sample(spawn_table)
      spawn_vehicle_sprite(edge, vehicle_type)
```

## 8.2 Key Principle

**MNL decides HOW MANY cars** appear on the road network (total flow volume).
**VEHICLE_WEALTH_SYSTEM decides WHICH cars** appear on each road segment (sprite selection).

These two systems operate at different granularities:
- MNL: zone-to-zone aggregate (500 zones, updated every game-day)
- Vehicle spawn: per-road-edge visual (thousands of edges, sprites refresh every few game-seconds)

## 8.3 Transit Vehicle Spawning

```
Transit vehicles are NOT spawned from the MNL output directly. They follow fixed routes:

bus_frequency = base_frequency * (transit_ridership / bus_capacity)
  // If ridership doubles, frequency increases (more buses on route)
  // Visualized as more bus sprites on the route's road edges

tram/subway: fixed schedule per transit line, always visible on their tracks
  // Frequency adjustable by player

transit_edge.flow_volume = transit_trips_on_this_segment / transit_vehicle_capacity
  // Adds transit vehicles to road edge sprite pool alongside cars
```

## 8.4 Pedestrian and Cyclist Spawning

```
Pedestrian sprites on sidewalks:
  sidewalk_density = walk_trips_zone / sidewalk_tile_count
  sprites_per_tile = clamp(sidewalk_density / 50, 0, 5)
  // Max 5 pedestrian sprites per sidewalk tile

Cyclist sprites on roads/bike lanes:
  cycle_density = cycle_trips_zone / bike_route_length
  sprites_per_tile = clamp(cycle_density / 30, 0, 3)
  // Max 3 cyclist sprites per road tile with bike infrastructure
  // Cyclists use bike lane sprite position (edge of road) if bike lane exists
  // Otherwise share road with vehicles (slightly right of center lane)
```

---

# 9. MARKET VISUAL ANIMATION

## 9.1 Farmer's Market (Weekly Event)

**Activation**: Farmer's market building (Section 6.3) triggers a weekly event. Active 1 game-day per game-week, typically Saturday morning (8am-2pm game-time).

```
Layout (on 2x2 market building tiles):
  stall_count: 6-8 stalls arranged in 2 rows of 3-4
  stall_sprite: 3x4 pixel colored awning (palette: red, green, white, blue, striped)
    - goods_display: 2x2 pixel colored blocks below awning (greens, oranges, browns for produce)
    - vendor_sprite: 1 citizen sprite behind counter, facing outward
    - vendor_animation: 2-frame gesture loop (arm wave / point at goods), 1.5s per frame

Customer behavior:
  customer_count: 10-15 citizen sprites active simultaneously within market tiles
  customer_pathfinding: slow random walk between stalls (40% normal speed)
  customer_at_stall:
    - walks to stall front, stops for 3-8 game-seconds (browsing)
    - purchase_probability: 40% per stall visit
    - purchase_animation:
      1. citizen hand reaches forward (1 frame, 0.3s)
      2. coin_particle: 1x1 yellow dot arcs from citizen to vendor (0.3s, parabolic)
      3. citizen walks away with bag_accessory: 2x2 pixel brown/white bag held at side
      4. bag_accessory persists until citizen exits market area

Supplier area (edge of market):
  1-2 small truck/cart sprites parked at market perimeter
  crate_sprites: 3x2 pixel wooden crate stacks next to trucks
  occasionally: vendor walks to truck, picks up crate (carry animation), returns to stall
```

## 9.2 Street Food Vendor

```
Sprite composition (1 tile):
  base: umbrella/tarp canopy, 6x6 pixel, solid color from vendor palette
  counter: 4x2 pixel surface below canopy
  vendor: citizen sprite behind counter
  cooking_equipment: 2x2 pixel dark square (grill/pot)

Particle effects:
  cooking_smoke: small grey-white puff particles
    - spawn_point: cooking_equipment position
    - size: 1x1 pixel, expanding to 2x2 over lifetime
    - color: rgb(180,180,180) fading to transparent
    - velocity: upward + slight wind drift
    - spawn_rate: 1 particle every 0.8s
    - lifetime: 1.5s
  food_steam: similar to smoke but lighter color rgb(220,220,230)
    - only when customer is being served (food on counter)

Customer queue:
  queue_length: 2-3 citizen sprites in single-file line facing vendor
  queue_behavior:
    - front citizen: served for 3-5 game-seconds, then walks away with food item
    - remaining citizens: shuffle forward 1 position
    - new citizen joins rear of queue every 5-10 game-seconds (if foot traffic sufficient)
  food_item_accessory: 1x2 pixel item held at hand height after purchase
    - color varies: brown (wrapped food), white (cup), red (skewer)
    - persists for 30 game-seconds after purchase, then despawns (eaten)
```

## 9.3 Night Market (Evening Event)

**Activation**: Requires cultural_profile.informal_economy > 50 OR player builds night market building (district policy). Active 6pm-11pm on designated nights (1-3 per game-week based on cultural profile).

```
Visual differences from day market:
  lighting:
    string_lights: row of 1-pixel warm yellow dots (#FFD580) connecting between stall awnings
      - glow_radius: 2-pixel soft circle around each light point
      - twinkle_animation: random lights dim to 60% and back, 0.5s cycle, staggered
    lantern_sprites: 2x3 pixel red/orange paper lanterns hanging from awning edges
      - 1-2 per stall
      - gentle sway animation: ±1 pixel horizontal, 2s period
    ground_glow: warm orange tint on ground tiles within market area (additive blend, 15% opacity)

  vendor_density: 2x day market (stalls packed tighter, temporary stalls fill gaps)
  customer_density: peak at 8-9pm game-time, 2x day market customer count

  customer_behavior:
    slower_browsing: 30% normal walk speed (leisurely)
    eating_while_walking: 40% of customers carry food_item_accessory
    group_behavior: 30% of customers walk in pairs or groups of 3 (linked movement, same destination)

  ambient_sound (when zoomed to level 1-2):
    crowd_murmur: low background chatter loop
    sizzle_sound: if food vendors present
    music: if musician performer spawned within market area

  music_note_particles (near musician vendors):
    spawn_point: musician sprite head position
    sprite: 2x2 pixel music note (eighth note shape), alternating white and yellow
    velocity: float upward + slight random horizontal drift
    spawn_rate: 1 every 2s
    lifetime: 2s, fade to transparent over last 0.5s
```

---

# CROSS-REFERENCE INDEX

| System | Primary Document | This Document Section | Integration Points |
|--------|-----------------|----------------------|-------------------|
| Parking supply/demand | NEW | Section 1 | AGENT_04 (BPR congestion), AGENT_05 (building placement), AGENT_03 (commercial revenue) |
| Parking policy | NEW | Section 1.5 | POLITICAL_LAW_SYSTEM.md (district policies), AGENT_07 (voter satisfaction) |
| Informal economy | NEW | Section 2 | CULTURAL_DNA_EMERGENCE.md (cultural dimensions), AGENT_03 (commercial simulation), MISSING_SYSTEMS.md (crime) |
| Weather citizen visuals | NEW | Section 3 | Research 12 (weather model), AGENT_09 (sprite pipeline), VISUAL_QUALITY_GUIDE.md |
| Wealth transport transitions | NEW | Section 4 | VEHICLE_WEALTH_SYSTEM.md (spawn tables), AGENT_02 (household lifecycle), Research 11 (MNL) |
| Street performers | NEW | Section 5 | CULTURAL_DNA_EMERGENCE.md (cultural engagement), AGENT_03 (commercial revenue), AGENT_10 (game feel) |
| Commercial sub-types | NEW | Section 6 | AGENT_05 (zone building growth), EXPANDED_ZONES (zone types), AGENT_03 (economic demand) |
| Elderly routines | NEW | Section 7 | AGENT_02 (citizen lifecycle, retirement), MISSING_SYSTEMS.md (healthcare), AGENT_06 (services) |
| Mode-to-vehicle reconciliation | NEW | Section 8 | Research 11 (MNL model), VEHICLE_WEALTH_SYSTEM.md (spawn tables), AGENT_04 (traffic assignment) |
| Market animations | NEW | Section 9 | EXPANDED_ZONES (farmer's market), Section 2 (informal economy), AGENT_09 (art pipeline) |

---

**This document closes all remaining design gaps identified in the final audit. Every system now has complete numeric specifications, formulas, visual descriptions, and cross-references to existing documents. Zero placeholders remain.**
