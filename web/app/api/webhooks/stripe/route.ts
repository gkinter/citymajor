import { NextResponse } from "next/server";
import type Stripe from "stripe";
import { handleStripeWebhookEvent } from "@/lib/stripe-webhook";
import { getStripeClient, getStripeWebhookSecret, isStripeCheckoutEnabled } from "@/lib/stripe";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/**
 * Stripe webhook endpoint skeleton.
 *
 * Register the deployment FQDN + `/api/webhooks/stripe` in the Stripe Dashboard.
 * Requires `STRIPE_WEBHOOK_SECRET`; checkout session creation requires the other
 * Stripe env vars (see `web/.env.example`).
 */
export async function GET() {
  return NextResponse.json({
    status: "ok",
    endpoint: "/api/webhooks/stripe",
    checkoutConfigured: isStripeCheckoutEnabled(),
    webhookSecretConfigured: Boolean(getStripeWebhookSecret()),
  });
}

export async function POST(req: Request) {
  const webhookSecret = getStripeWebhookSecret();
  if (!webhookSecret) {
    return NextResponse.json(
      { error: "STRIPE_WEBHOOK_SECRET is not configured" },
      { status: 503 },
    );
  }

  const signature = req.headers.get("stripe-signature");
  if (!signature) {
    return NextResponse.json({ error: "Missing stripe-signature header" }, { status: 400 });
  }

  const body = await req.text();

  let event: Stripe.Event;
  try {
    const stripe = getStripeClient();
    event = stripe.webhooks.constructEvent(body, signature, webhookSecret);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Invalid webhook signature";
    return NextResponse.json({ error: message }, { status: 400 });
  }

  const result = handleStripeWebhookEvent(event);

  if (result.status === "ignored" && result.reason.startsWith("unhandled_")) {
    console.info("[stripe-webhook] unhandled event", {
      type: event.type,
      id: event.id,
    });
  } else if (result.status === "ignored") {
    console.warn("[stripe-webhook] ignored event", {
      type: event.type,
      id: event.id,
      reason: result.reason,
    });
  } else if (result.status === "processed") {
    console.info("[stripe-webhook] granted tier", {
      type: event.type,
      id: event.id,
      userKey: result.userKey,
      tier: result.tier,
    });
  }

  // Always 200 for verified events — Stripe retries on non-2xx.
  return NextResponse.json({ received: true, result: result.status });
}
