# Research: 12 Balance Difficulty Weather



# Iron & Oak — Complete Design Specifications

## 1. Difficulty System

All values are multipliers where 1.0 = Normal baseline.

| Parameter | Easy | Normal | Hard | Expert |
|-----------|------|--------|------|--------|
| Starting cash | 1.5 | 1.0 | 0.7 | 0.5 |
| Tax revenue | 1.2 | 1.0 | 0.85 | 0.7 |
| Construction cost | 0.75 | 1.0 | 1.3 | 1.6 |
| Maintenance cost | 0.7 | 1.0 | 1.25 | 1.5 |
| Disaster frequency | 0.5 | 1.0 | 1.5 | 2.0 |
| Immigration rate | 1.4 | 1.0 | 0.75 | 0.5 |
| Happiness decay | 0.6 | 1.0 | 1.4 | 1.8 |
| Loan interest | 0.7 | 1.0 | 1.3 | 1.6 |
| Research speed | 1.3 | 1.0 | 0.8 | 0.6 |
| Crime rate | 0.6 | 1.0 | 1.3 | 1.7 |

**Additional Easy mode features:** Advisor hints enabled by default, no bankruptcy game-over (instead treasury floors at -$5,000), buildings never collapse from neglect.

**Additional Expert mode features:** No loans available until a Bank is constructed, random starting resource deposits (may lack key resources), rival cities compete for immigrants.

---

## 2. Win Conditions and City Rating

### Scoring Components (0–100 total)

| Category | Weight | Metric | Calculation |
|----------|--------|--------|-------------|
| Population | 15 | Current pop vs era milestone | `min(15, 15 * (pop / milestone))` |
| Happiness | 20 | Weighted average citizen happiness | `20 * (avgHappiness / 100)` |
| Budget Health | 15 | Months in surplus over last 24 months | `15 * (surplusMonths / 24)` |
| Infrastructure | 15 | % of buildings within service coverage | `15 * (coveredBuildings / totalBuildings)` |
| Environment | 10 | Pollution index inverted (0=toxic, 100=pristine) | `10 * (envScore / 100)` |
| Culture | 10 | Unique cultural buildings / era target | `min(10, 10 * (culturalBuildings / target))` |
| Technology | 10 | Technologies researched / available | `10 * (researchedTech / availableTech)` |
| Trade Balance | 5 | Net exports over last 12 months | `5 * clamp(tradeBalance / targetBalance, 0, 1)` |

### Population Milestones by Era

| Era | Bronze | Silver | Gold | Platinum |
|-----|--------|--------|------|----------|
| Frontier (1850) | 200 | 500 | 1,000 | 2,000 |
| Industrial (1900) | 2,000 | 5,000 | 15,000 | 30,000 |
| Postwar (1945) | 15,000 | 40,000 | 80,000 | 150,000 |
| Modern (1980) | 50,000 | 150,000 | 300,000 | 500,000 |
| Future (2020+) | 200,000 | 500,000 | 750,000 | 1,000,000 |

### Cultural Building Targets by Era

| Era | Target count | Examples |
|-----|-------------|----------|
| Frontier | 3 | Church, Schoolhouse, Town Hall |
| Industrial | 6 | Theater, Library, Museum, Park, Opera House, University |
| Postwar | 8 | Stadium, Concert Hall, Art Gallery, Zoo, Civic Center, Aquarium, Botanical Garden, Public Pool |
| Modern | 10 | Convention Center, Amphitheater, Skate Park, Community Center, Science Museum, Film Studio, Observatory, Marina, Sculpture Garden, Cultural District |
| Future | 12 | All above + VR Arena, Vertical Park, Innovation Hub |

### Rating Thresholds

| Rating | Score Range | Unlock |
|--------|-----------|--------|
| Bronze | 40–59 | Next era transition allowed |
| Silver | 60–74 | +1 bonus scenario unlocked |
| Gold | 75–89 | Cosmetic city skin unlocked |
| Platinum | 90–100 | Sandbox modifier unlocked + achievement |

A score below 40 for 24 consecutive game months triggers an "Impeachment Warning." Below 25 for 12 consecutive months ends the game (Easy mode: warning only, no game over).

---

## 3. Weather Generation Model

All temperatures in degrees C. Precipitation probability is the chance of rain on any given day that month. Intensity is mm per rain day. Wind distribution sums to 1.0 (calm / moderate / strong). Day length in hours of sunlight.

### 3.1 Tropical

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | 22 | 31 | 0.55 | 18 | 0.50/0.40/0.10 | 11.8 |
| Feb | 22 | 31 | 0.50 | 16 | 0.50/0.40/0.10 | 12.0 |
| Mar | 23 | 32 | 0.45 | 14 | 0.55/0.35/0.10 | 12.1 |
| Apr | 23 | 33 | 0.40 | 12 | 0.55/0.35/0.10 | 12.3 |
| May | 23 | 33 | 0.50 | 16 | 0.45/0.40/0.15 | 12.5 |
| Jun | 22 | 32 | 0.65 | 22 | 0.40/0.40/0.20 | 12.6 |
| Jul | 22 | 32 | 0.70 | 24 | 0.35/0.40/0.25 | 12.6 |
| Aug | 22 | 32 | 0.70 | 26 | 0.35/0.40/0.25 | 12.4 |
| Sep | 22 | 32 | 0.65 | 24 | 0.40/0.40/0.20 | 12.2 |
| Oct | 22 | 32 | 0.60 | 20 | 0.45/0.40/0.15 | 12.0 |
| Nov | 22 | 31 | 0.55 | 18 | 0.50/0.40/0.10 | 11.8 |
| Dec | 22 | 31 | 0.55 | 18 | 0.50/0.40/0.10 | 11.7 |

