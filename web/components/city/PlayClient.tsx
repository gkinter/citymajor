"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { z } from "zod";
import { EntitlementsSchema, type Entitlements } from "@/lib/entitlements";
import {
  NarrativeEventResponseSchema,
  type NarrativeEventResponse,
  type SimStateBucket,
} from "@/lib/narrative-templates";
import { deriveNarrativeBucket } from "@/lib/sim-metrics";
import type { GameSpeedLevel, SimClientApi, SimResources } from "@/lib/sim-bridge";
import {
  GRAPHICS_QUALITY_STORAGE_KEY,
  type GraphicsQualityTier,
} from "@/lib/constants";
import type { FpsStats } from "@/lib/types";
import type { ZoningTool } from "@/lib/zoning";
import { CityCanvas } from "@/components/city/CityCanvas";
import { FpsHud } from "@/components/city/FpsHud";
import { HeraldPanel } from "@/components/city/HeraldPanel";
import { QualityToolbar } from "@/components/city/QualityToolbar";
import { ResourcesHud } from "@/components/city/ResourcesHud";
import { SaveLoadControls } from "@/components/city/SaveLoadControls";
import { SpeedToolbar } from "@/components/city/SpeedToolbar";
import { ZoningToolbar } from "@/components/city/ZoningToolbar";

const NarrativeApiResponseSchema = NarrativeEventResponseSchema.extend({
  narrativeEventsRemaining: z.number().int().nonnegative().optional(),
});

const heraldButtonStyle = {
  position: "absolute" as const,
  top: 12,
  right: 12,
  zIndex: 15,
  display: "flex",
  alignItems: "center",
  gap: 8,
  padding: "8px 14px",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 12,
  fontWeight: 600,
  color: "#e8eef8",
  background: "rgba(8, 12, 24, 0.82)",
  border: "1px solid rgba(120, 160, 220, 0.25)",
  borderRadius: 8,
  cursor: "pointer",
};

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

export function PlayClient() {
  const [activeTool, setActiveTool] = useState<ZoningTool>("residential");
  const [gameSpeed, setGameSpeed] = useState<GameSpeedLevel>(1);
  const [qualityTier, setQualityTier] = useState<GraphicsQualityTier>(() =>
    readStoredQualityTier(),
  );
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
  const [simApi, setSimApi] = useState<SimClientApi | null>(null);
  const [heraldOpen, setHeraldOpen] = useState(false);
  const [heraldLoading, setHeraldLoading] = useState(false);
  const [heraldError, setHeraldError] = useState<string | null>(null);
  const [heraldEvent, setHeraldEvent] = useState<NarrativeEventResponse | null>(null);

  const statsRef = useRef(stats);
  statsRef.current = stats;

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

  const handleQualityChange = useCallback((tier: GraphicsQualityTier) => {
    setQualityTier(tier);
    window.localStorage.setItem(GRAPHICS_QUALITY_STORAGE_KEY, tier);
  }, []);

  const fetchHeraldStory = useCallback(async () => {
    const coverage = statsRef.current.healthcareCoverage ?? 0.5;
    const bucket: SimStateBucket = deriveNarrativeBucket(coverage);

    setHeraldLoading(true);
    setHeraldError(null);
    setHeraldEvent(null);

    try {
      const res = await fetch("/api/narrative/event", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          bucket,
          context: { metricValue: coverage },
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

  const openHerald = useCallback(() => {
    setHeraldOpen(true);
    void fetchHeraldStory();
  }, [fetchHeraldStory]);

  const quotaRemaining = entitlements?.narrativeEventsRemaining;
  const heraldDisabled =
    quotaRemaining !== undefined &&
    quotaRemaining !== Number.MAX_SAFE_INTEGER &&
    quotaRemaining <= 0;

  return (
    <div style={{ width: "100vw", height: "100vh", position: "relative" }}>
      <CityCanvas
        activeTool={activeTool}
        gameSpeed={gameSpeed}
        qualityTier={qualityTier}
        onStats={setStats}
        onSimResources={setSimResources}
        onSimApi={setSimApi}
      />
      <SpeedToolbar speedLevel={gameSpeed} onSpeedChange={setGameSpeed} />
      <QualityToolbar qualityTier={qualityTier} onQualityChange={handleQualityChange} />
      <SaveLoadControls
        simApi={simApi}
        entitlements={entitlements}
        onSlotsChanged={refreshEntitlements}
      />
      <ResourcesHud resources={simResources} />
      <FpsHud
        stats={stats}
        totalBuildings={stats.totalBuildings}
        activeTool={activeTool}
      />
      <ZoningToolbar activeTool={activeTool} onToolChange={setActiveTool} />

      <button
        type="button"
        style={{
          ...heraldButtonStyle,
          opacity: heraldDisabled ? 0.5 : 1,
          cursor: heraldDisabled ? "not-allowed" : "pointer",
        }}
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
      >
        <span>Herald</span>
        <span
          style={{
            padding: "2px 6px",
            borderRadius: 4,
            background: "rgba(94, 200, 255, 0.15)",
            border: "1px solid rgba(94, 200, 255, 0.35)",
            fontSize: 11,
            fontWeight: 500,
          }}
        >
          {entitlementsError ? "!" : formatQuota(quotaRemaining)}
        </span>
      </button>

      <HeraldPanel
        open={heraldOpen}
        onClose={() => setHeraldOpen(false)}
        event={heraldEvent}
        loading={heraldLoading}
        error={heraldError}
        quotaRemaining={quotaRemaining}
      />
    </div>
  );
}
