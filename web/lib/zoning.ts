import { GRID_SIZE } from "./constants";
import {
  ENGINE_ZONE_TYPE_ID,
  ZONE_OVERLAY_COLORS,
  engineZoneTypeLabel,
  ZONE_TIERS,
  type ZoneTierTool,
} from "./zone-tiers";

/** Active zoning tool in the /play toolbar. */
export type ZoningTool =
  | ZoneTierTool
  | "bulldoze"
  | "road";

/** Engine zone density bytes — mirrors TileData.ZoneDensity (low=1, med=2, high=3). */
export const ZONE_DENSITY_LEVELS = [1, 2, 3] as const;
export type ZoneDensityLevel = (typeof ZONE_DENSITY_LEVELS)[number];

export const ZONE_DENSITY_LABELS: Record<ZoneDensityLevel, string> = {
  1: "Low",
  2: "Med",
  3: "High",
};

export function nextZoneDensity(current: ZoneDensityLevel): ZoneDensityLevel {
  const idx = ZONE_DENSITY_LEVELS.indexOf(current);
  return ZONE_DENSITY_LEVELS[(idx + 1) % ZONE_DENSITY_LEVELS.length]!;
}
export type PaintBrushSize = (typeof PAINT_BRUSH_SIZES)[number];

/** Engine zone type IDs keyed by toolbar tool. */
export const ENGINE_ZONE_TYPE: Record<
  Exclude<ZoningTool, "bulldoze" | "road">,
  number
> = Object.fromEntries(
  ZONE_TIERS.map((tier) => [tier.tool, tier.engineZoneType]),
) as Record<Exclude<ZoningTool, "bulldoze" | "road">, number>;

/** Re-export engine zone constants for callers that need raw IDs. */
export { ENGINE_ZONE_TYPE_ID, ZONE_OVERLAY_COLORS };

export type ZoneTile = {
  tileX: number;
  tileZ: number;
  zoneType: number;
};

export type RoadTile = {
  tileX: number;
  tileZ: number;
  roadFlags: number;
};

/** Semi-transparent overlay tint for placed road tiles. */
export const ROAD_OVERLAY_COLOR = "#6b7280";

/** Sparse road tile with congestion density (0–1) from WasmTrafficLite. */
export type TrafficTile = {
  tileX: number;
  tileZ: number;
  density: number;
};

/** Sparse market-zone boundary friction heat (0–1) for P3.4 overlay. */
export type FrictionCorridorTile = {
  tileX: number;
  tileZ: number;
  friction: number;
};

export const ZONING_TOOLS: {
  id: ZoningTool;
  label: string;
  shortLabel: string;
  stub?: boolean;
}[] = [
  ...ZONE_TIERS.map((tier) => ({
    id: tier.tool,
    label: tier.label,
    shortLabel: tier.shortLabel,
  })),
  { id: "bulldoze", label: "Bulldoze", shortLabel: "✕" },
  { id: "road", label: "Road", shortLabel: "Rd" },
];

export function zoningToolLabel(tool: ZoningTool): string {
  return ZONING_TOOLS.find((t) => t.id === tool)?.label ?? tool;
}

export function zoningToolShortLabel(tool: ZoningTool): string {
  return ZONING_TOOLS.find((t) => t.id === tool)?.shortLabel ?? tool;
}

/** Overlay tint for the active toolbar tool (zone type or special tools). */
export function zoningToolColor(tool: ZoningTool): string {
  if (tool === "bulldoze") return "#e85d5d";
  if (tool === "road") return ROAD_OVERLAY_COLOR;
  return ZONE_OVERLAY_COLORS[ENGINE_ZONE_TYPE[tool]] ?? "#888888";
}

export function nextBrushSize(current: PaintBrushSize): PaintBrushSize {
  const idx = PAINT_BRUSH_SIZES.indexOf(current);
  return PAINT_BRUSH_SIZES[(idx + 1) % PAINT_BRUSH_SIZES.length]!;
}