**Seasonal events:**
- Hurricane: Jun–Nov, probability 0.03 per month (cumulative ~17% per season)
- Flooding: Jun–Oct, probability 0.05 per month when precip > 20mm
- Heatwave: Mar–May, probability 0.04 per month

### 3.2 Subtropical

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | 8 | 18 | 0.30 | 10 | 0.50/0.35/0.15 | 10.2 |
| Feb | 9 | 19 | 0.30 | 10 | 0.50/0.35/0.15 | 10.8 |
| Mar | 12 | 23 | 0.35 | 12 | 0.50/0.40/0.10 | 11.8 |
| Apr | 15 | 26 | 0.35 | 12 | 0.55/0.35/0.10 | 12.8 |
| May | 19 | 30 | 0.40 | 14 | 0.50/0.40/0.10 | 13.5 |
| Jun | 22 | 33 | 0.50 | 16 | 0.45/0.40/0.15 | 14.0 |
| Jul | 24 | 35 | 0.55 | 18 | 0.40/0.40/0.20 | 13.8 |
| Aug | 24 | 35 | 0.55 | 18 | 0.40/0.40/0.20 | 13.2 |
| Sep | 21 | 32 | 0.45 | 14 | 0.45/0.40/0.15 | 12.4 |
| Oct | 17 | 28 | 0.35 | 12 | 0.50/0.40/0.10 | 11.5 |
| Nov | 12 | 23 | 0.30 | 10 | 0.50/0.35/0.15 | 10.6 |
| Dec | 9 | 19 | 0.30 | 10 | 0.50/0.35/0.15 | 10.1 |

**Seasonal events:**
- Hurricane: Jun–Oct, probability 0.025 per month
- Heatwave: Jun–Aug, probability 0.06 per month
- Ice storm: Dec–Feb, probability 0.02 per month
- Tornado: Mar–May, probability 0.015 per month

### 3.3 Temperate

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | -2 | 5 | 0.40 | 8 | 0.35/0.45/0.20 | 8.5 |
| Feb | -1 | 6 | 0.38 | 8 | 0.35/0.45/0.20 | 9.5 |
| Mar | 2 | 11 | 0.38 | 9 | 0.40/0.45/0.15 | 11.0 |
| Apr | 5 | 15 | 0.35 | 9 | 0.45/0.40/0.15 | 12.8 |
| May | 9 | 20 | 0.35 | 10 | 0.50/0.40/0.10 | 14.2 |
| Jun | 13 | 24 | 0.35 | 10 | 0.55/0.35/0.10 | 15.2 |
| Jul | 15 | 26 | 0.30 | 10 | 0.55/0.35/0.10 | 15.5 |
| Aug | 14 | 25 | 0.32 | 10 | 0.55/0.35/0.10 | 14.5 |
| Sep | 11 | 21 | 0.35 | 10 | 0.50/0.40/0.10 | 13.0 |
| Oct | 7 | 15 | 0.38 | 10 | 0.45/0.40/0.15 | 11.2 |
| Nov | 3 | 9 | 0.40 | 10 | 0.40/0.40/0.20 | 9.5 |
| Dec | 0 | 6 | 0.42 | 9 | 0.35/0.45/0.20 | 8.3 |

**Seasonal events:**
- Blizzard: Dec–Feb, probability 0.04 per month
- Flooding: Mar–Apr (snowmelt), probability 0.03 per month
- Heatwave: Jun–Aug, probability 0.03 per month
- Fog: Oct–Dec, probability 0.08 per month (cosmetic + minor traffic penalty)

### 3.4 Continental

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | -18 | -6 | 0.25 | 5 | 0.30/0.40/0.30 | 7.5 |
| Feb | -16 | -4 | 0.22 | 4 | 0.30/0.40/0.30 | 8.8 |
| Mar | -8 | 3 | 0.28 | 6 | 0.35/0.40/0.25 | 10.5 |
| Apr | 0 | 12 | 0.30 | 8 | 0.40/0.40/0.20 | 12.5 |
| May | 6 | 20 | 0.35 | 10 | 0.45/0.40/0.15 | 14.5 |
| Jun | 12 | 26 | 0.40 | 12 | 0.50/0.40/0.10 | 16.0 |
| Jul | 15 | 28 | 0.38 | 12 | 0.55/0.35/0.10 | 16.5 |
| Aug | 13 | 26 | 0.35 | 10 | 0.55/0.35/0.10 | 15.0 |
| Sep | 7 | 19 | 0.30 | 8 | 0.50/0.40/0.10 | 13.0 |
| Oct | 1 | 10 | 0.28 | 7 | 0.40/0.40/0.20 | 11.0 |
| Nov | -7 | 1 | 0.28 | 6 | 0.35/0.40/0.25 | 9.0 |
| Dec | -15 | -5 | 0.25 | 5 | 0.30/0.40/0.30 | 7.2 |

**Seasonal events:**
- Blizzard: Nov–Mar, probability 0.06 per month
- Spring flood: Apr–May, probability 0.05 per month
- Heatwave: Jul–Aug, probability 0.04 per month
- Permafrost thaw: May (if Future era), probability 0.03

