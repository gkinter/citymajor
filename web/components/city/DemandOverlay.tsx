"use client";

import type { CSSProperties } from "react";
import { HUD_ZONE, hudPanel } from "@/lib/hud-theme";
import type { RciDemand } from "@/lib/sim-bridge";

function clampDemand(value: number): number {
  return Math.max(-1, Math.min(1, value));
}

function demandToDisplay(normalized: number): number {
  return Math.round(clampDemand(normalized) * 100);
}

type RciMeterProps = {
  label: "R" | "C" | "I";
  demand: number;
};

function RciMeter({ label, demand }: RciMeterProps) {
  const clamped = clampDemand(demand);
  const display = demandToDisplay(clamped);
  const halfPct = Math.abs(clamped) * 50;
  const fillStyle: CSSProperties =
    clamped >= 0
      ? { left: "50%", width: `${halfPct}%` }
      : { left: `${50 - halfPct}%`, width: `${halfPct}%` };

  return (
    <div className="hud-rci__row">
      <span className={`hud-rci__label hud-rci__label--${label.toLowerCase()}`}>
        {label}
      </span>
      <div className="hud-rci__track" aria-hidden>
        <div
          className={`hud-rci__fill hud-rci__fill--${label.toLowerCase()}`}
          style={fillStyle}
        />
      </div>
      <span className="hud-rci__value">
        {display > 0 ? `+${display}` : display}
      </span>
    </div>
  );
}

type DemandOverlayProps = {
  rci: RciDemand | null;
};

/** Bottom-center R/C/I demand bars — classic SimCity-style meters. */
export function DemandOverlay({ rci }: DemandOverlayProps) {
  if (!rci) return null;

  const panelStyle = hudPanel({
    ...HUD_ZONE.bottomCenter,
    bottom: 96,
    display: "flex",
    flexDirection: "column",
    gap: 3,
    padding: "6px 12px",
    pointerEvents: "none",
  });

  return (
    <div
      style={panelStyle}
      className="hud-rci hud-rci--panel"
      aria-label="RCI demand"
      title="R/C/I demand — high values mean zone that type to grow"
      data-onboarding-target="demand"
    >
      <RciMeter label="R" demand={rci.residential} />
      <RciMeter label="C" demand={rci.commercial} />
      <RciMeter label="I" demand={rci.industrial} />
    </div>
  );
}
