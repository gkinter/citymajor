import { GRID_SIZE } from "./constants";

/** Active zoning tool in the /play toolbar. */
export type ZoningTool =
  | "residential"
  | "commercial"
  | "industrial"
  | "bulldoze"
  | "road";

/** Supported paint brush diameters (tiles). */
export const PAINT_BRUSH_SIZES = [1, 3] as const;
export type PaintBrushSize = (typeof PAINT_BRUSH_SIZES)[number];

/**
 * All engine zone type IDs (TileData.cs).
 * 0=none, 1=res_low, 2=res_high, 3=commercial, 4=industrial,
 * 5=office, 6=mixed_use, 7=agricultural.
 */
export const ENGINE_ZONE_TYPES = {
  none: 0,
  residential_low: 1,
  residential_high: 2,
  commercial: 3,
  industrial: 4,
  office: 5,
  mixed_use: 6,
  agricultural: 7,
} as const;

export type EngineZoneTypeKey = Exclude<
  keyof typeof ENGINE_ZONE_TYPES,
  "none"
>;

export type EngineZoneTypeId =
  (typeof ENGINE_ZONE_TYPES)[EngineZoneTypeKey];

/** Legacy v1 toolbar tools → engine zone type ID. See zone-tiers.ts for all seven. */
export const ENGINE_ZONE_TYPE: Record<
  Exclude<ZoningTool, "bulldoze" | "road">,
  EngineZoneTypeId
> = {
  residential: ENGINE_ZONE_TYPES.residential_low,
  commercial: ENGINE_ZONE_TYPES.commercial,
  industrial: ENGINE_ZONE_TYPES.industrial,
};

/** Semi-transparent overlay tints keyed by engine zone type. */
export const ZONE_OVERLAY_COLORS: Record<number, string> = {
  [ENGINE_ZONE_TYPES.residential_low]: "#4a90d9",
  [ENGINE_ZONE_TYPES.residential_high]: "#3a6fb0",
  [ENGINE_ZONE_TYPES.commercial]: "#e8b84a",
  [ENGINE_ZONE_TYPES.industrial]: "#8b7355",
  [ENGINE_ZONE_TYPES.office]: "#9b7ed9",
  [ENGINE_ZONE_TYPES.mixed_use]: "#5cb88a",
  [ENGINE_ZONE_TYPES.agricultural]: "#8cb83c",
};

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

export const ZONING_TOOLS: {
  id: ZoningTool;
  label: string;
  shortLabel: string;
  stub?: boolean;
}[] = [
  { id: "residential", label: "Residential", shortLabel: "R" },
  { id: "commercial", label: "Commercial", shortLabel: "C" },
  { id: "industrial", label: "Industrial", shortLabel: "I" },
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