### 3.5 Mediterranean

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | 5 | 14 | 0.40 | 14 | 0.40/0.40/0.20 | 9.5 |
| Feb | 6 | 15 | 0.35 | 12 | 0.40/0.40/0.20 | 10.3 |
| Mar | 8 | 18 | 0.30 | 10 | 0.45/0.40/0.15 | 11.5 |
| Apr | 10 | 21 | 0.22 | 8 | 0.50/0.40/0.10 | 13.0 |
| May | 14 | 26 | 0.12 | 5 | 0.55/0.35/0.10 | 14.2 |
| Jun | 18 | 31 | 0.05 | 3 | 0.60/0.30/0.10 | 15.0 |
| Jul | 21 | 34 | 0.02 | 2 | 0.65/0.25/0.10 | 14.8 |
| Aug | 21 | 34 | 0.03 | 3 | 0.65/0.25/0.10 | 14.0 |
| Sep | 18 | 30 | 0.10 | 6 | 0.55/0.35/0.10 | 12.5 |
| Oct | 14 | 24 | 0.25 | 10 | 0.45/0.40/0.15 | 11.2 |
| Nov | 9 | 18 | 0.35 | 12 | 0.40/0.40/0.20 | 9.8 |
| Dec | 6 | 14 | 0.40 | 14 | 0.40/0.40/0.20 | 9.3 |

**Seasonal events:**
- Wildfire: Jun–Sep, probability 0.05 per month (higher if no rain 2+ consecutive months)
- Drought: Jun–Sep, probability 0.04 per month
- Flash flood: Oct–Nov, probability 0.03 per month (abrupt rain after dry season)
- Heatwave: Jul–Aug, probability 0.06 per month

### 3.6 Arid

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | 5 | 18 | 0.08 | 4 | 0.40/0.40/0.20 | 10.0 |
| Feb | 7 | 20 | 0.08 | 4 | 0.40/0.40/0.20 | 10.8 |
| Mar | 11 | 25 | 0.06 | 3 | 0.45/0.35/0.20 | 11.8 |
| Apr | 16 | 31 | 0.04 | 2 | 0.45/0.35/0.20 | 12.8 |
| May | 21 | 37 | 0.02 | 2 | 0.40/0.35/0.25 | 13.5 |
| Jun | 25 | 42 | 0.01 | 1 | 0.35/0.35/0.30 | 14.0 |
| Jul | 28 | 44 | 0.02 | 2 | 0.35/0.35/0.30 | 13.8 |
| Aug | 27 | 43 | 0.03 | 3 | 0.35/0.35/0.30 | 13.2 |
| Sep | 23 | 38 | 0.03 | 3 | 0.40/0.35/0.25 | 12.3 |
| Oct | 17 | 31 | 0.05 | 3 | 0.45/0.35/0.20 | 11.3 |
| Nov | 10 | 24 | 0.06 | 3 | 0.40/0.40/0.20 | 10.3 |
| Dec | 6 | 19 | 0.08 | 4 | 0.40/0.40/0.20 | 9.8 |

**Seasonal events:**
- Sandstorm: Mar–Jun, probability 0.06 per month (reduces visibility, damages exposed machinery)
- Drought: Apr–Sep, probability 0.08 per month (cumulative water crisis)
- Heatwave: Jun–Aug, probability 0.10 per month
- Flash flood: Jul–Sep, probability 0.02 per month (monsoon burst)

### 3.7 Arctic

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | -30 | -18 | 0.15 | 3 | 0.20/0.35/0.45 | 3.0 |
| Feb | -28 | -16 | 0.15 | 3 | 0.20/0.35/0.45 | 5.0 |
| Mar | -22 | -10 | 0.18 | 4 | 0.25/0.40/0.35 | 8.0 |
| Apr | -12 | -2 | 0.20 | 5 | 0.30/0.40/0.30 | 12.0 |
| May | -3 | 5 | 0.22 | 5 | 0.35/0.40/0.25 | 16.0 |
| Jun | 3 | 12 | 0.25 | 6 | 0.40/0.40/0.20 | 20.0 |
| Jul | 6 | 15 | 0.28 | 7 | 0.40/0.40/0.20 | 21.0 |
| Aug | 4 | 12 | 0.28 | 7 | 0.40/0.40/0.20 | 17.0 |
| Sep | 0 | 6 | 0.25 | 6 | 0.35/0.40/0.25 | 13.0 |
| Oct | -10 | -2 | 0.20 | 5 | 0.25/0.40/0.35 | 9.0 |
| Nov | -22 | -12 | 0.18 | 4 | 0.20/0.35/0.45 | 5.0 |
| Dec | -28 | -17 | 0.15 | 3 | 0.20/0.35/0.45 | 2.5 |

**Seasonal events:**
- Blizzard: Oct–Apr, probability 0.08 per month
- Polar night depression: Nov–Jan, happiness penalty -5 per month
- Ice breakup flood: May–Jun, probability 0.04 per month
- Aurora (cosmetic/happiness bonus): Oct–Mar, probability 0.15 per month, +2 happiness

### 3.8 Oceanic

