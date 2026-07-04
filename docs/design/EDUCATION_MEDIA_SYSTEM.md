# IRON & OAK: Deep Education & Media System

## Complete Design Document for Education, Schools, and Media Influence

---

## 1. SYSTEM OVERVIEW

### Design Philosophy

The existing GDD (Section 6.4) defines a basic education pipeline: Daycare -> Elementary -> High School -> Vocational -> University -> Graduate. This document expands that into a **full simulation** where education is not a checkbox ("has school: yes/no") but a complex, policy-driven system where the type, quality, and philosophy of education shapes your city's workforce, culture, crime rate, innovation capacity, and political landscape for decades.

Media is the companion system: while education shapes citizens over years, media shapes public opinion over weeks. Together they determine whether your population supports your policies or revolts against them.

### Integration Points

| Existing System | How Education/Media Hook In |
|----------------|----------------------------|
| Workforce quality (5.2) | Education output determines available worker skill tiers |
| Wealth mobility (6.5) | Education is the primary driver of upward wealth movement |
| Research system (Tech Tree) | Universities generate RP; T085/T089/T090 gate education tiers |
| Crime (Services) | Dropout rate correlates with crime; media coverage affects perception |
| Immigration (6.3) | School quality is an immigration attractor |
| Political system (6.8) | Media shapes approval rating; civic education affects voter turnout |
| Budget (7.0) | Education is a major expense category; media buildings generate revenue |
| Cultural profile (6.6) | School philosophy shapes neighborhood cultural dimensions |

---

## 2. SCHOOL TYPES AND PHILOSOPHIES

### 2.1 Complete School Type Table

| School Type | Era Available | Capacity | Construction Cost | Annual Operating Cost | Funding Source | Radius |
|------------|--------------|----------|------------------|----------------------|---------------|--------|
| **One-Room Schoolhouse** | Frontier | 30 students | $500 | $200 | Government | 15 tiles |
| **Elementary School (Public)** | Frontier+ | 200 students | $5,000 | $2,000 | Government | 15 tiles |
| **Elementary School (Private)** | Industrial+ | 60 students | $8,000 | $100 (tuition-funded) | Tuition ($500/student/yr) | City-wide |
| **Religious School** | Frontier+ | 100 students | $3,000 | $800 | Community donations + small tuition | 20 tiles (within congregation) |
| **High School (Public)** | Industrial+ | 400 students | $15,000 | $8,000 | Government | 25 tiles |
| **High School (Private)** | Industrial+ | 150 students | $25,000 | $500 (tuition-funded) | Tuition ($2,000/student/yr) | City-wide |
| **Vocational School** | Industrial+ | 100 students | $10,000 | $5,000 | Government | City-wide |
| **Montessori/Alternative** | Modern+ | 40 students | $6,000 | $200 (tuition-funded) | Tuition ($3,000/student/yr) | City-wide |
| **Community College** | Postwar+ | 500 students | $25,000 | $15,000 | Government + tuition | City-wide |
| **Teaching University** | Postwar+ | 1,000 students | $80,000 | $40,000 | Government + tuition + grants | City-wide |
| **Research University** | Postwar+ | 2,000 students | $150,000 | $60,000 | Gov + tuition + grants + endowment | City-wide |
| **Online Learning Center** | Future | 5,000 students | $10,000 | $8,000 | Government + subscriptions | City-wide |
| **Vocational Training Center** | Postwar+ | 200 students | $12,000 | $6,000 | Government + employer sponsorship | City-wide |
| **Trade Apprenticeship Hall** | Frontier+ | 50 students | $2,000 | $1,000 | Guild/industry funded | 20 tiles |

### 2.2 School Philosophy System

Each school (not just school type) can be assigned a **philosophy** that modifies its output. This is a policy lever the player controls per-school or as a city-wide default.

**Available Philosophies:**

| Philosophy | Available Era | Effect on Graduates | Tradeoffs |
|-----------|-------------|--------------------| ----------|
| **Traditional/Classical** | All | +10% literacy, +5% civic engagement | -5% creativity, -5% STEM aptitude |
| **Progressive/Montessori** | Modern+ | +15% creativity, +10% happiness | -5% standardized test scores, higher cost |
| **STEM Focus** | Postwar+ | +20% STEM aptitude, +10% tech industry fit | -10% arts/culture output, -5% civic engagement |
| **Liberal Arts** | Postwar+ | +15% cultural output, +10% civic engagement | -10% STEM aptitude, -5% vocational skills |
| **Vocational/Practical** | Industrial+ | +25% trade skills, +15% industrial efficiency | -15% university preparation, -10% research output |
| **Religious** | All | +10% community cohesion, -5% crime in neighborhood | -10% science aptitude, +10% social conservatism |
| **Military/Discipline** | Industrial+ | +15% civic discipline, -10% crime | -10% creativity, -5% happiness |
| **Environmental** | Modern+ | +20% green policy support, +10% environmental values | -5% industrial workforce, +5% construction cost (green standards) |

