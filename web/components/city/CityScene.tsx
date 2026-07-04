"use client";

import { OrbitControls } from "@react-three/drei";
import { useFrame, useThree } from "@react-three/fiber";
import { useMemo, useRef } from "react";
import * as THREE from "three";
import type { ChunkState, CityData, FpsStats, PickResult } from "@/lib/types";
import {
  updateChunkLod,
  updateChunkVisibility,
  visibleBuildingCount,
} from "@/lib/chunks";
import type { RoadTile, TrafficTile, ZoneTile } from "@/lib/zoning";
import type { ActiveEventSnapshot, EraProgress, ServiceCoverageSnapshot, ServiceViewMode } from "@/lib/sim-bridge";
import { BuildingInstances } from "./BuildingInstances";
import { CitizenDots } from "./CitizenDots";
import { TerrainChunks } from "./TerrainChunks";
import { TilePicker } from "./TilePicker";
import { RoadOverlay } from "./RoadOverlay";
import { ZoneOverlay } from "./ZoneOverlay";
import { TrafficOverlay } from "./TrafficOverlay";
import { ServiceCoverageOverlay } from "./ServiceCoverageOverlay";
import { EventMarkers } from "./EventMarkers";
import { EraLandmarkPlaceholder } from "./EraLandmarkPlaceholder";

type CitySceneProps = {
  city: CityData;
  chunks: ChunkState[];
  zones: ZoneTile[];
  roads: RoadTile[];
  traffic: TrafficTile[];
  showTrafficOverlay: boolean;
  serviceCoverage: ServiceCoverageSnapshot[];
  serviceViewMode: ServiceViewMode;
  pickedTile: PickResult;
  activeEvents?: ActiveEventSnapshot[];
  era?: number;
  eraProgress?: EraProgress;
  onEventMarkerClick?: (event: ActiveEventSnapshot) => void;
  onPick: (pick: PickResult) => void;
  onStats: (stats: FpsStats) => void;
  dpr: number;
  population?: number;
  householdCount?: number;
};

export function CityScene({
  city,
  chunks,
  zones,
  roads,
  traffic,
  showTrafficOverlay,
  serviceCoverage,
  serviceViewMode,
  pickedTile,
  activeEvents,
  era = 0,
  eraProgress,
  onEventMarkerClick,
  onPick,
  onStats,
  dpr,
  population = 0,
  householdCount,
}: CitySceneProps) {
  const { camera } = useThree();
  const fpsAccum = useRef({ frames: 0, last: performance.now(), fps: 0 });
  const statsSnapshot = useRef<Partial<FpsStats>>({});

  const dirLight = useMemo(() => new THREE.DirectionalLight("#fff5e6", 1.1), []);
  dirLight.position.set(80, 120, 40);

  useFrame(() => {
    const now = performance.now();
    fpsAccum.current.frames += 1;
    const elapsed = now - fpsAccum.current.last;
    if (elapsed >= 500) {
      fpsAccum.current.fps = (fpsAccum.current.frames * 1000) / elapsed;
      fpsAccum.current.frames = 0;
      fpsAccum.current.last = now;

      const visibleChunks = updateChunkVisibility(chunks, camera);
      const lodCounts = updateChunkLod(chunks, camera.position);
      const visibleBuildings = visibleBuildingCount(
        chunks,
        city.chunkBuildingIndices,
      );

      statsSnapshot.current = {
        fps: Math.round(fpsAccum.current.fps),
        dpr,
        visibleChunks,
        visibleBuildings,
        lodCounts,
        pickedTile,
      };
      onStats(statsSnapshot.current as FpsStats);
    }
  });

  return (
    <>
      <ambientLight intensity={0.35} />
      <primitive object={dirLight} />
      <hemisphereLight args={["#8ec5ff", "#1a2030", 0.4]} />
      <OrbitControls
        makeDefault
        enableDamping
        dampingFactor={0.08}
        maxPolarAngle={Math.PI / 2.1}
        minDistance={20}
        maxDistance={400}
        target={[GRID_CENTER, 0, GRID_CENTER]}
      />
      <TerrainChunks chunks={chunks} pickedTile={pickedTile} />
      <RoadOverlay roads={roads} />
      <TrafficOverlay traffic={traffic} visible={showTrafficOverlay} />
      {serviceViewMode === "off" ? (
        <ZoneOverlay zones={zones} />
      ) : (
        <ServiceCoverageOverlay tiles={serviceCoverage} mode={serviceViewMode} />
      )}
      <BuildingInstances city={city} chunks={chunks} />
      <CitizenDots
        city={city}
        chunks={chunks}
        population={population}
        householdCount={householdCount}
      />
      <EventMarkers
        activeEvents={activeEvents}
        onEventClick={onEventMarkerClick}
      />
      <EraLandmarkPlaceholder era={era} eraProgress={eraProgress} />
      <TilePicker onPick={onPick} />
    </>
  );
}

const GRID_CENTER = 128;
