# AGENT 07: POLITICS & GOVERNANCE

## Role
Build the political simulation: city council, elections, factions, law system (32 laws), approval ratings, lobbying, corruption, protests, media, and policy packages.

---

## Prerequisites
- Agent 01: EventBus, TimeManager
- Agent 02 (partial): Cultural DNA, voter demographics

---

## Phase 1: Approval System (Day 6-7)

### Tasks
1. **Mayor Approval** (0-100):
```
approval = weighted_average(
    city_happiness * 0.30,
    economic_health * 0.20,
    service_quality * 0.15,
    safety * 0.10,
    recent_decisions * 0.15,
    scandal_penalty * 0.10
)
```

2. **Decision Impact**:
   - Every player action (build, demolish, tax change, law) affects approval
   - Impact decays over 6 game months
   - Positive: new park (+2), tax cut (+5), new transit line (+3)
   - Negative: tax hike (-5), demolish historic building (-8), factory in residential (-10)
   - Emergency actions have reduced penalty (disaster response)

3. **Approval Consequences**:
   - >80: Bonus funding from higher government
   - 60-80: Normal operations
   - 40-60: Protests begin, council opposition
   - 20-40: Strikes, council blocks actions, recall vote
   - <20: Forced election / game over scenario

### AI Tool Usage
- **Claude**: Generate approval calculation, decision impact tracking
- **Reference**: POLITICAL_LAW_SYSTEM.md for all values
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/politics/ApprovalSystem.cs`
- `scripts/simulation/politics/DecisionTracker.cs`

---

## Phase 2: City Council & Elections (Day 7-9)

### Tasks (from POLITICAL_LAW_SYSTEM.md)
1. **9-Seat City Council**:
   - 5 factions: Progressive, Conservative, Business, Green, Labor
   - Each faction has policy priorities
   - Faction strength based on city demographics

2. **Elections** (every 4 game years):
   - Faction vote share determined by:
```
faction_vote = base_support * cultural_alignment * satisfaction * campaign_effect
base_support: based on demographics (workers -> Labor, wealthy -> Business)
cultural_alignment: how well faction matches city's cultural DNA
satisfaction: faction gains when their priorities unmet
campaign_effect: random modifier +-10%
```
   - Council composition changes after election
   - Player must work with council majority to pass laws

3. **Council Mechanics**:
   - Propose law -> council vote
   - Simple majority (5/9) for normal laws
   - Supermajority (7/9) for constitutional changes
   - Factions may block or support based on alignment
   - Player can "spend political capital" to push through opposed laws

4. **Recall Elections**:
   - Triggered when approval < 25% for 2+ years
   - Player must campaign (spend money) or accept defeat
   - Defeat = forced reset of all recent unpopular decisions

### AI Tool Usage
- **Claude**: Generate election simulation, faction AI, voting model
- **Claude**: Generate council interaction UI logic
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/politics/CityCouncil.cs`
- `scripts/simulation/politics/ElectionSystem.cs`
- `scripts/simulation/politics/Faction.cs`
- `data/factions.json`

---

## Phase 3: Law System (Day 9-11)

### Tasks
1. **32 Laws** across 7 categories:
```
Traffic Laws (5):
  - Speed limit (30/50/70/unlimited)
  - Parking regulations (free/metered/restricted)
  - Congestion charge (none/peak/always)
  - Bike lane mandate
  - School zone speed limits

Building Codes (5):
  - Height restrictions (none/3/6/12/unlimited stories)
  - Green building requirements
  - Accessibility mandate
  - Historic preservation
  - Density zoning override

Environmental Laws (5):
  - Emission standards (none/basic/strict/zero)
  - Noise ordinance
  - Waste sorting mandate
  - Tree protection
  - Water conservation

Labor Laws (5):
  - Minimum wage (low/medium/high)
  - Worker safety standards
  - Union recognition
  - Overtime regulations
  - Child labor ban

Social Laws (4):
  - Public smoking ban
  - Alcohol regulation
  - Drug policy (prohibition/decriminalization/legalization)
  - Universal basic income (Future era)

Tax Laws (4):
  - Progressive vs flat tax
  - Property tax exemptions
  - Business incentive zones
  - Tourism tax

Zoning Laws (4):
  - Mixed-use zoning
  - Industrial buffer zones
  - Green belt protection
  - Affordable housing quota
```

