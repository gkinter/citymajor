"use client";

import { useCallback, useEffect, useRef, useState } from "react";
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
import { techNameFromIndex } from "@/lib/tech-catalog";
import { HUD_ZONE } from "@/lib/hud-theme";
import { HudWordmark } from "@/components/city/HudWordmark";
import { QualityToolbar } from "@/components/city/QualityToolbar";
import { TrafficOverlayToggle } from "@/components/city/TrafficOverlayToggle";
import { ResourcesHud } from "@/components/city/ResourcesHud";
import { EraProgressPanel } from "@/components/city/EraProgressPanel";
import { SaveLoadControls } from "@/components/city/SaveLoadControls";
import { SpeedToolbar } from "@/components/city/SpeedToolbar";
import { ZoningToolbar } from "@/components/city/ZoningToolbar";
import { ServicesToolbar } from "@/components/city/ServicesToolbar";
import type { ServiceViewMode } from "@/lib/sim-bridge";
import {
  isResidentialZonePaint,
  OnboardingOverlay,
} from "@/components/city/OnboardingOverlay";
import { resolveRci } from "@/lib/zoning-economy";
import { GltfPreloader } from "@/components/city/GltfPreloader";
import type { HouseholdPreview } from "@/lib/population-l2";

const NarrativeApiResponseSchema = NarrativeEventResponseSchema.extend({
  narrativeEventsRemaining: z.number().int().nonnegative().optional(),
});

function formatQuota(remaining: number | undefined): string {
  if (remaining === undefined) return "…";
  if (remaining === Number.MAX_SAFE_INTEGER) return "∞";
  return String(remaining);
}

function readStoredQualityTier(): GraphicsQualityTier {
  if (typeof window === "undefined") return "high";
  const stored = window.localStorage.getItem(GRAPHICS_QUALITY_STORAGE_KEY);
  return stored === "low" ? "low" : "high";
}

function readStoredTrafficOverlay(): boolean {
  if (typeof window === "undefined") return true;
  const stored = window.localStorage.getItem(TRAFFIC_OVERLAY_STORAGE_KEY);
  return stored !== "off";
}

export function PlayClient() {
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
  const [researchToast, setResearchToast] = useState<string | null>(null);
  const [cashCrisisToast, setCashCrisisToast] = useState<string | null>(null);
  const [residentialZonePainted, setResidentialZonePainted] = useState(false);

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
    const newlyUnlocked = ids.filter((id) => !prev.has(id));
    prevUnlockedTechRef.current = new Set(ids);

    if (newlyUnlocked.length === 0) return;

    const names = newlyUnlocked
      .map((id) => techNameFromIndex(id))
      .filter((name): name is string => Boolean(name));

    if (names.length === 0) return;

    const label =
      names.length === 1
        ? names[0]
        : `${names.slice(0, 2).join(", ")}${names.length > 2 ? ` +${names.length - 2}` : ""}`;

    setResearchToast(`Research complete: ${label}`);
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

    setCashCrisisToast(crisis.message);
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

  const handleZonePainted = useCallback((zoneType: number) => {
    if (isResidentialZonePaint(zoneType)) {
      setResidentialZonePainted(true);
    }
  }, []);

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
        activeTool={activeTool}
        brushSize={brushSize}
        gameSpeed={gameSpeed}
        qualityTier={qualityTier}
        showTrafficOverlay={showTrafficOverlay}
        serviceViewMode={serviceViewMode}
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
      <TrafficOverlayToggle
        enabled={showTrafficOverlay}
        onToggle={handleTrafficOverlayToggle}
      />
      <ServicesToolbar
        viewMode={serviceViewMode}
        onViewModeChange={setServiceViewMode}
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
      <EraProgressPanel resources={simResources} />
      <FpsHud
        stats={stats}
        totalBuildings={stats.totalBuildings}
        activeTool={activeTool}
      />
      <ZoningToolbar
        activeTool={activeTool}
        brushSize={brushSize}
        onToolChange={setActiveTool}
        onBrushSizeChange={setBrushSize}
        rci={resolveRci(simResources)}
      />

      <div
        style={{
          ...HUD_ZONE.topRightActions,
          display: "flex",
          alignItems: "center",
          gap: 8,
          pointerEvents:
            researchOpen || economyOpen || lawOpen || heraldOpen
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
      />

      <HudToast
        message={researchToast}
        onDismiss={() => setResearchToast(null)}
        style={{ top: 130 }}
      />

      <HudToast
        message={cashCrisisToast}
        onDismiss={() => setCashCrisisToast(null)}
        style={{ top: researchToast ? 168 : 130 }}
      />

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

      <EraTransitionModal era={eraTransitionEra} onDismiss={dismissEraTransition} />

      <NewsTicker
        activeEvents={simResources?.activeEvents}
        simResources={simResources}
      />
    </div>
  );
}
