# CITYMAJOR — AI Art Pipeline

**Tool Selection & Production Workflow**

Aethos Ventures | Confidential | March 2026 | v1.0

---

## Executive Summary

> **Key Finding:** For an isometric city builder with static building sprites, terrain tiles, and small props — no character animation, no facial consistency, no walk cycles — AI art tools in 2026 can collapse the art pipeline from 12–18 months of contracted pixel art to 2–3 months of AI generation + human curation. The bottleneck shifts from art production to game feel and balance tuning.

CityMajor requires approximately 600–800 unique isometric building sprites across 5 historical eras, plus terrain tiles, roads, trees, props, and tiny cosmetic vehicles. Every asset is a static isometric object on a 64x32 grid. No character sheets, no animation frames, no facial consistency. This is the single easiest category of game art for AI to handle well, because architectural variation is expected (no two buildings look alike) and style consistency across an era palette is a solved problem via LoRA training and reference-image systems.

This document evaluates the best AI tools for this specific pipeline, recommends a production stack, and outlines the end-to-end workflow from concept exploration through shipped sprites.

---

## Recommended Tool Stack

The following tools are ranked by their role in the CityMajor production pipeline. Quality is the primary selection criterion; cost is secondary.

| Tool | Role in Pipeline | Price/mo | Quality | Priority |
|------|------------------|----------|---------|----------|
| **PixelLab** | Core sprite generation | $50 | ★★★★★ | Primary |
| **Scenario.com** | Style consistency & batch | $32.50 | ★★★★★ | Primary |
| **ComfyUI + SDXL** | Custom LoRA pipeline | Free* | ★★★★☆ | Primary |
| **Midjourney V7** | Concept & art direction | $30 | ★★★★★ | Secondary |
| **Qwen-Image-Layered** | Layer-separated assets | $0.075/img | ★★★★☆ | Secondary |
| **GPT-4o Image Gen** | Quick iteration & edits | Included** | ★★★☆☆ | Supplemental |

\* Requires local GPU (10GB+ VRAM). Zero per-image cost after setup.

\*\* Included in ChatGPT Pro subscription ($200/mo, already active).

---

## Tool Deep Dives

### 1. PixelLab — Core Sprite Generation Engine

**Why it wins:** PixelLab is the only tool purpose-built for pixel art game assets. It generates actual grid-aligned, pixel-perfect sprites using proprietary models (PixFlux and BitForge), not "pixel-looking" high-res images that need manual downscaling. This distinction matters enormously for a game targeting 64x32 isometric tiles.

**Key features for CityMajor:** 4/8-directional sprite rotation (critical for isometric perspective), Wang/dual-grid tileset generation for terrain, transparent PNG output that actually works, style reference locking to maintain era consistency across hundreds of variants, and direct Aseprite plugin integration for generate-to-refine workflows. Over 3,000 indie developers currently use the platform in production.

**Limitations:** No built-in manual editor (Aseprite required for final cleanup). Isometric building generation is functional but not as refined as top-down sprites — expect 60–70% of outputs to need minor perspective or palette corrections. No batch API for fully automated pipelines at the highest tier.

**Pricing:** $12–$50/month (tiered). API pricing at $0.008–$0.013 per image for batch workflows.

