import { hudEraName } from "@/lib/era";
import { techIdFromCatalogId, techNameFromIndex } from "@/lib/tech-catalog";
import {
  isBaselineContent,
  isContentUnlocked,
} from "@/lib/tech-unlocks";

/** Engine zone type IDs — mirrors TileData.cs / ZoneGrowthSystem.cs */
export const ENGINE_ZONE_TYPE_ID = {
  residentialLow: 1,
  residentialHigh: 2,
  commercial: 3,
  industrial: 4,
  office: 5,
  mixedUse: 6,
  agricultural: 7,
} as const;

/** Max zone type byte in baseline R/C/I palette (procedural fallback). */
export const BASELINE_MAX_ZONE_TYPE = ENGINE_ZONE_TYPE_ID.industrial;

export type ZoneTierTool =
  | "residential"
  | "residential_high"
  | "commercial"
  | "industrial"
  | "office"
  | "mixed"
  | "agricultural";

export type ZoneTier = {
  tool: ZoneTierTool;
  label: string;
  shortLabel: string;
  engineZoneType: number;
  /** Content key for tech-unlocks; baseline keys need no research. */
  contentKey: string;
  /** Catalog tech id (e.g. T030); null = baseline / always available. */
  requiredTechCatalogId: string | null;
  /** Minimum sim era index (0=Frontier) — P2.1 era gates. */
  minEra: number;
  overlayColor: string;
};

/** Paintable zone tiers shown in the zoning toolbar. */
export const ZONE_TIERS: ZoneTier[] = [
  {
    tool: "residential",
    label: "Residential Low",
    shortLabel: "R-low",
    engineZoneType: ENGINE_ZONE_TYPE_ID.residentialLow,
    contentKey: "residential_zone",
    requiredTechCatalogId: null,
    minEra: 0,
    overlayColor: "#5eb3f5",
  },
  {
    tool: "residential_high",
    label: "Residential High",
    shortLabel: "R-high",
    engineZoneType: ENGINE_ZONE_TYPE_ID.residentialHigh,
    contentKey: "residential_high_zone",
    requiredTechCatalogId: "T029",
    minEra: 0,
    overlayColor: "#6366f1",
  },
  {
    tool: "commercial",
    label: "Commercial",
    shortLabel: "C",
    engineZoneType: ENGINE_ZONE_TYPE_ID.commercial,
    contentKey: "commercial_zone",
    requiredTechCatalogId: null,
    minEra: 0,
    overlayColor: "#f0b429",
  },
  {
    tool: "industrial",
    label: "Industrial",
    shortLabel: "I",
    engineZoneType: ENGINE_ZONE_TYPE_ID.industrial,
    contentKey: "industrial_zone",
    requiredTechCatalogId: null,
    minEra: 0,
    overlayColor: "#b87333",
  },
  {
    tool: "office",
    label: "Office",
    shortLabel: "Office",
    engineZoneType: ENGINE_ZONE_TYPE_ID.office,
    contentKey: "office_zone",
    requiredTechCatalogId: "T134",
    minEra: 1,
    overlayColor: "#c084fc",
  },
  {
    tool: "mixed",
    label: "Mixed Use",
    shortLabel: "Mixed",
    engineZoneType: ENGINE_ZONE_TYPE_ID.mixedUse,
    contentKey: "mixed_use_zone",
    requiredTechCatalogId: "T086",
    minEra: 0,
    overlayColor: "#2dd4a8",
  },
  {
    tool: "agricultural",
    label: "Agricultural",
    shortLabel: "Ag",
    engineZoneType: ENGINE_ZONE_TYPE_ID.agricultural,
    contentKey: "agricultural_zone",
    requiredTechCatalogId: null,
    minEra: 0,
    overlayColor: "#84cc16",
  },
];

/** Overlay tints keyed by engine zone type — single source for map/minimap/3D. */
export const ZONE_OVERLAY_COLORS: Record<number, string> = Object.fromEntries(
  ZONE_TIERS.map((tier) => [tier.engineZoneType, tier.overlayColor]),
);

export function zoneTierByTool(tool: ZoneTierTool): ZoneTier | undefined {
  return ZONE_TIERS.find((tier) => tier.tool === tool);
}

/** Human-readable label for an engine zone type id (0 = unzoned). */
export function engineZoneTypeLabel(zoneType: number): string {
  if (zoneType === 0) return "None";
  const tier = ZONE_TIERS.find((t) => t.engineZoneType === zoneType);
  return tier?.shortLabel ?? tier?.label ?? `Type ${zoneType}`;
}

export function requiredTechIndex(catalogId: string): number {
  return techIdFromCatalogId(catalogId);
}

/** True when WASM PaintZone accepts extended zone bytes (office/mixed/ag). */
export function simSupportsExtendedZoneBytes(
  supportsZonePaint?: boolean,
): boolean {
  return supportsZonePaint === true;
}

/** Whether a tier is shown in the toolbar (hidden on procedural fallback). */
export function isZoneTierVisible(
  tier: ZoneTier,
  supportsZonePaint?: boolean,
): boolean {
  if (tier.engineZoneType <= BASELINE_MAX_ZONE_TYPE) return true;
  return simSupportsExtendedZoneBytes(supportsZonePaint);
}

/** Zone tiers exposed in the zoning toolbar for the current sim mode. */
export function getVisibleZoneTiers(
  supportsZonePaint?: boolean,
): ZoneTier[] {
  return ZONE_TIERS.filter((tier) => isZoneTierVisible(tier, supportsZonePaint));
}

/** Whether the city has reached the era gate for a zone tier. */
export function isZoneTierEraUnlocked(
  tier: ZoneTier,
  currentEra?: number,
): boolean {
  const era = currentEra ?? 0;
  return era >= tier.minEra;
}

/** Whether a zone tier is playable with current era + research state. */
export function isZoneTierUnlocked(
  tier: ZoneTier,
  unlockedTechIds?: number[],
  currentEra?: number,
): boolean {
  if (!isZoneTierEraUnlocked(tier, currentEra)) return false;
  if (isBaselineContent(tier.contentKey)) return true;
  const unlocked = new Set(unlockedTechIds ?? []);
  if (tier.requiredTechCatalogId) {
    const techIdx = requiredTechIndex(tier.requiredTechCatalogId);
    if (techIdx >= 0 && unlocked.has(techIdx)) return true;
  }
  return isContentUnlocked(tier.contentKey, unlocked);
}

/** Display name of the tech that gates a locked tier. */
export function lockedTierTechName(tier: ZoneTier): string | undefined {
  if (!tier.requiredTechCatalogId) return undefined;
  const idx = requiredTechIndex(tier.requiredTechCatalogId);
  return techNameFromIndex(idx);
}

/** Era name required when a tier is era-gated. */
export function lockedTierEraName(tier: ZoneTier): string | undefined {
  if (tier.minEra <= 0) return undefined;
  return hudEraName(tier.minEra);
}
