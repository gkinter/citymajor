import { NextResponse } from "next/server";
import {
  NarrativeEventRequestSchema,
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

  const bucket = resolveBucket(parsed.data.bucket, parsed.data.context);
  const event = narrativeFromBucket(bucket);

  if (parsed.data.context?.cityName) {
    event.body = event.body.replace("the city", parsed.data.context.cityName);
  }

  return NextResponse.json(event);
}
