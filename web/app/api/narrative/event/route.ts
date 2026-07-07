import { NextResponse } from "next/server";
import {
  generateNarrativeWithLlm,
  isNarrativeLlmAllowedForTier,
  isNarrativeLlmConfigured,
} from "@/lib/narrative-prompt";
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
  personalizeNarrativeEvent,
  resolveBucket,
} from "@/lib/narrative-templates";
import { applyUserIdCookie, ensureUserId } from "@/lib/user-identity";

export async function POST(req: Request) {
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

  const parsed = NarrativeEventRequestSchema.safeParse(body);
  if (!parsed.success) {
    return applyUserIdCookie(
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
    return applyUserIdCookie(
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
    return applyUserIdCookie(
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
  let event = narrativeFromBucket(bucket);

  const cityName = parsed.data.context?.cityName;
  if (cityName) {
    event = personalizeNarrativeEvent(event, cityName);
  }

  if (isNarrativeLlmAllowedForTier(tier)) {
    try {
      const llmEvent = await generateNarrativeWithLlm({
        bucket,
        context: parsed.data.context,
        template: event,
      });
      if (llmEvent) {
        event = cityName ? personalizeNarrativeEvent(llmEvent, cityName) : llmEvent;
      }
    } catch (err) {
      console.error("[narrative/event] LLM failed, using template fallback:", err);
    }
  }

  const validated = NarrativeEventResponseSchema.safeParse(event);
  if (!validated.success) {
    console.error("[narrative/event] response validation failed:", validated.error.flatten());
    return applyUserIdCookie(
      NextResponse.json({ error: "Failed to build narrative response" }, { status: 500 }),
      newCookie,
    );
  }

  return applyUserIdCookie(
    NextResponse.json({
      ...validated.data,
      narrativeEventsRemaining: quota.remaining,
    }),
    newCookie,
  );
}
