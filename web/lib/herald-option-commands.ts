import type { SimCommand } from "@/lib/sim-bridge";

/** Scalar effects for a Herald council option (tradeoff stubs until full politics sim). */
export type HeraldOptionEffects = {
  /** One-time treasury delta in city funds (negative = spend). */
  budgetAdjust?: number;
  /** Mayor approval swing in percentage points (−100…+100 scale). */
  approvalDelta?: number;
  /** Instant research-point grant (stub — no tech unlock side effects). */
  researchBoost?: number;
};

/**
 * Council option IDs from `narrative-templates.ts` / LLM Herald responses.
 * Values mirror tradeoff copy; tune when BudgetSystem exposes line-item APIs.
 */
export const HERALD_OPTION_EFFECTS: Record<string, HeraldOptionEffects> = {
  fund_clinic: { budgetAdjust: -2_400_000, approvalDelta: 4 },
  defer: { approvalDelta: -3 },
  private_partnership: { budgetAdjust: -800_000, approvalDelta: 2 },
  emission_caps: { approvalDelta: 2 },
  monitor_only: { approvalDelta: -5 },
  relocate_heavy: { budgetAdjust: -3_500_000, approvalDelta: 3 },
  transit_expansion: { budgetAdjust: -1_800_000, approvalDelta: 4 },
  congestion_pricing: { budgetAdjust: 400_000, approvalDelta: -3, researchBoost: 1 },
  status_quo: { approvalDelta: -2 },
  raise_taxes: { budgetAdjust: 3_100_000, approvalDelta: -10 },
  cut_services: { approvalDelta: -6 },
  issue_bonds: { budgetAdjust: 5_000_000, approvalDelta: -2 },
  inclusionary_zoning: { approvalDelta: 3 },
  public_housing: { budgetAdjust: -5_000_000, approvalDelta: 6 },
  rent_subsidy: { budgetAdjust: -900_000, approvalDelta: 4 },
  expand_patrols: { budgetAdjust: -600_000, approvalDelta: 5 },
  community_programs: { budgetAdjust: -400_000, approvalDelta: 3, researchBoost: 2 },
  surveillance: { budgetAdjust: -300_000, approvalDelta: -8 },
  town_hall: { budgetAdjust: -50_000, approvalDelta: 3 },
  parks_program: { budgetAdjust: -400_000, approvalDelta: 4 },
  stay_course: { approvalDelta: -3 },
  upzone_riverfront: { approvalDelta: -1, researchBoost: 3 },
  infrastructure_first: { budgetAdjust: -1_000_000, approvalDelta: 2, researchBoost: 5 },
  reject: { approvalDelta: 2 },
  review_metrics: { researchBoost: 2 },
  press_conference: { budgetAdjust: -25_000, approvalDelta: 2 },
};

const DEFAULT_EFFECTS: HeraldOptionEffects = { approvalDelta: 0 };

export type HeraldOptionContext = {
  eventId?: number;
};

/**
 * Map a Herald council option to concrete WASM sim commands.
 * Always emits `approval_event` (resolves linked sim event when `eventId` is set).
 */
export function heraldOptionToSimCommands(
  optionId: string,
  ctx: HeraldOptionContext = {},
): SimCommand[] {
  const effects = HERALD_OPTION_EFFECTS[optionId] ?? DEFAULT_EFFECTS;
  const commands: SimCommand[] = [];

  if (effects.budgetAdjust !== undefined && effects.budgetAdjust !== 0) {
    commands.push({ type: "budget_adjust", deltaFunds: effects.budgetAdjust });
  }

  if (effects.researchBoost !== undefined && effects.researchBoost > 0) {
    commands.push({ type: "research_boost", points: effects.researchBoost });
  }

  commands.push({
    type: "approval_event",
    approvalDelta: effects.approvalDelta ?? 0,
    eventId: ctx.eventId,
    optionId,
  });

  return commands;
}
