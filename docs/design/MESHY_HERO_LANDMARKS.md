# Meshy Hero Landmark Generation Briefs

**Linear:** [SB-3680](https://linear.app/softblaze/issue/SB-3680)  
**Status:** Draft — pilot set (8 landmarks)  
**Date:** 2026-07-04  
**Related:** [`BUILDING_ARCHETYPE_3D.md`](BUILDING_ARCHETYPE_3D.md), [`AI_ART_PIPELINE.md`](AI_ART_PIPELINE.md) §3D Module Pipeline, [`MASTER_GAME_CONCEPT.md`](MASTER_GAME_CONCEPT.md) §8

---

## Purpose

Define Meshy text-to-3D generation briefs for the **first eight hero landmarks** — hand-polished GLTF assets that anchor screenshots, trailers, and civic/service placement. Bulk zone buildings remain instanced archetypes; heroes are separate GLB files loaded by `TypeId` or service type.

**Output path:** `web/public/assets/gltf/heroes/`  
**Naming:** `hero_{era}_{id}.glb` — era slug matches `BUILDING_ARCHETYPE_3D` (`frontier` | `industrial` | `postwar` | `modern` | `future`).

---

## Global constraints

| Parameter | Value | Notes |
|-----------|-------|-------|
| World scale | **1 tile = 1 world unit** | See `web/lib/constants.ts` (`TILE_SIZE = 1`) |
| Footprint origin | Ground-center of footprint | Blender origin at `(footprintW/2, 0, footprintD/2)` in tile space |
| Up axis | **Y-up** | Match Three.js / glTF default |
| Poly budget | **2,000–8,000 tris** | Mid-fidelity; larger 4×4 assets may use upper band |
| Meshy `topology` | `triangle` | Required for web export |
| Meshy `art_style` | `realistic` | Civic/industrial architecture |
| Meshy `ai_model` | `meshy-5` | Preview → `meshy_refine_3d` for PBR textures |
| Meshy `should_remesh` | `true` | Enforce poly cap before refine |
| Materials | PBR metallic-roughness | Era palette applied in-engine via tint; bake neutral base colors |
| LOD | Single mesh L0 | L1 simplified mesh is a follow-up task (Blender decimate) |

### Era palette hints (in-engine tint)

Use these as **prompt color anchors**; final tint is applied at render time from `ERA_MATERIAL_HINTS` in `web/packages/sim-types/src/buildingArchetypes.ts`.

| Era | Key colors | Atmosphere |
|-----|------------|------------|
| `frontier` | `#8D6E63`, `#A1887F`, `#6B8E23` | Timber, brick sienna, field green, gas-lamp gold |
| `industrial` | `#B71C1C`, `#455A64`, `#708090` | Red brick, slate, iron, furnace orange haze |
| `postwar` | `#ECEFF1`, `#CFD8DC`, `#FF4500` | Concrete, chrome, neon accents |
| `modern` | `#90CAF9`, `#1565C0`, `#E0E0E0` | Glass blue, steel, white modernist |
| `future` | `#B2EBF2`, `#E1F5FE`, `#98FB98` | Ghost white, tech cyan, living green |

---

## Asset briefs

### 1. Frontier Town Hall

| Field | Value |
|-------|-------|
| **File** | `hero_frontier_city_hall.glb` |
| **Era** | `frontier` |
| **Footprint** | **2×2 tiles** (2×2×1.8 units tall approx.) |
| **Poly budget** | 3,500 tris |
| **Texture** | 1024×1024 |

**Meshy prompt:**
```
Mid-fidelity game asset, 1850s American frontier town hall, two-story wooden clapboard with covered porch, bell tower cupola, whitewashed timber walls with brown trim, peaked shingle roof, flagpole, gas lamp by entrance, simple symmetrical facade, clean readable silhouette, no surrounding terrain, isolated building on flat ground, PBR game-ready, orthographic-friendly three-quarter view
```

**Negative prompt:**
```
people, cars, horses, trees, terrain, grass, sky, clouds, text, signage, logos, interior, cutaway, low poly blob, cartoon, anime, photorealistic ground, HDR environment, blurry, broken geometry, floating parts, modern glass, skyscrapers
```

**Meshy `target_polycount`:** `3500`

---

### 2. Frontier Wooden Church

| Field | Value |
|-------|-------|
| **File** | `hero_frontier_church.glb` |
| **Era** | `frontier` |
| **Footprint** | **2×2 tiles** |
| **Poly budget** | 3,000 tris |
| **Texture** | 1024×1024 |

**Meshy prompt:**
```
Mid-fidelity game asset, 1860s frontier wooden church, white painted clapboard walls, steep gabled roof, simple steeple with cross, arched double doors, tall narrow windows, small cemetery-free isolated building, warm timber and white palette, clean silhouette for city builder game, PBR game-ready, no landscape
```

**Negative prompt:**
```
gravestones, cemetery, people, interior, stained glass detail overload, gothic cathedral scale, stone castle, modern materials, terrain, trees, sky, text, blurry, floating steeple, broken mesh
```

**Meshy `target_polycount`:** `3000`

---

### 3. Industrial Steel Mill

| Field | Value |
|-------|-------|
| **File** | `hero_industrial_steel_mill.glb` |
| **Era** | `industrial` |
| **Footprint** | **4×4 tiles** |
| **Poly budget** | 7,500 tris |
| **Texture** | 2048×2048 |

**Meshy prompt:**
```
Mid-fidelity game asset, Victorian industrial steel mill complex 1890s, red brick main hall with tall chimney stacks, sawtooth clerestory roof, iron trusses, blast furnace pipe, coal hopper, soot-stained brick and dark iron, compact factory footprint, no workers, isolated building on flat pad, city builder landmark, PBR game-ready, readable silhouette from distance
```

**Negative prompt:**
```
modern factory, clean white plant, people, vehicles, trains, smoke simulation mesh, terrain, trees, sky, interior, explosion, rusted ruins, cartoon, low detail blob, text, logos, oversized scale
```

**Meshy `target_polycount`:** `7500`

---

### 4. Victorian Train Station

| Field | Value |
|-------|-------|
| **File** | `hero_industrial_train_station.glb` |
| **Era** | `industrial` |
| **Footprint** | **3×3 tiles** |
| **Poly budget** | 6,000 tris |
| **Texture** | 2048×2048 |

**Meshy prompt:**
```
Mid-fidelity game asset, Victorian era grand train station 1905, red brick and sandstone facade, clock tower, arched windows, iron and glass canopy over platform entrance, Edwardian civic architecture, symmetrical front elevation, no train tracks or locomotive, isolated building on flat ground, city builder game landmark, PBR game-ready
```

**Negative prompt:**
```
modern metro station, airport, people, trains, rails, platforms extending to horizon, interior, terrain, trees, sky, blurry, cartoon, brutalist concrete, text on clock face, broken clock, floating canopy
```

**Meshy `target_polycount`:** `6000`

---

### 5. Postwar Art Deco Civic Hall

| Field | Value |
|-------|-------|
| **File** | `hero_postwar_civic_hall.glb` |
| **Era** | `postwar` |
| **Footprint** | **3×3 tiles** |
| **Poly budget** | 5,500 tris |
| **Texture** | 1024×1024 |

**Meshy prompt:**
```
Mid-fidelity game asset, 1930s Art Deco city hall, stepped ziggurat crown, limestone and cream concrete facade, vertical chrome fins, tall central tower, geometric relief patterns, flat roof sections, civic government building, no flags with text, isolated on flat pad, city builder landmark, PBR game-ready, clean 3/4 view silhouette
```

**Negative prompt:**
```
gothic cathedral, glass curtain wall skyscraper, people, cars, interior, terrain, trees, sky, neon signs, text, logos, cartoon, brutalist raw concrete only, broken geometry, floating tower
```

**Meshy `target_polycount`:** `5500`

---

### 6. Postwar General Hospital

| Field | Value |
|-------|-------|
| **File** | `hero_postwar_hospital.glb` |
| **Era** | `postwar` |
| **Footprint** | **3×3 tiles** |
| **Poly budget** | 5,000 tris |
| **Texture** | 1024×1024 |

**Meshy prompt:**
```
Mid-fidelity game asset, 1950s modernist general hospital, white and cream concrete wings, flat roofs with small mechanical penthouse, red cross emblem on central bay, ribbon windows, ambulance bay canopy, clean institutional architecture, isolated building no parking lot details, city builder service building, PBR game-ready
```

**Negative prompt:**
```
gothic hospital, ruined building, people, ambulances, helicopters, interior, terrain, trees, sky, text signage, cartoon, futuristic glass tower, blurry, red cross on every surface
```

**Meshy `target_polycount`:** `5000`

---

### 7. Modern Glass Civic Tower

| Field | Value |
|-------|-------|
| **File** | `hero_modern_glass_tower.glb` |
| **Era** | `modern` |
| **Footprint** | **2×2 tiles** (tall — ~12–16 story proportion) |
| **Poly budget** | 4,500 tris |
| **Texture** | 1024×1024 |

**Meshy prompt:**
```
Mid-fidelity game asset, 1980s modern glass office civic tower, blue reflective curtain wall, steel mullions, flat roof with mechanical bulkhead, square footprint skyscraper proportion, lobby glass at base, no surrounding plaza furniture, isolated tower on flat ground, city builder landmark, PBR game-ready, readable window grid
```

**Negative prompt:**
```
art deco, brick, gothic, people, interior, terrain, trees, sky, helicopter pad, antenna clutter, cartoon, twisted parametric shape, blurry, logos, text, damaged building
```

**Meshy `target_polycount`:** `4500`

---

### 8. Future Eco Campus Tower

| Field | Value |
|-------|-------|
| **File** | `hero_future_eco_tower.glb` |
| **Era** | `future` |
| **Footprint** | **3×3 tiles** |
| **Poly budget** | 6,500 tris |
| **Texture** | 2048×2048 |

**Meshy prompt:**
```
Mid-fidelity game asset, 2040s sustainable eco civic tower, white curved facade with integrated vertical gardens, cyan solar glass panels, green roof terrace, soft organic geometry mixed with clean tech lines, holographic accent bands without readable text, isolated building on flat pad, city builder future era landmark, PBR game-ready
```

**Negative prompt:**
```
cyberpunk slum, dystopian ruin, people, flying cars, interior, terrain, trees, sky, readable text, logos, cartoon, low poly blob, brutalist bunker, excessive spikes, broken mesh
```

**Meshy `target_polycount`:** `6500`

---

## Meshy workflow (per asset)

1. **Preview** — `meshy_text_to_3d` with prompt, `mode: preview`, `target_polycount` from table, `should_remesh: true`.
2. **Review silhouette** — reject if unreadable at 3/4 isometric camera distance; re-roll with tightened prompt.
3. **Refine** — `meshy_refine_3d` on accepted preview task for PBR textures.
4. **Optional retexture** — `meshy_retexture` if palette drift; pass era color keywords from table above.
5. **Download GLB** → Blender cleanup (checklist below) → export to `web/public/assets/gltf/heroes/`.

---

## Blender cleanup export checklist

Complete **every** item before committing a hero GLB.

### Import & scale

- [ ] Import Meshy GLB; verify units (1 Blender unit = 1 game tile).
- [ ] Set footprint on ground plane: X/Z dimensions match tile table (2, 3, or 4 units per axis).
- [ ] Raise/lowest vertex sits at **Y = 0** (ground plane).
- [ ] Origin to geometry: ground-center of footprint `(W/2, 0, D/2)`.
- [ ] Apply **Ctrl+A** → All Transforms (location, rotation, scale).

### Mesh hygiene

- [ ] Merge by distance (threshold ≤ 0.001) on coincident verts.
- [ ] Delete loose vertices/edges/faces.
- [ ] Remove internal faces (select non-manifold, fix holes).
- [ ] Triangulate (export will triangulate anyway — control ngons now).
- [ ] Confirm final tri count within budget ±10%.
- [ ] If over budget: Decimate (planar) or `meshy_remesh` before re-export.

### Materials & textures

- [ ] Principled BSDF → glTF metallic-roughness (no spec-gloss unless converted).
- [ ] Texture maps: Base Color, Metallic, Roughness, Normal (AO optional baked into roughness).
- [ ] Resize textures per asset table (1024 or 2048 max dimension).
- [ ] Power-of-two dimensions; PNG for base color (alpha only if needed).
- [ ] Name materials: `mat_{era}_{id}` e.g. `mat_frontier_city_hall`.
- [ ] Strip embedded lights, cameras, armatures (static prop).

### glTF export settings (Blender I/O)

- [ ] Format: **glTF Binary (.glb)**
- [ ] Include: Selected Objects (hero root empty + mesh) or unified mesh
- [ ] Transform: +Y Up (default)
- [ ] Geometry: Apply Modifiers, UVs, Normals, Tangents
- [ ] Compression: Draco optional (test load in R3F first)
- [ ] No animation clips (static landmark)

### Engine QA (web client)

- [ ] Place in `web/public/assets/gltf/heroes/` with canonical filename.
- [ ] Load in R3F dev scene; verify footprint alignment on 1×1 tile grid.
- [ ] Check silhouette at L1/L2 camera distances (`LOD_THRESHOLDS` in `constants.ts`).
- [ ] Night pass: emissive windows where appropriate (material tweak in Three.js, not separate mesh).
- [ ] File size target: **< 2 MB** per hero (textures dominate — compress PNG).

---

## Summary table

| # | Filename | Era | Footprint | Tris | Texture |
|---|----------|-----|-----------|------|---------|
| 1 | `hero_frontier_city_hall.glb` | frontier | 2×2 | 3,500 | 1024 |
| 2 | `hero_frontier_church.glb` | frontier | 2×2 | 3,000 | 1024 |
| 3 | `hero_industrial_steel_mill.glb` | industrial | 4×4 | 7,500 | 2048 |
| 4 | `hero_industrial_train_station.glb` | industrial | 3×3 | 6,000 | 2048 |
| 5 | `hero_postwar_civic_hall.glb` | postwar | 3×3 | 5,500 | 1024 |
| 6 | `hero_postwar_hospital.glb` | postwar | 3×3 | 5,000 | 1024 |
| 7 | `hero_modern_glass_tower.glb` | modern | 2×2 | 4,500 | 1024 |
| 8 | `hero_future_eco_tower.glb` | future | 3×3 | 6,500 | 2048 |

**Total pilot budget:** ~41,500 tris across 8 assets (avg ~5.2k).

---

## Next steps

- Wire hero `TypeId` / service mapping in `web/packages/sim-types` (separate task).
- Add `web/public/assets/gltf/heroes/.gitkeep` when first GLB lands.
- Expand to 8–12 heroes **per era** once pilot QA passes (see `AI_ART_PIPELINE.md`).
