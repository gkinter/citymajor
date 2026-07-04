# Research: 10 Economic Coefficients

The complete Leontief Input-Output Technology Matrix plan is ready. Here is a summary of what was produced and the research backing it.

## What the Plan Contains

The plan file at `/Users/fredericbeeg/.claude/plans/jazzy-whistling-piglet-agent-a3602d32d6e836bab.md` defines the complete economic model across **20 production chains**, **45 goods/resources**, and **5 eras** (E1: 1850-1899 through E5: 2020+).

### 20 Production Chains with Full Specifications

Each building specifies exact inputs, outputs, worker counts by education level, power draw (MW), water consumption (kL/month), pollution output (PU/month), construction cost, and monthly maintenance.

1. **Food -- Grain**: Wheat Farm -> Flour Mill -> Bakery (3 tiers each)
2. **Food -- Livestock**: Cattle Ranch, Poultry Farm, Fishing Wharf, Fish Farm
3. **Food -- Processing**: Food Market converting raw food to population food_units
4. **Construction -- Timber**: Logging Camp -> Sawmill (2 tiers)
5. **Construction -- Brick & Cement**: Brickworks, Cement Works (2 tiers each)
6. **Construction -- Steel**: Iron Mine -> Coal Mine -> Blast Furnace/Integrated Mill/EAF (full chain, 3 eras)
7. **Textile**: Cotton Plantation -> Gin -> Spinning Mill -> Weaving Mill -> Garment Factory
8. **Basic Energy -- Coal & Oil**: Coal/Oil Power Plants, Oil Well, Oil Refinery
9. **Advanced Energy**: Gas, Nuclear, Solar, Wind (onshore + offshore), Fusion
10. **Automotive**: Early (Model T era), Assembly Line, Robotic, EV Factory
11. **Electronics**: Sand Quarry -> Silicon Smelter -> Polysilicon Plant -> Semiconductor Fab
12. **Petrochemicals**: Naphtha -> Plastics/Chemicals, Pharmaceutical Plant
13. **Advanced Materials**: Lithium Mine/Refinery, Battery Factory, Rare Earth chain, Magnet Factory
14. **Consumer Goods**: Factory combining plastics + electronics + steel
15. **Water & Sanitation**: Pump Station, Water Treatment, Sewage Treatment, Waste/Recycling
16. **Mining -- Support**: Limestone, Clay, Nickel/Cobalt, Rubber, Glass
17. **Transportation**: Horse Stables, Train, Tram/Bus, Airport, Port
18. **Education & Research**: School, University, Research Lab, Advanced Research Center
19. **Services & Commerce**: Stores, Bank, Hospital
20. **Government & Civic**: Town Hall, City Hall, Police, Fire, Parks

### Research Sources for Key Ratios

All core coefficients trace to real-world data:

- **Steel**: World Steel Association (1.37t ore + 0.78t coal + 0.27t limestone per ton steel for BF-BOF)
- **Cement**: US EPA (1.5t limestone per ton clinker, 3.0-3.6 GJ/t)
- **Agriculture**: FAO (wheat yields 1.0 t/ha in 1850 to 3.5 t/ha in 2020, flour extraction 75%)
- **Energy**: IEA/NREL/DOE (coal 35-45% efficiency, NGCC 50-55%, nuclear 33% at 2.8g U/MWh, solar 24% CF, wind 36% CF onshore)
- **Automotive**: American Chemistry Council 2023 (977 kg steel, 226 kg aluminum, 193 kg plastics per vehicle)
- **Electronics**: USGS (2.5 kg quartzite per kg silicon), industry data (40% kerf loss, 80-90% die yield)
- **Petrochemicals**: EIA/ICIS (naphtha to ethylene 3.3:1, ethylene to PE ~1:1)
- **Batteries**: ICCT/EU Commission (NMC811: 0.10 kg Li, 0.65 kg Ni, 0.08 kg Co per kWh)
- **Water/Waste**: USGS (337 L/capita/day), EPA (0.81 t MSW/capita/year)

### Implementation Plan

When approved, this will be implemented as:
- `docs/research/LEONTIEF_MATRIX.md` -- the reference document
- `lib/game/goods.ts` -- resource enum (45 goods)
- `lib/game/eras.ts` -- 5 era definitions with tech unlock trees
- `lib/game/production-chains.ts` -- all building definitions as typed constants
- `lib/game/types.ts` -- TypeScript interfaces for the economic model