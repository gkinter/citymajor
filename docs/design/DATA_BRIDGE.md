# Data Bridge — `buildings.json` ↔ TypeId ↔ GLTF

**Date:** 2026-07-04  
**Status:** Current (proposed mapping — loader not yet implemented)  
**Closes:** [GAP_AUDIT_DESIGN_DOCS](./GAP_AUDIT_DESIGN_DOCS.md) fix #10  
**Related:** [BUILDING_ARCHETYPE_3D](./BUILDING_ARCHETYPE_3D.md), [MESHY_ASSET_PIPELINE](./MESHY_ASSET_PIPELINE.md), [WEB_V1_SCOPE](./WEB_V1_SCOPE.md)

---

## 1. Purpose

CityMajor carries building content in two parallel ID systems:

| Layer | ID form | Example | Used by |
|-------|---------|---------|---------|
| **Content DB** | string slug | `res_frontier_cabin` | `base/data/buildings/buildings.json`, UI, tech unlocks, events |
| **Simulation pool** | `ushort` TypeId | `105` | `BuildingData.TypeId`, WASM snapshot, zone growth |
| **3D instancing** | archetype key | `res_low_frontier_05` | `sim-types`, R3F `InstancedMesh`, Meshy manifest |
| **Asset file** | public GLTF path | `/assets/gltf/frontier/res_low_frontier_05.glb` | `web/lib/gltf-catalog.ts`, CDN |

This document is the **data-bridge spec** that connects those layers. TypeId → archetype key → GLTF path is **implemented** in `buildingArchetypes.ts` and `gltf-catalog.ts`. JSON string ID → TypeId is **specified here** but **not yet loaded at runtime** — the sim still uses hard-coded TypeIds in `ZoneGrowthSystem.cs`.

---

## 2. Pipeline

```
buildings.json (199 entries)
        │  proposed: slug + category + era + density → TypeId
        ▼
BuildingData.TypeId  (ushort, sim / WASM snapshot)
        │  classifyBuilding() + deriveEra() + deriveVariant()
        ▼
archetypeKey         e.g. res_low_frontier_05
        │  gltfPublicPath() / GLTF_CATALOG
        ▼
/assets/gltf/{era}/{key}.glb
```

**Runtime today (web spike):**

1. WASM / procedural city emits `typeId` per building slot.
2. `cityDataFromSnapshot()` calls `archetypeKey(typeId)` from `@citymajor/sim-types`.
3. `GltfBuildingBucket` resolves path via `resolveGltfPath(key)`; only **5 placeholder GLBs** ship in-repo.

---

## 3. TypeId taxonomy (implemented)

Authoritative ADR: [BUILDING_ARCHETYPE_3D](./BUILDING_ARCHETYPE_3D.md). TypeScript port: `web/packages/sim-types/src/buildingArchetypes.ts`.

### 3.1 Category ranges

| TypeId range | Category prefix | `classifyBuilding()` |
|--------------|-----------------|----------------------|
| 100–199 | `res_low` | Residential low |
| 200–299 | `res_high` | Residential high |
| 300–399 | `com` | Commercial |
| 400–499 | `ind` | Industrial |
| *other* | `svc` | Service / civic |

### 3.2 Era bands (within each 100-ID zone block)

| Offset in block | Era index | Era slug | TypeId example (`res_low`) |
|-----------------|-----------|----------|----------------------------|
| 0–19 | 0 | `frontier` | 100–119 |
| 20–39 | 1 | `industrial` | 120–139 |
| 40–59 | 2 | `postwar` | 140–159 |
| 60–79 | 3 | `modern` | 160–179 |
| 80–99 | 4 | `future` | 180–199 |

**Formulas** (zone categories only):

```
categoryBase = 100 | 200 | 300 | 400
eraIndex     = clamp(floor((typeId - categoryBase) / 20), 0, 4)
variant      = (typeId - categoryBase) % 20
archetypeKey = "{prefix}_{eraSlug}_{variant:02d}"
```

**Service exception:** TypeIds outside 100–499 use `svc_modern_{typeId % 20:02d}` — era is always `modern` in `deriveEra()`.

### 3.3 TypeId → archetype key → GLTF path

| TypeId | Archetype key | GLTF path |
|--------|---------------|-----------|
| 105 | `res_low_frontier_05` | `/assets/gltf/frontier/res_low_frontier_05.glb` |
| 128 | `res_low_industrial_08` | `/assets/gltf/industrial/res_low_industrial_08.glb` |
| 245 | `res_high_postwar_05` | `/assets/gltf/postwar/res_high_postwar_05.glb` |
| 363 | `com_modern_03` | `/assets/gltf/modern/com_modern_03.glb` |
| 487 | `ind_future_07` | `/assets/gltf/future/ind_future_07.glb` |
| 7 | `svc_modern_07` | `/assets/gltf/modern/svc_modern_07.glb` |

Path resolution: `web/lib/gltf-catalog.ts` — `gltfPublicPath(key)` → `/assets/gltf/{era}/{key}.glb` where `era` is parsed from the key (`res_low_frontier_05` → `frontier`).

---

## 4. `buildings.json` → TypeId (proposed)

**Source:** `base/data/buildings/buildings.json` — **199 entries** (audited 2026-07-04).

### 4.1 JSON schema (relevant fields)

| Field | Type | Bridge role |
|-------|------|-------------|
| `id` | string | Stable content slug (`res_frontier_cabin`) |
| `category` | string | `residential` \| `commercial` \| `industrial` \| `service` \| `infrastructure` \| `special` |
| `era` | 0–4 | Maps to era slug (`frontier` … `future`) |
| `density` | 0–2 | Residential: `≥2` → `res_high`; else `res_low` |
| `zone` | string | Placement zone hint (not used in TypeId math) |

