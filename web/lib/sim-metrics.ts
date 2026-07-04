import { BuildingCategory } from "@citymajor/sim-types";
import type { SimStateBucket } from "@/lib/narrative-templates";
import type { CityData } from "./types";

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

export function deriveNarrativeBucket(healthcareCoverage: number): SimStateBucket {
  return healthcareCoverage < 0.3 ? "healthcare_low" : "default";
}
