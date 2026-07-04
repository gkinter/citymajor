# AGENT 09: ART & SHADER PIPELINE

## Role
Generate all visual assets: pixel art sprites (930+), tile sets, building sprites, vehicle sprites, citizen sprites, UI textures, shaders (day/night, seasons, aging, weather), particle effects, and animations.

**This agent is the HEAVIEST user of AI art tools and Kimi image-to-code.**

---

## Prerequisites
- Agent 01: TileMap layer specs, sprite size standards
- Agent 05: Building database (what sprites are needed)

---

## Art Direction Standards

### Core Rules
- **Tile base**: 32x16px isometric diamond
- **Building heights**: 1 tile = 16px vertical. A 2-story building = 32px tall + 16px base = 48px total
- **Color palette**: Limited per era (16-24 colors per era palette)
- **Style**: Clean pixel art, NO anti-aliasing on edges, Stardew Valley tier quality
- **Outline**: 1px dark outline on all sprites
- **Shadow**: Consistent light from top-right (isometric standard)

### Era Color Palettes
```
Frontier (1850):  Earthy browns, warm wood tones, dusty greens, iron grey
Industrial (1890): Brick red, soot black, steel grey, smoky amber, gas-lamp yellow
Postwar (1930):   Concrete grey, chrome silver, pastel suburban, neon accents
Modern (1970):    Glass blue, white concrete, corporate grey, green parks
Future (2010+):   Clean white, electric blue, solar gold, holographic accents
```

### Seasonal Palettes (applied as shader tint)
```
Spring: Fresh greens (#4CAF50), cherry pink (#E91E63), rain grey (#78909C)
Summer: Vivid green (#2E7D32), golden sun (#FFB300), deep blue (#1565C0)
Autumn: Amber (#FF8F00), burnt orange (#E65100), russet (#5D4037)
Winter: Snow white (#ECEFF1), cool blue (#42A5F5), warm yellow window glow (#FFF176)
```

---

## Phase 1: Terrain & Road Tiles (Day 3-5)

### Tasks
1. **Terrain Tiles** (20 base x 4 seasons = 80 sprites):
```
Grass (flat, 3 variants)
Water (still, river flow, ocean)
Forest (deciduous, conifer, tropical)
Hills (elevation 1, 2, 3)
Rock/cliff
Sand/beach
Fertile soil (farmland)
Snow (winter variant of all above)
Desert
Swamp
```

2. **Road Tiles** (60 sprites):
   - 8 road types x 7-8 connection patterns (straight, curve, T, cross, end)
   - Dirt, cobblestone, paved, avenue, highway
   - Bridge variants over water
   - Tunnel entrance sprites

3. **Rail Tiles** (30 sprites):
   - Single track, double track, high-speed
   - Curves, switches, crossings
   - Platform edges, tunnel portals

### AI Tool Usage
- **Art AI (Midjourney/DALL-E)**: Generate base terrain tile concepts at higher resolution
- **Claude**: Write pixel art downscaling script (high-res concept -> 32x16 pixel art)
- **Kimi**: Screenshot of existing isometric terrain (OpenTTD, Stardew) -> generate matching Godot tileset .tres
- **Manual cleanup**: Touch up AI output in Aseprite/Pixelorama for consistency

### Output Files
- `assets/sprites/terrain/` (80 .png files)
- `assets/sprites/roads/` (60 .png files)
- `assets/sprites/rail/` (30 .png files)
- `scenes/main/TerrainTileSet.tres`
- `scenes/main/RoadTileSet.tres`

### Quality Check
- [ ] All tiles seamlessly connect (no visible seams)
- [ ] Seasonal variants are palette-consistent
- [ ] Water has 3-frame animation
- [ ] Road connections auto-detect correctly in TileMap

---

## Phase 2: Building Sprites (Day 5-10)

