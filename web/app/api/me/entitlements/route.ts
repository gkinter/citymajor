import { NextResponse } from "next/server";
import {
  EntitlementsSchema,
  SetTierBodySchema,
  entitlementsForTier,
} from "@/lib/entitlements";
import { resolveTierFromRequest } from "@/lib/resolve-tier";
import {
  getNarrativeEventsRemaining,
  userKeyFromRequest,
} from "@/lib/narrative-quota";
import { applyUserIdCookie, ensureUserId } from "@/lib/user-identity";

export async function GET(req: Request) {
  const { newCookie } = ensureUserId(req);
  const tier = resolveTierFromRequest(req);
  const userKey = userKeyFromRequest(req);
  const remaining = getNarrativeEventsRemaining(tier, userKey);
  const entitlements = entitlementsForTier(tier, remaining);
  return applyUserIdCookie(
    NextResponse.json(EntitlementsSchema.parse(entitlements)),
    newCookie,
  );
}

/**
 * Dev stub: set mock tier via JSON body; sets `citymajor_tier` cookie.
 *
 * SECURITY: gated to non-production only — leaving this handler exposed in
 * production would let any client self-grant `founder_pass` entitlements by
 * writing the `citymajor_tier` cookie without payment.
 */
export async function POST(req: Request) {
  const isProduction = process.env.NODE_ENV === "production";
  if (isProduction) {
    return NextResponse.json({ error: "Not found" }, { status: 404 });
  }

  const { newCookie } = ensureUserId(req);

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return applyUserIdCookie(
      NextResponse.json({ error: "Invalid JSON body" }, { status: 400 }),
      newCookie,
    );
  }

  const parsed = SetTierBodySchema.safeParse(body);
  if (!parsed.success) {
    return applyUserIdCookie(
      NextResponse.json(
        { error: "Validation failed", details: parsed.error.flatten() },
        { status: 400 },
      ),
      newCookie,
    );
  }

  const userKey = userKeyFromRequest(req);
  const remaining = getNarrativeEventsRemaining(parsed.data.tier, userKey);
  const entitlements = entitlementsForTier(parsed.data.tier, remaining);
  const response = NextResponse.json(EntitlementsSchema.parse(entitlements));
  response.cookies.set("citymajor_tier", parsed.data.tier, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
  });
  return applyUserIdCookie(response, newCookie);
}
