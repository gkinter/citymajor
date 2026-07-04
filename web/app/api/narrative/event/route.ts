import { NextResponse } from "next/server";
import { resolveTierFromRequest } from "@/lib/resolve-tier";
import {
  consumeNarrativeEvent,
  getNarrativeEventsRemaining,
  userKeyFromRequest,
} from "@/lib/narrative-quota";
import {
  NarrativeEventRequestSchema,
  NarrativeEventResponseSchema,
  narrativeFromBucket,
  resolveBucket,
} from "@/lib/narrative-templates";
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

  const parsed = NarrativeEventRequestSchema.safeParse(body);
  if (!parsed.success) {
    return withIdentityCookie(
      NextResponse.json(
        { error: "Validation failed", details: parsed.error.flatten() },
        { status: 400 },
      ),
      newCookie,
    );
  }

  const tier = resolveTierFromRequest(req);
  const userKey = userKeyFromRequest(req);
  const remainingBefore = getNarrativeEventsRemaining(tier, userKey);

  if (remainingBefore === 0) {
    return withIdentityCookie(
      NextResponse.json(
        {
          error: "Daily narrative quota exhausted",
          narrativeEventsRemaining: 0,
        },
        { status: 429 },
      ),
      newCookie,
    );
  }

  const quota = consumeNarrativeEvent(userKey, tier);
  if (!quota.ok) {
    return withIdentityCookie(
      NextResponse.json(
        {
          error: "Daily narrative quota exhausted",
          narrativeEventsRemaining: 0,
        },
        { status: 429 },
      ),
      newCookie,
    );
  }

  const bucket = resolveBucket(parsed.data.bucket, parsed.data.context);
  const event = narrativeFromBucket(bucket);

  const cityName = parsed.data.context?.cityName;
  if (cityName) {
    // narrativeFromBucket templates use the literal placeholder "the city"
    // as the substitution anchor; case-insensitive replace so lowercase
    // and title-cased variants both get personalized.
    event.body = event.body.replace(/the city/gi, cityName);
  }

  const payload = NarrativeEventResponseSchema.parse(event);
  return withIdentityCookie(
    NextResponse.json({
      ...payload,
      narrativeEventsRemaining: quota.remaining,
    }),
    newCookie,
  );
}
