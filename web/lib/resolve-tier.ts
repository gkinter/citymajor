import { getStoredTier } from "@/lib/tier-store";
import { TierSchema, type Tier } from "@/lib/entitlements";
import { getUserIdFromRequest } from "@/lib/user-identity";

/**
 * Resolve the caller's tier for entitlement gating.
 *
 * **Production** (per LIVE_SERVICES_ARCHITECTURE.md — never trust client grants):
 *   1. Stored tier keyed by the HMAC-signed `citymajor_uid` cookie (Stripe webhook → tier-store)
 *   2. `free`
 *
 * **Non-production** (mock /shop checkout without Stripe):
 *   1. `citymajor_tier` cookie (server-issued via POST /api/me/entitlements)
 *   2. Stored tier from webhook replay or manual tier-store seed
 *   3. Dev override header `X-CityMajor-Tier`
 *   4. `free`
 *
 * Mock cookie and dev header paths are intentionally disabled in production so
 * paying-tier entitlements cannot be self-granted without a verified payment.
 */
export function resolveTierFromRequest(req: Request): Tier {
  const isProduction = process.env.NODE_ENV === "production";

  if (!isProduction) {
    const cookie = req.headers.get("cookie") ?? "";
    const match = cookie.match(/(?:^|;\s*)citymajor_tier=(free|founder_pass)(?:;|$)/);
    if (match) {
      const parsed = TierSchema.safeParse(match[1]);
      if (parsed.success) return parsed.data;
    }
  }

  const userKey = getUserIdFromRequest(req);
  if (userKey) {
    const storedTier = getStoredTier(userKey);
    if (storedTier) return storedTier;
  }

  if (!isProduction) {
    const headerTier = req.headers.get("x-citymajor-tier");
    if (headerTier) {
      const parsed = TierSchema.safeParse(headerTier);
      if (parsed.success) return parsed.data;
    }
  }

  return "free";
}
