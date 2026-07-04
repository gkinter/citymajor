# ADR: Building Archetype Taxonomy for 3D Meshes

**Status:** Accepted  
**Linear:** SB-3677  
**Date:** 2026-07-04  
**Source of truth (2D):** `src/Forge.Engine/Rendering/BuildingRenderer.cs`

---

## Context

CityMajor is pivoting from procedural 2D isometric quads to instanced 3D building meshes in the web client (`web/`). The simulation already assigns every building a `TypeId` (`ushort`) and `State` (`byte`). The 2D renderer derives category, era, story count, roof style, and material palette from `TypeId` alone.

This ADR defines the **mesh archetype key** convention and documents the TypeId → archetype mapping so art pipeline, R3F renderer, and WASM sim share one contract. **No GLTF assets are specified here** — only taxonomy, keys, and material hints.

---

## Decision

### 1. TypeId category ranges

| Range       | Category            | Key prefix |
|-------------|---------------------|------------|
| 100–199     | Residential low     | `res_low`  |
| 200–299     | Residential high    | `res_high` |
| 300–399     | Commercial          | `com`      |
| 400–499     | Industrial          | `ind`      |
| *other*     | Service / civic     | `svc`      |

### 2. Era bands (within each 100-ID category block)

Each category divides its 100 IDs into five bands of 20:

| Offset in block | Era index | Era slug     | Historical label   |
|-----------------|-----------|--------------|--------------------|
| 0–19            | 0         | `frontier`   | Frontier / Colonial |
| 20–39           | 1         | `industrial` | Industrial age      |
| 40–59           | 2         | `postwar`    | Postwar             |
| 60–79           | 3         | `modern`     | Modern              |
| 80–99           | 4         | `future`     | Future              |

**Formula:** `eraIndex = clamp(floor((typeId - categoryBase) / 20), 0, 4)`

**Service exception:** TypeIds outside 100–499 have no era band; they default to `modern` (era index 3).

### 3. Building state (from `BuildingData.State`)

| Value | Enum slug       | 3D treatment                                      |
|-------|-----------------|---------------------------------------------------|
| 0     | `constructing`  | Scaffolding overlay; partial height; no windows   |
| 1     | `operational`   | Full mesh; windows; rooftop details               |
| 2     | `abandoned`     | Desaturated materials; broken windows; uneven roof |
| 3     | `demolishing`   | Skip render (matches 2D)                          |

Construction progress also scales with `Condition` when `< 50` even if state is operational (see 2D `ConstructionConditionThreshold`).

### 4. Archetype key format

```
{categoryPrefix}_{eraSlug}_{variant}
```

- **categoryPrefix:** `res_low` | `res_high` | `com` | `ind` | `svc`
- **eraSlug:** `frontier` | `industrial` | `postwar` | `modern` | `future`
- **variant:** two-digit string `00`–`19` — index within the era band (`(typeId - categoryBase) % 20`)

**Examples:**

| TypeId | Key                      |
|--------|--------------------------|
| 105    | `res_low_frontier_05`    |
| 128    | `res_low_industrial_08`  |
| 245    | `res_high_postwar_05`    |
| 363    | `com_modern_03`          |
| 487    | `ind_future_07`          |
| 12     | `svc_modern_12`          |

State is **not** encoded in the mesh key; apply as material modifiers and attachment overlays at render time.

### 5. Roof / silhouette archetype (per category)

Maps to GLTF sub-mesh or modular roof piece selection:

| Category     | Roof archetype   | Notes                                      |
|--------------|------------------|--------------------------------------------|
| `res_low`    | `peaked`         | Gable / pitched residential                |
| `res_high`   | `flat_lip`       | Parapet lip; water tank at 4+ stories      |
| `com`        | `flat_lip`       | Awning at ground door; roof HVAC blob      |
| `ind`        | `sawtooth`       | Zigzag clerestory; chimney                 |
| `svc`        | `flat_accent`    | Light base + colored accent stripe         |

### 6. Story count (`ComputeStories`)

Base stories by category, then level scaling (clamped 1–12):

| Category  | Base | Level scaling                                      |
|-----------|------|----------------------------------------------------|
| res_low   | 1    | `+ min(level - 1, 2)`                              |
| res_high  | 3    | `+ (level - 1) * 2`                                |
| com       | 2    | `+ min((level - 1) * 2, 8)`                       |
| ind       | 1    | `+ min(level - 1, 2)`                              |
| svc       | 2    | `+ min(level - 1, 1)`                              |

3D meshes should be authored at **unit story height** and stacked/scaled by `stories * PIXELS_PER_STORY` equivalent in world units.

### 7. Material hints

Era palette hex pairs (wall base lerp endpoints) are ported to TypeScript as `ERA_MATERIAL_HINTS` in `web/packages/sim-types/src/buildingArchetypes.ts`. Procedural materials lerp between `wallA` and `wallB` using `variation = (typeId % 7) / 7`.

Service buildings use accent colors from `typeId % 5` (fire, police, hospital, school, civic).

### 8. Implementation locations

| Artifact              | Path                                                |
|-----------------------|-----------------------------------------------------|
| TypeScript contract   | `web/packages/sim-types/src/buildingArchetypes.ts`  |
| Package entry         | `web/packages/sim-types/src/index.ts`               |
| This ADR              | `docs/design/BUILDING_ARCHETYPE_3D.md`              |

C# `BuildingRenderer` remains authoritative until a shared constants crate is extracted; web types must stay in sync with that file.

---

## Consequences

- Art pipeline names GLTF files after archetype keys (e.g. `res_low_frontier_05.glb`).
- One mesh per `(category, era, variant)` tuple = up to **5 × 5 × 20 = 500** zone meshes + service variants.
- State and night lighting are renderer concerns, not separate mesh assets.
- Future work: extract shared constants to a JSON or Rust/WASM module to avoid dual maintenance.

---

## Sample TypeId reference

| TypeId | Category | Era        | Variant | Archetype key              | Roof        |
|--------|----------|------------|---------|----------------------------|-------------|
| 100    | res_low  | frontier   | 00      | `res_low_frontier_00`      | peaked      |
| 115    | res_low  | frontier   | 15      | `res_low_frontier_15`      | peaked      |
| 137    | res_low  | industrial | 17      | `res_low_industrial_17`    | peaked      |
| 220    | res_high | industrial | 20→00   | `res_high_industrial_00`   | flat_lip    |
| 252    | res_high | postwar    | 02      | `res_high_postwar_02`      | flat_lip    |
| 310    | com      | frontier   | 10      | `com_frontier_10`          | flat_lip    |
| 378    | com      | modern     | 18      | `com_modern_18`            | flat_lip    |
| 441    | ind      | postwar    | 01      | `ind_postwar_01`           | sawtooth    |
| 499    | ind      | future     | 19      | `ind_future_19`            | sawtooth    |
| 7      | svc      | modern*    | 07      | `svc_modern_07`            | flat_accent |

\*Service buildings always use era `modern` regardless of TypeId.
