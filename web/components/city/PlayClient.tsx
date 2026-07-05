"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { z } from "zod";
import { EntitlementsSchema, type Entitlements } from "@/lib/entitlements";
import {
  NarrativeEventResponseSchema,
  type NarrativeEventResponse,
  type SimStateBucket,
} from "@/lib/narrative-templates";
import {
  cashCrisisRank,
  detectCashCrisis,
  deriveNarrativeBucket,
  explainNarrativeBucket,
  type CashCrisisKind,
  type CashCrisisWarning,
} from "@/lib/sim-metrics";
import { narrativeFromSimEvent } from "@/lib/event-catalog";
import { heraldOptionToSimCommands } from "@/lib/herald-option-commands";
import { narrativeFromEraTransition } from "@/lib/era-narrative";
import type {
  ActiveEventSnapshot,
  GameSpeedLevel,
  SimClientApi,
  SimResources,
} from "@/lib/sim-bridge";
import {
  EMPTY_CITY_STORAGE_KEY,
  GRAPHICS_QUALITY_STORAGE_KEY,
  TRAFFIC_OVERLAY_STORAGE_KEY,
  type GraphicsQualityTier,
} from "@/lib/constants";
import type { FpsStats } from "@/lib/types";
import { PopulationGrowthTracker } from "@/lib/population-growth";
import type { ZoningTool, PaintBrushSize } from "@/lib/zoning";
import { ApprovalMoodOverlay } from "@/components/city/ApprovalMoodOverlay";
import { CityCanvas } from "@/components/city/CityCanvas";
import { CrisisWarningModal } from "@/components/city/CrisisWarningModal";
import { EraTransitionModal } from "@/components/city/EraTransitionModal";
import { NewsTicker } from "@/components/city/NewsTicker";
import { FpsHud } from "@/components/city/FpsHud";
import { MinimapPanel } from "@/components/city/MinimapPanel";
import { HeraldButton } from "@/components/city/HeraldButton";
import { HeraldPanel } from "@/components/city/HeraldPanel";
import { ResearchButton } from "@/components/city/ResearchButton";
import { ResearchPanel } from "@/components/city/ResearchPanel";
import { EconomyButton } from "@/components/city/EconomyButton";
import { EconomyPanel } from "@/components/city/EconomyPanel";
import { LawButton } from "@/components/city/LawButton";
import { LawPanel } from "@/components/city/LawPanel";
import { CitizenButton } from "@/components/city/CitizenButton";
import { CitizenPanel } from "@/components/city/CitizenPanel";
import { HudToast } from "@/components/city/HudToast";
import { HudToastStack } from "@/components/city/HudToastStack";
import { ResearchUnlockToast } from "@/components/city/ResearchUnlockToast";
import { detectNewlyUnlockedContent, type NewlyUnlockedResearch } from "@/lib/tech-unlocks";
import { HUD_ZONE, hudActionButton } from "@/lib/hud-theme";
import { HudWordmark } from "@/components/city/HudWordmark";
import { QualityToolbar } from "@/components/city/QualityToolbar";
import { TrafficOverlayToggle } from "@/components/city/TrafficOverlayToggle";
import { DemandOverlay } from "@/components/city/DemandOverlay";
import { ResourcesHud } from "@/components/city/ResourcesHud";
import { PopulationPanel } from "@/components/city/PopulationPanel";
import { EraProgressPanel } from "@/components/city/EraProgressPanel";
import { BudgetPanel } from "@/components/city/BudgetPanel";
import { SaveLoadControls } from "@/components/city/SaveLoadControls";
import { SpeedToolbar } from "@/components/city/SpeedToolbar";
import { ZoningToolbar } from "@/components/city/ZoningToolbar";
import { BuildToolbar } from "@/components/city/BuildToolbar";
import { RoadTypeToolbar } from "@/components/city/RoadTypeToolbar";
import { TransportToolbar } from "@/components/city/TransportToolbar";
import { EducationToolbar } from "@/components/city/EducationToolbar";
import { ServicesToolbar } from "@/components/city/ServicesToolbar";
import type { ServiceViewMode } from "@/lib/sim-bridge";
import {
  isResidentialZonePaint,
  OnboardingOverlay,
} from "@/components/city/OnboardingOverlay";
import { resolveRci } from "@/lib/zoning-economy";
import { GltfPreloader } from "@/components/city/GltfPreloader";
import type { HouseholdPreview } from "@/lib/population-l2";
import {
  DEFAULT_ROAD_TIER,
  type RoadTier,
} from "@/lib/road-types";
import {
  DEFAULT_TRANSIT_MODE,
  type TransitModeId,
} from "@/lib/transit-modes";
import {
  isEducationBuildTypeId,
  type EducationBuildTypeId,
} from "@/lib/education-buildings";
import { handlePlayKeyboardShortcut } from "@/lib/play-keyboard";
import { HelpPanel } from "@/components/city/HelpPanel";

