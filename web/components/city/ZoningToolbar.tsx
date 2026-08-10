"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudLabel,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  isPaintFeedbackEnabled,
  setPaintFeedbackEnabled,
} from "@/lib/paint-feedback";
import type { RciDemand } from "@/lib/sim-bridge";
import {
  isZoningDemandHigh,
  zoningDemandHint,
} from "@/lib/zoning-economy";
import type { PaintBrushSize, ZoningTool, ZoneDensityLevel } from "@/lib/zoning";
import {
  nextBrushSize,
  nextZoneDensity,
  ZONE_DENSITY_LABELS,
  zoningToolColor,
  zoningToolShortLabel,
} from "@/lib/zoning";
import {
  getVisibleZoneTiers,
  isZoneTierEraUnlocked,
  isZoneTierUnlocked,
  isZoneTierVisible,
  lockedTierEraName,
  lockedTierTechName,
  zoneTierByTool,
  type ZoneTierTool,
} from "@/lib/zone-tiers";
import { useCallback, useEffect, useMemo, useState } from "react";

type ZoningToolbarProps = {
  activeTool: ZoningTool;
  brushSize: PaintBrushSize;
  zoneDensity: ZoneDensityLevel;
  onToolChange: (tool: ZoningTool) => void;
  onBrushSizeChange: (size: PaintBrushSize) => void;
  onZoneDensityChange: (density: ZoneDensityLevel) => void;
  /** Live RCI demand — drives tooltips and high-demand highlights. */
  rci?: RciDemand | null;
  /** WASM research indices — gates advanced zone tiers when omitted. */
  unlockedTechIds?: number[];
  /** WASM sim era index (0=Frontier) — P2.1 era gates for office/mixed. */
  currentEra?: number;
  /** True when WASM PaintZone is live (extended zone bytes 5–7). */
  supportsZoneBytes?: boolean;
};

function isZoneTierTool(tool: ZoningTool): tool is ZoneTierTool {
  return tool !== "bulldoze" && tool !== "road";
}

