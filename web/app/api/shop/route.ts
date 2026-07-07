import { NextResponse } from "next/server";
import {
  getMissingStripeCheckoutEnv,
  getStripePublishableKey,
  isStripeCheckoutEnabled,
} from "@/lib/stripe";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/**
 * Shop / Stripe checkout readiness probe.
 *
 * `GET /shop` uses server-side `isStripeCheckoutEnabled()` for the UI; this route
 * exposes the same signal plus missing env keys for deploy smoke tests.
 */
export async function GET() {
  const missing = getMissingStripeCheckoutEnv();
  return NextResponse.json({
    stripeCheckoutEnabled: isStripeCheckoutEnabled(),
    publishableKeyConfigured: Boolean(getStripePublishableKey()),
    checkoutEndpoint: "/api/checkout/founder-pass",
    ...(missing.length > 0 ? { missing } : {}),
  });
}
