"use client";

import { Canvas } from "@react-three/fiber";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type * as THREE from "three";
import type { FpsStats, PickResult, CityData } from "@/lib/types";
import { cityDataFromSnapshot, getCityData } from "@/lib/city-data";
import { createChunkStates } from "@/lib/chunks";
import { LOW_APPROVAL_WARNING_THRESHOLD, MAX_DPR } from "@/lib/constants";
import { LOW_HAPPINESS_APPROVAL } from "@/lib/sim-metrics";
import type { GraphicsQualityTier } from "@/lib/constants";
import type { CitizenDotPick } from "@/lib/population-l2";
import {
  createSimBridge,
  type ActiveEventSnapshot,
  type GameSpeedLevel,
  type ServiceCoverageSnapshot,
  type ServiceViewMode,
  type SimBridge,
  type SimClientApi,
  type SimResources,
  type SimSnapshot,
  resourcesFromSnapshot,
} from "@/lib/sim-bridge";
import { estimateHealthcareCoverage } from "@/lib/sim-metrics";
import type { ZoningTool, ZoneTile, RoadTile, TrafficTile, PaintBrushSize } from "@/lib/zoning";
import {
  brushTileOffsets,
  ENGINE_ZONE_TYPE,
  paintRoadBrush,
  paintZoneBrush,
} from "@/lib/zoning";
import { playPaintFeedback } from "@/lib/paint-feedback";
import { CityScene } from "./CityScene";
import { AdaptiveDpr } from "./AdaptiveDpr";
import { CityPostProcessing } from "./CityPostProcessing";
import { CanvasRenderHealth } from "./CanvasRenderHealth";

function skyColorForApproval(approval: number | undefined): string {
  const base = { r: 0x0b, g: 0x10, b: 0x20 };
  if (approval === undefined) {
    return `#${base.r.toString(16).padStart(2, "0")}${base.g.toString(16).padStart(2, "0")}${base.b.toString(16).padStart(2, "0")}`;
  }
  if (approval < LOW_APPROVAL_WARNING_THRESHOLD) {
    return "#2a0f14";
  }
  if (approval < LOW_HAPPINESS_APPROVAL) {
    return "#1a1220";
  }
  return `#${base.r.toString(16).padStart(2, "0")}${base.g.toString(16).padStart(2, "0")}${base.b.toString(16).padStart(2, "0")}`;
}

type CityCanvasProps = {
  activeTool: ZoningTool;
  brushSize: PaintBrushSize;
  /** When set, tile clicks place this building type via WASM `place_building`. */
  buildTypeId?: number | null;
  /** Road tier for the road tool (0=dirt, 1=paved, 2=highway); encoded in local roadFlags. */
  roadTier?: number;
  gameSpeed: GameSpeedLevel;
  qualityTier: GraphicsQualityTier;
  activeEvents?: ActiveEventSnapshot[];
  onEventMarkerClick?: (event: ActiveEventSnapshot) => void;
  onCitizenDotClick?: (pick: CitizenDotPick) => void;
  showTrafficOverlay?: boolean;
  serviceViewMode: ServiceViewMode;
  onStats: (stats: FpsStats) => void;
  onSimResources?: (resources: SimResources) => void;
  onSimApi?: (api: SimClientApi | null) => void;
  onZonePainted?: (zoneType: number) => void;
};

/** Encode road tier into roadFlags bits 4–5 (ToolSystem.cs); WASM PlaceRoad ignores tier. */
function roadFlagsForTier(roadTier: number | undefined): number {
  const tier = roadTier ?? 0;
  return ((tier & 0x03) << 4) | 0x01;
}

