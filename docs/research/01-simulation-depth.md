# Research: 01 Simulation Depth

The full research report is compiled at `/Users/fredericbeeg/.claude/plans/jazzy-whistling-piglet-agent-abed4e2a5de9def9c.md`. Here is a summary of findings across all 6 domains:

---

## Research Summary

### 1. Economic Dynamics
- **Victoria 3's Leontief Matrix** is the gold standard for city builder economics: flow-based (no stockpiles), fixed-proportion production, with discrete Production Methods as "matrix swaps." The key formula is `x = (I - A)^(-1) * d`. The "automation scam" (automating one factory can require MORE total workers across the supply chain) emerges naturally from matrix math.
- **Workers & Resources** proves that physical logistics (fuel costs, vehicle capacity, distance) should be THE core economic constraint, not abstract income. Emergent pricing at borders follows supply/demand elasticity curves that punish overspecialization.
- **Economic cycles** should emerge from systemic rules (Minsky's 3-stage debt model, fractional reserve banking, Kondratieff tech waves), not scripted events. Four design rules: finite currency, delayed feedback, imperfect information, destructible assets.
- **Real city finance** uses property tax with assessment lag, municipal bonds with credit ratings, TIF districts with spatial restrictions, and gets 30-40% from intergovernmental grants. Every city builder game gets this wrong by using a single tax slider.
- **Shadow economies** emerge when regulation cost exceeds compliance benefit. De Soto found that registering a legal business in Lima took 289 days and 31x minimum wage. 30-35% of developing world GDP is informal.
- **Automation displacement** follows a consistent historical pattern: agriculture 90% -> 2%, manufacturing 32% -> 8%, now information-age cognitive jobs. Each transition takes decades and creates massive social upheaval before new industries absorb displaced workers.

### 2. Political/Sociological Simulation
- **Frostpunk** uses dual meters (Hope/Discontent) with systemic consequences (not dice rolls). The Book of Laws exploits "creeping normality" -- each law is rational in isolation but the sequence degrades into totalitarianism. Key GDC insight: random consequences (50% suicide chance) felt meaningless; systemic consequences (permanent healthcare burden -> policy choice) felt powerful.
- **Victoria 3's Interest Groups** map political power through Wealth -> Political Strength -> Clout, modified by voting laws. The system elegantly models how democratization shifts power from wealthy elites to population masses.
- **NIMBY/YIMBY** is a real tension between local control and regional housing supply. Tokyo's national by-right zoning vs California's discretionary review creates wildly different housing outcomes. Prop 13's capped property taxes perversely incentivize commercial over residential zoning.
- **Gentrification** follows Neil Smith's Rent Gap Theory: capital flows when the gap between actual and potential ground rent exceeds a threshold. This is fully modelable as an emergent process.
- **Corruption** should be modeled as a rational equilibrium using the Principal-Agent problem: `EV(corruption) = AmountSkimmed * (1 - P(caught)) - Penalty`. When formal institutions are inefficient, corruption is the mathematically optimal strategy.

### 3. Religion and Culture
- Religious buildings historically served as BOTH spiritual AND economic centers across all civilizations (cathedral markets, medina souks, temple economies). Concentric zoning from the sacred center is a universal pattern.
- **Secularization** is triggered by existential security (Inglehart): as healthcare, food security, and safety improve, religious influence naturally declines -- but with a generational lag.
- **Ethnic clustering** follows Schelling's model: even mild cultural preferences (30% same-group neighbors) cascade into complete segregation. Chain migration and ethnic economies then lock clusters in place.

### 4. Infrastructure
- **Deferred maintenance compounds at ~7%/year**; emergency repairs cost 4-8x planned maintenance. This is the core "infrastructure trap" that makes it politically rational to defer but economically ruinous.
- **Induced demand (Duranton & Turner 2011):** Adding 10% road capacity increases traffic by exactly 10%. The Katy Freeway (26 lanes, $2.8B) made traffic WORSE. Braess's Paradox shows removing roads can improve flow.
- **Power grid failures** cascade through two mechanisms: transmission overload (rerouting) and frequency collapse (generation loss). Texas 2021 came within 4 minutes of total grid collapse requiring months to restart.
- **Water infrastructure** has hidden liabilities (lead pipes, combined sewers) with exponentially increasing cost curves -- first 50% of fixes is cheap, last 10-20% is prohibitively expensive.

### 5. Trade Systems
- Medieval trade networks created wealth through logistics (Hanseatic League) or arbitrage (Silk Road). Port cities developed institutional innovation, financial instruments, and political autonomy that inland resource-exporters never achieved.
- **Resource Curse / Dutch Disease:** Abundant resources HARM development by crowding out manufacturing, inflating local prices, and entrenching extractive institutions. Historical proof: Spain's silver caused deindustrialization while resource-poor England/Netherlands thrived.

### 6. Geography
- **Von Thunen's model** explains concentric land use based on transport costs and perishability, with rivers stretching rings along their banks. Geography determines city character (coastal = trade, mountain = defense/mining, plains = agriculture/expansion).
- Rivers provided the four essentials for ancient cities: agricultural surplus (silt), freshwater, transportation, and defense. Pre-railroad, shipping across the Mediterranean was cheaper than 100 miles overland.
- Resource distribution follows geological processes, not randomness -- terrain type should determine available resources.