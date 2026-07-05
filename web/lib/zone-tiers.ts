import {
  isBaselineContent,
  isContentUnlocked,
} from "@/lib/tech-unlocks";
import {
  ENGINE_ZONE_TYPES,
  type EngineZoneTypeKey,
  type ZoningTool,
} from "@/lib/zoning";

/** Paintable zone tier — one toolbar slot per engine zone type (TileData.cs). */
export type ZonePaintTool =
  | "residential_low"
  | "residential_high"
  | "commercial"
  | "industrial"
  | "office"
  | "mixed_use"
  | "agricultural";

/** Content unlock key in technologies.json `unlocks` arrays. */
export type ZoneContentUnlockKey =
  | "residential_zone"
  | "residential_high_zone"
  | "commercial_zone"
  | "industrial_zone"
  | "office_zone"
  | "mixed_use_zone"
  | "agricultural_zone";

export type ZoneTierDef = {
  tool: ZonePaintTool;
  engineKey: EngineZoneTypeKey;
  engineZoneTypeId: number;
  contentUnlockKey: ZoneContentUnlockKey;
  label: string;
  shortLabel: string;
};

/** All seven engine zone paint tiers in display order. */
export const ZONE_TIER_DEFS: readonly ZoneTierDef[] = [
  {
    tool: "residential_low",
    engineKey: "residential_low",
    engineZoneTypeId: ENGINE_ZONE_TYPES.residential_low,
    contentUnlockKey: "residential_zone",
    label: "Residential (Low)",
    shortLabel: "R",
  },
  {
    tool: "residential_high",
    engineKey: "residential_high",
    engineZoneTypeId: ENGINE_ZONE_TYPES.residential_high,
    contentUnlockKey: "residential_high_zone",
    label: "Residential (High)",
    shortLabel: "R+",
  },
  {
    tool: "commercial",
    engineKey: "commercial",
    engineZoneTypeId: ENGINE_ZONE_TYPES.commercial,
    contentUnlockKey: "commercial_zone",
    label: "Commercial",
    shortLabel: "C",
  },
  {
    tool: "industrial",
    engineKey: "industrial",
    engineZoneTypeId: ENGINE_ZONE_TYPES.industrial,
    contentUnlockKey: "industrial_zone",
    label: "Industrial",
    shortLabel: "I",
  },
  {
    tool: "office",
    engineKey: "office",
    engineZoneTypeId: ENGINE_ZONE_TYPES.office,
    contentUnlockKey: "office_zone",
    label: "Office",
    shortLabel: "O",
  },
  {
    tool: "mixed_use",
    engineKey: "mixed_use",
    engineZoneTypeId: ENGINE_ZONE_TYPES.mixed_use,
    contentUnlockKey: "mixed_use_zone",
    label: "Mixed Use",
    shortLabel: "Mx",
  },
  {
    tool: "agricultural",
    engineKey: "agricultural",
    engineZoneTypeId: ENGINE_ZONE_TYPES.agricultural,
    contentUnlockKey: "agricultural_zone",
    label: "Agricultural",
    shortLabel: "Ag",
  },
] as const;

export const ZONE_TIER_BY_TOOL: Readonly<Record<ZonePaintTool, ZoneTierDef>> =
  Object.fromEntries(
    ZONE_TIER_DEFS.map((tier) => [tier.tool, tier]),
  ) as Record<ZonePaintTool, ZoneTierDef>;

export const ZONE_TIER_BY_ENGINE_ID: Readonly<
  Record<number, ZoneTierDef | undefined>
> = Object.fromEntries(
  ZONE_TIER_DEFS.map((tier) => [tier.engineZoneTypeId, tier]),
);

/** Legacy v1 toolbar tools → full zone paint tier. */
export const ZONING_TOOL_TO_PAINT_TOOL: Readonly<
  Partial<Record<Exclude<ZoningTool, "bulldoze" | "road">, ZonePaintTool>>
> = {
  residential: "residential_low",
  commercial: "commercial",
  industrial: "industrial",
};

export function zoneTierForTool(tool: ZonePaintTool): ZoneTierDef {
  return ZONE_TIER_BY_TOOL[tool];
}

export function zoneTierForEngineId(
  engineZoneTypeId: number,
): ZoneTierDef | undefined {
  return ZONE_TIER_BY_ENGINE_ID[engineZoneTypeId];
}

export function zoneTierForZoningTool(
  tool: Exclude<ZoningTool, "bulldoze" | "road">,
): ZoneTierDef {
  const paintTool = ZONING_TOOL_TO_PAINT_TOOL[tool];
  if (!paintTool) {
    throw new Error(`No zone tier mapping for zoning tool: ${tool}`);
  }
  return ZONE_TIER_BY_TOOL[paintTool];
}

/** Whether a zone paint tier is available from baseline or researched tech. */
export function isZoneTierUnlocked(
  tool: ZonePaintTool,
  unlockedTechIds: ReadonlySet<number>,
): boolean {
  const { contentUnlockKey } = ZONE_TIER_BY_TOOL[tool];
  return (
    isBaselineContent(contentUnlockKey) ||
    isContentUnlocked(contentUnlockKey, unlockedTechIds)
  );
}

/** Convenience wrapper for legacy R/C/I toolbar tools. */
export function isZoningToolUnlocked(
  tool: Exclude<ZoningTool, "bulldoze" | "road">,
  unlockedTechIds: ReadonlySet<number>,
): boolean {
  const paintTool = ZONING_TOOL_TO_PAINT_TOOL[tool];
  if (!paintTool) return true;
  return isZoneTierUnlocked(paintTool, unlockedTechIds);
}

export function engineZoneTypeIdForTool(tool: ZonePaintTool): number {
  return ZONE_TIER_BY_TOOL[tool].engineZoneTypeId;
}