export function CityCanvas({
  activeTool,
  brushSize,
  buildTypeId,
  roadTier,
  gameSpeed,
  qualityTier,
  activeEvents,
  onEventMarkerClick,
  onCitizenDotClick,
  showTrafficOverlay = true,
  serviceViewMode,
  onStats,
  onSimResources,
  onSimApi,
  onZonePainted,
}: CityCanvasProps) {
  const [city, setCity] = useState<CityData>(() => getCityData());
  const [zones, setZones] = useState<ZoneTile[]>([]);
  const [roads, setRoads] = useState<RoadTile[]>([]);
  const [traffic, setTraffic] = useState<TrafficTile[]>([]);
  const [serviceCoverage, setServiceCoverage] = useState<ServiceCoverageSnapshot[]>(
    [],
  );
  const [simResources, setSimResources] = useState<SimResources | null>(null);
  const [simSource, setSimSource] = useState<"wasm" | "procedural">("procedural");
  const [bridgeReady, setBridgeReady] = useState(false);
  const [postProcessingDisabled, setPostProcessingDisabled] = useState(false);
  const [renderHealthStats, setRenderHealthStats] = useState<
    Pick<FpsStats, "visibleBuildings" | "visibleChunks" | "totalBuildings">
  >({
    visibleBuildings: 0,
    visibleChunks: 0,
    totalBuildings: city.buildings.length,
  });
  const chunks = useMemo(() => createChunkStates(), []);
  const healthcareCoverage = useMemo(
    () => simResources?.healthcareCoverage ?? estimateHealthcareCoverage(city),
    [simResources?.healthcareCoverage, city],
  );
  const policeCoverage = simResources?.policeCoverage;
  const fireCoverage = simResources?.fireCoverage;
  const [dpr, setDpr] = useState(1);
  useEffect(() => {
    setDpr(Math.min(MAX_DPR, window.devicePixelRatio));
  }, []);
  const [pickedTile, setPickedTile] = useState<PickResult>(null);
  const bridgeRef = useRef<SimBridge | null>(null);
  const gameSpeedRef = useRef(gameSpeed);
  gameSpeedRef.current = gameSpeed;
  const latestStats = useRef<FpsStats>({
    fps: 0,
    dpr,
    visibleChunks: 0,
    visibleBuildings: 0,
    totalBuildings: city.buildings.length,
    lodCounts: [0, 0, 0, 0],
    pickedTile: null,
  });

  const applySnapshot = useCallback(
    (snapshot: SimSnapshot) => {
      setCity(cityDataFromSnapshot(snapshot));
      setZones(snapshot.zones ?? []);
      setRoads(
        (snapshot.roads ?? []).map((r) => ({
          tileX: r.tileX,
          tileZ: r.tileZ,
          roadFlags: r.roadFlags,
        })),
      );
      setTraffic(
        (snapshot.traffic ?? []).map((t) => ({
          tileX: t.tileX,
          tileZ: t.tileZ,
          density: t.density,
        })),
      );
      setServiceCoverage(snapshot.serviceCoverage ?? []);
      const resources = resourcesFromSnapshot(snapshot);
      setSimResources(resources);
      onSimResources?.(resources);
    },
    [onSimResources],
  );

  useEffect(() => {
    const bridge = createSimBridge();
    bridgeRef.current = bridge;
    let cancelled = false;
    let raf = 0;
    // Deliberately `null` until the first RAF callback so the first `deltaMs`
    // we forward to the worker is a real inter-frame delta, not the entire
    // `await bridge.init(...)` window (which can be hundreds of ms of WASM
    // load time and would cause a giant first tick / spiral in the worker).
    let last: number | null = null;

    const startTickLoop = () => {
      last = null;
      const loop = (now: number) => {
        if (cancelled) return;
        const delta = last === null ? 0 : now - last;
        last = now;
        if (delta > 0) bridge.send({ type: "tick", deltaMs: delta });
        raf = requestAnimationFrame(loop);
      };
      raf = requestAnimationFrame(loop);
    };

    let fellBack = false;
    const handleWorkerFailure = (err: unknown) => {
      if (fellBack || cancelled) return;
      fellBack = true;
      console.warn(
        "[CityMajor] WASM sim unavailable — using procedural city fallback",
        err,
      );
      cancelAnimationFrame(raf);
      raf = 0;
      setBridgeReady(false);
      setSimSource("procedural");
      setCity(getCityData());
      setZones([]);
      setRoads([]);
      setTraffic([]);
      setServiceCoverage([]);
      setSimResources(null);
    };

    // Subscribe before init so the worker's first snapshot (posted before
    // `ready`) and any init-time errors reach the UI immediately.
    const unsubscribeSnapshot = bridge.onSnapshot(applySnapshot);
    const unsubscribeError = bridge.onError(handleWorkerFailure);

    (async () => {
      try {
        await bridge.init("/dotnet", 256);
        if (cancelled || fellBack) return;

        setSimSource("wasm");
        bridge.send({ type: "set_speed", level: gameSpeedRef.current });
        setBridgeReady(true);
        startTickLoop();
      } catch (err) {
        handleWorkerFailure(err);
      }
    })();

    return () => {
      cancelled = true;
      cancelAnimationFrame(raf);
      setBridgeReady(false);
      unsubscribeSnapshot();
      unsubscribeError();
      bridge.dispose();
      bridgeRef.current = null;
    };
  }, [applySnapshot]);

  useEffect(() => {
    const bridge = bridgeRef.current;
    if (!bridgeReady || !bridge) {
      onSimApi?.(null);
      return;
    }
    onSimApi?.({
      getSnapshot: () => bridge.getSnapshot(),
      applySnapshot: async (snapshot) => {
        applySnapshot(snapshot);
        await bridge.loadSnapshot(snapshot);
        const authoritative = bridge.getSnapshot();
        if (authoritative) applySnapshot(authoritative);
      },
      applyWasmSave: async (base64Cmjr) => {
        await bridge.loadCmjr(base64Cmjr);
        const authoritative = bridge.getSnapshot();
        if (authoritative) applySnapshot(authoritative);
      },
      exportWasmSave: () => bridge.exportCmjr(),
      sendCommand: (command) => bridge.send(command),
    });
    return () => onSimApi?.(null);
  }, [bridgeReady, applySnapshot, onSimApi]);

  useEffect(() => {
    bridgeRef.current?.send({ type: "set_speed", level: gameSpeed });
  }, [gameSpeed]);

  const handlePick = useCallback(
    (pick: PickResult) => {
      setPickedTile(pick);
      if (!pick) return;

      const bridge = bridgeRef.current;
      const wasmLive = bridgeReady && simSource === "wasm" && bridge;

      if (buildTypeId != null) {
        if (wasmLive) {
          for (const [dx, dz] of brushTileOffsets(brushSize)) {
            bridge.send({
              type: "place_building",
              tileX: pick.tileX + dx,
              tileZ: pick.tileZ + dz,
              typeId: buildTypeId,
            });
          }
        }
        playPaintFeedback("zone");
        return;
      }

      if (activeTool === "road") {
        const roadFlags = roadFlagsForTier(roadTier);
        setRoads((prev) =>
          paintRoadBrush(prev, pick.tileX, pick.tileZ, roadFlags, brushSize),
        );
        if (wasmLive) {
          for (const [dx, dz] of brushTileOffsets(brushSize)) {
            bridge.send({
              type: "place_road",
              tileX: pick.tileX + dx,
              tileZ: pick.tileZ + dz,
            });
          }
        }
        playPaintFeedback("road");
        return;
      }

      if (activeTool === "bulldoze") {
        setZones((prev) =>
          paintZoneBrush(prev, pick.tileX, pick.tileZ, 0, brushSize),
        );
        if (wasmLive) {
          for (const [dx, dz] of brushTileOffsets(brushSize)) {
            const tileX = pick.tileX + dx;
            const tileZ = pick.tileZ + dz;
            bridge.send({ type: "bulldoze", tileX, tileZ });
          }
        }
        playPaintFeedback("bulldoze");
        return;
      }

      const zoneType = ENGINE_ZONE_TYPE[activeTool];
      setZones((prev) =>
        paintZoneBrush(prev, pick.tileX, pick.tileZ, zoneType, brushSize),
      );
      if (wasmLive) {
        for (const [dx, dz] of brushTileOffsets(brushSize)) {
          const tileX = pick.tileX + dx;
          const tileZ = pick.tileZ + dz;
          bridge.send({
            type: "zone_paint",
            tileX,
            tileZ,
            zoneType,
          });
        }
      }
      playPaintFeedback("zone");
      onZonePainted?.(zoneType);
    },
    [activeTool, brushSize, buildTypeId, roadTier, bridgeReady, simSource, onZonePainted],
  );

  useEffect(() => {
    latestStats.current = {
      ...latestStats.current,
      pickedTile,
      dpr,
      simSource,
      healthcareCoverage,
      policeCoverage,
      fireCoverage,
    };
    onStats(latestStats.current);
  }, [pickedTile, dpr, simSource, healthcareCoverage, policeCoverage, fireCoverage, onStats]);

  const handleRenderHealth = useCallback(
    (partial: Pick<FpsStats, "visibleBuildings" | "visibleChunks" | "totalBuildings">) => {
      setRenderHealthStats((prev) => {
        if (
          prev.visibleBuildings === partial.visibleBuildings &&
          prev.visibleChunks === partial.visibleChunks &&
          prev.totalBuildings === partial.totalBuildings
        ) {
          return prev;
        }
        return partial;
      });
    },
    [],
  );

  const handleStats = (partial: FpsStats) => {
    latestStats.current = {
      ...partial,
      pickedTile,
      dpr,
      simSource,
      healthcareCoverage,
      policeCoverage,
      fireCoverage,
      totalBuildings: city.buildings.length,
    };
    onStats(latestStats.current);
  };

  const skyColor = useMemo(
    () => skyColorForApproval(simResources?.approval),
    [simResources?.approval],
  );

  const handleRenderFallback = useCallback((reason: string) => {
    console.warn(`[CityMajor] Render fallback (${reason}) — procedural city + terrain`);
    setPostProcessingDisabled(true);
    setSimSource("procedural");
    setCity(getCityData());
    setZones([]);
    setRoads([]);
    setTraffic([]);
    setServiceCoverage([]);
    setSimResources(null);
  }, []);

  const handleGlCreated = useCallback(
    ({ gl }: { gl: THREE.WebGLRenderer }) => {
      const canvas = gl.domElement;
      canvas.addEventListener("webglcontextlost", (event) => {
        event.preventDefault();
        console.warn("[CityMajor] WebGL context lost — awaiting restore");
      });
      canvas.addEventListener("webglcontextrestored", () => {
        console.info("[CityMajor] WebGL context restored");
        gl.setPixelRatio(dpr);
        gl.setSize(canvas.clientWidth, canvas.clientHeight, false);
      });
    },
    [dpr],
  );

  return (
    <div style={{ width: "100%", height: "100%", position: "relative" }}>
      <div data-testid="city-canvas" style={{ width: "100%", height: "100%" }}>
        <Canvas
          frameloop="always"
          dpr={dpr}
          camera={{ position: [140, 120, 140], fov: 50, near: 0.1, far: 800 }}
          gl={{
            antialias: true,
            powerPreference: "high-performance",
            preserveDrawingBuffer: true,
          }}
          onCreated={handleGlCreated}
          style={{ width: "100%", height: "100%", display: "block" }}
        >
          <color attach="background" args={[skyColor]} />
          <CityScene
              city={city}
              chunks={chunks}
              zones={zones}
              roads={roads}
              traffic={traffic}
              showTrafficOverlay={showTrafficOverlay}
              serviceCoverage={serviceCoverage}
              serviceViewMode={serviceViewMode}
              pickedTile={pickedTile}
              activeEvents={activeEvents}
              onEventMarkerClick={onEventMarkerClick}
              onCitizenDotClick={onCitizenDotClick}
              onPick={handlePick}
              onStats={handleStats}
              onRenderHealth={handleRenderHealth}
              dpr={dpr}
              population={simResources?.population ?? 0}
              householdCount={simResources?.householdCount}
              era={simResources?.era ?? 0}
              eraProgress={simResources?.eraProgress}
            />
          <AdaptiveDpr dpr={dpr} onDprChange={setDpr} />
          <CityPostProcessing
            qualityTier={qualityTier}
            disabled={postProcessingDisabled}
            sceneReady={
              renderHealthStats.visibleBuildings > 0 &&
              renderHealthStats.visibleChunks > 0
            }
          />
          <CanvasRenderHealth
            stats={renderHealthStats}
            postProcessingActive={
              qualityTier === "high" && !postProcessingDisabled
            }
            onDisablePostProcessing={() => setPostProcessingDisabled(true)}
            onRenderFallback={handleRenderFallback}
          />
        </Canvas>
      </div>
    </div>
  );
}
