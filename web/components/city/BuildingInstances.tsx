"use client";

import { BuildingCategory } from "@citymajor/sim-types";
import { useFrame } from "@react-three/fiber";
import { useMemo, useRef } from "react";
import * as THREE from "three";
import type { ChunkState, CityData } from "@/lib/types";
import {
  composeInstanceMatrix,
  lodVisualForBuilding,
  metalnessForCategory,
} from "@/lib/lod";

type BuildingInstancesProps = {
  city: CityData;
  chunks: ChunkState[];
};

/**
 * One InstancedMesh per archetype key (sim-types taxonomy).
 * Draw-call count scales with unique keys in the procedural city (~100–200).
 */
export function BuildingInstances({ city, chunks }: BuildingInstancesProps) {
  const meshRefs = useRef<Record<string, THREE.InstancedMesh | null>>({});
  const colorsRef = useRef<THREE.Color[]>([]);

  const buckets = useMemo(
    () =>
      city.archetypeKeys.map((key) => ({
        key,
        buildings: city.buildingsByArchetypeKey[key] ?? [],
        category: city.buildingsByArchetypeKey[key]?.[0]?.category ?? BuildingCategory.ResidentialLow,
      })),
    [city],
  );

  useFrame(() => {
    for (const { key, buildings } of buckets) {
      const mesh = meshRefs.current[key];
      if (!mesh || !buildings.length) continue;

      for (let i = 0; i < buildings.length; i++) {
        const building = buildings[i];
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
      {buckets.map(({ key, buildings, category }) =>
        buildings.length > 0 ? (
          <instancedMesh
            key={key}
            ref={(el) => {
              meshRefs.current[key] = el;
            }}
            args={[undefined, undefined, buildings.length]}
            frustumCulled={false}
            castShadow
            receiveShadow
          >
            <boxGeometry args={[1, 1, 1]} />
            <meshStandardMaterial
              vertexColors
              roughness={0.65}
              metalness={metalnessForCategory(category)}
            />
          </instancedMesh>
        ) : null,
      )}
    </>
  );
}
