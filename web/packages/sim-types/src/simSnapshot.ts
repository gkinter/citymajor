/**
 * WASM `SimSnapshotDto` TypeScript contract (camelCase JSON from GetRenderSnapshot).
 *
 * Canonical C#: `src/Forge.SimWasm/SimSnapshotDto.cs`
 * Field matrix: `docs/design/SIM_SNAPSHOT_V2.md`
 * Worker protocol: `docs/design/WASM_SIM_BRIDGE.md`
 *
 * Web R3F is an archival harness — these types track DTO accuracy only.
 * Product UI binds Unity `CitySimState`, not this package.
 */

/** Sparse building pool slot — fireRisk/serviceFlags for P5 save/load. */
export type BuildingDto = {
  id: number;
  typeId: number;
  tileX: number;
  tileZ: number;
  level: number;
  /** 0=constructing, 1=operational, 2=abandoned, 3=demolishing */
  state: number;
  condition: number;
  /** Burning intensity (0 = not on fire). */
  fireRisk?: number;
  /** Service bitmask (hydrants, stations, utilities). */
  serviceFlags?: number;
};

export type ZoneDto = {
  tileX: number;
  tileZ: number;
  zoneType: number;
};

export type RoadDto = {
  tileX: number;
  tileZ: number;
  roadFlags: number;
};

/** Segment-graph export — parallel arrays by node/edge id (P1.6 / P4.2). */
export type RoadGraphSnapshotDto = {
  nodeCount: number;
  nodeTypes: number[];
  nodeTileX: number[];
  nodeTileZ: number[];
  edgeCount: number;
  edgeFrom: number[];
  edgeTo: number[];
  edgeVolumes: number[];
  travelTimes: number[];
};

export type TrafficDto = {
  tileX: number;
  tileZ: number;
  density: number;
};

export type FrictionCorridorDto = {
  tileX: number;
  tileZ: number;
  /** Normalized corridor friction (0–1). */
  friction: number;
};

export type ServiceCoverageDto = {
  tileX: number;
  tileZ: number;
  health: number;
  police: number;
  fire: number;
  education: number;
};

export type ActiveEventDto = {
  eventId: number;
  typeId: string;
  phase: string;
  severity: number;
  tileX: number;
  tileY: number;
};

export type GoodImbalanceDto = {
  goodId: number;
  name: string;
  magnitude: number;
  price: number;
};

export type GoodFlowDto = {
  goodId: number;
  name: string;
  production: number;
  demand: number;
  /** (production − demand) / activity, clamped −1…+1. */
  inventoryRate: number;
};

export type MarketZonePriceDto = {
  goodId: number;
  name: string;
  minPrice: number;
  maxPrice: number;
  cityAvgPrice: number;
};

export type EconomySnapshotDto = {
  shortages: GoodImbalanceDto[];
  surpluses: GoodImbalanceDto[];
  flows: GoodFlowDto[];
  marketZoneCount: number;
  marketZonePrices: MarketZonePriceDto[];
};

export type HouseholdPreviewDto = {
  id: string;
  tileX: number;
  tileZ: number;
  happiness: number;
  commuteMin: number;
  homeBuildingId: number;
  workBuildingId: number;
  rentBurden: number;
};

export type PopulationL2Dto = {
  households: HouseholdPreviewDto[];
};

export type CommuteOdSampleDto = {
  homeTileX: number;
  homeTileZ: number;
  workTileX: number;
  workTileZ: number;
  tripCount: number;
};

/**
 * Full render snapshot — mirrors `Forge.SimWasm.SimSnapshotDto` property set.
 * All Cathedral scalars present on tip (Event*Mult, Law*Mult, utilities, fire/EMS, etc.).
 */