/** Immutable upsert/remove for optimistic zone overlay updates. */
export function upsertZoneTile(
  zones: ZoneTile[],
  tileX: number,
  tileZ: number,
  zoneType: number,
): ZoneTile[] {
  const existing = zones.findIndex(
    (z) => z.tileX === tileX && z.tileZ === tileZ,
  );
  if (zoneType === 0) {
    if (existing === -1) return zones;
    return zones.filter((_, i) => i !== existing);
  }
  if (existing === -1) {
    return [...zones, { tileX, tileZ, zoneType }];
  }
  if (zones[existing]!.zoneType === zoneType) return zones;
  const next = zones.slice();
  next[existing] = { tileX, tileZ, zoneType };
  return next;
}

/** Paint a square brush centered on (centerX, centerZ). */
export function paintZoneBrush(
  zones: ZoneTile[],
  centerX: number,
  centerZ: number,
  zoneType: number,
  brushSize: PaintBrushSize,
): ZoneTile[] {
  const half = Math.floor(brushSize / 2);
  let next = zones;
  for (let dx = -half; dx <= half; dx++) {
    for (let dz = -half; dz <= half; dz++) {
      const tileX = centerX + dx;
      const tileZ = centerZ + dz;
      if (
        tileX < 0 ||
        tileZ < 0 ||
        tileX >= GRID_SIZE ||
        tileZ >= GRID_SIZE
      ) {
        continue;
      }
      next = upsertZoneTile(next, tileX, tileZ, zoneType);
    }
  }
  return next;
}

/** Immutable upsert/remove for optimistic road overlay updates. */
export function upsertRoadTile(
  roads: RoadTile[],
  tileX: number,
  tileZ: number,
  roadFlags: number,
): RoadTile[] {
  const existing = roads.findIndex(
    (r) => r.tileX === tileX && r.tileZ === tileZ,
  );
  if (roadFlags === 0) {
    if (existing === -1) return roads;
    return roads.filter((_, i) => i !== existing);
  }
  if (existing === -1) {
    return [...roads, { tileX, tileZ, roadFlags }];
  }
  if (roads[existing]!.roadFlags === roadFlags) return roads;
  const next = roads.slice();
  next[existing] = { tileX, tileZ, roadFlags };
  return next;
}

/** Paint a square road brush centered on (centerX, centerZ). */
export function paintRoadBrush(
  roads: RoadTile[],
  centerX: number,
  centerZ: number,
  roadFlags: number,
  brushSize: PaintBrushSize,
): RoadTile[] {
  const half = Math.floor(brushSize / 2);
  let next = roads;
  for (let dx = -half; dx <= half; dx++) {
    for (let dz = -half; dz <= half; dz++) {
      const tileX = centerX + dx;
      const tileZ = centerZ + dz;
      if (
        tileX < 0 ||
        tileZ < 0 ||
        tileX >= GRID_SIZE ||
        tileZ >= GRID_SIZE
      ) {
        continue;
      }
      next = upsertRoadTile(next, tileX, tileZ, roadFlags);
    }
  }
  return next;
}

/** Tile offsets covered by a square brush (for multi-command WASM paint). */
/** Zone / road label for a grid tile (for hover tooltips). */
export function tileZoneLabel(
  tileX: number,
  tileZ: number,
  zones: ZoneTile[],
  roads: RoadTile[],
): string {
  const zone = zones.find((z) => z.tileX === tileX && z.tileZ === tileZ);
  if (zone && zone.zoneType !== 0) {
    return engineZoneTypeLabel(zone.zoneType);
  }
  const hasRoad = roads.some(
    (r) => r.tileX === tileX && r.tileZ === tileZ && r.roadFlags !== 0,
  );
  if (hasRoad) return "Road";
  return "None";
}

export function brushTileOffsets(brushSize: PaintBrushSize): [number, number][] {
  const half = Math.floor(brushSize / 2);
  const offsets: [number, number][] = [];
  for (let dx = -half; dx <= half; dx++) {
    for (let dz = -half; dz <= half; dz++) {
      offsets.push([dx, dz]);
    }
  }
  return offsets;
}
