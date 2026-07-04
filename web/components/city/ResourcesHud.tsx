"use client";

import type { CSSProperties } from "react";
import type { SimResources } from "@/lib/sim-bridge";

const ERA_NAMES = [
  "Ancient",
  "Medieval",
  "Colonial",
  "Industrial",
  "Modern",
  "Future",
] as const;

function eraName(era: number): string {
  return ERA_NAMES[era] ?? "Unknown";
}

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
        {resources ? eraName(resources.era) : "—"}
      </div>
    </div>
  );
}
