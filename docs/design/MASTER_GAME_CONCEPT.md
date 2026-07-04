# CityMajor — Master Game Concept Document

> Working title: **CityMajor**
> Name candidates: Brickborn, CityMajor, Smoke & Steeple
> Version: 1.0 | March 2026 | Confidential

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Game Identity](#2-game-identity)
3. [Core Gameplay Loop](#3-core-gameplay-loop)
4. [Economic System Overview](#4-economic-system-overview)
5. [The 5 Eras (1850-2050+)](#5-the-5-eras)
6. [Simulation Systems Summary](#6-simulation-systems-summary)
7. [LLM Integration](#7-llm-integration)
8. [Visual Identity](#8-visual-identity)
9. [Technical Overview](#9-technical-overview)
10. [Multiplayer](#10-multiplayer)
11. [Modding](#11-modding)
12. [Business Strategy](#12-business-strategy)
13. [Competitive Positioning](#13-competitive-positioning)
14. [The Economic Control Spectrum](#14-the-economic-control-spectrum)

---

## 1. Executive Summary

### Elevator Pitch

CityMajor is a deep retro pixel-art city builder where you shape a city across 200 years of history — from gas-lit cobblestones to gleaming smart cities. It plays like **SimCity 4 meets Victoria 3**: zone and build like a classic city builder, or take direct control of your economy like an industrial planner. A single slider lets you choose your level of economic control at any time, from pure free market to full central planning. An embedded LLM gives every citizen a voice, every council meeting a debate, and every newspaper a headline that actually reflects what is happening in your city.

### Unique Selling Points

1. **The Economic Control Spectrum** — A continuous slider from laissez-faire capitalism to total planned economy. No other city builder lets you choose your economic ideology and live with the consequences.
2. **Two play styles, one game** — Casual city builders zone and watch their city grow. Strategy players micromanage production chains, set wages, and negotiate trade deals. Switch between both at any time.
3. **200 years of history** — Five distinct eras from 1850 to 2050+, each transforming your city's technology, architecture, politics, and culture. Your Victorian industrial town becomes a modern metropolis.
4. **LLM-powered narrative** — AI mayors negotiate trade deals in natural language. Citizens write letters to the editor. Council factions debate your policies. A daily newspaper reports on your city with procedurally generated stories grounded in real simulation data.
5. **Deep emergent simulation** — 100K households with individual satisfaction, cultural DNA, and political leanings. 45 tradeable goods across 20 production chains. 6 political factions that form organically. 70+ laws. 154 technologies. The simulation produces genuine emergence; the LLM gives it a voice.
6. **Songs of Conquest-quality pixel art** — Hand-crafted isometric sprites at 64x32 tiles with 5 era-specific palettes, full day/night cycles, seasonal changes, and weather effects.

### Target Audience

**Primary**: City builder enthusiasts aged 20-45 who have played SimCity, Cities: Skylines, or Anno and want more depth without sacrificing accessibility. Players who watched Victoria 3 streams and thought "I wish I could see this as a city, not a map."

**Secondary**: Grand strategy players (Paradox audience) looking for something more visual and intimate. Management sim fans (Factorio, Satisfactory) who enjoy production chains. Retro/pixel art game collectors.

**Estimated addressable audience**: 2-5 million players across Steam, based on comparable titles (Cities: Skylines sold 12M+, Anno 1800 sold 2.5M+, Workers & Resources sold 500K+ in EA).

### Platform & Price

| Milestone | Platform | Price | Target Date |
|-----------|----------|-------|-------------|
| Demo (Steam Next Fest) | Steam (Windows) | Free | Q3 2027 |
| Early Access launch | Steam (Windows) | $24.99 | Q1 2028 |
| EA major updates (3-4) | Steam (Windows) | $24.99 | 2028-2029 |
| 1.0 Release | Steam (Windows, Linux, macOS) | $29.99 | Q4 2029 |
| Console port (stretch) | Switch 2, PS5, Xbox | $34.99 | 2030+ |

### Team

Solo developer with AI-assisted development pipeline. Art contracted to pixel art specialists. Music and SFX licensed or commissioned. LLM integration uses self-hosted open-source models (zero ongoing API cost).

---

## 2. Game Identity

### What Makes This Different

Every city builder on the market forces you into one of two boxes: either you are a mayor who zones land and watches buildings sprout (SimCity, Cities: Skylines), or you are an industrial planner who micromanages every factory and supply route (Workers & Resources, Factorio). No game lets you be both — or anything in between.

CityMajor breaks this false dichotomy. The Economic Control Spectrum is a continuous slider that determines how much of the economy the player directly controls. At 0%, you are playing a polished SimCity-like experience: zone residential, commercial, and industrial areas, lay roads, build services, set tax rates, and watch your city grow organically. At 100%, you are running a centrally planned economy: you decide what every factory produces, at what price, for whom, and in what quantity. You allocate housing, assign labor, set import quotas, and negotiate five-year plans.

Most players will land somewhere in the middle. Maybe you control the energy sector and public transit but let private businesses handle retail and food. Maybe you nationalize steel production because your city needs it for a bridge megaproject, then privatize it once the project is done. The slider is not a menu selection — it is a lived experience. You feel the friction of central planning (corruption, inefficiency, shortages) and the chaos of free markets (inequality, boom-bust cycles, monopolies). The economic ideology you choose shapes your city's culture, politics, architecture, and destiny.

### The Dual Play-Style System

**City Builder Mode (Casual/Classic)** is SimCity with soul. You zone areas as Residential, Commercial, or Industrial. You lay roads, pipes, and power lines. You place police stations, schools, hospitals, and parks. Buildings grow organically based on demand — the simulation handles production, pricing, and trade automatically. You set tax rates and pass laws, but you never need to look at a production chain or negotiate a trade deal. The RCI demand bars tell you what your city needs; you provide the infrastructure and services for it to thrive.

This mode is for the player who wants to build a beautiful, functioning city without spreadsheets. It works. It is complete. You can play hundreds of hours in this mode and never touch the economic planner.

**Economic Planner Mode (Deep/Strategic)** peels back the automation and hands you the levers. You see every production chain: iron ore flows to steel mills, steel flows to construction companies and vehicle factories, vehicles flow to dealerships and logistics companies. You can intervene at any point. Set the steel mill's production target. Fix the price of bread. Subsidize the solar panel factory. Impose an export quota on lumber. Nationalize the railway. Establish a state investment bank.

This mode is for the player who wants Victoria 3's economic depth rendered as a living, breathing city instead of a spreadsheet overlay on a political map. You see the factory you nationalized. You see the workers walking in. You see the trucks hauling goods to the depot. The abstraction becomes concrete.

**The key design insight**: these are not separate game modes with separate UIs and separate tutorials. They are opposite ends of a single spectrum. The UI progressively reveals complexity as the player slides toward more control. A player at 30% (regulated market) sees minimum wage laws and environmental regulations but not production quotas. A player at 70% (state capitalism) sees state-owned enterprise boards and five-year plan targets. The transition is gradual, discoverable, and reversible.

### LLM-Powered Narrative

City builders have always been silent. Your citizens are numbers. Your advisors are tooltip text. Your newspaper is a scrolling ticker of canned messages. CityMajor uses a self-hosted language model (Qwen 2.5, running locally) to transform simulation data into narrative.

When you raise taxes, the LLM does not just show "-5% approval." It generates a letter from a factory owner explaining that the tax increase will force layoffs. The Business faction's representative argues in the city council that you are destroying jobs. The Daily Herald runs an editorial titled "Mayor's Tax Grab Threatens Industrial Core." A citizen named Maria Kowalski, who works at the affected factory, writes to the letters column worried about her mortgage.

None of this is scripted. The LLM receives structured simulation data — tax rate changed from 12% to 18%, 340 industrial jobs at risk, Business faction approval dropped 15 points, 3 factories in red on operating costs — and produces narrative that reflects reality. The stories are grounded. They reference real buildings, real neighborhoods, real policy decisions. They make the city feel alive in a way no city builder has achieved.

### Five Eras, 200 Years

Your city begins in 1850 as a frontier settlement with dirt roads and horse-drawn carts. Over 200 years of game time, it transforms through five distinct eras, each bringing new technologies, building types, social challenges, and visual styles. The transition is not a menu toggle — it is earned through research, population growth, and economic development. Your choices in early eras constrain and enable options in later ones. A city that industrialized heavily in Era 2 faces different environmental and political challenges in Era 4 than one that stayed agrarian. History is path-dependent, and so is your city.

### Emergent Culture and Politics

Every household in CityMajor has a Cultural DNA — an 8-dimensional vector encoding values like individualism vs. collectivism, tradition vs. progress, religiosity, work ethic, and environmental concern. Cultural DNA drifts over time based on the neighborhood, laws, education, media, and life events. When enough households in a district share cultural traits, that district develops a character: the bohemian arts quarter, the conservative suburb, the immigrant enclave, the tech hub.

Six political factions emerge from these cultural patterns: Industrialists, Progressives, Traditionalists, Greens, Populists, and Technocrats. They do not exist as fixed entities — they coalesce when enough citizens share political leanings. They lobby for laws, organize protests, run candidates in elections, and react to your policies. A planned economy empowers Industrialists and Populists; a free market empowers Progressives and Technocrats. Your economic choices reshape your political landscape.

---

## 3. Core Gameplay Loop

### The Casual Player's Session (City Builder Mode)

A typical 45-minute session for a casual player:

1. **Check the pulse** (2 min) — Open the game, glance at the budget summary, approval rating, and RCI demand bars. Read the morning newspaper headline. "Housing Crisis Deepens as Population Surges Past 50,000."

2. **Respond to demand** (10 min) — The R bar is maxed out. Zone a new residential district on the hill east of downtown. Lay a main road connecting it to the highway interchange. Extend water and power lines. Zone some commercial along the main road for neighborhood shops.

3. **Build services** (8 min) — The new district needs a school, a fire station, and a small park. Place them. Check the coverage overlay — the police station downtown covers about 60% of the new area. Consider adding a substation.

4. **Handle an event** (5 min) — A factory fire breaks out in the industrial district. Send extra fire units. The LLM-generated news report describes the scene. Three options appear: increase fire code regulations (costs money, prevents future fires), offer disaster relief to affected workers (costs money, boosts approval), or do nothing (free, but approval drops).

5. **Tweak policy** (5 min) — Open the budget panel. Property tax revenue is up from the new housing. Allocate extra funding to transit — the bus network is overcrowded. Pass a new environmental regulation to reduce pollution from the aging smelter district.

6. **Watch and enjoy** (10 min) — Speed up time. Watch buildings sprout in the new district. See the school fill up. Watch traffic patterns shift as residents commute downtown. A seasonal festival triggers — autumn leaves on the trees, a harvest market in the town square. The newspaper reports on the new district: "East Hill: From Farmland to Family Haven."

7. **Save and plan** (5 min) — Check the research tree. Queue up "Mass Transit" — the tram network will unlock next era. Glance at the 5-year budget projection. Everything is in the green. Save and exit.

The casual player never opened a production chain, never set a price, never negotiated a trade deal. The economy ran itself. The city grew. It felt like a great SimCity session — but with more narrative depth and more meaningful policy choices.

### The Deep Player's Session (Economic Planner Mode)

A typical 90-minute session for a strategic player:

1. **Review the economic dashboard** (10 min) — Open the production overview. Steel output is down 12% because the ore mine is running at 60% capacity (labor shortage in the mining district). Check the trade ledger: lumber imports are eating into the trade surplus. The state investment bank has $2.4M in uncommitted capital.

2. **Solve the steel crisis** (15 min) — The mining district has low satisfaction (long commutes, no entertainment). Build a transit line from the worker housing district. Place a community center near the mine. Adjust the mining wage upward by 8% to attract workers. Set a production target: 1,200 tons/month by Q3. Check the downstream impact — the construction company needs 800 tons/month for the bridge megaproject.

3. **Negotiate a trade deal** (10 min) — The AI mayor of Riverside offers lumber at 15% below market rate in exchange for a 3-year steel supply contract. Open the trade negotiation screen. The LLM-powered AI mayor makes their case: "Our forests are mature and our mills are idle. Your steel is the finest in the region. Let us build something together." Counter-offer: 10% below market for lumber, 500 tons/month steel commitment (not 700 as they asked). They accept with conditions — you must maintain quality standards.

4. **Manage the state-owned railway** (10 min) — The railway corporation (state-owned, you sit on the board) reports Q2 earnings. Ridership is up but maintenance costs are rising on the Victorian-era track. Authorize a $1.2M track modernization program. Raise ticket prices 5% to fund it. Check the political impact — the Populist faction objects to fare increases.

5. **Five-year plan review** (15 min) — Open the central planning board. Review production targets vs. actuals. Electronics factory is exceeding targets (good). Textile mill is underperforming (outdated equipment). Authorize a technology upgrade for the textile mill from the state investment bank. Set new 5-year targets: increase food production 20% (population growth), begin domestic electronics manufacturing (currently 100% imported), expand port capacity for export growth.

6. **Political management** (10 min) — The Greens are organizing a protest against the coal power plant. You have three options: announce a coal phase-out timeline (expensive, satisfies Greens, angers Industrialists), crack down on the protest (cheap, angers everyone, increases corruption), or negotiate a compromise (moderate cost, partial satisfaction). Choose the compromise — announce a 10-year transition to mixed energy with an immediate 15% emission reduction target. Pass the Clean Air Act.

7. **Urban development** (15 min) — The southern waterfront is underdeveloped. This is not just zoning — you are designing an economic zone. Place a mixed-use development: commercial ground floors, residential upper floors. Designate it as a Special Economic Zone with tax incentives for tech companies. Route a tram line from the university. Place a public waterfront park. Set a 5-year vision: transform this into the city's tech hub. Set land prices to attract the right development.

8. **Save and strategize** (5 min) — Check the city archetype detector. Your city is classified as "Industrial Powerhouse transitioning to Mixed Economy." The research tree suggests investing in "Information Technology" to unlock the next wave of economic development. Save.

The deep player touched production chains, trade negotiations, state-owned enterprises, five-year plans, and political faction management. They played a fundamentally different game from the casual player — but in the same city, with the same simulation, using the same UI that simply revealed more depth.

### The Spectrum Player's Session

Most players will not be at 0% or 100%. A player at 40% (mixed economy) might:

- Zone areas and build infrastructure like a classic city builder
- Directly manage public utilities (power, water, transit) as state-owned enterprises
- Set minimum wage and environmental regulations
- Let private businesses handle retail, food, and consumer goods
- Intervene when the market fails: subsidize housing during a crisis, impose price controls during inflation, nationalize a failing essential industry
- Negotiate trade deals for strategic goods (energy, steel) but let the market handle consumer imports

This is the sweet spot — the player who wants more control than SimCity offers but does not want to micromanage every bakery. CityMajor is designed so this middle ground feels natural, not like a compromise.

---

## 4. Economic System Overview

### The Leontief Snapshot Economy

CityMajor's economy is built on a **Leontief Input-Output model**, the same mathematical framework used by real-world economists to model national economies and inspired by Victoria 3's approach. The city is divided into 8-16 **market zones** based on transport connectivity. Each zone independently solves a 45x45 matrix equation every game-day that determines production, consumption, prices, and trade.

**How it works in plain language**: Every good in the economy has a recipe. Steel requires iron ore and coal. Furniture requires lumber and fabric. Electronics require steel, plastic, and skilled labor. The Leontief matrix encodes all these relationships. When demand for housing goes up, the model traces backward through the entire supply chain: more housing requires more construction materials, which requires more steel and lumber, which requires more ore and timber, which requires more miners and loggers. Prices adjust based on surplus and shortage. The economy finds equilibrium — or fails to, creating the shortages and inflation that make the game interesting.

**45 goods across 5 categories**:
- **Raw Materials** (8): grain, ore, timber, oil, coal, livestock, fish, cotton
- **Processed Goods** (12): flour, steel, lumber, fuel, plastic, fabric, meat, chemicals, glass, cement, paper, rubber
- **Manufactured Goods** (10): furniture, electronics, vehicles, clothing, tools, machinery, appliances, building materials, pharmaceuticals, consumer goods
- **Consumer Services** (8): food, beverages, medicine, luxury goods, entertainment, retail, hospitality, personal services
- **Professional Services** (7): healthcare, education, legal, finance, transport, telecommunications, tourism

**20 production chains** define how goods transform. Each chain has 1-4 inputs, a production rate per worker, a skill requirement, and technology multipliers. The chains are era-dependent — a Victorian-era steel mill uses different inputs and has different efficiency than a modern electric arc furnace.

### The Economic Control Spectrum: How It Interacts with the Economy

At **low control** (0-20%), the Leontief solver runs fully automatically. Buildings produce based on market demand. Prices float freely. Businesses open and close based on profitability. The player sees aggregate indicators (GDP, unemployment, inflation) but not individual production targets.

At **medium control** (20-60%), the player can override specific parameters. Set minimum prices for essential goods. Cap prices on housing. Subsidize production chains. Impose tariffs on imports. The solver incorporates these constraints — a price floor creates a surplus that must be exported or stockpiled; a tariff makes imports expensive and stimulates domestic production (but may cause shortages if domestic capacity is insufficient).

At **high control** (60-100%), the player replaces market signals with directives. Set explicit production targets per factory. Allocate labor quotas per sector. Fix all prices. The Leontief solver becomes a planning tool: given your targets, it tells you whether the math works. If you set steel production at 2,000 tons/month but only have ore supply for 1,500, the planning board flags the gap. You must resolve it — increase mining, reduce targets, or import the shortfall.

### Inter-Zone and Global Trade

Market zones trade with each other based on price differentials and transport costs. A steel surplus in the industrial zone flows to the construction zone if the price (plus transport cost) is lower than the construction zone's alternative supply. This creates organic trade flows within the city that the player can observe on a trade map overlay.

The **global market** represents the outside world. World prices fluctuate with random walks and mean reversion. The player's city can import goods at world price plus tariff plus transport cost, and export at world price minus export tax minus transport cost. Import/export capacity is constrained by infrastructure — ports, airports, and rail connections to the city boundary.

In multiplayer, the global market is supplemented by direct player-to-player trade negotiations.

### Budget and Fiscal System

Revenue comes from six sources: income tax, property tax, sales tax, corporate tax, tariffs, and fines. Expenses cover services (police, fire, health, education, transit, garbage), infrastructure maintenance, welfare, subsidies, and debt service. Every law affects the budget — raising the minimum wage increases worker satisfaction but raises operating costs for businesses, reducing corporate tax revenue.

The budget updates monthly with a rolling 12-month history and 5-year projections. Deficit spending is possible through municipal bonds (with interest), but prolonged deficits trigger credit rating downgrades, higher borrowing costs, and eventually a fiscal crisis event.

---

## 5. The 5 Eras

### Era 1: Foundation (1850-1880)

**Theme**: Frontier settlement becomes a proper town.

**Technology**: Horse-drawn transport, gas lighting, basic water pumps, timber construction, early telegraph. Steam power arrives mid-era.

**Buildings**: Wooden houses, general stores, churches, one-room schoolhouses, livery stables, grain mills, lumber yards, blacksmiths. Late era: first brick buildings, a small rail depot.

**Economy**: Agricultural base. Grain, livestock, timber are the primary goods. Trade is limited to what a horse cart can carry. The economy is simple — 8-10 active goods, 5-6 production chains.

**Social**: Small, homogeneous population. Low crime, high community cohesion. Religion is central. No formal political factions yet — the town council is a handful of prominent citizens.

**Player experience**: Intimate scale. You know every building by name. Place individual structures rather than zone large areas. The challenge is bootstrapping: get enough housing for workers, enough farms for food, enough trade to afford your first brick building. Decisions feel weighty because resources are scarce.

**Era transition trigger**: Population reaches 5,000, railroad connection established, first industrial building constructed.

**Visual palette**: Warm earth tones. Brown, ochre, forest green. Dirt roads. Scattered trees. Smoke from chimneys. Long shadows from low-angle gas lamps at night.

### Era 2: Industrial Revolution (1880-1920)

**Theme**: The machine age transforms a quiet town into a booming industrial center.

**Technology**: Steam engines, coal power, electric streetlights, telephones, early automobiles. Rail network expansion. Steel-frame construction unlocks taller buildings.

**Buildings**: Factories with smoking chimneys, tenement housing, department stores, city hall, fire stations, hospitals, train stations, warehouses, banks. Victorian and Edwardian architecture.

**Economy**: Industrialization unlocks 15+ new goods and 10+ production chains. Coal and steel become strategic resources. The factory system creates a working class with distinct economic interests. Trade volume increases dramatically with rail connections. First labor disputes.

**Social**: Rapid population growth through immigration. Class divisions emerge: factory owners vs. workers. The first political factions crystallize — Industrialists (pro-business, low regulation) and Populists (workers' rights, fair wages). Pollution becomes a visible problem. Overcrowded tenements breed disease.

**Player experience**: The city explodes in scale. Zoning becomes essential — you cannot place every building individually anymore. The RCI bars appear. Industrial demand is insatiable. The challenge shifts from scarcity to management: keep the factories running, house the workers, prevent the cholera outbreak, extend the tram network to the new districts. First encounter with the economic control spectrum — do you let the robber barons run wild, or do you regulate?

**Era transition trigger**: Population reaches 25,000, electricity grid covers 50% of city, at least 3 industrial production chains active.

**Visual palette**: Red brick, iron grey, smokestack black. Cobblestone streets. Gas and early electric lights create pools of warm yellow against dark streets. Industrial haze softens distant buildings.

### Era 3: Modern Age (1920-1970)

**Theme**: The automobile, mass media, and two world wars reshape the city.

**Technology**: Cars, highways, radio, television, commercial aviation, refrigeration, plastics, nuclear power (late era). Suburban sprawl begins.

**Buildings**: Art deco skyscrapers, suburban tract housing, shopping centers, drive-in theaters, airports, highway interchanges, power plants, public housing projects, modernist civic buildings.

**Economy**: The full 45-good economy is active. Consumer goods drive growth. The automobile industry creates massive supply chains. Suburbanization creates new economic patterns — commercial follows residential outward. Global trade intensifies with air freight.

**Social**: Suburbanization vs. urban decline. The white flight / urban renewal tension. Mass media creates shared culture but also political polarization. All 6 factions are active. The Greens emerge as pollution and urban sprawl become contentious. Cold War geopolitics affect trade (if player chooses planned economy, trade partners may shift).

**Player experience**: The map fills up. Highway planning becomes critical — one bad interchange can gridlock the city for decades. The suburban vs. urban density choice is the defining decision. Do you build sprawling suburbs with highways (American model) or dense urban cores with transit (European model)? The economic spectrum matters more than ever: postwar economies worldwide experimented with everything from Scandinavian social democracy to Soviet central planning.

**Era transition trigger**: Population reaches 100,000, computer technology researched, at least one renewable energy source online.

**Visual palette**: Concrete grey, chrome silver, pastel suburban colors. Wide roads. Neon signs at night. The contrast between gleaming downtown towers and fading industrial districts tells a story.

### Era 4: Information Age (1970-2020)

**Theme**: Computers, globalization, and environmentalism transform the economy and society.

**Technology**: Personal computers, internet, mobile phones, renewable energy, high-speed rail, GPS, biotechnology. Automation begins displacing industrial workers.

**Buildings**: Glass office towers, tech campuses, server farms, solar and wind farms, recycling centers, mixed-use developments, transit-oriented developments, renovated historic districts, big-box retail, data centers.

**Economy**: Services overtake manufacturing. The knowledge economy creates high-wage tech jobs alongside low-wage service jobs — inequality widens. Globalization means cheap imports undercut domestic manufacturers. The player must manage deindustrialization: retrain workers, attract new industries, or protect old ones with tariffs. E-commerce disrupts retail.

**Social**: Culture wars intensify. Progressives vs. Traditionalists. Gentrification displaces long-time residents. Immigration creates cultural diversity and political tension. Environmental movements gain power. The Technocrats emerge as a faction — pro-automation, pro-data, skeptical of both government and traditional business.

**Player experience**: The city is mature. Growth is slower but more complex. The challenge is adaptation: your industrial economy is obsolete, your highway infrastructure is crumbling, your population is aging, and a new generation wants bike lanes and coffee shops where the factory used to be. The economic spectrum creates radically different experiences: a free-market city becomes a tech hub with extreme inequality; a planned economy struggles with innovation but maintains employment.

**Era transition trigger**: Population reaches 250,000, AI/automation technology researched, smart grid infrastructure built.

**Visual palette**: Glass and steel blues, green from parks and trees, warm LED lighting at night. Solar panels glint on rooftops. The visual contrast between renovated and neglected districts is stark.

### Era 5: Tomorrow (2020-2050+)

**Theme**: Climate change, artificial intelligence, and radical new technologies reshape urban life.

**Technology**: AI and automation, autonomous vehicles, fusion power (late era), vertical farming, hyperloop, 3D-printed buildings, arcologies, smart city sensors, carbon capture.

**Buildings**: Green skyscrapers with vertical gardens, autonomous vehicle depots, fusion plants, arcologies (self-contained mega-buildings), underground transit networks, drone delivery hubs, urban farms, carbon capture facilities.

**Economy**: Automation has replaced most routine labor. The fundamental economic question changes: in a world where machines produce most goods, how do you distribute wealth? Universal basic income, robot taxes, post-scarcity economics — these are not abstract debates but concrete policy levers the player must pull. The economic spectrum becomes its most interesting: a planned economy can allocate automation's benefits equitably; a free market creates a tiny ultra-rich class and mass unemployment.

**Social**: The population is diverse, educated, and politically active. Climate refugees may arrive (depending on global events). The tension between human labor and automation defines politics. New factions or faction realignments are possible. The city's identity — its archetype — is fully formed and recognized globally.

**Player experience**: The endgame. Your city is either a gleaming utopia or a crumbling dystopia, and probably something complicated in between. The challenge is legacy: can you solve climate change at the city level? Can you manage the automation transition without social collapse? Can you build an arcology? The sandbox opens up — with fusion power and advanced automation, resource constraints loosen, and the game becomes about vision rather than survival.

**Era transition**: There is no Era 6. Era 5 is the open-ended sandbox. The city continues to evolve indefinitely.

**Visual palette**: Clean whites and greens for sustainable buildings. Holographic blue UI elements on smart buildings. Lush vegetation integrated into architecture. At night, the city glows with subtle, efficient lighting — no more garish neon, unless your city went cyberpunk.

### Era Transition Mechanics

Era transitions are not instant. When transition conditions are met, a 2-year transition period begins. During this period:

- New era buildings become available but old era buildings still function
- A "golden age" event fires, boosting satisfaction and growth
- The newspaper runs a special edition celebrating the milestone
- The visual palette gradually shifts — new buildings use the new style, old ones remain
- Some old technologies become obsolete (coal power efficiency drops, horse carts disappear)
- New political tensions emerge as society adjusts

The player can delay an era transition by not researching the trigger technology, but this comes at a cost: competing cities (AI or multiplayer) that advance first gain trade and migration advantages.

---

## 6. Simulation Systems Summary

### Population

Up to **100,000 households** (representing ~350,000-500,000 individual citizens) are simulated with individual satisfaction scores, income, savings, employment, cultural DNA, and political leanings. Each household is 64 bytes; each citizen is 32 bytes — the entire population fits in L3 cache for fast iteration. Households make monthly decisions about employment, housing, consumption, and whether to stay or leave the city. Migration is driven by a multi-factor attractiveness model: jobs, housing availability, quality of life, taxes, and services compared against a global baseline. Population demographics follow realistic lifecycle tables — births, deaths, aging, education, retirement. Neighborhoods develop emergent character based on the aggregate cultural DNA of their residents.

### Economy

A **Leontief Input-Output model** with **45 goods** across **20 production chains**, divided into **8-16 market zones**. Each zone independently solves supply, demand, and pricing every game-day via a 45x45 matrix equation. Prices adjust toward equilibrium based on surplus and shortage. Inter-zone trade follows price differentials and transport costs. A global market provides import/export at fluctuating world prices, constrained by port and rail capacity. The player's position on the Economic Control Spectrum determines which parameters are automated and which are player-controlled. The economy supports everything from Victorian-era grain mills to modern tech campuses, with production chains unlocking across eras.

### Transport

Traffic uses a **macroscopic Bureau of Public Roads (BPR) flow model** — not individual vehicle pathfinding. The road network is extracted as a directed graph (25K nodes, 50K edges). 100K households are compressed into ~512 traffic zones. A gravity model generates an origin-destination matrix. **Frank-Wolfe algorithm** assigns flows to routes, converging to Wardrop user equilibrium. **Contraction Hierarchies** provide O(log n) shortest-path queries — 512 zone queries in 5ms vs. 1 second for naive Dijkstra. Visible vehicles are cosmetic sprites following pre-assigned paths. The system supports **15+ transit modes** across eras: walking, horse cart, horse tram, steam rail, electric tram, bus, subway, automobile, highway, monorail, light rail, high-speed rail, bicycle, autonomous vehicle, and hyperloop.

### Services

**Six service categories**: fire, police, health, education, parks, and utilities (power, water, sewage, garbage). Each service building has a coverage radius and capacity. Coverage is computed as a spatial overlay on the 1,024-partition grid — a fire station covers X tiles, a hospital serves Y citizens. When demand exceeds capacity, service quality degrades: longer emergency response times, overcrowded schools, rolling blackouts. Utility networks (power, water) are grid-based with supply/demand balance computed per substation zone every tick. Service quality is a major driver of household satisfaction and land value.

### Politics

**Six factions** — Industrialists, Progressives, Traditionalists, Greens, Populists, Technocrats — emerge from the aggregate political leanings of the population. Each faction has approval ratings that shift based on laws, economic conditions, events, and the player's choices. **70+ laws** across 10 categories (taxation, zoning, environment, labor, public safety, education, healthcare, transport, economy, social policy) can be enacted, repealed, or adjusted via sliders. Laws have cascading effects on the simulation: raising the minimum wage increases worker satisfaction but raises business costs, potentially causing layoffs, which reduces tax revenue and increases welfare spending. **Elections** occur periodically — the player's approval determines whether they stay in office (losing an election triggers a game-over or a handicapped continuation mode). **Protests** erupt when faction approval drops too low, affecting city function and satisfaction.

### Research

**154 technologies** across **7 tech eras** (Foundation through Future). Each technology has prerequisites, a research cost, and 1-4 effects on the simulation. Effects include unlocking new building types, new production chains, new laws, efficiency bonuses, and new event types. Research rate depends on education buildings (universities, research labs), education law funding, and citizen education levels. The tech tree has branching decisions — investing in nuclear power vs. renewables, highway infrastructure vs. mass transit, automation vs. labor protection. These branches are not cosmetic; they gate entire gameplay systems and constrain future options.

### Events

**80+ event types** across 11 categories: natural disasters (earthquake, flood, tornado, wildfire, blizzard, drought), man-made crises (factory fire, chemical spill, blackout, building collapse), economic shocks (recession, boom, trade embargo, market crash), social upheaval (protest, riot, festival, epidemic, crime wave), political drama (scandal, corruption, reform movement), environmental crises (pollution emergency, heatwave), infrastructure failures (bridge collapse, pipe burst, grid overload), cultural moments (landmark discovery, heritage festival), sports events (championship, Olympic bid), technology events (breakthrough, cyber attack), and global events (war, pandemic, refugee crisis, climate accord). Each event is a state machine with phases (Brewing, Active, Waning, Aftermath), spatial effects, player response options, and chain reactions. Events are triggered by probability, threshold conditions, or cascading from other events.

### Culture

**8 cultural dimensions** define every household's values: individualism-collectivism, tradition-progress, religiosity-secularism, work-leisure balance, environmentalism, risk tolerance, authority respect, and community orientation. These dimensions drift over time based on neighborhood, education, media exposure, economic conditions, and laws. When households cluster by cultural profile, neighborhoods develop distinct identities. **20 city archetypes** (Industrial Powerhouse, Tech Hub, Cultural Capital, Green Paradise, Trade Gateway, etc.) emerge from aggregate city-level cultural and economic metrics. **10 global cultural regions** (Western European, Eastern European, North American, East Asian, South Asian, Middle Eastern, Latin American, Sub-Saharan African, Southeast Asian, Oceanic) provide starting cultural templates that influence building styles, religious buildings, cuisine, festivals, and social norms.

---

## 7. LLM Integration

### Architecture

CityMajor embeds a **self-hosted language model** (Qwen 2.5 7B, quantized to 4-bit, running on the player's GPU) that transforms structured simulation data into natural language narrative. The LLM never drives the simulation — it reads simulation state and produces text. If the LLM is unavailable (weak hardware, player preference), the game falls back to template-based text with no gameplay impact.

The LLM operates on a **request queue** with priority levels. High-priority requests (trade negotiations, council debates) are processed immediately. Low-priority requests (newspaper flavor text, citizen letters) are queued and processed during idle frames. Target latency: <2 seconds for high-priority, <10 seconds for low-priority. Token budget per request: 200-500 tokens output.

### AI Mayors

Each neighboring city in the region is governed by an AI mayor with a personality profile: economic ideology (free market to planned), risk tolerance, trade aggressiveness, diplomatic style (friendly, shrewd, hostile). When the player initiates or receives a trade proposal, the AI mayor responds in natural language, referencing their city's economic situation and their personality.

Example interaction:
> **Mayor Chen of Riverside**: "Our timber surplus is becoming a storage problem, frankly. I know your construction sector is booming — I saw the bridge project in the Herald. Here is what I propose: 500 cubic meters per month at 12% below the Commodities Exchange rate, locked for 18 months. In exchange, I need a commitment on steel — 200 tons monthly. My rail network expansion depends on it."

The player responds by adjusting terms on a negotiation panel (quantity, price, duration, conditions). The AI mayor reacts to counter-offers with personality-appropriate responses — Mayor Chen might accept a reasonable counter; Mayor Volkov of Irongrad might walk away from anything less than their opening offer.

### Dynamic Quests

The LLM generates contextual quests grounded in simulation state. These are not random — they respond to what is actually happening in the city.

- **Low healthcare coverage** triggers: "Dr. Amara Osei petitions the city council for a clinic in the Riverside district. She reports treating patients in her apartment because the nearest hospital is a 45-minute bus ride away."
- **Industrial pollution spike** triggers: "The Millbrook Residents' Association has collected 2,300 signatures demanding action on air quality. Lead organizer James Whitfield says his daughter's asthma has worsened since the new chemical plant opened."
- **Budget surplus** triggers: "Councilwoman Park proposes a citywide fiber optic network, arguing it will attract tech companies. Estimated cost: $4.2M. Councilman Reeves counters that the money should fund affordable housing."

Each quest presents 2-3 options with clearly stated trade-offs. The outcomes are determined by the simulation, not the LLM — choosing to build the clinic actually builds a clinic and affects healthcare coverage, satisfaction, and budget.

### Council Debates

When the player proposes a major law or policy change, the city council convenes. Representatives from affected factions argue for or against the proposal, referencing real simulation data. The LLM generates these arguments based on faction ideology, current city conditions, and the specific policy.

> **Industrialist Representative**: "This emission cap will cost our steel mills an estimated $340,000 per year in compliance upgrades. Three of our five mills are already operating at thin margins. We risk losing 800 jobs."
>
> **Green Representative**: "The health data is clear. Respiratory hospital admissions in the South End are 3.2 times the city average. Every year we delay costs lives and healthcare dollars. The mills can modernize or they can leave — either outcome improves public health."
>
> **Populist Representative**: "My constituents work in those mills. They want clean air AND their jobs. Phase it in over 5 years. Give the mills time and give workers retraining options."

The debate is not just flavor — faction representatives' arguments reflect real data, and the player can use the debate to understand the trade-offs before voting.

### Citizen Stories

Individual citizens (procedurally generated with names, occupations, neighborhoods, and family situations derived from household data) occasionally surface with stories. These are low-priority LLM requests that add texture to the simulation.

- A letter to the editor from a retiree complaining about the bus route change
- A social media post from a young professional celebrating their new apartment in the waterfront development
- A local business owner interviewed about the impact of the new minimum wage
- A teacher's testimony at a school board hearing about overcrowded classrooms

These stories are opt-in — the player can read them in the newspaper, ignore them, or disable them entirely.

### The Daily Herald (City Newspaper)

Every game-day, the newspaper generates 3-5 stories based on simulation events. Stories are categorized: front page (major events, policy decisions), business (economic data, trade deals, new businesses), local (neighborhood happenings, human interest), sports (league results, stadium events), opinion (editorials reflecting faction viewpoints).

The newspaper serves as both narrative flavor and an information tool. A perceptive player can learn about problems before they become crises by reading between the lines of seemingly innocuous local stories.

---

## Web v1 pivot (2026-07)

> **Linear [SB-3704](https://linear.app/softblaze/issue/SB-3704).** CityMajor ships **web-only** as mesh 3D mid-fidelity (R3F + Three.js), not native Steam + pixel art. Simulation design below is largely unchanged; rendering, platform, and monetization sections are updated in §8–9 and §12.

| Dimension | Original plan (this doc) | **Web v1 (current)** |
|-----------|--------------------------|----------------------|
| Platform | Steam native, Forge Engine | **Browser** (Next.js shell + R3F canvas) |
| Rendering | Isometric 2D sprites, OpenGL | **Perspective mesh 3D**, `InstancedMesh` + LOD |
| Art | Premium pixel art, 600+ sprites | **Modular GLTF kitbash** (~40–60 archetypes/era) |
| Map scale | 1024×1024 / 50k buildings | **256×256 / ~5k buildings** (phased) |
| Monetization | $24.99 premium, no MTX | **Free core + Founder Pass $24.99 + cosmetics** |
| LLM | Self-hosted Qwen on player GPU | **Backend API + quotas**; templates always |

Historical pixel-art and Steam-native content in this document is **superseded for v1** but retained for sim/economy reference.

---

## 8. Visual Identity

### Art Direction

**Web v1:** Mid-fidelity mesh 3D (Cities: Skylines lite) — modular GLTF buildings assembled procedurally per era, perspective orbit camera, shared PBR materials. Hero landmarks (8–12/era) hand-polished; bulk volume via instanced archetypes.

**Original (superseded for v1):** CityMajor targeted **Songs of Conquest-level pixel art quality** — hand-crafted sprites with deliberate palette choices, expressive lighting, and meticulous attention to silhouette readability at zoom levels from neighborhood close-up to full-city overview.

**Isometric grid**: 64x32 pixel tiles (standard 2:1 isometric ratio). Buildings occupy 1x1 to 8x8 tile footprints depending on type and density. Sprites are drawn at 2x resolution (128x64 base tile) and downscaled for crisp rendering at default zoom.

**Pixel discipline**: No anti-aliasing in the sprite art. No sub-pixel rendering. Clean pixel edges at all zoom levels. Outlines are 1px dark (not black — a darker shade of the fill color). Dithering is used sparingly for gradients and atmospheric effects, following established patterns (checkerboard for mid-tones, ordered 4x4 for subtle transitions).

### Era Palettes

Each era has a distinct color palette that defines its visual character. Buildings constructed in an era retain that era's palette even as the city advances, creating a visual archaeology — you can read the city's history in its color.

**Era 1 — Foundation (1850-1880)**:
Warm earth tones. Base: `#8B7355` (timber brown), `#A0522D` (brick sienna), `#6B8E23` (field green), `#F5DEB3` (wheat). Accent: `#FFD700` (gas lamp gold), `#4A4A4A` (iron grey). Sky: warm amber at dusk, pale blue by day.

**Era 2 — Industrial (1880-1920)**:
Red brick and iron. Base: `#8B4513` (dark brick), `#A52A2A` (industrial red), `#708090` (slate grey), `#2F4F4F` (dark iron). Accent: `#FF8C00` (furnace orange), `#FFFACD` (electric lamp). Atmospheric haze: `#C0B8A4` (smog wash) at 15% opacity over distant buildings.

**Era 3 — Modern (1920-1970)**:
Concrete and chrome. Base: `#B0B0B0` (concrete), `#C0C0C0` (chrome), `#87CEEB` (glass blue), `#FAFAD2` (cream suburban). Accent: `#FF4500` (neon red), `#00FF7F` (neon green). Increased saturation for commercial districts, muted for residential.

**Era 4 — Information (1970-2020)**:
Glass and green. Base: `#4682B4` (steel blue glass), `#2E8B57` (park green), `#F0F0F0` (white modernist), `#696969` (dark glass). Accent: `#1E90FF` (LED blue), `#FFD700` (warm interior). Night palette shifts dramatically — office towers glow blue-white, residential is warm yellow.

**Era 5 — Tomorrow (2020-2050+)**:
Clean tech. Base: `#F8F8FF` (ghost white), `#00CED1` (tech cyan), `#98FB98` (living green), `#E6E6FA` (lavender). Accent: `#7B68EE` (holographic purple), `#00FA9A` (status green). Buildings have integrated vegetation — green pixels breaking geometric lines.

### Day/Night Cycle

A full day/night cycle completes every 6 real seconds at normal game speed (1 game-day = 1 real second, but the visual cycle is slowed for enjoyment). The cycle has 6 phases:

1. **Dawn** (warm pink-orange sky, long shadows from the east, lights turning off)
2. **Morning** (bright, clean light, full saturation)
3. **Midday** (slight desaturation, short shadows, heat shimmer in summer)
4. **Afternoon** (warm golden hour light, shadows lengthening west)
5. **Dusk** (deep orange-purple sky, lights turning on, windows glow)
6. **Night** (dark blue sky, city lights dominate, street lamps create light pools)

Window lights are per-building: residential buildings light up at dusk (warm yellow), commercial at business hours (cool white), industrial runs 24/7 (orange glow from furnaces). The night cityscape should be one of the game's most screenshot-worthy moments.

### Seasons

Four seasons cycle every game-year (6 real minutes at normal speed):

- **Spring**: Fresh green, cherry blossoms on park trees, rain showers
- **Summer**: Full green, bright light, occasional thunderstorms, heat haze
- **Autumn**: Orange-red foliage, falling leaves (particle effect), harvest markets
- **Winter**: Snow on rooftops and roads, bare trees, ice on water, shorter days

Seasons affect gameplay: winter increases heating demand (power/fuel consumption), summer increases cooling demand (in later eras), spring flooding is possible near rivers, autumn harvest affects food supply.

### Weather

Procedural weather system with 8 states: clear, partly cloudy, overcast, rain, heavy rain, thunderstorm, snow, fog. Weather affects visibility (fog reduces render distance), mood (overcast reduces a palette's saturation by 10%), and gameplay (heavy rain increases flood risk, thunderstorms can cause fires from lightning). Weather transitions smoothly over 30-60 game-minutes.

### Zoom Levels

**Web v1 (mesh LOD):** Same five conceptual zoom bands, implemented as mesh LOD swaps + instancing:

1. **Street** (L0): Full GLTF modules + detail; citizens as simple meshes or instanced dots; traffic as colored flow particles.
2. **Neighborhood** (L1): Simplified meshes; emissive window materials at night.
3. **District** (L2): Instanced boxes + zone color tint; overlays readable.
4. **City** (L3): Colored blocks / heatmap by zone; major roads and terrain dominate.
5. **Region** (L4): City cluster silhouette; multiplayer/trade context (phase 4).

LOD hysteresis at thresholds to avoid pop-in. See [`VISUAL_QUALITY_GUIDE.md`](VISUAL_QUALITY_GUIDE.md) header for perf checklist.

**Original pixel plan (superseded for v1):**

1. **Street** (closest): Individual citizens visible as 4x8 pixel sprites. Read shop signs. See individual trees. Count cars on roads.
2. **Neighborhood**: Buildings are fully detailed. Road markings visible. Parks show individual elements (benches, fountains). Traffic flow visible.
3. **District**: Buildings simplified to top-down silhouettes. Road network clear. Zone colors visible (R=green, C=blue, I=yellow). Service coverage overlays readable.
4. **City**: Full city visible. Buildings are colored blocks by zone type. Major roads and transit lines visible. Terrain features (rivers, hills, forests) dominate.
5. **Region** (farthest): Multiple cities visible for multiplayer/trade context. Your city is a glowing cluster. Neighboring cities are distant silhouettes.

---

## 9. Technical Overview

### Web v1 stack (current)

| Layer | Choice |
|-------|--------|
| **Shell / UI** | Next.js 16 + React 19 — HUD as HTML overlays on canvas |
| **3D** | React Three Fiber + Three.js — `InstancedMesh`, chunk frustum culling, LOD |
| **Simulation** | C# → **.NET 8 WASM** in Web Workers (reuse Forge sim ~9.4k LOC) |
| **Sync** | Double-buffer snapshots: WASM tick → read-only render snapshot → R3F `useFrame` |
| **Assets** | Modular GLTF per era on CDN; 20–40 draw calls/chunk budget |

Forge Engine (SDL2 + OpenGL sprite batcher, ~12.5k LOC) remains in-repo as **sim wiring reference** during migration; it is not the shipping renderer.

**Rendering (mesh 3D):**
- `InstancedMesh` per building archetype per visible chunk (not per unique building)
- 32×32 tile chunks — matches sim spatial partitions; frustum cull per chunk
- LOD swaps at zoom thresholds (full GLTF → simplified → boxes → heatmap)
- Day/night: hemisphere light + emissive window materials; tiered SSAO/bloom
- Terrain: heightmap or chunked plane meshes, merged at load

### Scale Targets

**Web v1 (locked):**

| Metric | Target | Notes |
|--------|--------|-------|
| Map size | **256×256** tiles (65,536) | 64 chunks @ 32×32 |
| Buildings | **~5,000** max instanced | ~40–60 archetypes/era |
| Households | **~10,000** | One era arc |
| Draw calls | 20–40 per visible chunk | Instancing + chunk culling |
| FPS | ≥30 integrated / ≥60 discrete | LOD + quality tiers mandatory |

**Full vision (post-v1, unchanged sim ceiling):**

| Metric | Target | Memory |
|--------|--------|--------|
| Map size | 1024x1024 tiles (1,048,576 tiles) | 18 MB tile data |
| Buildings | 50,000 max | 6.1 MB |
| Households | 100,000 max | 6.25 MB |
| Citizens | 500,000 max | 15.3 MB |
| Road nodes | 25,000 | 586 KB |
| Road edges | 50,000 | 1.53 MB |
| Spatial partitions | 1,024 (32x32 grid) | 256 KB |
| Traffic zones | 512 | 16 KB |
| Market zones | 8-16 | 32 KB |
| Cosmetic vehicles | 10,000 | 400 KB |
| **Total simulation memory** | | **~50 MB** |

### Threading Model

The simulation uses a **producer-consumer double-buffer** architecture:

- **Simulation thread(s)**: Run the tick loop (L0-L3) on the current simulation state. When a tick completes, publish a read-only snapshot to the render buffer.
- **Render thread**: Reads the latest snapshot and draws the frame. Never touches simulation state directly.
- **LLM thread**: Processes the language model request queue asynchronously. Writes completed text to a thread-safe output buffer that the UI reads.
- **I/O thread**: Handles save/load, autosave, and asset streaming.

The simulation itself uses `Parallel.For` for data-parallel work (building aggregation, partition updates, traffic assignment). On an 8-core CPU, the simulation targets 10-20 ticks/second with <16ms total frame time (simulation + render).

**Web v1 minimum**: Modern browser with WebGL2 + WASM; COOP/COEP for SharedArrayBuffer; integrated GPU (30 FPS at max fill with LOD).
**Recommended**: Discrete GPU, 8 GB RAM, broadband for GLTF CDN.
**LLM (web)**: Backend API at launch — free tier 10 LLM events/day; Founder Pass unlimited. Template fallback always available (no player-side model).

### Save System

Binary serialization using a custom format (not JSON, not XML — raw structs with version headers). A full city save at max scale is ~80 MB uncompressed, ~25 MB with LZ4 compression. Save time target: <2 seconds. Load time target: <5 seconds. Autosave runs on the I/O thread every 5 game-years (configurable).

---

## 10. Multiplayer

### Regional Co-op (2-4 Players)

CityMajor's multiplayer is **cooperative regional play**. 2-4 players each build their own city on a shared regional map. Cities are separate (each player controls their own) but connected by trade routes, shared infrastructure, migration, and regional events.

### How It Works

- Each player starts a city at a chosen location on the regional map
- A shared road/rail network connects the cities (players can collaboratively build inter-city infrastructure)
- Trade happens through direct negotiation (LLM-assisted AI is replaced by player-to-player chat) or automatic market mechanisms
- Migration flows between cities based on relative attractiveness — if Player A's city has better jobs but Player B's has better services, workers commute or relocate
- Regional events affect all cities: a recession hits the whole region, a flood threatens cities along the same river, a global trade embargo cuts off all cities from imports
- Players can specialize: one city focuses on heavy industry, another on tech, another on agriculture. Regional trade makes specialization viable and rewarding

### Shared Disasters

Natural disasters have regional scope. A river flood affects all downstream cities. A wildfire can spread from one city's outskirts toward another. An earthquake's epicenter might be between two cities, damaging both. Players must coordinate disaster response — sharing fire units, accepting refugees, providing emergency supplies.

### Trade Negotiations

In multiplayer, trade negotiations happen in real-time between players. The trade UI shows both cities' supply/demand/price data (with optional information hiding for competitive play). Players can negotiate complex multi-good, multi-year deals. A shared trade log tracks all agreements and fulfillment.

### Technical Implementation

- Peer-to-peer networking with one player as host
- Simulation runs independently per city; only trade/migration/event data is synchronized
- Network bandwidth is minimal — synchronizing aggregate economic data and events, not individual entities
- Save files include the full regional state; any player can host from a shared save
- Desync detection via periodic checksum comparison of aggregate state

### Competitive Variant (Optional)

An optional competitive mode adds city rankings, achievement races, and the possibility of economic warfare (dumping cheap goods to undercut a rival's industry, poaching workers with higher wages, lobbying for regional laws that disadvantage competitors).

---

## 11. Modding

### Three-Tier Modding Architecture

CityMajor is built with modding as a first-class feature, not an afterthought. Three tiers of modding support increasingly deep customization.

### Tier 1: Data Mods (JSON)

All game data is defined in JSON files that modders can override:

- **Buildings**: Add new building types with custom sprites, stats, and production chains
- **Goods**: Add new goods to the economy (extends the Leontief matrix)
- **Production chains**: Define new input-output relationships
- **Technologies**: Add new techs to the research tree
- **Laws**: Add new laws with custom effects
- **Events**: Define new event types with triggers, effects, and response options
- **Cultural profiles**: Add new cultural archetypes and city archetypes
- **Map themes**: New terrain types, climate settings, starting conditions

Data mods require no programming. They load automatically from a `mods/` directory and merge with base game data. Conflicts are resolved by load order (last wins).

### Tier 2: Script Mods (C#)

For deeper customization, modders can write C# scripts that hook into the simulation:

- **Custom simulation systems**: Add entirely new simulation layers (religion, crime syndicates, space program)
- **Custom AI behaviors**: Replace or extend AI mayor personalities
- **Custom event logic**: Complex multi-phase events with scripted outcomes
- **Custom UI panels**: Add new information displays and control panels
- **Custom LLM prompts**: Modify how the LLM generates text for different contexts

Script mods are compiled at load time using Roslyn. They run in a sandboxed AppDomain with limited system access (no file I/O outside the game directory, no network access).

### Tier 3: Total Conversion

Total conversion mods can replace the entire game content:

- Custom era definitions (medieval, sci-fi, fantasy, alternate history)
- Custom map generators
- Custom rendering shaders
- Custom music and sound packs

### Steam Workshop Integration

Full Steam Workshop support at launch:
- One-click install/uninstall
- Automatic dependency resolution
- Mod compatibility checker
- Mod load order manager
- Workshop ratings and reviews

---

## 12. Business Strategy

### Revenue Model (Web v1)

**Free-to-play core + Founder Pass + cosmetic shop.** Sim depth is never paywalled; cosmetics and convenience only.

| Tier | Price | Includes |
|------|-------|----------|
| **Free** | $0 | Full v1 gameplay (256×256, era arc, all core sim). 3 cloud saves. **10 LLM narrative events/day.** |
| **Founder Pass** | **$24.99** one-time | Unlimited LLM. 20 cloud saves. Founder monument skin. 3 cosmetic building packs. Early era-2 access. Credits name. |
| **Cosmetic shop** | $3–$15/item | Facade skins, landmark variants, mayor office themes — **sim-neutral only.** Stripe at launch. |
| **Solana (phase 2)** | — | Holder token perks + SOL/USDC rail atop Founder tier. Not required to play. |

**Original premium Steam model (superseded for v1):** $24.99 EA / $29.99 1.0, no MTX. Retained for long-term positioning reference.

### LLM: Backend API (Web)

Browser players cannot run local Qwen. v1 uses a **backend LLM proxy** with strict quotas and **template fallback always available**:

- Free: 10 LLM events/day (Herald headlines, mayor negotiation, council snippets)
- Founder Pass: unlimited + priority queue
- LLM never drives sim authority (unchanged from §7)
- Recurring API cost funded by Founder Pass + cosmetics (~$200–800/mo at 1K DAU target)

### Distribution & Demo Strategy (Web)

- **No Steam gate for v1** — playable in browser; frictionless entry drives virality
- Free core *is* the demo; Founder Pass converts depth fans who would have paid $25 on Steam
- Dev logs, streamer-friendly LLM newspaper, city-builder communities (Reddit, YouTube)
- 3D cosmetic skins are a natural MTX surface (facade swaps on instanced meshes)

### Financial outlook (web hybrid)

Revenue mixes Founder Pass one-time sales, cosmetic shop, and optional phase-2 crypto perks. Steam-style unit projections below are **historical**; web F2P conversion targets TBD post-launch.

| Scenario | EA Sales (Y1) | Revenue | Post-EA Sales | Total Revenue |
|----------|--------------|---------|---------------|---------------|
| Pessimistic | 5,000 | $87K | 10,000 | $260K |
| Moderate | 20,000 | $350K | 50,000 | $1.5M |
| Optimistic | 50,000 | $875K | 200,000 | $5.2M |

*Table assumes original Steam premium model for comparison only.*

---

## 13. Competitive Positioning

### The Market Gap

The city builder genre has a gaping hole. On one side, you have accessible but shallow games (Cities: Skylines 2, SimCity). On the other, you have deep but niche games (Workers & Resources, Victoria 3). No game bridges the gap. CityMajor sits squarely in the middle — and it is the only game that lets the player choose where on the depth spectrum they want to play.

### Head-to-Head Comparison

#### Cities: Skylines 2 (Colossal Order / Paradox, 2023)

**Their strengths**: Massive scale, road building tools, visual spectacle, strong modding community, brand recognition.

**Their weaknesses**: Performance disaster at launch (still struggling). Shallow simulation — economy is essentially decorative, traffic is the only real challenge. No meaningful political system. No historical progression. No production chains. DLC-heavy monetization that splits the community.

**CityMajor's advantage**: Actual economic simulation with real consequences. Political depth. Historical eras. LLM narrative. No DLC treadmill. CityMajor will never match CS2's visual fidelity, but pixel art sidesteps that competition entirely — it is a different aesthetic, not a lesser one.

**Target player**: CS2 players who are frustrated by the lack of depth and the performance issues. "I want my city to feel alive, not just look alive."

#### SimCity (2013, Maxis / EA)

**Their strengths**: The name. The legacy. GlassBox simulation was ambitious.

**Their weaknesses**: Online-only DRM disaster at launch. Tiny city sizes. Shallow late-game. Abandoned by EA. The franchise is effectively dead.

**CityMajor's advantage**: SimCity's spiritual successor for players who wanted more. Larger maps, deeper simulation, offline-first, no EA corporate baggage.

**Target player**: SimCity veterans who have been waiting for a worthy successor since 2013.

#### Anno 1800 (Ubisoft Blue Byte, 2019)

**Their strengths**: Beautiful production chain management. Gorgeous art direction. Satisfying logistics puzzle. Strong era theme (Industrial Revolution).

**Their weaknesses**: Not really a city builder — it is a logistics/RTS hybrid. No political simulation. Fixed historical period (no progression). Expensive DLC model ($120+ for all content). No modding. Always-online Ubisoft requirements.

**CityMajor's advantage**: Anno's production chain depth PLUS SimCity's city building. Multiple eras instead of one. Political and cultural simulation. Modding support. Fair pricing.

**Target player**: Anno players who wish the game cared about their citizens as much as their supply chains.

#### Victoria 3 (Paradox, 2022)

**Their strengths**: Deep economic simulation (Leontief-inspired). Political system with interest groups and laws. Historical grand strategy spanning 100 years.

**Their weaknesses**: Not a city builder — it is a map-painting grand strategy game. Abstraction is extreme: you never see a building, a citizen, or a street. Steep learning curve. Polarizing reception among Paradox fans (too simplified for Vic2 veterans, too complex for newcomers). War system widely criticized.

**CityMajor's advantage**: Victoria 3's economic depth rendered as a visible, tangible city instead of pie charts on a political map. You do not read that steel production increased 15% — you see the new furnace being built, the workers commuting in, the trucks hauling product. The abstraction becomes concrete. CityMajor borrows Vic3's economic model (Leontief I-O, market zones, production chains) but makes it visually intuitive.

**Target player**: Victoria 3 players who want to zoom in. Paradox fans who watch Vic3 streams and wish they could walk the streets of their industrial empire.

#### Tropico 6 (Limbic Entertainment, 2019)

**Their strengths**: Personality and humor. Political system with factions. Multi-era gameplay. Production chains.

**Their weaknesses**: Small scale. Cartoonish tone limits emotional investment. Shallow simulation beneath the charm. Linear era progression without meaningful choice.

**CityMajor's advantage**: Tropico's political flavor with ten times the simulation depth. Larger scale. Serious tone (with emergent humor from the LLM, not scripted jokes). Economic spectrum goes far beyond Tropico's dictator fantasy.

**Target player**: Tropico players who want real consequences for their political choices.

#### Workers & Resources: Soviet Republic (3Division, 2019 EA)

**Their strengths**: Unmatched production chain depth. Soviet aesthetic is unique. Dedicated community. Supply chain management is genuinely satisfying.

**Their weaknesses**: Brutally steep learning curve. No casual mode — it is always a logistics spreadsheet. Visually dated. Small development team, slow updates. Niche audience.

**CityMajor's advantage**: Workers & Resources' depth is accessible through the Economic Control Spectrum. At 100%, CityMajor approaches W&R's level of production chain management. But the casual player never has to see it. W&R forces everyone through the deep end; CityMajor has a shallow end.

**Target player**: Players who were intrigued by W&R but bounced off the learning curve. W&R veterans looking for something with broader appeal that they can play with friends who are not spreadsheet enthusiasts.

#### Frostpunk / Frostpunk 2 (11 bit studios, 2018/2024)

**Their strengths**: Extraordinary atmosphere and narrative tension. Meaningful moral choices. Tight, focused design. Strong visual identity.

**Their weaknesses**: Small scale, linear scenarios. Not a sandbox city builder — it is a survival narrative game in city builder clothing. Limited replayability after completing scenarios.

**CityMajor's advantage**: Frostpunk's moral weight in a sandbox context. CityMajor's LLM-generated stories create narrative tension organically, without scripted scenarios. The political and economic choices have Frostpunk-level consequences but emerge from simulation, not authored events.

**Target player**: Frostpunk fans who want the moral complexity in an open-ended sandbox.

### The Positioning Statement

CityMajor is the city builder for players who want their choices to matter. Not just where to put the road, but what kind of society to build. Not just how big the city grows, but who benefits and who suffers. Not just a beautiful skyline, but the stories of the people living under it.

No other game offers this combination: the accessibility of SimCity, the economic depth of Victoria 3, the production chains of Anno 1800, the political flavor of Tropico, the supply chain management of Workers & Resources, the moral weight of Frostpunk, and an AI narrator that makes every city unique. All in one game. All on a slider.

---

## 14. The Economic Control Spectrum

### Overview

The Economic Control Spectrum is CityMajor's defining feature — the single mechanic that differentiates it from every other city builder on the market. It is a continuous slider in the city settings that determines how much of the economy the player directly controls.

```
SLIDER: 0% <----------------------------------------> 100%
        Free Market                         Planned Economy

  0-20%        20-40%        40-60%        60-80%        80-100%
  LAISSEZ-    REGULATED     MIXED         STATE         PLANNED
  FAIRE       MARKET        ECONOMY       CAPITALISM    ECONOMY
```

The slider is not a binary mode switch. It is a continuous value that progressively unlocks and hides UI elements, control panels, policy options, and simulation parameters. Moving the slider 5% in either direction does not cause a jarring transition — it gently reveals or conceals one or two additional controls. The player can fine-tune their level of involvement at any time, and the economy smoothly adapts.

Critically, the slider can be moved during gameplay. A player who starts as a free-market mayor can gradually nationalize industries as their city grows. A planned economy can liberalize sector by sector. The transition creates gameplay: privatizing a state-owned steel mill means finding a buyer, negotiating terms, managing the workforce transition, and handling the political fallout from factions that benefited from state control.

---

### 0-20%: LAISSEZ-FAIRE

*"The business of the city is business."*

#### Philosophy

The government exists to build roads, run police and fire departments, and stay out of the way. Businesses open and close based on market forces. Prices are set by supply and demand. The invisible hand allocates resources. The player is a classic SimCity mayor — infrastructure provider, not economic manager.

#### What the Player Controls

- **Zoning**: Designate areas as Residential, Commercial, Industrial, Office, Mixed-Use
- **Infrastructure**: Roads, power lines, water pipes, sewage, transit
- **Services**: Police, fire, hospitals, schools, parks, garbage collection
- **Tax rates**: Income tax, property tax, sales tax (simple sliders, 0-25% range)
- **Basic ordinances**: Speed limits, noise curfews, building height limits

#### What Is Automated

- All production decisions (what factories make, how much, at what price)
- All hiring/firing decisions
- All trade (imports/exports happen automatically based on price signals)
- Wage levels (set by market supply/demand for labor)
- Business creation and closure (driven by profitability)
- Land development within zones (buildings grow organically)

#### UI at This Level

The economy panel shows aggregate indicators only:
- GDP and GDP growth rate
- Unemployment rate
- Inflation rate
- Trade balance (import vs. export totals)
- Average income by district
- RCI demand bars (the classic city builder readout)

No production chain viewer. No individual factory controls. No trade negotiation screen. No price-setting panels. The economic dashboard is a single page with 8-10 numbers and a few trend charts. Clean, simple, familiar to any SimCity player.

#### Laws and Policies Available

- Basic tax rate adjustment (income, property, sales)
- Building height restrictions
- Noise ordinances
- Speed limits
- Park requirements for new developments
- Basic fire and building codes

Laws that imply government economic intervention (minimum wage, price controls, subsidies, tariffs, nationalization) are **not visible** at this level. They do not appear in the law menu at all — they unlock progressively as the slider moves right.

#### Effects on the City

**Happiness**: High for wealthy citizens (low taxes, business opportunity, property value growth). Lower for poor citizens (no safety net, market-rate housing is expensive, no public transit subsidies). Income inequality grows over time.

**Growth**: Fast in booms, painful in busts. The economy is volatile — boom-bust cycles are pronounced. New businesses spring up quickly when demand exists, but they also fail quickly when demand drops.

**Corruption**: Low (minimal government involvement means minimal opportunity for corruption).

**Innovation**: High. Private competition drives technological adoption. Research rate gets a bonus from private-sector R&D spending.

**Risks**: Monopolies can form in key industries. Housing prices may become unaffordable. Environmental degradation is unchecked. Economic shocks hit hard with no safety net.

#### Historical Examples and Their Outcomes

- **Hong Kong (1960s-1990s)**: One of the freest economies in history. Extraordinary growth, world-class infrastructure, but extreme housing costs and income inequality. The government built public housing for 45% of the population as a pressure release valve — even laissez-faire has limits.
- **United States (Gilded Age, 1870-1900)**: Rapid industrialization, enormous wealth creation, but also robber barons, child labor, dangerous working conditions, and environmental destruction. Eventually triggered the Progressive Era reforms.
- **Chile (1975-1990, Chicago Boys)**: Radical free-market experiment under Pinochet. GDP growth was strong but inequality was extreme. When the authoritarian enforcer was removed, the population voted for a mixed economy.

**The lesson for the player**: Laissez-faire generates rapid growth but accumulates social debt. Inequality, environmental damage, and boom-bust volatility eventually create political pressure (protests, faction unrest, election risk) that forces intervention — or acceptance of the consequences.

---

### 20-40%: REGULATED MARKET

*"The market works, but it needs guardrails."*

#### Philosophy

The free market is the primary engine of growth, but the government sets rules to prevent its worst excesses. Minimum wage protects workers. Environmental regulations limit pollution. Anti-monopoly laws promote competition. Public utilities ensure universal access to power and water. The private sector handles production and most services; the government referees.

#### What the Player Controls (New at This Level)

Everything from 0-20%, plus:
- **Minimum wage** (slider: $0-$25/hr equivalent, adjusted for era)
- **Environmental regulations** (emission caps, green building requirements, recycling mandates)
- **Anti-monopoly enforcement** (break up companies that control >X% of a market)
- **Public utility management**: Player directly controls the power and water utilities (build plants, set capacity, manage grid). Power and water are now state-run services, not private businesses.
- **Basic labor laws** (maximum work hours, workplace safety standards, union rights)
- **Zoning regulations** (density limits, setback requirements, historic preservation)

#### What Is Automated

- Production decisions for all non-utility industries
- Pricing for all non-utility goods
- Trade (still automatic, but tariffs are now available as a law)
- Hiring/firing (subject to labor laws)
- Private-sector wage levels above minimum (market-determined)
- Business creation/closure (now subject to permitting requirements)

#### UI at This Level

The economy panel adds:
- **Regulatory compliance dashboard**: How many businesses comply with each regulation. Non-compliance rates by district.
- **Utility management panel**: Power plant capacity, water treatment capacity, grid load, maintenance schedules. This is the player's first taste of direct production management.
- **Labor statistics**: Minimum wage impact analysis, workplace injury rates, union membership.
- **Environmental metrics**: Air quality index, water pollution levels, emissions by industry.

The production chain viewer is still hidden. Individual factory controls are still hidden. But the player now sees more of the economic machinery — they can trace the impact of their regulations on business profitability and employment.

#### Laws and Policies Available (New)

- Minimum wage law (slider)
- Maximum work hours (slider)
- Workplace safety standards (toggle + enforcement level)
- Emission caps by industry type (slider)
- Green building requirement for new construction (toggle)
- Recycling mandate (toggle)
- Anti-monopoly threshold (slider: market share %)
- Historic preservation zones (zoning tool)
- Basic tariffs on imports (slider: 0-15%)
- Public transit subsidies (slider: 0-50% of operating cost)

#### Effects on the City

**Happiness**: More balanced than laissez-faire. Workers benefit from minimum wage and safety regulations. Wealthy citizens pay slightly more in compliance costs but benefit from social stability. Overall satisfaction is higher for the median citizen.

**Growth**: Slightly slower than laissez-faire but more stable. Regulations add friction to business creation but prevent the worst bust cycles. Fewer businesses fail catastrophically because they are forced to maintain standards.

**Corruption**: Low to moderate. Regulatory enforcement creates opportunities for bribery (businesses paying inspectors to overlook violations). A "corruption" overlay starts to become relevant.

**Innovation**: Moderate. Environmental regulations can drive green innovation (cleaner factories to comply with emission caps). But excessive regulation can stifle new businesses (high compliance costs for startups).

**Risks**: Over-regulation stifles growth ("regulatory capture" where incumbent businesses lobby for rules that block new competitors). Under-regulation fails to prevent the problems it was designed to address. Finding the right balance is the gameplay.

#### Historical Examples and Their Outcomes

- **Germany (Social Market Economy, post-1949)**: Strong regulations, universal healthcare, worker co-determination on corporate boards. Result: stable growth, low inequality by developed-world standards, world-class manufacturing. But also slower startup creation and higher costs.
- **United States (New Deal era, 1933-1970s)**: Minimum wage, Social Security, labor rights, environmental regulation. Created the middle class but eventually faced stagflation when regulation could not adapt to oil shocks.
- **Japan (1950s-1990s)**: Heavy regulation of banking and industry with government "guidance." Produced the economic miracle but also rigidity that contributed to the Lost Decades.

**The lesson for the player**: Regulation is a tuning problem, not a binary switch. Too little and the market's externalities accumulate. Too much and the economy calcifies. The optimal point shifts as the city grows and the economy evolves.

---

### 40-60%: MIXED ECONOMY

*"Some things are too important to leave to the market."*

#### Philosophy

The government owns and operates key industries — energy, transport, healthcare, heavy industry — while the private sector handles consumer goods, retail, and services. This is not ideological; it is pragmatic. The government invests in sectors where market failures are severe (natural monopolies, public goods, strategic industries) and lets the market handle sectors where competition works well.

#### What the Player Controls (New at This Level)

Everything from 20-40%, plus:
- **State-owned enterprises (SOEs)**: Player can nationalize existing industries or build new state-owned factories. SOEs have dedicated management panels: set production targets, investment budgets, hiring policies, and pricing.
- **Industrial policy**: Subsidies for target industries. Tax breaks for sectors you want to grow. Tariffs to protect domestic producers from cheap imports.
- **Price controls on essentials**: Cap prices on food, housing, medicine, and basic clothing. The simulation enforces the cap — shortages develop if the cap is below production cost.
- **State investment bank**: A dedicated pool of capital that the player allocates to strategic investments. Build a steel mill because the city needs it, not because it is profitable.
- **Import/export quotas**: Limit how much of a good can be imported (to protect domestic industry) or exported (to ensure domestic supply).
- **Wage negotiation**: Set wages for SOE workers directly. Private-sector wages still follow the market (above minimum wage).

#### What Is Automated

- Private-sector production decisions (for non-nationalized industries)
- Private-sector pricing (for non-controlled goods)
- Private-sector hiring (subject to labor laws)
- Consumer choice (households buy from cheapest available source, state or private)
- Small business creation/closure

#### UI at This Level

Major UI additions:
- **Production chain viewer**: The full 20-chain, 45-good production network becomes visible. Player can trace goods from raw material to consumer. Bottlenecks are highlighted in red.
- **SOE management panels**: For each state-owned enterprise, a detailed panel showing: production rate, efficiency, workforce, input consumption, output, revenue/cost, investment needs.
- **State investment bank dashboard**: Capital pool, investment portfolio, ROI tracking, capital allocation interface.
- **Price control panel**: List of controlled goods with current ceiling/floor prices, market equilibrium prices, and shortage/surplus indicators.
- **5-year plan overview** (simplified): Set high-level production targets for state-owned sectors. The system tracks progress toward targets.

#### Laws and Policies Available (New)

- Nationalization authority (per industry — player can nationalize any production chain)
- Industrial subsidies (per sector, slider: 0-50% of operating costs)
- Export quotas (per good, units/month cap)
- Import quotas (per good, units/month cap)
- Price ceilings on essential goods (per good, price slider)
- Price floors on domestic production (per good, price slider)
- State investment bank charter (toggle — creates the bank with initial capital)
- Public housing mandate (% of new residential that must be public/affordable)
- Free healthcare toggle (state covers all medical costs)
- Free education toggle (state covers all education costs)

#### Effects on the City

**Happiness**: High for median citizens. Essential goods are affordable (price controls), healthcare and education may be free, public housing is available. Wealthy citizens are less happy (higher taxes, limited profit opportunities in nationalized sectors). Workers in SOEs have stable employment and decent wages.

**Growth**: Moderate. State investment can drive growth in strategic sectors, but SOEs are typically less efficient than private companies (the simulation models this: SOE efficiency is 70-90% of private equivalent unless the player actively manages them). Private sector grows in non-controlled areas.

**Corruption**: Moderate to high. SOEs create bureaucratic positions that attract rent-seekers. The more the state controls, the more opportunities for corruption. A corruption system tracks embezzlement, bribery, and patronage. Corrupt officials reduce SOE efficiency and steal from the state budget.

**Innovation**: Moderate. State investment can fund basic research (universities, labs), but SOEs innovate slower than competitive private firms. The player must balance directed investment (building what the city needs) against organic innovation (letting the market discover what works).

**Risks**: SOE inefficiency drains the budget. Price controls cause shortages if set below equilibrium (a fixed bread price below production cost means bakers produce less or go bankrupt). Corruption compounds over time. The player must actively manage SOEs to prevent them from becoming sinkholes.

#### Historical Examples and Their Outcomes

- **France (Dirigisme, 1945-1980s)**: State-owned railways, energy, telecom, banking. Produced rapid postwar reconstruction, TGV, nuclear power program, and Airbus. But also rigid labor markets and chronic fiscal pressure from underperforming SOEs.
- **South Korea (1960s-1990s)**: Government directed capital to chaebols (Samsung, Hyundai, LG), picked winners, protected domestic industry with tariffs. Produced an economic miracle — but also corruption, cronyism, and the 1997 Asian financial crisis when the model's weaknesses were exposed.
- **India (License Raj, 1947-1991)**: Mixed economy with heavy state control of industry, import substitution, price controls. Produced stable but slow "Hindu rate of growth" (~3.5%/year). Liberalization in 1991 unleashed faster growth but also inequality.

**The lesson for the player**: Mixed economies can achieve remarkable things (France's nuclear program, Korea's industrial miracle) but require constant active management. SOEs that are ignored become liabilities. The player must be a hands-on manager, not a set-and-forget regulator.

---

### 60-80%: STATE CAPITALISM

*"The government does not replace the market. It becomes the market's biggest player."*

#### Philosophy

The government owns the commanding heights of the economy — energy, heavy industry, banking, transport, telecommunications, defense. These companies operate as profit-seeking enterprises but follow state direction on strategic matters. Private small businesses are allowed and even encouraged (restaurants, shops, services), but large-scale capital investment is directed by the state. The government is simultaneously regulator, investor, and competitor.

#### What the Player Controls (New at This Level)

Everything from 40-60%, plus:
- **State holding company**: All SOEs are consolidated under a state holding company with a unified dashboard. The player is effectively the CEO of the city's largest conglomerate.
- **Capital allocation board**: Direct control over where investment goes. Percentage of GDP directed to each sector. The board shows projected returns, employment impact, and strategic value.
- **Wage-setting authority**: Direct wage control for ALL sectors, not just SOEs. Private businesses must pay state-mandated wages by sector and skill level.
- **Land-use direction**: Beyond zoning, the player can designate specific plots for specific purposes. "This block will be a semiconductor factory." Private developers must comply or sell.
- **State banking**: The state bank sets interest rates, controls credit allocation, and decides which businesses get loans. A private business cannot expand without state bank approval.
- **Five-year plans** (full): Comprehensive production targets across all sectors. Quarterly progress reviews. Adjustment mechanisms.

#### What Is Automated

- Day-to-day operations of SOEs (the player sets strategy; AI managers handle execution)
- Small private business operations (restaurants, shops, personal services)
- Consumer purchasing decisions
- Household-level economic choices (within state-set constraints)

#### UI at This Level

The UI becomes significantly more complex:
- **State holding company dashboard**: Revenue, costs, headcount, efficiency, and investment for each SOE on one screen. Drill-down to individual enterprise management.
- **Capital allocation board**: Drag-and-drop interface for allocating annual investment budget across sectors. Each allocation shows projected impact on production, employment, and growth.
- **Five-year plan panel**: Full planning interface with sector-by-sector targets, progress tracking, bottleneck alerts, and adjustment recommendations.
- **State bank panel**: Interest rate control, credit allocation by sector, loan portfolio, non-performing loan tracking.
- **Corruption tracker**: Becomes prominent. Shows corruption levels by institution, suspected individuals, enforcement actions.
- **Trade panel**: Bilateral trade agreements with AI cities. Import/export volumes, prices, and strategic stockpiles.

#### Laws and Policies Available (New)

- State holding company charter
- Sector-wide wage mandates (per skill level, per industry)
- Directed land-use authority (override private property for state purposes)
- State bank charter (monopoly or dominant-position banking)
- Foreign investment restrictions (limit private foreign ownership)
- State media authority (influence public opinion, counteract opposition)
- Compulsory purchase powers (eminent domain for strategic projects)
- Exit visa requirements (prevent brain drain — citizens need approval to emigrate)

#### Effects on the City

**Happiness**: Dependent on execution. Well-managed state capitalism produces high employment, good wages, impressive infrastructure, and national pride (citizens are proud of the gleaming state-built subway system). Poorly managed state capitalism produces corruption, inefficiency, shortages of consumer goods, and resentment.

**Growth**: Can be very high in the short-to-medium term. State-directed investment overcomes market failures and coordination problems. The state can build a semiconductor industry from scratch in 10 years (something no free market would do for a small city). But long-term growth depends on avoiding the trap of diminishing returns from state investment.

**Corruption**: High. The concentration of economic power in state institutions creates enormous corruption pressure. The corruption system becomes a primary gameplay challenge: appoint honest managers, fund anti-corruption agencies, or accept that 10-20% of the state budget disappears into private pockets.

**Innovation**: Mixed. State-directed R&D can produce breakthroughs in targeted areas (infrastructure, energy, heavy industry). But consumer innovation suffers — the state is bad at predicting what people want to buy. If the player does not leave space for private entrepreneurship in consumer sectors, citizens complain about monotonous products.

**Risks**: The "middle-income trap" — state capitalism excels at catching up to developed economies but struggles to innovate at the frontier. Corruption can become entrenched and self-reinforcing. Political factions that benefit from state control resist liberalization, even when it is economically necessary. Brain drain if talented citizens emigrate to freer cities.

#### Historical Examples and Their Outcomes

- **Singapore (1965-present)**: Textbook state capitalism. Government owns major companies (Temasek Holdings), directs investment, controls land use, and runs world-class services. Result: one of the wealthiest nations per capita, excellent infrastructure, low corruption (through aggressive enforcement), but limited political freedom and high cost of living.
- **China (1978-present)**: State-owned enterprises dominate banking, energy, telecom, and heavy industry. Private sector thrives in consumer goods and tech. Result: the greatest economic expansion in human history, lifting 800 million out of poverty. But also inequality, environmental devastation, property bubbles, and an uncertain transition as the easy growth ends.
- **UAE/Dubai (1970s-present)**: Oil wealth directed by royal families into infrastructure, real estate, tourism, and financial services. Result: gleaming modern cities in the desert, economic diversification away from oil. But also labor exploitation, extreme inequality between citizens and migrants, and dependence on continued state investment.

**The lesson for the player**: State capitalism is the highest-performance economic system in CityMajor — if managed well. It produces the most impressive megaprojects, the fastest catch-up growth, and the most dramatic city transformations. But it demands constant attention. Corruption is the existential threat. And the transition to the next stage (either more freedom or more control) is politically painful.

---

### 80-100%: PLANNED ECONOMY

*"The plan knows. The plan provides. The plan is everything."*

#### Philosophy

The central planning board sets ALL production targets for ALL goods. Prices are fixed by the state. Housing is state-allocated. Employment is guaranteed — and compulsory. There is no private business ownership. The state controls imports and exports. The player is not a mayor — they are the General Secretary, the Chairman, the Supreme Economic Council. Every ton of steel, every loaf of bread, every apartment assignment passes through the plan.

#### What the Player Controls (New at This Level)

Everything from 60-80%, plus:
- **Central planning board**: The complete Leontief I-O model is exposed to the player. Set production targets for all 45 goods. The system calculates whether the math works (required inputs, labor, capacity). Imbalances are flagged. The player must resolve every gap.
- **Fixed pricing for all goods**: No market prices. The player sets the price of bread, steel, housing, medicine — everything. Prices are political tools (cheap bread keeps workers happy; cheap steel subsidizes construction).
- **State-allocated housing**: No private housing market. Citizens are assigned housing based on family size, employment, and seniority. The player controls allocation priorities.
- **Compulsory employment**: Every able-bodied adult is assigned a job. The player sets labor allocation quotas by sector. "We need 5,000 more workers in mining; transfer them from textile."
- **Complete trade control**: All imports and exports go through the state trading company. No private trade. The player negotiates every deal with AI mayors directly.
- **State media**: Full control of the news narrative. The LLM-generated newspaper becomes state propaganda (but citizens' private satisfaction still reflects reality). Suppress negative stories or allow a free press — each has consequences.

#### What Is Automated

- Nothing of economic significance. The player has full control. Only household-level consumption (citizens buy what is available at state prices) and demographic processes (births, deaths, aging) are outside player control.
- Day-to-day factory operations follow the plan automatically — but the plan itself is the player's responsibility.

#### UI at This Level

The UI is maximally complex:
- **Central Planning Board**: Full-screen interface showing all 45 goods, their production targets, actual output, input requirements, labor requirements, and surplus/deficit. A massive spreadsheet — but with good UX: color-coded cells, bottleneck highlighting, drill-down to individual factories, suggested adjustments.
- **Labor allocation panel**: Workforce by sector, transfer requests, training pipeline, productivity metrics.
- **Housing allocation panel**: Housing stock by district, occupancy rates, waiting lists, allocation queue, priority settings.
- **State pricing board**: All 45 goods with fixed prices, production cost comparison, subsidy calculations, consumer satisfaction impact.
- **Trade ministry**: Bilateral agreements, import/export volumes, strategic reserves, trade partner relationships.
- **State media control**: Newspaper story selection (approve/reject/modify), propaganda campaigns, public opinion tracking (what citizens believe vs. what is true).
- **Secret police / internal affairs** (if laws permit): Monitor dissident activity, prevent brain drain, enforce compliance. This is a gameplay system, not an endorsement — the game shows the human cost through citizen stories and satisfaction scores.

#### Laws and Policies Available (New)

- Central planning board charter (enables full planning mode)
- Abolition of private property (toggle — removes all private businesses)
- State housing allocation mandate
- Compulsory employment law
- State trading monopoly
- State media control
- Internal security service (secret police — suppresses dissent but has moral and satisfaction costs)
- Exit restrictions (prevent emigration — harshly unpopular, triggers underground resistance)
- Collectivization of agriculture (converts private farms to state farms — historically disastrous)
- Rationing system (distribute goods by ration cards instead of money)

#### Effects on the City

**Happiness**: Depends entirely on player competence. A well-run planned economy provides guaranteed employment, affordable housing, free healthcare and education, and a sense of collective purpose. Citizens' basic needs are met. But consumer choice is limited (the state decides what is available), personal freedom is restricted, and the absence of market signals means the economy produces what the plan says, not what people want. Satisfaction follows a U-curve: high at first (everyone has a job and a home), declining over time (monotonous goods, limited freedom, creeping inefficiency).

**Growth**: Can be spectacular in the early/mid game when the economy is developing. State-directed investment in heavy industry, infrastructure, and education produces rapid industrialization. The Soviet Union went from agrarian to space-faring in 40 years. But long-term growth stagnates as the planning system cannot adapt to increasing economic complexity. The more goods and chains the economy has, the harder it is to plan them all correctly.

**Corruption**: Very high. In a planned economy, EVERYTHING is allocated by officials, which means EVERYTHING can be diverted, skimmed, or traded on the black market. A shadow economy emerges: citizens trade scarce goods informally. Officials trade favors. The corruption system is the primary antagonist in planned economy gameplay.

**Innovation**: Very low for consumer goods and services (no competition, no profit motive, no consumer feedback). Moderate for state-directed research (the state can throw resources at priority areas). The player can maintain innovation by funding research institutions and creating "innovation zones" with relaxed planning constraints — but this is fighting the system's natural tendency.

**Risks**: The information problem — the player simply cannot process all the information needed to plan a complex economy optimally. Shortages and surpluses are inevitable. Shortages cause rationing, black markets, and citizen anger. Surpluses waste resources. The economy becomes brittle — a single planning error cascades through interconnected production chains. And the political system becomes authoritarian by necessity: citizens who disagree with the plan are dissidents; workers who underperform are saboteurs; the planning board's failures are blamed on wreckers.

#### Historical Examples and Their Outcomes

- **Soviet Union (1928-1991)**: The original planned economy. Rapid industrialization in the 1930s-1950s, space program, military superpower. But also famines (collectivization of agriculture killed millions), chronic consumer goods shortages, environmental catastrophe, and eventual economic stagnation that contributed to collapse. Citizens waited in line for bread while rockets went to space.
- **Cuba (1959-present)**: Universal healthcare and education, low inequality, but chronic shortages of consumer goods, housing decay, limited personal freedom, and economic dependence on external patrons (USSR, then Venezuela). Citizens are educated and healthy but cannot buy the things they want.
- **North Korea (1948-present)**: The extreme case. Total state control, military-first economy, catastrophic famines, complete isolation. A warning of what happens when planning failure is compounded by authoritarian refusal to adapt.
- **Soviet Union under NEP (1921-1928)**: Lenin's strategic retreat — state control of heavy industry but private agriculture and small business. Produced rapid recovery from civil war. Demonstrates that partial planning can work better than total planning.

**The lesson for the player**: The planned economy is CityMajor's hardest difficulty mode. It rewards deep engagement with the economic simulation (you must truly understand the Leontief matrix to avoid cascading shortages). It produces dramatic, cinematic gameplay — heroic five-year plans, desperate crisis management, moral dilemmas about freedom vs. equality. But it is a system at war with itself. The player fights corruption, fights information overload, fights citizen frustration, and fights the temptation to use authoritarian tools to paper over economic failures. Victory in a planned economy — building a prosperous, stable city where citizens are genuinely happy — is CityMajor's ultimate challenge.

---

### Spectrum Transitions: Moving the Slider

The most interesting gameplay often happens when the player moves the slider — transitioning from one economic system to another.

**Nationalization** (sliding right): The player selects a private industry to nationalize. The simulation calculates fair market value. The government pays (or doesn't — seizing assets is cheaper but tanks Business faction approval and foreign investment). Existing workers stay; management is replaced with state appointees. Production may dip during transition (institutional knowledge loss). The private sector reacts: other businesses worry they are next, reducing investment. The political system reacts: Populists cheer, Industrialists rage, Progressives worry about precedent.

**Privatization** (sliding left): The player selects a state-owned enterprise to privatize. The simulation generates potential buyers with different offers (price, employment guarantees, investment commitments). The player chooses. Workers may be laid off during restructuring. The new private owner may be more efficient but may also gut the enterprise for short-term profit. The political system reacts: Industrialists cheer, Populists rage, Greens worry about deregulation.

**Price liberalization** (removing price controls): When the player removes a price ceiling on a controlled good, the price jumps to equilibrium. If equilibrium is far above the controlled price, this causes a consumer shock — citizens suddenly cannot afford bread / medicine / housing. The LLM generates stories about the impact. Political factions react. The player must manage the transition: gradual price increases over months, or shock therapy all at once.

**Market creation** (introducing competition in a state sector): The player allows private companies to compete in a sector previously monopolized by the state. The SOE, accustomed to captive customers, loses market share. Workers may be displaced. But prices drop and quality improves as competition takes effect.

Each of these transitions is a multi-month gameplay event with political, economic, and social consequences. They are not instant toggles — they are stories.

---

### Summary: The Spectrum as Gameplay

The Economic Control Spectrum is not a settings menu. It is the core of CityMajor's identity. It is the answer to the question "What kind of society do you want to build?" — asked not abstractly, but through concrete mechanics with visible consequences.

A player who slides from 20% to 60% over 50 in-game years has a story: they started as a free-market mayor, watched inequality grow, saw protests in the streets, lost an election, won it back on a platform of industrial policy, nationalized the steel mills to build a bridge, discovered they enjoyed the control, expanded the state sector, fought corruption, and eventually built something that works — imperfectly, compromisingly, humanly.

A player who starts at 80% and slides down to 40% has a different story: they began with grand plans, achieved rapid industrialization, then hit the wall of complexity and corruption. Consumer goods were scarce. Citizens were restless. They allowed private bakeries, then private shops, then private factories. Each step required political courage. The Old Guard faction resisted. The liberalization produced inequality they had tried to prevent. They landed on a mixed economy — not their original vision, but the one their city could sustain.

These are not scripted narratives. They emerge from the interaction between the player's choices and the simulation's consequences. The LLM gives them a voice. The pixel art gives them a face. The music gives them a mood.

This is what city builders should be.

---

## Appendix A: Quick Reference Comparison

| Feature | SimCity | Cities:Skylines 2 | Anno 1800 | Victoria 3 | Workers & Resources | **CityMajor** |
|---------|---------|-------------------|-----------|------------|--------------------|----|
| Economic depth | Shallow | Shallow | Moderate | Deep | Very Deep | **Deep (adjustable)** |
| Political system | None | None | None | Deep | None | **Deep** |
| Production chains | None | None | Yes (fixed) | Yes | Yes (manual) | **Yes (adjustable)** |
| Historical eras | No | No | 1 era | 100 years | 1 era | **5 eras, 200 years** |
| LLM narrative | No | No | No | No | No | **Yes** |
| Play style options | Casual only | Casual only | Deep only | Deep only | Deep only | **Casual to Deep (slider)** |
| Pixel art | No | No | No | No | No | **Yes** |
| Multiplayer | No | No | Yes | Yes | No | **Yes (co-op)** |
| Modding | Limited | Yes | No | Yes | Limited | **Yes (3-tier)** |
| Price (base) | $20 | $50 | $60 | $50 | $25 | **$25-30** |

---

*CityMajor is not just another city builder. It is the city builder that the genre has been waiting for — one that trusts the player to choose their own depth, rewards both the architect and the economist, and tells the story of their city in a voice that has never existed before. Build the city. Choose the system. Live with the consequences.*
