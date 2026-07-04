import type Stripe from "stripe";
import { setStoredTier } from "@/lib/tier-store";

/** In-memory idempotency ledger — v1 stub; replace with durable store in v1.5. */
const processedEventIds = new Set<string>();

export type WebhookHandleResult =
  | { status: "processed"; userKey: string; tier: "founder_pass" }
  | { status: "duplicate" }
  | { status: "ignored"; reason: string };

export function wasStripeEventProcessed(eventId: string): boolean {
  return processedEventIds.has(eventId);
}

export function markStripeEventProcessed(eventId: string): void {
  processedEventIds.add(eventId);
}

export function _resetStripeWebhookStateForTests(): void {
  processedEventIds.clear();
}

function isValidUserKey(value: string | null | undefined): value is string {
  if (!value || value === "default-user") return false;
  // Signed user-id cookies use UUID v4; reject placeholder keys from misconfigured sessions.
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    value,
  );
}

function handleCheckoutSessionCompleted(
  session: Stripe.Checkout.Session,
): WebhookHandleResult {
  if (session.metadata?.product !== "founder_pass") {
    return { status: "ignored", reason: "not_founder_pass_product" };
  }

  const userKey = session.metadata.userKey ?? session.client_reference_id ?? null;
  if (!isValidUserKey(userKey)) {
    return { status: "ignored", reason: "missing_or_invalid_user_key" };
  }

  if (session.payment_status && session.payment_status !== "paid") {
    return { status: "ignored", reason: `payment_status_${session.payment_status}` };
  }

  setStoredTier(userKey, "founder_pass");
  return { status: "processed", userKey, tier: "founder_pass" };
}

/**
 * Dispatch verified Stripe webhook events.
 *
 * v1 handles `checkout.session.completed` for Founder Pass only.
 * Additional event types are acknowledged here as extension points for v1.5
 * (refunds, subscription lifecycle, chargebacks).
 */
export function handleStripeWebhookEvent(event: Stripe.Event): WebhookHandleResult {
  if (wasStripeEventProcessed(event.id)) {
    return { status: "duplicate" };
  }

  let result: WebhookHandleResult;

  switch (event.type) {
    case "checkout.session.completed":
      result = handleCheckoutSessionCompleted(event.data.object as Stripe.Checkout.Session);
      break;
    case "charge.refunded":
    case "customer.subscription.deleted":
      // v1.5: revoke founder_pass when payment is reversed.
      result = { status: "ignored", reason: `unimplemented_${event.type}` };
      break;
    default:
      result = { status: "ignored", reason: `unhandled_${event.type}` };
      break;
  }

  markStripeEventProcessed(event.id);
  return result;
}
