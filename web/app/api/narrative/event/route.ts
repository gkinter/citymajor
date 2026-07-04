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

export async function POST(req: Request) {
  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: "Invalid JSON body" }, { status: 400 });
  }

  const parsed = NarrativeEventRequestSchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: "Validation failed", details: parsed.error.flatten() },
      { status: 400 },
    );
  }

  const tier = resolveTierFromRequest(req);
  const userKey = userKeyFromRequest(req);
  const remainingBefore = getNarrativeEventsRemaining(tier, userKey);

  if (remainingBefore === 0) {
    return NextResponse.json(
      {
        error: "Daily narrative quota exhausted",
        narrativeEventsRemaining: 0,
      },
      { status: 429 },
    );
  }

  const quota = consumeNarrativeEvent(userKey, tier);
  if (!quota.ok) {
    return NextResponse.json(
      {
        error: "Daily narrative quota exhausted",
        narrativeEventsRemaining: 0,
      },
      { status: 429 },
    );
  }

  const bucket = resolveBucket(parsed.data.bucket, parsed.data.context);
  const event = narrativeFromBucket(bucket);

  if (parsed.data.context?.cityName) {
    event.body = event.body.replace("the city", parsed.data.context.cityName);
  }

  const payload = NarrativeEventResponseSchema.parse(event);
  return NextResponse.json({
    ...payload,
    narrativeEventsRemaining: quota.remaining,
  });
}
