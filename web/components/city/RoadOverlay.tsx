"use client";

import { useLayoutEffect, useRef } from "react";
import * as THREE from "three";
import type { RoadTile } from "@/lib/zoning";
import { ROAD_OVERLAY_COLOR } from "@/lib/zoning";

type RoadOverlayProps = {
  roads: RoadTile[];
};

const _color = new THREE.Color();
const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _quaternion = new THREE.Quaternion();
const _scale = new THREE.Vector3(0.94, 0.94, 1);
const _euler = new THREE.Euler(-Math.PI / 2, 0, 0);

const MAX_INSTANCES = 65536;

/** Dark asphalt tint planes slightly above terrain for placed road tiles. */
export function RoadOverlay({ roads }: RoadOverlayProps) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  useLayoutEffect(() => {
    const mesh = meshRef.current;
    if (!mesh) return;

    roads.forEach((tile, i) => {
      _position.set(tile.tileX + 0.5, 0.04, tile.tileZ + 0.5);
      _quaternion.setFromEuler(_euler);
      _matrix.compose(_position, _quaternion, _scale);
      mesh.setMatrixAt(i, _matrix);
      mesh.setColorAt(i, _color.set(ROAD_OVERLAY_COLOR));
    });

    mesh.count = roads.length;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [roads]);

  if (roads.length === 0) return null;

  return (
    <instancedMesh
      ref={meshRef}
      args={[undefined, undefined, Math.min(roads.length, MAX_INSTANCES)]}
      frustumCulled={false}
    >
      <planeGeometry args={[1, 1]} />
      <meshBasicMaterial
        transparent
        opacity={0.72}
        depthWrite={false}
        vertexColors
      />
    </instancedMesh>
  );
}
