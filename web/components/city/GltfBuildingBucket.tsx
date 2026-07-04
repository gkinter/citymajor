"use client";

import { useGLTF } from "@react-three/drei";
import { useFrame } from "@react-three/fiber";
import { useMemo, useRef } from "react";
import * as THREE from "three";
import { resolveGltfPath } from "@/lib/gltf-catalog";
import {
  composeInstanceMatrix,
  lodVisualForBuilding,
  metalnessForCategory,
} from "@/lib/lod";
import type { BuildingInstance, ChunkState } from "@/lib/types";

type GltfBuildingBucketProps = {
  archetypeKey: string;
  buildings: BuildingInstance[];
  chunks: ChunkState[];
  dayNightFactor?: number;
};

function firstMeshGeometry(root: THREE.Object3D): THREE.BufferGeometry {
  let found: THREE.BufferGeometry | null = null;
  root.traverse((child) => {
    if (!found && child instanceof THREE.Mesh) {
      found = child.geometry.clone();
    }
  });
  return found ?? new THREE.BoxGeometry(1, 1, 1);
}

/**
 * Instanced GLTF bucket for one archetype at LOD L0.
 * Geometry is cloned from the loaded GLTF scene for InstancedMesh batching.
 */
export function GltfBuildingBucket({
  archetypeKey,
  buildings,
  chunks,
  dayNightFactor = 0,
}: GltfBuildingBucketProps) {
  const path = resolveGltfPath(archetypeKey)!;

  const { scene } = useGLTF(path);
  const meshRef = useRef<THREE.InstancedMesh | null>(null);
  const colorsRef = useRef<THREE.Color[]>([]);

  const geometry = useMemo(() => firstMeshGeometry(scene), [scene]);
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

      const showGltf = !hidden && lod === 0 && !visual.wireframe;

      mesh.setMatrixAt(
        i,
        composeInstanceMatrix(
          building.tileX,
          building.tileZ,
          showGltf
            ? visual
            : { scale: [0, 0, 0], color: visual.color, opacity: 0 },
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
