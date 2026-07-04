import * as THREE from "three";
import { CHUNK_COUNT, CHUNK_SIZE, CHUNKS_PER_AXIS } from "./constants";
import { chunkCenter } from "./city-data";
import { LOD_THRESHOLDS } from "./constants";
import type { ChunkState } from "./types";

export function createChunkStates(): ChunkState[] {
  return Array.from({ length: CHUNK_COUNT }, (_, index) => {
    const chunkX = index % CHUNKS_PER_AXIS;
    const chunkZ = Math.floor(index / CHUNKS_PER_AXIS);
    return { index, chunkX, chunkZ, visible: true, lod: 0 };
  });
}

const _frustum = new THREE.Frustum();
const _projScreenMatrix = new THREE.Matrix4();
const _box = new THREE.Box3();
const _center = new THREE.Vector3();

/**
 * Chunk visibility via frustum vs axis-aligned chunk bounds on XZ plane.
 */
export function updateChunkVisibility(
  chunks: ChunkState[],
  camera: THREE.Camera,
): number {
  _projScreenMatrix.multiplyMatrices(
    camera.projectionMatrix,
    camera.matrixWorldInverse,
  );
  _frustum.setFromProjectionMatrix(_projScreenMatrix);

  let visible = 0;
  for (const chunk of chunks) {
    const originX = chunk.chunkX * CHUNK_SIZE;
    const originZ = chunk.chunkZ * CHUNK_SIZE;
    _box.min.set(originX, -2, originZ);
    _box.max.set(originX + CHUNK_SIZE, 80, originZ + CHUNK_SIZE);
    chunk.visible = _frustum.intersectsBox(_box);
    if (chunk.visible) visible += 1;
  }
  return visible;
}

/**
 * LOD with hysteresis per chunk based on camera distance to chunk center.
 */
export function updateChunkLod(
  chunks: ChunkState[],
  cameraPosition: THREE.Vector3,
): [number, number, number, number] {
  const counts: [number, number, number, number] = [0, 0, 0, 0];

  for (const chunk of chunks) {
    if (!chunk.visible) continue;

    const center = chunkCenter(chunk.index);
    const dx = cameraPosition.x - center.x;
    const dz = cameraPosition.z - center.z;
    const dist = Math.sqrt(dx * dx + dz * dz);
    const prev = chunk.lod;

    let next = prev;
    if (prev === 0) {
      if (dist > LOD_THRESHOLDS.enter[0]) next = 1;
    } else if (prev === 1) {
      if (dist < LOD_THRESHOLDS.exit[0]) next = 0;
      else if (dist > LOD_THRESHOLDS.enter[1]) next = 2;
    } else if (prev === 2) {
      if (dist < LOD_THRESHOLDS.exit[1]) next = 1;
      else if (dist > LOD_THRESHOLDS.enter[2]) next = 3;
    } else {
      if (dist < LOD_THRESHOLDS.exit[2]) next = 2;
    }

    chunk.lod = next;
    counts[next] += 1;
  }

  return counts;
}

export function visibleBuildingCount(
  chunks: ChunkState[],
  chunkBuildingIndices: number[][],
): number {
  let count = 0;
  for (const chunk of chunks) {
    if (chunk.visible) count += chunkBuildingIndices[chunk.index].length;
  }
  return count;
}
