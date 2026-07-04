"use client";

import { useThree } from "@react-three/fiber";
import { useEffect } from "react";
import * as THREE from "three";
import type { PickResult } from "@/lib/types";
import { CHUNK_SIZE, CHUNKS_PER_AXIS, GRID_SIZE } from "@/lib/constants";

type TilePickerProps = {
  onPick: (pick: PickResult) => void;
};

const _raycaster = new THREE.Raycaster();
const _pointer = new THREE.Vector2();
const _plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
const _hit = new THREE.Vector3();

/**
 * Tile raycast picking against ground plane (XZ), clamped to 256×256 grid.
 */
export function TilePicker({ onPick }: TilePickerProps) {
  const { camera, gl } = useThree();

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      const rect = gl.domElement.getBoundingClientRect();
      _pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
      _pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

      _raycaster.setFromCamera(_pointer, camera);
      if (!_raycaster.ray.intersectPlane(_plane, _hit)) {
        onPick(null);
        return;
      }

      const tileX = Math.floor(_hit.x);
      const tileZ = Math.floor(_hit.z);
      if (
        tileX < 0 ||
        tileZ < 0 ||
        tileX >= GRID_SIZE ||
        tileZ >= GRID_SIZE
      ) {
        onPick(null);
        return;
      }

      const chunkX = Math.floor(tileX / CHUNK_SIZE);
      const chunkZ = Math.floor(tileZ / CHUNK_SIZE);
      const chunkIndex = chunkZ * CHUNKS_PER_AXIS + chunkX;

      onPick({ tileX, tileZ, chunkIndex });
    };

    gl.domElement.addEventListener("pointerdown", handlePointerDown);
    return () => gl.domElement.removeEventListener("pointerdown", handlePointerDown);
  }, [camera, gl.domElement, onPick]);

  return null;
}
