import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import type { Tier } from "@/lib/entitlements";
import { entitlementsForTier } from "@/lib/entitlements";
import { getUserIdFromRequest } from "@/lib/user-identity";

export const SaveSlotSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(64),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  /** Opaque sim snapshot blob — stub only for v1. */
  payload: z.record(z.string(), z.unknown()).default({}),
});
export type SaveSlot = z.infer<typeof SaveSlotSchema>;

export const CreateSaveBodySchema = z.object({
  name: z.string().min(1).max(64),
  payload: z.record(z.string(), z.unknown()).optional(),
});

export const SaveListResponseSchema = z.object({
  saves: z.array(SaveSlotSchema),
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
});

const DATA_DIR = join(process.cwd(), ".data");
const SAVES_FILE = join(DATA_DIR, "saves.json");
const SAVES_TMP_FILE = `${SAVES_FILE}.tmp`;

const SavesFileSchema = z.record(z.string(), z.array(SaveSlotSchema));

type Store = z.infer<typeof SavesFileSchema>;

/**
 * In-memory cache — used only for reads outside the write path. Every
 * write reloads fresh from disk under a mutex to avoid TOCTOU
 * (see writeLock below), so this cache never causes lost updates.
 */
let memoryStore: Store = {};
let memoryStoreLoaded = false;

function readFromDisk(): Store {
  if (!existsSync(SAVES_FILE)) return {};
  let raw: unknown;
  try {
    raw = JSON.parse(readFileSync(SAVES_FILE, "utf8"));
  } catch (err) {
    throw new Error("saves.json is corrupt or unreadable", { cause: err });
  }
  try {
    return SavesFileSchema.parse(raw);
  } catch (err) {
    throw new Error("saves.json failed schema validation", { cause: err });
  }
}

function loadStore(): Store {
  if (memoryStoreLoaded) return memoryStore;
  memoryStore = readFromDisk();
  memoryStoreLoaded = true;
  return memoryStore;
}

function persistStore(store: Store): void {
  memoryStore = store;
  memoryStoreLoaded = true;
  if (!existsSync(DATA_DIR)) mkdirSync(DATA_DIR, { recursive: true });
  // Atomic write: stage to *.tmp then rename so a partial write or
  // concurrent reader never sees a corrupted saves.json.
  writeFileSync(SAVES_TMP_FILE, JSON.stringify(store, null, 2), "utf8");
  renameSync(SAVES_TMP_FILE, SAVES_FILE);
}

/**
 * Server-side identity — pulled ONLY from the HMAC-signed HTTP-only
 * `citymajor_uid` cookie. Client-controllable headers are intentionally
 * ignored to prevent trivial save-ownership impersonation.
 */
function userKey(req: Request): string {
  return getUserIdFromRequest(req) ?? "anonymous";
}

export function listSaves(req: Request, tier: Tier) {
  const store = loadStore();
  const key = userKey(req);
  const saves = store[key] ?? [];
  const maxSlots = entitlementsForTier(tier).maxSaveSlots;

  return SaveListResponseSchema.parse({
    saves,
    count: saves.length,
    maxSlots,
    tier,
  });
}

/**
 * In-process write mutex — serializes all mutating operations so the
 * load → check → write sequence stays atomic. On each turn we re-read
 * from disk to also protect against another process having written in
 * between (belt-and-braces alongside the atomic rename).
 */
let writeLock: Promise<unknown> = Promise.resolve();

function withWriteLock<T>(fn: () => T): Promise<T> {
  const prev = writeLock;
  const next = prev.catch(() => undefined).then(() => fn());
  writeLock = next.catch(() => undefined);
  return next;
}

export function createSave(
  req: Request,
  tier: Tier,
  body: z.infer<typeof CreateSaveBodySchema>,
): Promise<
  | {
      ok: true;
      status: 201;
      save: SaveSlot;
      count: number;
      maxSlots: number;
    }
  | {
      ok: false;
      status: 403;
      error: string;
      maxSlots: number;
      count: number;
    }
  | {
      ok: false;
      status: 500;
      error: string;
    }
> {
  return withWriteLock(() => {
    try {
      // Reload fresh from disk inside the lock: the memory cache may be
      // stale if another process wrote to saves.json since we last read.
      memoryStore = readFromDisk();
      memoryStoreLoaded = true;

      const store = memoryStore;
      const key = userKey(req);
      const saves = [...(store[key] ?? [])];
      const maxSlots = entitlementsForTier(tier).maxSaveSlots;

      if (saves.length >= maxSlots) {
        return {
          ok: false as const,
          status: 403 as const,
          error: `Save slot limit reached (${maxSlots} for ${tier}). Delete a save or upgrade to Founder Pass.`,
          maxSlots,
          count: saves.length,
        };
      }

      const now = new Date().toISOString();
      const slot: SaveSlot = {
        id: crypto.randomUUID(),
        name: body.name,
        createdAt: now,
        updatedAt: now,
        payload: body.payload ?? {},
      };

      saves.push(SaveSlotSchema.parse(slot));
      persistStore({ ...store, [key]: saves });

      return {
        ok: true as const,
        status: 201 as const,
        save: slot,
        count: saves.length,
        maxSlots,
      };
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Save store operation failed";
      return {
        ok: false as const,
        status: 500 as const,
        error: message,
      };
    }
  });
}

/** Test helper — reset store between runs. */
export function _resetSaveStoreForTests(): void {
  memoryStore = {};
  memoryStoreLoaded = false;
  writeLock = Promise.resolve();
}
