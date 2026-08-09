import { BuildingCategory } from "@citymajor/sim-types";
import {
  LOW_TREASURY_MIN_FUNDS,
  LOW_TREASURY_RUNWAY_MONTHS,
} from "@/lib/constants";
import type { SimStateBucket } from "@/lib/narrative-templates";
import type { SimResources } from "@/lib/sim-bridge";
import type { CityData } from "./types";

/** RCI demand at or above this value (-1..+1) counts as an extreme shortage. */
export const RCI_EXTREME_DEMAND = 0.65;

/** Goods shortage index at or above this value (0..1) triggers economy_shortage Herald bucket. */
export const GOODS_SHORTAGE_THRESHOLD = 0.35;

/** Mayor approval below this percent maps to the happiness_low Herald bucket. */
export const LOW_HAPPINESS_APPROVAL = 45;

/** Approval + treasury thresholds for prosperity_high stories. */
export const PROSPERITY_APPROVAL = 70;
export const PROSPERITY_FUNDS = 500_000;

export type NarrativeMetricsInput = {
  healthcareCoverage: number;
} & Partial<
  Pick<
    SimResources,
    | "approval"
    | "cityFunds"
    | "residentialDemand"
    | "commercialDemand"
    | "industrialDemand"
    | "goodsShortageIndex"
  >
>;

/**
 * M0 heuristic until WASM exports aggregate health coverage.
 * Service buildings proxy clinic/hospital capacity vs population scale.
 */
export function estimateHealthcareCoverage(city: CityData): number {
  const total = city.buildings.length;
  if (total === 0) return 0.15;

  const serviceCount = city.buildings.filter(
    (b) => b.category === BuildingCategory.Service,
  ).length;

  const ratio = serviceCount / Math.max(1, total / 400);
  return Math.min(1, Math.max(0, ratio));
}

/**
 * Map live sim metrics to a Herald template bucket (GAMEPLAY_LOOP #12).
 * Crisis signals (treasury, approval) take priority over RCI extremes.
 */
export function deriveNarrativeBucket(
  metrics: NarrativeMetricsInput,
): SimStateBucket {
  const {
    healthcareCoverage,
    approval,
    cityFunds,
    residentialDemand,
    commercialDemand,
    industrialDemand,
    goodsShortageIndex,
  } = metrics;

  if (cityFunds !== undefined && cityFunds < 0) {
    return "budget_crisis";
  }

  if (
    goodsShortageIndex !== undefined &&
    goodsShortageIndex >= GOODS_SHORTAGE_THRESHOLD
  ) {
    if (
      residentialDemand !== undefined &&
      residentialDemand >= RCI_EXTREME_DEMAND
    ) {
      return "housing_shortage";
    }
    return "economy_shortage";
  }

  if (approval !== undefined && approval < LOW_HAPPINESS_APPROVAL) {
    return "happiness_low";
  }

  if (
    residentialDemand !== undefined &&
    residentialDemand >= RCI_EXTREME_DEMAND
  ) {
    return "housing_shortage";
  }

  if (
    commercialDemand !== undefined &&
    commercialDemand >= RCI_EXTREME_DEMAND &&
    industrialDemand !== undefined &&
    industrialDemand >= RCI_EXTREME_DEMAND &&
    cityFunds !== undefined &&
    cityFunds >= 0
  ) {
    return "prosperity_high";
  }

  if (healthcareCoverage < 0.3) {
    return "healthcare_low";
  }

  if (
    approval !== undefined &&
    approval >= PROSPERITY_APPROVAL &&
    cityFunds !== undefined &&
    cityFunds >= PROSPERITY_FUNDS
  ) {
    return "prosperity_high";
  }

  return "default";
}

export type NarrativeBucketExplanation = {
  bucket: SimStateBucket;
  /** One-line hint shown in Herald + HUD tooltips — ties live metrics to the story bucket. */
  reason: string;
};

