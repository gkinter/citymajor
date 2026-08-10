/**
 * Cathedral P4.1 — parse/format commuter coverage + home→work O-D sample
 * from WASM snapshot/status JSON for HUD consumers.
 */

/** Aggregated home→work tile pair from PopulationSystem.CollectCommuteOdSample. */
export type CommuteOdSample = {
  homeTileX: number;
  homeTileZ: number;
  workTileX: number;
  workTileZ: number;
  tripCount: number;
};

/** Defensive parse of a single O-D row (camelCase WASM JSON). */
export function parseCommuteOdRow(raw: unknown): CommuteOdSample | null {
  if (!raw || typeof raw !== "object") return null;
  const row = raw as Record<string, unknown>;
  if (typeof row.homeTileX !== "number" || typeof row.homeTileZ !== "number") {
    return null;
  }
  if (typeof row.workTileX !== "number" || typeof row.workTileZ !== "number") {
    return null;
  }
  const tripCount = typeof row.tripCount === "number" ? row.tripCount : 0;
  if (!Number.isFinite(tripCount) || tripCount < 0) return null;
  return {
    homeTileX: row.homeTileX,
    homeTileZ: row.homeTileZ,
    workTileX: row.workTileX,
    workTileZ: row.workTileZ,
    tripCount,
  };
}

/**
 * Parse top O-D pairs from snapshot `commuteOdSample`.
 * Returns undefined when missing/empty/invalid so HUD can hide the list.
 */
export function parseCommuteOdSample(raw: unknown): CommuteOdSample[] | undefined {
  if (raw === undefined || raw === null) return undefined;
  if (!Array.isArray(raw)) return undefined;

  const rows: CommuteOdSample[] = [];
  for (const entry of raw) {
    const row = parseCommuteOdRow(entry);
    if (row) rows.push(row);
  }
  if (rows.length === 0) return undefined;
  return rows;
}

/**
 * Parse `commuterCoverage` (0–1 share of working commuters with valid home+work IDs).
 * Clamps finite numbers into [0, 1]; returns undefined for missing/invalid.
 */
export function parseCommuterCoverage(raw: unknown): number | undefined {
  if (typeof raw !== "number" || !Number.isFinite(raw)) return undefined;
  return Math.min(1, Math.max(0, raw));
}

export function formatCommuterCoverage(coverage: number): string {
  return `${Math.round(coverage * 100)}%`;
}

export function formatOdTilePair(row: CommuteOdSample): string {
  return `(${row.homeTileX},${row.homeTileZ}) → (${row.workTileX},${row.workTileZ})`;
}

/** Top N pairs sorted by tripCount descending (stable for ties). */
export function topCommuteOdPairs(
  sample: CommuteOdSample[] | undefined,
  limit = 5,
): CommuteOdSample[] {
  if (!sample || sample.length === 0 || limit <= 0) return [];
  return [...sample]
    .sort((a, b) => b.tripCount - a.tripCount)
    .slice(0, limit);
}
