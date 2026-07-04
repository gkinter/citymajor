"use client";

import { useGLTF } from "@react-three/drei";
import { useFrame } from "@react-three/fiber";
import { useMemo, useRef } from "react";
import * as THREE from "three";
import { resolveCatalogKey, resolveGltfPath } from "@/lib/gltf-catalog";
import {
  scaleVisualForSpawn,
  useBuildingSpawnScales,
} from "@/lib/building-spawn";
import {
  composeGltfInstanceMatrix,
  lodVisualForBuilding,
  metalnessForCategory,
  tileYawRadians,
  type GltfFootprint,
} from "@/lib/lod";
import type { BuildingInstance, ChunkState } from "@/lib/types";

type GltfBuildingBucketProps = {
  catalogKey: string;
  buildings: BuildingInstance[];
  chunks: ChunkState[];
  dayNightFactor?: number;
};

function meshFootprint(root: THREE.Object3D): {
  geometry: THREE.BufferGeometry;
  footprint: GltfFootprint;
} {
  let geometry: THREE.BufferGeometry | null = null;
  root.traverse((child) => {
    if (!geometry && child instanceof THREE.Mesh) {
      geometry = child.geometry.clone();
    }
  });

  const geom = geometry ?? new THREE.BoxGeometry(1, 1, 1);
  geom.computeBoundingBox();
  const box = geom.boundingBox ?? new THREE.Box3();
  const size = new THREE.Vector3();
  box.getSize(size);

  return {
    geometry: geom,
    footprint: {
      width: size.x,
      height: size.y,
      depth: size.z,
      minY: box.min.y,
    },
  };
}

/**
 * Instanced GLTF bucket for one shipped catalog key at LOD L0.
 * Multiple sim archetype variants can share the same GLB module.
 */
export function GltfBuildingBucket({
  catalogKey,
  buildings,
  chunks,
  dayNightFactor = 0,
}: GltfBuildingBucketProps) {
  const path = resolveGltfPath(catalogKey)!;

  const { scene } = useGLTF(path);
  const meshRef = useRef<THREE.InstancedMesh | null>(null);
  const colorsRef = useRef<THREE.Color[]>([]);
  const getSpawnScale = useBuildingSpawnScales(buildings);

  const { geometry, footprint } = useMemo(() => meshFootprint(scene), [scene]);
  const category = buildings[0]?.category;

  useFrame(() => {
    const mesh = meshRef.current;
    if (!mesh || !buildings.length) return;

    for (let i = 0; i < buildings.length; i++) {
      const building = buildings[i];
      const chunk = chunks[building.chunkIndex];
      const hidden = !chunk?.visible;
      const lod = chunk?.visible ? chunk.lod : 3;
      const visual = lodVisualForBuilding(building, 0, dayNightFactor);
      const spawnScale = getSpawnScale(building.id);
      const targetScale =
        spawnScale < 0.999
          ? scaleVisualForSpawn(visual.scale, spawnScale)
          : visual.scale;
      const rotationY = tileYawRadians(
        building.tileX,
        building.tileZ,
        building.typeId,
      );

      const showGltf = !hidden && lod === 0 && !visual.wireframe;

      mesh.setMatrixAt(
        i,
        composeGltfInstanceMatrix(
          building.tileX,
          building.tileZ,
          targetScale,
          footprint,
          rotationY,
          !showGltf,
        ),
      );

      if (!colorsRef.current[i]) colorsRef.current[i] = new THREE.Color();
      colorsRef.current[i].set(visual.color);
      mesh.setColorAt(i, colorsRef.current[i]);
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  });

  if (!buildings.length) return null;

  return (
    <instancedMesh
      ref={meshRef}
      args={[geometry, undefined, buildings.length]}
      frustumCulled={false}
      castShadow
      receiveShadow
    >
      <meshStandardMaterial
        vertexColors
        roughness={0.65}
        metalness={category ? metalnessForCategory(category) : 0.05}
      />
    </instancedMesh>
  );
}

/** Group buildings by shipped catalog key for GLTF instancing. */
export function groupBuildingsByCatalogKey(
  buildings: BuildingInstance[],
): { catalogKey: string; buildings: BuildingInstance[] }[] {
  const buckets = new Map<string, BuildingInstance[]>();

  for (const building of buildings) {
    const catalogKey = resolveCatalogKey(building.archetypeKey);
    if (!catalogKey) continue;
    const list = buckets.get(catalogKey);
    if (list) list.push(building);
    else buckets.set(catalogKey, [building]);
  }

  return [...buckets.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([catalogKey, grouped]) => ({ catalogKey, buildings: grouped }));
}
