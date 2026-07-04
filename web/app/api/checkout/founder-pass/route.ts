import { NextResponse } from "next/server";
import { userKeyFromRequest } from "@/lib/narrative-quota";
import {
  getFounderPassPriceId,
  getStripeClient,
  isStripeCheckoutEnabled,
} from "@/lib/stripe";

export async function GET() {
  return NextResponse.json({ configured: isStripeCheckoutEnabled() });
}

export async function POST(req: Request) {
  if (!isStripeCheckoutEnabled()) {
    return NextResponse.json(
      { error: "Stripe checkout is not configured" },
      { status: 503 },
    );
  }

  const origin = new URL(req.url).origin;
  const userKey = userKeyFromRequest(req);

  try {
    const stripe = getStripeClient();
    const session = await stripe.checkout.sessions.create({
      mode: "payment",
      line_items: [{ price: getFounderPassPriceId(), quantity: 1 }],
      success_url: `${origin}/shop?checkout=success`,
      cancel_url: `${origin}/shop?checkout=cancelled`,
      client_reference_id: userKey,
      metadata: { userKey, product: "founder_pass" },
    });

    if (!session.url) {
      return NextResponse.json(
        { error: "Stripe did not return a checkout URL" },
        { status: 502 },
      );
    }

    return NextResponse.json({ url: session.url, sessionId: session.id });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Checkout failed";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
