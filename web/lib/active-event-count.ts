import type { ActiveEventSnapshot } from "@/lib/sim-bridge";

/**
 * Resolve Herald HUD active-event count from WASM status scalar or event array.
 * Prefer the explicit scalar so empty cities still show `0` when exported.
 */
export function resolveActiveEventCount(
  count: number | undefined,
  events: ActiveEventSnapshot[] | undefined,
): number {
  if (typeof count === "number" && Number.isFinite(count) && count >= 0) {
    return Math.floor(count);
  }
  return events?.length ?? 0;
}

/** Format Herald badge label; omit when zero so the quota badge stays primary. */
export function formatActiveEventCountBadge(count: number): string | undefined {
  if (!Number.isFinite(count) || count <= 0) return undefined;
  return String(Math.floor(count));
}