| Month | Temp Min | Temp Max | Precip Prob | Precip mm | Wind C/M/S | Day Length |
|-------|----------|----------|-------------|-----------|------------|------------|
| Jan | 3 | 8 | 0.55 | 10 | 0.25/0.45/0.30 | 8.0 |
| Feb | 3 | 9 | 0.50 | 9 | 0.30/0.45/0.25 | 9.2 |
| Mar | 4 | 11 | 0.48 | 8 | 0.35/0.45/0.20 | 11.0 |
| Apr | 6 | 14 | 0.42 | 7 | 0.40/0.45/0.15 | 13.0 |
| May | 9 | 17 | 0.38 | 7 | 0.45/0.40/0.15 | 14.8 |
| Jun | 12 | 20 | 0.35 | 7 | 0.50/0.40/0.10 | 16.0 |
| Jul | 14 | 22 | 0.32 | 7 | 0.50/0.40/0.10 | 16.3 |
| Aug | 14 | 21 | 0.35 | 7 | 0.50/0.40/0.10 | 15.0 |
| Sep | 12 | 19 | 0.40 | 8 | 0.40/0.45/0.15 | 13.0 |
| Oct | 9 | 15 | 0.48 | 9 | 0.35/0.45/0.20 | 11.0 |
| Nov | 5 | 11 | 0.52 | 10 | 0.30/0.45/0.25 | 9.0 |
| Dec | 4 | 9 | 0.55 | 10 | 0.25/0.45/0.30 | 7.8 |

**Seasonal events:**
- Storm surge: Oct–Feb, probability 0.04 per month (coastal flooding)
- Persistent fog: Oct–Jan, probability 0.10 per month (traffic/port penalty)
- Gale winds: Nov–Mar, probability 0.05 per month (construction halted outdoors)
- Mild winter bloom: Feb–Mar, probability 0.08 (early agriculture bonus)

### Weather Event Impact Table

| Event | Duration (days) | Effect |
|-------|----------------|--------|
| Heatwave | 5–14 | -10 happiness, +15% water usage, +5% fire risk, outdoor workers -30% productivity |
| Blizzard | 2–5 | All construction halted, road speed -50%, +20% heating cost, chance of pipe burst |
| Hurricane | 1–3 | Destroys 1–5 buildings (random), floods coastal tiles, all activity halted |
| Drought | 30–90 | Agriculture yield -50%, water cost +100%, fire risk +25%, happiness -5/month |
| Flooding | 3–10 | Damages buildings in flood zone, road closures, disease risk +10% |
| Sandstorm | 1–3 | Solar panels -80%, outdoor work halted, building damage to exposed structures |
| Wildfire | 3–14 | Destroys forests/buildings in path, air quality -40, evacuations |
| Tornado | 0.1 (2–3 hours) | Destroys 1–3 buildings in path, random trajectory |

---

## 4. Map Generation Algorithm

### 4.1 Terrain Elevation (Simplex Noise)

Base terrain uses layered 2D simplex noise.

| Layer | Octaves | Base Frequency | Amplitude | Lacunarity | Persistence |
|-------|---------|---------------|-----------|------------|-------------|
| Continental | 1 | 0.003 | 1.0 | — | — |
| Regional | 4 | 0.008 | 0.6 | 2.0 | 0.5 |
| Local detail | 6 | 0.025 | 0.15 | 2.2 | 0.45 |
| Micro detail | 3 | 0.08 | 0.04 | 2.0 | 0.5 |

**Elevation mapping:**
- `finalElevation = continental * 0.5 + regional * 0.3 + local * 0.15 + micro * 0.05`
- Normalize to range [0, 1]
- Water threshold: < 0.35
- Beach/wetland: 0.35–0.40
- Flat buildable: 0.40–0.60
- Hill: 0.60–0.75
- Mountain: 0.75–0.90
- Peak (unbuildable): > 0.90

### 4.2 Water Body Generation

**Rivers:**
1. Pick 1–3 source points at elevation > 0.75
2. From each source, simulate downhill flow using gradient descent on the elevation map
3. At each step, move to the lowest neighboring tile (8-directional), with 10% chance of random deviation (meander)
4. River width starts at 1 tile, increases by 1 tile every 40 tiles of length
5. Rivers always terminate at the map edge or a lake/ocean
6. If the river reaches a local minimum before the edge, flood-fill to create a lake (max 50 tiles)

**Lakes:**
- Generated at local elevation minima with elevation 0.38–0.45
- Size: Poisson-distributed, mean = 20 tiles, min = 5, max = 80
- Shape: flood-fill from center using elevation < threshold + random(0, 0.02)

**Coastline (for Coastal and River Delta maps):**
- One map edge designated as ocean (elevation forced to 0.0 for 15–30% of map width)
- Coastline noise: apply 1D simplex noise (frequency 0.02, amplitude 8 tiles) to the border between land and sea
- Beach zone: 2–4 tiles of transition terrain

### 4.3 Forest Cluster Algorithm

Uses Poisson disc sampling for cluster centers, then growth.

**Parameters:**
- Minimum distance between cluster centers: 12 tiles
- Cluster radius: 5–20 tiles (uniform random)
- Tree density within cluster: 0.6–0.9 (probability per tile)
- Only on elevation 0.40–0.75 (not in water, not on peaks)
- Forest avoids a 3-tile buffer from rivers/lakes (riparian zone — different visual, still buildable)
- Total forest coverage target: 15–35% of land area

**Growth algorithm per cluster:**
1. Place center via Poisson disc
2. For each tile within radius: `placementChance = density * (1 - (dist / radius)^2)`
3. If random() < placementChance and tile is valid terrain, place tree

### 4.4 Resource Deposit Placement

