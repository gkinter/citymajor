import { NextResponse } from "next/server";
import { resolveTierFromRequest } from "@/lib/resolve-tier";
import { CreateSaveBodySchema, listSaves, createSave } from "@/lib/save-store";

export async function GET(req: Request) {
  const tier = resolveTierFromRequest(req);
  return NextResponse.json(listSaves(req, tier));
}

export async function POST(req: Request) {
  const tier = resolveTierFromRequest(req);

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: "Invalid JSON body" }, { status: 400 });
  }

  const parsed = CreateSaveBodySchema.safeParse(body);
  if (!parsed.success) {
    return NextResponse.json(
      { error: "Validation failed", details: parsed.error.flatten() },
      { status: 400 },
    );
  }

  const result = createSave(req, tier, parsed.data);
  if (!result.ok) {
    return NextResponse.json(
      {
        error: result.error,
        maxSlots: result.maxSlots,
        count: result.count,
        tier,
      },
      { status: result.status },
    );
  }

  return NextResponse.json(
    {
      save: result.save,
      count: result.count,
      maxSlots: result.maxSlots,
      tier,
    },
    { status: result.status },
  );
}