### 2.3 School Quality Calculation

Each school has a **quality score (0-100)** that determines how effectively it educates students.

**Quality Formula:**
```
school_quality = base_quality_by_type
  * student_teacher_ratio_factor      (optimal 15:1 = 1.0, 30:1 = 0.6, 10:1 = 1.2)
  * funding_per_student_factor        (below threshold: 0.5-0.8, above: 1.0-1.3)
  * building_condition_factor         (New: 1.1, Good: 1.0, Aging: 0.85, Decrepit: 0.6)
  * teacher_quality_factor            (based on teacher education level: 0.7-1.3)
  * facility_bonus                    (Library: +5, Lab: +8, Sports: +3, Arts room: +4, Computer lab: +6)
  * class_size_factor                 (20 students: 1.0, 35 students: 0.7, 15 students: 1.15)
  * neighborhood_safety_factor        (crime rate in area: 0.7-1.0)
```

**Base Quality by School Type:**

| School Type | Base Quality | Notes |
|------------|-------------|-------|
| One-Room Schoolhouse | 30 | Limited resources |
| Public Elementary | 50 | Standard baseline |
| Private Elementary | 65 | Better resourced |
| Religious School | 45 | Variable, community-dependent |
| Public High School | 50 | Standard baseline |
| Private High School | 70 | Selective admission |
| Vocational School | 55 | Practical focus |
| Montessori | 60 | Small classes, specialized |
| Community College | 45 | Open admission |
| Teaching University | 60 | Focus on instruction |
| Research University | 55 (teaching) / 90 (research) | Split focus |
| Online Learning | 35 | Self-directed, high dropout |

### 2.4 Facility Upgrades

Schools can be upgraded with additional facilities that improve quality and specialize output:

| Facility | Cost | Quality Bonus | Special Effect | Era |
|----------|------|--------------|----------------|-----|
| Library | $2,000 | +5 | +5% literacy rate for graduates | Frontier+ |
| Science Lab | $5,000 | +8 | +10% STEM aptitude for graduates | Industrial+ |
| Sports Field | $3,000 | +3 | +5% student health, -3% dropout rate | Frontier+ |
| Arts Room | $2,500 | +4 | +8% cultural output for graduates | Industrial+ |
| Computer Lab | $8,000 | +6 | +15% tech industry readiness | Modern+ |
| Workshop/Shop | $4,000 | +5 | +12% vocational skill output | Industrial+ |
| Auditorium | $6,000 | +3 | Enables school events, +2 neighborhood happiness | Postwar+ |
| Cafeteria | $3,000 | +2 | -5% absenteeism (fed students attend more) | Postwar+ |
| Counseling Office | $1,500 | +2 | -8% dropout rate | Postwar+ |
| Swimming Pool | $10,000 | +3 | +3% health, desirability boost for residential | Modern+ |
| Maker Space | $7,000 | +7 | +10% creativity, +8% STEM | Future |
| VR Learning Lab | $12,000 | +10 | +15% all learning metrics | Future |

---

## 3. SCHOOL TRANSPORT SYSTEM

### 3.1 Student Transport Modes

How students get to school creates real traffic patterns and costs:

| Transport Mode | Era | Cost/Student/Year | Range | Traffic Impact | Requirements |
|---------------|-----|------------------|-------|---------------|-------------|
| **Walking** | All | $0 | Within 10 tiles | Pedestrian traffic near schools, 7:30-8:30am | Safe walking routes (sidewalks, crossings) |
| **School Bus** | Postwar+ | $150/student | City-wide | Bus traffic on routes, 7:00-8:30am and 2:30-4:00pm | Bus depot, dedicated fleet |
| **Public Transit Pass** | Postwar+ | $100/student | Along transit routes | Increased ridership during school hours | Existing transit coverage to school |
| **Bicycle Program** | Modern+ | $30/student (bike purchase) | Within 20 tiles | Bike lane traffic near schools | Bike lanes or safe streets, bike rack at school |
| **Parent Drop-off** | Postwar+ | $0 (to city) | Unlimited | **SEVERE congestion** near school, 7:30-8:15am | Drop-off lane ($1,000 to build) |
| **Carpool Program** | Modern+ | $50/student (admin) | Within 30 tiles | Moderate traffic, concentrated times | Carpool coordinator (part of school admin) |

### 3.2 School Bus Fleet Management

If the player opts for school bus service, they manage a fleet:

**School Bus Specifications:**

