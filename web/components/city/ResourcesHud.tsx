"use client";

import type { CSSProperties } from "react";
import { hudEraBadgeStyle, hudEraName } from "@/lib/era";
import {
  HUD_ZONE,
  hudLabel,
  hudPanel,
} from "@/lib/hud-theme";
import type { RciDemand, SimResources } from "@/lib/sim-bridge";

function formatFunds(cityFunds: number): string {
  const abs = Math.abs(cityFunds);
  if (abs >= 1_000_000) return `$${(cityFunds / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `$${(cityFunds / 1_000).toFixed(1)}K`;
  return `$${cityFunds.toLocaleString()}`;
}

function formatPopulation(population: number): string {
  return population.toLocaleString();
}

function clampDemand(value: number): number {
  return Math.max(-1, Math.min(1, value));
}

function hasWasmRci(resources: SimResources): boolean {
  return (
    resources.residentialDemand !== undefined &&
    resources.commercialDemand !== undefined &&
    resources.industrialDemand !== undefined
  );
}

/** Fallback when WASM status lacks EconomySystem RCI fields (pre-rebuild bundle). */
function mockRciFromSnapshot(resources: SimResources): RciDemand {
  const pop = resources.population;
  if (pop <= 0) {
    return { residential: 0, commercial: 0, industrial: 0 };
  }
  const eraFactor = Math.min(1, (resources.era ?? 0) / 4);
  return {
    residential: clampDemand(pop / 12_000 - 0.15),
    commercial: clampDemand(pop / 9_000 - 0.2),
    industrial: clampDemand(eraFactor * 0.45 + pop / 20_000 - 0.25),
  };
}

function resolveRci(resources: SimResources | null): RciDemand | null {
  if (!resources) return null;
  if (hasWasmRci(resources)) {
    return {
      residential: clampDemand(resources.residentialDemand!),
      commercial: clampDemand(resources.commercialDemand!),
      industrial: clampDemand(resources.industrialDemand!),
    };
  }
  return mockRciFromSnapshot(resources);
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

type ResourcesHudProps = {
  resources: SimResources | null;
};

export function ResourcesHud({ resources }: ResourcesHudProps) {
  const panelStyle = hudPanel({
    ...HUD_ZONE.resources,
    display: "flex",
    alignItems: "center",
    gap: 12,
    padding: "6px 16px",
    pointerEvents: "none",
    whiteSpace: "nowrap",
  });

  const era = resources?.era ?? 0;
  const eraBadgeStyle: CSSProperties = {
    display: "inline-block",
    marginLeft: 4,
    padding: "1px 8px",
    borderRadius: 4,
    fontWeight: 600,
    fontSize: 11,
    letterSpacing: "0.02em",
    ...hudEraBadgeStyle(era),
  };

  const rci = resolveRci(resources);

  return (
    <div style={panelStyle}>
      <div style={{ display: "flex", gap: 20 }}>
        <div>
          <span style={hudLabel()}>Pop</span>
          {resources ? formatPopulation(resources.population) : "—"}
        </div>
        <div>
          <span style={hudLabel()}>Funds</span>
          {resources ? formatFunds(resources.cityFunds) : "—"}
        </div>
        <div>
          <span style={hudLabel()}>Tick</span>
          {resources ? resources.tick.toLocaleString() : "—"}
        </div>
        <div>
          <span style={hudLabel()}>Era</span>
          {resources ? (
            <span style={eraBadgeStyle}>{hudEraName(era)}</span>
          ) : (
            "—"
          )}
        </div>
      </div>

      {rci ? (
        <div
          className="hud-rci"
          aria-label="RCI demand"
          title="Residential, Commercial, Industrial demand"
        >
          <RciMeter label="R" demand={rci.residential} />
          <RciMeter label="C" demand={rci.commercial} />
          <RciMeter label="I" demand={rci.industrial} />
        </div>
      ) : null}
    </div>
  );
}
