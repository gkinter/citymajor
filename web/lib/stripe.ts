import Stripe from "stripe";

let stripeClient: Stripe | null = null;

/** Runtime env vars required for `POST /api/checkout/founder-pass`. */
export const STRIPE_CHECKOUT_ENV_KEYS = [
  "STRIPE_SECRET_KEY",
  "STRIPE_FOUNDER_PASS_PRICE_ID",
] as const;

/** Runtime env vars required for `POST /api/webhooks/stripe`. */
export const STRIPE_WEBHOOK_ENV_KEYS = [
  "STRIPE_SECRET_KEY",
  "STRIPE_WEBHOOK_SECRET",
] as const;

export type StripeCheckoutEnvKey = (typeof STRIPE_CHECKOUT_ENV_KEYS)[number];
export type StripeWebhookEnvKey = (typeof STRIPE_WEBHOOK_ENV_KEYS)[number];

function isEnvSet(name: string): boolean {
  return Boolean(process.env[name]?.trim());
}

export function getMissingStripeCheckoutEnv(): StripeCheckoutEnvKey[] {
  return STRIPE_CHECKOUT_ENV_KEYS.filter((key) => !isEnvSet(key));
}

export function getMissingStripeWebhookEnv(): StripeWebhookEnvKey[] {
  return STRIPE_WEBHOOK_ENV_KEYS.filter((key) => !isEnvSet(key));
}

export function isStripeCheckoutEnabled(): boolean {
  return getMissingStripeCheckoutEnv().length === 0;
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

export function getStripePublishableKey(): string | null {
  return process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY?.trim() || null;
}

export function isStripeWebhookEnabled(): boolean {
  return getMissingStripeWebhookEnv().length === 0;
}

export type StripeServiceUnavailableBody = {
  error: string;
  missing: readonly string[];
  configured: false;
};

export function stripeCheckoutUnavailableBody(): StripeServiceUnavailableBody {
  const missing = getMissingStripeCheckoutEnv();
  return {
    error: `Stripe checkout is not configured (missing: ${missing.join(", ")})`,
    missing,
    configured: false,
  };
}

export function stripeWebhookUnavailableBody(): StripeServiceUnavailableBody {
  const missing = getMissingStripeWebhookEnv();
  return {
    error: `Stripe webhook is not configured (missing: ${missing.join(", ")})`,
    missing,
    configured: false,
  };
}
