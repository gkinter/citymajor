/** Active zoning tool in the /play toolbar. */
export type ZoningTool =
  | "residential"
  | "commercial"
  | "industrial"
  | "bulldoze"
  | "road";

/** Engine zone type IDs (TileData.cs). */
export const ENGINE_ZONE_TYPE: Record<
  Exclude<ZoningTool, "bulldoze" | "road">,
  number
> = {
  residential: 1, // residential_low
  commercial: 3,
  industrial: 4,
};

/** Semi-transparent overlay tints keyed by engine zone type. */
export const ZONE_OVERLAY_COLORS: Record<number, string> = {
  1: "#4a90d9",
  2: "#4a90d9",
  3: "#e8b84a",
  4: "#8b7355",
  5: "#9b7ed9",
  6: "#5cb88a",
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

export const ZONING_TOOLS: {
  id: ZoningTool;
  label: string;
  stub?: boolean;
}[] = [
  { id: "residential", label: "Residential" },
  { id: "commercial", label: "Commercial" },
  { id: "industrial", label: "Industrial" },
  { id: "bulldoze", label: "Bulldoze" },
  { id: "road", label: "Road" },
];
