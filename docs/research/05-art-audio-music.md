# Research: 05 Art Audio Music

The full research report has been compiled at `/Users/fredericbeeg/.claude/plans/jazzy-whistling-piglet-agent-ae2c6b60f5fa984c1.md`. Here is a summary of the key findings and actionable recommendations:

---

## Research Summary

### 1. Pixel Art Production at Scale

**Songs of Conquest** was built by 4-8 people using Aseprite + PyxelEdit + Unity. Their critical lesson: they originally planned high-res pixel art but realized it would take 5 years for one faction. They intentionally scaled down resolution. **Eastward** spent 6 years producing ~10,000 hand-drawn frames with a 3-10 person team. Hand-painting normal maps for dynamic lighting doubled or tripled time per sprite.

**AI-assisted workflows in 2026** can cut asset production by ~70%. The recommended pipeline: train a custom SDXL LoRA on 50-100 hand-drawn sprites from your first era, then use Retro Diffusion (Aseprite plugin) to accelerate the remaining 400+ buildings. Always run output through palette indexing and manual cleanup. Raw AI output should never ship.

**Aseprite is the clear production choice** -- $20, industry standard, and the only tool with AI plugin support (Retro Diffusion, PixelAI).

### 2. Isometric Art Techniques

**Use 2:1 ratio (not true 30-degree)** -- this is non-negotiable for pixel art. Clean lines, integer math, proven by every classic isometric game. Recommended tile size: **64x32** for Songs of Conquest-quality detail.

**Multi-story buildings** should use the stacked height-tile technique: break buildings into individual floors at (x, y, z) coordinates. This gives perfect depth sorting, modularity (procedural skyscrapers), and construction animation for free.

**OpenTTD** achieves enormous visual variety with only ~7,000 sprites through layering, randomization, and dynamic callbacks that change building sprites based on game year. A similar system could keep Iron & Oak's actual sprite count at ~890 total while delivering rich variety.

### 3. Adaptive Music

The recommended stack is **AIVA for composition** (exports MIDI for precise transition authoring) + **FMOD Studio for runtime** (indie-friendly, excellent engine integration). The system should use vertical layering (stems that fade in/out based on population, era, zoom level) combined with horizontal re-sequencing for era transitions.

Each of the 5 eras should have a distinct musical identity: 1850=folk strings/dulcimer, 1900=brass/piano/mechanical rhythms, 1950=jazz/Americana, 2000=ambient electronic/indie-folk, 2050=organic-synthetic hybrid.

### 4. Sound Design

Zoom-level audio LOD is essential: zoomed out = 2D aggregate city hum + full score; zoomed in = 3D spatialized individual sounds + ducked music. The "city hum" must be data-driven (reading population density, zoning types, traffic). Placement sounds should follow the "plop psychology" -- tactile materiality scaled by building size, with era-appropriate construction sounds.

### 5. Art Direction Across Eras

Five distinct architectural vocabularies are defined with specific shape language, materials, rooflines, and pixel art rendering notes. Color theory maps each era to a psychological palette (sepia/warm for 1850, pastel optimism for 1950, cyan/clean for 2050). Night lighting evolves from flickering gas lamps (warm orange, tiny radius) through sodium vapor amber wash to sharp LED pools and futuristic ambient glow.

The **"Layered City" principle** is critical: real cities show multiple eras simultaneously. A Victorian home dwarfed by a Modernist tower tells the city's history visually.

### 6. UI Design

**m5x7 for primary UI, Proggy Clean for stat tables, m3x6 for tiny map labels only.** Follow Factorio's philosophy: function over form, progressive disclosure (UI complexity matches player progression), diegetic information (smoke from factories, lit windows in occupied buildings), and an Alt-mode toggle for information density. Panel chrome should subtly evolve with the dominant era.