"use client";

import type { CSSProperties } from "react";
import {
  HUD_COLORS,
  HUD_Z,
  HUD_ZONE,
  hudInfoPanel,
  hudLabel,
} from "@/lib/hud-theme";
import type { SimResources } from "@/lib/sim-bridge";

type BudgetPanelProps = {
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

const stubNote: CSSProperties = {
  marginTop: 6,
  fontSize: 11,
  lineHeight: 1.4,
  color: HUD_COLORS.textMuted,
};

function formatFunds(cityFunds: number): string {
  const abs = Math.abs(cityFunds);
  if (abs >= 1_000_000) return `$${(cityFunds / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `$${(cityFunds / 1_000).toFixed(1)}K`;
  return `$${cityFunds.toLocaleString()}`;
}

function formatCompactMoney(amount: number): string {
  const abs = Math.abs(amount);
  const sign = amount >= 0 ? "+" : "−";
  if (abs >= 1_000_000) return `${sign}$${(abs / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `${sign}$${(abs / 1_000).toFixed(1)}K`;
  return `${sign}$${abs.toLocaleString()}`;
}

function hasMonthlyBudget(resources: SimResources): boolean {
  return (
    resources.monthlyIncome !== undefined &&
    resources.monthlyExpenses !== undefined
  );
}

export function BudgetPanel({ resources }: BudgetPanelProps) {
  const hasBudget = resources !== null && hasMonthlyBudget(resources);
  const monthlyNet =
    hasBudget && resources
      ? resources.monthlyIncome! - resources.monthlyExpenses!
      : null;
  const topOffset = resources?.eraProgress ? 240 : 50;

  return (
    <aside
      aria-label="City budget"
      data-onboarding-target="budget"
      style={{
        ...HUD_ZONE.topRight,
        top: topOffset,
        zIndex: HUD_Z.panel,
        ...hudInfoPanel({ minWidth: 200, maxWidth: 240 }),
      }}
    >
      <div style={sectionTitle}>Budget</div>

      {resources ? (
        <div style={{ marginBottom: 4 }}>
          <span style={hudLabel()}>Treasury</span>
          {formatFunds(resources.cityFunds)}
        </div>
      ) : (
        <p style={stubNote}>
          TODO: Treasury unavailable — read{" "}
          <code style={{ fontSize: 10 }}>CityFunds</code> from WASM status JSON
          (BudgetSystem / PoliticsSystem).
        </p>
      )}

      {hasBudget && resources && monthlyNet !== null ? (
        <>
          <div style={{ marginBottom: 4 }}>
            <span style={hudLabel()}>Cashflow</span>
            <span
              style={{
                color: monthlyNet >= 0 ? "#7dffb2" : "#ff9aa8",
                fontVariantNumeric: "tabular-nums",
              }}
            >
              {formatCompactMoney(monthlyNet)}/mo
            </span>
          </div>
          <div style={{ fontSize: 11, opacity: 0.75, fontVariantNumeric: "tabular-nums" }}>
            {formatCompactMoney(resources.monthlyIncome!)} in ·{" "}
            {formatCompactMoney(-resources.monthlyExpenses!)} out
          </div>
        </>
      ) : resources ? (
        <p style={stubNote}>
          TODO: Monthly cashflow not exported — BudgetSystem ledgers missing from
          status JSON. PoliticsSystem fields available: approval{" "}
          {resources.approval !== undefined
            ? `${resources.approval.toFixed(0)}%`
            : "—"}
          , happiness{" "}
          {resources.happiness !== undefined
            ? `${(resources.happiness * 100).toFixed(0)}%`
            : "—"}
          .
        </p>
      ) : null}
    </aside>
  );
}