### 4.2 Mapping rules (zone buildings: residential, commercial, industrial, service)

1. **Category prefix** from `category` + `density`:
   - `residential` + `density < 2` → `res_low` (base **100**)
   - `residential` + `density ≥ 2` → `res_high` (base **200**)
   - `commercial` → `com` (base **300**)
   - `industrial` → `ind` (base **400**)
   - `service` → `svc` (base **0** — see gaps §6)
2. **Era band** from `era` field: `typeId += era * 20`.
3. **Variant index** within `(prefix, era)`: sort JSON entries by `id` ascending; assign `00` … `19` in order.
4. **TypeId** = `categoryBase + era * 20 + variantIndex` (svc: `variantIndex` only, 0–19).

### 4.3 Hero / non-zone buildings (`infrastructure`, `special`)

**46 entries** — no TypeId in the 100–499 taxonomy. These map to **per-slug hero GLTFs** under `/assets/gltf/heroes/{id}.glb` per [MESHY_HERO_LANDMARKS](./MESHY_HERO_LANDMARKS.md). Placed via `PlaceBuildingCommand` with a dedicated TypeId or service flag (TBD).

### 4.4 Band occupancy (JSON vs 20-variant cap)

| Era | Category | JSON count | Cap | Status |
|---|---|---|---|---|
| frontier | `res_low` | 8 | 20 | OK |
| frontier | `res_high` | 2 | 20 | OK |
| frontier | `com` | 7 | 20 | OK |
| frontier | `ind` | 5 | 20 | OK |
| frontier | `svc` | 6 | 20 | OK |
| industrial | `res_low` | 6 | 20 | OK |
| industrial | `res_high` | 4 | 20 | OK |
| industrial | `com` | 7 | 20 | OK |
| industrial | `ind` | 5 | 20 | OK |
| industrial | `svc` | 8 | 20 | OK |
| postwar | `res_low` | 7 | 20 | OK |
| postwar | `res_high` | 3 | 20 | OK |
| postwar | `com` | 6 | 20 | OK |
| postwar | `ind` | 5 | 20 | OK |
| postwar | `svc` | 8 | 20 | OK |
| modern | `res_low` | 9 | 20 | OK |
| modern | `res_high` | 3 | 20 | OK |
| modern | `com` | 11 | 20 | OK |
| modern | `ind` | 8 | 20 | OK |
| modern | `svc` | 8 | 20 | OK |
| future | `res_low` | 3 | 20 | OK |
| future | `res_high` | 4 | 20 | OK |
| future | `com` | 6 | 20 | OK |
| future | `ind` | 8 | 20 | OK |
| future | `svc` | 6 | 20 | OK |

All zone bands are within the 20-variant cap today. **283 of 400** possible zone archetype slots (4 categories × 5 eras × 20) have **no** JSON entry.

### 4.5 `ZoneGrowthSystem` hard-coded TypeIds (sim today)

Growth spawns only these TypeIds — all in **era 0 / frontier** band regardless of `WorldState.Era`:

| C# constant | TypeId | Archetype key | GLTF path |
|---|---|---|---|
| `ResLowSmallHouse` | 100 | `res_low_frontier_00` | `/assets/gltf/frontier/res_low_frontier_00.glb` |
| `ResLowMediumHouse` | 101 | `res_low_frontier_01` | `/assets/gltf/frontier/res_low_frontier_01.glb` |
| `ResLowLargeHouse` | 102 | `res_low_frontier_02` | `/assets/gltf/frontier/res_low_frontier_02.glb` |
| `ResLuxuryVilla` | 110 | `res_low_frontier_10` | `/assets/gltf/frontier/res_low_frontier_10.glb` |
| `ResHighApartmentSmall` | 200 | `res_high_frontier_00` | `/assets/gltf/frontier/res_high_frontier_00.glb` |
| `ResHighApartmentMedium` | 201 | `res_high_frontier_01` | `/assets/gltf/frontier/res_high_frontier_01.glb` |
| `ResHighApartmentLarge` | 202 | `res_high_frontier_02` | `/assets/gltf/frontier/res_high_frontier_02.glb` |
| `ResLuxuryTower` | 210 | `res_high_frontier_10` | `/assets/gltf/frontier/res_high_frontier_10.glb` |
| `ComSmallShop` | 300 | `com_frontier_00` | `/assets/gltf/frontier/com_frontier_00.glb` |
| `ComMediumStore` | 301 | `com_frontier_01` | `/assets/gltf/frontier/com_frontier_01.glb` |
| `ComLargeOffice` | 302 | `com_frontier_02` | `/assets/gltf/frontier/com_frontier_02.glb` |
| `ComUpscaleBoutique` | 310 | `com_frontier_10` | `/assets/gltf/frontier/com_frontier_10.glb` |
| `IndSmallWorkshop` | 400 | `ind_frontier_00` | `/assets/gltf/frontier/ind_frontier_00.glb` |
| `IndMediumFactory` | 401 | `ind_frontier_01` | `/assets/gltf/frontier/ind_frontier_01.glb` |
| `IndLargeFactory` | 402 | `ind_frontier_02` | `/assets/gltf/frontier/ind_frontier_02.glb` |

Density maps to variant offset 0/1/2 within frontier band. Luxury types (`110`, `210`, `310`) use variant `10` in frontier band.

---

## 5. Full mapping tables

### 5.1 Zone buildings (153 entries)

