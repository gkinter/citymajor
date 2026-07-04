# Game Identity: The Two Depth Axes

> This document is the definitive statement of what this game IS. Read this before anything else in the design docs. If any other document contradicts this one, this one wins.

---

## The Core Misconception We Must Kill

There is no casual mode. There is no "lite" experience. There is no difficulty setting that removes simulation depth. **Both play styles are deep.** The difference is *where* the depth lives, not *how much* depth there is.

A player running a market economy is not playing a simpler game than a player running a command economy. They are playing a game where the complexity manifests in *urban planning and indirect influence* rather than in *direct economic control*. The total cognitive load is comparable. The skills required are different.

This is the single most important design principle in the entire project.

---

## Axis 1: Urban Planning Depth (Always Active)

This axis is non-negotiable. Every player, regardless of economic mode, confronts the full weight of urban simulation. There are no training wheels. The city is alive, and it does not care about your intentions -- only your decisions.

### Socioeconomics

Wealth inequality drives everything. The Gini coefficient is not a hidden stat -- it is a visible, persistent metric on your dashboard, and it *will* haunt you.

Rich neighborhoods gentrify. Poor neighborhoods decline. Housing affordability crises emerge organically from land value dynamics, not from scripted events. When you fail -- when housing costs outpace wages, when shelters overflow, when the working poor can no longer afford rent -- homelessness appears *visually*. Tents on sidewalks. People sleeping in parks. Your citizens will see it. Your approval rating will reflect it.

Brain drain happens when educated citizens cannot find jobs that match their qualifications. They leave. They take their tax revenue with them. The university you built becomes a pipeline to other cities.

Social mobility depends on education quality, which depends on school funding, which depends on property taxes, which depends on land values, which depends on neighborhood desirability, which depends on school quality. This is not a metaphor. This is the actual simulation loop. It is a vicious cycle by design, and breaking it requires deliberate, sustained intervention across multiple systems simultaneously.

### Demographics

100,000 households with full lifecycle simulation. Citizens are born. They go to school. They find jobs (or they don't). They marry. They have children (or they don't -- fertility correlates with education, income, housing cost, cultural values). They age. They retire. They die.

Each generation is shaped by the city they grew up in. A child who attends a well-funded school in a safe neighborhood with clean air develops differently from a child in a neglected district with lead pipes and no parks. This is not flavor text. It produces measurably different adult citizens with different skill levels, health outcomes, political leanings, and economic productivity.

Baby booms are not events -- they are consequences. A period of prosperity and affordable housing produces more births. Five years later, your schools are overcrowded. Eighteen years later, the job market floods with new workers. If you built the right industries, they find work and the economy booms. If you didn't, unemployment spikes, crime rises, and an entire generation becomes disillusioned. Forty years after the baby boom, those citizens retire en masse. Healthcare demand surges. The tax base contracts. Pension obligations balloon.

