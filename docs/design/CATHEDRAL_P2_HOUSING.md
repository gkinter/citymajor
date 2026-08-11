# Cathedral P2 — Housing & Zoning

**Status:** Spec (characterization tests pinned; implementation partial)  
**Program:** WP-E cathedral stretch · [`SIM_FOUNDATION_CHARTER.md`](./SIM_FOUNDATION_CHARTER.md)  
**Prerequisites:** P1 traffic partition observability · [`AGENT_05_ZONING_BUILDINGS.md`](./AGENT_05_ZONING_BUILDINGS.md)  
**v1 scale:** 256×256 map · ~10k households · ~5k buildings · Frontier → Industrial era arc

---

## 1. Goal

Close the **housing market loop**: zoning density and supply → rent → household burden → satisfaction / migration → RCI residential demand. Player-visible zoning tools must expose office, mixed-use, park, and modern-era agricultural subsets without shipping full Modern/Future content.

---

## 2. Milestones (v1)

| ID | Deliverable | Current state |
|----|-------------|---------------|
| P2.1 | Zone palette: office (5), mixed (6), park ploppable, ag subset | **Partial** — `ZoneOffice`/`ZoneMixedUse` in `ZoneGrowthSystem`; park via `ServicePark` flag; ag zone type 7 exists |
| P2.2 | Density brush (low / med / high) on painted zones | **Stub** — `PaintZone` sets density=1; `ZoneDensity` used in spawn math |
| P2.3 | Rent = f(land_value, supply, goods_shortage) + rent burden export | **Not implemented** — housing satisfaction uses condition/level/overcrowding only |
| P2.4 | Rent burden → emigration bias + `housing_crisis` Herald event | **Not implemented** — emigration uses aggregate satisfaction only |
| P2.5 | Snapshot: `MeanRentBurden`, residential vacancy proxy | **P2.5 partial** — vacancy logic inside `GetHousingAvailabilityModifier` only |

---

## 3. Zone types to add (v1 playable)

Align `TileData.ZoneType` with brush palette and growth system:

| Byte | Code | Brush label | Growth / jobs | v1 notes |
|------|------|-------------|---------------|----------|
| 0 | — | None | — | Bulldoze clears |
| 1 | R-low | Residential (low) | Housing | Frontier cabins → industrial row houses |
| 2 | R-high | Residential (high) | Housing | Tenements / early apartments |
| 3 | C | Commercial | Jobs + shops | Existing |
| 4 | I | Industrial | Jobs + goods | Existing |
| 5 | O | Office | High-edu jobs | Uses commercial building catalog today; split office TypeIds in P2.1 |
| 6 | MX | Mixed-use | Res + commercial | `Math.Max(resDemand, comDemand)` |
| 7 | A | Agricultural | Ind proxy (×0.5 demand) | **v1 subset:** frontier farms + industrial market gardens only; no modern mega-farms |
| 8 | P | Park / recreation | No growth | Painted zone byte 8 (U3.4) + ploppable `ServicePark`; raises land value in `RecalculateLandValue` / `HasNearbyPark` |

**Era gating (Frontier → Industrial):** Office brush unlocks at Industrial era (`Era >= 3`). Mixed-use unlocks Colonial+ (`Era >= 2`). Ag subset always available in frontier band.

---

## 4. Density brush behavior

`TileData.ZoneDensity` encoding (unchanged):

| UI label | Byte | Occupant multiplier (`CalculateMaxOccupants`) | Building tier bias |
|----------|------|-----------------------------------------------|-------------------|
| Low | 0–1 | ×1 | Small archetype |
| Medium | 2 | ×2 | Medium archetype |
| High | 3 | ×4 | Large / tower archetype |

**Brush modes (tool contract):**

1. **Paint** — sets `ZoneType` + current density level from toolbar.
2. **Density-only** — same zone type; cycles or sets density without clearing buildings (invalid if operational building incompatible — defer demolition prompt to v2).
3. **Fill** — flood-fill contiguous same-type empty tiles inside selection.

**SimHost API (target):**

```csharp
void PaintZone(int x, int y, byte zoneType, byte density = 0 /* 0 = use toolbar default */);
```

Growth uses density in `SelectBuildingType` and `CalculateMaxOccupants` (`ZoneGrowthSystem`).

---

## 5. Housing market — rent formulas

Per-household monthly rent `R_i` (currency units / month):