| Resource | Valid Terrain | Cluster Size | Clusters per Map | Min Spacing |
|----------|-------------|-------------|-----------------|-------------|
| Iron ore | Hill, Mountain | 8–20 tiles | 2–5 | 30 tiles |
| Coal | Hill, Flat | 10–25 tiles | 2–4 | 25 tiles |
| Stone | Hill, Mountain | 6–15 tiles | 3–6 | 20 tiles |
| Fertile soil | Flat, near water | 15–40 tiles | 3–7 | 15 tiles |
| Oil | Flat, Arid only | 5–12 tiles | 1–3 | 40 tiles |
| Gold | Mountain | 3–8 tiles | 0–2 | 50 tiles |
| Timber | Forest | colocated with forest clusters | — | — |
| Fresh water | River/Lake adjacent | 2-tile band along water | — | — |

**Placement algorithm:**
1. After terrain and water generation, identify valid tiles per resource
2. Place cluster centers using weighted random (prefer tiles deeper into valid terrain)
3. Grow clusters using flood-fill with random edge removal
4. Guarantee: every map has at least 1 iron, 1 coal, 1 stone, 2 fertile soil deposits

### 4.5 Map Type Parameters

| Parameter | Valley | Coastal | Mountain | Plains | River Delta |
|-----------|--------|---------|----------|--------|-------------|
| Continental frequency | 0.004 | 0.003 | 0.005 | 0.002 | 0.003 |
| Continental amplitude | 1.2 | 0.8 | 1.5 | 0.4 | 0.6 |
| Water threshold | 0.30 | 0.38 | 0.28 | 0.32 | 0.40 |
| River count | 1 | 1–2 | 2–3 | 0–1 | 3–5 |
| Ocean edge | No | 1 edge | No | No | 1 edge |
| Mountain coverage | 15–25% | 5–10% | 35–50% | 0–3% | 0–5% |
| Flat land | 40–55% | 50–65% | 20–35% | 75–90% | 55–70% |
| Forest coverage | 25–35% | 15–25% | 20–30% | 10–20% | 15–25% |
| Min buildable area | 45% | 55% | 25% | 80% | 50% |
| Max buildable area | 65% | 75% | 45% | 95% | 75% |

**Valley-specific:** Apply a V-shaped elevation mask (low center band, high edges). The river runs through the valley floor.

**Coastal-specific:** Force one edge to ocean. Apply exponential elevation ramp from coast inland.

**Mountain-specific:** Raise regional amplitude. Guarantee at least one flat valley (force a 30x30 tile area to elevation 0.45–0.55).

**Plains-specific:** Reduce all noise amplitudes by 50%. Flatten everything between 0.42–0.58.

**River Delta-specific:** Force 3–5 rivers converging at the ocean edge. Increase wetland zone (0.35–0.42) to 15–25% of map. Rivers branch at elevation < 0.42 (split into 2 channels with 30% probability per tile).

### 4.6 Playability Validation

After generation, run these checks. If any fail, regenerate with a new seed (max 10 attempts before relaxing constraints by 10%).

1. **Water access:** At least 5% of land tiles are adjacent to fresh water (river or lake)
2. **Flat land minimum:** Buildable flat terrain >= map type minimum from table above
3. **Connectivity:** All flat land tiles are reachable from each other (flood-fill from largest flat region must cover > 90% of all flat tiles)
4. **Resource guarantee:** At least 1 iron, 1 coal, 1 stone, 2 fertile soil deposits exist
5. **Starting zone:** A contiguous flat area of at least 20x20 tiles exists within 30 tiles of a water source — this becomes the default camera position and suggested build site
6. **Edge access:** At least 2 map edges have flat land touching them (for trade routes)

---

## 5. Game Speed and Time Mapping

| Speed | Real seconds per game day | Real minutes per game month (30 days) | Real minutes per game year |
|-------|--------------------------|---------------------------------------|---------------------------|
| Pause | ∞ | ∞ | ∞ |
| 1x | 2.0 | 1.0 | 12.0 |
| 2x | 1.0 | 0.5 | 6.0 |
| 3x | 0.5 | 0.25 | 3.0 |

### Session Timing

| Scope | In-game years | At 2x avg speed | Notes |
|-------|--------------|-----------------|-------|
| Single era (~40 years) | 40 | ~4 hours | Core session unit |
| Full game (1850–2050) | 200 | ~20 hours | Extended campaign |
| Target complete playthrough | 200 | 15–25 hours | Accounts for pausing, planning |
| Scenario (standalone) | 10–30 | 1–3 hours | Quick session |

**Tick system:**
- Simulation tick: 100ms real time (10 ticks/second at 1x speed)
- At 1x: 1 game day = 20 ticks
- At 2x: 1 game day = 10 ticks
- At 3x: 1 game day = 5 ticks
- UI updates: every tick (smooth animations)
- Economy/population calculation: once per game day
- Building construction progress: once per game day
- Research progress: once per game day
- Budget summary: once per game month
- Season change: every 3 game months
- Era transition check: once per game year

---

## 6. Economic Balance Parameters

All monetary values are in game currency units ("$" — represents period-appropriate purchasing power, not inflation-adjusted real dollars).

### Starting Conditions by Era

