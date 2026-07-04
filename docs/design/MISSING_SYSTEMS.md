# IRON & OAK: Missing Systems & Gap Fills

## Comprehensive Audit of Every System Needed for Ultra-Realistic City Simulation

---

## 1. EMERGENCY SERVICES (DEEP)

### 1.1 Fire Department

| Building | Era | Coverage | Crew | Vehicles | Cost/yr |
|----------|-----|----------|------|----------|---------|
| Volunteer station | Frontier | 12 tiles | 8 volunteers | Hand pump | $200 |
| Fire station | Industrial | 18 tiles | 16 firefighters | Horse engine + ladder | $800 |
| Modern fire station | Postwar | 25 tiles | 24 crew | 2 engines, 1 ladder, 1 rescue | $3,000 |
| Fire HQ | Modern | 35 tiles | 40 crew | 4 engines, 2 ladders, hazmat | $8,000 |
| Specialized station | Modern | City-wide (dispatch) | 12 specialists | Hazmat, wildfire, airport crash | $5,000 |

**Response Time Model:**
```
response_time_minutes = (
  distance_to_nearest_station / vehicle_speed
  + crew_readiness_delay          (0.5-2 min based on staffing)
  + traffic_congestion_penalty    (0-5 min based on road conditions)
)

fire_damage = base_damage * (1 + response_time_minutes * 0.15)
// Every minute of delay = 15% more damage
```

**Fire Mechanics:**
- **Fire hydrants**: Placeable every 3-5 tiles. Without hydrant coverage, fire trucks must shuttle water (2x response time).
- **Fire spread**: Fires spread to adjacent buildings based on: building material (wood > brick > concrete > steel), wind speed, building separation, fire break (parks, roads).
- **Fire causes**: Electrical faults (old buildings), industrial accidents, arson, kitchen fires, lightning strikes, wildfire spread from forests.
- **Wildfire**: Forest tiles near the city can ignite in summer droughts. Firebreaks (cleared strips), fire lookout towers, and aerial firefighting (water bombers, Modern+) mitigate risk.
- **Arson investigation**: Suspicious fires trigger investigation. Arson rings can emerge in high-crime areas.
- **Fire prevention**: Building codes reduce fire risk. Smoke detectors (Modern+) reduce residential fire deaths 50%. Sprinkler mandates for commercial buildings.
- **Fire rating**: City gets a fire safety rating (1-10) that affects insurance costs for all buildings.

### 1.2 Emergency Medical Services (EMS)

| Building | Era | Response | Vehicles | Range |
|----------|-----|----------|----------|-------|
| First aid post | Frontier | Doctor on horseback | Horse | 10 tiles |
| Ambulance station | Postwar | 2 ambulances | Motor ambulance | 20 tiles |
| Paramedic station | Modern | 4 ambulances + ALS | Advanced life support | 30 tiles |
| Air ambulance base | Modern | Helicopter | Helicopter | City-wide + regional |

**EMS Mechanics:**
- EMS responds to: accidents (traffic, industrial, construction), medical emergencies (heart attack, stroke), fire injuries, crime injuries
- Response time directly affects survival rate: <5 min = 90% survival, 5-10 min = 70%, >15 min = 40%
- Hospital proximity matters -- EMS transports patient to nearest hospital with capacity
- Mass casualty events (disasters, building collapse) can overwhelm EMS capacity

### 1.3 Search & Rescue
- **SAR team**: Deployed after earthquakes, building collapses, floods, mining accidents
- **Equipment**: Heavy rescue vehicle (concrete cutting), swift water rescue boat, mountain rescue team
- **K9 unit**: Search dogs for rubble (unlocked via research)

### 1.4 Emergency Dispatch (911/112)
- **Dispatch center**: Building that coordinates fire, EMS, and police response. Without one, response times +30%.
- **Upgrade path**: Manual dispatch (Frontier) -> Radio dispatch (Postwar) -> Computer-aided dispatch (Modern) -> AI-assisted dispatch (Future, -20% response time)
- **Dispatcher staffing**: Understaffed dispatch = calls dropped, delayed response

---

## 2. CHILDCARE & EARLY YEARS

### 2.1 Childcare Buildings

| Type | Age | Capacity | Staff Ratio | Cost/yr | Era |
|------|-----|----------|-------------|---------|-----|
| Home daycare | 0-4 | 6 children | 1:3 | $100 | All |
| Daycare center | 0-4 | 30 children | 1:5 | $400 | Industrial+ |
| Kindergarten | 4-6 | 25 children | 1:8 | $300 | Industrial+ |
| After-school program | 6-12 | 40 children | 1:10 | $250 | Postwar+ |
| Summer camp | 6-16 | 60 children | 1:8 | $150 (seasonal) | Postwar+ |

### 2.2 Childcare Economics
- **Workforce participation**: Without childcare, 30-50% of parents (historically mothers) stay home -> reduced workforce -> reduced tax revenue -> slower growth
- **Childcare subsidy policy**: City can subsidize childcare costs. Expensive but dramatically increases workforce participation.
- **Parental leave policy**: Mandatory parental leave (0-12 months). More leave = higher birth rate + happier citizens + higher business costs.
- **Child protection services**: Social workers investigate neglect/abuse. Understaffed CPS = higher child mortality events (rare but impactful).

