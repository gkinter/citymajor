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

## Batch generation (Meshy API)

The repo ships a batch script that reads `scripts/meshy/manifest.json`, runs the Meshy **preview → refine** workflow per job, downloads GLB, and post-processes each file so the pivot sits at **ground center** (bottom of footprint on Y=0, X/Z centered).

### Prerequisites

- Node ≥ 20
- `pnpm install` at repo root (`@gltf-transform/core` is a root devDependency for the script)
- Meshy API key in env — **never commit keys**. Use a local env file or shell export:

```bash
export MESHY_API_KEY=msk_…   # Pro tier+; ~30 credits/asset (20 preview + 10 refine on meshy-6)
```

### Dry-run (no API key)

Plans all manifest jobs without spending credits. This is the default when `MESHY_API_KEY` is unset:

```bash
pnpm meshy:batch:dry
# or
node scripts/meshy/batch-generate.mjs --dry-run
```

Useful flags:

| Flag | Purpose |
|------|---------|
| `--only=<key>` | Single archetype, e.g. `res_low_frontier_00` |
| `--skip-existing` | Skip jobs whose output GLB already exists |
| `--limit=<n>` | Process first *n* jobs only |
| `--preview-only` | Preview mesh only (no refine credits) |
| `--poll-ms=<ms>` | Task poll interval (default 5000) |

### Live generation

```bash
MESHY_API_KEY=msk_… pnpm meshy:batch
```

Outputs land at `web/public/assets/gltf/{era}/{archetype_key}.glb`, matching the on-disk layout below.

### Manifest

`scripts/meshy/manifest.json` lists spike-batch jobs (`key`, `prompt`, `category`, `era`) aligned to prompts in [design/MESHY_ASSET_PIPELINE.md](./design/MESHY_ASSET_PIPELINE.md). Extend this file as the full ~500-mesh catalog is filled in.

### Post-processing

Meshy exports center the bounding box. The script wraps scene geometry in a translation node so:

- lowest vertex → **Y = 0**
- footprint center → **X = 0, Z = 0**

Implemented with `@gltf-transform/core` in `scripts/meshy/batch-generate.mjs`.

**Optional Blender polish:** After Meshy, use [Blender MCP](./BLENDER_MCP_SETUP.md) to decimate, fix pivot/scale, and re-export. Helper: `scripts/blender/citymajor_export_conventions.py`.

---

## Replacing with Meshy output

1. **Generate** via `pnpm meshy:batch` (or manually in Meshy UI) using prompts aligned to category roof archetype (`peaked`, `flat_lip`, `sawtooth`, `flat_accent`) and era material hints in `web/packages/sim-types/src/buildingArchetypes.ts`.
2. **Export** GLB (draco optional; keep under ~500 KB per module for instancing budgets).
3. **Name** file exactly `{archetype_key}.glb` and place under the correct `{era}/` folder.
4. **Update** `web/lib/gltf-catalog.ts` if adding keys beyond the five spike placeholders.
5. **Verify** scale: unit footprint ≈ 1 tile; origin at ground center; Y-up.
6. **Run** `pnpm build` in `web/` — static assets must resolve at `/assets/gltf/...`.

Do **not** rename archetype keys when swapping art — rendering applies state (constructing, abandoned) and night lighting as material modifiers, not separate mesh assets.

---

## Git LFS (Meshy GLBs)

Meshy refine outputs are **~8–12 MB each**; a full v1 spike batch (20 keys) is **~140 MB** on disk — above the ~50 MB practical git commit threshold. **Do not commit raw GLBs to git** without LFS.

```bash
# One-time repo setup (when ready to version art in git)
git lfs install
git lfs track "web/public/assets/gltf/**/*.glb"
git add .gitattributes
```

Until LFS is enabled, keep generated GLBs **local or on CDN** (see SB-3682). Commit only `scripts/meshy/batch-generate.mjs`, manifests, and catalog wiring. Regenerate on CI/preview hosts via `MESHY_USE_MCP=1 pnpm meshy:batch:mcp -- --skip-existing` or direct `MESHY_API_KEY` + `pnpm meshy:batch`.

**Existing raw blobs:** seven v1-core Meshy GLBs (~70 MB) are already in git history as plain blobs. Rewriting them requires **`git lfs migrate`** (approval required) — full runbook in [`design/MESHY_ASSET_CATALOG.md`](./design/MESHY_ASSET_CATALOG.md) § `git lfs migrate`. Coolify notes: [`DEPLOY_WEB.md`](./DEPLOY_WEB.md) § Git LFS and preview builds.

---

## Future integration

- `BuildingInstances` (or a dedicated `GltfBuildingInstances`) will `useGLTF(resolveGltfPath(key))` with fallback to procedural boxes when a path is missing.
- Expand `scripts/meshy/manifest.json` for the full 500-mesh catalog; batch script already supports `--limit` / `--skip-existing` for incremental runs.
- Shared constants may move to JSON/WASM to avoid dual maintenance with `BuildingRenderer.cs`.
