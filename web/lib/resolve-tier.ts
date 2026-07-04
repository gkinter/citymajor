import { getStoredTier } from "@/lib/tier-store";
import { TierSchema, type Tier } from "@/lib/entitlements";
import { getUserIdFromRequest } from "@/lib/user-identity";

/**
 * Resolve the caller's mock tier.
 *
 * Order:
 *   1. Signed `citymajor_tier` cookie (server-issued via /api/me/entitlements)
 *   2. Stored tier keyed by the verified user identity cookie
 *   3. Non-production dev override header `X-CityMajor-Tier`
 *   4. Free
 *
 * The dev header is intentionally NOT trusted in production so paying-tier
 * entitlements cannot be granted by a spoofed request header.
 */
export function resolveTierFromRequest(req: Request): Tier {
  const cookie = req.headers.get("cookie") ?? "";
  const match = cookie.match(/(?:^|;\s*)citymajor_tier=(free|founder_pass)(?:;|$)/);
  if (match) {
    const parsed = TierSchema.safeParse(match[1]);
    if (parsed.success) return parsed.data;
  }

  const userKey = getUserIdFromRequest(req);
  if (userKey) {
    const storedTier = getStoredTier(userKey);
    if (storedTier) return storedTier;
  }

  if (process.env.NODE_ENV !== "production") {
    const headerTier = req.headers.get("x-citymajor-tier");
    if (headerTier) {
      const parsed = TierSchema.safeParse(headerTier);
      if (parsed.success) return parsed.data;
    }
  }

  return "free";
}