### 2.3 Playgrounds & Child Spaces
- **Playground**: Small ploppable (2x2). Boosts happiness for families within 8 tiles. Requires maintenance.
- **Adventure playground**: Larger (4x4). Higher happiness boost. Modern era+.
- **Indoor play center**: Weather-independent. Good for cold/wet climates. Modern era+.
- **Child-safe streets**: Policy to designate residential streets as low-speed zones with play equipment. Boosts family happiness, reduces child accidents.

---

## 3. DEATH & END-OF-LIFE INFRASTRUCTURE

### 3.1 Death Processing Chain
```
Citizen dies -> Morgue/Hospital morgue (temporary)
            -> Coroner investigation (if suspicious)
            -> Funeral home (preparation)
            -> Cemetery OR Crematorium (final disposition)
```

### 3.2 Buildings

| Building | Era | Capacity | Cost/yr | Notes |
|----------|-----|----------|---------|-------|
| Hospital morgue | All | 20 bodies | Included in hospital | Temporary holding |
| City morgue | Industrial | 50 bodies | $200 | For unidentified/unclaimed |
| Funeral home | Frontier+ | 5 services/week | Private (no city cost) | Generates commercial tax |
| Cemetery | Frontier+ | 500-5,000 plots | $100-500 | Land-intensive. Eventually fills up. |
| Crematorium | Industrial+ | Unlimited (fuel cost) | $300 | Less land use. Pollution (minor). |
| Memorial park | Postwar+ | N/A | $200 | Green space + memorial function |
| Columbarium | Modern+ | 2,000 urns | $150 | Compact cremation memorial |
| Green burial ground | Modern+ | 1,000 plots | $80 | Natural, no embalming. Eco-conscious. |

### 3.3 Death Mechanics
- **Cemetery capacity**: Cemeteries fill up over decades. Full cemeteries stop accepting burials. Player must build new ones or expand.
- **Cemetery land value**: Cemeteries slightly reduce nearby residential land value but provide green space and cultural value.
- **Cultural death preferences**: Some cultures prefer burial, others cremation. Religious buildings influence this. Mismatched services = unhappiness.
- **Pandemic morgue overflow**: During pandemics, morgues can overflow -> temporary mass burial events (happiness impact).
- **Historic cemeteries**: Old cemeteries become historic landmarks. Can't be demolished without massive approval hit.

---

## 4. POSTAL & DELIVERY SYSTEM

| Building | Era | Coverage | Vehicles | Function |
|----------|-----|----------|----------|---------|
| Post office | Frontier | 15 tiles | Horse courier | Mail delivery, stamps |
| Sorting center | Industrial | City-wide | Mail wagons | Central mail processing |
| Main post office | Postwar | City-wide | Mail trucks | All postal services |
| Package hub | Modern | City-wide | Delivery vans | E-commerce logistics |
| Drone delivery hub | Future | 10 tiles | Delivery drones | Instant last-mile delivery |

**Postal Mechanics:**
- Post offices serve as service buildings (like fire/police). Coverage = citizen satisfaction.
- E-commerce era (Modern+): Package delivery volume explodes. More delivery vans = more traffic. Drone delivery reduces van traffic.
- Postal jobs: Entry-level employment. Good for low-education workforce.
- Postal revenue: Government-run mail is a revenue source (or cost center depending on pricing policy).

---

## 5. STREET INFRASTRUCTURE & PUBLIC REALM

### 5.1 Street Furniture

| Item | Effect | Cost | Era |
|------|--------|------|-----|
| Street light | Crime -15% in radius, enables night activity | $2/tile | Industrial+ |
| Gas lamp | Crime -8% in radius, charming | $1/tile | Frontier |
| Smart LED light | Crime -20%, energy efficient | $3/tile | Modern |
| Bench | Walkability +5%, elderly rest spots | $0.50 | All |
| Trash can | Litter -30% in radius | $0.25 | Industrial+ |
| Public toilet | Pedestrian satisfaction +10% | $100/yr | Industrial+ |
| Drinking fountain | Small happiness boost | $50 | All |
| Bike rack | Cycling mode share +5% near transit | $0.50 | Modern |
| Planter/flower box | Land value +2% in radius | $1 | All |
| Bus shelter | Transit satisfaction +10% at stop | $50 | Postwar+ |
| Information kiosk | Tourist satisfaction +5% | $100 | Modern |
| Public art/sculpture | Land value +3%, cultural boost | $500-5000 | All |

### 5.2 Street Lighting & Crime
- **Unlit streets at night**: Crime rate +30%, pedestrian traffic -50%, commercial revenue -20% after dark
- **Lit streets**: Crime rate normalized, night economy enabled
- **Smart lighting**: Motion-activated, energy-saving, integrated CCTV (Modern+)
- **Light pollution**: Excessive street lighting annoys residential areas. Dimming policy available.

### 5.3 Pedestrian Infrastructure
- **Sidewalks**: Automatically built with roads. Width affects pedestrian capacity.
- **Pedestrian zones**: Car-free streets. Massively boost commercial revenue and walkability. Require delivery vehicle exemptions.
- **Crosswalks**: Painted -> signalized -> grade-separated (overpass/underpass). Affects pedestrian safety and traffic flow.
- **Skywalks/covered walkways**: Climate-appropriate. Keep pedestrians comfortable in extreme heat, rain, or cold.

