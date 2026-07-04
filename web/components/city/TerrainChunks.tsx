"use client";

import { CHUNK_SIZE } from "@/lib/constants";
import type { ChunkState, PickResult } from "@/lib/types";
import { useMemo } from "react";

type TerrainChunksProps = {
  chunks: ChunkState[];
  pickedTile: PickResult;
};

/**
 * Flat terrain plane per 32×32 chunk (64 total). Frustum-culled via visibility flag.
 */
export function TerrainChunks({ chunks, pickedTile }: TerrainChunksProps) {
  const pickHighlight = useMemo(() => {
    if (!pickedTile) return null;
    return {
      x: pickedTile.tileX + 0.5,
      z: pickedTile.tileZ + 0.5,
    };
  }, [pickedTile]);

  return (
    <group>
      {chunks.map((chunk) => {
        if (!chunk.visible) return null;
        const ox = chunk.chunkX * CHUNK_SIZE;
        const oz = chunk.chunkZ * CHUNK_SIZE;
        return (
          <mesh
            key={chunk.index}
            position={[ox + CHUNK_SIZE / 2, 0, oz + CHUNK_SIZE / 2]}
            rotation={[-Math.PI / 2, 0, 0]}
            receiveShadow
            frustumCulled
            userData={{ chunkIndex: chunk.index, terrain: true }}
          >
            <planeGeometry args={[CHUNK_SIZE, CHUNK_SIZE]} />
            <meshStandardMaterial
              color={chunk.index % 2 === 0 ? "#1a2438" : "#162032"}
              roughness={0.95}
              metalness={0}
            />
          </mesh>
        );
      })}
      {pickHighlight ? (
        <mesh
          position={[pickHighlight.x, 0.02, pickHighlight.z]}
          rotation={[-Math.PI / 2, 0, 0]}
        >
          <planeGeometry args={[0.92, 0.92]} />
          <meshBasicMaterial color="#5ec8ff" transparent opacity={0.55} />
        </mesh>
      ) : null}
      <gridHelper
        args={[256, 64, "#2a3a55", "#1e2a40"]}
        position={[128, 0.01, 128]}
      />
    </group>
  );
}
