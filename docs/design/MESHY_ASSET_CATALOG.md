# CityMajor — Meshy Asset Catalog

**Status:** Canonical  
**Date:** 2026-07-04  
**Linear epic:** [SB-3730](https://linear.app/softblaze/issue/SB-3730) · **Spec:** [SB-3678](https://linear.app/softblaze/issue/SB-3678)  
**Related:** [`BUILDING_ARCHETYPE_3D.md`](BUILDING_ARCHETYPE_3D.md), [`MESHY_ASSET_PIPELINE.md`](MESHY_ASSET_PIPELINE.md), [`MESHY_HERO_LANDMARKS.md`](MESHY_HERO_LANDMARKS.md)

---

## Taxonomy overview

Per `BUILDING_ARCHETYPE_3D.md`, zone building archetype keys follow:

```
{category}_{era}_{variant}.glb
```

| Dimension | Values | Count |
|-----------|--------|-------|
| **Categories** | `res_low`, `res_high`, `com`, `ind`, `svc` | 5 |
| **Eras** | `frontier`, `industrial`, `postwar`, `modern`, `future` | 5 |
| **Variants** | `00`–`19` per category-era band | 20 |
| **Zone mesh total** | 5 × 5 × 20 | **500 keys** |

**Meshy cost:** 30 credits/asset (20 preview + 10 refine) → **15,000 credits** for full zone set.

### v1 subsets

| Subset | Keys | Notes |
|--------|------|-------|
| **v1 playable** (Frontier + Industrial) | **200** | 5 cat × 2 era × 20 var |
| **v1 ship minimum** | **40** | 4 var × 5 cat × 2 era; procedural fallback for missing keys |
| **Full vision** | **500** + heroes + props | All eras + LOD |

---

## Version control & storage

Meshy refine GLBs are **~8–12 MB each** ([`MESHY_ASSET_PIPELINE.md`](MESHY_ASSET_PIPELINE.md) § Git LFS). Do **not** commit production Meshy outputs as plain git blobs.

| Asset class | Typical size | In git? | Mechanism |
|-------------|--------------|---------|-----------|
| **Procedural placeholders** | &lt; 50 KB | Yes | Plain git (`pnpm generate:gltf-placeholders`) |
| **Meshy zone / hero GLBs** | 8–12 MB | **Git LFS only** or CDN ([SB-3682](https://linear.app/softblaze/issue/SB-3682)) | LFS pointer, or regenerate on preview |
| **Manifests, scripts, catalog** | KB | Yes | Always commit |

```bash
git lfs install
git lfs track "web/public/assets/gltf/**/*.glb"
git add .gitattributes
```

**Spike inventory (v1-core, 20 keys):** 7 Meshy GLBs in history as raw blobs (needs `git lfs migrate` before scaling); 13 procedural placeholders. Do not add another Meshy batch to plain git — P0 (108 assets) would exceed **1 GB**.

### Raw-blob inventory (HEAD, `citymajor-web-r3f-spike`)

These seven files are committed as **plain git blobs** (~**70 MB** total in `.git/objects`). `.gitattributes` already sets `filter=lfs` for new adds, but history was written before tracking — **`git add` alone does not rewrite past commits**.

| Path | Size (approx.) | First Meshy commit |
|------|----------------|--------------------|
| `web/public/assets/gltf/frontier/res_low_frontier_00.glb` | 7.8 MB | `e4d3d4f` |
| `web/public/assets/gltf/frontier/res_low_frontier_01.glb` | 8.8 MB | `3c5a851` |
| `web/public/assets/gltf/frontier/ind_frontier_00.glb` | 8.8 MB | `f93b855` |
| `web/public/assets/gltf/frontier/com_frontier_00.glb` | 9.5 MB | `3c5a851` |
| `web/public/assets/gltf/industrial/res_low_industrial_00.glb` | 11 MB | `3076c29` |
| `web/public/assets/gltf/industrial/res_high_industrial_00.glb` | 11 MB | `86dd2ff` |
| `web/public/assets/gltf/industrial/ind_industrial_00.glb` | 10 MB | (v1-core batch) |

The remaining 13 v1-core keys are procedural placeholders (&lt; 5 KB each). **`com_industrial_00.glb`** (~10 MB) exists on disk but is **untracked** — add it only **after** LFS migrate (or it will become another raw blob).

### `git lfs migrate` — approval required

> **Do not run without explicit maintainer approval.** This **rewrites git history** on every ref included in the migrate. All open PRs, worktrees, and clones must be rebased or re-cloned afterward. Coordinate before force-pushing.

**Prerequisites**

1. [Git LFS](https://git-lfs.com/) installed locally (`git lfs version`).
2. GitHub **Git LFS** enabled on `gkinter/citymajor` (Settings → Git LFS). Budget ~70 MB storage + bandwidth per fresh clone until history is rewritten on all branches.
3. Clean working tree on the branch to migrate (stash or commit unrelated edits).
4. Announce freeze: no concurrent pushes to `feat/wasm-r3f-integration-2026-07-04` (or target branch) during migrate + force-push.
5. List active worktrees: `git worktree list` — each must reset after migrate.

**Pre-flight (read-only — safe to run anytime)**

```bash
cd /path/to/citymajor-web-r3f-spike   # or canonical clone
git lfs install

# Confirm .gitattributes tracks GLBs (expect filter=lfs)
git check-attr filter -- web/public/assets/gltf/frontier/res_low_frontier_00.glb

# Confirm HEAD still stores raw blobs (size >> 200 bytes; not an LFS pointer)
git cat-file -s HEAD:web/public/assets/gltf/frontier/res_low_frontier_00.glb
# ~8209608 today → raw blob. After migrate → ~130 (pointer).

# Estimate repo bloat from large GLBs in history
git rev-list --objects --all -- \
  web/public/assets/gltf/frontier/res_low_frontier_00.glb \
  web/public/assets/gltf/frontier/res_low_frontier_01.glb \
  web/public/assets/gltf/frontier/ind_frontier_00.glb \
  web/public/assets/gltf/frontier/com_frontier_00.glb \
  web/public/assets/gltf/industrial/res_low_industrial_00.glb \
  web/public/assets/gltf/industrial/res_high_industrial_00.glb \
  web/public/assets/gltf/industrial/ind_industrial_00.glb \
  | git cat-file --batch-check='%(objecttype) %(objectname) %(objectsize)' \
  | awk '$1=="blob" && $3>1000000 {sum+=$3; n++} END {printf "%d blobs, %.1f MB\n", n, sum/1024/1024}'
```

**Migrate (destructive — run only after approval)**

Migrate **only the seven Meshy files** (leave procedural &lt; 5 KB placeholders as plain git blobs):

```bash
BRANCH=feat/wasm-r3f-integration-2026-07-04

git lfs migrate import \
  --include="web/public/assets/gltf/frontier/res_low_frontier_00.glb,web/public/assets/gltf/frontier/res_low_frontier_01.glb,web/public/assets/gltf/frontier/ind_frontier_00.glb,web/public/assets/gltf/frontier/com_frontier_00.glb,web/public/assets/gltf/industrial/res_low_industrial_00.glb,web/public/assets/gltf/industrial/res_high_industrial_00.glb,web/public/assets/gltf/industrial/ind_industrial_00.glb" \
  --include-ref=refs/heads/$BRANCH
```

To rewrite **all branches/tags** that contain these blobs (only if approved for full-repo cleanup):

```bash
git lfs migrate import \
  --include="web/public/assets/gltf/frontier/res_low_frontier_00.glb,..." \
  --everything
```

**Post-migrate verification (before push)**

```bash
# Pointer files in HEAD (~130 bytes), real bytes on disk after checkout
git cat-file -s HEAD:web/public/assets/gltf/frontier/res_low_frontier_00.glb
head -1 web/public/assets/gltf/frontier/res_low_frontier_00.glb
# Expect: version https://git-lfs.github.com/spec/v1

git lfs ls-files | wc -l          # expect ≥ 7
file web/public/assets/gltf/frontier/res_low_frontier_00.glb
# Expect: glTF binary, not ASCII pointer

cd web && pnpm build              # GLBs must resolve in standalone output
```

**Push (after approval)**

```bash
git push --force-with-lease origin $BRANCH
```

Notify anyone with a clone/worktree:

```bash
git fetch origin
git reset --hard origin/$BRANCH
git lfs pull
# or: re-clone + git lfs install
```

**After migrate — new Meshy assets**

1. Ensure `git lfs install` once per clone.
2. `git add web/public/assets/gltf/.../*.glb` — Git LFS smudge runs automatically via `.gitattributes`.
3. Never commit Meshy refine outputs without LFS; run `git lfs ls-files` before push.

Coolify preview builds: see [`DEPLOY_WEB.md`](../DEPLOY_WEB.md) § Git LFS and preview builds.

---

## LOD strategy

| Band | Treatment | Status |
|------|-----------|--------|
| **L0** | Full Meshy GLB (pivot ground-center) | P0–P2 batches |
| **L1** | Decimated mesh (gltf-transform / Blender) | P3 batch [SB-3747](https://linear.app/softblaze/issue/SB-3747) |
| **L2** | Procedural box / simplified silhouette | In-engine today |
| **L3** | Heatmap block | In-engine today |

---

## Hero landmarks (pilot 8)

Separate from instanced zone meshes. Output: `web/public/assets/gltf/heroes/`

| # | File | Era | Footprint | Tris | Linear |
|---|------|-----|-----------|------|--------|
| 1 | `hero_frontier_city_hall.glb` | frontier | 2×2 | 3,500 | SB-3741 |
| 2 | `hero_frontier_church.glb` | frontier | 2×2 | 3,000 | SB-3741 |
| 3 | `hero_industrial_steel_mill.glb` | industrial | 4×4 | 7,500 | SB-3741 |
| 4 | `hero_industrial_train_station.glb` | industrial | 3×3 | 6,000 | SB-3741 |
| 5 | `hero_postwar_civic_hall.glb` | postwar | 3×3 | 5,500 | SB-3741 |
| 6 | `hero_postwar_hospital.glb` | postwar | 3×3 | 5,000 | SB-3741 |
| 7 | `hero_modern_glass_tower.glb` | modern | 2×2 | 4,500 | SB-3741 |
| 8 | `hero_future_eco_tower.glb` | future | 3×3 | 6,500 | SB-3741 |

**Era-gate monuments:** Placeholder monument at era transition (procedural today; hero swap at 100% era progress per `SB-3719`).

Full briefs: [`MESHY_HERO_LANDMARKS.md`](MESHY_HERO_LANDMARKS.md).

---

## Service civic (`svc_modern_00`–`19`)

Service TypeIds outside 100–499 default to `modern` era. Twenty civic variants cover fire, police, hospital, school, and generic civic footprints.

| Key range | Count | Batch |
|-----------|-------|-------|
| `svc_modern_00`–`19` | 20 | [SB-3745](https://linear.app/softblaze/issue/SB-3745) |

`svc_modern_00` also appears in v1 core sample batch for pipeline validation.

---

## Props (future — not Meshy zone batches)

| Category | Examples | Priority |
|----------|----------|----------|
| **Roads** | Dirt, cobblestone, asphalt segments | P2 |
| **Vehicles** | Horse cart, tram, car, truck | P2 |
| **Trees** | Street trees, park clusters | P2 |
| **Boats / trains** | Harbor, rail props (not hero stations) | P3 |
| **Scaffolding** | Construction overlay (`BuildingState.constructing`) | P1 |

Props use separate manifest namespace under `web/public/assets/gltf/props/` (TBD).

---

## Batch table

| Batch ID | Era(s) | Category | Variant range | Count | Credits (×30) | Manifest | Linear | Priority |
|----------|--------|----------|---------------|-------|---------------|----------|--------|----------|
| `v1-core` | all 5 (sample) | mixed | `00` | 20 | 600 | `scripts/meshy/manifest-batch-v1-core.json` | [SB-3739](https://linear.app/softblaze/issue/SB-3739) | **P0 v1** |
| `v1-frontier-industrial-80` | frontier, industrial | res_low, res_high, com, ind | `01`–`10` | 80 | 2,400 | `scripts/meshy/manifest-batch-v1-frontier-industrial.json` | [SB-3740](https://linear.app/softblaze/issue/SB-3740) | **P0 v1** |
| `heroes-pilot` | all 5 | hero | 8 assets | 8 | 240 | `MESHY_HERO_LANDMARKS.md` | [SB-3741](https://linear.app/softblaze/issue/SB-3741) | **P0 v1** |
| `v1-frontier-industrial-full` | frontier, industrial | all 5 | `05`–`19` | 150 | 4,500 | `scripts/meshy/manifest-batch-v1-frontier-industrial-full.json` | [SB-3742](https://linear.app/softblaze/issue/SB-3742) | **P1 v1.5** |
| `postwar` | postwar | all 5 | `00`–`19` | 100 | 3,000 | `scripts/meshy/manifest-batch-postwar.json` | [SB-3743](https://linear.app/softblaze/issue/SB-3743) | **P1 v1.5** |
| `modern` | modern | all 5 | `00`–`19` | 100 | 3,000 | `scripts/meshy/manifest-batch-modern.json` | [SB-3744](https://linear.app/softblaze/issue/SB-3744) | **P1 v1.5** |
| `svc-modern` | modern | svc | `00`–`19` | 20 | 600 | `scripts/meshy/manifest-batch-svc-modern.json` | [SB-3745](https://linear.app/softblaze/issue/SB-3745) | **P1 v1.5** |
| `future` | future | all 5 | `00`–`19` | 100 | 3,000 | `scripts/meshy/manifest-batch-future.json` | [SB-3746](https://linear.app/softblaze/issue/SB-3746) | **P2 v2** |
| `lod-l1` | all shipped | top 120 keys | L1 decimate | 120 | 0 (post-process) | n/a | [SB-3747](https://linear.app/softblaze/issue/SB-3747) | **P3 full** |

### Credit rollup

| Priority | Batches | Assets | Credits |
|----------|---------|--------|---------|
| **P0 v1 ship** | core + fi-80 + heroes | 108 | **3,240** |
| **P1 v1.5** | fi-full + postwar + modern + svc | 370 | **11,100** |
| **P2 v2** | future | 100 | **3,000** |
| **P3** | LOD | 120 | 0 |
| **Full zone set** | all zone batches | 500 | **15,000** |

*v1 ship minimum (40 zone keys):* core batch variant-`00` samples (20) + fi-80 batch variants `01`–`03` subset (20) = 40 with procedural fallback for gaps.

---

## Output paths

| Asset type | Path |
|------------|------|
| Zone meshes | `web/public/assets/gltf/{era}/{key}.glb` |
| Hero landmarks | `web/public/assets/gltf/heroes/{filename}.glb` |
| Catalog registry | `web/lib/gltf-catalog.ts` → `SHIPPED_GLTF_KEYS` |

Post-process: ground-center pivot via `gltf-transform` in `scripts/meshy/batch-generate.mjs`.

---

## Regenerating manifests

```bash
node scripts/meshy/generate-manifest-batches.mjs
node scripts/meshy/batch-generate.mjs --dry-run   # plan only
MESHY_API_KEY=msk_… node scripts/meshy/batch-generate.mjs --only=res_low_frontier_00
```

Validate alignment:

```bash
node scripts/meshy/validate-manifest.mjs --no-disk
```

---

## Canonical roadmap cross-links

- **Linear initiative:** [CityMajor — Full Game Roadmap](https://linear.app/softblaze/initiative/citymajor-full-game-roadmap-675d3d25289b)
- **Canonical doc:** [CityMajor — Canonical Roadmap (v1 → Full Vision)](https://linear.app/softblaze/document/citymajor-canonical-roadmap-v1-full-vision-eb7277adafd0)
- **Master tracker:** [SB-3708](https://linear.app/softblaze/issue/SB-3708)