**URL:** [pixellab.ai](https://pixellab.ai)

---

### 2. Scenario.com — Style Bible Enforcer

**Why it wins:** Scenario's core differentiator is custom model training on your own art assets. Upload 10–15 curated reference buildings for each CityMajor era, train a custom model, and every subsequent generation is style-locked to that visual identity. This solves the "era palette drift" problem that plagues general-purpose generators.

**Key features for CityMajor:** 12 composition control modes, batch generation of 16 images per prompt, transparent PNG output, Unity integration via API, and the ability to train separate models per era. Used in production by Ubisoft, Scopely, and The Sandbox. Scenario also has a dedicated isometric tile generation tutorial and tooling in their pipeline.

**Limitations:** Custom model training requires 10–15 high-quality reference images per era (these become the art direction bottleneck). Output is not pixel-art-native — sprites need downscaling and palette quantization in post-processing. Monthly cost adds up if training multiple models.

**Pricing:** $32.50/month for Pro tier.

**URL:** [scenario.com](https://scenario.com)

---

### 3. ComfyUI + SDXL + Custom LoRAs — Maximum Control Pipeline

**Why it wins:** Zero per-image cost, maximum creative control, and the deepest ecosystem of pixel art models available. The proven workflow: SDXL base model → Pixel Art XL LoRA (by Astropulse/RetroDiffusion) → generate at 1024×1024 → downscale 8× with nearest-neighbor interpolation → palette quantize to era hex codes. This produces true pixel art with correct grid alignment.

**Key features for CityMajor:** Per-era style LoRAs trainable with Kohya SS (30–50 reference images, ~$4–8 per LoRA via CivitAI Trainer). ControlNet Canny extracts edge maps from best sprites to use as structural templates for variations — one house silhouette yields 12 distinct variants. ComfyUI-PixelArt-Detector node handles palette enforcement and grid alignment automatically. Flux Kontext LoRAs offer 94.7% character/object consistency for maintaining building identity across angles.

**Key models:** Pixel Art Diffusion XL "Sprite Shaper" (primary SDXL checkpoint by Yamer), Super_PixelArt_Sprite_XL (128×128 sprites at 1024×1024), Flux 2D Game Assets LoRA (trigger: GRPZA), and era-specific LoRAs (8-bit, 16-bit, 64-bit aesthetics).

**Limitations:** 2–3 day learning curve for ComfyUI setup with all nodes. Requires 10GB+ VRAM for local inference. LoRA training demands careful dataset curation. More technical overhead than hosted solutions.

**Pricing:** Free (open source). GPU electricity + optional $4–8 per LoRA training via cloud services.

---

### 4. Midjourney V7 — Concept Exploration & Art Direction

**Why it wins:** Unmatched quality for visual exploration and mood boards. Before committing to a style per era, Midjourney produces stunning isometric concept images from prompts like "isometric pixel art Victorian industrial district, red brick, smoking chimneys, 2:1 isometric, limited palette." The `--sref` style reference system supports 4.2 billion numerical style codes, and the new Style Creator tool generates custom codes interactively.

**Key features for CityMajor:** Style reference locking (`--sref`) for consistent era vibes across exploration sessions. V7 produces the highest-quality isometric concept art of any tool. Excellent for establishing the visual language of each era before training production LoRAs.

**Limitations:** No API access, no fine-tuning, no transparent background support, no pixel-perfect output. Unsuitable for final production assets. Concept and direction tool only.

**Pricing:** $30/month for Standard plan.

---

### 5. Qwen-Image-Layered — Layer-Separated Asset Generation

**Why it wins:** Alibaba's model outputs images with native RGBA layer decomposition — building separated from background, chimney smoke as a separate layer, window glow as a separate layer. This is explicitly designed for game art pipelines and represents a capability no Western model currently offers.

**Key features for CityMajor:** Pre-separated layers save enormous post-processing time for day/night window lighting swaps, seasonal decoration overlays, and smoke/particle effects. Apache 2.0 license (full commercial use). Qwen-Image-2512 ranks as the strongest open-source image model on AI Arena's blind human evaluation.

**Limitations:** Not pixel-art-native — outputs require downscaling and palette quantization. Layer separation quality varies by scene complexity. Newer model with less community documentation than SDXL/Flux ecosystem.

**Pricing:** $0.075 per image via API. Open-source (Apache 2.0) for self-hosting.

---

### 6. GPT-4o Image Generation — Rapid Iteration & Edits

**Why it wins:** Conversational editing loop. "Make this building but with a clock tower added" or "same style but Art Deco instead of Victorian" is faster than re-prompting any other tool. Useful for rapid concept iteration when designing 154+ building types and exploring variants before committing to final generation.

**Limitations:** Weak pixel art precision. Poor cross-session consistency. Not suitable for final production assets. Supplemental tool only.

**Pricing:** Included in ChatGPT Pro ($200/month, already active).

---

## Important Clarification: Kimi

> **Kimi Cannot Generate Images.** Kimi by Moonshot AI (K2.5, ~1 trillion parameters) is a multimodal understanding and reasoning model. It can process images as input but cannot create them. In testing, Kimi suggested external tools like DALL-E and Midjourney instead of generating images itself. Kimi's strength is visual-to-code conversion (generating frontend code from mockups), which is tangentially useful for game UI but irrelevant for asset creation. The Chinese AI model with strong image generation is **Qwen-Image by Alibaba**, not Kimi.

---

## Production Workflow

The following 6-step workflow takes CityMajor from blank canvas to shipped sprites. Estimated total timeline: 8–12 weeks for full 5-era asset set.

### Step 1: Era Visual Exploration (Week 1–2)

**Tool: Midjourney V7.** Generate 50–100 concept images per era using isometric pixel art prompts locked with `--sref` style codes. Explore palette variations, architectural styles, density levels, and atmospheric moods. The hex codes from the game concept doc (e.g., Era 2: `#8B4513` dark brick, `#A52A2A` industrial red, `#708090` slate grey) become prompt constraints. Output: 5 locked era mood boards with 10–15 hero building concepts each.

### Step 2: Style Bible & Reference Sets (Week 2–3)

**Tools: Aseprite + Midjourney outputs.** Hand-curate or manually refine the best 10–15 buildings per era into pixel-perfect reference sprites at the target resolution. These become the training data for LoRAs and the reference images for PixelLab/Scenario. Lock palette (exact hex codes), iso angle (2:1), line weight (1px darker shade, no black outlines), shadow direction (consistent light source per era), and dithering patterns. Output: 5 era style bibles with 10–15 reference sprites each (50–75 total).

### Step 3: Model Training (Week 3–4)

**Tools: Scenario.com custom training + Kohya SS for SDXL LoRAs.** Train one custom Scenario model per era using the reference sets. Simultaneously train SDXL LoRAs with Kohya SS (30–50 images per era, learning rate 0.0003–0.0008, 5–15 epochs, network dim 16–64). The dual approach gives redundancy: Scenario for quick hosted generation, local LoRAs for batch processing and fine control. Output: 5 era-locked generation models (Scenario) + 5 SDXL LoRAs.

### Step 4: Bulk Sprite Generation (Week 4–8)

**Tools: PixelLab (primary) + ComfyUI with LoRAs (batch).** Generate all building types per era. For each building type: prompt with era LoRA/reference, generate 15–20 variants, curate the best 3–5, select final candidate. PixelLab handles the core isometric sprites with its native grid alignment. ComfyUI handles batch generation of variants using ControlNet Canny (extract edge maps from best sprites as structural templates). Use Qwen-Image-Layered for buildings that need separate window/smoke/detail layers for programmatic day/night swaps. Target: ~50–80 building types per era × 5 eras = 250–400 unique buildings, plus density variants bringing total to 600–800 sprites.

### Step 5: Human Polish Pass (Week 8–10)

**Tool: Aseprite.** Palette enforcement: run all sprites through indexed color reduction to era hex codes. Fix any iso perspective inconsistencies (AI occasionally drifts 1–2 pixels on shadow angles). Polish the 30–40 most visible buildings (city hall, landmarks, era-defining structures) that appear in screenshots and the Steam trailer. The small residential filler visible only at city-wide zoom gets minimal touch-up. Estimated effort: 15–30 minutes per sprite for hero buildings, 5–10 minutes for filler.

### Step 6: Terrain, Props & Programmatic Effects (Week 10–12)

**Tools: PixelLab (tilesets) + shaders.** Generate terrain tilesets using PixelLab's Wang tile system. Roads, water, grass, dirt, concrete — these are pattern-based and well within AI capability. Trees, props, and tiny vehicles (16×8 pixel blobs) are trivial generation targets. Day/night cycles use a global color multiply shader pass + additive light sprites for windows. Seasons use palette-shifted foliage and snow overlay shaders. Weather is a screen-space particle system. These are rendering effects, not additional art assets.

---

## Why AI Art Works Specifically for City Builders

The CityMajor art pipeline is uniquely well-suited to AI generation for several structural reasons that don't apply to most game genres:

**Every building is independent.** A Victorian rowhouse doesn't need to look identical to a Victorian factory — it just needs to share the same era palette, pixel density, iso perspective, and line weight. This is a style consistency problem (solved by LoRA training and reference systems), not a frame-by-frame consistency problem (still difficult for AI).

**Architectural variation is expected.** No two buildings in a real city look identical. Slight AI variation in chimney placement, window patterns, or roofline actually helps rather than hurts. If a generated factory has a slightly asymmetric chimney, that's character. If a generated walk cycle has a wonky leg on frame 3, the game is broken.

**Static sprites only.** No character animation sheets, no facial consistency requirements, no multi-frame sequences. Each building is one image. This eliminates the hardest category of AI art (temporal consistency across frames) entirely.

**Visual effects are programmatic.** Day/night, seasons, and weather don't require separate sprite sets. A global color multiply pass, additive light sprites, and screen-space particle systems handle these at the rendering level. The art pipeline produces one set of base sprites; the engine handles the rest.

**Scale forgives imperfection.** At city-wide zoom (the primary play view), individual buildings are 32–64 pixels tall on screen. Minor AI artifacts are invisible at this scale. Only the 30–40 hero buildings seen in close-up or screenshots need pixel-perfect polish.

---

## Steam Disclosure & Community Considerations

Steam requires AI content disclosure. Approximately 20% of new Steam releases in 2025 disclosed AI content, and a browser extension called "AI Warning for Steam" now flags disclosed titles. The indie/pixel art community has vocal opposition to AI-generated art.

Recommended approach for CityMajor: Disclose AI usage honestly (Steam requires it). Frame it as "AI-assisted production with human art direction" rather than "AI-generated art." Ensure the 30–40 hero buildings and all marketing/trailer screenshots have visible human polish. The economic simulation and game design are the selling points — the pixel art is the visual wrapper, not the product. Players who bounce on AI art disclosure were unlikely to be the target audience (deep city builder / grand strategy enthusiasts care about simulation depth, not art production methods).

---

## Cost Estimate

| Item | Monthly Cost | Duration |
|------|-------------|----------|
| PixelLab Pro | $50 | 3 months |
| Scenario.com Pro | $32.50 | 3 months |
| Midjourney Standard | $30 | 2 months |
| ComfyUI + SDXL | Free (local GPU) | Ongoing |
| LoRA training (5 eras × $6) | $30 one-time | One-time |
| Qwen-Image API (~500 images) | ~$37.50 one-time | One-time |
| GPT-4o (ChatGPT Pro) | $200 (existing) | Ongoing |
| **TOTAL (new costs only)** | **~$405** | **Over 3 months** |

Excludes existing subscriptions (ChatGPT Pro, Claude Max). Compare to traditional contracted pixel art: $5–$25 per sprite × 800 sprites = $4,000–$20,000 + 6–18 months timeline.

---

## 3D Module Pipeline (Web v1)

> **Added 2026-07** for mesh 3D pivot ([SB-3704](https://linear.app/softblaze/issue/SB-3704)). Supplements — does not replace — the pixel sprite workflow above for historical reference.

The web client uses **hybrid procedural kitbash**, not 600 unique GLTF buildings. Pixel-art batch generation does not transfer; the bottleneck remains curation, not raw generation.

### Split

| Tier | % of visual | Method |
|------|-------------|--------|
| **Modules** | ~70% | **40–60 instanced archetypes per era** — walls, roofs, chimneys, awnings, window bays as modular GLTF parts |
| **Assembly** | procedural | Port `BuildingRenderer.cs` rules: footprint, stories, era palette, construction/abandoned states |
| **Hero landmarks** | ~30% polish budget | **8–12 hand-authored GLTF per era** (city hall, cathedral, steel mill) for screenshots/trailer |
| **Cosmetics (shop)** | swap layer | Alternate materials + facade modules on same `InstancedMesh` — sim-neutral MTX |

### TypeId → mesh archetype (preserve taxonomy)

```
100-199 Residential low  → res_low_{era}_{variant}
200-299 Residential high → res_high_{era}_{variant}
300-399 Commercial       → com_{era}_{variant}
400-499 Industrial       → ind_{era}_{variant}
500+     Services        → hero GLTF or service_{type}
```

Era bands (x00–x19 Frontier, x20–x39 Industrial, etc.) from `BuildingRenderer` comments stay unchanged.

### AI 3D production workflow

| Step | Tool | Output |
|------|------|--------|
| 1. Module draft | **Meshy**, **Tripo**, **Scenario 3D** | Raw modular parts (wall, roof, chimney) |
| 2. Cleanup | **Blender** — scale to grid, merge verts, PBR bake | Era-consistent GLTF modules |
| 3. Hero polish | Blender + reference renders from **Midjourney** mood boards | 8–12 landmarks/era |
| 4. Atlas & materials | Shared PBR per era; normal maps on heroes only | CDN-ready `*.gltf` + textures |
| 5. QA | In-engine — silhouette at L2 zoom, triangle budget, instancing | Ship to module library |

**Mid-fidelity rule:** Shared materials per era; normal maps on hero buildings only. Variety comes from procedural assembly + material swaps, not 5k unique meshes.

### Performance constraints

- One `InstancedMesh` per archetype per visible chunk — target 20–40 draw calls/chunk
- `Cache-Control: immutable` on CDN GLTF modules
- Cosmetic skins = alternate mesh/material on same instance index — no sim change

### Tools (3D-specific)

| Tool | Role | Notes |
|------|------|-------|
| Meshy / Tripo | Module draft generation | Fast iteration; expect Blender cleanup |
| Scenario 3D | Style-locked batch modules | Era consistency |
| Blender | Grid snap, LOD simplification, export GLTF | Required |
| ComfyUI + 3D nodes | Optional texture/normal generation | Supplemental |

---

## Bottom Line

> **Revised Assessment:** Art is no longer the primary bottleneck for CityMajor. For an isometric city builder with static buildings and no character work, AI art tools in 2026 collapse the pipeline from a multi-year contracted art budget to ~$400 and 8–12 weeks of generation + curation. The real bottleneck — and where development time should be concentrated — is whether the Economic Control Spectrum produces genuinely fun emergent decisions. Build the simulation headless with placeholder rectangles first. Prove the game is fun. Then layer the art.
