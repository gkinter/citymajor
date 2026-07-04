import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import type { Tier } from "@/lib/entitlements";
import { entitlementsForTier } from "@/lib/entitlements";
import { ensureDataDir, resolveDataDir } from "@/lib/data-dir";
import { getUserIdFromRequest } from "@/lib/user-identity";

const QuotaFileSchema = z.record(
  z.string(),
  z.object({
    date: z.string(),
    count: z.number().int().nonnegative(),
  }),
);

type QuotaStore = z.infer<typeof QuotaFileSchema>;

function quotaFilePath(): string {
  return join(resolveDataDir(), "narrative-quota.json");
}

let memoryStore: QuotaStore = {};

function todayKey(): string {
  return new Date().toISOString().slice(0, 10);
}

function loadStore(): QuotaStore {
  if (Object.keys(memoryStore).length > 0) return memoryStore;

  const quotaFile = quotaFilePath();
  try {
    if (existsSync(quotaFile)) {
      const raw = JSON.parse(readFileSync(quotaFile, "utf8")) as unknown;
      memoryStore = QuotaFileSchema.parse(raw);
      return memoryStore;
    }
  } catch (err) {
    console.error("[narrative-quota] loadStore failed, using empty store:", quotaFile, err);
    memoryStore = {};
  }
  return memoryStore;
}

function persistStore(store: QuotaStore): void {
  memoryStore = store;
  const quotaFile = quotaFilePath();
  try {
    ensureDataDir();
    writeFileSync(quotaFile, JSON.stringify(store, null, 2), "utf8");
  } catch (err) {
    console.error("[narrative-quota] persistStore failed (in-memory only):", quotaFile, err);
  }
}

/**
 * Server-side identity — pulled ONLY from the signed HTTP-only identity
 * cookie so clients cannot impersonate another user's quota by setting
 * request headers.
 */
export function userKeyFromRequest(req: Request): string {
  return getUserIdFromRequest(req) ?? "anonymous";
}

function usageForUser(store: QuotaStore, userKey: string): number {
  const today = todayKey();
  const entry = store[userKey];
  if (!entry || entry.date !== today) return 0;
  return entry.count;
}

export function getNarrativeEventsRemaining(tier: Tier, userKey: string): number {
  const max = entitlementsForTier(tier).maxNarrativeEventsPerDay;
  if (max === Number.MAX_SAFE_INTEGER) return Number.MAX_SAFE_INTEGER;

  const store = loadStore();
  const used = usageForUser(store, userKey);
  return Math.max(0, max - used);
}

export function consumeNarrativeEvent(userKey: string, tier: Tier): {
  ok: boolean;
  remaining: number;
} {
  const max = entitlementsForTier(tier).maxNarrativeEventsPerDay;
  if (max === Number.MAX_SAFE_INTEGER) {
    return { ok: true, remaining: Number.MAX_SAFE_INTEGER };
  }

  const store = loadStore();
  const today = todayKey();
  const entry = store[userKey];
  const used = entry?.date === today ? entry.count : 0;

  if (used >= max) {
    return { ok: false, remaining: 0 };
  }

  const next = { date: today, count: used + 1 };
  persistStore({ ...store, [userKey]: next });
  return { ok: true, remaining: max - next.count };
}

/** Test helper — reset store between runs. */
export function _resetNarrativeQuotaForTests(): void {
  memoryStore = {};
}
