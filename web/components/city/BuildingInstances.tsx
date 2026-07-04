"use client";

import { useFrame } from "@react-three/fiber";
import { useLayoutEffect, useMemo, useRef } from "react";
import * as THREE from "three";
import type { ChunkState, CityData } from "@/lib/types";
import { ARCHETYPE_COUNT } from "@/lib/constants";
import {
  archetypeBaseColor,
  composeInstanceMatrix,
  lodVisualForBuilding,
} from "@/lib/lod";

type BuildingInstancesProps = {
  city: CityData;
  chunks: ChunkState[];
};

/**
 * One InstancedMesh per building archetype (5 draw calls total).
 * Instance matrices updated per-frame for chunk culling + per-chunk LOD.
 */
export function BuildingInstances({ city, chunks }: BuildingInstancesProps) {
  const meshRefs = useRef<(THREE.InstancedMesh | null)[]>(
    Array.from({ length: ARCHETYPE_COUNT }, () => null),
  );
  const colorsRef = useRef<THREE.Color[]>([]);

  const counts = useMemo(
    () => city.buildingsByArchetype.map((list) => list.length),
    [city],
  );

  useLayoutEffect(() => {
    meshRefs.current.forEach((mesh, archetype) => {
      if (!mesh) return;
      const color = new THREE.Color(archetypeBaseColor(archetype));
      for (let i = 0; i < counts[archetype]; i++) {
        mesh.setColorAt(i, color);
      }
      if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
    });
  }, [counts]);

  useFrame(() => {
    for (let archetype = 0; archetype < ARCHETYPE_COUNT; archetype++) {
      const mesh = meshRefs.current[archetype];
      const list = city.buildingsByArchetype[archetype];
      if (!mesh || !list.length) continue;

      for (let i = 0; i < list.length; i++) {
        const building = list[i];
        const chunk = chunks[building.chunkIndex];
        const hidden = !chunk?.visible;
        const lod = chunk?.visible ? chunk.lod : 0;
        const visual = lodVisualForBuilding(building, lod);
        mesh.setMatrixAt(
          i,
          composeInstanceMatrix(building.tileX, building.tileZ, visual, hidden),
        );
        if (!colorsRef.current[i]) colorsRef.current[i] = new THREE.Color();
        const c = colorsRef.current[i];
        c.set(visual.color);
        mesh.setColorAt(i, c);
      }

      mesh.instanceMatrix.needsUpdate = true;
      if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
    }
  });

  return (
    <>
      {counts.map((count, archetype) =>
        count > 0 ? (
          <instancedMesh
            key={archetype}
            ref={(el) => {
              meshRefs.current[archetype] = el;
            }}
            args={[undefined, undefined, count]}
            frustumCulled={false}
            castShadow
            receiveShadow
          >
            <boxGeometry args={[1, 1, 1]} />
            <meshStandardMaterial
              vertexColors
              roughness={0.65}
              metalness={archetype >= 3 ? 0.15 : 0.05}
            />
          </instancedMesh>
        ) : null,
      )}
    </>
  );
}
