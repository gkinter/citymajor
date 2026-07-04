"use client";

import { BuildingCategory } from "@citymajor/sim-types";
import { useFrame } from "@react-three/fiber";
import { Suspense, useMemo, useRef } from "react";
import * as THREE from "three";
import { hasGltfAsset } from "@/lib/gltf-catalog";
import type { ChunkState, CityData } from "@/lib/types";
import {
  composeInstanceMatrix,
  lodVisualForBuilding,
  metalnessForCategory,
  tileYawRadians,
} from "@/lib/lod";
import {
  GltfBuildingBucket,
  groupBuildingsByCatalogKey,
} from "./GltfBuildingBucket";

type BuildingInstancesProps = {
  city: CityData;
  chunks: ChunkState[];
  /** 0 = day, 1 = night — drives window glow threshold in buildingVisualParams. */
  dayNightFactor?: number;
};

/**
 * One InstancedMesh per archetype key (sim-types taxonomy).
 * At LOD L0, catalog GLTF assets replace boxes via GltfBuildingBucket
 * (grouped by shipped catalog key with era/category fallback).
 */
export function BuildingInstances({
  city,
  chunks,
  dayNightFactor = 0,
}: BuildingInstancesProps) {
  const meshRefs = useRef<Record<string, THREE.InstancedMesh | null>>({});
  const colorsRef = useRef<THREE.Color[]>([]);

  const buckets = useMemo(
    () =>
      city.archetypeKeys.map((key) => ({
        key,
        buildings: city.buildingsByArchetypeKey[key] ?? [],
        category: city.buildingsByArchetypeKey[key]?.[0]?.category ?? BuildingCategory.ResidentialLow,
        useGltf: hasGltfAsset(key),
      })),
    [city],
  );

  const gltfBuckets = useMemo(
    () => groupBuildingsByCatalogKey(city.buildings),
    [city.buildings],
  );

  useFrame(() => {
    for (const { key, buildings, useGltf } of buckets) {
      const mesh = meshRefs.current[key];
      if (!mesh || !buildings.length) continue;

      let bucketWireframe = false;

      for (let i = 0; i < buildings.length; i++) {
        const building = buildings[i];
        const chunk = chunks[building.chunkIndex];
        const hidden = !chunk?.visible;
        const lod = chunk?.visible ? chunk.lod : 0;
        const visual = lodVisualForBuilding(building, lod, dayNightFactor);
        if (visual.wireframe) bucketWireframe = true;

        const gltfCoversL0 =
          useGltf && !hidden && lod === 0 && !visual.wireframe;
        const showBox = !gltfCoversL0;
        const rotationY = tileYawRadians(
          building.tileX,
          building.tileZ,
          building.typeId,
        );

        mesh.setMatrixAt(
          i,
          composeInstanceMatrix(
            building.tileX,
            building.tileZ,
            showBox ? visual : { scale: [0, 0, 0], color: visual.color, opacity: 0 },
            hidden || !showBox,
            showBox ? rotationY : 0,
          ),
        );
        if (!colorsRef.current[i]) colorsRef.current[i] = new THREE.Color();
        const c = colorsRef.current[i];
        c.set(visual.color);
        mesh.setColorAt(i, c);
      }

      const material = mesh.material;
      if (material instanceof THREE.MeshStandardMaterial) {
        material.wireframe = bucketWireframe;
      }

      mesh.instanceMatrix.needsUpdate = true;
      if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
    }
  });

  return (
    <>
      {gltfBuckets.map(({ catalogKey, buildings }) => (
        <Suspense key={catalogKey} fallback={null}>
          <GltfBuildingBucket
            catalogKey={catalogKey}
            buildings={buildings}
            chunks={chunks}
            dayNightFactor={dayNightFactor}
          />
        </Suspense>
      ))}
      {buckets.map(({ key, buildings, category }) => (
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
        ) : null
      ))}
    </>
  );
}