### 5.4 Urban Forestry
- **Street trees**: Plant along roads. Effects: land value +3%, air quality improvement, shade (reduces heat island), visual beauty, maintenance cost.
- **Tree types**: Deciduous (seasonal color), evergreen (year-round shade), fruit trees (community gathering).
- **Tree maintenance**: Trimming, disease treatment, storm damage removal. Neglected trees become hazards.
- **Canopy cover target**: City-wide metric. >30% canopy = significant heat reduction and happiness boost.

---

## 6. SANITATION & CITY MAINTENANCE

### 6.1 Street Cleaning

| Service | Era | Effect | Vehicle | Cost |
|---------|-----|--------|---------|------|
| Manual sweeping | Frontier | Basic cleanliness | Broom crews | $50/yr |
| Street sweeper | Postwar | Street cleanliness +50% | Sweeper truck | $200/yr per vehicle |
| Power washing | Modern | Remove grime, graffiti | Pressure wash truck | $300/yr per vehicle |

### 6.2 Seasonal Maintenance
- **Snow plowing**: Required in cold climates. Without it, roads unusable in winter. Plow trucks + salt storage.
- **De-icing salt**: Cheap but damages infrastructure long-term. Alternatives: sand, brine, heated roads (Future).
- **Leaf collection**: Autumn service. Clogged drains from leaves = localized flooding.
- **Pothole repair**: Roads deteriorate. Unfixed potholes slow traffic, damage vehicles, reduce satisfaction. Repair crew depot needed.

### 6.3 Pest Control
- **Rat population**: Grows with uncollected garbage, abandoned buildings, sewer problems. Rats spread disease, reduce property values.
- **Mosquito control**: In warm/wet climates. Standing water breeds mosquitoes. Leads to disease events. Drainage + spraying + environmental management.
- **Pest control office**: Building that reduces pest problems city-wide. Staffed by inspectors.

### 6.4 Graffiti & Vandalism
- **Graffiti**: Appears on walls in low-happiness, high-crime areas. Reduces property values.
- **Removal service**: Costs money but restores property values.
- **Legal graffiti walls**: Policy that channels graffiti to designated areas. Reduces illegal graffiti, creates cultural attraction.
- **Vandalism**: Broken street furniture, damaged bus stops. Maintenance crews repair. Frequency correlated with crime and youth unemployment.

---

## 7. CULTURAL & RELIGIOUS BUILDINGS

### 7.1 Religious Buildings (by cultural profile)

| Building | Cultures | Capacity | Era | Effect |
|----------|----------|----------|-----|--------|
| Small chapel | Christian | 50 | Frontier | Happiness +5% for religious citizens |
| Church | Christian | 200 | Frontier | Happiness +10%, community center |
| Cathedral | Christian | 1,000 | Industrial | Landmark, tourism, massive happiness |
| Mosque | Muslim | 300 | All | Happiness, community gathering |
| Grand mosque | Muslim | 2,000 | Industrial | Landmark, tourism |
| Temple | Buddhist/Hindu | 200 | All | Happiness, meditation garden |
| Pagoda | East Asian | 150 | All | Landmark, cultural value |
| Synagogue | Jewish | 150 | All | Community center, happiness |
| Shrine | Shinto/local | 50 | All | Small happiness, tradition |
| Monastery | Various | 30 monks | Frontier | Research bonus (early era), wine/beer production |

**Religious Mechanics:**
- Citizens with unmet religious needs have lower happiness
- Religious buildings generate community bonds (social trust +)
- Different religions have different holiday calendars affecting commerce
- Religious tensions: If one religion dominates and another is underserved, social friction events
- Secularization: As education rises and eras advance, religious attendance naturally declines (cultural drift)

### 7.2 Cultural Buildings

| Building | Capacity | Effect | Era |
|----------|----------|--------|-----|
| Community center | 100 | Social cohesion, events | Frontier+ |
| Public library | 200 | Education +, research +1 RP | Frontier+ |
| Art gallery | 50 | Cultural value, tourism | Industrial+ |
| History museum | 100 | Heritage preservation, tourism, education | Industrial+ |
| Science museum | 150 | Education +, research +2 RP | Postwar+ |
| Concert hall | 500 | Cultural events, happiness | Industrial+ |
| Opera house | 1,200 | Landmark, tourism, major happiness | Postwar+ |
| Theater | 300 | Cultural events, entertainment | Industrial+ |
| Cinema | 200 | Entertainment, commercial revenue | Postwar+ |
| Cultural center | 300 | Multi-purpose: gallery, classes, events | Modern+ |
| Public archive | N/A | Heritage preservation, research | Industrial+ |

---

## 8. RECREATION & LEISURE (DEEP)

### 8.1 Water Recreation

