import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import type { Tier } from "@/lib/entitlements";
import { entitlementsForTier } from "@/lib/entitlements";

const QuotaFileSchema = z.record(
  z.string(),
  z.object({
    date: z.string(),
    count: z.number().int().nonnegative(),
  }),
);

type QuotaStore = z.infer<typeof QuotaFileSchema>;

const DATA_DIR = join(process.cwd(), ".data");
const QUOTA_FILE = join(DATA_DIR, "narrative-quota.json");

let memoryStore: QuotaStore = {};

function todayKey(): string {
  return new Date().toISOString().slice(0, 10);
}

function loadStore(): QuotaStore {
  if (Object.keys(memoryStore).length > 0) return memoryStore;

  try {
    if (existsSync(QUOTA_FILE)) {
      const raw = JSON.parse(readFileSync(QUOTA_FILE, "utf8")) as unknown;
      memoryStore = QuotaFileSchema.parse(raw);
      return memoryStore;
    }
  } catch {
    memoryStore = {};
  }
  return memoryStore;
}

function persistStore(store: QuotaStore): void {
  memoryStore = store;
  try {
    if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true });
    writeFileSync(QUOTA_FILE, JSON.stringify(store, null, 2), "utf8");
  } catch {
    // Dev stub — in-memory only if filesystem is read-only.
  }
}

export function userKeyFromRequest(req: Request): string {
  return req.headers.get("x-citymajor-user") ?? "default-user";
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
