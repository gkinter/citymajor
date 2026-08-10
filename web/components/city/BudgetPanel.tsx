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
  marginTop: 10,
  marginBottom: 6,
  fontSize: 11,
  letterSpacing: "0.06em",
  textTransform: "uppercase",
  color: HUD_COLORS.textMuted,
};

const rowStyle: CSSProperties = {
  marginBottom: 4,
  fontSize: 12,
  fontVariantNumeric: "tabular-nums",
};

const stubNote: CSSProperties = {
  marginTop: 6,
  fontSize: 11,
  lineHeight: 1.4,
  color: HUD_COLORS.textMuted,
};

const detailStyle: CSSProperties = {
  fontSize: 11,
  opacity: 0.75,
  fontVariantNumeric: "tabular-nums",
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

function hasFoundationEconomy(resources: SimResources): boolean {
  return (
    resources.employmentRate !== undefined ||
    resources.tradeBalance !== undefined ||
    resources.meanTrafficDensity !== undefined ||
    resources.constructingBuildingCount !== undefined ||
    (resources.goodsShortageIndex !== undefined &&
      resources.goodsShortageIndex > 0)
  );
}

function formatDemand(demand: number): string {
  const pct = Math.round(demand * 100);
  return pct >= 0 ? `+${pct}%` : `${pct}%`;
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
        ...hudInfoPanel({ minWidth: 200, maxWidth: 260 }),
      }}
    >
      <div style={{ ...sectionTitle, marginTop: 0 }}>Budget</div>

      {!resources ? (
        <p style={stubNote}>
          Treasury unavailable — start or load a city with WASM sim active.
        </p>
      ) : (
        <>
          <div style={rowStyle}>
            <span style={hudLabel()}>Treasury</span>
            {formatFunds(resources.cityFunds)}
          </div>

          {hasBudget && monthlyNet !== null ? (
            <>
              <div style={rowStyle}>
                <span style={hudLabel()}>Cashflow</span>
                <span
                  style={{
                    color: monthlyNet >= 0 ? "#7dffb2" : "#ff9aa8",
                  }}
                >
                  {formatCompactMoney(monthlyNet)}/mo
                </span>
              </div>
              <div style={detailStyle}>
                {formatCompactMoney(resources.monthlyIncome!)} in ·{" "}
                {formatCompactMoney(-resources.monthlyExpenses!)} out
              </div>
            </>
          ) : null}

          {resources.approval !== undefined ? (
            <div style={{ ...rowStyle, marginTop: 6 }}>
              <span style={hudLabel()}>Approval</span>
              {resources.approval.toFixed(0)}%
            </div>
          ) : null}

          {hasFoundationEconomy(resources) ? (
            <>
              <div style={sectionTitle}>Economy</div>

              {resources.employmentRate !== undefined ? (
                <div style={rowStyle}>
                  <span style={hudLabel()}>Employment</span>
                  {Math.round(resources.employmentRate * 100)}%
                </div>
              ) : null}

              {resources.tradeBalance !== undefined ? (
                <>
                  <div style={rowStyle}>
                    <span style={hudLabel()}>Trade</span>
                    <span
                      style={{
                        color:
                          resources.tradeBalance >= 0 ? "#7dffb2" : "#ff9aa8",
                      }}
                    >
                      {formatCompactMoney(resources.tradeBalance)}/mo
                    </span>
                  </div>
                  {resources.monthlyExportValue !== undefined &&
                  resources.monthlyImportCost !== undefined ? (
                    <div style={detailStyle}>
                      Exports {formatCompactMoney(resources.monthlyExportValue)}{" "}
                      · Imports{" "}
                      {formatCompactMoney(-resources.monthlyImportCost)}
                    </div>
                  ) : null}
                </>
              ) : null}

              {resources.meanTrafficDensity !== undefined ? (
                <div style={rowStyle}>
                  <span style={hudLabel()}>Traffic</span>
                  {(resources.meanTrafficDensity * 100).toFixed(0)}%
                </div>
              ) : null}

              {resources.constructingBuildingCount !== undefined &&
              resources.constructingBuildingCount > 0 ? (
                <div style={rowStyle}>
                  <span style={hudLabel()}>Building</span>
                  {resources.constructingBuildingCount} cranes active
                </div>
              ) : null}

              {resources.goodsShortageIndex !== undefined &&
              resources.goodsShortageIndex > 0 ? (
                <div
                  style={{
                    ...rowStyle,
                    color:
                      resources.goodsShortageIndex >= 0.35
                        ? "#ffb347"
                        : undefined,
                  }}
                >
                  <span style={hudLabel()}>Goods</span>
                  {Math.round(resources.goodsShortageIndex * 100)}% shortage
                </div>
              ) : null}

              {resources.residentialDemand !== undefined ? (
                <div style={detailStyle}>
                  RCI {formatDemand(resources.residentialDemand)} ·{" "}
                  {formatDemand(resources.commercialDemand ?? 0)} ·{" "}
                  {formatDemand(resources.industrialDemand ?? 0)}
                </div>
              ) : null}
            </>
          ) : null}
        </>
      )}
    </aside>
  );
}
