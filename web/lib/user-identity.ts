import { createHmac, randomBytes, randomUUID, timingSafeEqual } from "node:crypto";
import type { NextResponse } from "next/server";

/**
 * User identity — verified via an HMAC-signed HTTP-only cookie so clients
 * cannot spoof another user by setting a request header. This is the ONLY
 * server-side source of truth for who "the caller" is; do not trust any
 * client-controllable headers for identity.
 */

const COOKIE_NAME = "citymajor_uid";
const COOKIE_MAX_AGE_S = 60 * 60 * 24 * 365; // 1 year

/** Former dev placeholder — reject if copied into runtime env (Bugbot: committed secret in prod). */
const BLOCKED_SESSION_SECRETS = new Set([
  "dev-only-secret-do-not-ship__please_set_CITYMAJOR_SESSION_SECRET",
]);

let devEphemeralSecret: string | null = null;
let warnedAboutDevSecret = false;

function getEphemeralDevSecret(): string {
  if (!devEphemeralSecret) {
    devEphemeralSecret = randomBytes(32).toString("base64url");
  }
  return devEphemeralSecret;
}

function getSecret(): string {
  const secret = process.env.CITYMAJOR_SESSION_SECRET;
  if (secret) {
    if (BLOCKED_SESSION_SECRETS.has(secret)) {
      throw new Error(
        "CITYMAJOR_SESSION_SECRET must not be the committed dev placeholder — generate a unique secret of at least 32 chars",
      );
    }
    if (secret.length >= 32) return secret;
  }
  if (process.env.NODE_ENV === "production") {
    throw new Error(
      "CITYMAJOR_SESSION_SECRET must be set to a value of at least 32 chars in production",
    );
  }
  if (!warnedAboutDevSecret) {
    warnedAboutDevSecret = true;
    console.warn(
      "[user-identity] CITYMAJOR_SESSION_SECRET not set — using ephemeral dev secret (non-production only; cookies reset on restart)",
    );
  }
  return getEphemeralDevSecret();
}

function sign(userId: string): string {
  return createHmac("sha256", getSecret()).update(userId).digest("base64url");
}

function verify(userId: string, signature: string): boolean {
  const expected = Buffer.from(sign(userId));
  const actual = Buffer.from(signature);
  if (expected.length !== actual.length) return false;
  return timingSafeEqual(expected, actual);
}

function encode(userId: string): string {
  return `${userId}.${sign(userId)}`;
}

function decode(value: string): string | null {
  const idx = value.lastIndexOf(".");
  if (idx <= 0 || idx === value.length - 1) return null;
  const userId = value.slice(0, idx);
  const signature = value.slice(idx + 1);
  if (!verify(userId, signature)) return null;
  return userId;
}

function readCookie(cookieHeader: string | null, name: string): string | null {
  if (!cookieHeader) return null;
  for (const part of cookieHeader.split(/;\s*/)) {
    const eq = part.indexOf("=");
    if (eq === -1) continue;
    if (part.slice(0, eq).trim() !== name) continue;
    try {
      return decodeURIComponent(part.slice(eq + 1));
    } catch {
      return null;
    }
  }
  return null;
}

/**
 * Extract a verified user identity from a request. Returns null when no
 * signed cookie is present or the signature is invalid.
 *
 * Does NOT read from any client-controllable header (e.g. x-citymajor-user)
 * — those are trivially spoofable and were the source of a save-ownership
 * impersonation bug.
 */
export function getUserIdFromRequest(req: Request): string | null {
  const cookie = readCookie(req.headers.get("cookie"), COOKIE_NAME);
  if (!cookie) return null;
  return decode(cookie);
}

export type IssuedUserIdCookie = {
  name: string;
  value: string;
  maxAge: number;
};

/** Shared security attributes for the signed user-id cookie. */
export const USER_ID_COOKIE_OPTIONS = {
  httpOnly: true,
  sameSite: "lax" as const,
  secure: process.env.NODE_ENV === "production",
  path: "/",
};

export function applyUserIdCookie(
  response: NextResponse,
  cookie?: IssuedUserIdCookie,
): NextResponse {
  if (!cookie) return response;
  response.cookies.set(cookie.name, cookie.value, {
    ...USER_ID_COOKIE_OPTIONS,
    maxAge: cookie.maxAge,
  });
  return response;
}

/**
 * Ensure the caller has a verified user identity. If the cookie is missing
 * or tampered, mint a fresh random UUID and return the cookie descriptor so
 * the route handler can attach it via NextResponse.cookies.set.
 */
export function ensureUserId(req: Request): {
  userId: string;
  newCookie?: IssuedUserIdCookie;
} {
  const existing = getUserIdFromRequest(req);
  if (existing) return { userId: existing };

  const userId = randomUUID();
  return {
    userId,
    newCookie: {
      name: COOKIE_NAME,
      value: encode(userId),
      maxAge: COOKIE_MAX_AGE_S,
    },
  };
}

export function userIdCookieName(): string {
  return COOKIE_NAME;
}
