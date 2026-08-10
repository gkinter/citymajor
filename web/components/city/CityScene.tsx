"use client";

import { OrbitControls } from "@react-three/drei";
import { useFrame, useThree } from "@react-three/fiber";
import { useCallback, useEffect, useMemo, useRef } from "react";
import * as THREE from "three";
import type { ChunkState, CityData, FpsStats, PickResult } from "@/lib/types";
import {
  updateChunkLod,
  updateChunkVisibility,
  visibleBuildingCount,
} from "@/lib/chunks";
import { getLoadedChunkCount } from "@/lib/chunk-load-state";
import type { RoadTile, TrafficTile, FrictionCorridorTile, ZoneTile } from "@/lib/zoning";
import type { CitizenDotPick } from "@/lib/population-l2";
import type { ActiveEventSnapshot, EraProgress, ServiceCoverageSnapshot, ServiceViewMode } from "@/lib/sim-bridge";
import { BuildingInstances } from "./BuildingInstances";
import { CitizenDots } from "./CitizenDots";
import { TerrainChunks } from "./TerrainChunks";
import { TilePicker } from "./TilePicker";
import { RoadOverlay } from "./RoadOverlay";
import { ZoneOverlay } from "./ZoneOverlay";
import { TrafficOverlay } from "./TrafficOverlay";
import { FrictionOverlay } from "./FrictionOverlay";
import { ServiceCoverageOverlay } from "./ServiceCoverageOverlay";
import { EventMarkers } from "./EventMarkers";
import { EraLandmarkPlaceholder } from "./EraLandmarkPlaceholder";
import { hasHeroGltfKey } from "@/lib/gltf-catalog";
import { FrontierChurchLandmark } from "./FrontierChurchLandmark";
import { FrontierCityHallLandmark } from "./FrontierCityHallLandmark";

type CitySceneProps = {
  city: CityData;
  chunks: ChunkState[];
  zones: ZoneTile[];
  roads: RoadTile[];
  traffic: TrafficTile[];
  showTrafficOverlay: boolean;
  frictionCorridors: FrictionCorridorTile[];
  showFrictionOverlay: boolean;
  serviceCoverage: ServiceCoverageSnapshot[];
  serviceViewMode: ServiceViewMode;
  pickedTile: PickResult;
  activeEvents?: ActiveEventSnapshot[];
  era?: number;
  eraProgress?: EraProgress;
  onEventMarkerClick?: (event: ActiveEventSnapshot) => void;
  onPick: (pick: PickResult) => void;
  onHover?: (pick: PickResult, pointer: { clientX: number; clientY: number }) => void;
  onHoverEnd?: () => void;
  onStats: (stats: FpsStats) => void;
  onRenderHealth?: (
    stats: Pick<FpsStats, "visibleBuildings" | "visibleChunks" | "totalBuildings">,
  ) => void;
  dpr: number;
  population?: number;
  householdCount?: number;
  onCitizenDotClick?: (pick: CitizenDotPick) => void;
  onRegisterCameraReset?: (reset: (() => void) | null) => void;
};

export function CityScene({
  city,
  chunks,
  zones,
  roads,
  traffic,
  showTrafficOverlay,
  frictionCorridors,
  showFrictionOverlay,
  serviceCoverage,
  serviceViewMode,
  pickedTile,
  activeEvents,
  era = 0,
  eraProgress,
  onEventMarkerClick,
  onPick,
  onHover,
  onHoverEnd,
  onStats,
  onRenderHealth,
  dpr,
  population = 0,
  householdCount,
  onCitizenDotClick,
  onRegisterCameraReset,
}: CitySceneProps) {
  const { camera, controls } = useThree();
  const fpsAccum = useRef({ frames: 0, last: performance.now(), fps: 0 });
  const statsSnapshot = useRef<Partial<FpsStats>>({});

  const dirLight = useMemo(() => new THREE.DirectionalLight("#fff5e6", 1.1), []);
  dirLight.position.set(80, 120, 40);

  const resetCamera = useCallback(() => {
    camera.position.set(...DEFAULT_CAMERA_POSITION);
    const orbit = controls as (THREE.EventDispatcher & {
      target: THREE.Vector3;
      update: () => void;
    }) | null;
    orbit?.target.set(GRID_CENTER, 0, GRID_CENTER);
    orbit?.update();
  }, [camera, controls]);

  useEffect(() => {
    onRegisterCameraReset?.(resetCamera);
    return () => onRegisterCameraReset?.(null);
  }, [onRegisterCameraReset, resetCamera]);

  useFrame(() => {
    const visibleChunks = updateChunkVisibility(chunks, camera);
    const lodCounts = updateChunkLod(chunks, camera.position);
    const visibleBuildings = visibleBuildingCount(
      chunks,
      city.chunkBuildingIndices,
    );

    onRenderHealth?.({
      visibleChunks,
      visibleBuildings,
      totalBuildings: city.buildings.length,
    });

    const now = performance.now();
    fpsAccum.current.frames += 1;
    const elapsed = now - fpsAccum.current.last;
    if (elapsed < 500) return;

    fpsAccum.current.fps = (fpsAccum.current.frames * 1000) / elapsed;
    fpsAccum.current.frames = 0;
    fpsAccum.current.last = now;

    statsSnapshot.current = {
      fps: Math.round(fpsAccum.current.fps),
      dpr,
      visibleChunks,
      loadedChunks: getLoadedChunkCount(),
      visibleBuildings,
      lodCounts,
      pickedTile,
    };
    onStats(statsSnapshot.current as FpsStats);
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
      <FrictionOverlay
        corridors={frictionCorridors}
        visible={showFrictionOverlay}
      />
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
        onDotClick={onCitizenDotClick}
      />
      <EventMarkers
        activeEvents={activeEvents}
        onEventClick={onEventMarkerClick}
      />
      <FrontierCityHallLandmark era={era} />
      {hasHeroGltfKey("hero_frontier_church") ? (
        <FrontierChurchLandmark era={era} />
      ) : null}
      <EraLandmarkPlaceholder era={era} eraProgress={eraProgress} />
      <TilePicker onPick={onPick} onHover={onHover} onHoverEnd={onHoverEnd} />
    </>
  );
}

const GRID_CENTER = 128;
const DEFAULT_CAMERA_POSITION = [140, 120, 140] as const;