| Facility | Era | Effect | Requirements |
|----------|-----|--------|-------------|
| Public beach | All | Tourism, happiness, swimming | Coastal/lake geography |
| Swimming pool (outdoor) | Postwar | Health, happiness (summer only) | Water supply |
| Swimming pool (indoor) | Modern | Year-round health/happiness | Heated, staffed |
| Marina | Postwar | Boat mooring, tourism, wealth | Coastal/lake |
| Fishing pier | Frontier | Recreation, food, tourism | Water access |
| Water park | Modern | Major tourism, family happiness | Large, expensive |
| Surf beach | All | Niche tourism | Coastal with waves |
| Kayak/canoe launch | Postwar | River recreation | River access |

### 8.2 Nature & Gardens

| Facility | Size | Effect | Era |
|----------|------|--------|-----|
| Small park | 2x2 | Land value +5%, happiness | All |
| City park | 6x6 | Land value +10%, major happiness | All |
| Botanical garden | 8x8 | Tourism, education, research | Industrial+ |
| Nature reserve | 20x20 | Wildlife, eco-tourism, education | Postwar+ |
| Zoo | 10x10 | Major tourism, education, happiness | Industrial+ |
| Aquarium | 6x6 | Tourism, education, marine research | Modern+ |
| Planetarium | 4x4 | Education, tourism | Postwar+ |
| Observatory | 3x3 | Research +3 RP, tourism (night) | Industrial+ |
| Hiking trails | Linear | Health, tourism | All (mountain/forest terrain) |
| Urban garden | 2x2 | Community bonds, food production | All |

### 8.3 Entertainment & Nightlife

| Venue | Era | Effect | Noise |
|-------|-----|--------|-------|
| Tavern/pub | Frontier | Happiness, community | Low |
| Bar | Postwar | Nightlife, commercial revenue | Medium |
| Nightclub | Modern | Night economy, tourism, noise | High |
| Comedy club | Modern | Happiness, cultural value | Low |
| Bowling alley | Postwar | Family entertainment | Low |
| Amusement park | Postwar | Major tourism, happiness, expensive | Medium |
| Theme park | Modern | Mega tourism, landmark | High |
| Casino | Postwar | Revenue, tourism, crime risk, addiction | Medium |
| Arcade | Postwar | Youth entertainment | Low |
| Escape room | Modern | Niche entertainment | None |
| VR center | Future | Futuristic entertainment | None |

### 8.4 Restaurants as Distinct Entities
- **Street food vendor** (2 tiles): Cheap food, cultural flavor, walkability boost
- **Fast food restaurant**: Affordable, family-friendly, obesity risk if dominant
- **Casual restaurant**: Middle-class dining, neighborhood anchor
- **Fine dining**: High-end, tourism, wealth indicator
- **Brewery/winery/distillery**: Production + tasting room + tourism
- **Food truck zone**: Designated areas for rotating food trucks (cultural diversity + tourism)

---

## 9. JUSTICE & LEGAL SYSTEM

### 9.1 Justice Pipeline
```
Crime committed -> Police investigation -> Arrest
  -> Holding cell (police station) -> Arraignment
  -> Court hearing -> Verdict
    -> Acquittal (released)
    -> Fine (revenue)
    -> Community service
    -> Probation (parole office monitors)
    -> Jail (short sentence, <1 year)
    -> Prison (long sentence, 1+ years)
    -> Rehabilitation program
    -> Release -> (risk of recidivism)
```

### 9.2 Justice Buildings

| Building | Era | Function | Capacity | Cost/yr |
|----------|-----|----------|----------|---------|
| Courthouse | Frontier | Trial + sentencing | 50 cases/month | $500 |
| County jail | Frontier | Short-term detention | 100 inmates | $400 |
| State prison | Industrial | Long-term incarceration | 500 inmates | $2,000 |
| Juvenile detention | Industrial | Youth offenders | 50 juveniles | $300 |
| Parole office | Postwar | Monitor released prisoners | 200 parolees | $150 |
| Legal aid office | Postwar | Free lawyers for poor | N/A | $200 |
| Rehab center | Modern | Drug/alcohol rehabilitation | 80 patients | $500 |
| Community service center | Modern | Alternative sentencing | N/A | $100 |
| Maximum security prison | Modern | Dangerous criminals | 200 inmates | $3,000 |

### 9.3 Justice Mechanics
- **Court backlog**: Too few courts = cases pile up. Suspects held too long. Citizens angry.
- **Prison overcrowding**: Over capacity = riots, escapes, human rights criticism, political pressure.
- **Recidivism**: Released prisoners re-offend based on: rehabilitation quality, job availability, housing, social support. Without rehab, 60%+ re-offend within 3 years.
- **Private vs. public prisons**: Policy choice. Private = cheaper but perverse incentives (profit from incarceration). Public = more expensive but better outcomes.
- **Death penalty**: Era/culture-dependent policy. Controversial. Affects crime deterrence slightly, political approval polarized.
- **Wrongful conviction events**: Rare events that generate media attention and reduce public trust in justice.

---

## 10. FINANCIAL INFRASTRUCTURE

| Building | Era | Function | Effect |
|----------|-----|----------|--------|
| Bank branch | Industrial | Savings, loans | Enables mortgages -> housing growth |
| Central bank | Postwar | Monetary policy | Interest rate control |
| Stock exchange | Postwar | Capital markets | Attracts financial sector jobs |
| Insurance company | Industrial | Risk coverage | Reduces impact of disasters on citizens |
| Real estate office | Industrial | Property market | Enables land speculation mechanic |
| Pawnshop | Frontier | Emergency cash for poor | Indicates poverty level |
| Microfinance office | Modern | Small business loans | Entrepreneurship boost in poor areas |
| ATM network | Modern | Cash access | Convenience (auto-placed with banks) |
| Cryptocurrency exchange | Future | Digital finance | Attracts tech-forward businesses |

