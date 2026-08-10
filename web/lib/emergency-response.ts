/**
 * Cathedral P5.2 — parse/format mean emergency response minutes from WASM
 * status for Resources / Economy HUD.
 */

/** Defensive parse of a finite, non-negative response-minutes value. */
export function parseEmergencyResponseMinutes(
  raw: unknown,
): number | undefined {
  if (typeof raw !== "number" || !Number.isFinite(raw) || raw < 0) {
    return undefined;
  }
  return raw;
}

/** Compact HUD readout, e.g. `4.2m` / `30m`. */
export function formatEmergencyResponseMinutes(minutes: number): string {
  if (!Number.isFinite(minutes) || minutes < 0) return "—";
  if (minutes >= 100) return `${Math.round(minutes)}m`;
  if (minutes >= 10) return `${minutes.toFixed(0)}m`;
  return `${minutes.toFixed(1)}m`;
}
