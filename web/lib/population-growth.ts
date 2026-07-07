/** Sim seconds per game month (WasmConfig.GameMonthInterval). */
export const GAME_MONTH_SIM_SECONDS = 30;

export type PopulationSample = {
  tick: number;
  population: number;
};

const MAX_SAMPLES = 24;
const MIN_TICK_SPAN = 8;

/**
 * Rolling window of population samples for HUD growth-rate estimation.
 * WASM may export populationGrowthRate later; this keeps the HUD live today.
 */
export class PopulationGrowthTracker {
  private samples: PopulationSample[] = [];

  push(tick: number, population: number): void {
    const last = this.samples[this.samples.length - 1];
    if (last && last.tick === tick && last.population === population) return;

    this.samples.push({ tick, population });
    if (this.samples.length > MAX_SAMPLES) {
      this.samples.shift();
    }
  }

  /** Estimated net population change per game month, or null when unstable. */
  estimatePerMonth(): number | null {
    if (this.samples.length < 2) return null;

    const first = this.samples[0]!;
    const last = this.samples[this.samples.length - 1]!;
    const deltaTick = last.tick - first.tick;
    if (deltaTick < MIN_TICK_SPAN) return null;

    const deltaPop = last.population - first.population;
    return Math.round((deltaPop / deltaTick) * GAME_MONTH_SIM_SECONDS);
  }

  reset(): void {
    this.samples = [];
  }
}

export function formatGrowthPerMonth(rate: number): string {
  if (rate === 0) return "0/mo";
  const sign = rate > 0 ? "+" : "";
  return `${sign}${rate.toLocaleString()}/mo`;
}

/** Effective household budget for citizen-dot rendering. */
export function resolveHouseholdBudget(
  householdCount: number,
  population: number,
): number {
  if (householdCount > 0) return householdCount;
  if (population > 0) return Math.ceil(population / 2.4);
  return 0;
}

/**
 * Distribute exactly `budget` dots across residential buildings (capped at maxDots),
 * weighted by building level. Sum of returned counts always equals min(budget, maxDots).
 */
export function allocateDotsPerBuilding(
  residentialIndices: readonly number[],
  levelForIndex: (buildingIndex: number) => number,
  budget: number,
  maxDots = 12_000,
): number[] {
  const buildingCount = residentialIndices.length;
  if (buildingCount === 0 || budget <= 0) {
    return new Array(buildingCount).fill(0);
  }

  const target = Math.min(budget, maxDots);
  const weights = residentialIndices.map((idx) =>
    Math.max(1, levelForIndex(idx)),
  );
  const totalWeight = weights.reduce((sum, w) => sum + w, 0);

  const quotas = weights.map((w) => (w / totalWeight) * target);
  const counts = quotas.map((q) => Math.floor(q));
  let assigned = counts.reduce((sum, c) => sum + c, 0);

  const remainders = quotas
    .map((q, i) => ({ i, remainder: q - counts[i]! }))
    .sort((a, b) => b.remainder - a.remainder);

  for (const { i } of remainders) {
    if (assigned >= target) break;
    counts[i]!++;
    assigned++;
  }

  return counts;
}
