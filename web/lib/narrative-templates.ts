import { z } from "zod";

/** Sim-state buckets used for template fallback when LLM is unavailable. */
export const SimStateBucketSchema = z.enum([
  "healthcare_low",
  "pollution_spike",
  "traffic_congestion",
  "budget_crisis",
  "housing_shortage",
  "economy_shortage",
  "crime_rising",
  "happiness_low",
  "prosperity_high",
  "default",
]);
export type SimStateBucket = z.infer<typeof SimStateBucketSchema>;

export const NarrativeEventRequestSchema = z.object({
  bucket: SimStateBucketSchema.optional(),
  /** Optional structured hints from the sim snapshot. */
  context: z
    .object({
      cityName: z.string().optional(),
      era: z.string().optional(),
      metricValue: z.number().optional(),
      goodsShortageIndex: z.number().optional(),
      utilityStressIndex: z.number().optional(),
      employmentRate: z.number().optional(),
      approval: z.number().optional(),
      cityFunds: z.number().optional(),
      residentialDemand: z.number().optional(),
      commercialDemand: z.number().optional(),
      industrialDemand: z.number().optional(),
      constructingBuildingCount: z.number().int().optional(),
      powerCoverageFraction: z.number().optional(),
      waterCoverageFraction: z.number().optional(),
    })
    .optional(),
});
export type NarrativeEventRequest = z.infer<typeof NarrativeEventRequestSchema>;

export const NarrativeOptionSchema = z.object({
  id: z.string(),
  label: z.string(),
  tradeoff: z.string(),
});

export const NarrativeSourceSchema = z.enum(["template", "llm"]);

export const NarrativeEventResponseSchema = z.object({
  bucket: SimStateBucketSchema,
  headline: z.string(),
  body: z.string(),
  options: z.array(NarrativeOptionSchema),
  source: NarrativeSourceSchema,
});

export type NarrativeEventResponse = z.infer<typeof NarrativeEventResponseSchema>;

/** Literal substring embedded in template copy; replaced with the player's city name. */
export const NARRATIVE_CITY_PLACEHOLDER = "the city";

const CITY_PLACEHOLDER_PATTERN = new RegExp(
  NARRATIVE_CITY_PLACEHOLDER.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"),
  "gi",
);

export function personalizeNarrativeText(text: string, cityName: string): string {
  return text.replace(CITY_PLACEHOLDER_PATTERN, cityName);
}

export function personalizeNarrativeEvent(
  event: NarrativeEventResponse,
  cityName: string,
): NarrativeEventResponse {
  return {
    ...event,
    headline: personalizeNarrativeText(event.headline, cityName),
    body: personalizeNarrativeText(event.body, cityName),
  };
}

type TemplateEntry = Omit<NarrativeEventResponse, "bucket" | "source">;

