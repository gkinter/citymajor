"use client";

import type { CSSProperties } from "react";
import { hudEraBadgeStyle, hudEraName } from "@/lib/era";
import {
  HUD_ZONE,
  hudLabel,
  hudPanel,
} from "@/lib/hud-theme";
import type { SimResources } from "@/lib/sim-bridge";

function formatFunds(cityFunds: number): string {
  const abs = Math.abs(cityFunds);
  if (abs >= 1_000_000) return `$${(cityFunds / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `$${(cityFunds / 1_000).toFixed(1)}K`;
  return `$${cityFunds.toLocaleString()}`;
}

function formatPopulation(population: number): string {
  return population.toLocaleString();
}

type ResourcesHudProps = {
  resources: SimResources | null;
};

export function ResourcesHud({ resources }: ResourcesHudProps) {
  const panelStyle = hudPanel({
    ...HUD_ZONE.resources,
    display: "flex",
    gap: 20,
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

  return (
    <div style={panelStyle}>
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
  );
}
