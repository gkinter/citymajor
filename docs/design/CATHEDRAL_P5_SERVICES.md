# Cathedral P5 — Services & Utilities Foundation

**Status:** Spec + characterization (P5.1 coverage export live)  
**Program:** [`CATHEDRAL_PROGRAM.md`](./CATHEDRAL_PROGRAM.md) pillar **P5 — Services**  
**Linear:** [SB-4987](https://linear.app/softblaze/issue/SB-4987)  
**Prerequisites:** ServiceSystem daily grids · L0 partition balance  
**References:** [`AGENT_06_SERVICES.md`](./AGENT_06_SERVICES.md) · [`WASM_SIM_BRIDGE.md`](./WASM_SIM_BRIDGE.md) · [`SIM_FOUNDATION_CHARTER.md`](./SIM_FOUNDATION_CHARTER.md)

---

## 1. Goal

Surface **power / water coverage** from the L0 utility partition balance so players (and Herald) can see blackouts and shortages. Wire **emergency response time** from road-graph distance × BPR congestion (P5.2). Ship **fire v1** hydrant coverage + spread (P5.3). Ship **EMS survival** from response minutes (P5.4). Ship **Tier-2 hospital capacity** — EMS transports to nearest hospital with free beds + HUD bind. Ship **Tier-2 wildfire / arson rings** — drought fuel spread + high-crime ignition clusters. Ship **Tier-2 aerial / lookout / fire rating** — lookout spark mitigation, aerial suppression, city fire safety rating → insurance premium. Ship **Tier-2 education depth** — household education progression under school coverage + research RP mult + Edu HUD. Ship **Tier-2 park amenity parity** — painted park zones boost health/exercise (not only land value) + Park HUD. Ship **Tier-2 health → P4** — `HealthSatisfaction` feeds household satisfaction / migration so parks + hospitals change city outcomes. Ship **Tier-2 hospital → HH health progression** — hospitals raise nearby `HealthSatisfaction` over time (education analogue) + Hosp HUD coverage. Ship **Tier-2 police / crime** — station quality deepens crime suppression; coverage → crime → `SafetySatisfaction` → satisfaction / immigration + Police HUD (arson already reads tile crime). Ship **Tier-2 waste / pollution** — garbage depots abate residential/commercial waste; coverage → pollution → environment satisfaction / immigration + Waste HUD. Ship **Tier-2 sewage / water contamination** — treatment plants abate waterborne pollution; coverage → water quality → health / environment / immigration + Sewage HUD. Ship **Manning / CSO storm overflow** — Manning pipe capacity + rational-method storm runoff; combined inflow over capacity overflows raw sewage into waterways → pollution / water quality + Sewage HUD CSO/util. Ship **Tier-2 internet / telecom** — telecom hubs deepen `TileData.InternetConnection` (0=none…3=5G); coverage → services satisfaction / immigration + Net HUD.

---

## 2. Milestones (v1)

| ID | Deliverable | Current state |
|----|-------------|---------------|
| **P5.1** | Utilities L0 balance on WASM status + HUD % | **Live** — `WorldState.PowerCoverageFraction` / `WaterCoverageFraction` → `WasmStatusDto` + ResourcesHud `Util` line |
| **P5.2** | Emergency response time = distance + traffic | **Live** — `EmergencyResponseTime` + mean minutes on `WasmStatusDto` / Resources+Economy HUD |
| **P5.3** | Fire v1 (spread + hydrant coverage) | **Live** — `FireResponse` + `HydrantCoverageFraction` / `ActiveFireCount` on snapshot + ResourcesHud Fire line |
| **P5.4** | EMS survival curve from response minutes | **Live** — `EmsSurvival` + `MeanEmsSurvivalRate` on snapshot + ResourcesHud `EMS Xm · N%` |
| **Tier-2** | Hospital capacity + EMS diversion | **Live** — `HospitalCapacity` + `HospitalBedOccupancyFraction` / `AvailableHospitalBeds` + ResourcesHud Hosp line |
| **Tier-2** | Wildfire / arson rings | **Live** — `WildfireArson` + `WildfireRiskIndex` / `ActiveWildfireTileCount` / `ArsonRiskIndex` / `ArsonRingActive` + Fire HUD 🌲/🕵️ |
| **Tier-2** | Aerial / lookout / fire rating | **Live** — lookout spark cut + aerial suppression + `FireSafetyRating` / `FireInsurancePremiumMult` + Fire HUD 🔭/✈️/⭐ |
| **Tier-2** | Education depth | **Live** — `EducationProgression` HH level-ups under school coverage + `MeanEducationLevel` / `EducationCoverageFraction` + Edu HUD 🎓 |
| **Tier-2** | Park amenity parity | **Live** — `ParkAmenity` painted zones + buildings raise health/leisure; `MeanParkAccess` / `ParkAccessFraction` / `MeanHealthSatisfaction` + Park HUD 🌳 |
| **Tier-2** | Health → P4 outcomes | **Live** — `HealthSatisfaction` weight in `CalculateSatisfaction` + mean-health immigration mod → happiness / emigration / immigration |
| **Tier-2** | Hospital → HH health | **Live** — `HealthProgression` raises / decays `HealthSatisfaction` under hospital coverage + `HealthCoverageFraction` + Hosp HUD ❤ / cov% |
| **Tier-2** | Police / crime depth | **Live** — `PoliceCrime` station quality → crime; `PoliceCoverageFraction` / `MeanCrimeRate` / `MeanSafetySatisfaction` + Police HUD 👮 → P4 sat / immigration |
| **Tier-2** | Waste / pollution depth | **Live** — `WasteCollection` depots → pollution + landfill capacity / recycling diversion; `WasteCoverageFraction` / `MeanPollution` / `MeanEnvironmentScore` / `LandfillUtilizationFraction` / `RecyclingDiversionRate` / `LandfillOverflowRate` + Waste HUD 🗑️ → P4 sat / immigration |
| **Tier-2** | Sewage / water contamination | **Live** — `SewageTreatment` plants → water quality; `SewageCoverageFraction` / `MeanWaterContamination` / `MeanWaterQuality` + Sewage HUD 💧 → P4 sat / immigration |
| **Tier-2** | Manning / CSO storm overflow | **Live** — `StormOverflow` Manning capacity + rational runoff; `MeanPipeUtilization` / `CsoOverflowRate` / `StormRunoffLoad` + Sewage HUD `cso`/`util` |
| **Tier-2** | Internet / telecom | **Live** — `TelecomNetwork` hubs → `InternetConnection`; T044/T045 fiber/5G tech gates; `InternetCoverageFraction` / `MeanInternetTier` / `MeanTelecomAccess` + Net HUD 📡 → P4 services sat / immigration |

---

## 3. Existing sim inventory (do not rewrite)

| Component | Path | Role |
|-----------|------|------|
| `UtilityPartitionBalance` | `src/Forge.Game/Simulation/UtilityPartitionBalance.cs` | Per-partition power/water supply vs demand on L0 tick |
| `ServiceSystem` | `src/Forge.Game/Simulation/ServiceSystem.cs` | Daily `RecalculatePowerGrid` / `RecalculateWaterGrid` BFS; owns `UtilityBalance` |
| `TileData.PowerGrid` / `WaterGrid` | `Forge.Engine` | Binary connected flags (0/1) for overlays |
| `WorldState.*CoverageFraction` | `Forge.Engine` | **0–1** city-wide partition coverage (default 1) |
| `UtilityCoverageExport` | `Forge.SimWasm` | Stepped tile samples for Unity GL overlay (**U** key) |
| Web HUD | `web/components/city/ResourcesHud.tsx` | `⚡ {power%} · 💧 {water%}` from status fractions × 100 |
| Unity HUD | `ResourcesHudController` | Same percent readout |

### Coverage formula (pinned)

```
powerCoverageFraction = poweredPartitions / partitionCount   // supply ≥ demand
waterCoverageFraction = wateredPartitions / partitionCount
utilityStressIndex    = max(blackoutFraction, waterShortageFraction)  // smoothed
```

Internal scale is **0–1**. HUD shows **percent** (`Math.round(fraction * 100)`).

---

## 4. WASM / client data contract

### 4.1 Live (P5.1)

`GetStatus` / snapshot JSON (camelCase):

```json
{
  "powerCoverageFraction": 0.875,
  "waterCoverageFraction": 0.75,
  "utilityStressIndex": 0.18
}
```

Worker maps the same keys onto `SimResources`; ResourcesHud renders when either coverage field is present.

### 4.2 Characterization

`CathedralUtilitiesTests` pins:

- Snapshot JSON includes `powerCoverageFraction` / `waterCoverageFraction` in `[0, 1]`
- `SimSnapshotDto` mirrors `WorldState` utility fields
- L0 tick without plants drops coverage below 1; plants restore local coverage
- Emergency response minutes rise with road-graph distance and BPR congestion (`EmergencyResponseTime_UsesDistancePlusTraffic`)
- `GetStatus` exports `meanEmergencyResponseMinutes` below the no-station default when a fire station covers zoned tiles

### 4.3 P5.2 response formula

```
responseMinutes =
  roadGraphPathCost(station → incident, BPR edge times) / vehicleSpeed
  + crewReadiness
```

`pathCost` uses Dijkstra on `WorldState.Roads` with `RoadEdgeTravelTimes` when present; otherwise free-flow edge costs; Euclidean tile distance as last resort. `ServiceSystem.CalculateFireResponseTime` forwards to `EmergencyResponseTime`. City-wide mean is sampled on a stride during `DailyTick` and exported as `meanEmergencyResponseMinutes` for ResourcesHud (`EMS`) and Economy Services.

### 4.4 Live (P5.2 HUD)

```json
{
  "meanEmergencyResponseMinutes": 4.2
}
```

Worker maps the key onto `SimResources`; ResourcesHud shows `EMS 4.2m` when present.

### 4.5 Live (P5.3 fire v1)

```json
{
  "hydrantCoverageFraction": 0.72,
  "activeFireCount": 2
}
```

- Hydrants = buildings with `ServiceHydrant` (`1 << 9`); coverage radius **3 tiles**
- No hydrant at incident → response minutes **×2** (shuttle water)
- Spread chance: `base * material * wind * adjacency * hydrant_inverse` (AGENT_06)
- Burning intensity stored in `BuildingData.FireRisk`; Unity ResourcesHud shows `🚰 {hydrant%} · 🔥 {n}` when fires active

`CathedralUtilitiesTests` pins hydrant response/spread math, adjacent ignition, and snapshot export.

### 4.6 Live (P5.4 EMS survival)

```json
{
  "meanEmsSurvivalRate": 0.72
}
```

- Curve (`MISSING_SYSTEMS` §1.2): &lt;5 min → 90%, 5–10 → 70%, 10–15 → 55%, ≥15 → 40%
- City mean = average of per-tile survival over the same zoned sample as `meanEmergencyResponseMinutes`
- Unity ResourcesHud EMS line: `{minutes} · {survival%}`

`CathedralUtilitiesTests` pins bucket thresholds, faster response → higher mean survival, and snapshot export.

### 4.7 Live (Tier-2 hospital capacity)

```json
{
  "hospitalBedOccupancyFraction": 0.4,
  "availableHospitalBeds": 12
}
```

- Hospitals = buildings with `ServiceHealth` (`1 << 4`); beds = `MaxOccupants` (fallback `50 × level`)
- EMS finds **nearest hospital with free beds** (skips full); transport minutes add to the survival chain
- No health buildings → transport `0` (P5.4 station-only survival preserved)
- Health buildings all full → `NoCapacityTransportMinutes` (30) diversion penalty
- Unity ResourcesHud Hosp line: `🏥 {free} free · {occ%}`

`CathedralUtilitiesTests` pins nearest-with-capacity diversion, full-city penalty, open beds raising survival, and snapshot export.

### 4.8 Live (Tier-2 wildfire / arson rings)

```json
{
  "wildfireRiskIndex": 0.7,
  "activeWildfireTileCount": 3,
  "arsonRiskIndex": 0.12,
  "arsonRingActive": false
}
```

- Fuel = forest terrain or park/ag zone without a building; roads / water / rock are firebreaks
- Drought risk: heatwave → 1.0, dry summer → 0.7, else baseline; sparks + cardinal spread on fuel; edge tiles can ignite buildings via `FireResponse`
- Arson: crime above threshold raises per-building ignition chance; ≥2 concurrent high-crime building fires → `ArsonRingActive`
- Unity ResourcesHud Fire line appends `🌲 {n|drought%}` and `🕵️` / arson % when relevant

`CathedralUtilitiesTests` pins drought buckets, firebreak stop, edge building ignition, arson ring detection, and snapshot export.

### 4.9 Live (Tier-2 aerial / lookout / fire rating)

```json
{
  "lookoutTowerCount": 1,
  "aerialFirefightingAvailable": true,
  "fireSafetyRating": 8,
  "fireInsurancePremiumMult": 0.94
}
```

- Lookouts = `ServiceLookout` (`1 << 10`); radius **20** tiles; spark chance × **0.25** under coverage
- Aerial = `ServiceAerialFire` (`1 << 11`); suppresses up to **3** burning fuel tiles / hour
- Fire safety rating **1–10** from hydrants + fire stations + lookouts + aerial − active fires / wildfire / arson / drought
- Insurance premium mult: **1.8** at rating 1 → **0.7** at rating 10 (`MISSING_SYSTEMS` §1.1)
- Unity ResourcesHud Fire line appends `🔭 {n}`, `✈️`, and `⭐ {rating}`

`CathedralUtilitiesTests` pins lookout spark cut, aerial suppress, rating/premium monotonicity, and snapshot export.

### 4.10 Live (Tier-2 education depth)

```json
{
  "meanEducationLevel": 1.4,
  "educationCoverageFraction": 0.62
}
```

- Households under school coverage (`ServiceEducation`) raise `Education` 0→3 over time (quality × coverage; higher levels harder)
- Thin / no coverage slowly decays education
- Research RP × `EducationLevelMultiplier` from mean level (0.70–1.30)
- Unity ResourcesHud Edu line: `🎓 {mean}/3 · {cov%}`

`CathedralUtilitiesTests` pins upgrade/decay chances, school coverage tick, research mult monotonicity, and snapshot export.

### 4.11 Live (Tier-2 park amenity parity)

```json
{
  "meanParkAccess": 0.42,
  "parkAccessFraction": 0.38,
  "meanHealthSatisfaction": 0.61
}
```

- Painted `ZonePark` (byte 8) and ploppable `ServicePark` share `ParkAmenity.HasNearbyPark` / `LocalParkAccess` (land value + health/exercise)
- Daily tick blends `HealthSatisfaction` / `LeisureSatisfaction` toward park-aware targets (AGENT_06 exercise term)
- Unity ResourcesHud Park line: `🌳 {access%} · ❤️ {health%}`

`CathedralUtilitiesTests` pins painted-zone access, health-score lift, HH tick, and snapshot export.

### 4.12 Live (Tier-2 health → P4 satisfaction / migration)

Park + hospital coverage already write `HouseholdData.HealthSatisfaction` and `MeanHealthSatisfaction`. This milestone closes the loop into **population outcomes**:

- `PopulationSystem.CalculateSatisfaction` weights `HealthSatisfaction` at **0.05** (services/leisure trimmed so weights still sum to 1.0)
- Staggered `Tick` maps that score into `Happiness` bytes → emigration threshold path unchanged
- `CalculateImmigration` multiplies by mean-health attractiveness (**0.55–1.45**, neutral ~1.0 at 128/255)
- Unity Park HUD tooltip notes the P4 coupling

`PopulationSystemTests` + `CathedralUtilitiesTests` pin higher health → higher satisfaction, park tick → satisfaction lift, and higher mean health → more immigrants.

### 4.13 Live (Tier-2 hospital → HH health progression)

```json
{
  "healthCoverageFraction": 0.48,
  "meanHealthSatisfaction": 0.61
}
```

- Households under hospital coverage (`ServiceHealth`) blend `HealthSatisfaction` toward a quality × coverage target (AGENT_06 healthcare + park exercise term)
- Thin / no coverage slowly decays toward an uncovered baseline
- Analogous to `EducationProgression` (schools → education levels); parks still contribute via exercise
- Unity ResourcesHud Hosp line appends `❤ {health%} · {cov%}`

`CathedralUtilitiesTests` pins recovery targets, hospital coverage tick, uncovered decay, park-exercise lift, and snapshot export.

### 4.14 Live (Tier-2 police / crime depth)

```json
{
  "policeCoverageFraction": 0.55,
  "meanCrimeRate": 0.18,
  "meanSafetySatisfaction": 0.72
}
```

- Police stations (`ServicePolice`) publish coverage; **station quality** (level × condition) scales the AGENT_06 police term so better stations suppress crime more at the same radius
- Daily tick blends `SafetySatisfaction` toward home-tile `(1 − crime)`
- `CalculateSatisfaction` reads HH `SafetySatisfaction`; immigration multiplies by mean-safety attractiveness (**0.55–1.45**)
- Arson / decline already consume tile crime — deepened stations lower those risk paths
- Unity ResourcesHud Police line: `👮 {safety%} · crime {crime%} · {cov%}`

`CathedralUtilitiesTests` + `PopulationSystemTests` pin quality→lower crime, police tick→safety, snapshot export, and safety→immigration.

### 4.15 Live (Tier-2 waste / pollution depth)

```json
{
  "wasteCoverageFraction": 0.48,
  "meanPollution": 0.22,
  "meanEnvironmentScore": 0.78,
  "landfillUtilizationFraction": 0.42,
  "recyclingDiversionRate": 0.35,
  "landfillOverflowRate": 0.02
}
```

- Garbage depots (`ServiceGarbage`, `1 << 13`) publish coverage; **depot quality** (level × condition) scales abatement so better plants clear more waste at the same radius
- **Landfill capacity** — `MaxOccupants` (fallback `200 × level`) tracks fill via `Occupants`; utilization softens abatement above ~70% and near-full sites lose cleanup power
- **Recycling diversion** — quality × mean level diverts 5–85% of collected waste from landfill (higher depots prolong lifespan)
- **Overflow** — intake beyond remaining capacity dumps illegally (pollution spike on weak-coverage tiles) → `LandfillOverflowRate`
- Daily tick: uncovered R/C/O zones accumulate waste pollution; covered tiles abate (scaled by capacity)
- Monthly industrial pollution rewrite re-applies the waste window so coverage effects persist
- Environment satisfaction + health progression already read tile pollution; immigration multiplies by environment attractiveness (**0.55–1.45**)
- Unity ResourcesHud Waste line: `🗑️ {env%} · div {div%} · fill {util%}` (coverage + overflow in tooltip)

`CathedralUtilitiesTests` + `PopulationSystemTests` pin quality→more abatement / diversion, full landfill→overflow pollution, snapshot export, and pollution→satisfaction / immigration.

### 4.16 Live (Tier-2 sewage / water contamination)

```json
{
  "sewageCoverageFraction": 0.52,
  "meanWaterContamination": 0.2,
  "meanWaterQuality": 0.8
}
```

- Treatment plants (`ServiceSewage`, `1 << 14`) publish coverage; **plant quality** (level × condition) scales removal so better plants clear more contamination at the same radius
- Daily tick: uncovered R/C/O/I zones accumulate waterborne pollution; covered tiles abate and set `SewageConnection`
- Monthly industrial pollution rewrite re-applies the sewage window so coverage effects persist
- Health progression + environment satisfaction already read tile pollution; immigration multiplies by water-quality attractiveness (**0.55–1.45**)
- Unity ResourcesHud Sewage line: `💧 {quality%} · contam {contam%} · {cov%}`

`CathedralUtilitiesTests` + `PopulationSystemTests` pin quality→more treatment, plant tick→lower contamination, snapshot export, and water quality→satisfaction / immigration.

### 4.17 Live (Manning / CSO storm overflow)

```json
{
  "meanPipeUtilization": 0.62,
  "csoOverflowRate": 0.08,
  "stormRunoffLoad": 0.22
}
```

- **Manning capacity** — gravity velocity `V = (1/n)·R^(2/3)·S^(1/2)` (n=0.013, R≈0.35 m, slope ≥ 0.5% from elevation); plant quality scales capacity and **combined-storm fraction** (poor ≈ fully combined; excellent ≈ mostly separated)
- **Rational runoff** — `Q = C·i·A` with surface C (road 0.90 … park/forest/water low); intensity from `Precipitation` + Rain/Storm weather floors
- When dry sewage + combined storm &gt; capacity → **CSO** raises tile pollution (feeds water quality / health / immigration via existing sewage path)
- Unity ResourcesHud Sewage line: `💧 {quality%} · cso {cso%} · util {util%}` (coverage + runoff in tooltip)

`CathedralUtilitiesTests` pin Manning velocity/capacity, storm→overflow, better plants→less CSO, snapshot export.

### 4.18 Live (Tier-2 internet / telecom)

```json
{
  "internetCoverageFraction": 0.58,
  "meanInternetTier": 1.8,
  "meanTelecomAccess": 0.6
}
```

- Telecom hubs (`ServiceTelecom`, `1 << 15`) publish coverage; **hub quality** (level × condition) scales effective factor so better hubs raise `TileData.InternetConnection` tier (0=none, 1=copper, 2=fiber, 3=5G)
- **Tech gates:** copper always; **fiber** needs T044 Internet Infrastructure (modern); **5G** needs T045 5G/6G Networks (Future) — `TelecomNetwork.MaxUnlockedTier` clamps the quality-derived tier
- Daily tick: zoned R/C/O/I tiles rewrite connection from coverage × quality × unlock cap; aggregates export coverage / mean tier / access
- Services satisfaction reads home-tile `InternetConnection`; immigration multiplies by telecom attractiveness (**0.55–1.45**)
- Unity ResourcesHud Net line: `📡 {access%} · tier {tier}/3 · {cov%}` (tooltip notes T044/T045)

`CathedralUtilitiesTests` + `PopulationSystemTests` pin quality→higher tier, tech caps, hub tick→connection + aggregates, snapshot export, and telecom→satisfaction / immigration.

## 5. Out of scope (this stub)

- Rewriting tile BFS grids — keep daily ServiceSystem path
- Full insurance market / flood-risk premium coupling (fire rating mult is the v1 hook)
- Garbage truck route logistics / multi-depot transfer-station graphs (capacity + diversion are the thin landfill lifespan hooks)
- Per-pipe graph routing / pump-station elevation lifts (Manning/CSO uses local slope + plant quality capacity)
- Separated-sewer retrofit construction events (quality→combined fraction is the thin hook)