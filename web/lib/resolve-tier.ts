import { getStoredTier } from "@/lib/tier-store";
import { TierSchema, type Tier } from "@/lib/entitlements";

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

  const userKey = req.headers.get("x-citymajor-user") ?? "default-user";
  const storedTier = getStoredTier(userKey);
  if (storedTier) return storedTier;

  return "free";
}
