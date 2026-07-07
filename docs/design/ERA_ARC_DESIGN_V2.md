# CityMajor: Era Arc Design (V2)

## 1. Intended Player Journey Per Era (30-Minute Slices)

The game spans 200 years (1850-2050+), broken down into five distinct eras. Assuming a ~150-minute core gameplay loop for a full city arc, each era represents roughly a 30-minute slice of focused gameplay where the primary challenges, aesthetics, and economic realities shift.

*   **Slice 1: Frontier (0-30 min)**
    *   **Focus:** Survival and foundation. Laying out the initial grid, connecting dirt roads, securing basic resources (food, water from wells), and managing early health risks (cholera outbreaks).
    *   **Pacing:** Slow, intimate. Placing individual wooden structures. Early steam power and cobblestone roads define the peak of this slice.
*   **Slice 2: Industrial (30-60 min)**
    *   **Focus:** Rapid expansion, mass production, and managing consequences. The city explodes in scale. The player must manage heavy coal pollution, worker strikes, and the shift to zoning over individual placement.
    *   **Pacing:** Frantic growth. Implementing the first mass transit (trams/trains) and dealing with fires and crime waves in dense brick tenements.
*   **Slice 3: Postwar (60-90 min)**
    *   **Focus:** Suburbanization, mass consumption, and infrastructure modernization. The city spreads out. Highways divide the city, commercial aviation connects it globally, and the player balances massive energy grids (hydro/nuclear).
    *   **Pacing:** Strategic realignment. Tearing down old slums for highway interchanges, managing a baby boom, and funding universities.
*   **Slice 4: Modern (90-120 min)**
    *   **Focus:** Information economy, gentrification, and ecological correction. Heavy industry leaves, replaced by tech hubs and service economies. The player faces housing crises, traffic gridlock, and the need to retrofit the city with green tech and smart grids.
    *   **Pacing:** Optimization and density. Building vertical glass towers, metro networks, and fighting pollution.
*   **Slice 5: Future (120-150 min)**
    *   **Focus:** Post-scarcity, extreme density, and sci-fi mega-projects. Managing the social fallout of AI automation (UBI debates) while deploying fusion power, arcologies, and hyperloop networks.
    *   **Pacing:** Sandbox mastery. Achieving utopia or managing a high-tech dystopia.

## 2. Building & Visual Unlock Cadence

The `BuildingRenderer` currently supports a procedural, era-aware visual progression driven by the building's `TypeId` within its category.

*   **Frontier (`x00-x19`):** 
    *   *Visuals:* Warm woods, rust, horizontal timber lines, peaked roofs. Max 3 stories.
    *   *Palette:* `#8D6E63` (wood) to `#A1887F`.
*   **Industrial (`x20-x39`):**
    *   *Visuals:* Dark brick, charcoal steel, sawtooth roofs for factories, flat roofs with parapets. Chimneys belching smoke.
    *   *Palette:* `#B71C1C` (brick red), `#37474F` (charcoal).
*   **Postwar (`x40-x59`):**
    *   *Visuals:* Light gray, cream, blue-gray concrete, prefabricated modular looks. Flat roofs.
    *   *Palette:* `#CFD8DC` (concrete), `#ECEFF1`.
*   **Modern (`x60-x79`):**
    *   *Visuals:* Blue glass curtain walls, medium steel, green roofs. Massive height increases (up to 60 stories).
    *   *Palette:* `#1565C0` (glass blue), `#90CAF9`.
*   **Future (`x80-x99`):**
    *   *Visuals:* White-blue sleek surfaces, cyan tints, dark glass, arcologies scaling up to 300 stories.
    *   *Palette:* `#E1F5FE`, `#B2EBF2`.

## 3. Research Milestones That Flip the City Aesthetic

The aesthetic transition between eras isn't just a color palette swap; it's driven by pivotal technologies that redefine the city's physical form.

*   **Frontier → Industrial:**
    *   **Structural Steel (T030) & Reinforced Concrete (T031):** Breaks the 6-story limit of brick, allowing skylines to emerge (up to 30 stories).
    *   **Coal Power Plant (T017):** Introduces smokestacks and heavy smog to the visual layer.
    *   **Asphalt Roads (T004):** Replaces dirt/cobblestone with dark, smooth thoroughfares.
