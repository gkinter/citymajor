import type { BuildingCategory, BuildingState, Era } from "@citymajor/sim-types";
import type { ZoneType } from "./constants";

export type BuildingInstance = {
  id: number;
  tileX: number;
  tileZ: number;
  typeId: number;
  archetypeKey: string;
  category: BuildingCategory;
  era: Era;
  zone: ZoneType;
  /** Sim building level (drives stories via sim-types). */
  level: number;
  stories: number;
  /** Sim condition 0–255 (construction progress + upkeep). */
  condition: number;
  state: BuildingState;
  chunkIndex: number;
  heat: number;
};

export type ChunkState = {
  index: number;
  chunkX: number;
  chunkZ: number;
  visible: boolean;
  lod: 0 | 1 | 2 | 3;
};

export type CityData = {
  buildings: BuildingInstance[];
  /** InstancedMesh buckets keyed by archetype key (e.g. res_low_frontier_05). */
  buildingsByArchetypeKey: Record<string, BuildingInstance[]>;
  /** Stable iteration order for draw-call batching. */
  archetypeKeys: string[];
  chunkBuildingIndices: number[][];
};

export type PickResult = {
  tileX: number;
  tileZ: number;
  chunkIndex: number;
} | null;

export type FpsStats = {
  fps: number;
  dpr: number;
  visibleChunks: number;
  visibleBuildings: number;
  totalBuildings: number;
  lodCounts: [number, number, number, number];
  pickedTile: PickResult;
  simSource?: "wasm" | "procedural";
  /** 0–1 healthcare coverage heuristic from city buildings (M0). */
  healthcareCoverage?: number;
};