2. **Law Effects**:
   - Each law modifies simulation parameters
   - E.g., "Emission standards: strict" -> -40% industrial pollution, -15% industrial profit
   - Effects apply city-wide or per-zone depending on law
   - Some laws require enforcement infrastructure (police for speed limits, inspectors for building codes)

3. **Law Approval Requirements**:
   - Each law needs council vote
   - Each faction has predefined stance on each law
   - Cultural DNA affects citizen acceptance
   - Unpopular laws cause protest chance

4. **Law Interactions**:
   - Some laws synergize: Green building + Emission standards = eco-city bonus
   - Some laws conflict: Low minimum wage + Union recognition = strikes
   - Policy packages: predefined sets of compatible laws (Singapore model, Nordic model, etc.)

### AI Tool Usage
- **Claude**: Generate all 32 law definitions with effects as JSON data
- **Claude**: Generate law interaction matrix
- **Reference**: POLITICAL_LAW_SYSTEM.md for full law specs
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/politics/LawSystem.cs`
- `scripts/simulation/politics/LawEffects.cs`
- `scripts/simulation/politics/PolicyPackage.cs`
- `data/laws.json`
- `data/policy_packages.json`

---

## Phase 4: Lobbying, Corruption & Protests (Day 11-12)

### Tasks
1. **12 Lobbying Groups**:
   - Each represents an interest (industry, environment, labor, etc.)
   - Lobby for/against specific laws
   - Player can accept lobby money (budget bonus) at cost (corruption increase)
   - Reject lobbying = lose industry support but keep integrity

2. **Corruption Index** (0-100):
```
corruption = base + lobby_acceptance * 5 + low_transparency * 10 - media_freedom * 15
```
   - Effects: reduced tax collection, higher construction costs, public distrust
   - Anti-corruption measures: transparency laws, independent media, audits

3. **Protest System** (from POLITICAL_LAW_SYSTEM.md):
   - 5 escalation phases: Complaint -> Petition -> Rally -> Protest -> Riot
   - Triggers: unpopular law, service failure, economic crisis
   - Response options: concede, negotiate, ignore, suppress
   - Suppression: works short-term, builds long-term resentment

4. **Media System**:
   - Newspapers (Industrial+), Radio (Postwar+), TV (Modern+), Social media (Future)
   - Media reports on player decisions
   - Positive coverage boosts approval
   - Scandal exposure: corrupt decisions may be exposed
   - State media vs. free press (player choice via laws)

### AI Tool Usage
- **Claude**: Generate lobbying group AI, corruption model, protest escalation
- **Claude**: Generate media system with dynamic news generation
- **Kimi**: NOT needed

### Output Files
- `scripts/simulation/politics/LobbyingSystem.cs`
- `scripts/simulation/politics/CorruptionIndex.cs`
- `scripts/simulation/politics/ProtestSystem.cs`
- `scripts/simulation/politics/MediaSystem.cs`
- `data/lobby_groups.json`

---

## Dependencies

### This Agent Needs
| From | What | When |
|------|------|------|
| Agent 01 | EventBus, TimeManager | Phase 1 |
| Agent 02 | Cultural DNA, voter demographics, satisfaction | Phase 1 |
| Agent 03 | Economic health metrics, tax revenue | Phase 1 |
| Agent 06 | Service quality scores | Phase 1 |

### This Agent Provides
| To | What | Format |
|----|------|--------|
| Agent 02 | Law effects on cultural drift | C# API |
| Agent 03 | Tax policy, regulations affecting business | C# API |
| Agent 06 | Budget allocation decisions | C# API |
| Agent 08 | Approval UI, council panel, law browser, election screen | C# API |

---

## Estimated Duration
- **With AI**: 5-6 days
- **Without AI**: 3-4 weeks
- **Can start**: After Agent 01 Phase 4 (Day 3)
