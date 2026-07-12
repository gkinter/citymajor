# Modern Era Content — Unity v1 Scope

**Status:** Locked for Unity port v1  
**Date:** 2026-07-12  
**Manifest:** [`base/data/tech/unity-modern-unlocks.json`](../../base/data/tech/unity-modern-unlocks.json)  
**Config:** [`src/Forge.SimCore/UnityModernConfig.cs`](../../src/Forge.SimCore/UnityModernConfig.cs)

---

## 1. Charter

CityMajor **Unity v1** is a **modern-era-only** city builder. The player starts in **present day (2026)** with contemporary zoning, economy, services, and research — not a Frontier→Industrial progression arc.

| Parameter | Unity v1 value |
|-----------|----------------|
| Default era | `modern` (index **3**) |
| Starting year | **2026** |
| Allowed era tags | `modern` only |
| World size | 256×256 (inherits `WasmConfig.DefaultWorldSize`) |
| Deferred eras | `frontier`, `industrial`, `postwar`, `future` |

Web v1 (`WEB_V1_SCOPE.md`) ships Frontier→Industrial; Unity v1 intentionally diverges to reduce art/sim scope for the first native client.

---

## 2. TypeId ranges (modern band)

Authoritative taxonomy: [`BUILDING_ARCHETYPE_3D.md`](./BUILDING_ARCHETYPE_3D.md), TypeScript: `web/packages/sim-types/src/buildingArchetypes.ts`.

Within each 100-ID category block, era index 3 (`modern`) occupies offsets **60–79**:

| Category | TypeId range | Archetype prefix | Variant slots |
|----------|--------------|------------------|---------------|
| Residential low | **160–179** | `res_low_modern_XX` | 20 |
| Residential high | **260–279** | `res_high_modern_XX` | 20 |
| Commercial | **360–379** | `com_modern_XX` | 20 |
| Industrial | **460–479** | `ind_modern_XX` | 20 |
| Service (sim band) | **500–599** | `svc_modern_XX` | 20 visual keys |
| Service (JSON modern slugs) | **560–579** | `svc_modern_XX` | 8 entries in `buildings.json` |

**Service exception:** All service TypeIds render as `svc_modern_{typeId % 20}` regardless of the era encoded in the TypeId (see `deriveEra()` in `buildingArchetypes.ts`).

**Formula (zone categories):**

```
typeId = categoryBase + eraIndex * 20 + variantIndex
eraIndex = 3  →  modern band
```

---

## 3. GLTF assets (`web/public/assets/gltf/modern/`)

### 3.1 Shipped zone representatives (catalog)

These keys are registered in `web/lib/gltf-catalog.ts` under the `modern/` folder:

| Archetype key | GLTF path |
|---------------|-----------|
| `res_low_modern_00` | `/assets/gltf/modern/res_low_modern_00.glb` |
| `res_high_modern_00` | `/assets/gltf/modern/res_high_modern_00.glb` |
| `com_modern_00` | `/assets/gltf/modern/com_modern_00.glb` |
| `ind_modern_00` | `/assets/gltf/modern/ind_modern_00.glb` |
| `svc_modern_00` … `svc_modern_07` | `/assets/gltf/modern/svc_modern_{00–07}.glb` |

Variants without a dedicated GLB reuse era representatives via `resolveCatalogKey()` (e.g. `com_modern_03` → nearest shipped `com_modern_00`).

### 3.2 Documented but not yet on disk

The DATA_BRIDGE mapping table defines paths for all modern zone variants (`com_modern_01` … `com_modern_10`, `res_low_modern_01` … `res_low_modern_08`, etc.). Unity v1 should treat catalog entries as the preload set; additional variants resolve on demand.

### 3.3 Hero / infrastructure / special (modern)

16 `buildings.json` entries at `era: 3` with `category: infrastructure | special` use **per-slug hero GLTFs**:

```
/assets/gltf/heroes/{slug}.glb
```

Examples:

- `inf_modern_solar_farm.glb`, `inf_modern_wind_farm.glb`, `inf_modern_nuclear_plant.glb`
- `spc_modern_sports_stadium.glb`, `spc_modern_theme_park.glb`, `spc_modern_tech_incubator.glb`

Registered hero landmark: `hero_modern_glass_tower` → `/assets/gltf/heroes/hero_modern_glass_tower.glb`

---

## 4. Content filtered from source JSON

### 4.1 Technologies (`base/data/tech/technologies.json`)

| Set | Count |
|-----|-------|
| Total in source | **156** |
| **Modern (v1 playable)** | **38** |
| Deferred (frontier/industrial/postwar/future) | **118** |

Modern tech IDs: `T011`, `T012`, `T023`, `T024`, `T025`, `T034`, `T035`, `T043`, `T044`, `T052`, `T053`, `T061`, `T062`, `T063`, `T072`, `T073`, `T077`, `T080`, `T082`, `T092`, `T093`, `T095b`, `T095`, `T097`, `T098`, `T107`, `T108`, `T109`, `T117`, `T118`, `T123`, `T131`, `T132`, `T137`, `T138`, `T143`, `T148`, `T153`.

