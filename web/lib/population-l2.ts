import type { SimResources } from "@/lib/sim-bridge";

/** Named household row when WASM exports population L2 sample. */
export type HouseholdPreview = {
  id: string;
  tileX: number;
  tileZ: number;
  /** 0–1 satisfaction. */
  happiness: number;
  /** Commute time in game minutes. */
  commuteMin: number;
};

export type PopulationL2Snapshot = {
  households: HouseholdPreview[];
};

export type CitizenAggregateStats = {
  population: number;
  householdCount: number;
  happiness?: number;
  populationGrowthRate?: number;
  avgHappiness?: number;
  avgCommuteMin?: number;
};

export type CitizenDotPick = {
  instanceId: number;
  buildingIndex: number;
  tileX: number;
  tileZ: number;
};

export function parsePopulationL2(raw: unknown): PopulationL2Snapshot | undefined {
  if (!raw || typeof raw !== "object") return undefined;
  const householdsRaw = (raw as { households?: unknown }).households;
  if (!Array.isArray(householdsRaw)) return undefined;

  const households: HouseholdPreview[] = [];
  for (const entry of householdsRaw) {
    if (!entry || typeof entry !== "object") continue;
    const row = entry as Record<string, unknown>;
    if (typeof row.id !== "string") continue;
    if (typeof row.tileX !== "number" || typeof row.tileZ !== "number") continue;
    households.push({
      id: row.id,
      tileX: row.tileX,
      tileZ: row.tileZ,
      happiness: typeof row.happiness === "number" ? row.happiness : 0,
      commuteMin: typeof row.commuteMin === "number" ? row.commuteMin : 0,
    });
  }

  if (households.length === 0) return undefined;
  return { households };
}

export function householdsFromResources(
  resources: SimResources | null,
): HouseholdPreview[] {
  return resources?.populationL2?.households ?? [];
}

export function aggregateStatsFromResources(
  resources: SimResources | null,
): CitizenAggregateStats | null {
  if (!resources) return null;

  const households = householdsFromResources(resources);
  const householdCount =
    resources.householdCount ??
    (households.length > 0 ? households.length : undefined) ??
    (resources.population > 0 ? Math.ceil(resources.population / 2.4) : 0);

  let avgHappiness: number | undefined;
  let avgCommuteMin: number | undefined;
  if (households.length > 0) {
    avgHappiness =
      households.reduce((sum, hh) => sum + hh.happiness, 0) / households.length;
    avgCommuteMin =
      households.reduce((sum, hh) => sum + hh.commuteMin, 0) / households.length;
  }

  return {
    population: resources.population,
    householdCount,
    happiness: resources.happiness,
    populationGrowthRate: resources.populationGrowthRate,
    avgHappiness,
    avgCommuteMin,
  };
}

export function findHouseholdAtTile(
  households: HouseholdPreview[],
  tileX: number,
  tileZ: number,
): HouseholdPreview | undefined {
  return households.find((hh) => hh.tileX === tileX && hh.tileZ === tileZ);
}

export function findHouseholdById(
  households: HouseholdPreview[],
  id: string | undefined,
): HouseholdPreview | undefined {
  if (!id) return undefined;
  return households.find((hh) => hh.id === id);
}

export function resolveSelectedHousehold(
  resources: SimResources | null,
  selection: { householdId?: string; tileX?: number; tileZ?: number } | null,
): HouseholdPreview | undefined {
  const households = householdsFromResources(resources);
  if (households.length === 0 || !selection) return undefined;

  const byId = findHouseholdById(households, selection.householdId);
  if (byId) return byId;

  if (selection.tileX !== undefined && selection.tileZ !== undefined) {
    return findHouseholdAtTile(households, selection.tileX, selection.tileZ);
  }

  return undefined;
}

export function formatHappiness(value: number): string {
  return `${Math.round(Math.min(1, Math.max(0, value)) * 100)}%`;
}

export function formatCommuteMin(value: number): string {
  if (value < 1) return "<1 min";
  if (value < 60) return `${value.toFixed(0)} min`;
  const hours = value / 60;
  return `${hours.toFixed(1)} hr`;
}