function formatFundsShort(cityFunds: number): string {
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

export type CashCrisisKind = "bankrupt" | "low-treasury" | "negative-cashflow";

export type CashCrisisSeverity = "critical" | "warn";

export type CashCrisisWarning = {
  kind: CashCrisisKind;
  severity: CashCrisisSeverity;
  message: string;
};

const CASH_CRISIS_RANK: Record<CashCrisisKind, number> = {
  "negative-cashflow": 1,
  "low-treasury": 2,
  bankrupt: 3,
};

/** Treasury balance that triggers a low-funds warning strip in ResourcesHud. */
export function resolveLowTreasuryThreshold(resources: SimResources): number {
  const expenses = resources.monthlyExpenses;
  if (expenses !== undefined && expenses > 0) {
    return Math.max(
      LOW_TREASURY_MIN_FUNDS,
      expenses * LOW_TREASURY_RUNWAY_MONTHS,
    );
  }
  return LOW_TREASURY_MIN_FUNDS;
}

/**
 * Detect bankruptcy / cash-crisis signals for HUD warning strip (GAMEPLAY_LOOP #9).
 * Treasury deficit takes priority over low runway, which takes priority over negative cashflow.
 */
export function detectCashCrisis(
  resources: SimResources | null,
): CashCrisisWarning | null {
  if (!resources) return null;

  const monthlyNet = hasMonthlyBudget(resources)
    ? resources.monthlyIncome! - resources.monthlyExpenses!
    : null;

  if (resources.cityFunds < 0) {
    return {
      kind: "bankrupt",
      severity: "critical",
      message: `Treasury ${formatFundsShort(resources.cityFunds)} — cut spending or raise revenue before services stall`,
    };
  }

  const threshold = resolveLowTreasuryThreshold(resources);
  if (resources.cityFunds < threshold) {
    return {
      kind: "low-treasury",
      severity: "warn",
      message: `Treasury ${formatFundsShort(resources.cityFunds)} below ${formatFundsShort(threshold)} runway — Herald may run deficit coverage`,
    };
  }

  if (monthlyNet !== null && monthlyNet < 0) {
    return {
      kind: "negative-cashflow",
      severity: "warn",
      message: `Negative cashflow ${formatCompactMoney(monthlyNet)}/mo — expenses exceed income`,
    };
  }

  return null;
}

/** Compare crisis severity for toast re-arming when conditions escalate. */
export function cashCrisisRank(kind: CashCrisisKind): number {
  return CASH_CRISIS_RANK[kind];
}

/**
 * Derive Herald bucket plus a player-facing reason string (lightweight UX #12).
 */
export function explainNarrativeBucket(
  metrics: NarrativeMetricsInput,
): NarrativeBucketExplanation {
  const bucket = deriveNarrativeBucket(metrics);
  const {
    healthcareCoverage,
    approval,
    cityFunds,
    residentialDemand,
    commercialDemand,
    industrialDemand,
    goodsShortageIndex,
  } = metrics;

  switch (bucket) {
    case "budget_crisis":
      return {
        bucket,
        reason:
          cityFunds !== undefined
            ? `Treasury ${formatFundsShort(cityFunds)} — deficit stories take priority in the Herald`
            : "Treasury in deficit — budget crisis coverage",
      };
    case "happiness_low":
      return {
        bucket,
        reason:
          approval !== undefined
            ? `Mayor approval ${approval.toFixed(0)}% (below ${LOW_HAPPINESS_APPROVAL}% unrest threshold)`
            : `Approval below ${LOW_HAPPINESS_APPROVAL}% — unrest coverage`,
      };
    case "housing_shortage":
      if (
        goodsShortageIndex !== undefined &&
        goodsShortageIndex >= GOODS_SHORTAGE_THRESHOLD
      ) {
        return {
          bucket,
          reason: `Goods shortage ${Math.round(goodsShortageIndex * 100)}% and residential demand extreme — housing pressure edition`,
        };
      }
      return {
        bucket,
        reason:
          residentialDemand !== undefined
            ? `Residential demand +${Math.round(residentialDemand * 100)} — housing pressure dominates headlines`
            : "Extreme residential demand — housing shortage coverage",
      };
    case "economy_shortage":
      return {
        bucket,
        reason:
          goodsShortageIndex !== undefined
            ? `Goods shortage index ${Math.round(goodsShortageIndex * 100)}% — supply-chain coverage in the Herald`
            : "Goods shortage — supply-chain coverage in the Herald",
      };
    case "prosperity_high":
      if (
        approval !== undefined &&
        approval >= PROSPERITY_APPROVAL &&
        cityFunds !== undefined &&
        cityFunds >= PROSPERITY_FUNDS
      ) {
        return {
          bucket,
          reason: `Approval ${approval.toFixed(0)}% and treasury ${formatFundsShort(cityFunds)} — prosperity beat`,
        };
      }
      return {
        bucket,
        reason:
          commercialDemand !== undefined && industrialDemand !== undefined
            ? `RCI demand surging (C +${Math.round(commercialDemand * 100)}, I +${Math.round(industrialDemand * 100)})`
            : "Strong RCI demand — growth stories",
      };
    case "healthcare_low":
      return {
        bucket,
        reason: `Healthcare coverage ~${Math.round(healthcareCoverage * 100)}% (clinic gap below 30%)`,
      };
    default:
      return {
        bucket,
        reason: "Balanced city pulse — general Herald coverage",
      };
  }
}