### Tasks
1. **Residential Buildings** (60 sprites):
```
Frontier: Cabin, farmhouse, wooden house, boarding house (x3 wealth)
Industrial: Row house, brownstone, tenement, Victorian (x3 wealth)
Postwar: Suburban home, apartment, public housing, duplex (x3 wealth)
Modern: Townhouse, condo, high-rise apartment, luxury condo (x3 wealth)
Future: Smart home, eco-pod, vertical garden apartment, luxury tower (x3 wealth)
```

2. **Commercial Buildings** (40 sprites):
```
Frontier: General store, saloon, market stall
Industrial: Department store, bank, restaurant, theater
Postwar: Strip mall, diner, gas station, supermarket
Modern: Shopping center, office tower, restaurant chain, boutique
Future: Drone delivery hub, VR experience center, automated store
```

3. **Industrial Buildings** (30 sprites):
```
Extraction: Mine, quarry, oil well, lumber camp, fishing dock, farm
Processing: Sawmill, smelter, refinery, mill, textile mill
Manufacturing: Factory, auto plant, electronics factory, food processing
Advanced: Automated factory, tech fab, biotech lab, 3D print hub
```

4. **Service Buildings** (40 sprites):
```
Fire: 5 tiers from volunteer shack to smart station
Police: 4 tiers from patrol post to smart precinct
Health: 5 tiers from frontier clinic to research hospital
Education: 6 types (daycare through university)
Other: Post office, library, courthouse, prison, cemetery, church
```

5. **Infrastructure Buildings** (30 sprites):
```
Power: Coal plant, oil plant, gas plant, nuclear, solar farm, wind farm, fusion
Water: Pump station, treatment plant, water tower
Waste: Landfill, recycling plant, incinerator
Telecom: Telegraph office, telephone exchange, cell tower, data center
Transit: Bus depot, train station, metro station, airport, seaport
```

6. **Special Buildings** (25 sprites):
```
Landmarks: Monument, statue, fountain, clock tower, observation deck
Recreation: Stadium, arena, pool, park pavilion, zoo entrance
Culture: Museum, art gallery, concert hall, theater, temple/church
```

### AI Tool Usage (HEAVY)
- **Art AI (Midjourney)**: Generate each building at 4x resolution with prompt:
  `"isometric pixel art [building name], [era] style, 32x16 tile base, clean pixel art,
   limited palette, dark outline, Stardew Valley quality, white background"`
- **Claude**: Write batch processing script to:
  1. Downscale AI output to correct pixel dimensions
  2. Apply era-specific color palette
  3. Add consistent outline and shadow
  4. Export as .png with transparent background
- **Kimi**: Screenshot of reference buildings from SimCity 2000/4 -> generate matching isometric sprites
- **Kimi**: Take each AI-generated building concept -> generate Godot SpriteFrames resource
- **Manual**: Final cleanup pass in Aseprite for palette consistency

### Batch Workflow (per building):
```
1. Art AI generates 4x concept (5 seconds)
2. Claude script downscales + palette-maps (automated)
3. Human reviews + accepts or rejects (10 seconds)
4. If accepted: auto-export to sprites folder
5. If rejected: regenerate with adjusted prompt
```

**At 30 seconds per building, 260 buildings = ~2 hours of generation time**

### Output Files
- `assets/sprites/buildings/residential/` (60 .png)
- `assets/sprites/buildings/commercial/` (40 .png)
- `assets/sprites/buildings/industrial/` (30 .png)
- `assets/sprites/buildings/service/` (40 .png)
- `assets/sprites/buildings/infrastructure/` (30 .png)
- `assets/sprites/buildings/special/` (25 .png)

---

## Phase 3: Vehicle & Citizen Sprites (Day 10-12)