| Bus Type | Era | Capacity | Cost Each | Annual Maint | Fuel Cost/Year | Lifespan |
|----------|-----|----------|-----------|-------------|---------------|----------|
| Horse-drawn school wagon | Frontier | 15 students | $200 | $80 | $50 (feed) | 10 years |
| Early motor bus | Industrial | 25 students | $1,000 | $200 | $150 | 12 years |
| Standard yellow bus | Postwar | 40 students | $3,000 | $500 | $400 | 15 years |
| Modern school bus | Modern | 50 students | $8,000 | $800 | $600 | 18 years |
| Electric school bus | Future | 50 students | $12,000 | $400 | $200 (electric) | 20 years |
| Autonomous school shuttle | Future | 30 students | $15,000 | $300 | $150 | 20 years |

**Fleet Calculation:**
```
buses_needed = total_bus_students / bus_capacity
  * route_efficiency_factor          (well-planned routes: 0.8, poor: 1.4)
  * am_pm_double_shift              (elementary + high school staggered: 0.7)
fleet_cost_annual = buses_needed * (maintenance + fuel + driver_salary)
driver_salary = $2,000/year (Postwar) to $8,000/year (Modern)
```

### 3.3 Parent Drop-off Congestion Model

Parent drop-off is **free to the city budget** but creates **severe traffic problems**:

**Drop-off Traffic Formula:**
```
drop_off_cars = students_at_school * parent_drive_percentage
  * (1 / average_siblings_at_same_school)       (siblings reduce trips)

parent_drive_percentage varies by:
  - No school bus service: 60% of students driven
  - School bus available: 25% still driven (parents prefer control)
  - Walking distance (<10 tiles): 10% driven
  - Bad weather: +20% driven
  - Wealthy neighborhoods: +15% (more cars available)

traffic_impact = drop_off_cars * 2     (arrive + depart)
  concentrated in 45-minute window (7:30-8:15am)
  on roads adjacent to school
```

**Gameplay effect**: A 400-student high school in a middle-class neighborhood with no bus service generates ~240 car trips in 45 minutes on the adjacent road. This can gridlock a 2-lane road. The player must either:
1. Build school bus service (costs money)
2. Provide transit passes (requires transit coverage)
3. Build wider roads near schools (expensive, ugly)
4. Implement bicycle programs (requires infrastructure)
5. Zone schools near transit hubs (good planning)

This is a **genuine urban planning challenge** that most city builders ignore.

### 3.4 School Zone Traffic Rules (Policy)

| Policy | Effect | Cost |
|--------|--------|------|
| **No school zone** | Normal traffic near schools | $0 |
| **Speed reduction zone** | -30% speed within 5 tiles of school, -40% accident rate | $500/school |
| **Crossing guards** | -60% pedestrian accidents near schools, +1 job/school | $1,000/school/year |
| **School zone cameras** (Modern+) | -50% speeding near schools, generates fine revenue ($200/yr) | $2,000/school |
| **Car-free school zone** (Modern+) | No private cars within 3 tiles during school hours, requires alt transport | $0, but requires transit |

---

## 4. EDUCATION QUALITY FACTORS (DEEP)

### 4.1 Student-Teacher Ratio

The single most impactful quality factor:

| Ratio | Quality Multiplier | Cost Implication | Real-World Equivalent |
|-------|-------------------|-----------------|---------------------|
| 8:1 | 1.30 | Very expensive (many teachers) | Elite private school |
| 12:1 | 1.15 | Expensive | Well-funded suburban |
| 15:1 | 1.00 | Standard | Adequate funding |
| 20:1 | 0.90 | Below standard | Underfunded |
| 25:1 | 0.75 | Poor | Budget crisis |
| 30:1 | 0.60 | Terrible | Severe underfunding |
| 35+:1 | 0.45 | Crisis | Failed school system |

**Teacher supply**: Teachers must be university-educated citizens. The city must produce enough graduates AND pay competitive wages to retain them. If teacher pay is below median income, teachers emigrate or switch careers.

**Teacher Pay Scale:**

| Era | Minimum Teacher Salary | Competitive Salary | Effect of Underpaying |
|-----|----------------------|-------------------|----------------------|
| Frontier | $100/year | $200/year | -20% teacher retention per $50 below competitive |
| Industrial | $500/year | $1,000/year | Same scaling |
| Postwar | $2,000/year | $4,000/year | Same scaling |
| Modern | $5,000/year | $10,000/year | Same scaling |
| Future | $8,000/year | $15,000/year | Same scaling |

### 4.2 Funding Per Student

Per-student funding determines what facilities, materials, and programs a school can offer:

| Funding Tier | $/Student/Year (Modern era) | Quality Factor | What It Buys |
|-------------|---------------------------|---------------|-------------|
| **Crisis** | <$500 | 0.50 | Bare building, no materials, overcrowded |
| **Minimal** | $500-1,000 | 0.70 | Basic textbooks, no extracurriculars |
| **Adequate** | $1,000-2,000 | 0.90 | Standard materials, some programs |
| **Good** | $2,000-3,500 | 1.00 | Full programs, maintained facilities |
| **Excellent** | $3,500-5,000 | 1.15 | Advanced programs, modern equipment |
| **Elite** | $5,000+ | 1.30 | Best equipment, small classes, extensive programs |