| JSON `id` | Name | TypeId | Archetype key | GLTF path |
|---|---|---|---|---|
| `com_frontier_assay_office` | Assay Office | 300 | `com_frontier_00` | `/assets/gltf/frontier/com_frontier_00.glb` |
| `com_frontier_bank` | Frontier Bank | 301 | `com_frontier_01` | `/assets/gltf/frontier/com_frontier_01.glb` |
| `com_frontier_general_store` | General Store | 302 | `com_frontier_02` | `/assets/gltf/frontier/com_frontier_02.glb` |
| `com_frontier_livery_stable` | Livery Stable | 303 | `com_frontier_03` | `/assets/gltf/frontier/com_frontier_03.glb` |
| `com_frontier_newspaper` | Newspaper Office | 304 | `com_frontier_04` | `/assets/gltf/frontier/com_frontier_04.glb` |
| `com_frontier_saloon` | Saloon | 305 | `com_frontier_05` | `/assets/gltf/frontier/com_frontier_05.glb` |
| `com_frontier_trading_post` | Trading Post | 306 | `com_frontier_06` | `/assets/gltf/frontier/com_frontier_06.glb` |
| `com_industrial_department_store` | Department Store | 320 | `com_industrial_00` | `/assets/gltf/industrial/com_industrial_00.glb` |
| `com_industrial_market_hall` | Market Hall | 321 | `com_industrial_01` | `/assets/gltf/industrial/com_industrial_01.glb` |
| `com_industrial_office_block` | Office Block | 322 | `com_industrial_02` | `/assets/gltf/industrial/com_industrial_02.glb` |
| `com_industrial_pharmacy` | Pharmacy | 323 | `com_industrial_03` | `/assets/gltf/industrial/com_industrial_03.glb` |
| `com_industrial_pub` | Public House | 324 | `com_industrial_04` | `/assets/gltf/industrial/com_industrial_04.glb` |
| `com_industrial_theater` | Theater | 325 | `com_industrial_05` | `/assets/gltf/industrial/com_industrial_05.glb` |
| `com_industrial_warehouse` | Commercial Warehouse | 326 | `com_industrial_06` | `/assets/gltf/industrial/com_industrial_06.glb` |
| `com_postwar_drive_in` | Drive-In Restaurant | 340 | `com_postwar_00` | `/assets/gltf/postwar/com_postwar_00.glb` |
| `com_postwar_motel` | Roadside Motel | 341 | `com_postwar_01` | `/assets/gltf/postwar/com_postwar_01.glb` |
| `com_postwar_office_tower` | Office Tower | 342 | `com_postwar_02` | `/assets/gltf/postwar/com_postwar_02.glb` |
| `com_postwar_shopping_center` | Shopping Center | 343 | `com_postwar_03` | `/assets/gltf/postwar/com_postwar_03.glb` |
| `com_postwar_strip_mall` | Strip Mall | 344 | `com_postwar_04` | `/assets/gltf/postwar/com_postwar_04.glb` |
| `com_postwar_supermarket` | Supermarket | 345 | `com_postwar_05` | `/assets/gltf/postwar/com_postwar_05.glb` |
| `com_modern_big_box_store` | Big Box Store | 360 | `com_modern_00` | `/assets/gltf/modern/com_modern_00.glb` |
| `com_modern_boutique_hotel` | Boutique Hotel | 361 | `com_modern_01` | `/assets/gltf/modern/com_modern_01.glb` |
| `com_modern_car_dealership` | Car Dealership | 362 | `com_modern_02` | `/assets/gltf/modern/com_modern_02.glb` |
| `com_modern_cinema_complex` | Multiplex Cinema | 363 | `com_modern_03` | `/assets/gltf/modern/com_modern_03.glb` |
| `com_modern_coffee_chain` | Coffee Chain | 364 | `com_modern_04` | `/assets/gltf/modern/com_modern_04.glb` |
| `com_modern_convention_center` | Convention Center | 365 | `com_modern_05` | `/assets/gltf/modern/com_modern_05.glb` |
| `com_modern_coworking` | Coworking Space | 366 | `com_modern_06` | `/assets/gltf/modern/com_modern_06.glb` |
| `com_modern_fast_food` | Fast Food Restaurant | 367 | `com_modern_07` | `/assets/gltf/modern/com_modern_07.glb` |
| `com_modern_fitness_center` | Fitness Center | 368 | `com_modern_08` | `/assets/gltf/modern/com_modern_08.glb` |
| `com_modern_shopping_mall` | Shopping Mall | 369 | `com_modern_09` | `/assets/gltf/modern/com_modern_09.glb` |
| `com_modern_tech_office` | Tech Office Campus | 370 | `com_modern_10` | `/assets/gltf/modern/com_modern_10.glb` |
| `com_future_ai_services` | AI Services Hub | 380 | `com_future_00` | `/assets/gltf/future/com_future_00.glb` |
| `com_future_drone_hub` | Drone Delivery Hub | 381 | `com_future_01` | `/assets/gltf/future/com_future_01.glb` |
| `com_future_experience_dome` | Experience Dome | 382 | `com_future_02` | `/assets/gltf/future/com_future_02.glb` |
| `com_future_holo_mall` | Holographic Mall | 383 | `com_future_03` | `/assets/gltf/future/com_future_03.glb` |
| `com_future_rooftop_agri` | Rooftop Agri-Market | 384 | `com_future_04` | `/assets/gltf/future/com_future_04.glb` |
| `com_future_vr_center` | VR Entertainment Center | 385 | `com_future_05` | `/assets/gltf/future/com_future_05.glb` |
| `ind_frontier_blacksmith` | Blacksmith | 400 | `ind_frontier_00` | `/assets/gltf/frontier/ind_frontier_00.glb` |
| `ind_frontier_grain_mill` | Grain Mill | 401 | `ind_frontier_01` | `/assets/gltf/frontier/ind_frontier_01.glb` |
| `ind_frontier_mine` | Mine Entrance | 402 | `ind_frontier_02` | `/assets/gltf/frontier/ind_frontier_02.glb` |
| `ind_frontier_sawmill` | Sawmill | 403 | `ind_frontier_03` | `/assets/gltf/frontier/ind_frontier_03.glb` |
| `ind_frontier_tannery` | Tannery | 404 | `ind_frontier_04` | `/assets/gltf/frontier/ind_frontier_04.glb` |
| `ind_industrial_brewery` | Brewery | 420 | `ind_industrial_00` | `/assets/gltf/industrial/ind_industrial_00.glb` |
| `ind_industrial_gasworks` | Gasworks | 421 | `ind_industrial_01` | `/assets/gltf/industrial/ind_industrial_01.glb` |
| `ind_industrial_rail_yard` | Rail Yard | 422 | `ind_industrial_02` | `/assets/gltf/industrial/ind_industrial_02.glb` |
| `ind_industrial_steel_works` | Steel Works | 423 | `ind_industrial_03` | `/assets/gltf/industrial/ind_industrial_03.glb` |
| `ind_industrial_textile_mill` | Textile Mill | 424 | `ind_industrial_04` | `/assets/gltf/industrial/ind_industrial_04.glb` |
| `ind_postwar_auto_factory` | Automobile Factory | 440 | `ind_postwar_00` | `/assets/gltf/postwar/ind_postwar_00.glb` |
| `ind_postwar_electronics` | Electronics Plant | 441 | `ind_postwar_01` | `/assets/gltf/postwar/ind_postwar_01.glb` |
| `ind_postwar_food_processing` | Food Processing Plant | 442 | `ind_postwar_02` | `/assets/gltf/postwar/ind_postwar_02.glb` |
| `ind_postwar_refinery` | Oil Refinery | 443 | `ind_postwar_03` | `/assets/gltf/postwar/ind_postwar_03.glb` |
| `ind_postwar_warehouse` | Industrial Warehouse | 444 | `ind_postwar_04` | `/assets/gltf/postwar/ind_postwar_04.glb` |
| `ind_modern_auto_plant` | Automobile Plant | 460 | `ind_modern_00` | `/assets/gltf/modern/ind_modern_00.glb` |
| `ind_modern_data_center` | Data Center | 461 | `ind_modern_01` | `/assets/gltf/modern/ind_modern_01.glb` |
| `ind_modern_electronics_factory` | Electronics Factory | 462 | `ind_modern_02` | `/assets/gltf/modern/ind_modern_02.glb` |
| `ind_modern_food_processing` | Food Processing Plant | 463 | `ind_modern_03` | `/assets/gltf/modern/ind_modern_03.glb` |
| `ind_modern_logistics_hub` | Logistics Hub | 464 | `ind_modern_04` | `/assets/gltf/modern/ind_modern_04.glb` |
| `ind_modern_pharma_lab` | Pharmaceutical Lab | 465 | `ind_modern_05` | `/assets/gltf/modern/ind_modern_05.glb` |
| `ind_modern_recycling_center` | Recycling Center | 466 | `ind_modern_06` | `/assets/gltf/modern/ind_modern_06.glb` |
| `ind_modern_solar_factory` | Solar Panel Factory | 467 | `ind_modern_07` | `/assets/gltf/modern/ind_modern_07.glb` |
| `ind_future_asteroid_mining_hq` | Asteroid Mining HQ | 480 | `ind_future_00` | `/assets/gltf/future/ind_future_00.glb` |
| `ind_future_auto_factory` | Automated Factory | 481 | `ind_future_01` | `/assets/gltf/future/ind_future_01.glb` |
| `ind_future_biotech_lab` | Biotech Research Lab | 482 | `ind_future_02` | `/assets/gltf/future/ind_future_02.glb` |
| `ind_future_carbon_capture` | Carbon Capture Plant | 483 | `ind_future_03` | `/assets/gltf/future/ind_future_03.glb` |
| `ind_future_fusion_plant` | Fusion Plant | 484 | `ind_future_04` | `/assets/gltf/future/ind_future_04.glb` |
| `ind_future_nanotech_lab` | Nanotechnology Lab | 485 | `ind_future_05` | `/assets/gltf/future/ind_future_05.glb` |
| `ind_future_quantum_center` | Quantum Computing Center | 486 | `ind_future_06` | `/assets/gltf/future/ind_future_06.glb` |
| `ind_future_vertical_farm` | Vertical Farm | 487 | `ind_future_07` | `/assets/gltf/future/ind_future_07.glb` |
| `res_frontier_hotel` | Frontier Hotel | 200 | `res_high_frontier_00` | `/assets/gltf/frontier/res_high_frontier_00.glb` |
| `res_frontier_saloon_rooms` | Saloon Rooms | 201 | `res_high_frontier_01` | `/assets/gltf/frontier/res_high_frontier_01.glb` |
| `res_industrial_apartment` | Industrial Apartment | 220 | `res_high_industrial_00` | `/assets/gltf/industrial/res_high_industrial_00.glb` |
| `res_industrial_boarding_house` | Company Boarding House | 221 | `res_high_industrial_01` | `/assets/gltf/industrial/res_high_industrial_01.glb` |
| `res_industrial_flophouse` | Flophouse | 222 | `res_high_industrial_02` | `/assets/gltf/industrial/res_high_industrial_02.glb` |
| `res_industrial_tenement` | Tenement Block | 223 | `res_high_industrial_03` | `/assets/gltf/industrial/res_high_industrial_03.glb` |
| `res_postwar_high_rise` | Residential High-Rise | 240 | `res_high_postwar_00` | `/assets/gltf/postwar/res_high_postwar_00.glb` |
| `res_postwar_housing_project` | Housing Project | 241 | `res_high_postwar_01` | `/assets/gltf/postwar/res_high_postwar_01.glb` |
| `res_postwar_luxury_tower` | Luxury Apartment Tower | 242 | `res_high_postwar_02` | `/assets/gltf/postwar/res_high_postwar_02.glb` |
| `res_modern_highrise_apt` | High-Rise Apartment | 260 | `res_high_modern_00` | `/assets/gltf/modern/res_high_modern_00.glb` |
| `res_modern_luxury_condo` | Luxury Condominium | 261 | `res_high_modern_01` | `/assets/gltf/modern/res_high_modern_01.glb` |
| `res_modern_penthouse_tower` | Penthouse Tower | 262 | `res_high_modern_02` | `/assets/gltf/modern/res_high_modern_02.glb` |
| `res_future_arcology` | Arcology | 280 | `res_high_future_00` | `/assets/gltf/future/res_high_future_00.glb` |
| `res_future_capsule_hotel` | Capsule Hotel | 281 | `res_high_future_01` | `/assets/gltf/future/res_high_future_01.glb` |
| `res_future_smart_apt` | Smart Apartment Complex | 282 | `res_high_future_02` | `/assets/gltf/future/res_high_future_02.glb` |
| `res_future_vertical_village` | Vertical Village | 283 | `res_high_future_03` | `/assets/gltf/future/res_high_future_03.glb` |
| `res_frontier_boarding_house` | Boarding House | 100 | `res_low_frontier_00` | `/assets/gltf/frontier/res_low_frontier_00.glb` |
| `res_frontier_cabin` | Frontier Cabin | 101 | `res_low_frontier_01` | `/assets/gltf/frontier/res_low_frontier_01.glb` |
| `res_frontier_dugout` | Sod Dugout | 102 | `res_low_frontier_02` | `/assets/gltf/frontier/res_low_frontier_02.glb` |
| `res_frontier_homestead` | Frontier Homestead | 103 | `res_low_frontier_03` | `/assets/gltf/frontier/res_low_frontier_03.glb` |
| `res_frontier_manor` | Frontier Manor | 104 | `res_low_frontier_04` | `/assets/gltf/frontier/res_low_frontier_04.glb` |
| `res_frontier_merchants_house` | Merchants House | 105 | `res_low_frontier_05` | `/assets/gltf/frontier/res_low_frontier_05.glb` |
| `res_frontier_ranch_house` | Ranch House | 106 | `res_low_frontier_06` | `/assets/gltf/frontier/res_low_frontier_06.glb` |
| `res_frontier_townhouse` | Frontier Townhouse | 107 | `res_low_frontier_07` | `/assets/gltf/frontier/res_low_frontier_07.glb` |
| `res_industrial_brownstone` | Brownstone | 120 | `res_low_industrial_00` | `/assets/gltf/industrial/res_low_industrial_00.glb` |
| `res_industrial_duplex` | Industrial Duplex | 121 | `res_low_industrial_01` | `/assets/gltf/industrial/res_low_industrial_01.glb` |
| `res_industrial_mansion` | Industrial Mansion | 122 | `res_low_industrial_02` | `/assets/gltf/industrial/res_low_industrial_02.glb` |
| `res_industrial_row_house` | Row House | 123 | `res_low_industrial_03` | `/assets/gltf/industrial/res_low_industrial_03.glb` |
| `res_industrial_victorian` | Victorian Townhouse | 124 | `res_low_industrial_04` | `/assets/gltf/industrial/res_low_industrial_04.glb` |
| `res_industrial_workers_cottage` | Workers Cottage | 125 | `res_low_industrial_05` | `/assets/gltf/industrial/res_low_industrial_05.glb` |
| `res_postwar_bungalow` | Postwar Bungalow | 140 | `res_low_postwar_00` | `/assets/gltf/postwar/res_low_postwar_00.glb` |
| `res_postwar_condo` | Condominium | 141 | `res_low_postwar_01` | `/assets/gltf/postwar/res_low_postwar_01.glb` |
| `res_postwar_garden_apt` | Garden Apartment | 142 | `res_low_postwar_02` | `/assets/gltf/postwar/res_low_postwar_02.glb` |
| `res_postwar_luxury_home` | Luxury Home | 143 | `res_low_postwar_03` | `/assets/gltf/postwar/res_low_postwar_03.glb` |
| `res_postwar_ranch` | Suburban Ranch | 144 | `res_low_postwar_04` | `/assets/gltf/postwar/res_low_postwar_04.glb` |
| `res_postwar_split_level` | Split-Level House | 145 | `res_low_postwar_05` | `/assets/gltf/postwar/res_low_postwar_05.glb` |
| `res_postwar_walkup` | Walkup Apartment | 146 | `res_low_postwar_06` | `/assets/gltf/postwar/res_low_postwar_06.glb` |
| `res_modern_duplex` | Modern Duplex | 160 | `res_low_modern_00` | `/assets/gltf/modern/res_low_modern_00.glb` |
| `res_modern_garden_apt` | Garden Apartment | 161 | `res_low_modern_01` | `/assets/gltf/modern/res_low_modern_01.glb` |
| `res_modern_gated_community` | Gated Community | 162 | `res_low_modern_02` | `/assets/gltf/modern/res_low_modern_02.glb` |
| `res_modern_midrise_apt` | Mid-Rise Apartment | 163 | `res_low_modern_03` | `/assets/gltf/modern/res_low_modern_03.glb` |
| `res_modern_senior_living` | Senior Living Complex | 164 | `res_low_modern_04` | `/assets/gltf/modern/res_low_modern_04.glb` |
| `res_modern_student_dorm` | Student Dormitory | 165 | `res_low_modern_05` | `/assets/gltf/modern/res_low_modern_05.glb` |
| `res_modern_studio_apt` | Studio Apartment Block | 166 | `res_low_modern_06` | `/assets/gltf/modern/res_low_modern_06.glb` |
| `res_modern_subsidized_housing` | Subsidized Housing | 167 | `res_low_modern_07` | `/assets/gltf/modern/res_low_modern_07.glb` |
| `res_modern_townhouse_row` | Modern Townhouse Row | 168 | `res_low_modern_08` | `/assets/gltf/modern/res_low_modern_08.glb` |
| `res_future_biome_dome` | Residential Biome Dome | 180 | `res_low_future_00` | `/assets/gltf/future/res_low_future_00.glb` |
| `res_future_eco_housing` | Eco-Housing Block | 181 | `res_low_future_01` | `/assets/gltf/future/res_low_future_01.glb` |
| `res_future_floating_apt` | Floating Apartment | 182 | `res_low_future_02` | `/assets/gltf/future/res_low_future_02.glb` |
| `svc_frontier_chapel` | Frontier Chapel | 0 | `svc_frontier_00` | `/assets/gltf/frontier/svc_frontier_00.glb` |
| `svc_frontier_doctors_office` | Doctors Office | 1 | `svc_frontier_01` | `/assets/gltf/frontier/svc_frontier_01.glb` |
| `svc_frontier_fire_brigade` | Volunteer Fire Brigade | 2 | `svc_frontier_02` | `/assets/gltf/frontier/svc_frontier_02.glb` |
| `svc_frontier_schoolhouse` | One-Room Schoolhouse | 3 | `svc_frontier_03` | `/assets/gltf/frontier/svc_frontier_03.glb` |
| `svc_frontier_sheriffs_office` | Sheriffs Office | 4 | `svc_frontier_04` | `/assets/gltf/frontier/svc_frontier_04.glb` |
| `svc_frontier_town_hall` | Town Hall | 5 | `svc_frontier_05` | `/assets/gltf/frontier/svc_frontier_05.glb` |
| `svc_industrial_church` | Church | 0 | `svc_industrial_00` | `/assets/gltf/industrial/svc_industrial_00.glb` |
| `svc_industrial_city_hall` | City Hall | 1 | `svc_industrial_01` | `/assets/gltf/industrial/svc_industrial_01.glb` |
| `svc_industrial_elementary` | Elementary School | 2 | `svc_industrial_02` | `/assets/gltf/industrial/svc_industrial_02.glb` |
| `svc_industrial_fire_station` | Fire Station | 3 | `svc_industrial_03` | `/assets/gltf/industrial/svc_industrial_03.glb` |
| `svc_industrial_high_school` | High School | 4 | `svc_industrial_04` | `/assets/gltf/industrial/svc_industrial_04.glb` |
| `svc_industrial_hospital` | City Hospital | 5 | `svc_industrial_05` | `/assets/gltf/industrial/svc_industrial_05.glb` |
| `svc_industrial_library` | Public Library | 6 | `svc_industrial_06` | `/assets/gltf/industrial/svc_industrial_06.glb` |
| `svc_industrial_police_station` | Police Station | 7 | `svc_industrial_07` | `/assets/gltf/industrial/svc_industrial_07.glb` |
| `svc_postwar_cathedral` | Cathedral | 0 | `svc_postwar_00` | `/assets/gltf/postwar/svc_postwar_00.glb` |
| `svc_postwar_civic_center` | Civic Center | 1 | `svc_postwar_01` | `/assets/gltf/postwar/svc_postwar_01.glb` |
| `svc_postwar_elementary` | Modern Elementary | 2 | `svc_postwar_02` | `/assets/gltf/postwar/svc_postwar_02.glb` |
| `svc_postwar_fire_station` | Modern Fire Station | 3 | `svc_postwar_03` | `/assets/gltf/postwar/svc_postwar_03.glb` |
| `svc_postwar_hospital` | General Hospital | 4 | `svc_postwar_04` | `/assets/gltf/postwar/svc_postwar_04.glb` |
| `svc_postwar_library` | Modern Library | 5 | `svc_postwar_05` | `/assets/gltf/postwar/svc_postwar_05.glb` |
| `svc_postwar_police_hq` | Police Headquarters | 6 | `svc_postwar_06` | `/assets/gltf/postwar/svc_postwar_06.glb` |
| `svc_postwar_university` | University Campus | 7 | `svc_postwar_07` | `/assets/gltf/postwar/svc_postwar_07.glb` |
| `svc_modern_community_college` | Community College | 0 | `svc_modern_00` | `/assets/gltf/modern/svc_modern_00.glb` |
| `svc_modern_daycare` | Daycare Center | 1 | `svc_modern_01` | `/assets/gltf/modern/svc_modern_01.glb` |
| `svc_modern_fire_station` | Modern Fire Station | 2 | `svc_modern_02` | `/assets/gltf/modern/svc_modern_02.glb` |
| `svc_modern_hospital` | Modern Hospital | 3 | `svc_modern_03` | `/assets/gltf/modern/svc_modern_03.glb` |
| `svc_modern_police_hq` | Police Headquarters | 4 | `svc_modern_04` | `/assets/gltf/modern/svc_modern_04.glb` |
| `svc_modern_public_library` | Public Library | 5 | `svc_modern_05` | `/assets/gltf/modern/svc_modern_05.glb` |
| `svc_modern_senior_center` | Senior Center | 6 | `svc_modern_06` | `/assets/gltf/modern/svc_modern_06.glb` |
| `svc_modern_university` | University Campus | 7 | `svc_modern_07` | `/assets/gltf/modern/svc_modern_07.glb` |
| `svc_future_ai_hospital` | AI Hospital | 0 | `svc_future_00` | `/assets/gltf/future/svc_future_00.glb` |
| `svc_future_auto_fire` | Automated Fire Response | 1 | `svc_future_01` | `/assets/gltf/future/svc_future_01.glb` |
| `svc_future_disaster_center` | Disaster Response Center | 2 | `svc_future_02` | `/assets/gltf/future/svc_future_02.glb` |
| `svc_future_drone_police` | Drone Police Station | 3 | `svc_future_03` | `/assets/gltf/future/svc_future_03.glb` |
| `svc_future_med_pod_clinic` | Med-Pod Clinic | 4 | `svc_future_04` | `/assets/gltf/future/svc_future_04.glb` |
| `svc_future_online_uni` | Online University Hub | 5 | `svc_future_05` | `/assets/gltf/future/svc_future_05.glb` |