Categories represented: transport, energy, construction, communication, agriculture, industrial, medical, environmental, social, computing, water_sanitation, urban_design, food_cuisine, entertainment, finance, tourism, military, mining.

**Research subset note:** Modern techs retain prerequisites from earlier eras (e.g. `T011` requires `T010`, `T031`). Unity v1 should either **pre-unlock prerequisite infra** at city start or **stub prerequisite checks** so the modern tree is reachable without simulating prior eras.

### 4.2 Buildings (`base/data/buildings/buildings.json`)

| Set | Count |
|-----|-------|
| Total in source | **199** |
| **Modern era (`era: 3`)** | **55** |
| Zone buildings (res/com/ind/svc) | **39** |
| Infrastructure + special (hero GLTF) | **16** |

Zone breakdown (modern):

| Category | JSON entries |
|----------|--------------|
| Residential | 12 |
| Commercial | 11 |
| Industrial | 8 |
| Service | 8 |

Full slug → TypeId → archetype mapping: [`DATA_BRIDGE.md`](./DATA_BRIDGE.md) §5.1 (modern rows).

---

## 5. WasmSimHost systems — Unity v1 applicability

Source: `src/Forge.SimWasm/WasmSimHost.cs` (thin host over `Forge.SimCore` / `Forge.Game.Simulation`).

### 5.1 Include in Unity v1

| System | Tick tier | Role |
|--------|-----------|------|
| **EconomySystem** | Daily + Monthly | RCI demand, Leontief market zones, income/expenses |
| **ZoneGrowthSystem** | Daily + Monthly | Zone growth, land value, upgrades — **must pin TypeIds to modern band** |
| **PopulationSystem** | Per-tick stagger + Monthly | Households, satisfaction, migration |
| **ServiceSystem** | Daily + Monthly | Health/police/fire/education coverage maps |
| **WasmTrafficLite** | 5s lite + 0.5s edge batch | 64-zone Frank-Wolfe BPR-lite traffic |
| **BudgetSystem** | Monthly | Treasury, tax rates |
| **PoliticsSystem** | Daily + Monthly | Approval rating |
| **ResearchSystem** | Monthly | RP generation, tech queue — **filter to modern tech catalog** |
| **EventSystem** | Daily | Random events (filter event defs for modern context) |
| **TradeSystem** | Monthly | Global import/export |
| **LawSystem** | On demand | Ordinances toggle |
| **CulturalDNASystem** | Yearly | Cultural preset drift |

### 5.2 Configure for modern-only

| Concern | Unity v1 behavior |
|---------|-------------------|
| `WorldState.Era` | Initialize to **3** (Modern); disable `CheckEraTransition` forward to Future |
| `ResearchSystem.EraRequirements` | Cap at Modern; no Future unlock |
| `ZoneGrowthSystem` spawn TypeIds | Replace frontier constants (100–402) with modern band (160–567) |
| Starter city seed | Modern residential/commercial/industrial/service mix |
| Year / calendar | Start at **2026** (`UnityModernConfig.StartingYear`) |

### 5.3 Defer / stub

| System | Reason |
|--------|--------|
| Era progression (Frontier→Industrial→…) | Out of v1 scope |
| Future-era tech (`T013`–`T015`, `T026`, `T036`, …) | Deferred |
| Full native Forge renderer | Unity replaces R3F |
| WASM JSON bridge | Unity reads sim in-process via `Forge.SimCore` |

---

## 6. Explicitly deferred eras

| Era | Index | Status |
|-----|-------|--------|
| **Frontier** | 0 | Deferred — no playable content, no era transition |
| **Industrial** | 1 | Deferred |
| **Postwar** | 2 | Deferred |
| **Future** | 4 | Deferred — no maglev, fusion, arcology, space tourism |

Deferred content remains in source JSON for shared data pipeline; `UnityModernConfig.FilterTechnologiesJson()` / `FilterBuildingsJson()` strip non-modern entries at load time.

---

## 7. Implementation index

| Artifact | Path |
|----------|------|
| Modern unlock manifest | `base/data/tech/unity-modern-unlocks.json` |
| Era filter config | `src/Forge.SimCore/UnityModernConfig.cs` |
| Archetype taxonomy | `web/packages/sim-types/src/buildingArchetypes.ts` |
| GLTF catalog | `web/lib/gltf-catalog.ts` |
| TypeId bridge rules | `web/lib/building-type-id-map.ts` |
| WASM sizing / traffic intervals | `src/Forge.SimWasm/WasmConfig.cs` (unchanged) |
| Sim host reference | `src/Forge.SimWasm/WasmSimHost.cs` |

---

## 8. Filter summary (quick reference)

```
technologies.json   156 total  →  38 modern (24%)
buildings.json      199 total  →  55 era=3 (28%)
  zone playable       39
  hero infra/special  16
TypeId modern bands   160-179, 260-279, 360-379, 460-479, svc 560-579
GLTF folder           web/public/assets/gltf/modern/  (12 shipped keys)
Deferred eras         frontier, industrial, postwar, future
```
