import type { ArchetypeId, ZoneType } from "./constants";

export type BuildingInstance = {
  id: number;
  tileX: number;
  tileZ: number;
  archetype: ArchetypeId;
  zone: ZoneType;
  stories: number;
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
  buildingsByArchetype: BuildingInstance[][];
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
  lodCounts: [number, number, number, number];
  pickedTile: PickResult;
};
