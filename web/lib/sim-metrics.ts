import { BuildingCategory } from "@citymajor/sim-types";
import type { SimStateBucket } from "@/lib/narrative-templates";
import type { SimResources } from "@/lib/sim-bridge";
import type { CityData } from "./types";

/** RCI demand at or above this value (-1..+1) counts as an extreme shortage. */
export const RCI_EXTREME_DEMAND = 0.65;

/** Mayor approval below this percent maps to the happiness_low Herald bucket. */
export const LOW_HAPPINESS_APPROVAL = 45;

/** Approval + treasury thresholds for prosperity_high stories. */
export const PROSPERITY_APPROVAL = 70;
export const PROSPERITY_FUNDS = 500_000;

export type NarrativeMetricsInput = {
  healthcareCoverage: number;
} & Partial<
  Pick<
    SimResources,
    | "approval"
    | "cityFunds"
    | "residentialDemand"
    | "commercialDemand"
    | "industrialDemand"
  >
>;

/**
 * M0 heuristic until WASM exports aggregate health coverage.
 * Service buildings proxy clinic/hospital capacity vs population scale.
 */
export function estimateHealthcareCoverage(city: CityData): number {
  const total = city.buildings.length;
  if (total === 0) return 0.15;

  const serviceCount = city.buildings.filter(
    (b) => b.category === BuildingCategory.Service,
  ).length;

  const ratio = serviceCount / Math.max(1, total / 400);
  return Math.min(1, Math.max(0, ratio));
}

/**
 * Map live sim metrics to a Herald template bucket (GAMEPLAY_LOOP #12).
 * Crisis signals (treasury, approval) take priority over RCI extremes.
 */
export function deriveNarrativeBucket(
  metrics: NarrativeMetricsInput,
): SimStateBucket {
  const {
    healthcareCoverage,
    approval,
    cityFunds,
    residentialDemand,
    commercialDemand,
    industrialDemand,
  } = metrics;

  if (cityFunds !== undefined && cityFunds < 0) {
    return "budget_crisis";
  }

  if (approval !== undefined && approval < LOW_HAPPINESS_APPROVAL) {
    return "happiness_low";
  }

  if (
    residentialDemand !== undefined &&
    residentialDemand >= RCI_EXTREME_DEMAND
  ) {
    return "housing_shortage";
  }

  if (
    commercialDemand !== undefined &&
    commercialDemand >= RCI_EXTREME_DEMAND &&
    industrialDemand !== undefined &&
    industrialDemand >= RCI_EXTREME_DEMAND &&
    cityFunds !== undefined &&
    cityFunds >= 0
  ) {
    return "prosperity_high";
  }

  if (healthcareCoverage < 0.3) {
    return "healthcare_low";
  }

  if (
    approval !== undefined &&
    approval >= PROSPERITY_APPROVAL &&
    cityFunds !== undefined &&
    cityFunds >= PROSPERITY_FUNDS
  ) {
    return "prosperity_high";
  }

  return "default";
}