**Financial Mechanics:**
- **Mortgage availability**: Banks enable mortgages. Without banks, only wealthy can build houses. Banks = faster residential growth.
- **Insurance**: City insurance rating based on fire safety, flood risk, crime. Higher rating = lower premiums = more businesses willing to locate.
- **Stock market events**: Boom/bust cycles affect commercial zone growth and citizen wealth.
- **Banking crisis events**: Bank runs, credit freezes. Cascade through economy.

---

## 11. TELECOMMUNICATIONS

| Infrastructure | Era | Coverage | Effect | Cost |
|---------------|-----|----------|--------|------|
| Telegraph office | Frontier | City-wide | +10% trade efficiency | $50 |
| Telephone exchange | Industrial | 20 tiles | Communication, business efficiency | $200 |
| Radio tower | Industrial | City-wide | Media, emergency broadcast | $300 |
| TV station | Postwar | City-wide | Media, entertainment, education | $500 |
| Cell tower | Modern | 15 tiles | Mobile communication | $100 per tower |
| Fiber optic hub | Modern | 10 tiles | High-speed internet, enables tech sector | $300 |
| Data center | Modern | City-wide | Cloud services, tech industry | $2,000 |
| 5G tower | Future | 8 tiles (dense) | IoT, smart city infrastructure | $150 per tower |
| Satellite ground station | Modern | Regional | International communication | $1,000 |

**Telecom Mechanics:**
- **Internet coverage**: Required for modern office zones and tech industry. No fiber = no tech companies.
- **Cell coverage gaps**: Dead zones reduce citizen satisfaction and business efficiency.
- **Media infrastructure**: TV/radio towers enable corresponding media types (see Media System doc).
- **Smart city prerequisite**: 5G/fiber coverage required for Future-era smart infrastructure (smart traffic, sensors, IoT).

---

## 12. CONSTRUCTION PROCESS

### 12.1 Construction as Gameplay

Buildings don't appear instantly. Construction is a visible, simulated process:

| Phase | Duration | Effect |
|-------|----------|--------|
| **Permit** | 1-5 days | Queued at city hall. Backlog if understaffed. |
| **Site prep** | 2-10 days | Demolition of old structure, excavation. Dust, noise, truck traffic. |
| **Foundation** | 5-15 days | Concrete trucks, scaffolding appears. |
| **Construction** | 15-90 days | Cranes, scaffolding, worker activity. Traffic disruption on adjacent roads. |
| **Finishing** | 5-20 days | Interior work, utilities connection. |
| **Inspection** | 1-3 days | Building inspector verifies code compliance. |
| **Occupancy** | Immediate | Building becomes operational. |

### 12.2 Construction Impact
- **Traffic disruption**: Construction sites reduce adjacent road capacity by 30-50%. Lane closures for utility connections.
- **Noise**: Construction generates noise pollution (8am-6pm). Nighttime construction bans available as policy.
- **Construction workers**: Require workforce. Construction boom without enough workers = delays + wage inflation.
- **Materials supply**: Construction requires materials from production chains (lumber, bricks, steel, cement, glass). Material shortage = construction slowdown.
- **Crane skyline**: Active construction shows cranes on the city skyline. Visual indicator of growth.

### 12.3 Demolition & Renovation
- **Demolition**: Takes time, generates debris (requires waste management), temporary dust/noise.
- **Renovation/retrofit**: Upgrade existing buildings (energy efficiency, earthquake resistance, accessibility). Cheaper than demolish+rebuild but limited improvements.
- **Historic building renovation**: Required for heritage preservation. Special craftsmen needed. Expensive but preserves cultural value + tourism.
- **Asbestos/hazmat**: Old buildings (Industrial/Postwar era) may contain hazardous materials. Special removal required. Adds cost and time.

---

## 13. TOURISM INFRASTRUCTURE

| Building | Era | Capacity | Effect |
|----------|-----|----------|--------|
| Inn/guesthouse | Frontier | 10 guests | Basic visitor accommodation |
| Hotel (2-star) | Industrial | 50 guests | Budget accommodation |
| Hotel (3-star) | Postwar | 100 guests | Standard accommodation |
| Hotel (4-star) | Postwar | 200 guests | Upscale, pool, restaurant |
| Hotel (5-star/luxury) | Modern | 150 guests | Premium, spa, prestige |
| Hostel | Postwar | 80 guests | Budget, backpacker market |
| Resort | Postwar | 300 guests | Destination hotel, beach/mountain |
| Convention center | Modern | 5,000 capacity | Business tourism, trade shows |
| Tourist info center | Industrial | N/A | Visitor satisfaction +15% |
| Souvenir shop | Industrial | N/A | Tourism revenue boost |
| Tour bus depot | Modern | N/A | Organized tourism, traffic impact |
| Cruise terminal | Modern | 3,000/ship | Massive tourism influx + traffic |