**Funding source breakdown:**
```
school_funding = government_allocation                  (player-controlled slider)
  + property_tax_local_share * neighborhood_land_value  (wealthy areas self-fund better)
  + private_tuition                                     (private schools only)
  + endowment_income                                    (universities, some private schools)
  + federal/regional_grants                             (if regional play, larger gov subsidies)
```

**Inequality mechanic**: Schools in wealthy neighborhoods naturally receive more funding through property tax share. Without active redistribution policy, poor neighborhoods get worse schools, perpetuating poverty. The player can set a **funding equalization policy** (costs more total budget) to level the playing field.

### 4.3 Building Condition

Schools are buildings that age and deteriorate like any other:

| Condition | Age Range | Quality Factor | Visual | Maintenance Cost |
|-----------|----------|---------------|--------|-----------------|
| **New** | 0-5 years | 1.10 | Pristine sprite, bright colors | $200/year |
| **Good** | 5-15 years | 1.00 | Clean, minor wear | $400/year |
| **Aging** | 15-30 years | 0.85 | Faded paint, worn paths | $800/year |
| **Decrepit** | 30-50 years | 0.60 | Cracked walls, dark windows, overgrown | $1,500/year (or renovate for $10,000) |
| **Condemned** | 50+ years (no maintenance) | 0.30 | Boarded windows, danger signs | Must renovate ($15,000) or demolish |

**Renovation mechanic**: The player can renovate an aging/decrepit school for a one-time cost, resetting condition to "Good." This is cheaper than building new but requires the school to close for 6 game-months during renovation. Students must attend other schools or have no education during this period.

### 4.4 Teacher Quality Tiers

Not all teachers are equal. Teacher quality depends on where they were educated and how well they are compensated:

| Teacher Quality | Source | Quality Multiplier | Salary Demand |
|----------------|--------|-------------------|--------------|
| **Untrained** | No university degree (Frontier era common) | 0.70 | Below average |
| **Basic** | Community college or teaching certificate | 0.85 | Average |
| **Standard** | Teaching university graduate | 1.00 | Average |
| **Advanced** | Research university graduate | 1.15 | Above average |
| **Master** | Graduate degree + 10yr experience | 1.30 | High |

**Teacher recruitment**: Schools draw from your city's university-educated population. If your universities are low quality, your teachers are low quality, which makes your schools low quality, which produces fewer university-ready graduates. This is the **education death spiral** -- one of Iron & Oak's most dangerous feedback loops.

---

## 5. EDUCATION OUTCOMES & CITY EFFECTS

### 5.1 Graduate Output by Education Level

Each education level produces citizens with specific capabilities:

| Education Level | Workforce Tier | Jobs Available | Tax Contribution | Crime Risk | Innovation |
|----------------|---------------|---------------|-----------------|-----------|-----------|
| **No education** | Unskilled labor | Manual labor, farming | Very low ($20/yr) | High (2.0x) | None |
| **Elementary** | Basic literate | Factory floor, retail, service | Low ($50/yr) | Moderate (1.3x) | None |
| **High School** | Skilled worker | Foreman, clerk, technician | Moderate ($150/yr) | Low (0.8x) | None |
| **Vocational** | Specialist trade | Electrician, mechanic, welder | Moderate-High ($200/yr) | Very Low (0.5x) | Low |
| **Community College** | Semi-professional | Nursing, accounting, IT support | Moderate ($180/yr) | Low (0.7x) | Low |
| **University** | Professional | Engineer, doctor, teacher, lawyer | High ($400/yr) | Very Low (0.4x) | Moderate |
| **Graduate** | Specialist/Researcher | Researcher, professor, surgeon | Very High ($600/yr) | Minimal (0.2x) | High |

### 5.2 City-Wide Education Metrics

These aggregate metrics drive major gameplay effects:

**Literacy Rate:**
```
literacy_rate = (pop_with_elementary_or_higher / total_pop_over_12) * 100

Effects:
  < 30%: -30% commercial output, -50% office zone demand, +40% crime
  30-60%: -10% commercial output, -20% office demand
  60-80%: Standard baseline
  80-95%: +10% commercial output, +20% office demand, +10% research
  95%+:   +20% commercial output, +40% office demand, +20% research, tourism bonus
```

**STEM Index (0-100):**
```
stem_index = (STEM-educated graduates / total graduates) * 100
  * university_research_factor

Effects:
  < 20: Cannot attract tech industry
  20-40: Basic tech industry possible
  40-60: Moderate tech industry, some research labs
  60-80: Strong tech sector, research university attracts talent
  80+:   Silicon Valley effect -- tech boom, massive wealth, innovation events
```

