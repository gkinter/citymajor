"use client";

import type { CSSProperties } from "react";
import {
  economyFromRciFallback,
  formatGoodName,
  zoningHintForGood,
} from "@/lib/economy-goods";
import {
  HUD_COLORS,
  hudSlidePanel,
} from "@/lib/hud-theme";
import type { EconomySnapshot, SimResources } from "@/lib/sim-bridge";

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

const sectionTitle: CSSProperties = {
  fontWeight: 700,
  marginBottom: 8,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
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

type EconomyPanelProps = {
  open: boolean;
  onClose: () => void;
  resources: SimResources | null;
};

type DisplayRow = {
  name: string;
  magnitude: number;
  hint?: string;
};

function resolveEconomyView(resources: SimResources | null): {
  shortages: DisplayRow[];
  surpluses: DisplayRow[];
  fallbackLabel?: string;
} {
  const economy = resources?.economy;
  if (economy) {
    return {
      shortages: economy.shortages.map((row) => ({
        name: row.name,
        magnitude: row.magnitude,
        hint: zoningHintForGood(row.name),
      })),
      surpluses: economy.surpluses.map((row) => ({
        name: row.name,
        magnitude: row.magnitude,
        hint: zoningHintForGood(row.name),
      })),
    };
  }

  const fallback = economyFromRciFallback(resources);
  if (!fallback) return { shortages: [], surpluses: [] };

  return {
    fallbackLabel: fallback.label,
    shortages: fallback.shortages.map((row) => ({
      name: row.name,
      magnitude: row.magnitude,
      hint:
        row.name.startsWith("Residential")
          ? "Zone R for housing"
          : row.name.startsWith("Commercial")
            ? "Zone C for shops"
            : row.name.startsWith("Industrial")
              ? "Zone I for jobs"
              : undefined,
    })),
    surpluses: fallback.surpluses.map((row) => ({
      name: row.name,
      magnitude: row.magnitude,
    })),
  };
}

function formatMagnitude(value: number, isRciFallback: boolean): string {
  if (isRciFallback) return value >= 0 ? `+${(value * 100).toFixed(0)}` : `${(value * 100).toFixed(0)}`;
  if (value >= 100) return value.toFixed(0);
  if (value >= 10) return value.toFixed(1);
  return value.toFixed(2);
}

function ImbalanceList({
  title,
  rows,
  tone,
  isRciFallback,
}: {
  title: string;
  rows: DisplayRow[];
  tone: "shortage" | "surplus";
  isRciFallback: boolean;
}) {
  if (rows.length === 0) {
    return (
      <section className="hud-economy-section" aria-label={title}>
        <div
          style={{
            ...sectionTitle,
            color: tone === "shortage" ? "#ff9aa8" : "#7dffb2",
          }}
        >
          {title}
        </div>
        <p className="hud-economy-empty">None detected</p>
      </section>
    );
  }

  return (
    <section className="hud-economy-section" aria-label={title}>
      <div
        style={{
          ...sectionTitle,
          color: tone === "shortage" ? "#ff9aa8" : "#7dffb2",
        }}
      >
        {title}
      </div>
      <ul className="hud-economy-list">
        {rows.map((row) => (
          <li key={row.name} className={`hud-economy-list__item hud-economy-list__item--${tone}`}>
            <div className="hud-economy-list__row">
              <span className="hud-economy-list__name">
                {formatGoodName(row.name)}
              </span>
              <span className="hud-economy-list__magnitude">
                {formatMagnitude(row.magnitude, isRciFallback)}
              </span>
            </div>
            {row.hint ? (
              <div className="hud-economy-list__hint">{row.hint}</div>
            ) : null}
          </li>
        ))}
      </ul>
    </section>
  );
}

export function EconomyPanel({ open, onClose, resources }: EconomyPanelProps) {
  if (!open) return null;

  const view = resolveEconomyView(resources);
  const isRciFallback = Boolean(view.fallbackLabel);

  return (
    <aside
      className="hud-economy-panel"
      aria-label="City economy"
      style={hudSlidePanel()}
    >
      <header style={headerStyle}>
        <div>
          <div style={{ fontWeight: 700, fontSize: 14 }}>Market</div>
          <div style={{ fontSize: 11, opacity: 0.65, marginTop: 2 }}>
            Leontief goods flow
          </div>
        </div>
        <button
          type="button"
          className="hud-economy-close"
          style={{
            padding: "4px 10px",
            fontFamily: "inherit",
            fontSize: 12,
            color: HUD_COLORS.text,
            background: "transparent",
            border: `1px solid ${HUD_COLORS.borderSubtle}`,
            borderRadius: 6,
            cursor: "pointer",
          }}
          onClick={onClose}
        >
          Close
        </button>
      </header>

      <div style={bodyStyle}>
        {view.fallbackLabel ? (
          <p style={fallbackNote}>{view.fallbackLabel}</p>
        ) : null}

        <ImbalanceList
          title="Shortages"
          rows={view.shortages}
          tone="shortage"
          isRciFallback={isRciFallback}
        />

        <ImbalanceList
          title="Surpluses"
          rows={view.surpluses}
          tone="surplus"
          isRciFallback={isRciFallback}
        />
      </div>
    </aside>
  );
}