### 5.2 Hero / infrastructure / special (46 entries)

| JSON `id` | Name | Era | Hero GLTF path (proposed) |
|---|---|---|---|
| `inf_frontier_outhouse_row` | Outhouse Row | frontier | `/assets/gltf/heroes/inf_frontier_outhouse_row.glb` |
| `inf_frontier_well` | Town Well | frontier | `/assets/gltf/heroes/inf_frontier_well.glb` |
| `inf_frontier_windmill` | Windmill | frontier | `/assets/gltf/heroes/inf_frontier_windmill.glb` |
| `inf_future_atmo_processor` | Atmospheric Processor | future | `/assets/gltf/heroes/inf_future_atmo_processor.glb` |
| `inf_future_desalination` | Fusion Desalination Plant | future | `/assets/gltf/heroes/inf_future_desalination.glb` |
| `inf_future_fusion_reactor` | Fusion Reactor | future | `/assets/gltf/heroes/inf_future_fusion_reactor.glb` |
| `inf_future_hyperloop` | Hyperloop Terminal | future | `/assets/gltf/heroes/inf_future_hyperloop.glb` |
| `inf_future_maglev_station` | Maglev Station | future | `/assets/gltf/heroes/inf_future_maglev_station.glb` |
| `inf_future_orbital_receiver` | Orbital Solar Receiver | future | `/assets/gltf/heroes/inf_future_orbital_receiver.glb` |
| `inf_future_quantum_hub` | Quantum Communication Hub | future | `/assets/gltf/heroes/inf_future_quantum_hub.glb` |
| `inf_industrial_coal_plant` | Coal Power Plant | industrial | `/assets/gltf/heroes/inf_industrial_coal_plant.glb` |
| `inf_industrial_sewer_pump` | Sewer Pump Station | industrial | `/assets/gltf/heroes/inf_industrial_sewer_pump.glb` |
| `inf_industrial_water_tower` | Water Tower | industrial | `/assets/gltf/heroes/inf_industrial_water_tower.glb` |
| `inf_modern_5g_tower` | 5G Cell Tower | modern | `/assets/gltf/heroes/inf_modern_5g_tower.glb` |
| `inf_modern_fiber_hub` | Fiber Optic Hub | modern | `/assets/gltf/heroes/inf_modern_fiber_hub.glb` |
| `inf_modern_nuclear_plant` | Nuclear Power Plant | modern | `/assets/gltf/heroes/inf_modern_nuclear_plant.glb` |
| `inf_modern_sewage_plant` | Sewage Treatment Plant | modern | `/assets/gltf/heroes/inf_modern_sewage_plant.glb` |
| `inf_modern_smart_grid` | Smart Grid Station | modern | `/assets/gltf/heroes/inf_modern_smart_grid.glb` |
| `inf_modern_solar_farm` | Solar Farm | modern | `/assets/gltf/heroes/inf_modern_solar_farm.glb` |
| `inf_modern_water_treatment` | Advanced Water Treatment | modern | `/assets/gltf/heroes/inf_modern_water_treatment.glb` |
| `inf_modern_wind_farm` | Wind Farm | modern | `/assets/gltf/heroes/inf_modern_wind_farm.glb` |
| `inf_postwar_gas_plant` | Gas Turbine Plant | postwar | `/assets/gltf/heroes/inf_postwar_gas_plant.glb` |
| `inf_postwar_hydroelectric` | Hydroelectric Dam | postwar | `/assets/gltf/heroes/inf_postwar_hydroelectric.glb` |
| `inf_postwar_treatment_plant` | Water Treatment Plant | postwar | `/assets/gltf/heroes/inf_postwar_treatment_plant.glb` |
| `inf_postwar_waste_plant` | Wastewater Treatment | postwar | `/assets/gltf/heroes/inf_postwar_waste_plant.glb` |
| `spc_frontier_cemetery` | Cemetery | frontier | `/assets/gltf/heroes/spc_frontier_cemetery.glb` |
| `spc_frontier_town_square` | Town Square | frontier | `/assets/gltf/heroes/spc_frontier_town_square.glb` |
| `spc_future_ai_governance` | AI Governance Tower | future | `/assets/gltf/heroes/spc_future_ai_governance.glb` |
| `spc_future_biopark` | Bioengineered Park | future | `/assets/gltf/heroes/spc_future_biopark.glb` |
| `spc_future_floating_district` | Floating District | future | `/assets/gltf/heroes/spc_future_floating_district.glb` |
| `spc_future_monument_unity` | Monument to Unity | future | `/assets/gltf/heroes/spc_future_monument_unity.glb` |
| `spc_future_space_elevator` | Space Elevator Terminal | future | `/assets/gltf/heroes/spc_future_space_elevator.glb` |
| `spc_future_time_capsule` | Millennium Time Capsule | future | `/assets/gltf/heroes/spc_future_time_capsule.glb` |
| `spc_future_underwater_habitat` | Underwater Habitat | future | `/assets/gltf/heroes/spc_future_underwater_habitat.glb` |
| `spc_future_zero_g_arena` | Zero-G Arena | future | `/assets/gltf/heroes/spc_future_zero_g_arena.glb` |
| `spc_industrial_city_park` | City Park | industrial | `/assets/gltf/heroes/spc_industrial_city_park.glb` |
| `spc_industrial_monument` | War Monument | industrial | `/assets/gltf/heroes/spc_industrial_monument.glb` |
| `spc_modern_art_gallery` | Modern Art Gallery | modern | `/assets/gltf/heroes/spc_modern_art_gallery.glb` |
| `spc_modern_botanical_garden` | Botanical Garden | modern | `/assets/gltf/heroes/spc_modern_botanical_garden.glb` |
| `spc_modern_concert_arena` | Concert Arena | modern | `/assets/gltf/heroes/spc_modern_concert_arena.glb` |
| `spc_modern_science_museum` | Science Museum | modern | `/assets/gltf/heroes/spc_modern_science_museum.glb` |
| `spc_modern_skate_park` | Skate Park | modern | `/assets/gltf/heroes/spc_modern_skate_park.glb` |
| `spc_modern_sports_stadium` | Sports Stadium | modern | `/assets/gltf/heroes/spc_modern_sports_stadium.glb` |
| `spc_modern_tech_incubator` | Tech Incubator | modern | `/assets/gltf/heroes/spc_modern_tech_incubator.glb` |
| `spc_modern_theme_park` | Theme Park | modern | `/assets/gltf/heroes/spc_modern_theme_park.glb` |
| `spc_postwar_community_pool` | Community Pool | postwar | `/assets/gltf/heroes/spc_postwar_community_pool.glb` |

