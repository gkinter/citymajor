# Sim Snapshot V2 — Field Contract (Cathedral P7.1)

**Status:** Current as of tip `8f57614` (2026-08-11)  
**Closes:** Cathedral **P7.1** / Linear [SB-4261](https://linear.app/softblaze/issue/SB-4261)  
**Sources of truth (code):**

| Surface | Path | Role |
|---------|------|------|
| Engine immutable buffer | `src/Forge.Engine/Simulation/SimSnapshot.cs` | Double-buffered render/UI copy from `WorldState` after each tick |
| WASM JSON export | `src/Forge.SimWasm/SimSnapshotDto.cs` | CamelCase JSON via `GetRenderSnapshot()` (web harness + characterization) |
| Unity HUD binding | `unity/.../Sim/CitySimState.cs` + `CitySimBridge.cs` | In-process product UI — subset + derived fields |

**Related:** [`WASM_SIM_BRIDGE.md`](./WASM_SIM_BRIDGE.md) (worker protocol / cadence), [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md) §P7, OpenSpec [`snapshot-contract.md`](../../openspec/specs/snapshot-contract.md).

---

## 1. Purpose

Document **which fields exist on tip**, **who owns them**, and **how often they publish** — without inventing sim features. Web R3F is an archival harness; Unity is the product client. Both must stay consistent with `WorldState` via these exports.

---

## 2. Cadence & ownership

### 2.1 Tick / publish rates

| Layer | Rate | Notes |
|-------|------|-------|
| **Sim tick** | **8 Hz** (`Tick(0.125)`) | Shared: Unity `CitySimBridge`, WASM worker |
| **Engine `SimSnapshot.CaptureFrom`** | End of each sim tick | Full tile/building/vehicle copy for render thread |
| **WASM full JSON snapshot** | **≤ 4 Hz** (`SNAPSHOT_MIN_MS = 250`) | `GetRenderSnapshot()` — see WASM_SIM_BRIDGE §4 |
| **WASM resource merge** | **≤ 10 Hz** | Lightweight counters between full snapshots |
| **Unity `CitySimState`** | Each bridge publish (~8 Hz when SimHost ready) | Maps `SimSnapshot` + live systems (economy sample, friction, L2 HH) |
| **Traffic lite / Frank–Wolfe** | ≤ once per ~5 sim seconds | Edge volumes/travel times on `RoadGraph` — not every 8 Hz tick |
| **Household L2 sample** | Day-boundary / on demand in DTO build | Cap `PopulationSystem.DefaultL2SampleLimit` = **100** |
| **Friction corridors** | On snapshot build when `ActiveZoneCount > 1` | Stride 2 tile samples; empty otherwise |

### 2.2 Ownership legend

| Owner | Meaning |
|-------|---------|
| **WorldState** | Scalar / array mutated by sim systems; snapshotted |
| **EconomySystem** | Demand, goods, market zones, friction corridors |
| **PopulationSystem** | HH sample, commute O-D, commuter coverage |
| **EventSystem** | Active events list + `ApplyEventEffectsToState` → Event*Mult |
| **LawSystem** | Law*Mult scalars on WorldState |
| **ServiceSystem** | Per-tile health/police/fire/education coverage |
| **ResearchSystem** | Queue / eureka / branching (WASM DTO) |
| **Traffic assignment** | Mode shares + edge volume tables passed into DTO |
| **Client-derived** | Computed only in Unity bridge / HUD (not on engine snap) |

---

## 3. Three export surfaces

```
WorldState ──CaptureFrom──► SimSnapshot (engine)
                │
                ├── SimSnapshotDto.From(...) ──JSON──► web worker / harness
                │
                └── CitySimBridge ──► CitySimState (+ LatestFrictionCorridors, council seats, …)
```

**Do not add a field to one surface without updating the others when product UI or WASM smoke needs it.** **P7.4** CI: `./scripts/verify-sim-snapshot-v2.sh` (or `pnpm verify:sim-snapshot-v2`) asserts §4 ✅ cells ↔ tip C# exports.

---

## 4. Scalar field matrix (tip)

Legend: **E** = `SimSnapshot`, **W** = `SimSnapshotDto`, **U** = `CitySimState`.

### 4.1 Core / budget / happiness

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| Tick / TickCount | ✅ | ✅ `tick` | — | WorldState | |
| DateString | ✅ | — | — | WorldState | Engine/UI calendar |
| TimeOfDay | ✅ | — | ✅ | Derived from TickCount | 0–24 |
| CityFunds | ✅ | ✅ | ✅ `Funds` | WorldState | |
| Population | ✅ | ✅ | ✅ | WorldState | |
| HouseholdCount | — | ✅ | ✅ | WorldState.Households | |
| Happiness | ✅ | ✅ | ✅ | WorldState | |
| MonthlyIncome / Expenses | ✅ | ✅ | ✅ | WorldState.Income/Expenses | |
| Property/Commercial/IndustrialTaxRate | ✅ | — | — | WorldState | |
| LoanBalance | ✅ | — | — | WorldState | |
| Era | ✅ | ✅ | — (HUD era badge via bridge) | WorldState | |
| Weather / Season / Wind* | ✅ | — | — | WorldState | Visual life |
| ApprovalRating | ✅ | ✅ `approval` (×100 %) | ✅ `Approval` | WorldState | DTO is percent |
| EmploymentRate | ✅ | ✅ | ✅ | WorldState | |
| UnemploymentRate | — | — | ✅ | Client-derived | `1 − EmploymentRate` |
| TradeBalance / MonthlyExport/Import | ✅ | ✅ | ✅ | WorldState | Unity economy strip; WASM save/load |
| MeanTrafficDensity | ✅ | ✅ | ✅ | WorldState | |
| RushMultiplier | — | — | ✅ | Client-derived | Cosmetic |

### 4.2 Housing / abandonment (P2)

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| MeanRentBurden | ✅ | ✅ | ✅ | WorldState | |
| ResidentialVacancy | ✅ | ✅ | ✅ | WorldState | 1 = fully vacant |
| ConstructingBuildingCount | ✅ | ✅ | ✅ | WorldState | |
| AbandonedBuildingCount | ✅ | ✅ | ✅ | WorldState | P2.6 |
| BuildingCount | ✅ | via `buildings[]` | ✅ | Pool | |

### 4.3 Economy / goods / delivery (P3)

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| ShortageGoods / SurplusGoods | ✅ arrays | via `economy.*` | — | WorldState tops | DTO uses `EconomySnapshotDto` |
| GoodsShortageIndex / SurplusIndex | ✅ | ✅ | ✅ | WorldState | |
| InterZoneTradeVolume | ✅ | ✅ | ✅ | WorldState | |
| MeanInterZoneFriction | ✅ | ✅ | ✅ | WorldState | ≥ 1 |
| GoodsTransportCostIndex | ✅ | ✅ | ✅ | WorldState | 0–1 |
| MeanGoodsDeliveryDelay | ✅ | ✅ | ✅ | WorldState | P3.5; 0 free-flow … 1 congested |
| MarketZoneCount | ✅ | ✅ | ✅ | WorldState / Economy | 1–16 |
| Residential/Commercial/IndustrialDemand | — | ✅ | ✅ | EconomySystem | |
| Economy.Shortages/Surpluses/Flows/MarketZonePrices | — | ✅ nested | partial (Food/Water/Steel avg + spread) | EconomySystem | |
| FrictionCorridors[] | — | ✅ | via `LatestFrictionCorridors` | EconomySystem | Sparse tile heat |
| MaxPartitionPriceSpread | — | — | ✅ | Client-derived | From zone price spreads |
| HasGoodsPrices / Food|Water|SteelAvgPrice | — | — | ✅ | Client-derived | |

### 4.4 Traffic / commute / mode share (P4)

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| TileTraffic[] | ✅ dense | sparse `traffic[]` | — | Tiles | |
| RoadGraph (+ volumes/times) | — | ✅ | — | RoadGraph + traffic lite | |
| CommuterCoverage | — | ✅ | ✅ | PopulationSystem | |
| CommuteOdSample[] | — | ✅ (limit 16) | ✅ (limit 16) | PopulationSystem | Unity Cathedral HUD top pairs |
| Car/Transit/WalkModeShare | — | ✅ | ✅ | Traffic assignment args | |
| TransitLineCount / BusCoverage | — | ✅ | — | WorldState | |
| MeanCommuteMinutes / MeanCommuteSatisfaction | — | — | ✅ | Client-derived from HH / traffic | U3.5 |

### 4.5 Utilities / emergency / fire / EMS (P5)

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| PowerCoverageFraction | ✅ | ✅ | ✅ | WorldState | |
| WaterCoverageFraction | ✅ | ✅ | ✅ | WorldState | |
| BlackoutFraction | ✅ | ✅ | ✅ | WorldState | Rolling L0 utility deficit |
| WaterShortageFraction | ✅ | ✅ | ✅ | WorldState | Rolling L0 utility deficit |
| UtilityStressIndex | ✅ | ✅ | ✅ | WorldState | |
| MeanEmergencyResponseMinutes | ✅ | ✅ | ✅ | WorldState | Default 30 (no station) |
| HydrantCoverageFraction | ✅ | ✅ | ✅ | WorldState | P5.3 |
| ActiveFireCount | ✅ | ✅ | ✅ | WorldState | P5.3 |
| MeanEmsSurvivalRate | ✅ | ✅ | ✅ | WorldState | P5.4; default 0.40 |
| HospitalBedOccupancyFraction | ✅ | ✅ | ✅ | WorldState | Tier-2 hospital capacity |
| AvailableHospitalBeds | ✅ | ✅ | ✅ | WorldState | Tier-2 EMS diversion |
| WildfireRiskIndex | ✅ | ✅ | ✅ | WorldState | Tier-2 drought / wildfire risk |
| ActiveWildfireTileCount | ✅ | ✅ | ✅ | WorldState | Tier-2 burning fuel tiles |
| ArsonRiskIndex | ✅ | ✅ | ✅ | WorldState | Tier-2 crime-driven arson risk |
| ArsonRingActive | ✅ | ✅ | ✅ | WorldState | Tier-2 high-crime fire cluster |
| LookoutTowerCount | ✅ | ✅ | ✅ | WorldState | Tier-2 lookout towers |
| AerialFirefightingAvailable | ✅ | ✅ | ✅ | WorldState | Tier-2 aerial / water bomber |
| FireSafetyRating | ✅ | ✅ | ✅ | WorldState | Tier-2 fire rating 1–10 |
| FireInsurancePremiumMult | ✅ | ✅ | ✅ | WorldState | Tier-2 insurance from fire rating |
| MeanEducationLevel | ✅ | ✅ | ✅ | WorldState | Tier-2 education depth (0–3) |
| EducationCoverageFraction | ✅ | ✅ | ✅ | WorldState | Tier-2 HH under school coverage |
| ServiceCoverage[] | — | ✅ | overlay via services | ServiceSystem | Sparse zoned tiles |
| CouncilSeats | ✅ `byte[]` | ✅ `int[]` | via bridge getter | Politics | Length 9 |

### 4.6 Laws / events (P6)

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| ActiveLawCount | ✅ | — | ✅ | LawSystem | |
| ActiveLawIds[] | — | ✅ | — | LawSystem | String slug ids; WASM save/load |
| LawTrafficCapacityMult | ✅ | ✅ | ✅ | WorldState | |
| LawConstructionSpeedMult | ✅ | ✅ | — | WorldState | Not on Unity HUD tip |
| LawSpawnDemandMult | ✅ | ✅ | ✅ | WorldState | |
| LawResidential/Industrial/CommercialSpawnMult | ✅ | ✅ | ✅ | WorldState | |
| ActiveEventCount | ✅ | ✅ | ✅ | EventSystem / WorldState | |
| ActiveEvents[] | — | ✅ | Herald path | EventSystem | |
| EventTaxRevenueMult | ✅ | ✅ | ✅ | WorldState | P6.2 / P7.5 |
| EventImmigrationMult | ✅ | ✅ | ✅ | WorldState | |
| EventCommercialSpawnMult | ✅ | ✅ | ✅ | WorldState | |
| EventProductivityMult | ✅ | ✅ | ✅ | WorldState | |
| EventResearchMult | ✅ | ✅ | ✅ | WorldState | |
| EventSpawnDemandMult | ✅ | ✅ | ✅ | WorldState | |
| ActiveOrdinances / NextElectionYear | ✅ | ✅ | — | WorldState | Bitfield + election calendar on WASM DTO; law toggles also via ActiveLawIds |
| CulturalDna[] | ✅ | ✅ | — | WorldState | Politics flavor vector (−1…+1, length 8); WASM save/load |

### 4.7 Research

| Field | E | W | U | Owner | Notes |
|-------|---|---|---|-------|-------|
| ResearchPoints / ResearchRate | ✅ | points only on DTO | — | WorldState | |
| CurrentResearchId / Progress | ✅ | ✅ | — | WorldState | |
| UnlockedTechIds / ResearchQueue / QueueProgress | — | ✅ | Research panel | ResearchSystem | |
| EurekaBonuses / BranchingChoices | — | ✅ | — | ResearchSystem | |

---

## 5. Nested / array payloads

### 5.1 Engine `SimSnapshot`

| Array | Element | Cadence |
|-------|---------|---------|
| `TileTerrainTypes` / `TileZoneTypes` / `TileRoadFlags` | `byte[world²]` | Every capture |
| `TileTraffic` | `float[world²]` | Every capture |
| `Buildings[]` | GridX/Y, TypeId, Level, State, Occupants, MaxOccupants, Condition | Dense packed active slots |
| `Vehicles[]` | WorldX/Y, TypeId, Heading, Speed, MaxSpeed, Flags | Dense packed |
| `ShortageGoods` / `SurplusGoods` | GoodId + Score | Up to 5 each |
| `CouncilSeats` | `byte[9]` | Clone |
| `CulturalDna` | `float[8]` | Clone |

### 5.2 WASM `SimSnapshotDto` (JSON camelCase)

| Nested type | Fields | Notes |
|-------------|--------|-------|
| `BuildingDto` | id, typeId, tileX, tileZ, level, state, condition, fireRisk, serviceFlags | Sparse pool slots; fireRisk/serviceFlags for P5 save/load |
| `ZoneDto` / `RoadDto` | tile + type/flags | Sparse non-zero |
| `RoadGraphSnapshotDto` | nodeTypes/positions, edgeFrom/To, volumes, travelTimes | P1.6 / P4.2 |
| `TrafficDto` | tileX/Z, density | density ≥ 0.01 |
| `ServiceCoverageDto` | health, police, fire, education | Zoned tiles only |
| `FrictionCorridorDto` | tileX/Z, friction 0–1 | P3.4 |
| `ActiveEventDto` | eventId, typeId, phase, severity, tileX/Y | |
| `ActiveLawIds` | `string[]` | laws.json slug ids currently enabled |
| `ActiveOrdinances` / `NextElectionYear` | `ulong` / `int` | Politics bitfield + election calendar |
| `TradeBalance` / `MonthlyExportValue` / `MonthlyImportCost` | floats | P3 trade strip; restore seeds TradeSystem until next ProcessTrade |
| `CulturalDna` | `float[8]` | Politics flavor vector (−1…+1) |
| `EconomySnapshotDto` | shortages, surpluses, flows, marketZoneCount, marketZonePrices | |
| `PopulationL2Dto` | households[≤100] | Id, tiles, happiness, commuteMin, home/work building ids, rentBurden |
| `CommuteOdSampleDto` | home/work tiles, tripCount | Top 16 |

### 5.3 Unity extras (not full DTO mirrors)

| Binding | Source |
|---------|--------|
| `CitySimState.Households[]` | Population L2 sample (subset fields) |
| `CitySimState.CommuteOdSample[]` | `SimHost.CollectCommuteOdSample` (same collector as WASM DTO, limit 16) |
| `LatestFrictionCorridors` | Same `CollectFrictionCorridors` as WASM |
| Law sample id/name/active | Law catalog preview for panel |
| Mode-share + commute sat/minutes | Live traffic / population after tick |

---

## 6. Known parity gaps (honest)

These are **documentation of tip reality**, not a backlog invent:

1. ~~**WASM_SIM_BRIDGE §6** TypeScript sketch stale~~ — **closed:** `@citymajor/sim-types` `simSnapshot.ts` + WASM_SIM_BRIDGE §6 + archival `sim-bridge.ts` fields match tip `SimSnapshotDto` (Event*Mult, Law*Mult, ActiveLawIds, ordinances, utilities, CulturalDna, TradeBalance, fire/wildfire/hospital).
2. **Planned-only** (not on tip): Economic Control Spectrum slider (P6.4 / v2), bilateral trade, multiplayer — do not claim present.
---

## 7. Change protocol

1. Mutate `WorldState` in the owning system.
2. Add to `SimSnapshot.CaptureFrom` if Unity/native render needs it.
3. Add to `SimSnapshotDto.From` + JSON source-gen context if WASM/web smoke needs it.
4. Bind in `CitySimBridge` → `CitySimState` / Cathedral HUD if product UI needs it.
5. Update **this file** + mark CATHEDRAL_PROGRAM changelog.
6. Run `./scripts/verify-sim-snapshot-v2.sh` (extend `FIELD_EXPAND` / `REVERSE_ALLOW` if the checker cannot map a compound label).
7. Prefer characterization tests over inventing HUD-only fake metrics.

---

## 8. Changelog

| Date | Change |
|------|--------|
| 2026-08-11 | **P7.1** initial `SIM_SNAPSHOT_V2.md` from tip `8f57614` exports (Event*Mult, AbandonedBuildingCount, fire/EMS, delivery delay, HH L2, friction corridors, mode share, council seats) |
| 2026-08-11 | **P7.4** gap-matrix CI — `scripts/verify-sim-snapshot-v2.py` doc↔export check (SB-4264) |
| 2026-08-11 | **Tier-2 hospital capacity** — `HospitalBedOccupancyFraction` / `AvailableHospitalBeds` on E+W+U |
| 2026-08-11 | **Law\*Mult WASM DTO** — traffic/construction/spawn Mults export + `ApplySnapshotDto` restore (closes former §6 gap #2) |
| 2026-08-11 | **ActiveLawIds WASM DTO** — ordinance slug ids export + `ApplySnapshotDto` restore (deeper than Law\*Mult alone) |
| 2026-08-11 | **ActiveOrdinances / NextElectionYear WASM DTO** — politics bitfield + election year export + restore (closes former §6 gap #6) |
| 2026-08-11 | **BlackoutFraction / WaterShortageFraction WASM DTO** — rolling L0 utility shortages export + restore (closes former §6 gap #1) |
| 2026-08-11 | **CulturalDna[] WASM DTO** — politics flavor vector export + `ApplySnapshotDto` restore (closes former §6 gap #5) |
| 2026-08-11 | **TradeBalance / export-import WASM DTO** — TradeBalance + MonthlyExportValue/ImportCost export + `ApplySnapshotDto` restore (closes former §6 gap #1) |
| 2026-08-11 | **Commute O-D Unity parity** — `CitySimState.CommuteOdSample[]` via `SimHost.CollectCommuteOdSample`; Cathedral HUD top pairs (closes former §6 Commute O-D gap) |
| 2026-08-11 | **Tier-2 wildfire / arson rings** — `WildfireRiskIndex` / `ActiveWildfireTileCount` / `ArsonRiskIndex` / `ArsonRingActive` on E+W+U |
| 2026-08-11 | **Tier-2 aerial / lookout / fire rating** — `LookoutTowerCount` / `AerialFirefightingAvailable` / `FireSafetyRating` / `FireInsurancePremiumMult` on E+W+U |
| 2026-08-11 | **Tier-2 education depth** — `MeanEducationLevel` / `EducationCoverageFraction` on E+W+U; HH progression under school coverage |
| 2026-08-11 | **WASM_SIM_BRIDGE / sim-types TS refresh** — `@citymajor/sim-types` `simSnapshot.ts` + WASM_SIM_BRIDGE §6 + archival `sim-bridge.ts` match tip DTO (closes former §6 gap #1) |
