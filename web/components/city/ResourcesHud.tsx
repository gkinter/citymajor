"use client";

import type { CSSProperties } from "react";
import { LOW_APPROVAL_WARNING_THRESHOLD } from "@/lib/constants";
import { hudEraBadgeStyle, hudEraName } from "@/lib/era";
import {
  HUD_ZONE,
  hudPanel,
} from "@/lib/hud-theme";
import { detectCashCrisis, LOW_HAPPINESS_APPROVAL } from "@/lib/sim-metrics";
import type { SimResources } from "@/lib/sim-bridge";
import { formatGrowthPerMonth } from "@/lib/population-growth";
import { formatEmergencyResponseMinutes } from "@/lib/emergency-response";

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

function hasMonthlyBudget(resources: SimResources): boolean {
  return (
    resources.monthlyIncome !== undefined &&
    resources.monthlyExpenses !== undefined
  );
}

function hasTradeData(resources: SimResources): boolean {
  return (
    resources.monthlyExportValue !== undefined ||
    resources.monthlyImportCost !== undefined ||
    resources.tradeBalance !== undefined
  );
}

function hasUtilityData(resources: SimResources): boolean {
  return (
    resources.powerCoverageFraction !== undefined ||
    resources.waterCoverageFraction !== undefined
  );
}

type UtilityStressTone = "ok" | "warn" | "stress";

function utilityStressTone(stress: number): UtilityStressTone {
  if (stress < 0.2) return "ok";
  if (stress <= 0.5) return "warn";
  return "stress";
}

function employmentTone(rate: number): MetricTone {
  const pct = rate * 100;
  if (pct >= 70) return "good";
  if (pct >= 50) return "warn";
  return "bad";
}

