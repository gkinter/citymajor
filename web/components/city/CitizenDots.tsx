"use client";

import { BuildingCategory, BuildingState } from "@citymajor/sim-types";
import { useFrame } from "@react-three/fiber";
import type { ThreeEvent } from "@react-three/fiber";
import { useLayoutEffect, useMemo, useRef } from "react";
import * as THREE from "three";
import {
  allocateDotsPerBuilding,
  resolveHouseholdBudget,
} from "@/lib/population-growth";
import type { CitizenDotPick } from "@/lib/population-l2";
import type { ChunkState, CityData } from "@/lib/types";

type CitizenDotsProps = {
  city: CityData;
  chunks: ChunkState[];
  /** City-wide household count from WASM snapshot. */
  householdCount?: number;
  population: number;
  onDotClick?: (pick: CitizenDotPick) => void;
};

type DotSlot = {
  buildingIndex: number;
  localX: number;
  localZ: number;
};

const MAX_DOTS = 12_000;
const DOT_RADIUS = 0.07;
const WARM_PALETTE = ["#ffe8b8", "#ffd080", "#ffb84d"];

const _matrix = new THREE.Matrix4();
const _position = new THREE.Vector3();
const _quaternion = new THREE.Quaternion();
const _scale = new THREE.Vector3(DOT_RADIUS, DOT_RADIUS, DOT_RADIUS);
const _color = new THREE.Color();

function isResidential(category: BuildingCategory): boolean {
  return (
    category === BuildingCategory.ResidentialLow ||
    category === BuildingCategory.ResidentialHigh
  );
}

/**
 * Warm citizen markers above residential buildings at LOD L2 — reads alive from
 * the render snapshot without per-building household IDs (SB-3689 / SB-3712).
 */
export function CitizenDots({
  city,
  chunks,
  householdCount = 0,
  population,
  onDotClick,
}: CitizenDotsProps) {
  const meshRef = useRef<THREE.InstancedMesh>(null);
  const slotsRef = useRef<DotSlot[]>([]);
  const timeRef = useRef(0);

  const residentialIndices = useMemo(() => {
    const indices: number[] = [];
    for (let i = 0; i < city.buildings.length; i++) {
      const b = city.buildings[i]!;
      if (!isResidential(b.category)) continue;
      if (b.state !== BuildingState.Operational) continue;
      indices.push(i);
    }
    return indices;
  }, [city.buildings]);

  const dotBudget = useMemo(
    () => resolveHouseholdBudget(householdCount, population),
    [householdCount, population],
  );

  const dotsPerBuilding = useMemo(
    () =>
      allocateDotsPerBuilding(
        residentialIndices,
        (buildingIndex) => city.buildings[buildingIndex]?.level ?? 1,
        dotBudget,
        MAX_DOTS,
      ),
    [city.buildings, dotBudget, residentialIndices],
  );

  useLayoutEffect(() => {
    const slots: DotSlot[] = [];
    for (let ri = 0; ri < residentialIndices.length; ri++) {
      const buildingIndex = residentialIndices[ri]!;
      const building = city.buildings[buildingIndex]!;
      const dotCount = dotsPerBuilding[ri] ?? 0;
      for (let j = 0; j < dotCount; j++) {
        const angle = (j / Math.max(1, dotCount)) * Math.PI * 2 + building.id * 0.17;
        const radius = 0.12 + (j % 2) * 0.06;
        slots.push({
          buildingIndex,
          localX: Math.cos(angle) * radius,
          localZ: Math.sin(angle) * radius,
        });
      }
    }
    slotsRef.current = slots;

    const mesh = meshRef.current;
    if (!mesh) return;
    mesh.count = slots.length;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [city.buildings, dotsPerBuilding, residentialIndices]);

  useFrame((_, delta) => {
    const mesh = meshRef.current;
    const slots = slotsRef.current;
    if (!mesh || slots.length === 0) return;

    timeRef.current += delta;
    const t = timeRef.current;

    for (let i = 0; i < slots.length; i++) {
      const slot = slots[i]!;
      const building = city.buildings[slot.buildingIndex]!;
      const chunk = chunks[building.chunkIndex];
      const show = chunk?.visible && chunk.lod === 2;

      if (!show) {
        _scale.set(0, 0, 0);
      } else {
        const bob = Math.sin(t * 2.4 + building.id * 0.31 + i * 0.2) * 0.04;
        const roofY = 0.35 + building.level * 0.12 + bob;
        _position.set(
          building.tileX + 0.5 + slot.localX,
          roofY,
          building.tileZ + 0.5 + slot.localZ,
        );
        _scale.set(DOT_RADIUS, DOT_RADIUS, DOT_RADIUS);
      }

      _quaternion.identity();
      _matrix.compose(_position, _quaternion, _scale);
      mesh.setMatrixAt(i, _matrix);

      if (!mesh.instanceColor) continue;
      const palette = WARM_PALETTE[(building.id + i) % WARM_PALETTE.length]!;
      _color.set(palette);
      mesh.setColorAt(i, _color);
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  });

  const handleClick = (event: ThreeEvent<MouseEvent>) => {
    if (!onDotClick) return;
    const instanceId = event.instanceId;
    if (instanceId === undefined || instanceId < 0) return;
    event.stopPropagation();
    const slot = slotsRef.current[instanceId];
    if (!slot) return;
    const building = city.buildings[slot.buildingIndex];
    if (!building) return;
    onDotClick({
      instanceId,
      buildingIndex: slot.buildingIndex,
      tileX: building.tileX,
      tileZ: building.tileZ,
    });
  };

  if (residentialIndices.length === 0 || dotBudget <= 0) {
    return null;
  }

  return (
    <instancedMesh
      ref={meshRef}
      args={[undefined, undefined, MAX_DOTS]}
      frustumCulled={false}
      onClick={onDotClick ? handleClick : undefined}
    >
      <sphereGeometry args={[1, 6, 6]} />
      <meshBasicMaterial vertexColors transparent opacity={0.92} depthWrite={false} />
    </instancedMesh>
  );
}