```
base_rent(tile) = 200 + 1800 * land_value(tile)     // land_value ∈ [0,1] on tile
supply_factor     = clamp( city_vacancy / 0.15, 0.5, 2.0 )
                    // city_vacancy = 1 - occupied_res_capacity / total_res_capacity
goods_factor      = 1 + 0.25 * goods_shortage_index  // WorldState.GoodsShortageIndex ∈ [0,1]
                    // models food/utility cost pass-through into housing pressure

R_i = base_rent(home_tile) * supply_factor * goods_factor * (0.85 + 0.05 * building_level)
```

**Rent burden** (household):

```
burden_i = R_i / max(monthly_income_i, 1)
```

City aggregate for snapshot / Herald:

```
mean_rent_burden = mean(burden_i) over households with home_tile != 0
```

**Housing satisfaction replacement** (`CalculateHousingSatisfaction`): blend physical quality (condition, level, overcrowding) with affordability:

```
afford_score = clamp(1 - burden_i / 0.45, 0, 1) * 100   // 45% burden = broke
housing_sat  = 0.55 * physical_score + 0.45 * afford_score
```

Constants live in `PopulationSystem` next to existing satisfaction weights.

---

## 6. Rent burden threshold → migration & events

| Threshold | Effect |
|-----------|--------|
| `burden > 0.45` | −15 housing satisfaction equivalent; increments `months_high_burden` per household |
| `burden > 0.55` for 3+ consecutive months | +0.25 emigration probability per month (stacks with unhappiness emigration) |
| `mean_rent_burden > 0.50` for 2+ consecutive months | Fire Herald template `housing_crisis` (category: population) |
| `city_vacancy < 0.05` AND `ResidentialDemand > 0.3` | Fire Herald `housing_shortage` (distinct from goods shortage) |

**Immigration modifier** (extends `CalculateImmigration`):

```
housing_availability *= clamp(city_vacancy / 0.20, 0.1, 2.0)   // existing vacancy modifier
rent_attractiveness  = clamp(1.2 - mean_rent_burden, 0.3, 1.5)
immigration_rate    *= rent_attractiveness
```

**v2 defer:** rent control laws, social housing queue, homeless encampments on `HomeBuildingId == 0`.

---

## 7. Goods panel / HUD cross-links

- Goods shortage index already on snapshot (`GoodsShortageIndex`) — feeds `goods_factor` in rent.
- Residential demand from `ZoneGrowthSystem.GetResidentialDemand` must use same vacancy definition as `supply_factor` (single source: `CalculateHousingSupply`).

---

## 8. Acceptance tests (characterization)

| Test | File | Status |
|------|------|--------|
| Rent burden rises when residential demand high and supply low | `CathedralHousingTests` | **Skipped** (`P2.3 not implemented`) |
| Migration out when `rent_burden > threshold` | `CathedralHousingTests` | **Skipped** (`P2.3 not implemented`) |
| Density brush doubles max occupants at high vs low | `ZoneDensityTests` (`CalculateMaxOccupants_Density3_DoublesEachStepVsDensity1`) | **Done** — ×1/×2/×4 stepwise doubling |
| Office zone employs edu≥3 households | `PopulationSystemTests` (`EmploymentMatching_OfficeZone_*`); era-gate Industrial via `zone-tiers.test.ts` | **Done** (`a990636`) |
| `housing_crisis` event when mean burden > 0.50 | Herald integration | Pending P2.4 |
| `MeanRentBurden` on snapshot matches manual calc | `CathedralHousingTests` | Pending P2.5 |

Run:

```bash
dotnet test tests/Forge.SimCore.Tests --filter "FullyQualifiedName~Cathedral"
```

---

## 9. Snapshot export (P2)

| Field | Source | P2 status |
|-------|--------|-----------|
| `MeanRentBurden` | `PopulationSystem` monthly rollup | **TODO P2.3** — spec only until rent implemented |
| `ResidentialDemand` | `EconomySystem` / `ZoneGrowthSystem` | Live |
| `GoodsShortageIndex` | `EconomySystem.PublishImbalancesTo` | Live (feeds rent) |

---

## 10. Implementation order (recommended)

1. **P2.2** Density brush API + `PaintZone(x,y,type,density)` — unblocks growth tier tests.  
2. **P2.1** Office / mixed / park palette + era gates — no new sim math.  
3. **P2.3** Rent + burden + unskip characterization tests.  
4. **P2.4** Migration bias + `housing_crisis` Herald hook.  
5. **P2.5** `MeanRentBurden` on `WorldState` / `SimSnapshot`.