### Tasks
1. **Vehicles** (~120 sprites, from VEHICLE_WEALTH_SYSTEM.md):
```
Per era, per wealth class:
Frontier: Horse cart, horse carriage, bicycle
Industrial: Model T, delivery wagon, coal truck, streetcar
Postwar: Family sedan, pickup truck, delivery van, bus, diesel train
Modern: Sedan, SUV, sports car, semi truck, electric train, ambulance
Future: Electric car, autonomous pod, drone delivery, maglev

4 directions per vehicle (N, S, E, W isometric)
= ~120 unique vehicle sprites x 4 directions = ~480 sprite frames
```

2. **Citizens** (48 sprite frames):
```
3 wealth classes x 4 directions x 4 walk cycle frames = 48
+ 8 special states: waiting, sitting, working, protesting, celebrating
```

3. **Construction Workers** (12 sprite frames):
   - 4 directions x 3 animation frames (hammering, carrying, idle)

### AI Tool Usage
- **Art AI**: Generate vehicle concepts at 4x, one prompt per era
- **Claude**: Write sprite sheet packer (individual sprites -> atlas)
- **Claude**: Generate walk cycle animation data (frame timing, pivot points)
- **Kimi**: Screenshot of OpenTTD vehicle sprites -> generate matching style for custom vehicles
- **Kimi**: Screenshot of Stardew Valley character sprites -> match art quality for citizens

### Output Files
- `assets/sprites/vehicles/` (organized by era)
- `assets/sprites/citizens/` (walk cycles)
- `assets/sprites/construction/`

---

## Phase 4: Shaders (Day 12-14)

### Tasks
1. **Day/Night Cycle Shader**:
```glsl
// Applied to entire viewport
shader_type canvas_item;
uniform float time_of_day : hint_range(0.0, 24.0);
uniform sampler2D light_gradient;  // dawn->day->dusk->night color strip

void fragment() {
    vec4 base = texture(TEXTURE, UV);
    vec4 tint = texture(light_gradient, vec2(time_of_day / 24.0, 0.5));
    COLOR = base * tint;
    // Night: add yellow glow to window pixels
    if (time_of_day > 18.0 || time_of_day < 6.0) {
        if (base.r > 0.8 && base.g > 0.7) {  // detect window color
            COLOR = vec4(1.0, 0.95, 0.5, 1.0);  // warm glow
        }
    }
}
```

2. **Seasonal Palette Shader**:
   - Palette swap: remap green -> autumn orange, winter white
   - Applied per-tile based on current season
   - Smooth transition between seasons (1 game week blend)

3. **Building Aging Shader**:
```glsl
// Applies wear based on building condition (0-100)
uniform float condition : hint_range(0.0, 100.0);
// At condition < 50: add crack overlay
// At condition < 30: add broken window pixels
// At condition < 10: heavy degradation, partial collapse look
```

4. **Weather Shaders**:
   - Rain: falling droplet particles + wet surface darkening
   - Snow: falling snowflakes + accumulation on roofs
   - Fog: semi-transparent overlay reducing visibility
   - Heat shimmer: wavy distortion above roads in summer

5. **Water Shader**:
   - Animated flow (UV scrolling)
   - Reflection of nearby buildings (simplified)
   - Pollution coloring (clean blue -> polluted brown/green)

6. **Pollution Shader**:
   - Smog haze near industrial areas
   - Ground contamination discoloration
   - Intensity scales with pollution level

### AI Tool Usage
- **Claude**: Generate all GLSL shaders for Godot
- **Kimi**: Screenshot of pixel art game with good day/night cycle -> analyze shader technique -> generate equivalent
- **Kimi**: Screenshot of rain/snow effects in pixel games -> generate particle system
- **Research agent**: Study Godot shader tutorials for best practices

### Output Files
- `assets/shaders/day_night.gdshader`
- `assets/shaders/seasonal_palette.gdshader`
- `assets/shaders/building_aging.gdshader`
- `assets/shaders/rain.gdshader`
- `assets/shaders/snow.gdshader`
- `assets/shaders/water.gdshader`
- `assets/shaders/pollution.gdshader`
- `assets/shaders/heat_shimmer.gdshader`

