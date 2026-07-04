import { NextResponse } from "next/server";
import { resolveTierFromRequest } from "@/lib/resolve-tier";
import { CreateSaveBodySchema, listSaves, createSave } from "@/lib/save-store";
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
  // Even when we mint a fresh cookie, the request itself has no verified
  // identity yet, so listSaves returns the "anonymous" bucket for this
  // first response and subsequent requests will use the signed cookie.
  const tier = resolveTierFromRequest(req);
  return withIdentityCookie(NextResponse.json(listSaves(req, tier)), newCookie);
}

export async function POST(req: Request) {
  const { newCookie } = ensureUserId(req);
  const tier = resolveTierFromRequest(req);

  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return withIdentityCookie(
      NextResponse.json({ error: "Invalid JSON body" }, { status: 400 }),
      newCookie,
    );
  }

  const parsed = CreateSaveBodySchema.safeParse(body);
  if (!parsed.success) {
    return withIdentityCookie(
      NextResponse.json(
        { error: "Validation failed", details: parsed.error.flatten() },
        { status: 400 },
      ),
      newCookie,
    );
  }

  const result = await createSave(req, tier, parsed.data);
  if (!result.ok) {
    return withIdentityCookie(
      NextResponse.json(
        {
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

  return withIdentityCookie(
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