*   **Industrial → Postwar:**
    *   **Highway Engineering (T007):** Massive footprint interchanges physically divide existing neighborhoods.
    *   **Prefabricated Construction (T032):** Spawns sprawling, uniform suburban housing tracts.
*   **Postwar → Modern:**
    *   **Glass and Curtain Wall (T034):** Transforms dull concrete skylines into reflecting glass monuments.
    *   **Metro/Subway System (T011):** Cleans up surface-level streetcar congestion.
*   **Modern → Future:**
    *   **Arcology Design (T037):** Introduces mega-structures that dwarf all previous skyscrapers.
    *   **3D-Printed Construction (T036):** Radically alters construction animations (from scaffolding to robotic fabrication).

## 4. Herald Narrative Beats Per Era

The Daily Herald (via the LLM) should contextualize the player's progression with era-specific narrative beats drawn from the `events.json` and world state.

*   **Frontier:** 
    *   *Beats:* "Gold Rush Brings Thousands," "The Railroad Arrives," "Cholera Sweeps the Lower Wards."
    *   *Tone:* Hopeful, rugged, survivalist.
*   **Industrial:**
    *   *Beats:* "Robber Barons Flourish," "Union Workers Strike at Steel Mill," "Smog Chokes the City."
    *   *Tone:* Gritty, tense, class-divided.
*   **Postwar:**
    *   *Beats:* "The Automobile Boom," "Television in Every Home," "Flight 101 Touches Down at New Airport."
    *   *Tone:* Optimistic, consumerist, sprawling.
*   **Modern:**
    *   *Beats:* "The Internet Revolution," "Housing Bubble Bursts," "Gentrification Protests Erupt Downtown."
    *   *Tone:* Fast-paced, connected, anxious.
*   **Future:**
    *   *Beats:* "AI Automation Wave Causes Mass Layoffs," "Quantum Breakthrough at the University," "Debate Over Universal Basic Income Rages."
    *   *Tone:* Utopian vs. Dystopian, post-scarcity, hyper-advanced.

## 5. Current Gaps

*   **Era Proxy:** Currently, a building's era is purely derived from its `TypeId` (`DeriveEra` in `BuildingRenderer.cs`), and the global era check (`check era` in `IronAndOakGame.cs`) is just a commented-out tick proxy.
*   **Lack of Player-Facing Goals:** The player never "achieves" the next era. The city just slowly morphs as new technologies are unlocked. There is no triumphant moment or fanfare signaling that the city has entered the Industrial or Modern age.
*   **Path Dependency Weakness:** While `MASTER_GAME_CONCEPT` mentions that choices in early eras constrain later ones, there are no hard mechanical gates forcing the player to commit to an era-defining strategy.

## 6. Proposals: Fleshing Out the Era Arc

To make era progression feel earned and monumental, we propose the following mechanics:

### A. Era Quests (LLM-Driven)
The LLM should generate a major "Era Transition Quest" when the player approaches the thresholds for the next era. 
*   *Example (Frontier → Industrial):* "The Centennial Exhibition." The mayor must stockpile steel, build a central train station, and pass a basic education act. The LLM tracks progress and generates celebratory articles upon completion.

### B. Landmark Unlocks
Eras should be hard-gated behind the construction of a **Monumental Landmark**.
*   *Industrial Gate:* "The Grand Terminus" (requires X steel, Y capital).
*   *Postwar Gate:* "The Interstate Hub" or "The First Hydro Dam."
*   *Modern Gate:* "The International Airport" or "The Silicon Campus."
*   *Future Gate:* "The Fusion Reactor" or "The Spaceport."

### C. Population & Tech Gates
Instead of bleeding slowly into the next era, lock the next era's tech tree until the player satisfies a trifecta of conditions:
1.  **Population Gate:** (e.g., 25,000 for Industrial, 250,000 for Modern).
2.  **Tech Gate:** Must have researched at least 80% of the previous era's technologies.
3.  **Stability Gate:** Must maintain a positive approval rating or high happiness for 3 consecutive months, proving the city is stable enough to survive the upheaval of an era shift.