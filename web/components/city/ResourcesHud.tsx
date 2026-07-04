"use client";

import type { CSSProperties } from "react";
import type { SimResources } from "@/lib/sim-bridge";
import { hudEraBadgeStyle, hudEraName } from "@/lib/era";

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
  const panelStyle: CSSProperties = {
    position: "absolute",
    top: 12,
    left: "50%",
    transform: "translateX(-50%)",
    display: "flex",
    gap: 20,
    padding: "8px 16px",
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
    fontSize: 12,
    lineHeight: 1.4,
    color: "#e8eef8",
    background: "rgba(8, 12, 24, 0.82)",
    border: "1px solid rgba(120, 160, 220, 0.25)",
    borderRadius: 8,
    pointerEvents: "none",
    whiteSpace: "nowrap",
  };

  const labelStyle: CSSProperties = {
    opacity: 0.65,
    marginRight: 6,
  };

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
        <span style={labelStyle}>Pop</span>
        {resources ? formatPopulation(resources.population) : "—"}
      </div>
      <div>
        <span style={labelStyle}>Funds</span>
        {resources ? formatFunds(resources.cityFunds) : "—"}
      </div>
      <div>
        <span style={labelStyle}>Tick</span>
        {resources ? resources.tick.toLocaleString() : "—"}
      </div>
      <div>
        <span style={labelStyle}>Era</span>
        {resources ? (
          <span style={eraBadgeStyle}>{hudEraName(era)}</span>
        ) : (
          "—"
        )}
      </div>
    </div>
  );
}
