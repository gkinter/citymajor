"use client";

import type { CSSProperties } from "react";
import { LOW_APPROVAL_WARNING_THRESHOLD } from "@/lib/constants";
import { hudEraBadgeStyle, hudEraName } from "@/lib/era";
import {
  HUD_ZONE,
  hudPanel,
} from "@/lib/hud-theme";
import { LOW_HAPPINESS_APPROVAL } from "@/lib/sim-metrics";
import type { RciDemand, SimResources } from "@/lib/sim-bridge";
import { resolveRci } from "@/lib/zoning-economy";

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

function formatPopulation(population: number): string {
  return population.toLocaleString();
}

function formatPercent(value: number, decimals = 0): string {
  return `${value.toFixed(decimals)}%`;
}

type MetricTone = "good" | "warn" | "bad" | "neutral";

function approvalTone(approval: number): MetricTone {
  if (approval > 60) return "good";
  if (approval >= 40) return "warn";
  return "bad";
}

function happinessTone(happiness: number): MetricTone {
  const pct = happiness * 100;
  if (pct > 70) return "good";
  if (pct >= 40) return "warn";
  return "bad";
}

function toneClass(tone: MetricTone, prefix: string): string {
  if (tone === "neutral") return prefix;
  return `${prefix} ${prefix}--${tone}`;
}

function clampDemand(value: number): number {
  return Math.max(-1, Math.min(1, value));
}

function demandToDisplay(normalized: number): number {
  return Math.round(clampDemand(normalized) * 100);
}

function hasMonthlyBudget(resources: SimResources): boolean {
  return (
    resources.monthlyIncome !== undefined &&
    resources.monthlyExpenses !== undefined
  );
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
  const monthlyNet =
    resources && hasMonthlyBudget(resources)
      ? resources.monthlyIncome! - resources.monthlyExpenses!
      : null;

  return (
    <div style={panelStyle}>
      <div className="hud-resources">
        <div className="hud-resources__stat">
          <span className="hud-resources__label">Pop</span>
          <span className="hud-resources__value">
            {resources ? formatPopulation(resources.population) : "—"}
          </span>
        </div>

        <div className="hud-resources__stat">
          <span className="hud-resources__label">Funds</span>
          <span className="hud-resources__value">
            {resources ? formatFunds(resources.cityFunds) : "—"}
          </span>
          {monthlyNet !== null ? (
            <span
              className={`hud-resources__sub ${
                monthlyNet >= 0
                  ? "hud-resources__sub--good"
                  : "hud-resources__sub--bad"
              }`}
              title="Net monthly budget"
            >
              ({formatCompactMoney(monthlyNet)}/mo)
            </span>
          ) : null}
        </div>

        {resources?.monthlyIncome !== undefined &&
        resources.monthlyExpenses !== undefined ? (
          <div
            className="hud-resources__stat"
            title={`Monthly budget: ${formatFunds(resources.monthlyIncome)} income, ${formatFunds(resources.monthlyExpenses)} expenses`}
          >
            <span className="hud-resources__label">Mo</span>
            <span className="hud-resources__budget">
              <span
                className="hud-resources__budget-in"
                title="Monthly tax & fee income"
              >
                {formatCompactMoney(resources.monthlyIncome)}
              </span>
              <span className="hud-resources__budget-sep">/</span>
              <span
                className="hud-resources__budget-out"
                title="Monthly service & upkeep expenses"
              >
                {formatCompactMoney(-resources.monthlyExpenses)}
              </span>
            </span>
          </div>
        ) : null}

        {resources?.approval !== undefined ? (
          <div
            className="hud-resources__stat"
            title={`Mayor approval drives Herald unrest stories below ${LOW_HAPPINESS_APPROVAL}% and triggers a crisis warning below ${LOW_APPROVAL_WARNING_THRESHOLD}%.`}
          >
            <span className="hud-resources__label">Approval</span>
            <span
              className={toneClass(
                approvalTone(resources.approval),
                "hud-resources__value",
              )}
            >
              {formatPercent(resources.approval)}
            </span>
          </div>
        ) : null}

        {resources?.happiness !== undefined ? (
          <div
            className="hud-resources__stat"
            title="Citizen satisfaction from services and living conditions. Sustained low happiness erodes mayor approval over time."
          >
            <span className="hud-resources__label">Happy</span>
            <span
              className={toneClass(
                happinessTone(resources.happiness),
                "hud-resources__value",
              )}
            >
              {formatPercent(resources.happiness * 100)}
            </span>
          </div>
        ) : null}

        <div className="hud-resources__stat">
          <span className="hud-resources__label">Tick</span>
          <span className="hud-resources__value">
            {resources ? resources.tick.toLocaleString() : "—"}
          </span>
        </div>

        <div className="hud-resources__stat">
          <span className="hud-resources__label">Era</span>
          {resources ? (
            <span style={eraBadgeStyle}>{hudEraName(era)}</span>
          ) : (
            <span className="hud-resources__value">—</span>
          )}
        </div>
      </div>

      {rci ? (
        <div
          className="hud-rci"
          aria-label="RCI demand"
          title="R/C/I demand — high values mean zone that type to grow"
          data-onboarding-target="demand"
        >
          <RciMeter label="R" demand={rci.residential} />
          <RciMeter label="C" demand={rci.commercial} />
          <RciMeter label="I" demand={rci.industrial} />
        </div>
      ) : null}
    </div>
  );
}