**Arts & Culture Index (0-100):**
```
culture_index = (arts-educated graduates / total graduates) * 50
  + (cultural_buildings / population_per_1000) * 50

Effects:
  < 20: Cultural desert, no tourism from culture, brain drain of creatives
  20-40: Basic cultural scene, some galleries
  40-60: Vibrant arts district possible, moderate cultural tourism
  60-80: Major cultural destination, theaters, galleries, music venues flourish
  80+:   Cultural capital -- massive tourism, attracts wealthy residents, property value surge
```

**Vocational Skills Index (0-100):**
```
vocational_index = (vocational graduates / total workforce) * 100

Effects:
  < 10: Construction slower and more expensive, infrastructure quality -20%
  10-25: Standard baseline
  25-40: Construction 15% faster, infrastructure quality +10%, trade wages stable
  40+:   Construction 25% faster, infrastructure quality +20%, export of skilled workers to region
```

### 5.3 Dropout Rate & Crime Correlation

**Dropout Rate Calculation:**
```
dropout_rate = base_rate_by_era
  * (1 / school_quality_factor)           (low quality = more dropouts)
  * poverty_factor                        (destitute households: 3.0x, working: 1.5x, middle: 1.0x)
  * employment_opportunity_factor         (if teen jobs pay well, dropout rises)
  * peer_effect                           (neighborhood dropout rate: if >30%, self-reinforcing)
  * family_stability_factor               (single parent: 1.3x, two parent: 1.0x)
```

**Base Dropout Rates by Era:**

| Era | Base Rate (% of teens leaving before completion) |
|-----|----------------------------------------------|
| Frontier | 60% (education not mandatory, children work) |
| Industrial | 40% (child labor still common) |
| Postwar | 20% (compulsory education laws) |
| Modern | 10% (social pressure + requirements) |
| Future | 5% (online alternatives reduce total dropout) |

**Crime Correlation:**
```
crime_modifier_from_education = 1.0
  + (dropout_rate * 0.5)                  (each 10% dropout = +5% crime)
  + (unemployment_among_uneducated * 0.3) (unemployed dropouts = crime)
  - (vocational_employment_rate * 0.2)    (employed trades = less crime)
```

---

## 6. MEDIA SYSTEM

### 6.1 Media Types by Era

| Media Type | Era Available | Reach | Influence Power | Construction Cost | Operating Cost/Year | Unlock Tech |
|-----------|-------------|-------|----------------|------------------|--------------------| ------------|
| **Town Crier** | Frontier | 10 tiles | 5/100 | $0 (free with town hall) | $50 | None |
| **Newspaper** | Frontier+ | City-wide (literate pop only) | 20/100 | $2,000 | $1,000 | None |
| **Pamphlet Press** | Industrial+ | Neighborhood (distributed) | 15/100 | $500 | $200 | T038 (Telegraph) |
| **Radio Station** | Industrial+ | City-wide | 35/100 | $10,000 | $3,000 | T040 (Radio Broadcasting) |
| **Newsreel Cinema** | Postwar | City-wide (moviegoers) | 25/100 | $15,000 | $5,000 | T041 (Television) |
| **Television Station** | Postwar+ | City-wide | 60/100 | $30,000 | $10,000 | T041 (Television) |
| **Cable TV Network** | Modern+ | City-wide | 50/100 | $20,000 | $8,000 | T044 (Internet) |
| **Internet News Site** | Modern+ | City-wide (connected pop) | 40/100 | $5,000 | $3,000 | T044 (Internet) |
| **Social Media Platform** | Future | City-wide + regional | 70/100 | $2,000 | $1,000 | T045 (5G/6G) |
| **Streaming Service** | Future | City-wide | 45/100 | $8,000 | $4,000 | T045 (5G/6G) |

### 6.2 Media Influence Mechanics

Media outlets shape **public opinion** on policy issues. Each outlet has a **bias** and an **audience**.

**Media Bias Spectrum:**

| Bias | Favors | Opposes | Audience Tendency |
|------|--------|---------|------------------|
| **Pro-Business** | Low taxes, deregulation, industry | Environmental regulation, unions, welfare | Wealthy, upper-middle |
| **Pro-Labor** | Worker protections, transit, public housing | Tax cuts for wealthy, gentrification | Working class, destitute |
| **Pro-Environment** | Green energy, parks, emission controls | Industry expansion, highway building | Educated middle class, young adults |
| **Populist** | Whatever is popular, sensationalism | Nuanced policy, long-term planning | Broad working/middle class |
| **Independent/Centrist** | Balanced reporting, fact-based | Extremes on either side | Educated population |
| **Government-Aligned** | Current mayor's policies | Opposition viewpoints | Varies (state media) |

