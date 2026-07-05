import { NextResponse } from "next/server";
import { entitlementsForTier } from "@/lib/entitlements";
import { generateNarrativeWithLlm, isNarrativeLlmConfigured } from "@/lib/narrative-prompt";
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

  const { llmEnabled } = entitlementsForTier(tier);
  if (llmEnabled && isNarrativeLlmConfigured()) {
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

  const payload = NarrativeEventResponseSchema.parse(event);
  return applyUserIdCookie(
    NextResponse.json({
      ...payload,
      narrativeEventsRemaining: quota.remaining,
    }),
    newCookie,
  );
}