const TEMPLATES: Record<SimStateBucket, TemplateEntry> = {
  healthcare_low: {
    headline: "Clinic petition reaches city hall",
    body: "Healthcare gaps are widening across the city. Dr. Amara Osei reports treating patients in apartments because the nearest hospital is a long bus ride away. Riverside residents demand a neighborhood clinic.",
    options: [
      { id: "fund_clinic", label: "Fund a public clinic", tradeoff: "−$2.4M budget, +healthcare coverage in Riverside" },
      { id: "defer", label: "Defer to next budget cycle", tradeoff: "No immediate cost, satisfaction −8 in affected districts" },
      { id: "private_partnership", label: "Offer tax incentives to a private provider", tradeoff: "Lower upfront cost, slower coverage gains" },
    ],
  },
  pollution_spike: {
    headline: "Smog advisory issued for South End",
    body: "Industrial emissions pushed particulate levels past safe thresholds across the city. Respiratory admissions are climbing and the Greens faction is calling for emergency zoning review.",
    options: [
      { id: "emission_caps", label: "Impose temporary emission caps", tradeoff: "−12% industrial output, pollution −25%" },
      { id: "monitor_only", label: "Monitor and issue health guidance", tradeoff: "No economic hit, pollution persists, approval −5" },
      { id: "relocate_heavy", label: "Fast-track heavy industry relocation", tradeoff: "High relocation subsidies, long-term land-value recovery" },
    ],
  },
  traffic_congestion: {
    headline: "Commute times hit a new peak",
    body: "Average rush-hour travel time crossed 48 minutes. Logistics firms warn that delivery delays are raising retail prices across the city.",
    options: [
      { id: "transit_expansion", label: "Expand bus lanes and frequency", tradeoff: "−$1.8M OPEX, traffic −15% in core corridors" },
      { id: "congestion_pricing", label: "Pilot congestion pricing downtown", tradeoff: "Revenue +$400K/mo, unpopular with commuters" },
      { id: "status_quo", label: "Hold current transport plan", tradeoff: "No spend, congestion worsens over 2 quarters" },
    ],
  },
  budget_crisis: {
    headline: "Treasury projects a shortfall",
    body: "The city's projected expenses exceed revenue for the third consecutive quarter. Department heads are asked to submit 8% contingency cuts.",
    options: [
      { id: "raise_taxes", label: "Raise income tax 2 points", tradeoff: "+$3.1M revenue, business faction approval −10" },
      { id: "cut_services", label: "Trim non-essential services", tradeoff: "Satisfaction −6, preserves current tax rate" },
      { id: "issue_bonds", label: "Issue municipal bonds", tradeoff: "Immediate liquidity, debt service +$200K/mo" },
    ],
  },
  housing_shortage: {
    headline: "Rent burden triggers protest organizing",
    body: "Median rent now consumes 42% of household income in the North Quarter. Tenants' unions across the city plan a march unless affordable units are approved.",
    options: [
      { id: "inclusionary_zoning", label: "Mandate 15% affordable units in new builds", tradeoff: "Developer pushback, gradual rent relief" },
      { id: "public_housing", label: "Break ground on public housing blocks", tradeoff: "−$5M capital, +1,200 affordable units over 18 months" },
      { id: "rent_subsidy", label: "Launch a temporary rent subsidy", tradeoff: "−$900K/mo OPEX, fast relief, no new supply" },
    ],
  },
  economy_shortage: {
    headline: "Store shelves thin as supply chains strain",
    body: "Wholesale buyers report persistent stockouts across the city. Shopkeepers blame freight delays and weak industrial throughput; households are paying more for basics.",
    options: [
      { id: "industrial_incentives", label: "Offer tax breaks for new industrial capacity", tradeoff: "−$1.2M revenue, gradual goods relief" },
      { id: "import_subsidy", label: "Subsidize emergency freight imports", tradeoff: "−$800K/quarter, faster shelf recovery" },
      { id: "rationing_review", label: "Study rationing for essential goods", tradeoff: "Stabilizes prices, unpopular with retailers" },
    ],
  },
  crime_rising: {
    headline: "Police chief requests overtime budget",
    body: "Property crime rose 18% quarter-over-quarter across the city. Shop owners on Market Street are closing early and demanding visible patrols.",
    options: [
      { id: "expand_patrols", label: "Fund overtime patrols", tradeoff: "−$600K/quarter, crime −10%, police approval +5" },
      { id: "community_programs", label: "Invest in youth and community programs", tradeoff: "Slower crime reduction, long-term satisfaction gains" },
      { id: "surveillance", label: "Install CCTV network in hot spots", tradeoff: "Crime −6%, civil-liberties faction approval −8" },
    ],
  },
  happiness_low: {
    headline: "Approval ratings slip below comfort zone",
    body: "Satisfaction across the city dipped under 45%. Local media run editorials asking whether current leadership still reflects residents' priorities.",
    options: [
      { id: "town_hall", label: "Hold a televised town hall", tradeoff: "Minor approval bump if promises are kept" },
      { id: "parks_program", label: "Launch a quick-win parks beautification blitz", tradeoff: "−$400K, +satisfaction in 3 districts" },
      { id: "stay_course", label: "Stay the course", tradeoff: "Risk further approval erosion" },
    ],
  },
  prosperity_high: {
    headline: "Investors eye expansion zones",
    body: "Tax revenue and employment are both above trend. Commercial developers petition for upzoning along the city's riverfront corridor.",
    options: [
      { id: "upzone_riverfront", label: "Approve riverfront upzoning", tradeoff: "+jobs, +traffic and pollution pressure" },
      { id: "infrastructure_first", label: "Require infrastructure upgrades first", tradeoff: "Slower growth, fewer downstream crises" },
      { id: "reject", label: "Reject upzoning for now", tradeoff: "Preserve character, forgo near-term revenue spike" },
    ],
  },
  default: {
    headline: "Council notes for the week",
    body: "Routine municipal business continues across the city. Advisors recommend monitoring healthcare, pollution, and budget metrics for emerging issues.",
    options: [
      { id: "review_metrics", label: "Review city metrics", tradeoff: "No immediate cost" },
      { id: "press_conference", label: "Hold a press conference", tradeoff: "Small approval swing depending on recent performance" },
    ],
  },
};

export function resolveBucket(
  explicit: SimStateBucket | undefined,
  context?: NarrativeEventRequest["context"],
): SimStateBucket {
  if (explicit && explicit !== "default") return explicit;

  const metric = context?.metricValue;
  if (metric !== undefined) {
    if (metric < 0.3) return "healthcare_low";
    if (metric > 0.85) return "pollution_spike";
  }

  return explicit ?? "default";
}

export function narrativeFromBucket(bucket: SimStateBucket): NarrativeEventResponse {
  const template = TEMPLATES[bucket];
  return { bucket, ...template, source: "template" };
}