| Parameter | Frontier (1850) | Industrial (1900) | Postwar (1945) | Modern (1980) | Future (2020+) |
|-----------|----------------|-------------------|----------------|---------------|----------------|
| Population | 50 | 500 | 5,000 | 25,000 | 100,000 |
| Starting cash | $10,000 | $75,000 | $500,000 | $3,000,000 | $15,000,000 |
| Tax rate default | 6% | 8% | 10% | 12% | 10% |
| Average citizen income/mo | $30 | $80 | $250 | $800 | $2,500 |
| Road cost/tile | $20 | $50 | $150 | $500 | $1,200 |
| Basic house cost | $200 | $800 | $3,000 | $15,000 | $50,000 |
| Commercial building | $500 | $2,000 | $8,000 | $40,000 | $120,000 |
| Industrial building | $800 | $3,500 | $12,000 | $60,000 | $180,000 |
| School | $1,500 | $5,000 | $20,000 | $100,000 | $300,000 |
| Hospital | — | $8,000 | $35,000 | $200,000 | $600,000 |
| Police station | $600 | $3,000 | $10,000 | $50,000 | $150,000 |
| Fire station | $400 | $2,500 | $8,000 | $40,000 | $120,000 |
| Park | $100 | $500 | $2,000 | $10,000 | $30,000 |
| Power plant | — | $10,000 | $50,000 | $250,000 | $800,000 |
| Water treatment | — | $6,000 | $25,000 | $120,000 | $400,000 |
| Monthly maintenance (% of construction cost) | 1.5% | 1.2% | 1.0% | 0.8% | 0.6% |
| Loan max (% of city value) | 20% | 25% | 30% | 35% | 40% |
| Loan interest (annual) | 8% | 6% | 5% | 4% | 3% |

### Tax Revenue Formula

```
monthlyTaxRevenue = population * avgIncome * taxRate * happinessMultiplier * difficultyMultiplier

happinessMultiplier:
  happiness >= 80: 1.15 (citizens earn more in happy cities)
  happiness 60-79: 1.0
  happiness 40-59: 0.85
  happiness 20-39: 0.65
  happiness < 20:  0.40 (mass unemployment, underground economy)
```

### Commercial/Industrial Revenue

```
commercialRevenue = commercialBuildings * avgIncome * 0.3 * occupancyRate * landValueMultiplier
industrialRevenue = industrialBuildings * avgIncome * 0.5 * occupancyRate * resourceAccessMultiplier

landValueMultiplier: 0.5 (remote) to 2.0 (prime location near services and transport)
resourceAccessMultiplier: 0.3 (no resources nearby) to 1.5 (on resource deposit with transport)
```

---

## 7. Growth Rate Curves

### Population Growth Formula

```
monthlyGrowth = naturalGrowth + immigration - emigration - deaths

naturalGrowth = population * naturalRate * seasonMultiplier
immigration = baseImmigration * attractionScore * difficultyMultiplier
emigration = population * emigrationRate * (1 - happiness/100)
deaths = population * deathRate * healthcareMultiplier
```

### Natural Growth Rate by Era

| Era | Max natural rate (monthly) | Base death rate (monthly) | Notes |
|-----|--------------------------|--------------------------|-------|
| Frontier | 0.008 | 0.005 | High birth, high death |
| Industrial | 0.007 | 0.004 | Better medicine |
| Postwar | 0.006 | 0.002 | Baby boom potential |
| Modern | 0.004 | 0.0015 | Lower birth rates |
| Future | 0.003 | 0.001 | Aging population |

### Immigration System

```
attractionScore = (
  0.25 * (happiness / 100) +
  0.20 * (jobAvailability) +        // unoccupied job slots / total slots, capped at 1.0
  0.15 * (housingAvailability) +     // unoccupied homes / total homes, capped at 1.0
  0.15 * (serviceCoverage) +         // % of population within service radius
  0.10 * (taxCompetitiveness) +      // 1.0 at 8%, linear scale
  0.10 * (environmentScore / 100) +
  0.05 * (culturalScore / 100)
)
```

| Era | Base monthly immigration cap | Attraction score threshold to receive any |
|-----|----------------------------|------------------------------------------|
| Frontier | 10 | 0.3 |
| Industrial | 50 | 0.35 |
| Postwar | 200 | 0.4 |
| Modern | 500 | 0.4 |
| Future | 1,000 | 0.45 |

### Growth Phases

**Exponential growth triggers** (all must be true):
- Happiness > 70
- Housing availability > 20%
- Job availability > 15%
- Service coverage > 60%
- No active disasters
- Growth formula applies with full immigration cap

**Linear growth** (default state):
- Happiness 40–70
- Basic services covered
- Immigration = attractionScore * cap * 0.5

**Stagnation triggers** (any one sufficient):
- Happiness < 40
- Housing availability < 5%
- Job availability < 5%
- Immigration drops to 0, natural growth halved

**Death spiral conditions** (city is unrecoverable when ALL are true for 12+ months):
- Population < 50% of peak
- Happiness < 20
- Treasury negative for 6+ consecutive months
- No active construction projects
- On Easy/Normal: warning displayed. On Hard/Expert: game over after 12 months of death spiral.

### S-Curve Carrying Capacity

```
carryingCapacity = baseCapacity * serviceCoverageMultiplier

baseCapacity = (residentialSlots * 1.0)  // hard cap: can't exceed housing

serviceCoverageMultiplier:
  if waterCoverage < 1.0: *= waterCoverage     // water is most critical
  if healthcareCoverage < 1.0: *= (0.5 + healthcareCoverage * 0.5)
  if educationCoverage < 1.0: *= (0.7 + educationCoverage * 0.3)
  if fireCoverage < 1.0: *= (0.8 + fireCoverage * 0.2)
  if policeCoverage < 1.0: *= (0.85 + policeCoverage * 0.15)

actualGrowthRate = maxGrowthRate * (1 - population / carryingCapacity)
```

When population approaches carrying capacity, growth asymptotically approaches zero. The player must build more housing and services to raise the cap.

