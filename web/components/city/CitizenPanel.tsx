"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  hudActionButton,
  hudSlidePanel,
} from "@/lib/hud-theme";
import { formatGrowthPerMonth } from "@/lib/population-growth";
import {
  aggregateStatsFromResources,
  formatCommuteMin,
  formatHappiness,
  householdsFromResources,
  resolveSelectedHousehold,
  type HouseholdPreview,
} from "@/lib/population-l2";
import type { SimResources } from "@/lib/sim-bridge";

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const bodyStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "16px 18px 24px",
};

const statRowStyle: CSSProperties = {
  display: "flex",
  gap: 16,
  marginBottom: 16,
  fontSize: 12,
  flexWrap: "wrap",
};

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 8,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const fallbackNote: CSSProperties = {
  marginBottom: 14,
  padding: "8px 10px",
  borderRadius: 6,
  fontSize: 11,
  lineHeight: 1.4,
  color: HUD_COLORS.textMuted,
  background: HUD_COLORS.rowBg,
  border: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const selectedCardStyle: CSSProperties = {
  marginBottom: 16,
  padding: "12px 14px",
  borderRadius: 8,
  border: `1px solid rgba(255, 184, 77, 0.55)`,
  background: "rgba(255, 184, 77, 0.08)",
};

type CitizenSelection = {
  householdId?: string;
  tileX?: number;
  tileZ?: number;
};

type CitizenPanelProps = {
  open: boolean;
  onClose: () => void;
  resources: SimResources | null;
  selection?: CitizenSelection | null;
  onSelectHousehold?: (household: HouseholdPreview) => void;
};

function formatPopulation(value: number): string {
  return value.toLocaleString();
}

function StatBlock({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="hud-citizen-stat__label">{label}</span>
      <span className="hud-citizen-stat__value">{value}</span>
    </div>
  );
}

function HouseholdCard({
  household,
  selected,
  onSelect,
}: {
  household: HouseholdPreview;
  selected: boolean;
  onSelect?: () => void;
}) {
  const content = (
    <>
      <div className="hud-citizen-list__row">
        <span className="hud-citizen-list__name">{household.id}</span>
        <span className="hud-citizen-list__happiness">
          {formatHappiness(household.happiness)}
        </span>
      </div>
      <div className="hud-citizen-list__meta">
        Tile ({household.tileX}, {household.tileZ}) · Commute{" "}
        {formatCommuteMin(household.commuteMin)}
      </div>
    </>
  );

  if (!onSelect) {
    return (
      <li
        className={`hud-citizen-list__item${selected ? " hud-citizen-list__item--selected" : ""}`}
      >
        {content}
      </li>
    );
  }

  return (
    <li
      className={`hud-citizen-list__item${selected ? " hud-citizen-list__item--selected" : ""}`}
    >
      <button
        type="button"
        className="hud-citizen-list__select"
        onClick={onSelect}
        aria-label={`View household ${household.id}`}
        style={{
          display: "block",
          width: "100%",
          margin: 0,
          padding: 0,
          border: "none",
          background: "transparent",
          color: "inherit",
          font: "inherit",
          textAlign: "left",
          cursor: "pointer",
        }}
      >
        {content}
      </button>
    </li>
  );
}

export function CitizenPanel({
  open,
  onClose,
  resources,
  selection = null,
  onSelectHousehold,
}: CitizenPanelProps) {
  if (!open) return null;

  const aggregate = aggregateStatsFromResources(resources);
  const households = householdsFromResources(resources);
  const hasL2 = households.length > 0;
  const selectedHousehold = resolveSelectedHousehold(resources, selection);
  const growthRate = resources?.populationGrowthRate;

  return (
    <aside
      className="hud-citizen-panel"
      style={hudSlidePanel()}
      role="dialog"
      aria-label="Citizens"
      aria-modal="true"
    >
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 15, letterSpacing: "0.04em" }}>
            Citizens
          </div>
          <div style={{ opacity: 0.65, fontSize: 11, marginTop: 2 }}>
            {aggregate
              ? `${formatPopulation(aggregate.householdCount)} households · ${formatPopulation(aggregate.population)} residents`
              : "Awaiting simulation data"}
          </div>
        </div>
        <button
          type="button"
          style={{ ...hudActionButton(), padding: "4px 10px", fontSize: 12 }}
          onClick={onClose}
          aria-label="Close"
        >
          ✕
        </button>
      </header>

      <div style={bodyStyle}>
        {aggregate ? (
          <div style={statRowStyle} aria-label="City-wide citizen stats">
            <StatBlock
              label="Population"
              value={formatPopulation(aggregate.population)}
            />
            <StatBlock
              label="Households"
              value={formatPopulation(aggregate.householdCount)}
            />
            {aggregate.happiness !== undefined ? (
              <StatBlock
                label="Happiness"
                value={formatHappiness(aggregate.happiness)}
              />
            ) : aggregate.avgHappiness !== undefined ? (
              <StatBlock
                label="Avg happiness"
                value={formatHappiness(aggregate.avgHappiness)}
              />
            ) : null}
            {growthRate !== undefined ? (
              <StatBlock label="Growth" value={formatGrowthPerMonth(growthRate)} />
            ) : null}
            {aggregate.avgCommuteMin !== undefined ? (
              <StatBlock
                label="Avg commute"
                value={formatCommuteMin(aggregate.avgCommuteMin)}
              />
            ) : null}
          </div>
        ) : null}

        {selectedHousehold ? (
          <section style={selectedCardStyle} aria-label="Selected household">
            <div style={{ ...sectionTitle, color: "#ffd080" }}>Selected</div>
            <div className="hud-citizen-list__row">
              <span className="hud-citizen-list__name">{selectedHousehold.id}</span>
              <span className="hud-citizen-list__happiness">
                {formatHappiness(selectedHousehold.happiness)}
              </span>
            </div>
            <div className="hud-citizen-list__meta">
              Home tile ({selectedHousehold.tileX}, {selectedHousehold.tileZ})
            </div>
            <div className="hud-citizen-list__meta">
              Commute {formatCommuteMin(selectedHousehold.commuteMin)}
            </div>
          </section>
        ) : selection?.tileX !== undefined && selection.tileZ !== undefined ? (
          <section style={selectedCardStyle} aria-label="Selected household stub">
            <div style={{ ...sectionTitle, color: "#ffd080" }}>Selected</div>
            <p style={{ margin: 0, fontSize: 12, lineHeight: 1.45 }}>
              Residential tile ({selection.tileX}, {selection.tileZ})
            </p>
            <p
              style={{
                margin: "8px 0 0",
                fontSize: 11,
                lineHeight: 1.4,
                color: HUD_COLORS.textMuted,
              }}
            >
              Named household detail will appear here once WASM exports a
              population L2 sample for this tile.
            </p>
          </section>
        ) : null}

        {hasL2 ? (
          <>
            <div className="hud-citizen-list__heading">
              Named households ({households.length})
            </div>
            <ul className="hud-citizen-list">
              {households.map((household) => (
                <HouseholdCard
                  key={household.id}
                  household={household}
                  selected={selectedHousehold?.id === household.id}
                  onSelect={
                    onSelectHousehold
                      ? () => onSelectHousehold(household)
                      : undefined
                  }
                />
              ))}
            </ul>
          </>
        ) : (
          <p style={fallbackNote}>
            Aggregate stats reflect the live WASM snapshot. A named household
            list with happiness and commute fields will populate when the sim
            exports <code style={{ fontSize: 10 }}>populationL2.households</code>.
            Click a citizen dot on the map to drill down.
          </p>
        )}
      </div>
    </aside>
  );
}
