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

export async function GET(req: Request) {
  const tier = resolveTierFromRequest(req);
  const userKey = userKeyFromRequest(req);
  const remaining = getNarrativeEventsRemaining(tier, userKey);
  const entitlements = entitlementsForTier(tier, remaining);
  return NextResponse.json(EntitlementsSchema.parse(entitlements));
}

/** Dev stub: set mock tier via JSON body; sets `citymajor_tier` cookie. */
export async function POST(req: Request) {
  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: "Invalid JSON body" }, { status: 400 });
  }

  const parsed = SetTierBodySchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: "Validation failed", details: parsed.error.flatten() },
      { status: 400 },
    );
  }

  const userKey = userKeyFromRequest(req);
  const remaining = getNarrativeEventsRemaining(parsed.data.tier, userKey);
  const entitlements = entitlementsForTier(parsed.data.tier, remaining);
  const response = NextResponse.json(EntitlementsSchema.parse(entitlements));
  response.cookies.set("citymajor_tier", parsed.data.tier, {
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
  });
  return response;
}