---

## 8. Scenario Definitions

### Scenario 1: "The Dust Bowl"

| Property | Value |
|----------|-------|
| Map type | Plains |
| Climate | Arid |
| Starting era | Frontier (1850) |
| Starting population | 300 |
| Starting cash | $8,000 |
| Win condition | Maintain population above 200 AND achieve food self-sufficiency (agricultural output >= population * 2 food units/month) for 12 consecutive months |
| Time limit | 15 years (1850–1865) |
| Difficulty rating | 3/5 (Hard) |
| Special rules | Drought event fires every 6–18 months (random interval) lasting 2–4 months. Fertile soil deposits are 50% smaller than normal. Water sources may dry up during drought (river flow -40%). No irrigation technology available until Year 5. Trade caravans arrive 50% less frequently. |
| Starting conditions | 3 farms already built, 1 well, 1 general store. Two of the farms are on marginal soil (70% normal yield). |
| Fail condition | Population drops below 100 at any point OR treasury goes below -$2,000 |

### Scenario 2: "Gridlock"

| Property | Value |
|----------|-------|
| Map type | Valley |
| Climate | Temperate |
| Starting era | Modern (1980) |
| Starting population | 80,000 |
| Starting cash | $2,000,000 |
| Win condition | Reduce average commute time from 45 minutes to under 15 minutes without demolishing any building in the Historic Core zone (marked on map, ~200 buildings) |
| Time limit | 10 years |
| Difficulty rating | 4/5 (Very Hard) |
| Special rules | Historic Core is a 40x40 tile zone in the city center — buildings cannot be demolished or modified. Roads in Historic Core cannot be widened. Public transit construction costs 30% less (government grant). Traffic simulation runs at high fidelity (individual agent pathfinding). Every demolished building outside Historic Core costs $50,000 in demolition + triggers a -3 happiness event ("neighborhood character lost"). |
| Starting conditions | Pre-built city with: 60% residential (suburbs), 30% commercial (downtown), 10% industrial (ring road). Only 2-lane roads everywhere. No public transit. 3 highways entering from map edges all funnel into downtown. |
| Fail condition | Average commute exceeds 60 minutes (infrastructure collapse) OR any Historic Core building demolished |

### Scenario 3: "The Merger"

| Property | Value |
|----------|-------|
| Map type | River Delta |
| Climate | Subtropical |
| Starting era | Postwar (1945) |
| Starting population | 12,000 (Town A: 7,000, Town B: 5,000) |
| Starting cash | $400,000 |
| Win condition | Merge both towns into one unified city: connected road network, shared services covering both populations, single positive budget, and average happiness > 60 across both populations |
| Time limit | 20 years |
| Difficulty rating | 3/5 (Hard) |
| Special rules | The two towns are separated by a river (bridge cost: $80,000). Each town has its own independent services (2 sets of everything). Town A residents have -15 happiness toward Town B services and vice versa for the first 5 years ("rival loyalty" — decays 3/year). Duplicate services cost double maintenance until unified. Player controls both towns from the start but budget is merged. Residents will not cross the river for work until a bridge exists. |
| Starting conditions | Town A: industrial economy, 2 factories, 1 school, 1 fire station, basic grid roads. Town B: agricultural economy, 5 farms, 1 school, 1 church, sparse roads. No bridge. One ferry crossing (capacity 50 people/day). |
| Fail condition | Either town's population drops below 2,000 OR happiness in either town drops below 25 |

### Scenario 4: "Green New Deal"

| Property | Value |
|----------|-------|
| Map type | Coastal |
| Climate | Oceanic |
| Starting era | Modern (1980), transitions to Future |
| Starting population | 150,000 |
| Starting cash | $10,000,000 |
| Win condition | Reduce carbon emissions to zero (0 fossil fuel power, 0 fossil fuel industry, all vehicles electric or public transit) while maintaining population above 140,000 and happiness above 55 |
| Time limit | 30 years (1980–2010) |
| Difficulty rating | 4/5 (Very Hard) |
| Special rules | Start with 3 coal power plants and 2 oil refineries providing 80% of city energy and 40% of jobs. Closing a fossil fuel plant eliminates 500 jobs instantly. Renewable energy costs 2x normal for the first 10 years (technology immaturity), then 1x. Federal carbon tax kicks in at Year 10: $500/month per fossil fuel building, increasing by $200/year. Green tech research available immediately. Retraining center building (new type): converts industrial workers to green sector over 6 months, costs $200,000, capacity 200 workers. |
| Starting conditions | Heavy industry city. 3 coal plants, 2 refineries, 8 factories (4 use fossil fuels), extensive road network, no bike lanes, no public transit, 1 small wind farm (5% of energy). Air quality score: 35/100. |
| Fail condition | Population drops below 120,000 OR city goes bankrupt |

### Scenario 5: "The Commuter Problem"

