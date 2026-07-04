# Meshy asset pipeline (CityMajor 3D buildings)

**Status:** Placeholder phase  
**Related ADR:** [BUILDING_ARCHETYPE_3D.md](./design/BUILDING_ARCHETYPE_3D.md)  
**Catalog:** `web/lib/gltf-catalog.ts`

---

## Overview

CityMajor’s web client (`web/`) will eventually render instanced GLTF building modules keyed by sim-types archetype strings (e.g. `res_low_frontier_05`). Until Meshy-generated art is ready, the repo ships **procedural placeholder `.glb` files** so R3F loaders, path resolution, and CI can be wired without blocking on art.

---

## Placeholder assets (current)

| Archetype key              | Era folder   | File |
|----------------------------|--------------|------|
| `res_low_frontier_00`      | `frontier`   | `web/public/assets/gltf/frontier/res_low_frontier_00.glb` |
| `com_frontier_00`          | `frontier`   | `web/public/assets/gltf/frontier/com_frontier_00.glb` |
| `ind_industrial_00`        | `industrial` | `web/public/assets/gltf/industrial/ind_industrial_00.glb` |
| `res_high_industrial_00`   | `industrial` | `web/public/assets/gltf/industrial/res_high_industrial_00.glb` |
| `svc_modern_00`            | `modern`     | `web/public/assets/gltf/modern/svc_modern_00.glb` |

These are **low-poly primitives** (boxes, pyramids, sawtooth stubs) exported via `@gltf-transform/core`. They are **not** production art — replace in place when Meshy deliverables land.

### Regenerate placeholders

```bash
cd web
pnpm generate:gltf-placeholders
```

Script: `web/scripts/generate-placeholder-gltf.mjs`

---

## On-disk layout

```
web/public/assets/gltf/
  {era}/
    {archetype_key}.glb
```

- `{era}` ∈ `frontier` | `industrial` | `postwar` | `modern` | `future`
- `{archetype_key}` = `{category}_{era}_{variant}` per sim-types `archetypeKey()`

Full taxonomy target: up to **500** zone meshes (5 categories × 5 eras × 20 variants) plus service variants — placeholders cover one representative per category for the R3F spike.

---

## Replacing with Meshy output

1. **Generate** in Meshy using prompts aligned to category roof archetype (`peaked`, `flat_lip`, `sawtooth`, `flat_accent`) and era material hints in `web/packages/sim-types/src/buildingArchetypes.ts`.
2. **Export** GLB (draco optional; keep under ~500 KB per module for instancing budgets).
3. **Name** file exactly `{archetype_key}.glb` and place under the correct `{era}/` folder.
4. **Update** `web/lib/gltf-catalog.ts` if adding keys beyond the five spike placeholders.
5. **Verify** scale: unit footprint ≈ 1 tile; origin at ground center; Y-up.
6. **Run** `pnpm build` in `web/` — static assets must resolve at `/assets/gltf/...`.

Do **not** rename archetype keys when swapping art — rendering applies state (constructing, abandoned) and night lighting as material modifiers, not separate mesh assets.

---

## Future integration

- `BuildingInstances` (or a dedicated `GltfBuildingInstances`) will `useGLTF(resolveGltfPath(key))` with fallback to procedural boxes when a path is missing.
- Consider a manifest JSON generated from Meshy batch exports for the full 500-mesh catalog.
- Shared constants may move to JSON/WASM to avoid dual maintenance with `BuildingRenderer.cs`.
