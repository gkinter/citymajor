import Stripe from "stripe";

let stripeClient: Stripe | null = null;

export function isStripeCheckoutEnabled(): boolean {
  return Boolean(
    process.env.STRIPE_SECRET_KEY?.trim() &&
      process.env.STRIPE_FOUNDER_PASS_PRICE_ID?.trim(),
  );
}

export function getStripeClient(): Stripe {
  const secretKey = process.env.STRIPE_SECRET_KEY?.trim();
  if (!secretKey) {
    throw new Error("STRIPE_SECRET_KEY is not configured");
  }
  if (!stripeClient) {
    stripeClient = new Stripe(secretKey);
  }
  return stripeClient;
}

export function getFounderPassPriceId(): string {
  const priceId = process.env.STRIPE_FOUNDER_PASS_PRICE_ID?.trim();
  if (!priceId) {
    throw new Error("STRIPE_FOUNDER_PASS_PRICE_ID is not configured");
  }
  return priceId;
}

export function getStripeWebhookSecret(): string | null {
  return process.env.STRIPE_WEBHOOK_SECRET?.trim() || null;
}
