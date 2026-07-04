import { z } from "zod";

export const TierSchema = z.enum(["free", "founder_pass"]);
export type Tier = z.infer<typeof TierSchema>;

export const EntitlementsSchema = z.object({
  tier: TierSchema,
  maxSaveSlots: z.number().int().positive(),
  maxNarrativeEventsPerDay: z.number().int().positive(),
  narrativeEventsRemaining: z.number().int().nonnegative(),
  llmEnabled: z.boolean(),
});
export type Entitlements = z.infer<typeof EntitlementsSchema>;

const TIER_LIMITS: Record<Tier, Omit<Entitlements, "tier" | "narrativeEventsRemaining">> = {
  free: {
    maxSaveSlots: 3,
    maxNarrativeEventsPerDay: 10,
    llmEnabled: false,
  },
  founder_pass: {
    maxSaveSlots: 20,
    maxNarrativeEventsPerDay: Number.MAX_SAFE_INTEGER,
    llmEnabled: true,
  },
};

export function entitlementsForTier(
  tier: Tier,
  narrativeEventsRemaining?: number,
): Entitlements {
  const limits = TIER_LIMITS[tier];
  const remaining =
    narrativeEventsRemaining ?? limits.maxNarrativeEventsPerDay;
  return { tier, ...limits, narrativeEventsRemaining: remaining };
}

/** Mock identity — header `X-CityMajor-Tier` overrides cookie for dev. */
export function resolveTierFromRequest(req: Request): Tier {
  const headerTier = req.headers.get("x-citymajor-tier");
  if (headerTier) {
    const parsed = TierSchema.safeParse(headerTier);
    if (parsed.success) return parsed.data;
  }

  const cookie = req.headers.get("cookie") ?? "";
  const match = cookie.match(/(?:^|;\s*)citymajor_tier=(free|founder_pass)(?:;|$)/);
  if (match) {
    const parsed = TierSchema.safeParse(match[1]);
    if (parsed.success) return parsed.data;
  }

  return "free";
}

export const SetTierBodySchema = z.object({
  tier: TierSchema,
});