| Property | Value |
|----------|-------|
| Map type | Plains |
| Climate | Continental |
| Starting era | Modern (1980) |
| Starting population | 200,000 |
| Starting cash | $5,000,000 |
| Win condition | Average commute time under 15 minutes for 6 consecutive months |
| Time limit | 15 years |
| Difficulty rating | 3/5 (Hard) |
| Special rules | City is a classic bedroom community: 70% residential in sprawling suburbs, commercial/industrial concentrated in a small downtown core and an edge-of-city industrial park. Starting average commute: 45 minutes. All existing roads are 2-lane. Highway construction costs 2x normal (NIMBY opposition — each highway segment within 10 tiles of residential triggers a happiness event: -2 happiness for affected residents for 12 months). Telecommuting tech becomes available at Year 8 (reduces commute need for 20% of office workers). Mixed-use zoning unlocked from start (normally a later-era feature). |
| Starting conditions | Sprawl city layout. Downtown: 15x15 tiles of commercial. Industrial park: 20x10 tiles on east edge. Residential everywhere else. Two 2-lane roads connect suburbs to downtown. No rail. Bus system with 3 routes covering 15% of the city. |
| Fail condition | Average commute exceeds 50 minutes (people leave) — triggers 5% monthly emigration |

### Scenario 6: "Rust Belt Revival"

| Property | Value |
|----------|-------|
| Map type | Valley |
| Climate | Continental |
| Starting era | Modern (1980) |
| Starting population | 120,000 (declining — losing 200/month) |
| Starting cash | $3,000,000 |
| Win condition | Reverse population decline (3 consecutive years of net positive growth) AND unemployment below 8% AND budget surplus for 12 consecutive months |
| Time limit | 25 years |
| Difficulty rating | 4/5 (Very Hard) |
| Special rules | 5 factories close over the first 3 years (one every ~7 months), each eliminating 1,000 jobs. Abandoned factories become blight zones (-10 land value in 8-tile radius, +5% crime rate). Demolishing a factory costs $100,000. Converting a factory to mixed-use costs $300,000 but takes 18 months. University construction unlocked from start (normally Industrial era+). Tech startup building (new type): costs $150,000, employs 200, requires university within 15 tiles. Tourism buildings get 1.5x effectiveness if placed near the river. Federal revitalization grant: $500,000 at Year 5 if city has a university. |
| Starting conditions | Industrial city past its prime. 8 factories (5 will close), 2 steel mills (1 will close at Year 5), worker housing surrounding factories, downtown with 30% vacancy, 1 community college, aging infrastructure (roads at 40% condition, water system at 50%). |
| Fail condition | Population drops below 60,000 OR unemployment exceeds 25% |

### Scenario 7: "Silver Tsunami"

| Property | Value |
|----------|-------|
| Map type | Coastal |
| Climate | Mediterranean |
| Starting era | Future (2020) |
| Starting population | 250,000 |
| Starting cash | $12,000,000 |
| Win condition | Healthcare coverage > 95% AND elderly happiness > 65 AND budget balanced AND elderly care facility occupancy < 90% (not overcrowded), sustained for 24 consecutive months |
| Time limit | 20 years |
| Difficulty rating | 3/5 (Hard) |
| Special rules | Population age distribution: 35% over 65 (vs normal 15%), growing 1% per year. Elderly citizens require 3x healthcare service capacity. Elderly citizens pay 50% less tax (retirement income). New building types unlocked: Senior Living Community ($400,000, houses 200, requires adjacent park), Geriatric Hospital ($800,000, 2x coverage of normal hospital for elderly), Community Health Clinic ($150,000, preventive care reduces hospital load by 15%), Autonomous Transit ($200,000/route, door-to-door service for mobility-impaired). Young worker immigration bonus: for every 2 elderly care jobs created, attract 1 young immigrant family. |
| Starting conditions | Affluent retirement city. 12 residential zones (4 senior-heavy), 3 hospitals (at 95% capacity), 2 parks, commercial downtown, small tech sector. No geriatric facilities. Public transit covers 40% of city. Property values high (expensive to build). |
| Fail condition | Healthcare coverage drops below 60% for 6 months OR elderly happiness drops below 30 |

### Scenario 8: "Boomtown"

| Property | Value |
|----------|-------|
| Map type | Plains transitioning to Arid |
| Climate | Arid |
| Starting era | Industrial (1900) |
| Starting population | 800 |
| Starting cash | $50,000 |
| Win condition | Grow to 20,000 population AND have a diversified economy (no single sector > 40% of jobs) AND maintain happiness above 50 AND have $200,000 in treasury |
| Time limit | 20 years |
| Difficulty rating | 2/5 (Moderate) |
| Special rules | Oil discovery at game start: 3 large oil deposits revealed on map. Oil well cost: $5,000, generates $2,000/month revenue. Oil boom triggers massive immigration: +50 people/month for the first 5 years (triple normal). But: housing demand outstrips supply (homelessness mechanic — unhappy unsheltered workers). Crime rate +30% base during boom years. Oil price crash at Year 8–10 (random): oil revenue drops 60% for 18 months. Second crash at Year 15–18 (guaranteed): oil revenue drops 80% permanently (wells depleting). Water scarcity: only 2 water sources on map, population > 10,000 requires water infrastructure investment. Saloon building (era-specific): +5 happiness, +8% crime rate. |
| Starting conditions | Tiny frontier town. 1 general store, 1 saloon, 1 sheriff's office, 1 church, 15 houses. Railroad track crosses the north edge of the map (trade access). Oil deposits visible but not yet drilled. |
| Fail condition | Population drops below 500 after reaching 5,000 (bust without recovery) OR crime rate exceeds 50% (lawlessness) |

---

That completes all eight specification sections with exact numerical parameters ready for implementation. Each system is internally consistent: difficulty multipliers apply to the economic parameters in Section 6, growth curves in Section 7 use the immigration caps and happiness thresholds defined throughout, weather events from Section 3 interact with the disaster frequency multiplier from Section 1, and scenario starting conditions reference the era defaults from Section 6 with scenario-specific overrides.