export type BuildMode = "zone" | "road" | "plop" | "education";

const NarrativeApiResponseSchema = NarrativeEventResponseSchema.extend({
  narrativeEventsRemaining: z.number().int().nonnegative().optional(),
});

function formatQuota(remaining: number | undefined): string {
  if (remaining === undefined) return "…";
  if (remaining === Number.MAX_SAFE_INTEGER) return "∞";
  return String(remaining);
}

function readStoredQualityTier(): GraphicsQualityTier {
  if (typeof window === "undefined") return "low";
  const stored = window.localStorage.getItem(GRAPHICS_QUALITY_STORAGE_KEY);
  if (stored === "high" || stored === "low") return stored;
  return "low";
}

function readStoredTrafficOverlay(): boolean {
  if (typeof window === "undefined") return true;
  const stored = window.localStorage.getItem(TRAFFIC_OVERLAY_STORAGE_KEY);
  return stored !== "off";
}

function readEmptyCityPref(urlEmpty: boolean): boolean {
  if (typeof window === "undefined") return urlEmpty;
  if (urlEmpty) return true;
  return window.localStorage.getItem(EMPTY_CITY_STORAGE_KEY) === "1";
}

export function PlayClient() {
  const searchParams = useSearchParams();
  const urlEmpty = searchParams.get("empty") === "1";
  const [emptyCity, setEmptyCity] = useState(() => readEmptyCityPref(urlEmpty));
  const [activeTool, setActiveTool] = useState<ZoningTool>("residential");
  const [brushSize, setBrushSize] = useState<PaintBrushSize>(1);
  const [gameSpeed, setGameSpeed] = useState<GameSpeedLevel>(1);
  const [qualityTier, setQualityTier] = useState<GraphicsQualityTier>(() =>
    readStoredQualityTier(),
  );
  const [showTrafficOverlay, setShowTrafficOverlay] = useState(() =>
    readStoredTrafficOverlay(),
  );
  const [serviceViewMode, setServiceViewMode] = useState<ServiceViewMode>("off");
  const [stats, setStats] = useState<FpsStats>({
    fps: 0,
    dpr: 1,
    visibleChunks: 0,
    visibleBuildings: 0,
    totalBuildings: 0,
    lodCounts: [0, 0, 0, 0],
    pickedTile: null,
  });
  const [entitlements, setEntitlements] = useState<Entitlements | null>(null);
  const [entitlementsError, setEntitlementsError] = useState<string | null>(null);
  const [simResources, setSimResources] = useState<SimResources | null>(null);
  const [populationGrowthPerMonth, setPopulationGrowthPerMonth] = useState<
    number | null
  >(null);
  const [simApi, setSimApi] = useState<SimClientApi | null>(null);
  const [heraldOpen, setHeraldOpen] = useState(false);
  const [heraldLoading, setHeraldLoading] = useState(false);
  const [heraldError, setHeraldError] = useState<string | null>(null);
  const [heraldEvent, setHeraldEvent] = useState<NarrativeEventResponse | null>(null);
  const [heraldBucketReason, setHeraldBucketReason] = useState<string | null>(null);
  const [heraldSimEvent, setHeraldSimEvent] = useState<ActiveEventSnapshot | null>(null);
  const [heraldSpecialEdition, setHeraldSpecialEdition] = useState(false);
  const [eraTransitionEra, setEraTransitionEra] = useState<number | null>(null);
  const [researchOpen, setResearchOpen] = useState(false);
  const [economyOpen, setEconomyOpen] = useState(false);
  const [lawOpen, setLawOpen] = useState(false);
  const [citizenOpen, setCitizenOpen] = useState(false);
  const [citizenSelection, setCitizenSelection] = useState<{
    householdId?: string;
    tileX?: number;
    tileZ?: number;
  } | null>(null);
  const [researchUnlock, setResearchUnlock] = useState<NewlyUnlockedResearch | null>(
    null,
  );
  const [cashCrisisToast, setCashCrisisToast] = useState<CashCrisisWarning | null>(
    null,
  );
  const [residentialZonePainted, setResidentialZonePainted] = useState(false);
  const [buildMode, setBuildMode] = useState<BuildMode>("zone");
  const [buildTypeId, setBuildTypeId] = useState<number | null>(null);
  const [roadTier, setRoadTier] = useState<RoadTier>(DEFAULT_ROAD_TIER);
  const [transitMode, setTransitMode] = useState<TransitModeId>(
    DEFAULT_TRANSIT_MODE,
  );
  const [buildOpen, setBuildOpen] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);

  const statsRef = useRef(stats);
  statsRef.current = stats;
  const simResourcesRef = useRef(simResources);
  simResourcesRef.current = simResources;
  const heraldedEventIdsRef = useRef<Set<number>>(new Set());
  const prevEraRef = useRef<number | null>(null);
  const prevUnlockedTechRef = useRef<Set<number>>(new Set());
  const prevCashCrisisKindRef = useRef<CashCrisisKind | null>(null);
  const simApiRef = useRef(simApi);
  simApiRef.current = simApi;
  const populationGrowthRef = useRef(new PopulationGrowthTracker());

  const refreshEntitlements = useCallback(async () => {
    try {
      const res = await fetch("/api/me/entitlements", { credentials: "include" });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const json: unknown = await res.json();
      const parsed = EntitlementsSchema.safeParse(json);
      if (!parsed.success) throw new Error("Invalid entitlements response");
      setEntitlements(parsed.data);
      setEntitlementsError(null);
    } catch (err) {
      setEntitlementsError(err instanceof Error ? err.message : "Failed to load entitlements");
    }
  }, []);

  useEffect(() => {
    void refreshEntitlements();
  }, [refreshEntitlements]);

  useEffect(() => {
    if (urlEmpty) {
      window.localStorage.setItem(EMPTY_CITY_STORAGE_KEY, "1");
      setEmptyCity(true);
    }
  }, [urlEmpty]);

  const handleEmptyCityToggle = useCallback((enabled: boolean) => {
    setEmptyCity(enabled);
    window.localStorage.setItem(EMPTY_CITY_STORAGE_KEY, enabled ? "1" : "0");
  }, []);

  // Playwright smoke: exportWasmSave() without clicking Save UI (localStorage gate).
  useEffect(() => {
    if (typeof window === "undefined") return;
    if (window.localStorage.getItem("citymajor_smoke") !== "1") return;
    window.__citymajorSimApi = simApi;
    return () => {
      window.__citymajorSimApi = null;
    };
  }, [simApi]);

  const handleSimResources = useCallback((resources: SimResources) => {
    populationGrowthRef.current.push(resources.tick, resources.population);
    setPopulationGrowthPerMonth(populationGrowthRef.current.estimatePerMonth());
    setSimResources(resources);
  }, []);

  const handleQualityChange = useCallback((tier: GraphicsQualityTier) => {
    setQualityTier(tier);
    window.localStorage.setItem(GRAPHICS_QUALITY_STORAGE_KEY, tier);
  }, []);

  const handleTrafficOverlayToggle = useCallback((enabled: boolean) => {
    setShowTrafficOverlay(enabled);
    window.localStorage.setItem(
      TRAFFIC_OVERLAY_STORAGE_KEY,
      enabled ? "on" : "off",
    );
  }, []);

  const fetchHeraldStory = useCallback(async (simEvent?: ActiveEventSnapshot) => {
    if (simEvent) {
      setHeraldLoading(true);
      setHeraldError(null);
      setHeraldSimEvent(simEvent);
      setHeraldSpecialEdition(false);
      setHeraldBucketReason("Triggered by an active city event in the simulation");
      try {
        setHeraldEvent(narrativeFromSimEvent(simEvent.typeId));
      } catch (err) {
        setHeraldError(
          err instanceof Error ? err.message : "Failed to load Herald story",
        );
      } finally {
        setHeraldLoading(false);
      }
      return;
    }

    const coverage =
      simResourcesRef.current?.healthcareCoverage ??
      statsRef.current.healthcareCoverage ??
      0.5;
    const resources = simResourcesRef.current;
    const bucket: SimStateBucket = deriveNarrativeBucket({
      healthcareCoverage: coverage,
      ...(resources
        ? {
            approval: resources.approval,
            cityFunds: resources.cityFunds,
            residentialDemand: resources.residentialDemand,
            commercialDemand: resources.commercialDemand,
            industrialDemand: resources.industrialDemand,
          }
        : {}),
    });
    const { reason: bucketReason } = explainNarrativeBucket({
      healthcareCoverage: coverage,
      ...(resources
        ? {
            approval: resources.approval,
            cityFunds: resources.cityFunds,
            residentialDemand: resources.residentialDemand,
            commercialDemand: resources.commercialDemand,
            industrialDemand: resources.industrialDemand,
          }
        : {}),
    });

    setHeraldLoading(true);
    setHeraldError(null);
    setHeraldEvent(null);
    setHeraldSimEvent(null);
    setHeraldSpecialEdition(false);
    setHeraldBucketReason(bucketReason);

    try {
      const res = await fetch("/api/narrative/event", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          bucket,
          context: {
            metricValue: coverage,
            era:
              resources?.era !== undefined ? String(resources.era) : undefined,
          },
        }),
      });

      const json: unknown = await res.json();

      if (!res.ok) {
        const message =
          typeof json === "object" &&
          json !== null &&
          "error" in json &&
          typeof (json as { error: unknown }).error === "string"
            ? (json as { error: string }).error
            : `HTTP ${res.status}`;
        throw new Error(message);
      }

      const parsed = NarrativeApiResponseSchema.safeParse(json);
      if (!parsed.success) {
        throw new Error("Invalid narrative response from server");
      }

      const { narrativeEventsRemaining, ...event } = parsed.data;
      setHeraldEvent(event);

      if (narrativeEventsRemaining !== undefined && entitlements) {
        setEntitlements({ ...entitlements, narrativeEventsRemaining });
      } else {
        await refreshEntitlements();
      }
    } catch (err) {
      setHeraldError(err instanceof Error ? err.message : "Failed to load Herald story");
    } finally {
      setHeraldLoading(false);
    }
  }, [entitlements, refreshEntitlements]);

  const openHerald = useCallback(
    (simEvent?: ActiveEventSnapshot) => {
      setHeraldOpen(true);
      void fetchHeraldStory(simEvent);
    },
    [fetchHeraldStory],
  );

  const openEraHerald = useCallback((era: number) => {
    setHeraldOpen(true);
    setHeraldLoading(true);
    setHeraldError(null);
    setHeraldEvent(null);
    setHeraldSimEvent(null);
    setHeraldSpecialEdition(true);
    setHeraldBucketReason(
      "Population and technology gates cleared — era transition special edition",
    );
    try {
      setHeraldEvent(narrativeFromEraTransition(era));
    } catch (err) {
      setHeraldError(
        err instanceof Error ? err.message : "Failed to load Herald story",
      );
    } finally {
      setHeraldLoading(false);
    }
  }, []);

  useEffect(() => {
    const era = simResources?.era;
    if (era === undefined) return;

    const prev = prevEraRef.current;
    prevEraRef.current = era;

    if (prev === null || era <= prev) return;

    setEraTransitionEra(era);
    openEraHerald(era);
  }, [simResources?.era, openEraHerald]);

  const dismissEraTransition = useCallback(() => {
    setEraTransitionEra(null);
  }, []);

  useEffect(() => {
    const ids = simResources?.unlockedTechIds;
    if (!ids) return;

    const prev = prevUnlockedTechRef.current;
    const unlock = detectNewlyUnlockedContent(prev, ids);
    prevUnlockedTechRef.current = new Set(ids);

    if (!unlock) return;
    setResearchUnlock(unlock);
  }, [simResources?.unlockedTechIds]);

  useEffect(() => {
    const crisis = detectCashCrisis(simResources);
    const kind = crisis?.kind ?? null;
    const prev = prevCashCrisisKindRef.current;
    prevCashCrisisKindRef.current = kind;

    if (!crisis) return;
    if (prev !== null && kind !== null && cashCrisisRank(kind) <= cashCrisisRank(prev)) {
      return;
    }

    setCashCrisisToast(crisis);
  }, [simResources]);

  useEffect(() => {
    const events = simResources?.activeEvents;
    if (!events?.length) return;

    for (const event of events) {
      if (event.phase !== "active") continue;
      if (heraldedEventIdsRef.current.has(event.eventId)) continue;

      heraldedEventIdsRef.current.add(event.eventId);
      openHerald(event);
      break;
    }
  }, [simResources?.activeEvents, openHerald]);

  const handleHeraldOptionSelect = useCallback(
    (optionId: string) => {
      const simEvent = heraldSimEvent;
      const commands = heraldOptionToSimCommands(optionId, {
        eventId: simEvent?.eventId,
      });
      console.info("[CityMajor] Herald council choice", {
        optionId,
        eventTypeId: simEvent?.typeId,
        eventId: simEvent?.eventId,
        commands,
      });
      const api = simApiRef.current;
      if (!api) return;
      for (const command of commands) {
        api.sendCommand(command);
      }
    },
    [heraldSimEvent],
  );

  const handleEnqueueResearch = useCallback((techId: number) => {
    simApiRef.current?.sendCommand({ type: "enqueue_research", techId });
  }, []);

  const handleSetLawActive = useCallback((lawId: string, active: boolean) => {
    simApiRef.current?.sendCommand({ type: "set_law_active", lawId, active });
  }, []);

  const handleZonePainted = useCallback((zoneType: number) => {
    if (isResidentialZonePaint(zoneType)) {
      setResidentialZonePainted(true);
    }
  }, []);

  const handleToolChange = useCallback((tool: ZoningTool) => {
    setActiveTool(tool);
    setBuildOpen(false);
    if (tool === "road") {
      setBuildMode("road");
      setBuildTypeId(null);
      return;
    }
    setBuildMode("zone");
    setBuildTypeId(null);
  }, []);

  const handleBuildSelect = useCallback((typeId: number) => {
    setBuildTypeId(typeId);
    if (isEducationBuildTypeId(typeId)) {
      setBuildMode("education");
      setServiceViewMode("education");
    } else {
      setBuildMode("plop");
    }
  }, []);

  const handleEducationSelect = useCallback((typeId: EducationBuildTypeId | null) => {
    setBuildTypeId(typeId);
    if (typeId === null) {
      setBuildMode((mode) => (mode === "education" ? "zone" : mode));
      return;
    }
    setBuildMode("education");
    setBuildOpen(false);
  }, []);

  const handleServiceViewModeChange = useCallback((mode: ServiceViewMode) => {
    setServiceViewMode(mode);
    if (mode !== "education" && buildMode === "education" && buildTypeId !== null) {
      setBuildMode("plop");
    }
  }, [buildMode, buildTypeId]);

  const handleRoadTierSelect = useCallback((tier: RoadTier) => {
    setRoadTier(tier);
    setBuildMode("road");
    setActiveTool("road");
    setBuildTypeId(null);
    setBuildOpen(false);
  }, []);

  const handleTransitModeSelect = useCallback((modeId: TransitModeId) => {
    setTransitMode(modeId);
    setBuildMode("road");
    setActiveTool("road");
    setBuildTypeId(null);
    setBuildOpen(false);
  }, []);

  const handleEnterZoneMode = useCallback(() => {
    setBuildOpen(false);
    setBuildTypeId(null);
    setBuildMode("zone");
    setActiveTool((tool) =>
      tool === "road" || tool === "bulldoze" ? "residential" : tool,
    );
  }, []);

  const handleToggleBuildMenu = useCallback(() => {
    setBuildOpen((open) => !open);
  }, []);

  const handleKeyboardSelectRoad = useCallback(() => {
    handleToolChange("road");
  }, [handleToolChange]);

  const handleToggleHelp = useCallback(() => {
    setHelpOpen((open) => !open);
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const handlers = {
        onToggleHelp: handleToggleHelp,
        onToggleBuildMenu: handleToggleBuildMenu,
        onEnterZoneMode: handleEnterZoneMode,
        onSelectRoad: handleKeyboardSelectRoad,
        onSelectZoneTool: handleToolChange,
        unlockedTechIds: simResources?.unlockedTechIds,
      };

      if (event.key === "?") {
        handlePlayKeyboardShortcut(event, handlers);
        return;
      }

      if (helpOpen) return;

      if (
        heraldOpen ||
        researchOpen ||
        economyOpen ||
        lawOpen ||
        citizenOpen
      ) {
        return;
      }

      handlePlayKeyboardShortcut(event, handlers);
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [
    citizenOpen,
    economyOpen,
    handleEnterZoneMode,
    handleKeyboardSelectRoad,
    handleToggleBuildMenu,
    handleToggleHelp,
    handleToolChange,
    helpOpen,
    heraldOpen,
    lawOpen,
    researchOpen,
    simResources?.unlockedTechIds,
  ]);

  const canvasActiveTool: ZoningTool =
    buildMode === "road" ? "road" : activeTool;

  const handleCitizenDotClick = useCallback(
    (pick: { tileX: number; tileZ: number; buildingIndex: number }) => {
      setCitizenSelection({ tileX: pick.tileX, tileZ: pick.tileZ });
      setCitizenOpen(true);
    },
    [],
  );

  const handleSelectHousehold = useCallback((household: HouseholdPreview) => {
    setCitizenSelection({
      householdId: household.id,
      tileX: household.tileX,
      tileZ: household.tileZ,
    });
  }, []);

  const quotaRemaining = entitlements?.narrativeEventsRemaining;
  const heraldDisabled =
    quotaRemaining !== undefined &&
    quotaRemaining !== Number.MAX_SAFE_INTEGER &&
    quotaRemaining <= 0;

  return (
    <div style={{ width: "100vw", height: "100vh", position: "relative" }}>
      <GltfPreloader />
      <CityCanvas
        key={emptyCity ? "empty" : "starter"}
        activeTool={canvasActiveTool}
        brushSize={brushSize}
        buildTypeId={
          buildMode === "plop" || buildMode === "education" ? buildTypeId : null
        }
        roadTier={buildMode === "road" ? roadTier : undefined}
        gameSpeed={gameSpeed}
        qualityTier={qualityTier}
        showTrafficOverlay={showTrafficOverlay}
        serviceViewMode={serviceViewMode}
        skipStarterCity={emptyCity}
        activeEvents={simResources?.activeEvents}
        onEventMarkerClick={openHerald}
        onCitizenDotClick={handleCitizenDotClick}
        onStats={setStats}
        onSimResources={handleSimResources}
        onSimApi={setSimApi}
        onZonePainted={handleZonePainted}
      />
      <ApprovalMoodOverlay approval={simResources?.approval} />
      <HudWordmark />
      <SpeedToolbar speedLevel={gameSpeed} onSpeedChange={setGameSpeed} />
      <QualityToolbar qualityTier={qualityTier} onQualityChange={handleQualityChange} />
      <label
        style={{
          position: "fixed",
          top: 12,
          left: 200,
          zIndex: 20,
          display: "flex",
          alignItems: "center",
          gap: 6,
          fontSize: 12,
          color: "rgba(255,255,255,0.75)",
          pointerEvents: "auto",
          cursor: "pointer",
          userSelect: "none",
        }}
        title="Start with terrain only — no seeded roads, zones, or buildings (reloads sim)"
      >
        <input
          type="checkbox"
          checked={emptyCity}
          onChange={(e) => handleEmptyCityToggle(e.target.checked)}
        />
        Empty start
      </label>
      <TrafficOverlayToggle
        enabled={showTrafficOverlay}
        onToggle={handleTrafficOverlayToggle}
      />
      <ServicesToolbar
        viewMode={serviceViewMode}
        onViewModeChange={handleServiceViewModeChange}
        healthcareCoverage={simResources?.healthcareCoverage}
        policeCoverage={simResources?.policeCoverage}
        fireCoverage={simResources?.fireCoverage}
      />
      <SaveLoadControls
        simApi={simApi}
        entitlements={entitlements}
        onSlotsChanged={refreshEntitlements}
      />
      <ResourcesHud
        resources={simResources}
        populationGrowthPerMonth={populationGrowthPerMonth}
      />
      <DemandOverlay rci={resolveRci(simResources)} />
      <PopulationPanel resources={simResources} />
      <EraProgressPanel resources={simResources} />
      <BudgetPanel resources={simResources} />
      <FpsHud
        stats={stats}
        totalBuildings={stats.totalBuildings}
        activeTool={canvasActiveTool}
      />
      <MinimapPanel />
      <div style={{ ...HUD_ZONE.bottomLeft, pointerEvents: "auto" }}>
        <button
          type="button"
          style={{
            ...hudActionButton(),
            ...(buildOpen || buildMode === "plop" || buildMode === "education"
              ? { fontWeight: 700 }
              : {}),
          }}
          aria-pressed={buildOpen || buildMode === "plop" || buildMode === "education"}
          title="Open build catalog — place civic and service buildings"
          onClick={() => setBuildOpen((open) => !open)}
        >
          Build
        </button>
      </div>
      <ZoningToolbar
        activeTool={activeTool}
        brushSize={brushSize}
        onToolChange={handleToolChange}
        onBrushSizeChange={setBrushSize}
        rci={resolveRci(simResources)}
        unlockedTechIds={simResources?.unlockedTechIds}
      />
      <EducationToolbar
        viewMode={serviceViewMode}
        onViewModeChange={handleServiceViewModeChange}
        educationCoverage={simResources?.educationCoverage}
        selectedTypeId={
          buildMode === "education" && buildTypeId !== null && isEducationBuildTypeId(buildTypeId)
            ? buildTypeId
            : null
        }
        onSelectTypeId={handleEducationSelect}
      />
      {buildMode === "road" ? (
        <>
          <RoadTypeToolbar
            activeRoadTier={roadTier}
            onSelect={handleRoadTierSelect}
            unlockedTechIds={simResources?.unlockedTechIds}
          />
          <TransportToolbar
            activeTransitMode={transitMode}
            onSelect={handleTransitModeSelect}
            unlockedTechIds={simResources?.unlockedTechIds}
          />
        </>
      ) : null}
      {buildOpen ? (
        <BuildToolbar
          activeBuildTypeId={buildTypeId}
          onSelect={handleBuildSelect}
          unlockedTechIds={simResources?.unlockedTechIds ?? []}
          onClose={() => setBuildOpen(false)}
        />
      ) : null}

      <div
        style={{
          ...HUD_ZONE.topRightActions,
          display: "flex",
          alignItems: "center",
          gap: 8,
          pointerEvents:
            researchOpen || economyOpen || lawOpen || heraldOpen || helpOpen
              ? "none"
              : "auto",
        }}
      >
        <ResearchButton
          badgeLabel={
            simResources?.techCount !== undefined
              ? String(simResources.techCount)
              : "—"
          }
          title="Open research catalog"
          onClick={() => setResearchOpen(true)}
        />
        <EconomyButton
          title="Goods shortages and surpluses"
          active={economyOpen}
          onClick={() => setEconomyOpen((open) => !open)}
        />
        <LawButton
          title="Law catalog and active ordinances"
          active={lawOpen}
          badgeLabel={
            simResources?.activeLawCount !== undefined
              ? String(simResources.activeLawCount)
              : undefined
          }
          onClick={() => setLawOpen((open) => !open)}
        />
        <CitizenButton
          badgeLabel={
            simResources?.householdCount !== undefined
              ? String(simResources.householdCount)
              : simResources?.population !== undefined
                ? String(simResources.population)
                : "—"
          }
          title="Household stats and citizen drill-down"
          active={citizenOpen}
          onClick={() => setCitizenOpen((open) => !open)}
        />
        <HeraldButton
          embedded
          quotaLabel={entitlementsError ? "!" : formatQuota(quotaRemaining)}
          disabled={heraldDisabled}
          title={
            entitlementsError
              ? entitlementsError
              : heraldDisabled
                ? "Daily narrative quota exhausted"
                : "Open the Daily Herald"
          }
          onClick={() => {
            if (!heraldDisabled) openHerald();
          }}
        />
      </div>

      <ResearchPanel
        open={researchOpen}
        onClose={() => setResearchOpen(false)}
        techCount={simResources?.techCount}
        researchPoints={simResources?.researchPoints}
        researchRate={simResources?.researchRate}
        currentResearchId={simResources?.currentResearchId}
        currentResearchProgress={simResources?.currentResearchProgress}
        currentResearchMonthsRemaining={simResources?.currentResearchMonthsRemaining}
        unlockedTechIds={simResources?.unlockedTechIds}
        onEnqueueResearch={handleEnqueueResearch}
      />

      <EconomyPanel
        open={economyOpen}
        onClose={() => setEconomyOpen(false)}
        resources={simResources}
      />

      <LawPanel
        open={lawOpen}
        onClose={() => setLawOpen(false)}
        resources={simResources}
        onSetLawActive={handleSetLawActive}
      />

      <CitizenPanel
        open={citizenOpen}
        onClose={() => setCitizenOpen(false)}
        resources={simResources}
        selection={citizenSelection}
        onSelectHousehold={handleSelectHousehold}
      />

      <HudToastStack>
        <ResearchUnlockToast
          unlock={researchUnlock}
          onDismiss={() => setResearchUnlock(null)}
        />
        <HudToast
          message={cashCrisisToast?.message ?? null}
          severity={cashCrisisToast?.severity}
          onDismiss={() => setCashCrisisToast(null)}
        />
      </HudToastStack>

      <HeraldPanel
        open={heraldOpen}
        onClose={() => {
          setHeraldOpen(false);
          setHeraldSpecialEdition(false);
        }}
        event={heraldEvent}
        loading={heraldLoading}
        error={heraldError}
        quotaRemaining={quotaRemaining}
        specialEdition={heraldSpecialEdition}
        bucketReason={heraldBucketReason}
        onOptionSelect={heraldSpecialEdition ? undefined : handleHeraldOptionSelect}
      />

      <OnboardingOverlay
        residentialZonePainted={residentialZonePainted}
        demandVisible={resolveRci(simResources) !== null}
        heraldOpen={heraldOpen}
        researchOpen={researchOpen}
      />

      <CrisisWarningModal resources={simResources} />

      <HelpPanel open={helpOpen} onClose={() => setHelpOpen(false)} />

      <EraTransitionModal era={eraTransitionEra} onDismiss={dismissEraTransition} />

      <NewsTicker
        activeEvents={simResources?.activeEvents}
        simResources={simResources}
      />
    </div>
  );
}