**Tourism Mechanics:**
- **Accommodation capacity**: Tourists only visit if beds are available. Insufficient hotels = missed revenue.
- **Tourist satisfaction**: Based on: attractions, safety, cleanliness, transit, accommodation quality, food scene.
- **Tourism seasons**: Peak/off-peak based on climate. Seasonal pricing.
- **Airbnb effect (Modern+)**: Citizens rent out rooms. Reduces housing supply, increases rental prices. Neighborhood character changes. Policy: ban, regulate, or allow.
- **Tourism carrying capacity**: Too many tourists overwhelm infrastructure and annoy residents. Overtourism penalty.
- **Heritage tourism**: Historic buildings, cultural sites, museums generate steady tourist flow.
- **Eco-tourism**: Nature reserves, hiking trails, wildlife. Requires conservation.

---

## 14. HOUSING MARKET (DEEP)

### 14.1 Housing Types

| Type | Density | Era | Wealth | Size |
|------|---------|-----|--------|------|
| Shack/shanty | Ultra-low | Frontier | Destitute | 1 tile |
| Cottage | Low | Frontier | Working | 1 tile |
| Row house | Medium | Industrial | Working/Middle | 1 tile |
| Detached house | Low | Postwar | Middle | 2 tiles |
| Apartment building | Medium | Industrial | Working/Middle | 2 tiles |
| High-rise apartment | High | Postwar | Middle | 4 tiles |
| Luxury condo | High | Modern | Upper-middle/Wealthy | 4 tiles |
| Public housing | Medium | Postwar | Working/Destitute | 2 tiles |
| Senior housing | Medium | Modern | Elderly | 2 tiles |
| Student housing | High | Postwar | Young adult | 2 tiles |
| Mansion/estate | Very low | All | Wealthy | 4-6 tiles |
| Homeless shelter | N/A | Industrial+ | Destitute | 2 tiles |

### 14.2 Housing Market Mechanics
- **Housing price model**: Based on land value + construction quality + supply/demand ratio. Prices visible in overlay.
- **Affordability crisis**: When median house price > 5x median income, affordability crisis triggers. Citizens protest, emigrate.
- **Rent vs. own**: Working class rent, middle class split, wealthy own. Rent control policy available.
- **Housing bubble**: Rapid price increases can bubble. When bubble pops, prices crash, construction halts, banks stressed.
- **Homelessness**: When affordable housing unavailable, tent camps appear on vacant lots and under bridges. Visual indicator of policy failure.
- **Slum formation**: Overcrowded, undermaintained housing in poor areas. Disease risk, fire risk, crime. Can be improved via public investment or demolished (displacing residents).
- **Gentrification**: Rising prices displace original residents. Cultural character changes. See Cultural DNA system.

---

## 15. UTILITY INFRASTRUCTURE (DEEP)

### 15.1 Water System

| Component | Era | Function |
|-----------|-----|---------|
| Well | Frontier | Basic water, limited capacity |
| Water pump station | Industrial | Extracts from river/lake/aquifer |
| Water tower | Industrial | Pressure regulation, storage |
| Water treatment plant | Industrial | Purifies water for drinking |
| Reservoir/dam | Postwar | Large-scale storage, hydropower |
| Desalination plant | Modern | Converts seawater (coastal only) |
| Water pipe network | All | Distribution to buildings |

**Water Mechanics:**
- **Pressure model**: Pressure drops with distance from pump/tower. Elevated areas need boosters.
- **Water quality**: Contamination from industrial runoff, aging pipes, or treatment failure. Causes disease.
- **Drought**: Water supply drops. Rationing policy available. Affects agriculture.
- **Pipe aging**: Old pipes leak (water loss) and eventually burst (water main breaks, road flooding).
- **Water meter**: Revenue from water sales. Pricing policy (flat rate vs. usage-based).

### 15.2 Sewer System

| Component | Era | Function |
|-----------|-----|---------|
| Cesspit | Frontier | Basic, unsanitary |
| Sewer pipe network | Industrial | Carries wastewater |
| Sewage treatment plant | Industrial | Processes wastewater |
| Combined sewer | Industrial | Handles both sewage + stormwater (overflow risk) |
| Separated sewer | Modern | Separate pipes for sewage and storm (expensive retrofit) |
| Green infrastructure | Modern | Rain gardens, permeable pavement (reduces runoff) |

**Sewer Mechanics:**
- **Combined sewer overflow (CSO)**: During heavy rain, combined sewers overflow raw sewage into waterways. Public health crisis.
- **Separated sewer retrofit**: Expensive but eliminates CSO. Major construction disruption.
- **Treatment plant capacity**: Overloaded plants dump partially treated water. Environmental violation.

### 15.3 Stormwater Management
- **Storm drains**: Prevent street flooding during rain.
- **Retention ponds**: Hold excess water during storms. Release slowly.
- **Flood walls/levees**: Protect riverside areas. Failure = catastrophic flooding.
- **Green infrastructure**: Permeable pavement, bioswales, green roofs. Reduces runoff naturally.
- **Flood zone mapping**: Overlay showing flood risk areas. Building in flood zones = higher insurance, flood damage risk.