---

## Phase 5: Effects & Animations (Day 14-15)

### Tasks
1. **Particle Effects** (Godot GPUParticles2D):
   - Smoke (factory chimneys, fires)
   - Steam (train, power plant)
   - Sparks (construction, factory)
   - Dust (demolition, dirt roads)
   - Leaves (autumn, parks)
   - Confetti (celebrations, milestones)

2. **Building Animations**:
   - Construction scaffold: 4-phase sequence
   - Factory chimney: continuous smoke emission
   - Windmill: rotating blades
   - Solar panel: sun-tracking rotation
   - Neon signs: flickering at night (Modern+)

3. **UI Animations**:
   - Panel slide in/out
   - Button hover highlight
   - Progress bar fill
   - Number tick-up (satisfying counter animation)
   - Notification pop-in with bounce

### AI Tool Usage
- **Claude**: Generate all Godot particle system configurations (.tres)
- **Claude**: Generate AnimationPlayer keyframe data for building animations
- **Kimi**: Screenshot of pixel art smoke/steam effects -> generate particle parameters
- **Art AI**: Generate effect sprite sheets (smoke puffs, spark flashes)

### Output Files
- `scenes/effects/SmokeEffect.tscn`
- `scenes/effects/SteamEffect.tscn`
- `scenes/effects/ConstructionEffect.tscn`
- `scenes/effects/WeatherRain.tscn`
- `scenes/effects/WeatherSnow.tscn`

---

## Phase 6: Audio Asset Generation (Day 15-16)

### Tasks
1. **Music** (10 tracks, via Suno/Udio):
   - Frontier era: Acoustic guitar, harmonica, banjo
   - Industrial era: Piano, strings, brass
   - Postwar era: Jazz ensemble, big band
   - Modern era: Electronic ambient, synth
   - Future era: Ethereal synth, AI-generated harmonics
   - 2 variants per era (day + night mood)

2. **Sound Effects** (79 SFX, via ElevenLabs):
   - Construction: hammer, saw, crane, demolition rumble
   - Traffic: car engine, horn, horse hooves, train whistle
   - Transit: bus doors, train arrival jingle, ferry horn
   - Industry: factory hum, smelter roar, sawmill buzz
   - UI: click, confirm, cancel, cash register, paper shuffle
   - Weather: rain, thunder, wind, snow crunch
   - Events: fire alarm, celebration cheers, protest chants
   - Political: applause, boo, bell toll

### AI Tool Usage
- **Suno/Udio**: Generate all 10 music tracks with era-specific prompts
- **ElevenLabs SFX tool**: Generate all 79 sound effects
- **Claude**: Generate Godot AudioStreamPlayer configurations and bus routing
- **Manual**: Listen, accept/reject, adjust prompts

### Output Files
- `assets/audio/music/` (10 .ogg files)
- `assets/audio/sfx/` (79 .ogg files)
- `assets/audio/ambient/` (4 seasonal loops)
- `scripts/game/AudioManager.gd`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | TileMap specs, sprite size standards | Phase 1 |
| Agent 05 | Complete building database (what to draw) | Phase 2 |
| Agent 04 | Vehicle list with era/wealth mapping | Phase 3 |
| Agent 10 | Art style references from successful games | Phase 1 |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| ALL agents | Placeholder -> final art swap | .png, .tres |
| Agent 01 | TileSet resources | .tres files |
| Agent 04 | Vehicle sprites | .png + SpriteFrames |
| Agent 05 | Building sprites | .png + metadata |
| Agent 08 | UI textures, fonts, icons | .png + .tres |

---

## Estimated Duration
- **With AI**: 10-12 days (HEAVIEST asset production)
- **Without AI**: 3-4 months (art is the bottleneck without AI)
- **Can start**: After Agent 01 Phase 1 (Day 1) for terrain
- **Note**: This agent runs longest but doesn't block others (placeholders work)
