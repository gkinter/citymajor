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
import { ensureUserId } from "@/lib/user-identity";

function withIdentityCookie(
  response: NextResponse,
  newCookie?: { name: string; value: string; maxAge: number },
): NextResponse {
  if (!newCookie) return response;
  response.cookies.set(newCookie.name, newCookie.value, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: newCookie.maxAge,
  });
  return response;
}

export async function GET(req: Request) {
  const { newCookie } = ensureUserId(req);
  const tier = resolveTierFromRequest(req);
  const userKey = userKeyFromRequest(req);
  const remaining = getNarrativeEventsRemaining(tier, userKey);
  const entitlements = entitlementsForTier(tier, remaining);
  return withIdentityCookie(
    NextResponse.json(EntitlementsSchema.parse(entitlements)),
    newCookie,
  );
}

/** Dev stub: set mock tier via JSON body; sets `citymajor_tier` cookie. */
export async function POST(req: Request) {
  const { newCookie } = ensureUserId(req);

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return withIdentityCookie(
      NextResponse.json({ error: "Invalid JSON body" }, { status: 400 }),
      newCookie,
    );
  }

  const parsed = SetTierBodySchema.safeParse(body);
  if (!parsed.success) {
    return withIdentityCookie(
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
  return withIdentityCookie(response, newCookie);
}