### 15.4 Gas/Heating Network
- **Gas pipeline**: Natural gas distribution for heating and cooking (Industrial+).
- **District heating**: Central heating plant distributes hot water through pipes. More efficient than individual heating (Modern+).
- **Gas leak events**: Aging pipes + poor maintenance = explosion risk.

---

## 16. WEATHER & CLIMATE (CONTINUOUS)

### 16.1 Weather as Continuous System

Weather isn't just discrete events -- it's a continuous simulation affecting everything:

| Weather State | Temperature | Precipitation | Wind | Effect |
|--------------|-------------|---------------|------|--------|
| Clear | Varies by season | None | Calm | Baseline operations |
| Overcast | Slight cooling | None | Light | Solar energy -30% |
| Rain (light) | Cooling | Light | Light | Outdoor activity -20%, runoff |
| Rain (heavy) | Cooling | Heavy | Moderate | Flooding risk, traffic -30% |
| Thunderstorm | Cooling | Intense | Strong | Lightning fires, flooding, power outage risk |
| Snow (light) | Below freezing | Light | Calm | Scenic, road speed -20% |
| Snow (heavy) | Below freezing | Heavy | Strong | Road speed -60%, plowing needed |
| Blizzard | Well below freezing | Intense | Severe | Roads closed, power outage risk, heating emergency |
| Heatwave | Extreme high | None | Calm | AC demand spike, health risk (elderly), fire risk |
| Fog | Varies | Mist | Calm | Airport closed, traffic -20%, accidents +30% |
| Hurricane | Warm | Extreme | Extreme | Building damage, flooding, evacuation |

### 16.2 Seasonal Energy Demand
```
heating_demand = max(0, (comfort_temp - outside_temp) * building_count * insulation_factor)
cooling_demand = max(0, (outside_temp - comfort_temp) * building_count * ac_penetration)
total_energy = base_load + heating_demand + cooling_demand
```

### 16.3 Climate Change (Long-term)
- Over decades (Modern+ era), climate gradually shifts:
  - Average temperature rises 1-3 degrees
  - Extreme weather events become more frequent
  - Sea level rises (coastal flooding risk increases)
  - Drought frequency increases in arid zones
  - Snowfall decreases in temperate zones
- Player can mitigate through emissions reduction (green energy, transit, regulations)
- Climate change speed tied to global (all cities combined) emissions in multiplayer

---

## 17. ACCESSIBILITY & INCLUSION

### 17.1 Accessibility Features
- **Wheelchair ramps**: Required on all public buildings (Modern+). Buildable upgrade for older buildings.
- **Accessible transit**: Low-floor buses, elevator access at metro stations, wheelchair spaces on trains.
- **Tactile paving**: Textured sidewalk surfaces for visually impaired.
- **Audio signals**: At crosswalks for blind pedestrians.
- **Accessibility rating**: Per-building and city-wide metric. Affects citizen satisfaction for disabled population.
- **Disability employment programs**: Government program that places disabled citizens in appropriate jobs. Reduces welfare costs.
- **Policy: ADA/Equality Act equivalent**: Mandate accessibility in new construction. Retrofit timeline for old buildings.

### 17.2 Inclusion Metrics
- **Language services**: For immigrant communities. Translation services at government offices, multilingual signage.
- **Gender equality index**: Affects workforce participation, wage gap, political representation.
- **Age-friendly city**: Accessibility, healthcare, senior housing, slow zones, bench density.

---

## 18. NIGHT ECONOMY

### 18.1 Night-Time Systems
- **Night shift workers**: Hospitals, factories, transit operate 24/7. Workers commute at non-peak hours.
- **Late-night transit**: Bus/metro service frequency drops after midnight. Night bus routes possible (expensive).
- **Night economy districts**: Designated areas where bars, clubs, restaurants operate late. Generate tax revenue but noise complaints.
- **Noise ordinance enforcement**: Quiet hours (10pm-7am in residential). Violations generate complaints. Enforcement costs money.
- **Night safety**: Street lighting, police patrols, CCTV reduce nighttime crime. Safe nights = thriving night economy.
- **24-hour businesses**: Convenience stores, pharmacies, gas stations. Serve night workers and late-night customers.

### 18.2 Red Light Districts (Optional/Toggle)
- **Adult entertainment zone**: Regulated area with adult businesses. Generates tax revenue. Increases tourism (certain segments). Controversial.
- **Policy toggle**: Player can allow, restrict to specific zone, or ban entirely.
- **Effects**: Revenue, tourism (some), land value depression nearby, moral opposition from religious/traditional demographics, crime correlation.
- **Regulation vs. prohibition**: Regulated = safer, taxed, contained. Prohibited = underground, untaxed, dangerous.

---

## 19. INFRASTRUCTURE AGING & LIFECYCLE

### 19.1 Age Tracking
Every piece of infrastructure has an age counter and condition rating (100% new -> 0% failed):

```
condition_decay_per_year = base_decay * usage_intensity * weather_exposure * maintenance_quality_inverse

// Example: A road with heavy truck traffic in harsh winter with poor maintenance:
condition_decay = 5% * 2.0 * 1.5 * 1.3 = 19.5% per year (fails in ~5 years)

// Same road with light traffic, mild climate, good maintenance:
condition_decay = 5% * 0.8 * 0.8 * 0.7 = 2.24% per year (lasts ~45 years)
```

