import { NextResponse } from "next/server";
import { resolveTierFromRequest } from "@/lib/resolve-tier";
import { CreateSaveBodySchema, listSaves, createSave } from "@/lib/save-store";
import { applyUserIdCookie, ensureUserId } from "@/lib/user-identity";

export async function GET(req: Request) {
  let newCookie: ReturnType<typeof ensureUserId>["newCookie"];
  try {
    ({ newCookie } = ensureUserId(req));
  } catch (err) {
    console.error("[api/saves] ensureUserId failed:", err);
    return NextResponse.json(
      { error: "Session configuration error" },
      { status: 500 },
    );
  }

  const tier = resolveTierFromRequest(req);
  try {
    return applyUserIdCookie(
      NextResponse.json(listSaves(req, tier)),
      newCookie,
    );
  } catch (err) {
    console.error("[api/saves] listSaves failed:", err);
    return applyUserIdCookie(
      NextResponse.json({ error: "Save store unavailable" }, { status: 500 }),
      newCookie,
    );
  }
}

export async function POST(req: Request) {
  let newCookie: ReturnType<typeof ensureUserId>["newCookie"];
  try {
    ({ newCookie } = ensureUserId(req));
  } catch (err) {
    console.error("[api/saves] ensureUserId failed:", err);
    return NextResponse.json(
      { error: "Session configuration error" },
      { status: 500 },
    );
  }

  const tier = resolveTierFromRequest(req);

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return applyUserIdCookie(
      NextResponse.json({ error: "Invalid JSON body" }, { status: 400 }),
      newCookie,
    );
  }

  const parsed = CreateSaveBodySchema.safeParse(body);
  if (!parsed.success) {
    return applyUserIdCookie(
      NextResponse.json(
        { error: "Validation failed", details: parsed.error.flatten() },
        { status: 400 },
      ),
      newCookie,
    );
  }

  const result = await createSave(req, tier, parsed.data);
  if (!result.ok) {
    return applyUserIdCookie(
      NextResponse.json(
        result.status === 500
          ? { error: result.error }
          : {
              error: result.error,
              maxSlots: result.maxSlots,
              count: result.count,
              tier,
            },
        { status: result.status },
      ),
      newCookie,
    );
  }

  return applyUserIdCookie(
    NextResponse.json(
      {
        save: result.save,
        count: result.count,
        maxSlots: result.maxSlots,
        tier,
      },
      { status: result.status },
    ),
    newCookie,
  );
}