export type SimSnapshotDto = {
  tick: number;
  population: number;
  householdCount: number;
  cityFunds: number;
  era: number;
  residentialDemand: number;
  commercialDemand: number;
  industrialDemand: number;
  /** Mayor approval percent (0–100). */
  approval: number;
  happiness: number;
  monthlyIncome: number;
  monthlyExpenses: number;
  /** Net trade balance this month (exports − imports). */
  tradeBalance: number;
  monthlyExportValue: number;
  monthlyImportCost: number;

  buildings: BuildingDto[];
  zones: ZoneDto[];
  roads: RoadDto[];
  roadGraph: RoadGraphSnapshotDto;
  traffic: TrafficDto[];
  serviceCoverage: ServiceCoverageDto[];
  frictionCorridors: FrictionCorridorDto[];
  activeEvents: ActiveEventDto[];
  activeEventCount: number;

  /** laws.json slug ids currently enabled (P6.1 save/load). */
  activeLawIds: string[];
  /** WorldState ordinance bitfield (politics path). */
  activeOrdinances: number;
  nextElectionYear: number;
  lawTrafficCapacityMult: number;
  lawConstructionSpeedMult: number;
  lawSpawnDemandMult: number;
  lawResidentialSpawnMult: number;
  lawIndustrialSpawnMult: number;
  lawCommercialSpawnMult: number;

  eventTaxRevenueMult: number;
  eventImmigrationMult: number;
  eventCommercialSpawnMult: number;
  eventProductivityMult: number;
  eventResearchMult: number;
  eventSpawnDemandMult: number;

  economy: EconomySnapshotDto;
  populationL2: PopulationL2Dto;

  researchPoints: number;
  currentResearchId: number;
  currentResearchProgress: number;
  unlockedTechIds: number[];
  researchQueue: number[];
  queueProgress: number[];
  /** Tech id → eureka bonus (JSON object keys are strings). */
  eurekaBonuses: Record<string, number>;
  branchingChoices: Record<string, number>;

  constructingBuildingCount: number;
  abandonedBuildingCount: number;
  employmentRate: number;
  meanTrafficDensity: number;

  powerCoverageFraction: number;
  waterCoverageFraction: number;
  blackoutFraction: number;
  waterShortageFraction: number;
  utilityStressIndex: number;

  goodsShortageIndex: number;
  goodsSurplusIndex: number;
  interZoneTradeVolume: number;
  meanInterZoneFriction: number;
  goodsTransportCostIndex: number;
  meanGoodsDeliveryDelay: number;

  meanRentBurden: number;
  residentialVacancy: number;

  meanEmergencyResponseMinutes: number;
  hydrantCoverageFraction: number;
  activeFireCount: number;
  meanEmsSurvivalRate: number;
  hospitalBedOccupancyFraction: number;
  availableHospitalBeds: number;
  wildfireRiskIndex: number;
  activeWildfireTileCount: number;
  arsonRiskIndex: number;
  arsonRingActive: boolean;
  lookoutTowerCount: number;
  aerialFirefightingAvailable: boolean;
  /** Fire safety rating 1–10. */
  fireSafetyRating: number;
  fireInsurancePremiumMult: number;

  marketZoneCount: number;
  commuterCoverage: number;
  commuteOdSample: CommuteOdSampleDto[];
  /** Faction id per council seat (length 9). */
  councilSeats: number[];
  /** Politics flavor vector (−1…+1, length 8). */
  culturalDna: number[];

  carModeShare: number;
  transitModeShare: number;
  walkModeShare: number;
  transitLineCount: number;
  busCoverage: number;
};

/** Lightweight GetStatus / resource-merge counters (subset + harness extras). */
export type SimResourcesDto = Pick<
  SimSnapshotDto,
  | "tick"
  | "population"
  | "householdCount"
  | "cityFunds"
  | "era"
  | "residentialDemand"
  | "commercialDemand"
  | "industrialDemand"
  | "approval"
  | "happiness"
  | "monthlyIncome"
  | "monthlyExpenses"
  | "tradeBalance"
  | "monthlyExportValue"
  | "monthlyImportCost"
  | "activeEventCount"
  | "activeLawIds"
  | "activeOrdinances"
  | "nextElectionYear"
  | "lawTrafficCapacityMult"
  | "lawConstructionSpeedMult"
  | "lawSpawnDemandMult"
  | "lawResidentialSpawnMult"
  | "lawIndustrialSpawnMult"
  | "lawCommercialSpawnMult"
  | "eventTaxRevenueMult"
  | "eventImmigrationMult"
  | "eventCommercialSpawnMult"
  | "eventProductivityMult"
  | "eventResearchMult"
  | "eventSpawnDemandMult"
  | "researchPoints"
  | "currentResearchId"
  | "currentResearchProgress"
  | "unlockedTechIds"
  | "constructingBuildingCount"
  | "abandonedBuildingCount"
  | "employmentRate"
  | "meanTrafficDensity"
  | "powerCoverageFraction"
  | "waterCoverageFraction"
  | "blackoutFraction"
  | "waterShortageFraction"
  | "utilityStressIndex"
  | "goodsShortageIndex"
  | "goodsSurplusIndex"
  | "interZoneTradeVolume"
  | "meanInterZoneFriction"
  | "goodsTransportCostIndex"
  | "meanGoodsDeliveryDelay"
  | "meanRentBurden"
  | "residentialVacancy"
  | "meanEmergencyResponseMinutes"
  | "hydrantCoverageFraction"
  | "activeFireCount"
  | "meanEmsSurvivalRate"
  | "hospitalBedOccupancyFraction"
  | "availableHospitalBeds"
  | "wildfireRiskIndex"
  | "activeWildfireTileCount"
  | "arsonRiskIndex"
  | "arsonRingActive"
  | "lookoutTowerCount"
  | "aerialFirefightingAvailable"
  | "fireSafetyRating"
  | "fireInsurancePremiumMult"
  | "marketZoneCount"
  | "commuterCoverage"
  | "councilSeats"
  | "culturalDna"
  | "carModeShare"
  | "transitModeShare"
  | "walkModeShare"
  | "transitLineCount"
  | "busCoverage"
>;
