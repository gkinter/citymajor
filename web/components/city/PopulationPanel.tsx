"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import { formatGrowthPerMonth } from "@/lib/population-growth";
import { aggregateStatsFromResources } from "@/lib/population-l2";
import type { SimResources } from "@/lib/sim-bridge";

type PopulationPanelProps = {
  resources: SimResources | null;
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 6,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const popValueStyle: CSSProperties = {
  fontSize: 20,
  fontWeight: 700,
  fontVariantNumeric: "tabular-nums",
  color: HUD_COLORS.text,
};

export function PopulationPanel({ resources }: PopulationPanelProps) {
  const stats = aggregateStatsFromResources(resources);
  if (!stats) return null;

  return (
    <aside
      aria-label="Population"
      data-onboarding-target="population"
      style={{
        ...HUD_ZONE.leftStack,
        ...hudInfoPanel({ minWidth: 140 }),
      }}
    >
      <div style={sectionTitle}>Population</div>
      <div>
        <span style={popValueStyle}>{stats.population.toLocaleString()}</span>
        {stats.populationGrowthRate !== undefined ? (
          <span
            style={{ marginLeft: 8, fontSize: 11, opacity: 0.85 }}
            title="Net population change per game month"
          >
            ({formatGrowthPerMonth(stats.populationGrowthRate)})
          </span>
        ) : null}
      </div>
      {stats.householdCount > 0 ? (
        <div style={{ marginTop: 4, fontSize: 11, opacity: 0.75 }}>
          <span style={hudLabel()}>Homes</span>
          {stats.householdCount.toLocaleString()}
        </div>
      ) : null}
    </aside>
  );
}
