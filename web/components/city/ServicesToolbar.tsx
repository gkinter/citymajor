"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import {
  COVERAGE_HEAT_LEGEND,
  SERVICE_COVERAGE_MODES,
  type ServiceCoverageMode,
} from "@/lib/service-coverage";
import type { ServiceViewMode } from "@/lib/sim-bridge";

type ServicesToolbarProps = {
  viewMode: ServiceViewMode;
  onViewModeChange: (mode: ServiceViewMode) => void;
  healthcareCoverage?: number;
  policeCoverage?: number;
  fireCoverage?: number;
};

function formatPct(value: number | undefined): string {
  if (value === undefined) return "—";
  return `${Math.round(value * 100)}%`;
}

export function ServicesToolbar({
  viewMode,
  onViewModeChange,
  healthcareCoverage,
  policeCoverage,
  fireCoverage,
}: ServicesToolbarProps) {
  const overlayOn = viewMode !== "off";
  const coveragePct: Record<ServiceCoverageMode, number | undefined> = {
    health: healthcareCoverage,
    police: policeCoverage,
    fire: fireCoverage,
  };

  return (
    <div
      style={{
        ...HUD_ZONE.bottomLeft,
        display: "flex",
        flexDirection: "column",
        gap: 6,
        pointerEvents: "auto",
      }}
    >
      <div
        data-testid="services-toolbar"
        style={hudToolbar()}
        role="toolbar"
        aria-label="Services coverage overlay"
      >
        <span
          style={{
            opacity: 0.7,
            alignSelf: "center",
            marginRight: 4,
            color: HUD_COLORS.textMuted,
          }}
        >
          Services
        </span>
        <button
          type="button"
          style={{ ...hudButton(overlayOn), minWidth: 44 }}
          aria-pressed={overlayOn}
          title="Show service coverage overlay (defaults to Health)"
          onClick={() => onViewModeChange(overlayOn ? viewMode : "health")}
        >
          On
        </button>
        <button
          type="button"
          style={{ ...hudButton(!overlayOn), minWidth: 44 }}
          aria-pressed={!overlayOn}
          title="Hide service coverage overlay"
          onClick={() => onViewModeChange("off")}
        >
          Off
        </button>
        {SERVICE_COVERAGE_MODES.map(({ id, label, shortLabel, overlayColor }) => (
          <button
            key={id}
            type="button"
            data-testid={`services-mode-${id}`}
            style={{
              ...hudButton(viewMode === id),
              minWidth: 58,
              display: "flex",
              alignItems: "center",
              gap: 6,
              opacity: overlayOn ? 1 : 0.45,
            }}
            aria-label={label}
            aria-pressed={viewMode === id}
            disabled={!overlayOn}
            title={
              overlayOn
                ? `Show ${label.toLowerCase()} coverage`
                : "Turn Services On to pick a coverage mode"
            }
            onClick={() => onViewModeChange(id)}
          >
            <span
              style={{
                width: 8,
                height: 8,
                borderRadius: 2,
                background: overlayColor,
                flexShrink: 0,
              }}
              aria-hidden
            />
            {shortLabel}
          </button>
        ))}
        <span
          style={{
            marginLeft: 6,
            fontSize: 11,
            color: HUD_COLORS.textDim,
            fontFamily: "ui-monospace, monospace",
            alignSelf: "center",
          }}
          title="City-wide mean coverage over zoned tiles"
        >
          {SERVICE_COVERAGE_MODES.map(({ id, shortLabel, overlayColor }, index) => (
            <span key={id}>
              {index > 0 ? " · " : null}
              <span style={{ color: overlayColor }}>{shortLabel}</span>{" "}
              {formatPct(coveragePct[id])}
            </span>
          ))}
        </span>
      </div>

      {overlayOn ? (
        <div
          data-testid="services-legend"
          style={{
            ...hudToolbar(),
            padding: "3px 10px",
            fontSize: 10,
            gap: 8,
            opacity: 0.85,
          }}
          aria-label="Coverage heat legend"
        >
          <span style={{ color: HUD_COLORS.textMuted, marginRight: 2 }}>
            Coverage
          </span>
          {COVERAGE_HEAT_LEGEND.map(({ label, color }) => (
            <span
              key={label}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 4,
                color: HUD_COLORS.textMuted,
              }}
              title={`${label} service coverage`}
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: 2,
                  background: color,
                  flexShrink: 0,
                }}
                aria-hidden
              />
              {label}
            </span>
          ))}
        </div>
      ) : null}
    </div>
  );
}
