/**
 * Cathedral P4 — parse/format WasmTrafficLite car/transit/walk mode shares
 * from WASM snapshot/status JSON for Economy Transport HUD.
 */

/** Defensive parse of a single 0–1 mode share. */
export function parseModeShare(raw: unknown): number | undefined {
  if (typeof raw !== "number" || !Number.isFinite(raw)) return undefined;
  return Math.min(1, Math.max(0, raw));
}

export function formatModeShare(share: number): string {
  return `${Math.round(share * 100)}%`;
}

/** Compact HUD line: Car 65% · Transit 20% · Walk 15%. */
export function formatModeShareTriplet(
  car: number,
  transit: number,
  walk: number,
): string {
  return `Car ${formatModeShare(car)} · Transit ${formatModeShare(transit)} · Walk ${formatModeShare(walk)}`;
}

/**
 * True when at least one share is present so the Transport section can show
 * the mode-share row (missing siblings render as 0%).
 */
export function hasModeShares(resources: {
  carModeShare?: number;
  transitModeShare?: number;
  walkModeShare?: number;
}): boolean {
  return (
    resources.carModeShare !== undefined ||
    resources.transitModeShare !== undefined ||
    resources.walkModeShare !== undefined
  );
}
