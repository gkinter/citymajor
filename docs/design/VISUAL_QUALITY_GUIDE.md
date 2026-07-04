# IRON & OAK: Visual Quality Guide

> **Superseded for Web v1 (2026-07).** CityMajor pivoted to **mesh 3D mid-fidelity** (R3F + Three.js, CS-lite). This document remains the historical reference for the **pixel-art / Forge Engine** plan. For current targets see:
>
> - [`MASTER_GAME_CONCEPT.md`](MASTER_GAME_CONCEPT.md) §8 — mesh LOD zoom levels, era PBR materials
> - [`AI_ART_PIPELINE.md`](AI_ART_PIPELINE.md) — **3D Module Pipeline (Web v1)** (modular GLTF kitbash)
> - Linear [SB-3704](https://linear.app/softblaze/issue/SB-3704) — instancing, chunk culling, quality tiers

**Web v1 rendering targets:** `InstancedMesh` per building archetype per chunk; 32×32 spatial grid for frustum culling; LOD hysteresis at four zoom bands (full GLTF → simplified mesh → instanced boxes → colored blocks); dynamic `devicePixelRatio` downscale when FPS &lt; 30; shadow tiers (off / key buildings / full); citizens as instanced dots beyond street zoom.

---

## How Good Can This Game Actually Look?

**Short answer: Premium pixel art quality on par with Songs of Conquest / Eastward, with fluid traffic that feels as satisfying as Mini Motorways (which proved colored dots moving smoothly is MORE satisfying than detailed 3D cars).**

---

## 1. THE MINI MOTORWAYS REVELATION

The most important finding from this research:

> **You don't need 3D cars with turn signals to make traffic satisfying.**
> Mini Motorways uses colored rectangles on flat lines and is one of the most visually satisfying traffic games ever made. The secret is: **smooth movement + audio feedback + density**.

Cities: Skylines traffic looks good because of volume and flow, not because individual cars are detailed. At typical play zoom, CS cars are 5-10 pixels tall. What you actually see is *patterns of movement*.

### What Makes Traffic Visually Satisfying
1. **Smooth sub-pixel movement** (no jittery grid-snapping)
2. **Correct speed ratios** (vertical = 50% of horizontal in 2:1 isometric)
3. **Density variation** (rush hour flood vs. quiet night = city feels alive)
4. **Audio feedback** (smooth flow sounds vs. honking when congested)
5. **Color coding by vehicle type** (buses = one color, trucks = another, cars = varied)
6. **Congestion visible instantly** (vehicles bunch up, speed drops, color shifts to red on overlay)

### Achievable Traffic for Iron & Oak

| Metric | Target | How |
|--------|--------|-----|
| Vehicles on screen | 80-150 simultaneously | RenderingServer pooled sprites (500 pool) |
| Vehicle size | 10-16px long | Appropriate for 32x16 tile scale |
| Directions | 4 (NE, NW, SE, SW) | 4 sprite variants per vehicle type |
| Animation | 2-frame (wheel rotation) | Manual texture region swap |
| Vehicle types visible | 6-8 (car, truck, bus, van, bike, emergency, train, tram) | Sprite sheet |
| Movement smoothness | Sub-pixel rendering, 60fps interpolation | Godot built-in |
| Night detail | Headlight glow (1px yellow), brake light (1px red) | Palette swap shader |
| Rush hour effect | 3x vehicle density 7-9am, 5-7pm | Flow model drives spawn rate |
| Congestion visual | Vehicles slow down, bunch up, stop at red lights | Speed from BPR model |

**This will look BETTER than Theotown's traffic and competitive with Cities: Skylines at typical zoom levels.**

---

## 2. VISUAL QUALITY TIERS: CHEAP vs PREMIUM

### What Makes Pixel Art Look "Cheap"

From analyzing Theotown, generic itch.io city builders, and amateur pixel art:

- Oversaturated colors (pure red #FF0000, pure blue #0000FF)
- No consistent light direction (shadows face random directions)
- Flat shading (no gradients or value ramps)
- Visible tile repetition (every grass tile identical)
- No ambient animation (static buildings, no smoke, no movement)
- Grid lines visible (tiles don't blend at edges)
- No day/night variation (always bright daytime)
- No weather (always clear sky)
- Vehicles slide across roads like cardboard cutouts

### What Makes Pixel Art Look "Premium"

From analyzing Songs of Conquest, Eastward, Stardew Valley, Hyper Light Drifter:

- **Color economy**: 48-64 colors with smooth value ramps (not randomly picked)
- **Consistent lighting**: Top-left light source across ALL assets
- **Value hierarchy**: Dark outlines, medium fills, light highlights -- readable at any zoom
- **Tile variation**: 3-4 variants per terrain type, randomly placed
- **Ambient animation layers**: Smoke + citizens + vehicles + weather = life
- **Day/night palette shift**: Single shader that transforms the entire mood
- **Warm window glow at night**: The single most visually appealing detail in any city builder
- **Water with reflection/distortion**: Simple flip + tint technique, looks magical
- **Post-processing**: Subtle bloom on light sources, vignette at edges

### The Key Insight

> The difference between cheap and premium pixel art is **not** about pixel count or sprite detail. It's about **lighting consistency, palette discipline, and ambient animation layers.** A 32x16 tile with 48 colors and proper lighting looks better than a 64x32 tile with 256 colors and no lighting direction.

---

## 3. ACHIEVABLE VISUAL FEATURES (at 60fps in Godot 4)

### Tier 1: Must-Have (Biggest Visual Impact)

#### 1a. Global Day/Night Palette Swap
```
Impact: ★★★★★ (transforms the entire game)
Effort: 1 shader + palette art
Performance: Nearly free (single ColorRect covering screen)
```

How it works:
- 1 palette texture with 4 rows: Dawn, Day, Dusk, Night
- Shader remaps every pixel's color to the time-of-day row
- Night row: darker values + warm yellow for window pixels
- Transition: lerp between rows over 1 game-hour

Visual result: The same city looks completely different at 4 times of day. Night mode with warm window glow is an immediate "wow" moment. Screenshots look 4x more interesting.

Reference: [KoBeWi's Palette Swap Shader for Godot](https://github.com/KoBeWi/Godot-Palette-Swap-Shader)

#### 1b. Smooth Vehicle Flow
```
Impact: ★★★★★ (makes city feel alive)
Effort: Already planned (RenderingServer pool)
Performance: 150 sprites via RenderingServer = trivial
```

How it works:
- 500 pooled canvas items via RenderingServer (not Node2D)
- Vehicles follow road segment paths from traffic model
- Speed matches congestion level (smooth deceleration in jams)
- Sub-pixel movement (no jitter)
- Rush hour variation: 3x density during peak hours

Visual result: Roads feel alive. Traffic jams are visible and satisfying to fix. Watching a well-planned network flow smoothly is the core visual reward.

#### 1c. Building Smoke/Steam
```
Impact: ★★★★☆ (immediate life)
Effort: 1 GPUParticles2D template, reused
Performance: 8-12 active particle emitters = trivial
```

How it works:
- GPUParticles2D on industrial/power buildings
- Smoke rises, spreads, fades (4-8 particles per emitter)
- Color tinted by building type (grey smoke, white steam, dark exhaust)
- Affected by wind direction (optional)

Visual result: Factories and power plants look operational. Industrial districts feel busy.

#### 1d. Color Palette Discipline
```
Impact: ★★★★★ (prevents "cheap" look at source)
Effort: 1-2 days of palette design
Performance: Zero (it's art direction, not runtime)
```

The palette:
- 48 base colors with smooth value ramps
- 5 era sub-palettes (Frontier=earthy, Industrial=soot, Modern=glass)
- 4 seasonal sub-palettes (Spring=fresh, Summer=vivid, Autumn=warm, Winter=cool)
- All palettes share the same value structure (darks, mids, lights)
- **Rule: no fully saturated colors.** Everything is slightly desaturated for cohesion.

Reference: [SLYNYRD's palette design guide](https://www.slynyrd.com/blog/2018/1/10/pixelblog-1-color-palettes)

---

### Tier 2: High Impact (Worth the Effort)

#### 2a. Water Shader with Reflection
```
Impact: ★★★★☆ (beautiful rivers and coastlines)
Effort: 1 shader (ready-made available for Godot)
Performance: Low (applied only to water tiles)
```

How it works:
- Base water tile with animated UV scrolling (2-3 frame flow)
- Flip nearby building/tree sprites vertically below water line
- Blue tint + transparency on flipped sprites
- Optional: distortion shader for wavy reflections

Visual result: Rivers and coastlines look stunning. Waterfront property areas become visually premium. Major differentiator from Theotown.

References:
- [Pixel art water shader for Godot](https://godotshaders.com/shader/pixel-art-water/)
- [Water surface shader tutorial](https://injuly.in/blog/water-shader/index.html)

#### 2b. Weather Particles
```
Impact: ★★★★☆ (atmospheric, seasonal variety)
Effort: 2 GPUParticles2D systems + overlay sprites
Performance: Trivial (GPU-driven particles)
```

Rain system:
- GPUParticles2D: 200-400 rain drop particles (diagonal, fast)
- Overlay: wet surface darkening shader on ground tiles
- Puddle sprites on flat ground tiles (animated 3-frame ripple)
- Sound: rain ambience varies by intensity

Snow system:
- GPUParticles2D: 200-400 snowflake particles (slow, drifting)
- Overlay: snow accumulation sprites on roofs and ground
- Accumulation grows over game hours during snowfall

Visual result: Seasons feel dramatic. Rain makes the city moody. Snow transforms the entire landscape.

#### 2c. Citizen Sprites
```
Impact: ★★★☆☆ (life at close zoom)
Effort: 6-frame walk cycle x 4 directions x 3 wealth variants = 72 frames
Performance: 200 pooled via RenderingServer = trivial
```

How it works:
- 200 citizen sprites via RenderingServer (same pattern as vehicles)
- Walk along sidewalk paths adjacent to roads
- Density varies by time of day (rush hour, lunch, evening, night)
- At zoom 3+: hidden (too small to see anyway)
- Wealth-coded: different sprite variants for poor/middle/rich areas

Visual result: At close zoom, streets feel populated. Combined with vehicles, creates the "alive city" effect.

#### 2d. Seasonal Palette Variations
```
Impact: ★★★★☆ (4x visual variety for free)
Effort: 4 palette rows (extend day/night shader)
Performance: Zero additional (same shader)
```

How it works:
- Extend the day/night palette to include seasonal variants
- Spring: fresh greens, pink cherry blossoms
- Summer: vivid greens, golden sunlight, deep blue water
- Autumn: amber, burnt orange, russet trees
- Winter: white snow, cool blues, bare brown trees

Visual result: The city transforms 4 times per game year. Each season looks distinct and beautiful. Screenshots always interesting.

---

### Tier 3: Delight (Wow Factor)

#### 3a. Bloom/Glow Post-Process
```
Impact: ★★★☆☆ (premium feel on special elements)
Effort: 1 post-process shader
Performance: Medium (full-screen Gaussian blur)
```

How it works:
- Full-screen post-process on a CanvasLayer
- Extract pixels above brightness threshold
- Gaussian blur (2-pass: horizontal + vertical, 29 samples each)
- Recombine with original at low opacity (10-20%)

Applied to:
- Neon signs (Modern/Future era) at night
- Street lights at night
- Fire (buildings on fire, fireplaces)
- Special buildings (power plants, research labs)
- NOT applied during daytime (performance savings)

Visual result: Night scenes feel magical. Neon signs pop. Fire events are dramatic.

#### 3b. Building Quality Visual Progression
```
Impact: ★★★☆☆ (communicates gameplay progress)
Effort: Art direction rule, no shader
Performance: Zero
```

Following [Foundation's technique](https://www.gamedeveloper.com/art/deep-dive-the-art-of-i-foundation-i-):
- Low-quality buildings: muted colors, simpler shapes, fewer details, slight chaos
- Mid-quality buildings: cleaner lines, moderate detail, balanced palette
- High-quality buildings: sharper details, stronger color contrast, more ornament
- Abandoned buildings: desaturated, broken details (shader-driven aging)

Visual result: You can tell a neighborhood's wealth class at a glance. Visual storytelling without UI.

#### 3c. Ambient Details
```
Impact: ★★☆☆☆ per item, ★★★★☆ cumulative
Effort: 2-4 frame animations each
Performance: Negligible
```

Animated details to scatter:
- Flags waving on government buildings (2-frame)
- Birds landing/taking off near parks (3-frame)
- Steam vents on industrial buildings (2-frame)
- Waving trees/vegetation (2-frame, wind-responsive)
- Flickering street lights (palette toggle)
- Church bells at noon (subtle ring animation)
- Fountain spray in parks (3-frame)

Visual result: Cumulative effect of 8-12 ambient details makes the city feel like a living organism.

#### 3d. Dynamic Fog/Cloud Shadows
```
Impact: ★★☆☆☆ (atmospheric)
Effort: 1 shader (noise-based scrolling overlay)
Performance: Very low
```

How it works:
- Full-screen overlay with scrolling noise texture
- Creates moving shadow patterns across the city
- Intensity varies by weather (heavy fog in autumn, light in summer)
- At night: not used (darkness is the atmosphere)

Visual result: Subtle depth and atmosphere. City feels like it exists under a sky.

---

## 4. PERFORMANCE BUDGET

### Godot 4 Reality Check

| Feature | Count | Method | Performance |
|---------|-------|--------|-------------|
| Terrain tiles visible | ~400-800 | Chunked TileMap (32x32) | ~1-2ms |
| Building sprites visible | ~100-200 | TileMap layer | ~0.5-1ms |
| Vehicle sprites | 80-150 on screen | RenderingServer (500 pool) | ~0.3-0.5ms |
| Citizen sprites | 30-60 on screen | RenderingServer (200 pool) | ~0.2ms |
| Smoke/steam emitters | 8-12 active | GPUParticles2D | ~0.2ms |
| Weather particles | 200-400 drops/flakes | GPUParticles2D | ~0.3ms |
| Day/night shader | 1 full-screen | ColorRect shader | ~0.1ms |
| Water shader | ~20-40 water tiles | Per-tile shader | ~0.2ms |
| Bloom post-process | 1 full-screen (night only) | 2-pass Gaussian | ~0.5-1ms |
| Fog/cloud overlay | 1 full-screen | Noise scroll shader | ~0.1ms |
| **TOTAL** | | | **~3.5-6ms** |

**Target frame time: 16.6ms (60fps)**
**Rendering budget: ~6ms**
**Simulation bridge: ~1ms**
**Available headroom: ~9ms**

This is well within budget. We could double the vehicle count and still maintain 60fps.

### Known Limits to Respect

| Limit | Value | Workaround |
|-------|-------|------------|
| PointLight2D max | 16 before all go invisible | Use shader-based fake lights |
| Animated sprites | ~31 AnimatedSprite2D nodes before perf drops | Use RenderingServer + manual animation |
| TileMap Y-sort | Perf cliff at 500x500+ | Chunked loading (32x32) |
| Unique shaders | Each breaks draw batching | Share materials, minimize unique shaders |

---

## 5. VISUAL COMPARISON: Iron & Oak vs Competitors

### At Launch Quality (Tier 1 + Tier 2 features)

| Visual Feature | Theotown | SimCity 4 | Cities: Skylines | **Iron & Oak** |
|---------------|----------|-----------|-----------------|----------------|
| Art style | Pixel (basic) | Pre-rendered 3D | Full 3D | **Pixel (premium)** |
| Day/night cycle | No | Yes | Yes | **Yes (palette swap)** |
| Seasons | No | No | Yes (mod) | **Yes (4 seasons)** |
| Weather effects | No | Yes (basic) | Yes | **Yes (rain, snow, fog)** |
| Traffic animation | Basic | Good | Excellent | **Good (smooth flow)** |
| Building animation | None | Smoke only | Full | **Smoke, lights, flags** |
| Water quality | Static blue | Animated | Full 3D | **Animated + reflection** |
| Night lighting | None | Building glow | Full 3D lights | **Palette glow + bloom** |
| Citizen sprites | None | Tiny dots | Full 3D | **Walk cycles (close zoom)** |
| Visual "life" feel | Low | Medium | High | **Medium-High** |

### Where Iron & Oak Wins Visually
1. **Pixel art charm** that 3D games can't match (Stardew Valley proved this market exists)
2. **Palette-driven seasons** that transform the entire city aesthetically
3. **Consistent art direction** (AI-generated but palette-enforced)
4. **Night mode** with warm window glow (most pixel art city builders don't have this)

### Where Iron & Oak Won't Match 3D Games
1. Individual vehicle detail (10px car vs. full 3D model)
2. Camera rotation with real 3D perspective
3. Individual citizen facial expressions / reactions
4. Realistic water physics (our water is tile-based + shader)

**This is fine.** The target audience wants pixel art WITH depth, not 3D city builder #47.

---

## 6. KIMI IMAGE-TO-CODE WORKFLOW FOR VISUAL ASSETS

### When to Use Kimi for Visual Work

| Task | Kimi Usage | Alternative |
|------|-----------|-------------|
| UI panel layouts | Screenshot reference game → generate .tscn | Build from scratch |
| Shader reference | Screenshot visual effect → analyze → generate equivalent .gdshader | Write from scratch |
| Tileset configuration | Screenshot isometric tileset → generate TileSet .tres | Manual editor work |
| Sprite sheet layout | Screenshot reference → generate SpriteFrames metadata | Manual JSON |
| Color palette extraction | Screenshot game with good palette → extract hex values | Manual color picking |

### When NOT to Use Kimi
- Generating actual pixel art sprites (use dedicated art AI: Midjourney/DALL-E)
- Writing simulation code (use Claude)
- Animation timing (use game feel research)

---

## 7. ART AI PIPELINE FOR BUILDINGS

### The Batch Generation Workflow

```
Step 1: Describe building in Midjourney/DALL-E prompt
        "isometric pixel art [frontier wooden cabin], 32x16 tile base,
         clean pixel art, 48-color earthy palette, dark 1px outline,
         top-left lighting, white background, Stardew Valley quality"

Step 2: Generate 4 variations (10 seconds)

Step 3: Human picks best one (5 seconds)

Step 4: Claude post-processing script:
        - Downscale to exact pixel dimensions
        - Remap colors to Iron & Oak palette (48 base colors)
        - Add consistent 1px dark outline
        - Add shadow on bottom-right (consistent with top-left light)
        - Export as .png with transparent background

Step 5: Quality check:
        - Does it match era palette? (Frontier = earthy browns)
        - Is the silhouette distinct? (recognizable at zoom 3)
        - Is the lighting direction correct? (top-left)
        - Does it tile cleanly with adjacent buildings?

At 30 seconds per building, 260 buildings = ~2 hours
```

### Palette Enforcement Script (Critical for Consistency)

The #1 reason AI-generated pixel art looks "cheap" is palette inconsistency. Solution:

```python
# Post-process every AI-generated sprite to enforce palette
def enforce_palette(image, palette):
    """Map every pixel to nearest color in palette."""
    for pixel in image:
        nearest = min(palette, key=lambda c: color_distance(pixel, c))
        pixel = nearest
    return image
```

This ensures ALL buildings look cohesive regardless of which AI generated them.

---

## 8. PRIORITY IMPLEMENTATION ORDER

For maximum visual impact with minimum effort, implement in this exact order:

### Week 1: Foundation Visuals
1. **48-color base palette** with era/season variants (art direction, no code)
2. **Day/night palette swap shader** (1 shader, transforms entire game)
3. **Smooth vehicle movement** via RenderingServer (already planned)
4. **3 terrain tile variants** per type (prevents repetition)

### Week 2: Life
5. **Building smoke/steam** via GPUParticles2D (instant life)
6. **Water shader** with reflection (beautiful rivers/coasts)
7. **Citizen walk cycles** via RenderingServer (close zoom life)
8. **Ambient sounds** varying by zoom and time (not visual, but multiplies visual satisfaction)

### Week 3: Atmosphere
9. **Rain and snow** weather particles + surface reactions
10. **Seasonal palette variations** (extend day/night shader)
11. **Bloom/glow** at night for neon signs and street lights
12. **Ambient details** (flags, birds, steam vents)

### Week 4: Polish
13. **Building aging shader** (condition = visual degradation)
14. **Fog/cloud shadows** (atmospheric overlay)
15. **Heat shimmer** on industrial zones
16. **Sound design** for traffic flow (smooth vs. congested audio)

---

## Sources

### Visual Analysis
- [SLYNYRD Pixel Art Tutorials](https://www.slynyrd.com/blog/2022/11/28/pixelblog-41-isometric-pixel-art)
- [Foundation Art Deep Dive](https://www.gamedeveloper.com/art/deep-dive-the-art-of-i-foundation-i-)
- [HD-2D (Octopath) Technique](https://en.wikipedia.org/wiki/HD-2D)
- [Dead Cells 3D-to-Pixel Pipeline](https://www.gamedeveloper.com/production/art-design-deep-dive-using-a-3d-pipeline-for-2d-animation-in-i-dead-cells-i-)
- [SimCity 4 Rendering](http://oceanquigley.blogspot.com/2010/04/lighting-buildings-in-simcity4.html)

### Traffic Satisfaction
- [Mini Motorways Design Analysis](https://www.oreateai.com/blog/drawing-the-lines-how-mini-motorways-turns-traffic-chaos-into-zen/880990314d943ee6371e04550b056c23)
- [Cities Skylines Traffic Architecture](https://www.gamedeveloper.com/design/how-traffic-works-in-cities-skylines)

### Godot Performance
- [RenderingServer 10K Sprites](https://forum.godotengine.org/t/can-godot-draw-10k-sprites-or-more-using-renderingserver/95080)
- [PointLight2D 16 Limit](https://github.com/godotengine/godot/issues/68812)
- [Animated Sprite Performance](https://github.com/godotengine/godot/issues/74540)
- [Isometric Lighting in Godot 4.4](https://www.connorwolf.com/post/realtime-2d-lighting-with-shadows-on-isometric-tiles-in-godot-4-4)

### Shaders & Effects
- [KoBeWi Palette Swap Shader](https://github.com/KoBeWi/Godot-Palette-Swap-Shader)
- [Pixel Art Water Shader](https://godotshaders.com/shader/pixel-art-water/)
- [Procedural 2D Fog](https://godotshaders.com/shader/procedural-2d-fog-with-pixelation/)
- [Heat Shimmer Shader](https://godotshaders.com/shader/yoshis-island-shimmer-heat-haze-distortion/)
- [2D Normal Map Lighting](https://www.gdquest.com/tutorial/godot/2d/lighting-with-normal-maps/)

### Animation
- [Walk Cycle Guide (8 frames)](https://www.slynyrd.com/blog/2024/5/24/pixelblog-50-human-walk-cycle)
- [Sub-pixel Animation](https://peerdh.com/blogs/programming-insights/frame-by-frame-techniques-for-fluid-character-movement-in-pixel-art-2)