### 19.2 Infrastructure Report Card
- Annual report grading all infrastructure categories A through F
- Low grades increase political pressure
- Very low grades trigger infrastructure failure events (bridge collapse, water main break, power outage)
- Report card affects business confidence and investment

### 19.3 Deferred Maintenance Trap
```
deferred_maintenance_cost = original_repair_cost * (1 + years_deferred * 0.15)^2

// Deferring a $1,000 repair for 5 years:
// Cost to fix now = $1,000 * (1 + 5 * 0.15)^2 = $1,000 * 3.0625 = $3,063
// Deferring saves money short-term but costs 3x long-term
```

---

## 20. CITIZEN LIFE SIMULATION

### 20.1 Daily Routine
Citizens follow daily patterns:
```
6:00-7:00   Wake up, morning routine
7:00-8:00   Commute to work/school (RUSH HOUR)
8:00-12:00  Work/school
12:00-13:00 Lunch break (some commercial activity)
13:00-17:00 Work/school
17:00-18:00 Commute home (RUSH HOUR)
18:00-20:00 Shopping, errands, recreation
20:00-22:00 Home entertainment, dining out
22:00-6:00  Sleep
```

### 20.2 Life Events
| Event | Age | Effect |
|-------|-----|--------|
| Birth | 0 | New citizen added to household |
| First school day | 5 | Requires elementary school |
| Graduation | 18 | Seeks job or university |
| First job | 18-25 | Begins earning, paying tax |
| Marriage | 20-35 | Forms new household, seeks housing |
| First child | 22-38 | Needs childcare, larger housing |
| Career change | 30-50 | May retrain, change commute |
| Divorce | Any adult | Household splits, housing demand |
| Retirement | 60-70 | Stops working, draws pension |
| Health crisis | Any | Healthcare demand spike |
| Death | Variable | Cemetery/crematorium demand |

### 20.3 Addiction & Social Issues
- **Alcohol/drug addiction**: Rates increase with unemployment, poverty, stress. Creates healthcare burden (ER visits), crime (petty theft), homelessness, family breakdown.
- **Gambling addiction**: If casinos exist, ~5% of gamblers develop problems. Support services needed.
- **Rehabilitation**: Treatment centers reduce addiction rates. Without treatment, addicts cycle through ER -> jail -> streets.

### 20.4 Marriage & Family
- **Marriage rates**: Affected by economic stability, cultural values, housing availability.
- **Divorce rates**: Affected by economic stress, cultural values, era (divorce becomes more accepted over time).
- **Single-parent households**: Higher childcare demand, higher poverty risk.
- **Multi-generational housing**: In some cultures, extended families share homes. Reduces housing demand per capita but increases building density.

---

## 21. MISCELLANEOUS MISSING SYSTEMS

### 21.1 Animals
- **Animal shelter**: Stray cats/dogs. Catches strays, adoption program. Without it, stray animal population grows.
- **Veterinary clinic**: Pet healthcare. Commercial building, generates revenue.
- **Dog park**: Off-leash area. Happiness boost for pet owners.
- **Wildlife corridors**: Green connections between nature areas. Biodiversity. Eco-tourism.
- **Mounted police**: Horse patrol in parks and tourist areas. Era: All. Adds charm.
- **Bird sanctuary**: Nature reserve variant. Birdwatching tourism.
- **Pest animals**: Deer eating gardens (suburbs), coyotes/foxes in urban areas, bears near mountains. Management needed.

### 21.2 Public Spaces
- **Town square/plaza**: Central gathering space. Markets, events, protests all happen here. Major land value boost.
- **Fountain**: Decorative, cooling in summer, landmark potential.
- **Monument/memorial**: War memorial, founder statue, civil rights monument. Cultural value + tourism.
- **Clock tower**: Landmark building. Charming. Town identity.
- **Lighthouse**: Coastal landmark. Navigation aid. Tourism.
- **Bandstand/gazebo**: Small event venue in parks. Free concerts.
- **Public pool**: Summer recreation. Health. Community.

### 21.3 Signage & Wayfinding
- **Street signs**: Auto-placed on roads. Named streets generate neighborhood identity.
- **Wayfinding signage**: Tourist areas get directional signs to attractions.
- **Bilingual/multilingual signage**: In diverse neighborhoods. Policy choice.
- **Digital information boards**: Bus arrival times, city announcements (Modern+).

### 21.4 Real Estate & Land
- **Eminent domain**: Seize land for public projects. Compensation required. Political cost.
- **Land value tax**: Tax land value (not building value). Encourages development, discourages speculation.
- **Vacant lot management**: Empty lots attract problems. City can tax vacant lots, convert to community gardens, or force development.
- **Property speculation**: NPCs buy land and hold, waiting for value increase. Drives up prices.

### 21.5 Citizen-Named Stories
- **Named citizens**: Random citizens get names and mini-stories visible in a feed. "Maria Garcia graduated from university today." "James Chen opened a bakery on Elm Street." Creates emotional connection.
- **Follow a citizen**: Click any citizen to see their daily routine, commute, workplace, home, happiness, and life events.
- **Citizen complaints feed**: "Traffic on Bridge Road is terrible!" "My kids' school is overcrowded!" Gives player specific, actionable feedback.