**Bias assignment**: When a media building is constructed, the player chooses its bias OR it is randomly assigned based on neighborhood character. Players can attempt to influence bias through funding, but direct control is limited in democratic governance modes.

### 6.3 Public Opinion Model

Media influence feeds directly into the political approval system (GDD Section 6.8):

**Opinion Shift Formula:**
```
opinion_shift_on_issue = SUM(for each media outlet:
  outlet_reach * outlet_influence_power * outlet_bias_on_issue * audience_trust
) / total_media_reach

Where:
  outlet_reach = % of population that consumes this outlet
  outlet_influence_power = base influence (see table above)
  outlet_bias_on_issue = -1.0 (strongly against) to +1.0 (strongly for)
  audience_trust = 0.3 (skeptical, educated pop) to 0.9 (trusting, low media literacy)
```

**Media Literacy Factor:**
```
media_literacy = education_index * 0.5 + civic_education_factor * 0.3 + internet_access * 0.2

Effects on audience_trust:
  media_literacy < 0.3: audience_trust = 0.9 (believe what they see/hear)
  media_literacy 0.3-0.6: audience_trust = 0.6 (moderate skepticism)
  media_literacy 0.6-0.8: audience_trust = 0.4 (critical consumers)
  media_literacy > 0.8: audience_trust = 0.3 (fact-check, compare sources)
```

**Gameplay implication**: A city with low education and a single pro-business TV station will strongly support whatever that station pushes. A city with high education and diverse media will be harder to sway but also harder to manipulate -- genuine policy quality matters more.

### 6.4 Media Events

Media outlets generate **events** that affect gameplay:

| Event | Trigger | Effect | Duration |
|-------|---------|--------|----------|
| **Scandal expose** | Corruption event + newspaper exists | Approval -15, public demands reform | 3 months |
| **Positive puff piece** | Media bias aligns with recent success | Approval +5 on related metric | 1 month |
| **Fear campaign** | Crime spike + sensationalist media | Public demands more police (even if crime is down) | 2 months |
| **Environmental panic** | Pollution event + green media | Public demands environmental action, blocks industrial expansion | 2 months |
| **Economic boosterism** | Pro-business media + growth period | Public supports business-friendly policies, +10% investment | 3 months |
| **Viral social media event** (Future) | Random, any issue | Rapid opinion swing (+-20 on random issue), fades quickly | 2 weeks |
| **Misinformation crisis** (Future) | Low media literacy + social media | Public confused on issue, approval volatile +-10 randomly | 1 month |
| **Investigative journalism** | Well-funded newspaper + city problem | Reveals hidden problem (infrastructure decay, budget waste), forces response | Until addressed |
| **Media blackout** | Authoritarian policy + state media only | No negative events from media, but happiness -10 long-term | Ongoing |

### 6.5 Propaganda & Media Freedom (Policy)

The player can set a **Media Freedom** policy that has major tradeoffs:

| Policy Level | Effect on Media | Effect on City | Era Available |
|-------------|----------------|---------------|-------------|
| **Free Press** | All biases operate freely, investigative journalism possible | Accurate public opinion, scandals exposed, harder to pass unpopular policies | All |
| **Licensed Media** | Media outlets require license (deny license to suppress), $500/yr revenue per license | Slight opinion control, some outlets silenced, -5 happiness if citizens notice | Industrial+ |
| **State Media** | Government controls all media messaging | Full opinion control on one issue per month, -15 happiness, -20 immigration (word gets out) | Industrial+ |
| **Censorship** | Block specific stories or outlets | -10 approval when discovered (50% chance/year), prevents scandal events | Industrial+ |
| **Internet Censorship** (Future) | Block social media, filter news sites | Prevents viral events and misinformation, -25 happiness among tech-savvy, tech companies leave | Future |

**Governance interaction**: Authoritarian governance styles (if the player has chosen that path) get cheaper access to state media and censorship. Democratic governance makes these policies more costly in approval and immigration.

### 6.6 Media Revenue & Economy

Media buildings are not just influence tools -- they contribute to the economy:

| Media Type | Jobs Created | Revenue Generated | Tax Income | Cultural Output |
|-----------|-------------|-------------------|-----------|----------------|
| Newspaper | 10-30 | $500-2,000/yr (subscriptions + ads) | $50-200/yr | +2 culture index |
| Radio Station | 15-40 | $2,000-8,000/yr (ads) | $200-800/yr | +5 culture index |
| TV Station | 50-150 | $10,000-40,000/yr (ads) | $1,000-4,000/yr | +10 culture index |
| Internet News | 5-20 | $1,000-5,000/yr (ads + subs) | $100-500/yr | +3 culture index |
| Social Media HQ | 100-500 | $20,000-100,000/yr | $2,000-10,000/yr | +15 culture index |

---

## 7. CURRICULUM & EDUCATION POLICY

### 7.1 Curriculum Policy Choices

