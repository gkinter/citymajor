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
    overlayColor: "#4a90d9",
  },
  {
    tool: "residential_high",
    label: "Residential High",
    shortLabel: "R-high",
    engineZoneType: ENGINE_ZONE_TYPE_ID.residentialHigh,
    contentKey: "residential_high_zone",
    requiredTechCatalogId: "T030",
    overlayColor: "#3a7bc8",
  },
  {
    tool: "commercial",
    label: "Commercial",
    shortLabel: "C",
    engineZoneType: ENGINE_ZONE_TYPE_ID.commercial,
    contentKey: "commercial_zone",
    requiredTechCatalogId: null,
    overlayColor: "#e8b84a",
  },
  {
    tool: "industrial",
    label: "Industrial",
    shortLabel: "I",
    engineZoneType: ENGINE_ZONE_TYPE_ID.industrial,
    contentKey: "industrial_zone",
    requiredTechCatalogId: null,
    overlayColor: "#8b7355",
  },
  {
    tool: "office",
    label: "Office",
    shortLabel: "Office",
    engineZoneType: ENGINE_ZONE_TYPE_ID.office,
    contentKey: "office_zone",
    requiredTechCatalogId: "T086",
    overlayColor: "#9b7ed9",
  },
  {
    tool: "mixed",
    label: "Mixed Use",
    shortLabel: "Mixed",
    engineZoneType: ENGINE_ZONE_TYPE_ID.mixedUse,
    contentKey: "mixed_use_zone",
    requiredTechCatalogId: "T085",
    overlayColor: "#5cb88a",
  },
  {
    tool: "agricultural",
    label: "Agricultural",
    shortLabel: "Ag",
    engineZoneType: ENGINE_ZONE_TYPE_ID.agricultural,
    contentKey: "agricultural_zone",
    requiredTechCatalogId: "T047",
    overlayColor: "#8cb43c",
  },
];

export function zoneTierByTool(tool: ZoneTierTool): ZoneTier | undefined {
  return ZONE_TIERS.find((tier) => tier.tool === tool);
}

export function requiredTechIndex(catalogId: string): number {
  return techIdFromCatalogId(catalogId);
}

/** Whether a zone tier is playable with the current research state. */
export function isZoneTierUnlocked(
  tier: ZoneTier,
  unlockedTechIds?: number[],
): boolean {
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