export function ZoningToolbar({
  activeTool,
  brushSize,
  zoneDensity,
  onToolChange,
  onBrushSizeChange,
  onZoneDensityChange,
  rci = null,
  unlockedTechIds,
  currentEra,
  supportsZoneBytes = false,
}: ZoningToolbarProps) {
  const [feedbackOn, setFeedbackOn] = useState(false);

  useEffect(() => {
    setFeedbackOn(isPaintFeedbackEnabled());
  }, []);

  const unlockedTechSet = useMemo(
    () => new Set(unlockedTechIds ?? []),
    [unlockedTechIds],
  );

  const visibleTiers = useMemo(
    () => getVisibleZoneTiers(supportsZoneBytes),
    [supportsZoneBytes],
  );

  const zoningTools = useMemo(
    () => [
      ...visibleTiers.map((tier) => ({
        id: tier.tool,
        label: tier.label,
        shortLabel: tier.shortLabel,
      })),
      { id: "bulldoze" as const, label: "Bulldoze", shortLabel: "✕" },
      { id: "road" as const, label: "Road", shortLabel: "Rd", stub: true },
    ],
    [visibleTiers],
  );

  const activeColor = zoningToolColor(activeTool);
  const showBrush = true;

  const toggleFeedback = useCallback(() => {
    const next = !feedbackOn;
    setPaintFeedbackEnabled(next);
    setFeedbackOn(next);
  }, [feedbackOn]);

  const cycleBrush = useCallback(() => {
    onBrushSizeChange(nextBrushSize(brushSize));
  }, [brushSize, onBrushSizeChange]);

  const cycleDensity = useCallback(() => {
    onZoneDensityChange(nextZoneDensity(zoneDensity));
  }, [zoneDensity, onZoneDensityChange]);

  const isToolUnlocked = useCallback(
    (tool: ZoningTool): boolean => {
      if (!isZoneTierTool(tool)) return true;
      const tier = zoneTierByTool(tool);
      if (!tier) return true;
      if (!isZoneTierVisible(tier, supportsZoneBytes)) return false;
      return isZoneTierUnlocked(tier, [...unlockedTechSet], currentEra);
    },
    [currentEra, supportsZoneBytes, unlockedTechSet],
  );

  const toolTitle = useCallback(
    (tool: ZoningTool, label: string, stub?: boolean): string => {
      if (stub) return "Road tool — coming soon";

      if (isZoneTierTool(tool)) {
        const tier = zoneTierByTool(tool);
        if (tier && !isZoneTierVisible(tier, supportsZoneBytes)) {
          return "Requires WASM sim (extended zone types)";
        }
        if (tier && !isZoneTierEraUnlocked(tier, currentEra)) {
          const eraName = lockedTierEraName(tier);
          return eraName
            ? `Locked — reach ${eraName} era`
            : "Locked — era requirement not met";
        }
        if (tier && !isZoneTierUnlocked(tier, [...unlockedTechSet], currentEra)) {
          const techName = lockedTierTechName(tier);
          return techName
            ? `Locked — research ${techName}`
            : `Locked — research required`;
        }
      }

      const demandHint = zoningDemandHint(tool, rci);
      return demandHint ? `${label}: ${demandHint}` : label;
    },
    [rci, currentEra, supportsZoneBytes, unlockedTechSet],
  );

  return (
    <div
      data-testid="zoning-toolbar"
      style={{ ...HUD_ZONE.bottomCenter, display: "flex", flexDirection: "column", alignItems: "center", gap: 6, pointerEvents: "auto" }}
    >
      <div
        style={{
          ...hudToolbar(),
          alignItems: "center",
          padding: "4px 10px",
          fontSize: 11,
          gap: 10,
        }}
        aria-live="polite"
      >
        <span
          style={{
            width: 10,
            height: 10,
            borderRadius: 2,
            background: activeColor,
            boxShadow: `0 0 8px ${activeColor}88`,
            flexShrink: 0,
          }}
          aria-hidden
        />
        <span>
          <span style={hudLabel()}>Zone</span>
          <strong>{zoningToolShortLabel(activeTool)}</strong>
        </span>
        {showBrush ? (
          <button
            type="button"
            style={{
              ...hudButton(false),
              padding: "2px 8px",
              fontSize: 11,
            }}
            title="Cycle brush size"
            onClick={cycleBrush}
          >
            Brush {brushSize}×{brushSize}
          </button>
        ) : null}
        <button
          type="button"
          data-testid="zone-density-toggle"
          style={{
            ...hudButton(false),
            padding: "2px 8px",
            fontSize: 11,
          }}
          title="Cycle zone density (low / med / high)"
          onClick={cycleDensity}
        >
          Density {ZONE_DENSITY_LABELS[zoneDensity]}
        </button>
        <button
          type="button"
          style={{
            ...hudButton(false),
            padding: "2px 8px",
            fontSize: 11,
            opacity: feedbackOn ? 1 : 0.55,
          }}
          title={
            feedbackOn
              ? "Mute paint sound & haptics"
              : "Enable paint sound & haptics"
          }
          aria-pressed={feedbackOn}
          onClick={toggleFeedback}
        >
          {feedbackOn ? "🔊" : "🔇"}
        </button>
      </div>

      <div
        style={hudToolbar()}
        role="toolbar"
        aria-label="Zoning tools"
        data-onboarding-target="zoning"
      >
        {zoningTools.map(({ id, label, shortLabel, stub }) => {
          const unlocked = isToolUnlocked(id);
          const locked = !unlocked;
          const title = toolTitle(id, label, stub);
          const highDemand = unlocked && !stub && isZoningDemandHigh(id, rci);

          return (
            <button
              key={id}
              type="button"
              data-testid={`zoning-tool-${id}`}
              className={highDemand ? "hud-zoning-btn--demand" : undefined}
              style={hudButton(activeTool === id && unlocked, stub || locked)}
              disabled={stub || locked}
              title={title}
              aria-pressed={activeTool === id}
              aria-disabled={locked || stub}
              onClick={() => {
                if (!stub && unlocked) onToolChange(id);
              }}
            >
              {shortLabel}
              {locked ? " 🔒" : null}
              {highDemand ? " ↑" : null}
              {stub ? " ⏳" : null}
            </button>
          );
        })}
      </div>

      <div
        data-testid="zoning-legend"
        style={{
          ...hudToolbar(),
          padding: "3px 10px",
          fontSize: 10,
          gap: 8,
          opacity: 0.85,
        }}
        aria-label="Zone color legend"
      >
        {visibleTiers.map((tier) => {
          const unlocked = isZoneTierUnlocked(tier, [...unlockedTechSet], currentEra);
          return (
            <span
              key={tier.tool}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 4,
                color: unlocked ? HUD_COLORS.textMuted : HUD_COLORS.textDisabled,
              }}
              title={
                unlocked
                  ? tier.label
                  : lockedTierEraName(tier) ?? lockedTierTechName(tier) ?? "Locked"
              }
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: 2,
                  background: tier.overlayColor,
                  opacity: unlocked ? 1 : 0.35,
                  flexShrink: 0,
                }}
                aria-hidden
              />
              {tier.shortLabel}
            </span>
          );
        })}
      </div>
    </div>
  );
}
