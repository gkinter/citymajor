"use client";

import {
  HUD_COLORS,
  HUD_ZONE,
  hudButton,
  hudToolbar,
} from "@/lib/hud-theme";
import type { ServiceViewMode } from "@/lib/sim-bridge";

type ServicesToolbarProps = {
  viewMode: ServiceViewMode;
  onViewModeChange: (mode: ServiceViewMode) => void;
  healthcareCoverage?: number;
  policeCoverage?: number;
  fireCoverage?: number;
};

const SUBMODES: { id: Exclude<ServiceViewMode, "off" | "education">; label: string }[] = [
  { id: "health", label: "Health" },
  { id: "police", label: "Police" },
  { id: "fire", label: "Fire" },
];

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

  return (
    <div
      data-testid="services-toolbar"
      style={hudToolbar(HUD_ZONE.bottomLeft)}
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
      {SUBMODES.map(({ id, label }) => (
        <button
          key={id}
          type="button"
          style={{
            ...hudButton(viewMode === id),
            minWidth: 58,
            opacity: overlayOn ? 1 : 0.45,
          }}
          aria-pressed={viewMode === id}
          disabled={!overlayOn}
          title={
            overlayOn
              ? `Show ${label.toLowerCase()} coverage`
              : "Turn Services On to pick a coverage mode"
          }
          onClick={() => onViewModeChange(id)}
        >
          {label}
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
        H {formatPct(healthcareCoverage)} · P {formatPct(policeCoverage)} · F{" "}
        {formatPct(fireCoverage)}
      </span>
    </div>
  );
}
