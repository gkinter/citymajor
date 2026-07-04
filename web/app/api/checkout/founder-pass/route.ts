import { NextResponse } from "next/server";
import { applyUserIdCookie, ensureUserId } from "@/lib/user-identity";
import {
  getFounderPassPriceId,
  getStripeClient,
  getStripePublishableKey,
  isStripeCheckoutEnabled,
} from "@/lib/stripe";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET() {
  return NextResponse.json({
    configured: isStripeCheckoutEnabled(),
    publishableKey: getStripePublishableKey(),
  });
}

export async function POST(req: Request) {
  if (!isStripeCheckoutEnabled()) {
    return NextResponse.json(
      { error: "Stripe checkout is not configured" },
      { status: 503 },
    );
  }

  const { userId, newCookie } = ensureUserId(req);
  const origin = new URL(req.url).origin;

  try {
    const stripe = getStripeClient();
    const session = await stripe.checkout.sessions.create({
      mode: "payment",
      line_items: [{ price: getFounderPassPriceId(), quantity: 1 }],
      success_url: `${origin}/shop?checkout=success`,
      cancel_url: `${origin}/shop?checkout=cancelled`,
      client_reference_id: userId,
      metadata: { userKey: userId, product: "founder_pass" },
    });

    if (!session.url) {
      return applyUserIdCookie(
        NextResponse.json(
          { error: "Stripe did not return a checkout URL" },
          { status: 502 },
        ),
        newCookie,
      );
    }

    return applyUserIdCookie(
      NextResponse.json({ url: session.url, sessionId: session.id }),
      newCookie,
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Checkout failed";
    return applyUserIdCookie(
      NextResponse.json({ error: message }, { status: 500 }),
      newCookie,
    );
  }
}