function formatTradeInflow(amount: number): string {
  const abs = Math.abs(amount);
  if (abs >= 1_000_000) return `+$${(abs / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `+$${(abs / 1_000).toFixed(1)}K`;
  return `+$${abs.toLocaleString()}`;
}

function formatTradeOutflow(amount: number): string {
  const abs = Math.abs(amount);
  if (abs >= 1_000_000) return `−$${(abs / 1_000_000).toFixed(1)}M`;
  if (abs >= 1_000) return `−$${(abs / 1_000).toFixed(1)}K`;
  return `−$${abs.toLocaleString()}`;
}

type ResourcesHudProps = {
  resources: SimResources | null;
  /** Client-estimated or WASM-exported net population change per game month. */
  populationGrowthPerMonth?: number | null;
};

export function ResourcesHud({
  resources,
  populationGrowthPerMonth = null,
}: ResourcesHudProps) {
  const panelStyle = hudPanel({
    ...HUD_ZONE.resources,
    display: "flex",
    flexDirection: "column",
    alignItems: "stretch",
    gap: 0,
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

  const monthlyNet =
    resources && hasMonthlyBudget(resources)
      ? resources.monthlyIncome! - resources.monthlyExpenses!
      : null;
  const growthRate =
    resources?.populationGrowthRate ?? populationGrowthPerMonth ?? null;
  const cashCrisis = detectCashCrisis(resources);

  return (
    <div style={panelStyle}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 12,
        }}
      >
      <div className="hud-resources">
        <div className="hud-resources__stat">
          <span className="hud-resources__label">Pop</span>
          <span className="hud-resources__value">
            {resources ? formatPopulation(resources.population) : "—"}
          </span>
          {resources?.householdCount !== undefined ? (
            <span
              className="hud-resources__sub"
              title="Households tracked by PopulationSystem"
            >
              ({resources.householdCount.toLocaleString()} homes)
            </span>
          ) : null}
          {growthRate !== null ? (
            <span
              className={`hud-resources__sub ${
                growthRate > 0
                  ? "hud-resources__sub--good"
                  : growthRate < 0
                    ? "hud-resources__sub--bad"
                    : ""
              }`}
              title="Net population change per game month"
            >
              ({formatGrowthPerMonth(growthRate)})
            </span>
          ) : null}
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

        {resources && hasTradeData(resources) ? (
          <div
            className="hud-resources__stat"
            title={`Global market trade last month: ${formatTradeInflow(resources.monthlyExportValue ?? 0)} exports, ${formatTradeOutflow(resources.monthlyImportCost ?? 0)} imports`}
          >
            <span className="hud-resources__label">Trade</span>
            <span className="hud-resources__trade">
              <span
                className="hud-resources__trade-export"
                title="Export revenue"
              >
                {formatTradeInflow(resources.monthlyExportValue ?? 0)}
              </span>
              <span className="hud-resources__budget-sep">/</span>
              <span
                className="hud-resources__trade-import"
                title="Import cost"
              >
                {formatTradeOutflow(resources.monthlyImportCost ?? 0)}
              </span>
              {resources.tradeBalance !== undefined ? (
                <>
                  <span className="hud-resources__budget-sep">·</span>
                  <span
                    className={`hud-resources__trade-balance ${
                      resources.tradeBalance >= 0
                        ? "hud-resources__trade-balance--good"
                        : "hud-resources__trade-balance--bad"
                    }`}
                    title="Net trade balance (exports − imports)"
                  >
                    ({formatCompactMoney(resources.tradeBalance)})
                  </span>
                </>
              ) : null}
            </span>
          </div>
        ) : null}

        {resources && hasUtilityData(resources) ? (
          <div
            className={`hud-resources__stat hud-resources__utilities hud-resources__utilities--${utilityStressTone(
              resources.utilityStressIndex ?? 0,
            )}`}
            title={`Utility coverage — power ${Math.round((resources.powerCoverageFraction ?? 0) * 100)}%, water ${Math.round((resources.waterCoverageFraction ?? 0) * 100)}%${
              resources.utilityStressIndex !== undefined
                ? `; stress ${Math.round(resources.utilityStressIndex * 100)}%`
                : ""
            }`}
          >
            <span className="hud-resources__label">Util</span>
            <span
              className={`hud-resources__utilities-value hud-resources__utilities-value--${utilityStressTone(
                resources.utilityStressIndex ?? 0,
              )}`}
            >
              ⚡ {Math.round((resources.powerCoverageFraction ?? 0) * 100)}% · 💧{" "}
              {Math.round((resources.waterCoverageFraction ?? 0) * 100)}%
            </span>
          </div>
        ) : null}

        {resources?.meanEmergencyResponseMinutes !== undefined ? (
          <div
            className="hud-resources__stat"
            title="Mean fire/EMS response minutes over sampled zoned tiles (road distance + traffic)."
            data-testid="resources-ems-response"
          >
            <span className="hud-resources__label">EMS</span>
            <span className="hud-resources__value">
              {formatEmergencyResponseMinutes(
                resources.meanEmergencyResponseMinutes,
              )}
            </span>
          </div>
        ) : null}

        {resources?.employmentRate !== undefined ? (
          <div
            className="hud-resources__stat"
            title="Share of working-age households with a workplace"
          >
            <span className="hud-resources__label">Jobs</span>
            <span
              className={toneClass(
                employmentTone(resources.employmentRate),
                "hud-resources__value",
              )}
            >
              {formatPercent(resources.employmentRate * 100)}
            </span>
          </div>
        ) : null}

        {resources?.constructingBuildingCount !== undefined &&
        resources.constructingBuildingCount > 0 ? (
          <div
            className="hud-resources__stat hud-resources__cranes"
            title="Buildings under construction"
          >
            <span className="hud-resources__value">
              🚧 {resources.constructingBuildingCount}{" "}
              {resources.constructingBuildingCount === 1
                ? "building"
                : "buildings"}
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
      </div>

      {cashCrisis ? (
        <div
          className={`hud-resources__crisis hud-resources__crisis--${cashCrisis.severity}`}
          role="alert"
          aria-live="polite"
          title="Cash crisis — check budget, taxes, and Herald deficit coverage"
        >
          <span className="hud-resources__crisis-label" aria-hidden>
            {cashCrisis.severity === "critical" ? "Bankruptcy" : "Cash crisis"}
          </span>
          <span className="hud-resources__crisis-message">{cashCrisis.message}</span>
        </div>
      ) : null}
    </div>
  );
}
