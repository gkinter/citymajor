import { NextResponse } from "next/server";
import { deleteSave, getSave } from "@/lib/save-store";
import { applyUserIdCookie, ensureUserId } from "@/lib/user-identity";

type RouteContext = { params: Promise<{ id: string }> };

export async function GET(req: Request, context: RouteContext) {
  const { userId, newCookie } = ensureUserId(req);
  const { id } = await context.params;

  const result = getSave(req, id, userId);
  if (!result.ok) {
    return applyUserIdCookie(
      NextResponse.json({ error: result.error }, { status: result.status }),
      newCookie,
    );
  }

  return applyUserIdCookie(
    NextResponse.json({
      save: result.save,
      downloadUrl: result.downloadUrl,
    }),
    newCookie,
  );
}

export async function DELETE(req: Request, context: RouteContext) {
  const { userId, newCookie } = ensureUserId(req);
  const { id } = await context.params;

  const result = await deleteSave(req, id, userId);
  if (!result.ok) {
    return applyUserIdCookie(
      NextResponse.json({ error: result.error }, { status: result.status }),
      newCookie,
    );
  }

  return applyUserIdCookie(new NextResponse(null, { status: 204 }), newCookie);
}