Immigration changes cultural composition over decades. First-generation immigrants cluster in affordable districts (Schelling's segregation model). Second-generation immigrants integrate or don't, depending on education access, economic opportunity, and cultural policy. Third-generation immigrants may be indistinguishable from the native population -- or they may form a distinct, permanent community. Your decisions determine which.

### Cultural Dynamics

Every district tracks 8 cultural dimensions:

| Dimension | What It Means |
|-----------|--------------|
| **Work Ethic** | Productivity, overtime tolerance, hustle culture vs. work-life balance |
| **Collectivism** | Community orientation, mutual aid, willingness to fund public goods |
| **Risk Tolerance** | Entrepreneurship, startup culture, tolerance for economic disruption |
| **Environmental Values** | Support for green policy, willingness to pay for sustainability |
| **Social Trust** | Institutional confidence, corruption tolerance, civic participation |
| **Progressivism** | Openness to change, social liberalism, acceptance of diversity |
| **Hierarchy Acceptance** | Deference to authority, tolerance for inequality, meritocracy beliefs |
| **Cultural Pride** | Heritage preservation, resistance to gentrification, identity politics |

These are not static. They *drift* based on lived experience. Education raises progressivism. Prosperity raises risk tolerance. Corruption scandals crater social trust. Immigration increases cultural pride in established communities. Media outlets amplify and accelerate cultural shifts. Over decades, neighborhoods develop genuinely distinct identities -- not because of a "character" slider, but because the people who live there have been shaped by different experiences.

Gentrification is not an event that fires when property values cross a threshold. It is an emergent process: artists move to cheap neighborhoods, followed by cafes, followed by young professionals, followed by luxury development, followed by the displacement of everyone who made the neighborhood attractive in the first place. Cultural Pride fights this. Community organizations slow it. Rent control policy can freeze it -- but at the cost of housing supply.

Religious influence wanes with education (secularization curve). Cultural conflicts emerge between conservative and progressive districts -- not as scripted crises, but as persistent political friction that shapes elections, law proposals, and protest movements.

### Political Simulation

Six political factions whose power is not assigned -- it *emerges* from economic conditions, cultural drift, and demographic composition.

Elections occur every 4 years. They are not random. The outcome is a deterministic function of citizen satisfaction, factional allegiance, campaign spending, media influence, and voter turnout (which correlates with education and social trust).

The law system contains 70+ laws, each with real tradeoffs:

- **Minimum wage**: Helps workers, costs jobs. The elasticity depends on industry composition.
- **Emission standards**: Cleans the air, drives away heavy industry. The timeline matters -- strict standards attract green tech firms long-term but cause immediate factory closures.
- **Rent control**: Prevents displacement, reduces housing construction. The second-order effects take a decade to manifest.
- **School vouchers**: Increases choice, defunds public schools. Wealthy families benefit more. Inequality widens.

NIMBY protests block construction near existing residential areas. This is not a random event -- it is a predictable response from property owners who benefit from scarcity. The political faction that represents homeowners will *always* oppose density unless you build sufficient political capital to override them.

Corruption emerges from rational incentives, not random chance. When government contracts are large and oversight is weak, corruption appears. It is not a moral failing of simulated citizens -- it is a Nash equilibrium that your institutional design either prevents or enables.

### Infrastructure Physics

Infrastructure does not simply "work" or "not work." It degrades. Deferred maintenance compounds at 7% per year. A $100 million bridge that you neglect for 15 years now costs $280 million to bring back to standard. Or it collapses.

Specific systems:

- **Water**: Lead pipes poison residents (increased disease, reduced cognitive development in children). Water pressure drops with elevation -- hilltop neighborhoods need pumping stations. Combined sewers overflow during storms, dumping raw sewage into rivers.
- **Power**: Grids cascade-fail. One overloaded substation trips, shifting load to neighbors, which trip in turn. Brownouts in summer when AC demand peaks. Blackouts in winter when heating demand spikes.
- **Roads**: Deteriorate under heavy trucks following the fourth-power law (doubling axle weight causes 16x the damage). Potholes form. Maintenance costs scale non-linearly with neglect.
- **Seismic**: Earthquake damage depends on building material, construction era, and seismic code compliance. A magnitude 6.0 quake that destroys unreinforced masonry buildings leaves modern steel-frame structures intact. Building codes save lives -- but only if enforced, and only if applied retroactively (which is expensive and politically unpopular).

Induced demand fills new highways. This is not optional. Build a 6-lane expressway and it will be congested within 5 years. This is the fundamental tension of transport planning, and the simulation enforces it.

### Environmental Systems

Pollution disperses via Gaussian plume model based on wind direction and speed. A factory on the west side of the city poisons the east side when prevailing winds blow east. This is spatial. It appears on the map. Citizens in affected areas get sick, property values drop, political dissatisfaction rises.

Noise propagates and reflects off buildings. A highway creates a noise corridor. Sound walls help. Distance helps more. High-density residential next to a highway is a design error with measurable health consequences.

Soil contamination persists for *decades* after factories close. Brownfield remediation is expensive and slow. The land sits unusable, a scar on the city, while the cleanup costs drain the budget.

Climate change increases disaster frequency over the game's 200-year arc. The city you build in year 1 faces a different climate in year 100. Coastal areas flood more frequently. Heat waves intensify. Droughts stress water systems. This is not a difficulty modifier -- it is the baseline simulation.

Heat islands form in dense concrete areas with insufficient green space. Urban forestry mitigates heat (measurably: 2-4 degree reduction in tree-covered areas), improves air quality, increases property values, and improves mental health outcomes. Trees are infrastructure.

### Education as a Generational System

School quality today determines workforce quality in 15-20 years. This is the longest feedback loop in the game, and it is the most important.

Teacher shortage spirals are self-reinforcing: bad schools produce fewer graduates, fewer graduates become teachers, fewer teachers make schools worse. Breaking this cycle requires sustained investment over a full generation -- 20+ years of above-average education funding before the effects fully materialize.

Different school philosophies produce different citizen types:

| Philosophy | Produces | Tradeoff |
|-----------|----------|----------|
| **Traditional** | Disciplined, hierarchical workers | Lower creativity, lower entrepreneurship |
| **Montessori** | Creative, independent thinkers | Higher education cost, slower standardized results |
| **STEM-focused** | Technical workforce, researchers | Weaker social skills, cultural blind spots |
| **Vocational** | Skilled tradespeople, immediate employment | Lower social mobility ceiling |

The Finnish model (high teacher pay, low standardized testing, play-based learning) produces measurably different outcomes than the American model (low teacher pay, heavy testing, competitive admissions). Both are available. Both have 20-year feedback cycles. Choose wisely.

### Healthcare Cascading

Healthcare is not a service you provide -- it is a system that interacts with every other system:

Pollution causes respiratory disease. Respiratory disease fills hospitals. Full hospitals mean longer wait times. Longer wait times mean higher death rates. Higher death rates mean population decline. Population decline means a shrinking tax base. A shrinking tax base means budget cuts. Budget cuts mean less healthcare funding. Less healthcare funding means worse outcomes. The spiral continues.

Pandemic events test preparedness. Hospital capacity, research infrastructure, public trust in institutions, and political willingness to enact restrictions all determine outcomes. A city with high social trust and strong healthcare weathers a pandemic. A city with low trust and underfunded hospitals does not.

Mental health infrastructure prevents crime and addiction. This is a causal link in the simulation, not a flavor bonus. Underfunded mental health services produce measurably higher crime rates, substance abuse, homelessness, and economic losses.

### Transport as Urban Design

Transport is not a logistics problem. It is an urban design problem.

Induced demand means more roads equals more traffic. This is implemented via the BPR (Bureau of Public Roads) congestion formula. It is mathematically inevitable. The simulation does not cheat.

Braess's Paradox means that removing a road can *improve* traffic flow. This is counterintuitive and real. Players who understand network theory will outperform players who build highways.

Mode choice depends on travel time, cost, and comfort, calculated via multinomial logit model. Citizens do not take transit because you built it. They take transit because it is *faster, cheaper, or more convenient* than driving. If it isn't, they drive, no matter how many rail lines you build.

Transit-oriented development creates walkable neighborhoods where residents drive less, exercise more, shop locally, and form stronger community bonds. This is not a bonus -- it is an emergent property of land use and transport integration.

Highway construction *destroys* neighborhoods. It is historically accurate and mechanically enforced. Building an expressway through a residential district displaces residents, craters property values in adjacent blocks, creates noise and pollution corridors, and severs the urban fabric. The economic gains from faster freight movement may or may not offset the social damage. That calculation is yours to make.

---

## Axis 2: Economic Control Depth (Player's Choice)

This is where the spectrum lives. Not a difficulty setting -- a *philosophical choice* about the role of government in economic life.

All five levels share the same underlying simulation engine: 45 goods, 30 industries, Leontief input-output matrix, Cobb-Douglas production functions, Walrasian price clearing. The difference is how much of this machinery the player directly controls versus how much they influence indirectly.

### Level 1: Market Economy (Default)

Businesses open and close based on supply and demand. The player builds infrastructure, sets tax rates, enacts regulations. The economy is a *consequence* of urban planning decisions, not something directly managed.

This is still deep. Tax policy affects growth rates. Zoning determines what can be built where. Infrastructure quality determines which industries can operate. Education quality determines what workforce is available. Environmental regulations determine which businesses stay and which leave.

The player shapes the economy *indirectly* through urban design. You don't tell a tech company to open an office. You build a university near a transit hub with good housing and clean air, and tech companies appear because the conditions are right.

*Think: "I built a university near the tech district, invested in fiber optic infrastructure, and kept housing affordable for young professionals. Now startups are clustering here organically."*

### Level 2: Regulatory Economy

The player gains direct regulatory tools:

- Minimum wages, environmental standards, building codes
- Anti-monopoly enforcement, labor protections, consumer safety
- Tax incentives and abatements to attract specific industries
- Zoning overlays: enterprise zones, historic districts, green energy districts, innovation corridors
- Trade agreements and tariffs with neighboring cities

The economy still runs on market forces, but the player sets the rules of the game. Smart regulation creates the conditions for the growth you want. Bad regulation strangles it.

*Think: "I'm creating an enterprise zone with tax breaks for clean energy manufacturers, strict emission standards to drive out dirty industry, and vocational training programs to supply the workforce. The market does the rest."*

### Level 3: Mixed Economy

The player directly operates key sectors: energy, water, transit, healthcare, housing. State-owned enterprises compete alongside private businesses.

Industrial policy becomes available: subsidize strategic industries, protect infant industries with tariffs, fund R&D programs. Public housing programs run parallel to private development. The player must balance the efficiency of markets with the equity of public provision.

*Think: "I run the power grid, the water system, and public transit. I built public housing in every district to keep rents stable. Private developers handle commercial and luxury residential. I'm subsidizing the semiconductor industry because I want to be a chip hub in 20 years."*

### Level 4: Dirigiste Economy

The player directs major investment through a state development bank. Five-year economic plans set production targets and investment priorities. The government picks winners.

Private businesses exist but follow state direction through a combination of incentives, regulations, and direct investment. Import substitution or export-oriented industrialization -- the player chooses the development strategy.

*Think: "I am Singapore. I've identified biotech, fintech, and advanced manufacturing as strategic sectors. The state development bank is funding research parks, the education system is producing specialized graduates, and foreign companies receive tax holidays if they transfer technology. Private enterprise thrives -- within the framework I've designed."*

### Level 5: Command Economy

The central planning board -- which is the player -- sets ALL production quotas for all 45 goods across all 30 industries. Prices are fixed by decree. Housing is state-allocated. Employment is state-directed. Private business does not exist.

The player interacts directly with the Leontief input-output matrix. This is Gosplan. You are solving a system of simultaneous equations where every output is someone else's input, every surplus creates waste, and every shortage cascades through the entire production chain.

```
CENTRAL PLANNING BOARD - Monthly Production Review

Steel Mill #3 (Eastside Industrial District)
  Required inputs:  140 tons iron ore, 80 tons coal, 12 MW electricity
  Current supply:   Iron ore: 140t [OK], Coal: 60t [DEFICIT], Electricity: 12 MW [OK]

  Coal shortage cause: Railroad Line 7 capacity maxed at 150t/month
                       Coal Mine #1 produces 200t but only 150t reaches the mill

  OPTIONS:
  (A) Build second rail line to Coal Mine #1     [Cost: 12M, Time: 8 months]
  (B) Reduce steel quota from 500t to 380t       [Cascading shortage in construction sector]
  (C) Open new mine closer to mill               [Cost: 45M, Time: 18 months]
  (D) Redirect coal from Steel Mill #1 (Westside) [Westside steel quota drops by 40t]
```

This is not a simplification. This is not an abstraction. The player is *literally solving the economic calculation problem* that brought down the Soviet Union. It is brutally difficult. It is deeply rewarding when it works. And when it fails -- when the cascading shortages spiral out of control, when citizens queue for bread while warehouses overflow with unsold televisions -- it fails in historically authentic ways.

*Think: "I am directly solving the Leontief equation. I AM the economy."*

---

## The Competitive Landscape

No existing game occupies this design space. Every competitor makes tradeoffs that leave major simulation dimensions shallow or absent.

| Game | Urban Planning | Economics | Politics | Culture | Demographics |
|------|:---:|:---:|:---:|:---:|:---:|
| **Cities: Skylines 2** | Good | None | None | None | Basic |
| **SimCity 4** | Good | Basic (RCI) | None | None | None |
| **Anno 1800** | Basic | Good (supply chains) | None | None | Tier-based |
| **Victoria 3** | None (grand strategy) | Excellent | Excellent | Good | Good |
| **Tropico 6** | Basic | Basic | Flavor | Flavor | Basic |
| **Workers & Resources** | Basic | Excellent (planned only) | None | None | None |
| **Frostpunk** | None | Crisis management | Moral pressure | None | Survival |
| **This Game** | **Excellent** | **Excellent (full spectrum)** | **Excellent** | **Excellent** | **Excellent** |

Victoria 3 comes closest in simulation depth -- but it is a grand strategy game, not a city builder. You manage nations, not neighborhoods. You don't place buildings or design intersections or watch individual citizens walk to work.

Workers & Resources comes closest in economic simulation -- but it only models command economies, has no political simulation, no cultural dynamics, and no demographic lifecycle.

Cities: Skylines 2 comes closest in urban planning -- but it has no economy (the "demand" system is a simplified abstraction), no politics, no culture, and only basic population tracking.

**No game has ever combined deep urban planning, deep economics, deep politics, deep culture, and deep demographics in a single simulation.** This is the gap. This is the game.

---

## The Tagline

Not "build a city." Not "manage an economy."

**"Every decision echoes for generations."**

---

Build a school today.

Watch graduates enter the workforce in 15 years. See them start families and demand housing in neighborhoods they can afford. Watch those neighborhoods improve as educated, employed families move in. See property values rise. Watch the original residents -- the ones who couldn't afford the school's district -- get priced out. See them relocate to the district across the highway, where the schools are worse and the air is dirtier.

Watch political factions shift as the demographics of each district change. See the next election produce a different city council. Watch new laws reshape zoning, taxation, and public investment. See the ripple effects cascade through the economy as businesses respond to the new regulatory environment.

Twenty years later, the school you built has transformed the city. Not in the way you planned. Not entirely in ways you wanted. But in ways that are *logical*, *traceable*, and *yours*.

Fifty years later, the grandchildren of those first graduates are running the city. Their values were shaped by the culture of the neighborhoods they grew up in, which were shaped by the economic conditions of their parents' generation, which were shaped by the education system you designed, which was shaped by the tax revenue from the industries you attracted, which were attracted by the infrastructure you built.

One decision. A hundred years of consequences.

That is the game.
