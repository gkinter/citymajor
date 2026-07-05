"use client";

import { useThree } from "@react-three/fiber";
import { useEffect } from "react";
import * as THREE from "three";
import type { PickResult } from "@/lib/types";
import { CHUNK_SIZE, CHUNKS_PER_AXIS, GRID_SIZE } from "@/lib/constants";

type TilePickerProps = {
  onPick: (pick: PickResult) => void;
  onHover?: (pick: PickResult, pointer: { clientX: number; clientY: number }) => void;
  onHoverEnd?: () => void;
};

const _raycaster = new THREE.Raycaster();
const _pointer = new THREE.Vector2();
const _plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
const _hit = new THREE.Vector3();

function pickTileFromPointer(
  event: PointerEvent,
  camera: THREE.Camera,
  canvas: HTMLCanvasElement,
): PickResult {
  const rect = canvas.getBoundingClientRect();
  _pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  _pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

  _raycaster.setFromCamera(_pointer, camera);
  if (!_raycaster.ray.intersectPlane(_plane, _hit)) {
    return null;
  }

  const tileX = Math.floor(_hit.x);
  const tileZ = Math.floor(_hit.z);
  if (tileX < 0 || tileZ < 0 || tileX >= GRID_SIZE || tileZ >= GRID_SIZE) {
    return null;
  }

  const chunkX = Math.floor(tileX / CHUNK_SIZE);
  const chunkZ = Math.floor(tileZ / CHUNK_SIZE);
  const chunkIndex = chunkZ * CHUNKS_PER_AXIS + chunkX;

  return { tileX, tileZ, chunkIndex };
}

/**
 * Tile raycast picking against ground plane (XZ), clamped to 256×256 grid.
 */
export function TilePicker({ onPick, onHover, onHoverEnd }: TilePickerProps) {
  const { camera, gl } = useThree();

  useEffect(() => {
    const canvas = gl.domElement;

    const handlePointerDown = (event: PointerEvent) => {
      onPick(pickTileFromPointer(event, camera, canvas));
    };

    const handlePointerMove = (event: PointerEvent) => {
      if (!onHover) return;
      const pick = pickTileFromPointer(event, camera, canvas);
      if (!pick) {
        onHoverEnd?.();
        return;
      }
      onHover(pick, { clientX: event.clientX, clientY: event.clientY });
    };

    const handlePointerLeave = () => {
      onHoverEnd?.();
    };

    canvas.addEventListener("pointerdown", handlePointerDown);
    if (onHover) {
      canvas.addEventListener("pointermove", handlePointerMove);
      canvas.addEventListener("pointerleave", handlePointerLeave);
    }
    return () => {
      canvas.removeEventListener("pointerdown", handlePointerDown);
      canvas.removeEventListener("pointermove", handlePointerMove);
      canvas.removeEventListener("pointerleave", handlePointerLeave);
    };
  }, [camera, gl.domElement, onPick, onHover, onHoverEnd]);

  return null;
}
