import {
  WEB_V1_PLAYABLE_ERAS,
  type WebV1PlayableEra,
} from "@/lib/tech-catalog";
import {
  isBaselineContent,
  isContentUnlocked,
} from "@/lib/tech-unlocks";

/** v1 transit paint modes — bus and rail stubs for Frontier → Industrial. */
export type TransitModeKind = "bus" | "rail";

export type TransitModeId = "horse_bus_line" | "rail_track";

export type TransitModeDefinition = {
  id: TransitModeId;
  kind: TransitModeKind;
  /** Content key from technologies.json / v1-unlock-bridge capabilities. */
  contentKey: TransitModeId;
  label: string;
  shortLabel: string;
  overlayColor: string;
  era: WebV1PlayableEra;
};

/**
 * v1 transit mode stubs — AGENT_04 Phase 3 subset.
 * Keys: horse_bus_line (T003), rail_track (T002).
 */
export const TRANSIT_MODE_DEFINITIONS: readonly TransitModeDefinition[] = [
  {
    id: "horse_bus_line",
    kind: "bus",
    contentKey: "horse_bus_line",
    label: "Horse Bus",
    shortLabel: "Bus",
    overlayColor: "#b8860b",
    era: "frontier",
  },
  {
    id: "rail_track",
    kind: "rail",
    contentKey: "rail_track",
    label: "Rail",
    shortLabel: "Rail",
    overlayColor: "#5c4a3a",
    era: "frontier",
  },
] as const;

export const DEFAULT_TRANSIT_MODE: TransitModeId = "horse_bus_line";

const PLAYABLE_ERA_SET = new Set<string>(WEB_V1_PLAYABLE_ERAS);

export function toUnlockedTechSet(
  unlockedTechIds?: readonly number[],
): ReadonlySet<number> {
  return new Set(unlockedTechIds ?? []);
}

/** Transit modes exposed in web v1 (Frontier + Industrial only). */
export function getPlayableTransitModes(): readonly TransitModeDefinition[] {
  return TRANSIT_MODE_DEFINITIONS.filter((def) =>
    PLAYABLE_ERA_SET.has(def.era),
  );
}

/** Playable transit modes grouped by kind for toolbar layout. */
export function getPlayableTransitModesByKind(): Record<
  TransitModeKind,
  readonly TransitModeDefinition[]
> {
  const modes = getPlayableTransitModes();
  return {
    bus: modes.filter((def) => def.kind === "bus"),
    rail: modes.filter((def) => def.kind === "rail"),
  };
}

/** Whether a transit mode is available from baseline content or researched tech. */
export function isTransitModeUnlocked(
  modeId: TransitModeId,
  unlockedTechIds?: readonly number[],
): boolean {
  const def = TRANSIT_MODE_DEFINITIONS.find((d) => d.id === modeId);
  if (!def) return false;
  if (isBaselineContent(def.contentKey)) return true;
  return isContentUnlocked(def.contentKey, toUnlockedTechSet(unlockedTechIds));
}

export function transitModeLabel(modeId: TransitModeId): string {
  return (
    TRANSIT_MODE_DEFINITIONS.find((d) => d.id === modeId)?.label ?? "Transit"
  );
}

export function transitModeOverlayColor(modeId: TransitModeId): string {
  return (
    TRANSIT_MODE_DEFINITIONS.find((d) => d.id === modeId)?.overlayColor ??
    "#6b7280"
  );
}