---

## 6. Known gaps

| Gap | Detail | Mitigation |
|-----|--------|------------|
| **199 JSON vs 500 archetype slots** | [BUILDING_ARCHETYPE_3D](./BUILDING_ARCHETYPE_3D.md) cites **5 × 5 × 20 = 500** zone mesh variants. JSON defines **153** zone buildings + **46** heroes. **283** zone archetype keys (of 400) have no JSON content. | v1 ships **40–60** Meshy modules/era ([WEB_V1_SCOPE](./WEB_V1_SCOPE.md)); reuse archetype keys across similar JSON entries. |
| **Service era mismatch** | JSON has **36** service buildings across 5 eras. `deriveEra()` forces **`svc_modern_*`** for all TypeIds outside 100–499. Proposed table assigns TypeId `0–19` per era band, but only one `svc_modern_00` key exists. | Extend service TypeId space (e.g. 500–599) **or** hero GLTF per `svc_*` slug **or** collapse service visuals to 20 civic archetypes. |
| **No runtime JSON loader** | `buildings.json` is not parsed into TypeIds in Forge or WASM. `ZoneGrowthSystem` uses 15 hard-coded constants. | Implement `BuildingCatalog` loader; wire `PlaceBuildingCommand` + growth to catalog. |
| **Infrastructure / special** | 46 entries outside TypeId taxonomy. | Hero pipeline; separate from instanced zone meshes. |
| **Placeholder GLTF coverage** | Only **5** keys in `GLTF_CATALOG`. `hasGltfAsset()` returns true only for catalog keys — other paths fall back to procedural boxes. | Meshy batch per `scripts/meshy/manifest.json`; expand catalog as assets land. |
| **Dual maintenance** | C# `BuildingRenderer.cs` + TS `buildingArchetypes.ts` must stay aligned. | Future: shared JSON constants crate (noted in ADR). |