These are city-wide policy settings that affect ALL public schools. Private and religious schools may deviate (at player's choice per-school).

**Curriculum Balance Sliders (must total 100%):**

| Subject Area | Min% | Max% | Default% | Effect of Emphasis |
|-------------|------|------|---------|-------------------|
| **Core Academics** (reading, math, science) | 30% | 70% | 45% | +literacy, +STEM base, standard workforce |
| **Vocational/Trades** | 0% | 40% | 15% | +trade skills, +industrial efficiency, -university prep |
| **Arts & Humanities** | 0% | 30% | 15% | +culture output, +creativity, +tourism |
| **Physical Education** | 5% | 25% | 10% | +citizen health, -healthcare cost, -obesity rate |
| **Civic Education** | 0% | 20% | 10% | +voter turnout, +civic engagement, -corruption |
| **Electives/Free Choice** | 0% | 20% | 5% | +student happiness, +creativity, less predictable output |

### 7.2 Controversial Policy Choices

These policies create **genuine gameplay dilemmas** with tradeoffs:

#### Standardized Testing vs. Creative Education

| Choice | Effect | Public Response |
|--------|--------|----------------|
| **Strict standardized testing** | +15% consistent graduate quality, -10% creativity, -5% student happiness, easy to measure school performance | Parents: mixed. Teachers: dislike (-5 teacher retention). Business: like. |
| **No standardized testing** | -10% consistent quality (variance increases), +15% creativity, +5% student happiness, harder to compare schools | Parents: mixed. Teachers: like (+5 retention). Business: uncertain. |
| **Balanced approach** | Neutral on all metrics, +5% school admin cost | Broadly acceptable, no strong reactions. |

#### Sex Education Policy

| Choice | Effect | Public Response |
|--------|--------|----------------|
| **Comprehensive sex ed** | -15% teen pregnancy rate, -10% STD rate, +5% birth control adoption | Religious communities: -20 happiness. Progressive: +10 happiness. |
| **Abstinence-only** | -5% teen pregnancy (weak effect), no STD impact | Religious communities: +10 happiness. Progressive: -15 happiness. |
| **No sex ed** | No effect on rates (natural baseline) | Neutral. |

Gameplay effect: Teen pregnancy affects dropout rate (pregnant teens 70% dropout rate). Lowering teen pregnancy keeps more young women in education, improving long-term workforce quality. But the policy choice angers a specific demographic.

#### Environmental Education

| Choice | Effect | Public Response |
|--------|--------|----------------|
| **Mandatory environmental ed** | +20% green policy support in next generation, +10% recycling participation, +5% green energy demand | Industry: -5 approval. Environmentalists: +15 approval. |
| **Optional environmental ed** | +5% green policy support, minor effects | Neutral. |
| **No environmental ed** | No effect, natural opinion formation | Industry: neutral. Environmentalists: -10 approval. |

#### Religious Education in Public Schools

| Choice | Effect | Public Response |
|--------|--------|----------------|
| **Secular only** | No religious influence on graduates, standard science curriculum | Religious communities: -10 happiness. Secular: +5 happiness. |
| **Comparative religion** | +5% cultural tolerance, +3% civic engagement | Religious communities: mixed. Secular: mixed. |
| **Dominant religion taught** | +10% community cohesion in religious neighborhoods, -5% science aptitude | Religious communities: +15 happiness. Secular/diverse: -20 happiness. Immigration from different religions: -30%. |

### 7.3 Education Budget Slider

The player allocates education funding through a budget slider (GDD Section 7.0):

**Education Budget as % of Total City Budget:**

| Allocation | Effect |
|-----------|--------|
| **0-5%** | Schools close. Education stops. Workforce degrades over 10 years. Crime spikes. |
| **5-10%** | Minimal funding. High student-teacher ratios, no facilities, high dropout. |
| **10-15%** | Below average. Schools function but poorly. Brain drain begins. |
| **15-20%** | Standard. Schools function adequately. Baseline outcomes. |
| **20-25%** | Good funding. Above-average outcomes. Attracts families. |
| **25-30%** | Excellent. Top-tier schools, low dropout, strong workforce pipeline. |
| **30%+** | Diminishing returns. Facilities maxed out. Budget pressure elsewhere. |

**Recommended gameplay balance**: 18-22% produces good education outcomes without starving other services. Below 12% triggers the education death spiral. Above 28% starves police/fire/health.

---

## 8. UNIVERSITY SYSTEM (EXPANDED)

### 8.1 University Types

Expanding the GDD's single "University" building into a richer system:

| University Type | Focus | Capacity | Construction Cost | Operating Cost/Yr | Research Points/Month | Graduate Output |
|----------------|-------|----------|------------------|-------------------|-----------------------|----------------|
| **Community College** | Practical education | 500 | $25,000 | $15,000 | 0 | Semi-professional workers |
| **Teaching University** | Undergraduate education | 1,000 | $80,000 | $40,000 | 2 RP | Standard professionals |
| **Research University** | Research + education | 2,000 | $150,000 | $60,000 | 10 RP | Professionals + researchers |
| **Technical Institute** | Engineering/STEM | 800 | $100,000 | $50,000 | 8 RP (STEM only) | Engineers, technicians |
| **Liberal Arts College** | Humanities/Arts | 400 | $60,000 | $30,000 | 3 RP (Culture) | Teachers, artists, writers |
| **Medical School** | Healthcare training | 300 | $120,000 | $70,000 | 5 RP (Medical) | Doctors, nurses, researchers |
| **Law School** | Legal training | 200 | $50,000 | $25,000 | 1 RP (Governance) | Lawyers, administrators |
| **Online University** (Future) | Distance learning | 10,000 | $20,000 | $15,000 | 1 RP | Mixed (lower quality: 0.7x) |

### 8.2 University Campus Effects

Universities create a **campus zone** that affects surrounding tiles:

| Effect | Radius | Magnitude |
|--------|--------|-----------|
| Land value increase | 10 tiles | +15-30% |
| Student housing demand | 15 tiles | Creates demand for low-rent residential |
| Commercial boost (cafes, bookstores) | 8 tiles | +20% commercial demand |
| Crime reduction | 10 tiles | -10% crime rate |
| Cultural output | 20 tiles | +5-15 culture index (depending on type) |
| Traffic generation | 5 tiles | Heavy pedestrian + vehicle traffic during terms |
| Property tax income | 0 (campus is tax-exempt) | -$X in lost property tax |
| Gentrification pressure | 15 tiles | Over time, student area gentrifies as graduates stay |

### 8.3 Research vs. Teaching Tradeoff

For Research Universities, the player faces the dilemma from the tech tree doc:

```
research_output = base_RP * (staff_assigned_to_research / total_staff)
teaching_quality = base_quality * (staff_assigned_to_teaching / total_staff)

Slider: Research Focus <----> Teaching Focus
  100% Research: 10 RP/month, teaching quality 0.5x (students poorly taught)
  50/50 Split:   5 RP/month, teaching quality 1.0x (balanced)
  100% Teaching:  0 RP/month, teaching quality 1.3x (excellent teaching, no research)
```

**Strategic choice**: Early in the game, you need research points to unlock technologies. But shifting too far toward research means your university produces poorly-educated graduates who become bad teachers, triggering the education death spiral. The optimal balance shifts over time.

---

## 9. IMPLEMENTATION NOTES

### 9.1 Data Structures

**School Entity:**
```
School {
  id: int
  type: SchoolType
  philosophy: Philosophy
  capacity: int
  enrolled: int
  teachers: int
  quality_score: float (0-100)
  condition: BuildingCondition
  facilities: Facility[]
  transport_mode: TransportMode
  bus_fleet_size: int
  funding_per_student: float
  curriculum: CurriculumBalance
  graduates_this_year: int
  dropout_rate: float
}
```

**Media Entity:**
```
MediaOutlet {
  id: int
  type: MediaType
  bias: MediaBias
  reach: float (0-1, fraction of population)
  influence_power: float (0-100)
  revenue: float
  employees: int
  audience_trust: float (0-1)
  active_narratives: Narrative[]
}
```

### 9.2 Simulation Tick Costs

| System | Update Frequency | Target Time | Strategy |
|--------|-----------------|-------------|----------|
| School quality | Monthly | <0.5ms | Recalculate only on change (funding, staffing, condition) |
| Graduation cycle | Yearly | <1ms | Batch process all schools |
| Media opinion shift | Weekly | <0.3ms | Aggregate, not per-citizen |
| Education metrics | Monthly | <0.5ms | Cached, dirty flag |
| School transport | Daily (traffic) | Included in traffic sim | Feed school trips into existing flow model |
| Media events | Weekly (roll) | <0.1ms | Probability check against event table |

### 9.3 Sprite & UI Requirements

**New Building Sprites Needed:**

| Building | Variants (era) | Total Sprites |
|---------|----------------|---------------|
| Schools (all types) | 5 eras x 8 types = 40, but many era-locked = ~20 | 20 |
| Universities (all types) | 3-4 types x 3 eras = ~10 | 10 |
| Media buildings | 10 types x relevant eras = ~15 | 15 |
| School bus (added to vehicle system) | 5 eras = 5 | 5 |
| **Total new sprites** | | **~50** |

**UI Panels Needed:**
- School management panel (per-school detail + city overview)
- Curriculum policy panel (sliders)
- Education statistics overlay (school quality heatmap, literacy rate, dropout hotspots)
- Media management panel (outlet list, bias display, reach metrics)
- Public opinion tracker (per-issue, showing media influence direction)