### Count summary

| Set | Count |
|-----|-------|
| `buildings.json` total | 199 |
| Zone-mapped (res/com/ind/svc) | 153 |
| Hero / infra / special | 46 |
| Theoretical zone archetype keys (4×5×20) | 400 |
| Theoretical + service keys per ADR (5×5×20) | 500 |
| JSON-defined unique zone archetype keys | 153 |
| Shipped placeholder GLBs | 5 |

---

## 7. Implementation index

| Concern | Path |
|---------|------|
| Content DB | `base/data/buildings/buildings.json` |
| TypeId → archetype (TS) | `web/packages/sim-types/src/buildingArchetypes.ts` |
| TypeId → visuals (C#) | `src/Forge.Engine/Rendering/BuildingRenderer.cs` |
| Growth TypeId selection | `src/Forge.Game/Simulation/ZoneGrowthSystem.cs` |
| Snapshot → R3F | `web/lib/city-data.ts` |
| GLTF path catalog | `web/lib/gltf-catalog.ts` |
| Placeholder assets | `web/public/assets/gltf/` |
| Meshy job manifest | `scripts/meshy/manifest.json` |
| Archetype ADR | `docs/design/BUILDING_ARCHETYPE_3D.md` |

---

## 8. Acceptance (bridge complete)

- [ ] `BuildingCatalog` loads `buildings.json` and exposes `slug → TypeId`
- [ ] `ZoneGrowthSystem.SelectBuildingType` draws from catalog, not hard-coded constants
- [ ] WASM `PlaceBuildingCommand` resolves slug or TypeId consistently
- [ ] `hasGltfAsset()` or CDN manifest covers all archetype keys used in v1 era arc
- [ ] Hero GLTF paths wired for civic / infrastructure placements

---

*Generated from `buildings.json` (199 entries) and `buildingArchetypes.ts` taxonomy. Re-run band tables if JSON changes.*
